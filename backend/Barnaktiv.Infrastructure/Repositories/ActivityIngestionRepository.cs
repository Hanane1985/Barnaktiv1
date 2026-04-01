using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain.Entities;
using Barnaktiv.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Repositories;

public sealed class ActivityIngestionRepository(ApplicationDbContext dbContext)
    : IActivityIngestionRepository
{
    public async Task<IReadOnlyDictionary<string, Activity>> GetBySourceKeyAndExternalIdsAsync(
        string sourceKey,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken)
    {
        var externalIdSet = CreateExternalIdSet(externalIds);

        if (externalIdSet.Count == 0)
        {
            return new Dictionary<string, Activity>(StringComparer.OrdinalIgnoreCase);
        }

        // Loading one source at a time avoids translating large external-id sets into
        // expensive SQL `IN`/`OPENJSON` predicates, which was timing out during ingestion.
        var sourceActivities = await dbContext.Activities
            .Where(activity => activity.SourceKey == sourceKey)
            .ToListAsync(cancellationToken);

        return sourceActivities
            .Where(activity => externalIdSet.Contains(activity.ExternalId))
            .ToDictionary(
                activity => activity.ExternalId,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task AddActivityAsync(Activity activity, CancellationToken cancellationToken)
    {
        await dbContext.Activities.AddAsync(activity, cancellationToken);
    }

    public async Task AddRawPayloadAsync(
        RawActivityPayload rawPayload,
        CancellationToken cancellationToken)
    {
        await dbContext.RawActivityPayloads.AddAsync(rawPayload, cancellationToken);
    }

    public async Task RemoveActivitiesNotInExternalIdsAsync(
        string sourceKey,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken)
    {
        var externalIdSet = CreateExternalIdSet(externalIds);
        var sourceActivities = await dbContext.Activities
            .Where(activity => activity.SourceKey == sourceKey)
            .ToListAsync(cancellationToken);
        var activitiesToRemove = sourceActivities
            .Where(activity => !externalIdSet.Contains(activity.ExternalId))
            .ToList();

        if (activitiesToRemove.Count == 0)
        {
            return;
        }

        dbContext.Activities.RemoveRange(activitiesToRemove);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static HashSet<string> CreateExternalIdSet(IReadOnlyCollection<string> externalIds)
    {
        return externalIds
            .Where(externalId => !string.IsNullOrWhiteSpace(externalId))
            .Select(externalId => externalId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
