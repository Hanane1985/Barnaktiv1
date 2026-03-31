using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;
using Barnaktiv.Domain.Enums;

namespace Barnaktiv.Infrastructure.Scrapers;

public sealed class SlsGoteborgSportAdminBookingScraper(HttpClient httpClient) : IActivityScraper
{
    private const string Organizer = "SLS Göteborg";
    private const string City = "Göteborg";
    private const int DetailRequestConcurrency = 8;

    private static readonly Regex GroupRowStartRegex = new(
        "<div class='grupplist-row' id='grupp(?<id>\\d+)'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SectionRegex = new(
        "<div id='grupptyp\\d+'[^>]*>.*?<h3>(?<title>.*?)</h3>\\s*(?:<div class=\"grupptyp-desc\">(?<description>.*?)</div>)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex BookingLinkRegex = new(
        "https?://sportadmin\\.se/book/\\?F=(?<value>[0-9a-fA-F\\-]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex BirthYearRangeRegex = new(
        "(?<!\\d)(?<from>19\\d{2}|20\\d{2})\\s*-\\s*(?<to>19\\d{2}|20\\d{2})(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BirthYearRegex = new(
        "(?<!\\d)(?<year>19\\d{2}|20\\d{2})(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AgeRangeRegex = new(
        "(?<!\\d)(?<from>\\d{1,2})\\s*(?:-|\\u2010|\\u2011|\\u2012|\\u2013|\\u2014|\\u2212|till|to)\\s*(?<to>\\d{1,2})\\s*(?:\\+?\\s*)?(?:\\u00e5r|yrs?|years?)(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex MonthRangeRegex = new(
        "(?<!\\d)(?<from>\\d{1,2})\\s*(?:-|\\u2010|\\u2011|\\u2012|\\u2013|\\u2014|\\u2212|till|to)\\s*(?<to>\\d{1,2})\\s*(?:m\\u00e5n(?:ader)?|months?)(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex MonthRangePrefixedRegex = new(
        "(?<!\\d)(?<from>\\d{1,2})\\s*(?:m\\u00e5n(?:ader)?|months?)\\s*(?:-|\\u2010|\\u2011|\\u2012|\\u2013|\\u2014|\\u2212|till|to)\\s*(?<to>\\d{1,2})\\s*(?:m\\u00e5n(?:ader)?|months?)(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AgeFromRegex = new(
        "(?:fr\\u00e5n|from)\\s*(?<from>\\d{1,2})\\s*(?:\\u00e5r|yrs?|years?)|(?<!\\d)(?<from>\\d{1,2})\\s*\\+\\s*(?:\\u00e5r|yrs?|years?)(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex AgeSingleRegex = new(
        "(?<!\\d)(?<age>\\d{1,2})\\s*(?:\\u00e5r|yrs?|years?)(?!\\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex DateRegex = new(
        "\\d{4}-\\d{2}-\\d{2}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PriceRegex = new(
        "(?<value>\\d+(?:[\\.,]\\d{1,2})?)\\s*kr",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public string Kind => "sls-goteborg-sportadmin-bookings-html-v1";

    public async Task<ScrapeResult> ScrapeAsync(
        ConfiguredIngestionSource source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.EndpointUrl))
        {
            return new ScrapeResult([], [$"Source '{source.SourceKey}' has no endpoint URL."]);
        }

        var formId = await ResolveFormIdAsync(source.EndpointUrl, cancellationToken);

        if (string.IsNullOrWhiteSpace(formId))
        {
            return new ScrapeResult(
                [],
                [$"Source '{source.SourceKey}' has no valid SportAdmin form id and no booking link could be discovered from the page."]);
        }

        var groupsUrl = $"https://sportadmin.se/book/loadGroups.asp?F={formId}";
        var groupsHtml = await GetHtmlAsync(groupsUrl, cancellationToken);
        var sectionMatches = SectionRegex.Matches(groupsHtml);
        var summaries = new List<GroupSummary>();
        var items = new List<ScrapedActivityItem>();
        var errors = new List<string>();

        var rowMatches = GroupRowStartRegex.Matches(groupsHtml);

