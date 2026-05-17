using Barnaktiv.Application.DTOs.Ingestion;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityIngestionService
{
    Task<IReadOnlyList<IngestionSourceDto>> GetSourcesAsync(CancellationToken cancellationToken);

    Task<IngestionRunDto> RunAsync(CancellationToken cancellationToken, string? sourceKey = null);
}
