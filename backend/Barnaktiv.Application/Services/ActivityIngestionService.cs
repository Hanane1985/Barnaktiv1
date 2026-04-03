using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Barnaktiv.Application.DTOs.Ingestion;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Models.Ingestion;
using Barnaktiv.Domain.Entities;
using Barnaktiv.Domain.Enums;

namespace Barnaktiv.Application.Services;

public sealed class ActivityIngestionService(
    IIngestionSourceProvider sourceProvider,
    IEnumerable<IActivityScraper> scrapers,
    IActivityIngestionRepository repository) : IActivityIngestionService
{
    private const int SaveBatchSize = 100;

    public Task<IReadOnlyList<IngestionSourceDto>> GetSourcesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<IngestionSourceDto> sources = sourceProvider
            .GetSources()
            .Select(source => new IngestionSourceDto(
                source.SourceKey,
                source.Name,
                source.ScraperKind,
                source.EndpointUrl,
                source.IsEnabled,
                source.MaxPages))
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

            var seenExternalIds = (scrapeResult.DiscoveredExternalIds ?? scrapeResult.Items
                    .Select(item => item.ExternalId)
                    .ToList())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var existingActivitiesByExternalId = new Dictionary<string, Activity>(
                await repository.GetBySourceKeyAndExternalIdsAsync(
                    source.SourceKey,
                    seenExternalIds,
                    cancellationToken),
                StringComparer.OrdinalIgnoreCase);
            var pendingChanges = 0;

            foreach (var item in scrapeResult.Items)
            {
                var now = DateTime.UtcNow;

                if (!existingActivitiesByExternalId.TryGetValue(item.ExternalId, out var existingActivity))
                {
                    var activity = new Activity
                    {
                        SourceKey = source.SourceKey,
                        ExternalId = item.ExternalId,
                        CreatedAt = now,
                    };

                    ApplyItem(activity, item);
                    activity.Source = source.Name;
                    activity.UpdatedAt = now;
                    activity.LastSeenAt = now;

                    await repository.AddActivityAsync(activity, cancellationToken);
                    existingActivitiesByExternalId[item.ExternalId] = activity;
                    activitiesCreated++;
                }
                else
                {
                    ApplyItem(existingActivity, item);
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
                pendingChanges++;

                if (pendingChanges >= SaveBatchSize)
                {
                    await repository.SaveChangesAsync(cancellationToken);
                    pendingChanges = 0;
                }
            }

            if (seenExternalIds.Count > 0 && scrapeResult.CanRemoveMissingActivities)
            {
                await repository.RemoveActivitiesNotInExternalIdsAsync(
                    source.SourceKey,
                    seenExternalIds,
                    cancellationToken);
            }

            if (pendingChanges > 0 || (seenExternalIds.Count > 0 && scrapeResult.CanRemoveMissingActivities))
            {
                await repository.SaveChangesAsync(cancellationToken);
            }
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

    private static void ApplyItem(Activity activity, ScrapedActivityItem item)
    {
        activity.Title = PreferIncoming(item.Title, activity.Title);
        activity.Organizer = PreferIncoming(item.Organizer, activity.Organizer);
        activity.WebsiteUrl = PreferIncoming(item.WebsiteUrl, activity.WebsiteUrl);
        activity.SignupUrl = PreferIncoming(item.SignupUrl, activity.SignupUrl);
        activity.ImageUrl = PreferIncoming(item.ImageUrl, activity.ImageUrl);
        activity.Sport = PreferIncoming(item.Sport, activity.Sport);
        activity.ListingType = item.ListingType;
        activity.RegistrationStatus = PreferIncoming(
            item.RegistrationStatus,
            activity.RegistrationStatus);
        activity.RegistrationOpenAt = PreferIncoming(
            item.RegistrationOpenAt,
            activity.RegistrationOpenAt);
        activity.RegistrationCloseAt = PreferIncoming(
            item.RegistrationCloseAt,
            activity.RegistrationCloseAt);

        if (item.IsPartial)
        {
            activity.Description = FillIfMissing(activity.Description, item.Description);
            activity.Location = FillIfMissing(activity.Location, item.Location);
            activity.City = FillIfMissing(activity.City, item.City);
            activity.Category = FillIfMissing(activity.Category, item.Category);

            if (activity.Date == default && item.Date != default)
            {
                activity.Date = item.Date;
            }

            if (activity.Price == default || item.Price > 0m)
            {
                activity.Price = item.Price;
            }

            if ((activity.AgeFrom == 0 && activity.AgeTo == 0) ||
                item.AgeFrom > 0 ||
                item.AgeTo > 0)
            {
                activity.AgeFrom = item.AgeFrom;
                activity.AgeTo = item.AgeTo;
            }

            return;
        }

        activity.Description = PreferIncoming(item.Description, activity.Description);
        activity.Location = PreferIncoming(item.Location, activity.Location);
        activity.City = PreferIncoming(item.City, activity.City);
        activity.Category = PreferIncoming(item.Category, activity.Category);

        if (item.Date != default)
        {
            activity.Date = item.Date;
        }

        activity.Price = item.Price;
        activity.AgeFrom = item.AgeFrom;
        activity.AgeTo = item.AgeTo;
    }

    private static string ComputeHash(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string PreferIncoming(string incoming, string current)
    {
        return string.IsNullOrWhiteSpace(incoming)
            ? current
            : SanitizeText(incoming);
    }

    private static string FillIfMissing(string current, string incoming)
    {
        return string.IsNullOrWhiteSpace(current)
            ? SanitizeText(incoming)
            : current;
    }

    private static string SanitizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value;

        for (var index = 0; index < 2; index++)
        {
            var decoded = WebUtility.HtmlDecode(normalized);

            if (string.Equals(decoded, normalized, StringComparison.Ordinal))
            {
                break;
            }

            normalized = decoded;
        }

        normalized = normalized
            .Replace("\u00A0", " ", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

        return Regex.Replace(normalized, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static RegistrationStatus PreferIncoming(
        RegistrationStatus incoming,
        RegistrationStatus current)
    {
        return incoming == RegistrationStatus.Unknown
            ? current
            : incoming;
    }

    private static DateTime? PreferIncoming(DateTime? incoming, DateTime? current)
    {
        return incoming ?? current;
    }
}