        for (var rowIndex = 0; rowIndex < rowMatches.Count; rowIndex++)
        {
            var rowMatch = rowMatches[rowIndex];
            var rowEndIndex = rowIndex < rowMatches.Count - 1
                ? rowMatches[rowIndex + 1].Index
                : groupsHtml.Length;
            var summary = ParseSummary(rowMatch, rowEndIndex, sectionMatches, groupsHtml);

            if (summary is null || !IsPotentiallyRelevant(summary))
            {
                continue;
            }
            summaries.Add(summary);
        }

        using var detailSemaphore = new SemaphoreSlim(DetailRequestConcurrency);
        var processedGroups = await Task.WhenAll(
            summaries.Select(summary =>
                ProcessGroupAsync(
                    source,
                    formId,
                    groupsUrl,
                    summary,
                    detailSemaphore,
                    cancellationToken)));

        foreach (var processedGroup in processedGroups)
        {
            if (processedGroup.Item is not null)
            {
                items.Add(processedGroup.Item);
            }

            if (processedGroup.Errors.Count > 0)
            {
                errors.AddRange(processedGroup.Errors);
            }
        }

        if (items.Count == 0)
        {
            errors.Add("No child-targeted SportAdmin booking activities could be parsed from the SLS G\u00f6teborg booking page.");
        }

