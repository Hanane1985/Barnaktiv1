namespace Barnaktiv.Application.DTOs.Ingestion;

public sealed record IngestionSourceDto(
    string SourceKey,
    string Name,
    string ScraperKind,
    string EndpointUrl,
    bool IsEnabled);
