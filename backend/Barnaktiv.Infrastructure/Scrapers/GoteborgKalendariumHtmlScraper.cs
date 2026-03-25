using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;

namespace Barnaktiv.Infrastructure.Scrapers;

public sealed class GoteborgKalendariumHtmlScraper(HttpClient httpClient) : IActivityScraper
{
    private const int AbsoluteMaxPages = 200;
    private const int DetailRequestConcurrency = 6;

    private static readonly Uri BaseUri = new("https://goteborg.se");
    private static readonly Regex ActivityLinkRegex = new(
        "<a class=\"c-card__title-link\"[^>]*href=\"(?<href>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PageLinkRegex = new(
        @"[?&]page=(?<page>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex JsonLdRegex = new(
        "<script type=\"application/ld\\+json\">\\s*(?<json>.*?)\\s*</script>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CostRegex = new(
        "<dt[^>]*>Kostnad</dt><dd[^>]*>(?<value>.*?)</dd>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex DecimalRegex = new(
        @"(?<amount>\d+(?:[.,]\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AgeRangeRegex = new(
        @"(?<!\d)(?<from>\d{1,2})\s*(?:-|\u2013|\u2014|till|to)\s*(?<to>\d{1,2})\s*\u00E5r(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AgeFromRegex = new(
        @"(?:fr\u00E5n|from)\s*(?<from>\d{1,2})\s*\u00E5r|(?<!\d)(?<from>\d{1,2})\s*\+\s*\u00E5r(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AgeSingleRegex = new(
        @"(?:f\u00F6r\s+(?:dig|barn(?:en)?|ungdom(?:ar)?))\s*(?<age>\d{1,2})\s*\u00E5r|(?<!\d)(?<age>\d{1,2})-\u00E5r(?:ing|ingar)(?!\w)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public string Kind => "goteborg-kalendarium-html-v1";

    public async Task<ScrapeResult> ScrapeAsync(
        ConfiguredIngestionSource source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.EndpointUrl))
        {
            return new ScrapeResult([], [$"Source '{source.SourceKey}' has no endpoint URL."]);
        }

        var errors = new ConcurrentBag<string>();
        var items = new ConcurrentBag<ScrapedActivityItem>();
        var detailUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configuredMaxPages = source.MaxPages.GetValueOrDefault();
        var pageCount = configuredMaxPages > 0
            ? Math.Clamp(configuredMaxPages, 1, AbsoluteMaxPages)
            : AbsoluteMaxPages;

        for (var page = 0; page < pageCount; page++)
        {
            var pageUrl = BuildPageUrl(source.EndpointUrl, page);
            string listHtml;

            try
            {
                listHtml = await httpClient.GetStringAsync(pageUrl, cancellationToken);
            }
            catch (Exception exception)
            {
                errors.Add($"Failed to fetch list page {page + 1}: {exception.Message}");
                continue;
            }

            if (page == 0 && source.MaxPages is null)
            {
                pageCount = DetectPageCount(listHtml) ?? pageCount;
            }

            var matches = ActivityLinkRegex.Matches(listHtml);

            if (matches.Count == 0)
            {
                if (page == 0)
                {
                    errors.Add("No activity links were found on the Goteborg kalendarium page.");
                }

                break;
            }

            var linksAddedOnPage = 0;

            foreach (Match match in matches)
            {
                var relativeHref = WebUtility.HtmlDecode(match.Groups["href"].Value);

                if (string.IsNullOrWhiteSpace(relativeHref))
                {
                    continue;
                }

                if (detailUrls.Add(new Uri(BaseUri, relativeHref).ToString()))
                {
                    linksAddedOnPage++;
                }
            }

            if (linksAddedOnPage == 0)
            {
                break;
            }
        }

