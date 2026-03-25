namespace Barnaktiv.Application.Models.Ingestion;

public sealed record ConfiguredIngestionSource(
    string SourceKey,
    string Name,
    string ScraperKind,
    string EndpointUrl,
    bool IsEnabled);
