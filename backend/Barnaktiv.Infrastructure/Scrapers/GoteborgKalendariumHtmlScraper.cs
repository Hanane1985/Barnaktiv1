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
    private const int DetailRequestConcurrency = 3;
    private const int RequestAttemptCount = 3;
    private const string ActivityCardMarker =
        "<div class=\"o-grid__column o-grid__column--stretch\" data-size=\"4/4 4/8@m 4/12@l\" data-testid=\"kalendarium-activity\">";

    private static readonly Uri BaseUri = new("https://goteborg.se");
    private static readonly CultureInfo SwedishCulture = CultureInfo.GetCultureInfo("sv-SE");
    private static readonly Regex ActivityLinkRegex = new(
        "<a class=\"c-card__title-link\"[^>]*href=\"(?<href>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CardTitleLinkRegex = new(
        "<a class=\"c-card__title-link\"[^>]*href=\"(?<href>[^\"]+)\"[^>]*>(?<title>.*?)</a>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex CardBylineRegex = new(
        "<span class=\"c-card__byline\">(?<value>.*?)</span>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex CardImageRegex = new(
        "<img class=\"c-image__image\"[^>]*src=\"(?<src>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex CardLabelValueRegex = new(
        "<dt[^>]*>(?<label>.*?)</dt><dd[^>]*>(?<value>.*?)</dd>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
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
        @"(?<!\d)(?<from>\d{1,2})\s*(?:-|\u2010|\u2011|\u2012|\u2013|\u2014|\u2212|till|to)\s*(?<to>\d{1,2})\s*(?:\u00E5r|yrs?|years?)(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AgeFromRegex = new(
        @"(?:fr\u00E5n|from)\s*(?<from>\d{1,2})\s*(?:\u00E5r|yrs?|years?)|(?<!\d)(?<from>\d{1,2})\s*\+\s*(?:\u00E5r|yrs?|years?)(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AgeSingleRegex = new(
        @"(?:f\u00F6r\s+(?:dig|barn(?:en)?|ungdom(?:ar)?))\s*:?\s*(?<age>\d{1,2})\s*(?:\u00E5r|yrs?|years?)|(?<!\d)(?<age>\d{1,2})-\u00E5r(?:ing|ingar)(?!\w)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex GradeRangeRegex = new(
        @"(?:\u00E5rskurs|klass|grade)\s*(?<from>\d{1,2})\s*(?:-|\u2010|\u2011|\u2012|\u2013|\u2014|\u2212|till|to)\s*(?<to>\d{1,2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex GradeFromRegex = new(
        @"(?:\u00E5rskurs|klass|grade)\s*(?<from>\d{1,2})\s*(?:\+|och upp\u00E5t|and up)(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly string[] ListDateFormats =
    [
        "dddd d MMMM",
        "dddd dd MMMM",
        "d MMMM",
        "dd MMMM",
        "dddd d MMM",
        "dddd dd MMM",
        "d MMM",
        "dd MMM"
    ];

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
        var cardFallbacksByDetailUrl = new Dictionary<string, ListCardFallback>(
            StringComparer.OrdinalIgnoreCase);
        var discoveredExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configuredMaxPages = source.MaxPages.GetValueOrDefault();
        var pageCount = configuredMaxPages > 0
            ? Math.Clamp(configuredMaxPages, 1, AbsoluteMaxPages)
            : AbsoluteMaxPages;
        var hasListPageErrors = false;

        for (var page = 0; page < pageCount; page++)
        {
            var pageUrl = BuildPageUrl(source.EndpointUrl, page);
            var listPageResult = await GetPageHtmlAsync(pageUrl, cancellationToken);

            if (!listPageResult.IsSuccess)
            {
                hasListPageErrors = true;
                errors.Add(
                    $"Failed to fetch list page {page + 1}: {listPageResult.ErrorMessage}");
                continue;
            }

            var listHtml = listPageResult.Content!;

            if (page == 0 && source.MaxPages is null)
            {
                pageCount = DetectPageCount(listHtml) ?? pageCount;
            }

            var cardFallbacks = ExtractListCardFallbacks(listHtml);

            if (cardFallbacks.Count == 0)
            {
                if (page == 0)
                {
                    errors.Add("No activity links were found on the Goteborg kalendarium page.");
                }

                break;
            }

            var linksAddedOnPage = 0;

            foreach (var cardFallback in cardFallbacks)
            {
                discoveredExternalIds.Add(cardFallback.ExternalId);

                if (cardFallbacksByDetailUrl.TryAdd(cardFallback.DetailUrl, cardFallback))
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
            cardFallbacksByDetailUrl,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = DetailRequestConcurrency,
                CancellationToken = cancellationToken
            },
            async (entry, ct) =>
            {
                var detailUrl = entry.Key;
                var cardFallback = entry.Value;
                var detailPageResult = await GetPageHtmlAsync(detailUrl, ct);

                if (!detailPageResult.IsSuccess)
                {
                    items.Add(CreateFallbackItem(cardFallback));
                    errors.Add(
                        $"Failed to fetch detail page '{detailUrl}': {detailPageResult.ErrorMessage}. Imported list-card fallback instead.");
                    return;
                }

                try
                {
                    var detailHtml = detailPageResult.Content!;

                    if (!TryParseDetail(detailUrl, detailHtml, out var item, out var error))
                    {
                        items.Add(CreateFallbackItem(cardFallback));
                        errors.Add($"{error} Imported list-card fallback instead.");
                        return;
                    }

                    items.Add(item!);
                }
                catch (Exception exception)
                {
                    items.Add(CreateFallbackItem(cardFallback));
                    errors.Add(
                        $"Failed to parse detail page '{detailUrl}': {exception.Message}. Imported list-card fallback instead.");
                }
            });

        return new ScrapeResult(
            items.ToList(),
            errors.ToList(),
            discoveredExternalIds.ToList(),
            !hasListPageErrors && discoveredExternalIds.Count > 0);
    }

    private async Task<PageFetchResult> GetPageHtmlAsync(string url, CancellationToken cancellationToken)
    {
        string? errorMessage = null;

        for (var attempt = 1; attempt <= RequestAttemptCount; attempt++)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return new PageFetchResult(
                        true,
                        await response.Content.ReadAsStringAsync(cancellationToken),
                        null);
                }

                errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";

                if (!ShouldRetry(response.StatusCode) || attempt == RequestAttemptCount)
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                errorMessage = "The request timed out.";
            }
            catch (HttpRequestException exception)
            {
                errorMessage = exception.Message;

                if (exception.StatusCode is { } statusCode &&
                    !ShouldRetry(statusCode))
                {
                    break;
                }
            }

            if (attempt < RequestAttemptCount)
            {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
            }
        }

        return new PageFetchResult(false, null, errorMessage ?? "Unknown request error.");
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

    private static string? TryExtractActivityId(string url)
    {
        var activityIdMatch = Regex.Match(
            url,
            @"[?&]activityId=(?<id>[0-9a-fA-F\-]+)",
            RegexOptions.CultureInvariant);

        return activityIdMatch.Success
            ? activityIdMatch.Groups["id"].Value
            : null;
    }

    private static IReadOnlyList<ListCardFallback> ExtractListCardFallbacks(string listHtml)
    {
        var segments = listHtml.Split(ActivityCardMarker, StringSplitOptions.None);
        var fallbacks = new List<ListCardFallback>();

        for (var i = 1; i < segments.Length; i++)
        {
            var fallback = TryParseListCard(segments[i]);

            if (fallback is not null)
            {
                fallbacks.Add(fallback);
            }
        }

        if (fallbacks.Count == 0)
        {
            foreach (Match match in ActivityLinkRegex.Matches(listHtml))
            {
                var relativeHref = WebUtility.HtmlDecode(match.Groups["href"].Value);
                var externalId = TryExtractActivityId(relativeHref);

                if (string.IsNullOrWhiteSpace(relativeHref) || string.IsNullOrWhiteSpace(externalId))
                {
                    continue;
                }

                fallbacks.Add(new ListCardFallback(
                    externalId,
                    new Uri(BaseUri, relativeHref).ToString(),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty));
            }
        }

        return fallbacks;
    }

    private static ListCardFallback? TryParseListCard(string cardHtml)
    {
        var titleLinkMatch = CardTitleLinkRegex.Match(cardHtml);

        if (!titleLinkMatch.Success)
        {
            return null;
        }

        var relativeHref = WebUtility.HtmlDecode(titleLinkMatch.Groups["href"].Value);
        var externalId = TryExtractActivityId(relativeHref);

        if (string.IsNullOrWhiteSpace(relativeHref) || string.IsNullOrWhiteSpace(externalId))
        {
            return null;
        }

        var title = StripHtml(titleLinkMatch.Groups["title"].Value).Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var organizer = CardBylineRegex.Match(cardHtml) is { Success: true } bylineMatch
            ? StripHtml(bylineMatch.Groups["value"].Value).Trim()
            : string.Empty;
        var imageUrl = CardImageRegex.Match(cardHtml) is { Success: true } imageMatch
            ? WebUtility.HtmlDecode(imageMatch.Groups["src"].Value).Trim()
            : string.Empty;
        var startDateText = string.Empty;
        var endDateText = string.Empty;
        var timeText = string.Empty;

        foreach (Match labelValueMatch in CardLabelValueRegex.Matches(cardHtml))
        {
            var label = StripHtml(labelValueMatch.Groups["label"].Value).Trim();
            var value = StripHtml(labelValueMatch.Groups["value"].Value).Trim();

            switch (label)
            {
                case "Datum":
                case "Börjar":
                    startDateText = value;
                    break;
                case "Slutar":
                    endDateText = value;
                    break;
                case "Tid":
                    timeText = value;
                    break;
            }
        }

        return new ListCardFallback(
            externalId,
            new Uri(BaseUri, relativeHref).ToString(),
            title,
            organizer,
            imageUrl,
            startDateText,
            endDateText,
            timeText,
            cardHtml);
    }

    private static ScrapedActivityItem CreateFallbackItem(ListCardFallback cardFallback)
    {
        var description = BuildFallbackDescription(cardFallback);
        var (ageFrom, ageTo) = InferAgeRange(cardFallback.Title, description, []);
        var date = ResolveFallbackDate(cardFallback);

        var rawPayload = JsonSerializer.Serialize(new
        {
            kind = "goteborg-kalendarium-list-card-fallback",
            cardFallback.ExternalId,
            cardFallback.DetailUrl,
            cardFallback.Title,
            cardFallback.Organizer,
            cardFallback.ImageUrl,
            cardFallback.StartDateText,
            cardFallback.EndDateText,
            cardFallback.TimeText
        });

        return new ScrapedActivityItem(
            cardFallback.ExternalId,
            cardFallback.Title,
            description,
            cardFallback.Organizer,
            string.Empty,
            "Goteborg",
            ageFrom,
            ageTo,
            "Kalendarium",
            date,
            0m,
            cardFallback.DetailUrl,
            cardFallback.ImageUrl,
            rawPayload,
            true);
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

    private static string BuildFallbackDescription(ListCardFallback cardFallback)
    {
        var descriptionParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(cardFallback.Organizer))
        {
            descriptionParts.Add($"Arrangör: {cardFallback.Organizer}");
        }

        if (!string.IsNullOrWhiteSpace(cardFallback.StartDateText))
        {
            descriptionParts.Add($"Datum: {cardFallback.StartDateText}");
        }

        if (!string.IsNullOrWhiteSpace(cardFallback.EndDateText))
        {
            descriptionParts.Add($"Slutar: {cardFallback.EndDateText}");
        }

        if (!string.IsNullOrWhiteSpace(cardFallback.TimeText))
        {
            descriptionParts.Add($"Tid: {cardFallback.TimeText}");
        }

        return string.Join(" ", descriptionParts);
    }

    private static DateTime ResolveFallbackDate(ListCardFallback cardFallback)
    {
        if (TryParseListDate(cardFallback.StartDateText, out var parsedDate))
        {
            return parsedDate;
        }

        if (TryParseListDate(cardFallback.EndDateText, out parsedDate))
        {
            return parsedDate;
        }

        return DateTime.Today;
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

        if (normalizedText.Contains("alla \u00E5ldrar", StringComparison.Ordinal) ||
            normalizedText.Contains("alla aldrar", StringComparison.Ordinal))
        {
            return (0, 99);
        }

        if (normalizedText.Contains("f\u00F6rskol", StringComparison.Ordinal))
        {
            return (3, 5);
        }

        if (normalizedText.Contains("mellanstad", StringComparison.Ordinal))
        {
            return (10, 12);
        }

        if (normalizedText.Contains("l\u00E5gstad", StringComparison.Ordinal))
        {
            return (7, 9);
        }

        if (normalizedText.Contains("h\u00F6gstad", StringComparison.Ordinal))
        {
            return (13, 15);
        }

        if (normalizedText.Contains("gymnas", StringComparison.Ordinal))
        {
            return (16, 19);
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

        var gradeRangeMatch = GradeRangeRegex.Match(input);

        if (gradeRangeMatch.Success &&
            int.TryParse(gradeRangeMatch.Groups["from"].Value, out var gradeFrom) &&
            int.TryParse(gradeRangeMatch.Groups["to"].Value, out var gradeTo))
        {
            ageFrom = GradeToApproximateAge(gradeFrom);
            ageTo = GradeToApproximateAge(gradeTo);
            return NormalizeAgeRange(ref ageFrom, ref ageTo);
        }

        var gradeFromMatch = GradeFromRegex.Match(input);

        if (gradeFromMatch.Success &&
            int.TryParse(gradeFromMatch.Groups["from"].Value, out gradeFrom))
        {
            ageFrom = GradeToApproximateAge(gradeFrom);
            ageTo = 25;
            return NormalizeAgeRange(ref ageFrom, ref ageTo);
        }

        return false;
    }

    private static bool TryParseListDate(string input, out DateTime date)
    {
        var normalizedInput = NormalizeWhitespace(StripHtml(input))
            .Trim()
            .TrimEnd('.', ',');

        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            date = default;
            return false;
        }

        var referenceDate = DateTime.Today;
        var candidates = new HashSet<DateTime>();

        if (DateTime.TryParse(
                normalizedInput,
                SwedishCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedWithYear) &&
            parsedWithYear.Year > 1900)
        {
            candidates.Add(parsedWithYear.Date);
        }

        for (var year = referenceDate.Year - 1; year <= referenceDate.Year + 1; year++)
        {
            foreach (var format in ListDateFormats)
            {
                if (DateTime.TryParseExact(
                        $"{normalizedInput} {year}",
                        $"{format} yyyy",
                        SwedishCulture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out var parsedCandidate))
                {
                    candidates.Add(parsedCandidate.Date);
                }
            }
        }

        if (candidates.Count == 0)
        {
            date = default;
            return false;
        }

        date = candidates
            .OrderBy(candidate => Math.Abs((candidate - referenceDate).TotalDays))
            .ThenBy(candidate => candidate)
            .First();
        return true;
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

    private static int GradeToApproximateAge(int grade)
    {
        return Math.Clamp(grade + 6, 0, 99);
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

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant);
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

    private static bool ShouldRetry(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        return TimeSpan.FromMilliseconds(300 * attempt * attempt);
    }

    private sealed record ListCardFallback(
        string ExternalId,
        string DetailUrl,
        string Title,
        string Organizer,
        string ImageUrl,
        string StartDateText,
        string EndDateText,
        string TimeText,
        string RawHtml);

    private sealed record PageFetchResult(
        bool IsSuccess,
        string? Content,
        string? ErrorMessage);
}