        await Parallel.ForEachAsync(
            detailUrls,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = DetailRequestConcurrency,
                CancellationToken = cancellationToken
            },
            async (detailUrl, ct) =>
            {
                try
                {
                    var detailHtml = await httpClient.GetStringAsync(detailUrl, ct);

                    if (!TryParseDetail(detailUrl, detailHtml, out var item, out var error))
                    {
                        errors.Add(error);
                        return;
                    }

                    items.Add(item!);
                }
                catch (Exception exception)
                {
                    errors.Add($"Failed to fetch detail page '{detailUrl}': {exception.Message}");
                }
            });

        return new ScrapeResult(items.ToList(), errors.ToList());
    }

    private static string BuildPageUrl(string endpointUrl, int page)
    {
        var pageParameter = $"page={page}";

        if (Regex.IsMatch(endpointUrl, @"([?&])page=\d+", RegexOptions.IgnoreCase))
        {
            return Regex.Replace(
                endpointUrl,
                @"([?&])page=\d+",
                $"$1{pageParameter}",
                RegexOptions.IgnoreCase);
        }

        return endpointUrl.Contains('?')
            ? $"{endpointUrl}&{pageParameter}"
            : $"{endpointUrl}?{pageParameter}";
    }

    private static bool TryParseDetail(
        string detailUrl,
        string detailHtml,
        out ScrapedActivityItem? item,
        out string error)
    {
        var activityIdMatch = Regex.Match(
            detailUrl,
            @"[?&]activityId=(?<id>[0-9a-fA-F\-]+)",
            RegexOptions.CultureInvariant);

        if (!activityIdMatch.Success)
        {
            item = null;
            error = $"Detail URL '{detailUrl}' has no activityId.";
            return false;
        }

        var jsonLdMatch = JsonLdRegex.Match(detailHtml);

        if (!jsonLdMatch.Success)
        {
            item = null;
            error = $"Detail page '{detailUrl}' has no JSON-LD event payload.";
            return false;
        }

        using var document = JsonDocument.Parse(jsonLdMatch.Groups["json"].Value);
        var root = document.RootElement;

        var title = GetString(root, "name");
        var startDateText = GetString(root, "startDate");

        if (string.IsNullOrWhiteSpace(title) || !DateTime.TryParse(startDateText, out var date))
        {
            item = null;
            error = $"Detail page '{detailUrl}' is missing a valid title or startDate.";
            return false;
        }

        var description = StripHtml(GetString(root, "description") ?? string.Empty);
        var imageUrl = GetString(root, "image") ?? string.Empty;
        var organizer = GetNestedString(root, "organizer", "name") ?? string.Empty;
        var location = GetNestedString(root, "location", "address")
            ?? GetNestedString(root, "geo", "address")
            ?? string.Empty;
        var category = GetFirstCategory(root) ?? "Kalendarium";
        var audiences = GetPropertyValues(root, "M\u00E5lgrupper");
        var (ageFrom, ageTo) = InferAgeRange(title, description, audiences);
        var isFree = root.TryGetProperty("isAccessibleForFree", out var isFreeElement) &&
                     isFreeElement.ValueKind == JsonValueKind.True;
        var price = TryExtractPrice(detailHtml, isFree);

        item = new ScrapedActivityItem(
            activityIdMatch.Groups["id"].Value,
            title.Trim(),
            description,
            organizer.Trim(),
            location.Trim(),
            "Goteborg",
            ageFrom,
            ageTo,
            category.Trim(),
            date,
            price,
            detailUrl,
            imageUrl.Trim(),
            detailHtml);

        error = string.Empty;
        return true;
    }

    private static int? DetectPageCount(string listHtml)
    {
        var pageIndexes = PageLinkRegex.Matches(listHtml)
            .Select(match => int.TryParse(match.Groups["page"].Value, out var pageIndex)
                ? pageIndex
                : (int?)null)
            .Where(pageIndex => pageIndex.HasValue)
            .Select(pageIndex => pageIndex!.Value)
            .ToList();

        if (pageIndexes.Count == 0)
        {
            return null;
        }

        return Math.Clamp(pageIndexes.Max() + 1, 1, AbsoluteMaxPages);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static string? GetNestedString(JsonElement element, string parentName, string childName)
    {
        if (!element.TryGetProperty(parentName, out var parent))
        {
            return null;
        }

        return parent.ValueKind == JsonValueKind.Object
            ? GetString(parent, childName)
            : null;
    }

    private static string? GetFirstCategory(JsonElement element)
    {
        return GetPropertyValues(element, "Kategorier").FirstOrDefault();
    }

    private static IReadOnlyList<string> GetPropertyValues(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty("additionalProperty", out var properties) ||
            properties.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        foreach (var property in properties.EnumerateArray())
        {
            if (!string.Equals(
                    GetString(property, "name"),
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!property.TryGetProperty("value", out var value))
            {
                return Array.Empty<string>();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Select(entry => entry.GetString())
                    .OfType<string>()
                    .Where(entry => !string.IsNullOrWhiteSpace(entry))
                    .ToList();
            }

            return value.ValueKind == JsonValueKind.String &&
                   !string.IsNullOrWhiteSpace(value.GetString())
                ? [value.GetString()!]
                : Array.Empty<string>();
        }

        return Array.Empty<string>();
    }

    private static (int AgeFrom, int AgeTo) InferAgeRange(
        string title,
        string description,
        IReadOnlyList<string> audiences)
    {
        var searchText = $"{title} {description}".Trim();

        if (TryExtractNumericAgeRange(searchText, out var exactAgeFrom, out var exactAgeTo))
        {
            return (exactAgeFrom, exactAgeTo);
        }

        var normalizedText = searchText.ToLowerInvariant();

        if (normalizedText.Contains("baby", StringComparison.Ordinal) ||
            normalizedText.Contains("bebis", StringComparison.Ordinal))
        {
            return (0, 1);
        }

        if (normalizedText.Contains("sm\u00E5barn", StringComparison.Ordinal) ||
            normalizedText.Contains("sm\u00E5 barn", StringComparison.Ordinal))
        {
            return (0, 3);
        }

        if (normalizedText.Contains("f\u00F6rskol", StringComparison.Ordinal))
        {
            return (3, 5);
        }

        if (normalizedText.Contains("familj", StringComparison.Ordinal))
        {
            return (0, 12);
        }

        if (normalizedText.Contains("barn och unga", StringComparison.Ordinal) ||
            normalizedText.Contains("barn & unga", StringComparison.Ordinal))
        {
            return (0, 25);
        }

        var normalizedAudiences = audiences
            .Select(audience => audience.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        if (normalizedAudiences.Contains("barn") && normalizedAudiences.Contains("ungdom"))
        {
            return (0, 25);
        }

        if (normalizedAudiences.Contains("ungdom"))
        {
            return (13, 25);
        }

        if (normalizedAudiences.Contains("barn"))
        {
            return (0, 12);
        }

        if (normalizedText.Contains("ungdom", StringComparison.Ordinal) ||
            normalizedText.Contains("unga", StringComparison.Ordinal) ||
            normalizedText.Contains("ton\u00E5r", StringComparison.Ordinal))
        {
            return (13, 25);
        }

        if (normalizedText.Contains("barn", StringComparison.Ordinal))
        {
            return (0, 12);
        }

        return (0, 0);
    }

    private static bool TryExtractNumericAgeRange(
        string input,
        out int ageFrom,
        out int ageTo)
    {
        var rangeMatch = AgeRangeRegex.Match(input);

        if (rangeMatch.Success &&
            int.TryParse(rangeMatch.Groups["from"].Value, out ageFrom) &&
            int.TryParse(rangeMatch.Groups["to"].Value, out ageTo))
        {
            return NormalizeAgeRange(ref ageFrom, ref ageTo);
        }

        var openEndedMatch = AgeFromRegex.Match(input);

        if (openEndedMatch.Success &&
            int.TryParse(openEndedMatch.Groups["from"].Value, out ageFrom))
        {
            ageTo = 99;
            return NormalizeAgeRange(ref ageFrom, ref ageTo);
        }

        var singleMatch = AgeSingleRegex.Match(input);

        if (singleMatch.Success &&
            int.TryParse(singleMatch.Groups["age"].Value, out ageFrom))
        {
            ageTo = ageFrom;
            return NormalizeAgeRange(ref ageFrom, ref ageTo);
        }

        ageFrom = 0;
        ageTo = 0;
        return false;
    }

    private static bool NormalizeAgeRange(ref int ageFrom, ref int ageTo)
    {
        ageFrom = Math.Clamp(ageFrom, 0, 99);
        ageTo = Math.Clamp(ageTo, 0, 99);

        if (ageFrom == 0 && ageTo == 0)
        {
            return false;
        }

        if (ageFrom > ageTo)
        {
            (ageFrom, ageTo) = (ageTo, ageFrom);
        }

        return true;
    }

    private static string StripHtml(string value)
    {
        var withoutTags = HtmlTagRegex.Replace(value, " ");
        return WebUtility.HtmlDecode(withoutTags)
            .Replace("\u00A0", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static decimal TryExtractPrice(string detailHtml, bool isFree)
    {
        if (isFree)
        {
            return 0m;
        }

        var costMatch = CostRegex.Match(detailHtml);

        if (!costMatch.Success)
        {
            return 0m;
        }

        var costText = StripHtml(costMatch.Groups["value"].Value);

        if (costText.Contains("gratis", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        var amountMatch = DecimalRegex.Match(costText);

        if (!amountMatch.Success)
        {
            return 0m;
        }

        var normalizedAmount = amountMatch.Groups["amount"].Value.Replace(',', '.');
        return decimal.TryParse(
            normalizedAmount,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : 0m;
    }
}
