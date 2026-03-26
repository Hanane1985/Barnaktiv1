using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;
using Barnaktiv.Domain.Enums;

namespace Barnaktiv.Infrastructure.Scrapers;

public sealed partial class BKHackenStartPlayingScraper(HttpClient httpClient) : IActivityScraper
{
    private const string ContactEmail = "overgangar@bkhacken.se";
    private const string Organizer = "BK Häcken";
    private const string City = "Göteborg";
    private const string Location = "Slätta Damm";
    private static readonly CultureInfo SwedishCulture = CultureInfo.GetCultureInfo("sv-SE");
    private static readonly Regex SectionRegex = new(
        "<div class=inner\\s*><section id='(?<id>[^']*)'><span class=rub title='(?<title>[^']*)'>.*?</span></section><div[^>]*>(?<content>.*?)</div><div style=clear:both></div><div class=hr></div></div>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlTagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BirthYearRegex = new(
        "(?<group>Pojkar|Flickor)\\s+födda\\s+(?<year>\\d{4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex UpdatedDateRegex = new(
        "uppdaterades(?:\\s+senast)?\\s+(?<value>\\d{1,2}(?::a|:e)?\\s+[A-Za-zÅÄÖåäö]+\\s+\\d{4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex DateTimeRegex = new(
        "(?<day>\\d{1,2})(?::a|:e)?\\s+(?<month>[A-Za-zÅÄÖåäö]+)(?:\\s+(?<year>\\d{4}))?(?:\\s+klockan\\s+(?<time>\\d{1,2}:\\d{2}))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex PriceRegex = new(
        "Medlemsavgiften\\s+är\\s+(?<member>\\d+)[:-].*?träningsavgiften\\s+är\\s+(?<training>\\d+)[:-]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    public string Kind => "bk-hacken-start-playing-html-v1";

    public async Task<ScrapeResult> ScrapeAsync(
        ConfiguredIngestionSource source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source.EndpointUrl))
        {
            return new ScrapeResult([], [$"Source '{source.SourceKey}' has no endpoint URL."]);
        }

        using var response = await httpClient.GetAsync(source.EndpointUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new ScrapeResult(
                [],
                [$"GET {source.EndpointUrl} returned {(int)response.StatusCode} {response.ReasonPhrase}."]);
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var sections = ExtractSections(html);
        var errors = new List<string>();
        var items = new List<ScrapedActivityItem>();

        AddIfParsed(
            items,
            errors,
            TryCreate2020StartItem(source.EndpointUrl, sections));

        AddRangeIfParsed(
            items,
            errors,
            TryCreateEarlyYearsItems(source.EndpointUrl, sections));

        AddIfParsed(
            items,
            errors,
            TryCreateMiddleYearsItem(source.EndpointUrl, sections));

        AddIfParsed(
            items,
            errors,
            TryCreateTeenYearsItem(source.EndpointUrl, sections));

        AddIfParsed(
            items,
            errors,
            TryCreateNpfItem(source.EndpointUrl, sections));

        if (items.Count == 0)
        {
            errors.Add("No BK Häcken registration programs could be parsed from the page.");
        }

        return new ScrapeResult(
            items,
            errors,
            items.Select(item => item.ExternalId).ToList(),
            items.Count > 0);
    }

    private static Dictionary<string, SectionContent> ExtractSections(string html)
    {
        var sections = new Dictionary<string, SectionContent>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in SectionRegex.Matches(html))
        {
            var title = WebUtility.HtmlDecode(match.Groups["title"].Value).Trim();
            var contentHtml = match.Groups["content"].Value.Trim();
            var text = NormalizeWhitespace(StripHtml(contentHtml));
            var updatedAt = TryExtractUpdatedAt(text);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            sections[title] = new SectionContent(title, contentHtml, text, updatedAt);
        }

