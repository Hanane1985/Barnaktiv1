using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;

namespace Barnaktiv.Infrastructure.Scrapers;

public sealed class PassalenMecCalendarScraper(HttpClient httpClient) : IActivityScraper
{
    private const int AbsoluteMaxMonths = 120;
    private const int RequestAttemptCount = 3;
    private const string Organizer = "Passalen";

    private static readonly Regex FullCalendarConfigRegex = new(
        "jQuery\\(\"#mec_skin_(?<id>\\d+)\"\\)\\.mecFullCalendar\\s*\\(\\s*\\{(?<config>.*?)\\}\\s*\\);",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex AttsRegex = new(
        "atts:\\s*\"(?<value>.*?)\"\\s*,",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex AjaxUrlRegex = new(
        "ajax_url:\\s*\"(?<value>.*?)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex CurrentMonthIdRegex = new(
        "id=\"mec_month_navigator_\\d+_(?<monthId>\\d{6})\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex YearSelectRegex = new(
        "<select id=\"mec_sf_year_\\d+\">(?<options>.*?)</select>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex YearOptionRegex = new(
        "<option value=\"(?<year>\\d{4})\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex JsonLdRegex = new(
        "<script type=\"application/ld\\+json\">\\s*(?<json>.*?)\\s*</script>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex DecimalRegex = new(
        @"(?<amount>\d+(?:[.,]\d+)?)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AgeRangeRegex = new(
        "(?<!\\d)(?<from>\\d{1,2})\\s*(?:-|till|to)\\s*(?<to>\\d{1,2})\\s*(?:\\u00e5r|yrs?|years?)(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AgeFromRegex = new(
        "(?:fr\\u00e5n|from)\\s*(?<from>\\d{1,2})\\s*(?:\\u00e5r|yrs?|years?)|(?<!\\d)(?<from>\\d{1,2})\\s*\\+\\s*(?:\\u00e5r|yrs?|years?)(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AgeSingleRegex = new(
        "(?<!\\d)(?<age>\\d{1,2})\\s*(?:\\u00e5r|yrs?|years?)(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly string[] DescriptionFieldLabels =
    [
        "Ort:",
        "Information om aktiviteten:",
        "Schema:",
        "Dag:",
        "Tid:",
        "M\u00f6tesplats:",
        "Adress:",
        "N\u00e4rmaste h\u00e5llplats:",
        "Kostnad:",
        "\u00c5lder:",
        "Ledare:",
        "Anm\u00e4lan:"
    ];

    public string Kind => "passalen-mec-calendar-v1";

    public async Task<ScrapeResult> ScrapeAsync(
        ConfiguredIngestionSource source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.EndpointUrl))
        {
            return new ScrapeResult([], [$"Source '{source.SourceKey}' has no endpoint URL."]);
        }

        var pageResult = await GetHtmlAsync(source.EndpointUrl, cancellationToken);

        if (!pageResult.IsSuccess)
        {
            return new ScrapeResult(
                [],
                [$"Failed to fetch Passalen calendar page: {pageResult.ErrorMessage}"]);
        }

        var pageHtml = pageResult.Content!;

        if (!TryParseCalendarContext(source.EndpointUrl, pageHtml, out var calendarContext, out var calendarError))
        {
            return new ScrapeResult([], [$"Passalen calendar page could not be parsed: {calendarError}"]);
        }

        var maxMonthsToScan = source.MaxPages is > 0
            ? Math.Clamp(source.MaxPages.Value, 1, Math.Min(calendarContext.TotalMonthsAvailable, AbsoluteMaxMonths))
            : Math.Min(calendarContext.TotalMonthsAvailable, AbsoluteMaxMonths);
        var errors = new List<string>();
        var items = new List<ScrapedActivityItem>();
        var discoveredExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hadMonthErrors = false;

        for (var monthOffset = 0; monthOffset < maxMonthsToScan; monthOffset++)
        {
            var monthDate = calendarContext.StartMonth.AddMonths(monthOffset);
            var monthResult = await LoadMonthAsync(
                calendarContext.AdminAjaxUrl,
                calendarContext.EncodedAtts,
                monthDate.Year,
                monthDate.Month,
                cancellationToken);

            if (!monthResult.IsSuccess)
            {
                hadMonthErrors = true;
                errors.Add(
                    $"Failed to fetch Passalen month {monthDate:yyyy-MM}: {monthResult.ErrorMessage}");
                continue;
            }

            if (!string.Equals(
                    monthResult.LoadedMonthId,
                    monthDate.ToString("yyyyMM", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                hadMonthErrors = true;
                errors.Add(
                    $"Passalen month request {monthDate:yyyy-MM} returned '{monthResult.LoadedMonthId}' instead of the requested month.");
                continue;
            }

            foreach (var jsonBlock in ExtractEventJsonBlocks(monthResult.MonthHtml))
            {
                if (!TryMapEvent(jsonBlock, out var item, out var error))
                {
                    errors.Add($"Passalen month {monthDate:yyyy-MM}: {error}");
                    continue;
                }

                if (discoveredExternalIds.Add(item!.ExternalId))
                {
                    items.Add(item);
                }
            }
        }

        if (items.Count == 0)
        {
            errors.Add("No activities were parsed from the Passalen calendar.");
        }

        var scannedAllExposedMonths = maxMonthsToScan >= calendarContext.TotalMonthsAvailable;

        return new ScrapeResult(
            items,
            errors,
            discoveredExternalIds.ToList(),
            scannedAllExposedMonths && !hadMonthErrors);
    }

    private async Task<PageFetchResult> GetHtmlAsync(string url, CancellationToken cancellationToken)
    {
        return await SendAsync(
            () =>
            {
                var request = CreateRequest(HttpMethod.Get, url);
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                return request;
            },
            cancellationToken);
    }

    private async Task<MonthFetchResult> LoadMonthAsync(
        string adminAjaxUrl,
        string encodedAtts,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var formPairs = ParseFormEncodedPairs(encodedAtts);
        formPairs.Add(new KeyValuePair<string, string>("action", "mec_monthly_view_load_month"));
        formPairs.Add(new KeyValuePair<string, string>("mec_month", month.ToString("00", CultureInfo.InvariantCulture)));
        formPairs.Add(new KeyValuePair<string, string>("mec_year", year.ToString(CultureInfo.InvariantCulture)));
        formPairs.Add(new KeyValuePair<string, string>("navigator_click", "true"));
        formPairs.Add(new KeyValuePair<string, string>("apply_sf_date", "0"));

        var response = await SendAsync(
            () =>
            {
                var request = CreateRequest(HttpMethod.Post, adminAjaxUrl);
                request.Headers.Accept.ParseAdd("application/json,text/javascript,*/*;q=0.8");
                request.Content = new FormUrlEncodedContent(formPairs);
                return request;
            },
            cancellationToken);

        if (!response.IsSuccess)
        {
            return new MonthFetchResult(false, string.Empty, string.Empty, response.ErrorMessage);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Content!);
            var root = document.RootElement;
            var monthHtml = GetString(root, "month") ?? string.Empty;
            var loadedMonthId = GetNestedString(root, "current_month", "id") ?? string.Empty;

            return new MonthFetchResult(true, monthHtml, loadedMonthId, null);
        }
        catch (Exception exception)
        {
            return new MonthFetchResult(
                false,
                string.Empty,
                string.Empty,
                $"Monthly MEC payload could not be parsed: {exception.Message}");
        }
    }

    private async Task<PageFetchResult> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        string? errorMessage = null;

        for (var attempt = 1; attempt <= RequestAttemptCount; attempt++)
        {
            try
            {
                using var request = requestFactory();
                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return new PageFetchResult(
                        true,
                        await response.Content.ReadAsStringAsync(cancellationToken),
                        null);
                }

                errorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                errorMessage = "The request timed out.";
            }
            catch (HttpRequestException exception)
            {
                errorMessage = exception.Message;
            }

            if (attempt < RequestAttemptCount)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt * attempt), cancellationToken);
            }
        }

        return new PageFetchResult(false, null, errorMessage ?? "Unknown request error.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0 Safari/537.36");
        request.Headers.AcceptLanguage.ParseAdd("sv-SE,sv;q=0.9,en;q=0.8");
        return request;
    }

    private static bool TryParseCalendarContext(
        string endpointUrl,
        string pageHtml,
        out CalendarContext context,
        out string error)
    {
        var configMatch = FullCalendarConfigRegex.Match(pageHtml);

        if (!configMatch.Success)
        {
            context = new CalendarContext(string.Empty, default, string.Empty, 0);
            error = "Missing MEC full-calendar initialization block.";
            return false;
        }

        var config = configMatch.Groups["config"].Value;
        var attsMatch = AttsRegex.Match(config);

        if (!attsMatch.Success)
        {
            context = new CalendarContext(string.Empty, default, string.Empty, 0);
            error = "Missing MEC 'atts' configuration.";
            return false;
        }

        var monthMatch = CurrentMonthIdRegex.Match(pageHtml);

        if (!monthMatch.Success ||
            !DateTime.TryParseExact(
                monthMatch.Groups["monthId"].Value,
                "yyyyMM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var startMonth))
        {
            context = new CalendarContext(string.Empty, default, string.Empty, 0);
            error = "Missing current month marker.";
            return false;
        }

        var yearSelectMatch = YearSelectRegex.Match(pageHtml);
        var maxYear = startMonth.Year;

        if (yearSelectMatch.Success)
        {
            var parsedYears = YearOptionRegex.Matches(yearSelectMatch.Groups["options"].Value)
                .Select(match => int.TryParse(match.Groups["year"].Value, out var year)
                    ? year
                    : (int?)null)
                .Where(year => year.HasValue)
                .Select(year => year!.Value)
                .ToList();

            if (parsedYears.Count > 0)
            {
                maxYear = parsedYears.Max();
            }
        }

        var totalMonthsAvailable = Math.Max(
            1,
            ((maxYear - startMonth.Year) * 12) + (12 - startMonth.Month + 1));
        var adminAjaxUrl = AjaxUrlRegex.Match(config) is { Success: true } ajaxUrlMatch
            ? WebUtility.HtmlDecode(ajaxUrlMatch.Groups["value"].Value)
            : new Uri(new Uri(endpointUrl), "/wp-admin/admin-ajax.php").ToString();

        context = new CalendarContext(
            WebUtility.HtmlDecode(attsMatch.Groups["value"].Value),
            new DateTime(startMonth.Year, startMonth.Month, 1),
            adminAjaxUrl,
            totalMonthsAvailable);
        error = string.Empty;
        return true;
    }

    private static IReadOnlyList<string> ExtractEventJsonBlocks(string monthHtml)
    {
        return JsonLdRegex.Matches(monthHtml)
            .Select(match => match.Groups["json"].Value)
            .Where(json => !string.IsNullOrWhiteSpace(json))
            .ToList();
    }

    private static bool TryMapEvent(
        string rawJson,
        out ScrapedActivityItem? item,
        out string error)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;

            if (!string.Equals(
                    GetString(root, "@type"),
                    "Event",
                    StringComparison.OrdinalIgnoreCase))
            {
                item = null;
                error = "JSON-LD block is not an Event.";
                return false;
            }

            var title = GetString(root, "name");
            var startDateText = GetString(root, "startDate");

            if (string.IsNullOrWhiteSpace(title) ||
                !DateTime.TryParse(
                    startDateText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                    out var startDate))
            {
                item = null;
                error = "Event JSON-LD is missing a valid name or startDate.";
                return false;
            }

            var description = NormalizeWhitespace(GetString(root, "description") ?? string.Empty);
            var websiteUrl = GetString(root, "url")
                ?? GetNestedString(root, "offers", "url")
                ?? string.Empty;
            var imageUrl = GetString(root, "image") ?? string.Empty;
            var locationName = GetNestedString(root, "location", "name") ?? string.Empty;
            var locationAddress = GetNestedString(root, "location", "address") ?? string.Empty;
            var city = ResolveCity(description, locationName, locationAddress);
            var location = ResolveLocation(description, locationName, locationAddress);
            var price = ResolvePrice(description, GetNestedString(root, "offers", "price"));
            var (ageFrom, ageTo) = InferAgeRange(title, description);
            var externalId = BuildExternalId(title, websiteUrl, startDate, location);

            item = new ScrapedActivityItem(
                externalId,
                title.Trim(),
                description,
                Organizer,
                location,
                city,
                ageFrom,
                ageTo,
                string.Empty,
                startDate,
                price,
                websiteUrl.Trim(),
                imageUrl.Trim(),
                rawJson);

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            item = null;
            error = $"Event JSON-LD could not be parsed: {exception.Message}";
            return false;
        }
    }

    private static string BuildExternalId(
        string title,
        string websiteUrl,
        DateTime startDate,
        string location)
    {
        var seed = string.Join(
            "|",
            title.Trim().ToLowerInvariant(),
            websiteUrl.Trim().ToLowerInvariant(),
            location.Trim().ToLowerInvariant(),
            startDate.ToString("O", CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(hash);
    }

    private static string ResolveCity(
        string description,
        string locationName,
        string locationAddress)
    {
        var explicitCity = ExtractLabeledValue(description, "Ort:");
        var combined = $"{explicitCity} {locationName} {locationAddress}".Trim();

        if (combined.Contains("g\u00f6teborg", StringComparison.OrdinalIgnoreCase))
        {
            return "G\u00f6teborg";
        }

        if (combined.Contains("stockholm", StringComparison.OrdinalIgnoreCase))
        {
            return "Stockholm";
        }

        if (combined.Contains("m\u00f6lndal", StringComparison.OrdinalIgnoreCase) &&
            combined.Contains("h\u00e4rryda", StringComparison.OrdinalIgnoreCase))
        {
            return "M\u00f6lndal/H\u00e4rryda";
        }

        if (combined.Contains("m\u00f6lndal", StringComparison.OrdinalIgnoreCase))
        {
            return "M\u00f6lndal";
        }

        if (combined.Contains("h\u00e4rryda", StringComparison.OrdinalIgnoreCase))
        {
            return "H\u00e4rryda";
        }

        return explicitCity;
    }

    private static string ResolveLocation(
        string description,
        string locationName,
        string locationAddress)
    {
        if (!string.IsNullOrWhiteSpace(locationName))
        {
            return locationName.Trim();
        }

        var meetingPlace = ExtractLabeledValue(description, "M\u00f6tesplats:");

        if (!string.IsNullOrWhiteSpace(meetingPlace))
        {
            return meetingPlace;
        }

        var address = ExtractLabeledValue(description, "Adress:");

        if (!string.IsNullOrWhiteSpace(address))
        {
            return address;
        }

        return locationAddress.StartsWith("N\u00e4rmaste h\u00e5llplats", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : locationAddress.Trim();
    }

    private static decimal ResolvePrice(string description, string? priceFromOffer)
    {
        if (!string.IsNullOrWhiteSpace(priceFromOffer) &&
            decimal.TryParse(
                priceFromOffer.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsedOfferPrice))
        {
            return parsedOfferPrice;
        }

        var priceText = ExtractLabeledValue(description, "Kostnad:");

        if (string.IsNullOrWhiteSpace(priceText) ||
            priceText.Contains("gratis", StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        var amountMatch = DecimalRegex.Match(priceText);

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

    private static (int AgeFrom, int AgeTo) InferAgeRange(string title, string description)
    {
        var ageText = ExtractLabeledValue(description, "\u00c5lder:");
        var searchText = $"{ageText} {title} {description}".Trim();

        if (TryExtractNumericAgeRange(searchText, out var ageFrom, out var ageTo))
        {
            return (ageFrom, ageTo);
        }

        var normalized = searchText.ToLowerInvariant();

        if (normalized.Contains("alla \u00e5ldrar", StringComparison.Ordinal) ||
            normalized.Contains("alla aldrar", StringComparison.Ordinal))
        {
            return (0, 99);
        }

        if (normalized.Contains("barn och unga", StringComparison.Ordinal) ||
            normalized.Contains("barn & unga", StringComparison.Ordinal))
        {
            return (0, 25);
        }

        if (normalized.Contains("ungdom", StringComparison.Ordinal) ||
            normalized.Contains("unga", StringComparison.Ordinal) ||
            normalized.Contains("ton\u00e5r", StringComparison.Ordinal))
        {
            return (13, 25);
        }

        if (normalized.Contains("barn", StringComparison.Ordinal))
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

    private static string ExtractLabeledValue(string description, string label)
    {
        var normalized = NormalizeWhitespace(description);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var startIndex = normalized.IndexOf(label, StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += label.Length;
        var endIndex = normalized.Length;

        foreach (var candidate in DescriptionFieldLabels)
        {
            if (string.Equals(candidate, label, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidateIndex = normalized.IndexOf(candidate, startIndex, StringComparison.OrdinalIgnoreCase);

            if (candidateIndex >= 0 && candidateIndex < endIndex)
            {
                endIndex = candidateIndex;
            }
        }

        return normalized[startIndex..endIndex].Trim();
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value ?? string.Empty, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static List<KeyValuePair<string, string>> ParseFormEncodedPairs(string input)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        var normalizedInput = WebUtility.HtmlDecode(input);

        foreach (var segment in normalizedInput.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = segment.IndexOf('=');
            var rawKey = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            var rawValue = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
            var key = UrlDecode(rawKey);

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            pairs.Add(new KeyValuePair<string, string>(key, UrlDecode(rawValue)));
        }

        return pairs;
    }

    private static string UrlDecode(string value)
    {
        return Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
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
        if (!element.TryGetProperty(parentName, out var parent) ||
            parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetString(parent, childName);
    }

    private sealed record CalendarContext(
        string EncodedAtts,
        DateTime StartMonth,
        string AdminAjaxUrl,
        int TotalMonthsAvailable);

    private sealed record PageFetchResult(
        bool IsSuccess,
        string? Content,
        string? ErrorMessage);

    private sealed record MonthFetchResult(
        bool IsSuccess,
        string MonthHtml,
        string LoadedMonthId,
        string? ErrorMessage);
}
