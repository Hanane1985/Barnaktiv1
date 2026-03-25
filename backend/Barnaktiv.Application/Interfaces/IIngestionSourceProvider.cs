using Barnaktiv.Application.Models.Ingestion;

namespace Barnaktiv.Application.Interfaces;

public interface IIngestionSourceProvider
{
    IReadOnlyList<ConfiguredIngestionSource> GetSources();
}
