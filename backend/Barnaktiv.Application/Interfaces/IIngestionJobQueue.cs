using Barnaktiv.Application.DTOs.Ingestion;

namespace Barnaktiv.Application.Interfaces;

public interface IIngestionJobQueue
{
    ValueTask<IngestionJobDto> EnqueueAsync(string? sourceKey, CancellationToken cancellationToken = default);

    IngestionJobDto? TryGetJob(Guid jobId);
}
