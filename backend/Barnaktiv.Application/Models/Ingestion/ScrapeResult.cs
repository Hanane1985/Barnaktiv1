namespace Barnaktiv.Application.Models.Ingestion;

public sealed record ScrapeResult(
    IReadOnlyList<ScrapedActivityItem> Items,
    IReadOnlyList<string> Errors,
    IReadOnlyCollection<string>? DiscoveredExternalIds = null,
    bool CanRemoveMissingActivities = false);
