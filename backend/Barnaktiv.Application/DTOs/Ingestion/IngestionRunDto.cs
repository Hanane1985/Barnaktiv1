namespace Barnaktiv.Application.DTOs.Ingestion;

public sealed record IngestionRunDto(
    DateTime StartedAt,
    DateTime CompletedAt,
    int SourcesConfigured,
    int SourcesProcessed,
    int ActivitiesCreated,
    int ActivitiesUpdated,
    int PayloadsStored,
    IReadOnlyList<string> Errors);