        return sections;
    }

    private static ParseResult TryCreate2020StartItem(
        string sourceUrl,
        IReadOnlyDictionary<string, SectionContent> sections)
    {
        const string sectionTitle = "Uppstart för pojkar och flickor födda 2020";

        if (!sections.TryGetValue(sectionTitle, out var section))
        {
            return ParseResult.FromError($"Missing expected BK Häcken section '{sectionTitle}'.");
        }

        var openAt = TryExtractDateTime(
            section.Text,
            "Den\\s+(?<value>\\d{1,2}(?::a|:e)?\\s+[A-Za-zÅÄÖåäö]+\\s+\\d{4}\\s+klockan\\s+\\d{1,2}:\\d{2})");
        var closeAt = TryExtractDateTime(
            section.Text,
            "till och med\\s+(?<value>\\d{1,2}(?::a|:e)?\\s+[A-Za-zÅÄÖåäö]+\\s+\\d{4}\\s+klockan\\s+\\d{1,2}:\\d{2})");

        if (openAt.HasValue && closeAt.HasValue && closeAt.Value < openAt.Value)
        {
            closeAt = closeAt.Value.AddYears(openAt.Value.Year - closeAt.Value.Year);
        }

        var registrationStatus = ResolveWindowStatus(
            section.Text,
            openAt,
            closeAt,
            "INTRESSEANMÄLAN ÄR NU STÄNGD",
            "INTRESSEANMÄLAN ÄR NU ÖPPEN");
        var date = openAt ?? section.UpdatedAt ?? DateTime.Today;
        var (ageFrom, ageTo) = GetAgeRangeForBirthYear(2020, date);
        var price = TryExtract2020Price(section.Text);
        var description =
            "BK Häcken öppnar uppstarten för barn födda 2020 via sin ungdomssida. Träningen startar i början av april på Slätta Damm efter en obligatorisk föräldrautbildning.";

        return ParseResult.Success(
            new ScrapedActivityItem(
                "bk-hacken-2020-start",
                "BK Häcken fotbollsuppstart för barn födda 2020",
                description,
                Organizer,
                Location,
                City,
                ageFrom,
                ageTo,
                "Börja spela",
                date,
                price,
                sourceUrl,
                string.Empty,
                CreateRawPayload(sourceUrl, section, registrationStatus, openAt, closeAt),
                false,
                "Fotboll",
                ActivityListingType.Program,
                registrationStatus,
                openAt,
                closeAt,
                string.Empty));
    }

    private static ParseResults TryCreateEarlyYearsItems(
        string sourceUrl,
        IReadOnlyDictionary<string, SectionContent> sections)
    {
        const string sectionTitle = "Information gällande barn födda mellan 2017-2019";

        if (!sections.TryGetValue(sectionTitle, out var section))
        {
            return ParseResults.FromError($"Missing expected BK Häcken section '{sectionTitle}'.");
        }

        var results = new List<ScrapedActivityItem>();
        var updatedAt = section.UpdatedAt ?? DateTime.Today;
        var registrationClosed = section.Text.Contains(
            "Intresseanmälan är nu stängd",
            StringComparison.OrdinalIgnoreCase);
        var closeAt = TryExtractDateTime(
            section.Text,
            "fram till fredag\\s+(?<value>\\d{1,2}\\s+[A-Za-zÅÄÖåäö]+\\s+klockan\\s+\\d{1,2}:\\d{2})",
            updatedAt.Year);
        DateTime? openAt = closeAt.HasValue
            ? ISOWeek.ToDateTime(closeAt.Value.Year, 8, DayOfWeek.Monday)
            : null;
        var openSectionText = TryExtractBetween(
            section.Text,
            "Följande årskullar tar emot intresseanmälan under vecka 8:",
            "Följande årskullar är fulltecknade:");
        var fullSectionText = TryExtractBetween(
            section.Text,
            "Följande årskullar är fulltecknade:",
            "Intresseanmälan");

        foreach (Match match in BirthYearRegex.Matches(openSectionText))
        {
            var group = NormalizeWhitespace(match.Groups["group"].Value);
            var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            var cohortDate = openAt ?? closeAt ?? updatedAt;
            var (ageFrom, ageTo) = GetAgeRangeForBirthYear(year, cohortDate);
            var status = registrationClosed ? RegistrationStatus.Closed : RegistrationStatus.Open;
            var title = $"BK Häcken {group.ToLowerInvariant()} födda {year}";
            var description =
                $"{group} födda {year} tas emot via BK Häckens intresseanmälan för barn 6-9 år när årskullen har plats. Kontakt sker via {ContactEmail}.";

            results.Add(new ScrapedActivityItem(
                $"bk-hacken-{group.ToLowerInvariant()}-{year}",
                title,
                description,
                Organizer,
                Location,
                City,
                ageFrom,
                ageTo,
                "Börja spela",
                openAt ?? closeAt ?? updatedAt,
                0m,
                sourceUrl,
                string.Empty,
                CreateRawPayload(sourceUrl, section, status, openAt, closeAt, group, year),
                false,
                "Fotboll",
                ActivityListingType.Program,
                status,
                openAt,
                closeAt,
                status == RegistrationStatus.Open ? $"mailto:{ContactEmail}" : string.Empty));
        }

        foreach (Match match in BirthYearRegex.Matches(fullSectionText))
        {
            var group = NormalizeWhitespace(match.Groups["group"].Value);
            var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            var (ageFrom, ageTo) = GetAgeRangeForBirthYear(year, updatedAt);
            var title = $"BK Häcken {group.ToLowerInvariant()} födda {year}";
            var description =
                $"{group} födda {year} är fulltecknad hos BK Häcken just nu. Klubben uppdaterar sidan i februari och augusti om platser öppnar.";

            results.Add(new ScrapedActivityItem(
                $"bk-hacken-{group.ToLowerInvariant()}-{year}-full",
                title,
                description,
                Organizer,
                Location,
                City,
                ageFrom,
                ageTo,
                "Börja spela",
                updatedAt,
                0m,
                sourceUrl,
                string.Empty,
                CreateRawPayload(
                    sourceUrl,
                    section,
                    RegistrationStatus.Full,
                    null,
                    null,
                    group,
                    year),
                false,
                "Fotboll",
                ActivityListingType.Program,
                RegistrationStatus.Full,
                null,
                null,
                string.Empty));
        }

        return results.Count == 0
            ? ParseResults.FromError("BK Häcken 2017-2019 section was found, but no cohorts could be parsed.")
            : ParseResults.Success(results);
    }

    private static ParseResult TryCreateMiddleYearsItem(
        string sourceUrl,
        IReadOnlyDictionary<string, SectionContent> sections)
    {
        const string sectionTitle = "Information gällande barn födda mellan 2014-2016";

        if (!sections.TryGetValue(sectionTitle, out var section))
        {
            return ParseResult.FromError($"Missing expected BK Häcken section '{sectionTitle}'.");
        }

        var date = section.UpdatedAt ?? DateTime.Today;
        var (ageFrom, ageTo) = GetAgeRangeForBirthYears(2014, 2016, date);
        var description =
            "BK Häcken tar inte emot direkt intresseanmälan för spelare födda 2014-2016. Barnet måste gå via sin nuvarande klubb för dialog om övergång.";

        return ParseResult.Success(
            new ScrapedActivityItem(
                "bk-hacken-2014-2016",
                "BK Häcken barn födda 2014-2016",
                description,
                Organizer,
                Location,
                City,
                ageFrom,
                ageTo,
                "Börja spela",
                date,
                0m,
                sourceUrl,
                string.Empty,
                CreateRawPayload(sourceUrl, section, RegistrationStatus.Closed, null, null),
                false,
                "Fotboll",
                ActivityListingType.Program,
                RegistrationStatus.Closed,
                null,
                null,
                string.Empty));
    }

    private static ParseResult TryCreateTeenYearsItem(
        string sourceUrl,
        IReadOnlyDictionary<string, SectionContent> sections)
    {
        const string sectionTitle = "Information gällande ungdomar födda mellan 2007-2013";

        if (!sections.TryGetValue(sectionTitle, out var section))
        {
            return ParseResult.FromError($"Missing expected BK Häcken section '{sectionTitle}'.");
        }

        var date = section.UpdatedAt ?? DateTime.Today;
        var (ageFrom, ageTo) = GetAgeRangeForBirthYears(2007, 2013, date);
        var description =
            "BK Häckens ungdoms- och akademispår för spelare födda 2007-2013 har ingen öppen anmälan. Övergångar går via nuvarande klubb eller klubbens scouting.";

        return ParseResult.Success(
            new ScrapedActivityItem(
                "bk-hacken-2007-2013",
                "BK Häcken ungdomar födda 2007-2013",
                description,
                Organizer,
                Location,
                City,
                ageFrom,
                ageTo,
                "Börja spela",
                date,
                0m,
                sourceUrl,
                string.Empty,
                CreateRawPayload(sourceUrl, section, RegistrationStatus.Closed, null, null),
                false,
                "Fotboll",
                ActivityListingType.Program,
                RegistrationStatus.Closed,
                null,
                null,
                string.Empty));
    }

    private static ParseResult TryCreateNpfItem(
        string sourceUrl,
        IReadOnlyDictionary<string, SectionContent> sections)
    {
        const string sectionTitle = "Fotboll för alla - Barn och ungdomar med NPF";

        if (!sections.TryGetValue(sectionTitle, out var section))
        {
            return ParseResult.FromError($"Missing expected BK Häcken section '{sectionTitle}'.");
        }

        var updatedAt = section.UpdatedAt ?? DateTime.Today;
        var status = section.Text.Contains("fulltecknad", StringComparison.OrdinalIgnoreCase)
            ? RegistrationStatus.Full
            : RegistrationStatus.Unknown;
        var description =
            "Fotboll för alla är BK Häckens anpassade fotbollsverksamhet för barn och ungdomar med NPF. Gruppen är enligt klubben fulltecknad just nu.";

        return ParseResult.Success(
            new ScrapedActivityItem(
                "bk-hacken-fotboll-for-alla-npf",
                "BK Häcken Fotboll för alla (NPF)",
                description,
                Organizer,
                Location,
                City,
                6,
                19,
                "Anpassad träning",
                updatedAt,
                0m,
                sourceUrl,
                string.Empty,
                CreateRawPayload(sourceUrl, section, status, null, null),
                false,
                "Fotboll",
                ActivityListingType.Program,
                status,
                null,
                null,
                string.Empty));
    }

    private static RegistrationStatus ResolveWindowStatus(
        string text,
        DateTime? openAt,
        DateTime? closeAt,
        string closedMarker,
        string openMarker)
    {
        if (text.Contains(closedMarker, StringComparison.OrdinalIgnoreCase))
        {
            return RegistrationStatus.Closed;
        }

        if (text.Contains("fulltecknad", StringComparison.OrdinalIgnoreCase))
        {
            return RegistrationStatus.Full;
        }

        if (text.Contains(openMarker, StringComparison.OrdinalIgnoreCase))
        {
            return RegistrationStatus.Open;
        }

        var now = DateTime.UtcNow;

        if (openAt.HasValue && now < openAt.Value.ToUniversalTime())
        {
            return RegistrationStatus.Upcoming;
        }

        if (openAt.HasValue && closeAt.HasValue && now >= openAt.Value.ToUniversalTime() && now <= closeAt.Value.ToUniversalTime())
        {
            return RegistrationStatus.Open;
        }

        if (closeAt.HasValue && now > closeAt.Value.ToUniversalTime())
        {
            return RegistrationStatus.Closed;
        }

        return RegistrationStatus.Unknown;
    }

    private static DateTime? TryExtractUpdatedAt(string text)
    {
        var match = UpdatedDateRegex.Match(text);
        return match.Success
            ? ParseSwedishDateTime(match.Groups["value"].Value, null)
            : null;
    }

    private static DateTime? TryExtractDateTime(
        string text,
        string pattern,
        int? defaultYear = null)
    {
        var match = Regex.Match(
            text,
            pattern,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        return match.Success
            ? ParseSwedishDateTime(match.Groups["value"].Value, defaultYear)
            : null;
    }

    private static DateTime? ParseSwedishDateTime(string value, int? defaultYear)
    {
        var match = DateTimeRegex.Match(NormalizeWhitespace(value));

        if (!match.Success ||
            !int.TryParse(match.Groups["day"].Value, out var day) ||
            !TryGetMonth(match.Groups["month"].Value, out var month))
        {
            return null;
        }

        var year = match.Groups["year"].Success && int.TryParse(match.Groups["year"].Value, out var parsedYear)
            ? parsedYear
            : defaultYear;

        if (!year.HasValue)
        {
            return null;
        }

        var hour = 0;
        var minute = 0;

        if (match.Groups["time"].Success)
        {
            var timeParts = match.Groups["time"].Value.Split(':', StringSplitOptions.RemoveEmptyEntries);

            if (timeParts.Length != 2 ||
                !int.TryParse(timeParts[0], out hour) ||
                !int.TryParse(timeParts[1], out minute))
            {
                return null;
            }
        }

        try
        {
            return new DateTime(year.Value, month, day, hour, minute, 0, DateTimeKind.Local);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool TryGetMonth(string monthName, out int month)
    {
        month = 0;
        var normalized = monthName.Trim().TrimEnd('.').ToLower(SwedishCulture);
        var monthNames = SwedishCulture.DateTimeFormat.MonthNames;
        var abbreviatedMonthNames = SwedishCulture.DateTimeFormat.AbbreviatedMonthNames;

        for (var index = 0; index < 12; index++)
        {
            if (string.Equals(monthNames[index], normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(abbreviatedMonthNames[index], normalized, StringComparison.OrdinalIgnoreCase))
            {
                month = index + 1;
                return true;
            }
        }

        return false;
    }

    private static decimal TryExtract2020Price(string text)
    {
        var match = PriceRegex.Match(text);

        return match.Success &&
               decimal.TryParse(
                   match.Groups["member"].Value,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out var memberPrice) &&
               decimal.TryParse(
                   match.Groups["training"].Value,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out var trainingPrice)
            ? memberPrice + trainingPrice
            : 0m;
    }

    private static (int AgeFrom, int AgeTo) GetAgeRangeForBirthYear(int birthYear, DateTime referenceDate)
    {
        var ageFrom = Math.Max(0, referenceDate.Year - birthYear - 1);
        var ageTo = Math.Max(ageFrom, referenceDate.Year - birthYear);
        return (ageFrom, ageTo);
    }

    private static (int AgeFrom, int AgeTo) GetAgeRangeForBirthYears(
        int oldestBirthYear,
        int youngestBirthYear,
        DateTime referenceDate)
    {
        var youngestRange = GetAgeRangeForBirthYear(youngestBirthYear, referenceDate);
        var oldestRange = GetAgeRangeForBirthYear(oldestBirthYear, referenceDate);
        return (youngestRange.AgeFrom, oldestRange.AgeTo);
    }

    private static string TryExtractBetween(string text, string startMarker, string endMarker)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0)
        {
            return string.Empty;
        }

        startIndex += startMarker.Length;
        var endIndex = text.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);

        return endIndex < 0
            ? text[startIndex..]
            : text[startIndex..endIndex];
    }

    private static string CreateRawPayload(
        string sourceUrl,
        SectionContent section,
        RegistrationStatus status,
        DateTime? openAt,
        DateTime? closeAt,
        string? group = null,
        int? birthYear = null)
    {
        return JsonSerializer.Serialize(new
        {
            sourceUrl,
            section.Title,
            section.Html,
            section.Text,
            section.UpdatedAt,
            status,
            openAt,
            closeAt,
            group,
            birthYear
        });
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
        return Regex.Replace(value, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static void AddIfParsed(
        ICollection<ScrapedActivityItem> items,
        ICollection<string> errors,
        ParseResult result)
    {
        if (result.Item is not null)
        {
            items.Add(result.Item);
        }

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            errors.Add(result.Error);
        }
    }

    private static void AddRangeIfParsed(
        ICollection<ScrapedActivityItem> items,
        ICollection<string> errors,
        ParseResults results)
    {
        foreach (var item in results.Items)
        {
            items.Add(item);
        }

        if (!string.IsNullOrWhiteSpace(results.Error))
        {
            errors.Add(results.Error);
        }
    }

    private sealed record SectionContent(
        string Title,
        string Html,
        string Text,
        DateTime? UpdatedAt);

    private sealed record ParseResult(
        ScrapedActivityItem? Item,
        string? Error)
    {
        public static ParseResult Success(ScrapedActivityItem item) => new(item, null);

        public static ParseResult FromError(string error) => new(null, error);
    }

    private sealed record ParseResults(
        IReadOnlyList<ScrapedActivityItem> Items,
        string? Error)
    {
        public static ParseResults Success(IReadOnlyList<ScrapedActivityItem> items) => new(items, null);

        public static ParseResults FromError(string error) => new([], error);
    }
}
