using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;
using Barnaktiv.Domain.Enums;

namespace Barnaktiv.Infrastructure.Scrapers;

public sealed class BKHackenSportAdminBookingScraper(HttpClient httpClient) : IActivityScraper
{
    private const string Organizer = "BK Häcken";
    private const string City = "Göteborg";
    private static readonly Regex GroupRowRegex = new(
        "<div class='grupplist-row' id='grupp(?<id>\\d+)'.*?<h4>(?<title>.*?)</h4>.*?<div class=\"resp-small-label\">Ålder:</div>\\s*(?<age>.*?)\\s*</div>.*?<div class=\"resp-small-label\">Plats:</div>\\s*(?<place>.*?)\\s*</div>.*?<div class=\"resp-small-label\">Öppnas:</div>\\s*(?<open>.*?)\\s*</div>.*?<div class=\"grupplist-statusbox [^\"]+\">\\s*(?<status>.*?)\\s*</div>.*?(?:(?<spots>\\d+)\\s+platser\\s+kvar)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex SectionTitleRegex = new(
        "<h3>(?<value>.*?)</h3>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex BirthYearRangeRegex = new(
        "(?<from>20\\d{2})\\s*-\\s*(?<to>20\\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AgeRangeRegex = new(
        "(?<from>\\d{1,2})\\s*-\\s*(?<to>\\d{1,2})\\s*år",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex DateRangeRegex = new(
        "(?<start>\\d{4}-\\d{2}-\\d{2})\\s*-\\s*(?<end>\\d{4}-\\d{2}-\\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PriceRegex = new(
        "(?<value>\\d+(?:[\\.,]\\d{1,2})?)\\s*kr",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public string Kind => "bk-hacken-sportadmin-bookings-html-v1";

    public async Task<ScrapeResult> ScrapeAsync(
        ConfiguredIngestionSource source,
        CancellationToken cancellationToken)
    {
        var formId = TryExtractFormId(source.EndpointUrl);

        if (string.IsNullOrWhiteSpace(formId))
        {
            return new ScrapeResult([], [$"Source '{source.SourceKey}' has no valid SportAdmin form id in EndpointUrl."]);
        }

        var groupsUrl = $"https://sportadmin.se/book/loadGroups.asp?F={formId}";
        var groupsHtml = await GetHtmlAsync(groupsUrl, cancellationToken);
        var sectionMatches = SectionTitleRegex.Matches(groupsHtml);
        var items = new List<ScrapedActivityItem>();
        var errors = new List<string>();

        foreach (Match rowMatch in GroupRowRegex.Matches(groupsHtml))
        {
            var summary = ParseSummary(rowMatch, sectionMatches);

            if (summary is null)
            {
                continue;
            }

            var detailUrl = $"https://sportadmin.se/book/?F={formId}&grupp={summary.GroupId}";
            var detailContentUrl =
                $"https://sportadmin.se/book/bookPageController_paymentservice.asp?F={formId}&grupp={summary.GroupId}&subaction=";

            GroupDetail detail;

            try
            {
                var detailHtml = await GetHtmlAsync(detailContentUrl, cancellationToken);
                detail = ParseDetail(detailHtml);
            }
            catch (Exception exception)
            {
                errors.Add($"Group '{summary.GroupId}' could not be fetched: {exception.Message}");
                continue;
            }

            if (!IsChildTargeted(summary, detail))
            {
                continue;
            }

            var activityDate = detail.StartDate ?? DateTime.Today;
            var ageRange = ResolveAgeRange(summary.AgeText, detail.InformationText, activityDate);

            if (ageRange is null || ageRange.Value.AgeTo > 19)
            {
                continue;
            }

            var registrationOpenAt = TryParseOpenAt(summary.OpenText, activityDate.Year);
            var registrationCloseAt = detail.CloseAt;
            var registrationStatus = ResolveRegistrationStatus(
                summary.StatusText,
                registrationOpenAt,
                registrationCloseAt,
                summary.SpotsLeft);
            var location = ResolveLocation(summary.PlaceText, detail.Places);
            var description = BuildDescription(detail.InformationText);
            var price = detail.Price ?? 0m;

            items.Add(new ScrapedActivityItem(
                $"bk-hacken-sportadmin-{summary.GroupId}",
                string.IsNullOrWhiteSpace(detail.Title) ? summary.Title : detail.Title,
                description,
                Organizer,
                location,
                City,
                ageRange.Value.AgeFrom,
                ageRange.Value.AgeTo,
                summary.SectionTitle,
                activityDate,
                price,
                detailUrl,
                string.Empty,
                CreateRawPayload(source.EndpointUrl, groupsUrl, detailUrl, summary, detail, registrationStatus),
                false,
                "Fotboll",
                ActivityListingType.Program,
                registrationStatus,
                registrationOpenAt,
                registrationCloseAt,
                detailUrl));
        }

        if (items.Count == 0)
        {
            errors.Add("No child-targeted SportAdmin booking activities could be parsed from the BK Häcken booking page.");
        }

        return new ScrapeResult(
            items,
            errors,
            items.Select(item => item.ExternalId).ToList(),
            items.Count > 0);
    }

    private async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0 Safari/537.36");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.9));
        request.Headers.AcceptLanguage.ParseAdd("sv-SE,sv;q=0.9,en;q=0.8");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static GroupSummary? ParseSummary(Match rowMatch, MatchCollection sectionMatches)
    {
        if (!rowMatch.Success)
        {
            return null;
        }

        var title = CleanText(rowMatch.Groups["title"].Value);

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var sectionTitle = ResolveSectionTitle(rowMatch.Index, sectionMatches);
        var ageText = CleanText(rowMatch.Groups["age"].Value);
        var placeText = CleanText(rowMatch.Groups["place"].Value);
        var openText = CleanText(rowMatch.Groups["open"].Value);
        var statusText = CleanText(rowMatch.Groups["status"].Value);
        var spotsText = CleanText(rowMatch.Groups["spots"].Value);

        return new GroupSummary(
            rowMatch.Groups["id"].Value,
            title,
            sectionTitle,
            ageText,
            placeText,
            openText,
            statusText,
            int.TryParse(spotsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var spotsLeft)
                ? spotsLeft
                : null);
    }

    private static GroupDetail ParseDetail(string html)
    {
        var title = ExtractValue(html, "<div class=\"bookingtitle\">(?<value>.*?)</div>");
        var statusText = ExtractValue(html, "<div class=\"bookingstatus\">(?<value>.*?)</div>");
        var informationHtml = ExtractValue(
            html,
            "<div class=\"sa-card-titles\">\\s*Information\\s*</div>\\s*<div>(?<value>.*?)</div>\\s*</div>\\s*</div>\\s*<div class=\"sa-card-row\">");
        var informationText = CleanText(informationHtml);
        var places = Regex.Matches(
                html,
                "<div class=\"sa-trainings\">\\s*<span[^>]*>.*?</span>\\s*<span[^>]*>(?<value>.*?)</span>\\s*</div>",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Select(match => CleanText(match.Groups["value"].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var datesText = ExtractValue(
            html,
            "<div class=\"sa-card-titles\">\\s*Start-/Slutdatum\\s*</div>\\s*<div>\\s*(?<value>.*?)\\s*</div>");
        var closeAtText = ExtractValue(html, "Stänger\\s+(?<value>\\d{4}-\\d{2}-\\d{2}\\s+\\d{2}:\\d{2}:\\d{2})");
        var priceMatch = PriceRegex.Match(informationText);
        decimal? price = priceMatch.Success &&
                         decimal.TryParse(
                             priceMatch.Groups["value"].Value.Replace(",", ".", StringComparison.Ordinal),
                             NumberStyles.AllowDecimalPoint,
                             CultureInfo.InvariantCulture,
                             out var parsedPrice)
            ? parsedPrice
            : null;

        DateTime? startDate = null;
        var dateRangeMatch = DateRangeRegex.Match(CleanText(datesText));

        if (dateRangeMatch.Success &&
            DateTime.TryParseExact(
                dateRangeMatch.Groups["start"].Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsedStartDate))
        {
            startDate = DateTime.SpecifyKind(parsedStartDate, DateTimeKind.Local);
        }

        DateTime? closeAt = null;

        if (!string.IsNullOrWhiteSpace(closeAtText) &&
            DateTime.TryParseExact(
                closeAtText,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsedCloseAt))
        {
            closeAt = DateTime.SpecifyKind(parsedCloseAt, DateTimeKind.Local);
        }

        return new GroupDetail(
            title,
            statusText,
            informationText,
            places,
            startDate,
            closeAt,
            price);
    }

    private static bool IsChildTargeted(GroupSummary summary, GroupDetail detail)
    {
        var combined = NormalizeWhitespace(
            string.Join(
                " ",
                summary.SectionTitle,
                summary.Title,
                summary.AgeText,
                detail.InformationText,
                detail.Title,
                detail.StatusText));

        var hasAgeRange = ResolveAgeRange(summary.AgeText, detail.InformationText, detail.StartDate ?? DateTime.Today)
            is not null;
        var containsChildKeywords = ContainsAny(
            combined,
            "barn",
            "ungdom",
            "födelseår",
            "åldrarna",
            "camp",
            "lov",
            "fotbollsskola");
        var containsLeaderSignals = ContainsAny(
            combined,
            "ledare",
            "övningstrupp",
            "workshop",
            "konferensrum",
            "teori");

        if (containsLeaderSignals && !containsChildKeywords && !hasAgeRange)
        {
            return false;
        }

        return hasAgeRange || containsChildKeywords;
    }

    private static (int AgeFrom, int AgeTo)? ResolveAgeRange(
        string summaryAgeText,
        string detailInformationText,
        DateTime referenceDate)
    {
        var birthYearText = string.Join(" ", summaryAgeText, detailInformationText);
        var birthYearMatch = BirthYearRangeRegex.Match(birthYearText);

        if (birthYearMatch.Success &&
            int.TryParse(birthYearMatch.Groups["from"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oldestBirthYear) &&
            int.TryParse(birthYearMatch.Groups["to"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var youngestBirthYear))
        {
            var oldestRange = GetAgeRangeForBirthYear(oldestBirthYear, referenceDate);
            var youngestRange = GetAgeRangeForBirthYear(youngestBirthYear, referenceDate);
            return (youngestRange.AgeFrom, oldestRange.AgeTo);
        }

        var ageMatch = AgeRangeRegex.Match(birthYearText);

        if (ageMatch.Success &&
            int.TryParse(ageMatch.Groups["from"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ageFrom) &&
            int.TryParse(ageMatch.Groups["to"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ageTo))
        {
            return (Math.Min(ageFrom, ageTo), Math.Max(ageFrom, ageTo));
        }

        return null;
    }

    private static (int AgeFrom, int AgeTo) GetAgeRangeForBirthYear(int birthYear, DateTime referenceDate)
    {
        var ageFrom = Math.Max(0, referenceDate.Year - birthYear - 1);
        var ageTo = Math.Max(ageFrom, referenceDate.Year - birthYear);
        return (ageFrom, ageTo);
    }

    private static RegistrationStatus ResolveRegistrationStatus(
        string statusText,
        DateTime? openAt,
        DateTime? closeAt,
        int? spotsLeft)
    {
        if (ContainsAny(statusText, "full"))
        {
            return RegistrationStatus.Full;
        }

        if (spotsLeft.HasValue && spotsLeft.Value <= 0)
        {
            return RegistrationStatus.Full;
        }

        if (ContainsAny(statusText, "öppen"))
        {
            return ContainsAny(statusText, "ej öppen")
                ? ResolveClosedOrUpcoming(openAt)
                : RegistrationStatus.Open;
        }

        if (closeAt.HasValue && DateTime.UtcNow > closeAt.Value.ToUniversalTime())
        {
            return RegistrationStatus.Closed;
        }

        return ResolveClosedOrUpcoming(openAt);
    }

    private static RegistrationStatus ResolveClosedOrUpcoming(DateTime? openAt)
    {
        if (!openAt.HasValue)
        {
            return RegistrationStatus.Closed;
        }

        return DateTime.UtcNow < openAt.Value.ToUniversalTime()
            ? RegistrationStatus.Upcoming
            : RegistrationStatus.Closed;
    }

    private static DateTime? TryParseOpenAt(string value, int defaultYear)
    {
        var normalized = CleanText(value);

        if (string.IsNullOrWhiteSpace(normalized) || normalized == "-")
        {
            return null;
        }

        return DateTime.TryParseExact(
            $"{normalized}/{defaultYear}",
            "d/M HH:mm/yyyy",
            CultureInfo.GetCultureInfo("sv-SE"),
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Local)
            : null;
    }

    private static string ResolveLocation(string summaryPlace, IReadOnlyList<string> detailPlaces)
    {
        var normalizedSummaryPlace = CleanText(summaryPlace);

        if (!string.IsNullOrWhiteSpace(normalizedSummaryPlace) && normalizedSummaryPlace != "-")
        {
            return normalizedSummaryPlace;
        }

        return detailPlaces.FirstOrDefault() ?? "Location to be confirmed";
    }

    private static string BuildDescription(string informationText)
    {
        var cleaned = NormalizeWhitespace(informationText);

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Barnaktivitet via BK Häckens SportAdmin-bokning.";
        }

        return cleaned.Length <= 280
            ? cleaned
            : $"{cleaned[..277].TrimEnd()}...";
    }

    private static string ResolveSectionTitle(int rowIndex, MatchCollection sectionMatches)
    {
        var title = "SportAdmin booking";

        foreach (Match sectionMatch in sectionMatches)
        {
            if (sectionMatch.Index >= rowIndex)
            {
                break;
            }

            title = CleanText(sectionMatch.Groups["value"].Value);
        }

        return title;
    }

    private static string ExtractValue(string html, string pattern)
    {
        var match = Regex.Match(
            html,
            pattern,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success
            ? CleanText(match.Groups["value"].Value)
            : string.Empty;
    }

    private static string TryExtractFormId(string endpointUrl)
    {
        var match = Regex.Match(
            endpointUrl,
            "[?&]F=(?<value>[0-9a-fA-F\\-]+)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups["value"].Value
            : string.Empty;
    }

    private static string CreateRawPayload(
        string sourceUrl,
        string groupsUrl,
        string detailUrl,
        GroupSummary summary,
        GroupDetail detail,
        RegistrationStatus registrationStatus)
    {
        return JsonSerializer.Serialize(new
        {
            sourceUrl,
            groupsUrl,
            detailUrl,
            SummaryGroupId = summary.GroupId,
            SummaryTitle = summary.Title,
            SummarySectionTitle = summary.SectionTitle,
            SummaryAgeText = summary.AgeText,
            SummaryPlaceText = summary.PlaceText,
            SummaryOpenText = summary.OpenText,
            SummaryStatusText = summary.StatusText,
            SummarySpotsLeft = summary.SpotsLeft,
            DetailTitle = detail.Title,
            DetailStatusText = detail.StatusText,
            DetailInformationText = detail.InformationText,
            DetailPlaces = detail.Places,
            DetailStartDate = detail.StartDate,
            DetailCloseAt = detail.CloseAt,
            DetailPrice = detail.Price,
            registrationStatus
        });
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanText(string value)
    {
        return NormalizeWhitespace(
            WebUtility.HtmlDecode(HtmlTagRegex.Replace(value, " "))
                .Replace("\u00A0", " ", StringComparison.Ordinal)
                .Replace("\r", " ", StringComparison.Ordinal)
                .Replace("\n", " ", StringComparison.Ordinal));
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private sealed record GroupSummary(
        string GroupId,
        string Title,
        string SectionTitle,
        string AgeText,
        string PlaceText,
        string OpenText,
        string StatusText,
        int? SpotsLeft);

    private sealed record GroupDetail(
        string Title,
        string StatusText,
        string InformationText,
        IReadOnlyList<string> Places,
        DateTime? StartDate,
        DateTime? CloseAt,
        decimal? Price);
}
