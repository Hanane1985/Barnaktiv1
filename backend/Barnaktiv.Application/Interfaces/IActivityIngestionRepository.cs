using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityIngestionRepository
{
    Task<Activity?> GetBySourceKeyAndExternalIdAsync(
        string sourceKey,
        string externalId,
        CancellationToken cancellationToken);

    Task AddActivityAsync(Activity activity, CancellationToken cancellationToken);

    Task AddRawPayloadAsync(RawActivityPayload rawPayload, CancellationToken cancellationToken);

    Task RemoveActivitiesNotInExternalIdsAsync(
        string sourceKey,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
