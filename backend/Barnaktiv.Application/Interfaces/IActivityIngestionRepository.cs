using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityIngestionRepository
{
    Task<IReadOnlyDictionary<string, Activity>> GetBySourceKeyAndExternalIdsAsync(
        string sourceKey,
        IReadOnlyCollection<string> externalIds,
        CancellationToken cancellationToken);

    Task AddActivityAsync(Activity activity, CancellationToken cancellationToken);

    Task AddRawPayloadAsync(RawActivityPayload rawPayload, CancellationToken cancellationToken);

    Task RemoveActivitiesNotInExternalIdsAsync(
        string sourceKey,
        IReadOnlyCollection<string> keepExternalIds,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);
}
