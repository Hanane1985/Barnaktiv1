using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain.Entities;
using Barnaktiv.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Repositories;

public sealed class ActivityIngestionRepository(ApplicationDbContext dbContext)
    : IActivityIngestionRepository
{
    public async Task<Activity?> GetBySourceKeyAndExternalIdAsync(
        string sourceKey,
        string externalId,
        CancellationToken cancellationToken)
    {
        var trackedActivity = dbContext.Activities.Local.FirstOrDefault(activity =>
            activity.SourceKey == sourceKey && activity.ExternalId == externalId);

        if (trackedActivity is not null)
        {
            return trackedActivity;
        }

        return await dbContext.Activities.FirstOrDefaultAsync(activity =>
            activity.SourceKey == sourceKey &&
            activity.ExternalId == externalId,
            cancellationToken);
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

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
