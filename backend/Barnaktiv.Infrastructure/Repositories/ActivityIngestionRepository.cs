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
        if (externalIds.Count == 0)
        {
            return new Dictionary<string, Activity>(StringComparer.OrdinalIgnoreCase);
        }

        var activitiesByExternalId = dbContext.Activities.Local
            .Where(activity => activity.SourceKey == sourceKey)
            .Where(activity => externalIds.Contains(activity.ExternalId))
            .ToDictionary(
                activity => activity.ExternalId,
                StringComparer.OrdinalIgnoreCase);
        var remainingExternalIds = externalIds
            .Where(externalId => !activitiesByExternalId.ContainsKey(externalId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var externalIdChunk in remainingExternalIds.Chunk(500))
        {
            var chunkActivities = await dbContext.Activities
                .Where(activity => activity.SourceKey == sourceKey)
                .Where(activity => externalIdChunk.Contains(activity.ExternalId))
                .ToListAsync(cancellationToken);

            foreach (var activity in chunkActivities)
            {
                activitiesByExternalId[activity.ExternalId] = activity;
            }
        }

        return activitiesByExternalId;
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
        var activitiesToRemove = await dbContext.Activities
            .Where(activity => activity.SourceKey == sourceKey)
            .Where(activity => !externalIds.Contains(activity.ExternalId))
            .ToListAsync(cancellationToken);

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
}