        return new ScrapeResult(
            items,
            errors,
            items.Select(item => item.ExternalId).ToList(),
            items.Count > 0);
    }

    private async Task<ProcessedGroupResult> ProcessGroupAsync(
        ConfiguredIngestionSource source,
        string formId,
        string groupsUrl,
        GroupSummary summary,
        SemaphoreSlim detailSemaphore,
        CancellationToken cancellationToken)
    {
        var detailUrl = $"https://sportadmin.se/book/?F={formId}&grupp={summary.GroupId}";
        var detailContentUrl =
            $"https://sportadmin.se/book/bookPageController_paymentservice.asp?F={formId}&grupp={summary.GroupId}&subaction=";

        await detailSemaphore.WaitAsync(cancellationToken);

        try
        {
            GroupDetail detail;

            try
            {
                var detailHtml = await GetHtmlAsync(detailContentUrl, cancellationToken);
                detail = ParseDetail(detailHtml);
            }
            catch (Exception exception)
            {
                return new ProcessedGroupResult(
                    null,
                    [$"Group '{summary.GroupId}' could not be fetched: {exception.Message}"]);
            }

            var activityDate = detail.StartDate ?? DateTime.Today;
            var ageRange = ResolveAgeRange(summary, detail, activityDate);

            if (!IsChildTargeted(summary, detail, ageRange))
            {
                return ProcessedGroupResult.Empty;
            }

            if (ageRange is null)
            {
                return new ProcessedGroupResult(
                    null,
                    [$"Group '{summary.GroupId}' looked child-targeted but had no parseable age range."]);
            }

            var registrationOpenAt = TryParseOpenAt(summary.OpenText, activityDate);
            var registrationCloseAt = detail.CloseAt;
            var registrationStatus = ResolveRegistrationStatus(
                summary.StatusText,
                registrationOpenAt,
                registrationCloseAt,
                summary.SpotsLeft);
            var location = ResolveLocation(summary.PlaceText, detail.Places);
            var title = ResolveTitle(summary, detail);
            var description = BuildDescription(summary.SectionDescription, detail.InformationText);
            var price = detail.Price ?? 0m;
            var sport = ResolveSport(summary, detail);
            var category = ResolveCategory(summary.SectionTitle, title);

            return new ProcessedGroupResult(
                new ScrapedActivityItem(
                    $"sls-goteborg-sportadmin-{summary.GroupId}",
                    title,
                    description,
                    Organizer,
                    location,
                    City,
                    ageRange.Value.AgeFrom,
                    ageRange.Value.AgeTo,
                    category,
                    activityDate,
                    price,
                    detailUrl,
                    string.Empty,
                    CreateRawPayload(source.EndpointUrl, groupsUrl, detailUrl, summary, detail, registrationStatus),
                    false,
                    sport,
                    ActivityListingType.Program,
                    registrationStatus,
                    registrationOpenAt,
                    registrationCloseAt,
                    detailUrl),
                []);
        }
        finally
        {
            detailSemaphore.Release();
        }
    }

    private async Task<string> ResolveFormIdAsync(string endpointUrl, CancellationToken cancellationToken)
    {
        var formId = TryExtractFormId(endpointUrl);

        if (!string.IsNullOrWhiteSpace(formId))
        {
            return formId;
        }

        var html = await GetHtmlAsync(endpointUrl, cancellationToken);
        var bookingLinkMatch = BookingLinkRegex.Match(html);

        return bookingLinkMatch.Success
            ? bookingLinkMatch.Groups["value"].Value
            : string.Empty;
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

    private static GroupSummary? ParseSummary(
        Match rowMatch,
        int rowEndIndex,
        MatchCollection sectionMatches,
        string groupsHtml)
    {
        if (!rowMatch.Success)
        {
            return null;
        }

        var rowHtml = groupsHtml[rowMatch.Index..rowEndIndex];
        var title = ExtractValue(rowHtml, "<h4>(?<value>.*?)</h4>");

        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var section = ResolveSection(rowMatch.Index, sectionMatches);
        var ageText = ExtractValue(
            rowHtml,
            "<div class=\"resp-small-label\">\\s*\u00c5lder:\\s*</div>\\s*(?<value>.*?)\\s*</div>");
        var placeText = ExtractValue(
            rowHtml,
            "<div class=\"resp-small-label\">\\s*Plats:\\s*</div>\\s*(?<value>.*?)\\s*</div>");
        var openText = ExtractValue(
            rowHtml,
            "<div class=\"resp-small-label\">\\s*\u00d6ppnas:\\s*</div>\\s*(?<value>.*?)\\s*</div>");
        var statusText = ExtractValue(
            rowHtml,
            "<div class=\"grupplist-statusbox [^\"]+\">\\s*(?<value>.*?)\\s*</div>");
        var spotsText = ExtractValue(
            rowHtml,
            "(?<value>\\d+)\\s+platser\\s+kvar");

        return new GroupSummary(
            rowMatch.Groups["id"].Value,
            title,
            section.Title,
            section.Description,
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
        var ageText = ExtractValue(
            html,
            "<div class=\"sa-card-titles\">\\s*\u00c5lder\\s*</div>\\s*<span>\\s*(?<value>.*?)\\s*</span>");
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
            "<div class=\"sa-card-titles\">\\s*Start(?:-/Slut)?datum\\s*</div>\\s*<div>\\s*(?<value>.*?)\\s*</div>");
        var closeAtText = ExtractValue(html, "St\u00e4nger\\s+(?<value>\\d{4}-\\d{2}-\\d{2}\\s+\\d{2}:\\d{2}:\\d{2})");
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
        var parsedDates = DateRegex.Matches(CleanText(datesText))
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (parsedDates.Count > 0 &&
            DateTime.TryParseExact(
                parsedDates[0],
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
            ageText,
            places,
            startDate,
            closeAt,
            price);
    }

    private static bool IsPotentiallyRelevant(GroupSummary summary)
    {
        var combined = NormalizeComparable(
            string.Join(
                " ",
                summary.SectionTitle,
                summary.SectionDescription,
                summary.Title));

        return !ContainsAny(
            combined,
            "intresseanmalan",
            "vuxna",
            "vuxenkurs",
            "vattensakerhetsutbildning vuxen",
            "senior",
            "motionssimning",
            "vattengymnastik");
    }

    private static bool IsChildTargeted(
        GroupSummary summary,
        GroupDetail detail,
        (int AgeFrom, int AgeTo)? ageRange)
    {
        if (ageRange is { } parsedAgeRange)
        {
            return parsedAgeRange.AgeFrom <= 19 && parsedAgeRange.AgeTo <= 19;
        }

        var combined = NormalizeComparable(
            string.Join(
                " ",
                summary.SectionTitle,
                summary.SectionDescription,
                summary.Title,
                detail.Title,
                detail.AgeText,
                detail.InformationText));

        if (ContainsAny(
                combined,
                "vuxna",
                "vuxenkurs",
                "senior",
                "65+",
                "motionssimning",
                "vattengymnastik",
                "vattensakerhetsutbildning vuxen"))
        {
            return false;
        }

        return ContainsAny(
            combined,
            "barn",
            "ungdom",
            "babysim",
            "simskola",
            "guldgrodan",
            "rakan",
            "krabban",
            "doppingen",
            "grodan",
            "salen",
            "bojen",
            "sjohasten",
            "sjolejonet",
            "sjostjarnan",
            "simlyftet");
    }

    private static (int AgeFrom, int AgeTo)? ResolveAgeRange(
        GroupSummary summary,
        GroupDetail detail,
        DateTime referenceDate)
    {
        var combined = NormalizeWhitespace(
            string.Join(
                " ",
                summary.AgeText,
                summary.SectionDescription,
                summary.Title,
                detail.Title,
                detail.AgeText,
                detail.InformationText));

        var birthYearRangeMatch = BirthYearRangeRegex.Match(combined);

        if (birthYearRangeMatch.Success &&
            int.TryParse(birthYearRangeMatch.Groups["from"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oldestBirthYear) &&
            int.TryParse(birthYearRangeMatch.Groups["to"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var youngestBirthYear))
        {
            return GetAgeRangeForBirthYears(oldestBirthYear, youngestBirthYear, referenceDate);
        }

        var singleBirthYears = BirthYearRegex.Matches(combined)
            .Select(match => int.TryParse(match.Groups["year"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
                ? year
                : 0)
            .Where(year => year > 0)
            .Distinct()
            .OrderBy(year => year)
            .ToList();

        if (singleBirthYears.Count > 0)
        {
            return GetAgeRangeForBirthYears(singleBirthYears[0], singleBirthYears[^1], referenceDate);
        }

        var monthRangeMatch = MonthRangeRegex.Match(combined);

        if (!monthRangeMatch.Success)
        {
            monthRangeMatch = MonthRangePrefixedRegex.Match(combined);
        }

        if (monthRangeMatch.Success &&
            int.TryParse(monthRangeMatch.Groups["from"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var monthFrom) &&
            int.TryParse(monthRangeMatch.Groups["to"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var monthTo))
        {
            return (Math.Min(monthFrom, monthTo) / 12, Math.Max(monthFrom, monthTo) / 12);
        }

        var ageRangeMatch = AgeRangeRegex.Match(combined);

        if (ageRangeMatch.Success &&
            int.TryParse(ageRangeMatch.Groups["from"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ageFrom) &&
            int.TryParse(ageRangeMatch.Groups["to"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ageTo))
        {
            return (Math.Min(ageFrom, ageTo), Math.Max(ageFrom, ageTo));
        }

        var ageFromMatch = AgeFromRegex.Match(combined);

        if (ageFromMatch.Success &&
            int.TryParse(ageFromMatch.Groups["from"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var openEndedAgeFrom))
        {
            return (openEndedAgeFrom, 99);
        }

        var ageSingleMatch = AgeSingleRegex.Match(combined);

        if (ageSingleMatch.Success &&
            int.TryParse(ageSingleMatch.Groups["age"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exactAge))
        {
            return (exactAge, exactAge);
        }

        return null;
    }

    private static (int AgeFrom, int AgeTo) GetAgeRangeForBirthYears(
        int oldestBirthYear,
        int youngestBirthYear,
        DateTime referenceDate)
    {
        var oldestRange = GetAgeRangeForBirthYear(oldestBirthYear, referenceDate);
        var youngestRange = GetAgeRangeForBirthYear(youngestBirthYear, referenceDate);
        return (youngestRange.AgeFrom, oldestRange.AgeTo);
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
        if (spotsLeft.HasValue && spotsLeft.Value <= 0)
        {
            return RegistrationStatus.Full;
        }

        if (ContainsAny(NormalizeComparable(statusText), "full", "fullbokad", "bevaka"))
        {
            return RegistrationStatus.Full;
        }

        if (ContainsAny(statusText, "\u00d6ppen"))
        {
            return ContainsAny(statusText, "Ej \u00f6ppen")
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

    private static DateTime? TryParseOpenAt(string value, DateTime activityDate)
    {
        var normalized = CleanText(value);

        if (string.IsNullOrWhiteSpace(normalized) || normalized == "-")
        {
            return null;
        }

        if (!DateTime.TryParseExact(
                $"{normalized}/{activityDate.Year}",
                "d/M HH:mm/yyyy",
                CultureInfo.GetCultureInfo("sv-SE"),
                DateTimeStyles.AssumeLocal,
                out var parsed))
        {
            return null;
        }

        var openAt = DateTime.SpecifyKind(parsed, DateTimeKind.Local);

        if (openAt > activityDate.AddMonths(6))
        {
            openAt = openAt.AddYears(-1);
        }
        else if (openAt < activityDate.AddMonths(-6))
        {
            openAt = openAt.AddYears(1);
        }

        return openAt;
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

    private static string ResolveTitle(GroupSummary summary, GroupDetail detail)
    {
        var title = string.IsNullOrWhiteSpace(detail.Title)
            ? summary.Title
            : detail.Title;
        var normalizedTitle = NormalizeComparable(title);
        var normalizedSectionTitle = NormalizeComparable(summary.SectionTitle);

        if (string.IsNullOrWhiteSpace(summary.SectionTitle) ||
            normalizedTitle.Contains(normalizedSectionTitle, StringComparison.Ordinal))
        {
            return title;
        }

        return LooksLikeScheduleTitle(title)
            ? $"{summary.SectionTitle} - {title}"
            : title;
    }

    private static bool LooksLikeScheduleTitle(string value)
    {
        var normalized = NormalizeComparable(value);
        return Regex.IsMatch(
            normalized,
            "^(man|tis|ons|tor|fre|lor|son|man-fre|tis-fre|\\d{1,2}:\\d{2})",
            RegexOptions.CultureInvariant);
    }

    private static string BuildDescription(string sectionDescription, string informationText)
    {
        var parts = new[]
            {
                NormalizeWhitespace(sectionDescription),
                NormalizeWhitespace(informationText)
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parts.Count == 0)
        {
            return "Barnaktivitet via SLS G\u00f6teborgs SportAdmin-bokning.";
        }

        var combined = string.Join(" ", parts);

        return combined.Length <= 320
            ? combined
            : $"{combined[..317].TrimEnd()}...";
    }

    private static string ResolveSport(GroupSummary summary, GroupDetail detail)
    {
        var combined = NormalizeComparable(
            string.Join(
                " ",
                summary.SectionTitle,
                summary.SectionDescription,
                detail.Title,
                detail.InformationText));

        return combined.Contains("livradd", StringComparison.Ordinal)
            ? "Livraddning"
            : "Simning";
    }

    private static string ResolveCategory(string sectionTitle, string title)
    {
        var combined = NormalizeComparable($"{sectionTitle} {title}");

        if (combined.Contains("livradd", StringComparison.Ordinal))
        {
            return "Livraddning";
        }

        if (combined.Contains("vattensakerhet", StringComparison.Ordinal))
        {
            return "Vattensakerhet";
        }

        return CleanText(sectionTitle);
    }

    private static SectionContext ResolveSection(int rowIndex, MatchCollection sectionMatches)
    {
        var title = "SportAdmin booking";
        var description = string.Empty;

        foreach (Match sectionMatch in sectionMatches)
        {
            if (sectionMatch.Index >= rowIndex)
            {
                break;
            }

            title = CleanText(sectionMatch.Groups["title"].Value);
            description = CleanText(sectionMatch.Groups["description"].Value);
        }

        return new SectionContext(title, description);
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
            SummarySectionDescription = summary.SectionDescription,
            SummaryAgeText = summary.AgeText,
            SummaryPlaceText = summary.PlaceText,
            SummaryOpenText = summary.OpenText,
            SummaryStatusText = summary.StatusText,
            SummarySpotsLeft = summary.SpotsLeft,
            DetailTitle = detail.Title,
            DetailStatusText = detail.StatusText,
            DetailInformationText = detail.InformationText,
            DetailAgeText = detail.AgeText,
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

    private static string NormalizeComparable(string value)
    {
        return NormalizeWhitespace(value)
            .ToLowerInvariant()
            .Replace("\u00e5", "a", StringComparison.Ordinal)
            .Replace("\u00e4", "a", StringComparison.Ordinal)
            .Replace("\u00f6", "o", StringComparison.Ordinal);
    }

    private sealed record SectionContext(string Title, string Description);

    private sealed record GroupSummary(
        string GroupId,
        string Title,
        string SectionTitle,
        string SectionDescription,
        string AgeText,
        string PlaceText,
        string OpenText,
        string StatusText,
        int? SpotsLeft);

    private sealed record GroupDetail(
        string Title,
        string StatusText,
        string InformationText,
        string AgeText,
        IReadOnlyList<string> Places,
        DateTime? StartDate,
        DateTime? CloseAt,
        decimal? Price);

    private sealed record ProcessedGroupResult(
        ScrapedActivityItem? Item,
        IReadOnlyList<string> Errors)
    {
        public static readonly ProcessedGroupResult Empty = new(null, []);
    }
}
