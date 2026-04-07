using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain.Entities;
using Barnaktiv.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Barnaktiv.Infrastructure.Repositories;

public sealed class ActivityIngestionRepository(ApplicationDbContext dbContext)
    : IActivityIngestionRepository
{
    private const int ExternalIdLookupBatchSize = 200;

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

        var matchingActivities = new List<Activity>();

        foreach (var externalIdBatch in externalIdSet.Chunk(ExternalIdLookupBatchSize))
        {
            var batchPredicate = BuildSourceAndExternalIdsPredicate(sourceKey, externalIdBatch);
            var batchActivities = await dbContext.Activities
                .Where(batchPredicate)
                .ToListAsync(cancellationToken);

            matchingActivities.AddRange(batchActivities);
        }

        return matchingActivities
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
        DateTime runStartedAtUtc,
        CancellationToken cancellationToken)
    {
        var externalIdSet = CreateExternalIdSet(externalIds);

        if (externalIdSet.Count == 0)
        {
            return;
        }

        await dbContext.Activities
            .Where(activity => activity.SourceKey == sourceKey)
            .Where(activity => activity.LastSeenAt == null || activity.LastSeenAt < runStartedAtUtc)
            .ExecuteDeleteAsync(cancellationToken);
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

    private static Expression<Func<Activity, bool>> BuildSourceAndExternalIdsPredicate(
        string sourceKey,
        string[] externalIds)
    {
        var activityParameter = Expression.Parameter(typeof(Activity), "activity");
        var sourceKeyProperty = Expression.Property(activityParameter, nameof(Activity.SourceKey));
        var externalIdProperty = Expression.Property(activityParameter, nameof(Activity.ExternalId));
        Expression? externalIdPredicate = null;

        foreach (var externalId in externalIds)
        {
            var equalsExternalId = Expression.Equal(
                externalIdProperty,
                Expression.Constant(externalId));

            externalIdPredicate = externalIdPredicate is null
                ? equalsExternalId
                : Expression.OrElse(externalIdPredicate, equalsExternalId);
        }

        if (externalIdPredicate is null)
        {
            return _ => false;
        }

        var sourceKeyPredicate = Expression.Equal(
            sourceKeyProperty,
            Expression.Constant(sourceKey));
        var body = Expression.AndAlso(sourceKeyPredicate, externalIdPredicate);

        return Expression.Lambda<Func<Activity, bool>>(body, activityParameter);
    }
}
