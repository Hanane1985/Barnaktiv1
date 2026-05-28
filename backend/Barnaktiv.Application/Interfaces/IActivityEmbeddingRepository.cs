namespace Barnaktiv.Application.Interfaces;

public interface IActivityEmbeddingRepository
{
    Task<IReadOnlyDictionary<Guid, StoredActivityEmbedding>> GetByActivityIdsAsync(
        IReadOnlyCollection<Guid> activityIds,
        CancellationToken cancellationToken);

    Task UpsertAsync(
        IReadOnlyList<ActivityEmbeddingUpsertItem> items,
        CancellationToken cancellationToken);
}

public sealed record StoredActivityEmbedding(
    Guid ActivityId,
    string ContentHash,
    float[] Vector);

public sealed record ActivityEmbeddingUpsertItem(
    Guid ActivityId,
    string ContentHash,
    float[] Vector);
