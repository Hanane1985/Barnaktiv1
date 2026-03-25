using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;
using Microsoft.Extensions.Configuration;

namespace Barnaktiv.Infrastructure.Configuration;

public sealed class IngestionSourceProvider(IConfiguration configuration)
    : IIngestionSourceProvider
{
    public IReadOnlyList<ConfiguredIngestionSource> GetSources()
    {
        return configuration
            .GetSection("Ingestion:Sources")
            .GetChildren()
            .Select(section => new ConfiguredIngestionSource(
                section["SourceKey"]?.Trim() ?? string.Empty,
                section["Name"]?.Trim() ?? string.Empty,
                section["ScraperKind"]?.Trim() ?? string.Empty,
                section["EndpointUrl"]?.Trim() ?? string.Empty,
                bool.TryParse(section["IsEnabled"], out var isEnabled) && isEnabled))
            .Where(source => !string.IsNullOrWhiteSpace(source.SourceKey))
            .ToList();
    }
}
