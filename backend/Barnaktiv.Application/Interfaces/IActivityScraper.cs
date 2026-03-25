using Barnaktiv.Application.Models.Ingestion;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityScraper
{
    string Kind { get; }

    Task<ScrapeResult> ScrapeAsync(
        ConfiguredIngestionSource source,
        CancellationToken cancellationToken);
}
