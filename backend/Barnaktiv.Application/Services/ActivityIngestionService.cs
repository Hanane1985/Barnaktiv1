using System.Security.Cryptography;
using System.Text;
using Barnaktiv.Application.DTOs.Ingestion;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;
using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Services;

public sealed class ActivityIngestionService(
    IIngestionSourceProvider sourceProvider,
    IEnumerable<IActivityScraper> scrapers,
    IActivityIngestionRepository repository) : IActivityIngestionService
{
    public Task<IReadOnlyList<IngestionSourceDto>> GetSourcesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IngestionSourceDto> sources = sourceProvider
            .GetSources()
            .Select(source => new IngestionSourceDto(
                source.SourceKey,
                source.Name,
                source.ScraperKind,
                source.EndpointUrl,
                source.IsEnabled))
            .ToList();

        return Task.FromResult(sources);
    }

    public async Task<IngestionRunDto> RunAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var configuredSources = sourceProvider.GetSources();
        var enabledSources = configuredSources.Where(source => source.IsEnabled).ToList();
        var scraperMap = scrapers.ToDictionary(
            scraper => scraper.Kind,
            StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var activitiesCreated = 0;
        var activitiesUpdated = 0;
        var payloadsStored = 0;
        var sourcesProcessed = 0;

        foreach (var source in enabledSources)
        {
            if (!scraperMap.TryGetValue(source.ScraperKind, out var scraper))
            {
                errors.Add(
                    $"[{source.SourceKey}] No scraper is registered for '{source.ScraperKind}'.");
                continue;
            }

            ScrapeResult scrapeResult;

            try
            {
                scrapeResult = await scraper.ScrapeAsync(source, cancellationToken);
            }
            catch (Exception exception)
            {
                errors.Add($"[{source.SourceKey}] {exception.Message}");
                continue;
            }

            sourcesProcessed++;

            foreach (var scrapeError in scrapeResult.Errors)
            {
                errors.Add($"[{source.SourceKey}] {scrapeError}");
            }

            foreach (var item in scrapeResult.Items)
            {
                var now = DateTime.UtcNow;
                var existingActivity = await repository.GetBySourceKeyAndExternalIdAsync(
                    source.SourceKey,
                    item.ExternalId,
                    cancellationToken);

                if (existingActivity is null)
                {
                    var activity = new Activity
                    {
                        SourceKey = source.SourceKey,
                        ExternalId = item.ExternalId,
                        Title = item.Title,
                        Description = item.Description,
                        Organizer = item.Organizer,
                        Location = item.Location,
                        City = item.City,
                        AgeFrom = item.AgeFrom,
                        AgeTo = item.AgeTo,
                        Category = item.Category,
                        Date = item.Date,
                        Price = item.Price,
                        WebsiteUrl = item.WebsiteUrl,
                        ImageUrl = item.ImageUrl,
                        Source = source.Name,
                        CreatedAt = now,
                        UpdatedAt = now,
                        LastSeenAt = now,
                    };

                    await repository.AddActivityAsync(activity, cancellationToken);
                    activitiesCreated++;
                }
                else
                {
                    existingActivity.Title = item.Title;
                    existingActivity.Description = item.Description;
                    existingActivity.Organizer = item.Organizer;
                    existingActivity.Location = item.Location;
                    existingActivity.City = item.City;
                    existingActivity.AgeFrom = item.AgeFrom;
                    existingActivity.AgeTo = item.AgeTo;
                    existingActivity.Category = item.Category;
                    existingActivity.Date = item.Date;
                    existingActivity.Price = item.Price;
                    existingActivity.WebsiteUrl = item.WebsiteUrl;
                    existingActivity.ImageUrl = item.ImageUrl;
                    existingActivity.Source = source.Name;
                    existingActivity.UpdatedAt = now;
                    existingActivity.LastSeenAt = now;
                    activitiesUpdated++;
                }

                var rawPayload = new RawActivityPayload
                {
                    SourceKey = source.SourceKey,
                    ExternalId = item.ExternalId,
                    ContentHash = ComputeHash(item.RawPayload),
                    Payload = item.RawPayload,
                    FetchedAt = now,
                    ImportedAt = now,
                };

                await repository.AddRawPayloadAsync(rawPayload, cancellationToken);
                payloadsStored++;
            }

            await repository.SaveChangesAsync(cancellationToken);
        }

        return new IngestionRunDto(
            startedAt,
            DateTime.UtcNow,
            configuredSources.Count,
            sourcesProcessed,
            activitiesCreated,
            activitiesUpdated,
            payloadsStored,
            errors);
    }

    private static string ComputeHash(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
