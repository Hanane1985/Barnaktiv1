namespace Barnaktiv.Application.DTOs.Ingestion;

public sealed record IngestionJobDto(
    Guid JobId,
    string Status,
    string? SourceKey,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IngestionRunDto? Result,
    string? Error);
