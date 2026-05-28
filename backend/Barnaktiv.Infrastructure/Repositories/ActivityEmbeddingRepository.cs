using System.Text.Json;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain.Entities;
using Barnaktiv.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Repositories;

public sealed class ActivityEmbeddingRepository(ApplicationDbContext dbContext) : IActivityEmbeddingRepository
{
    public async Task<IReadOnlyDictionary<Guid, StoredActivityEmbedding>> GetByActivityIdsAsync(
        IReadOnlyCollection<Guid> activityIds,
        CancellationToken cancellationToken)
    {
        if (activityIds.Count == 0)
        {
            return new Dictionary<Guid, StoredActivityEmbedding>();
        }

        var ids = activityIds.Distinct().ToList();
        var embeddings = await dbContext.ActivityEmbeddings
            .AsNoTracking()
            .Where(embedding => ids.Contains(embedding.ActivityId))
            .ToListAsync(cancellationToken);

        return embeddings.ToDictionary(
            embedding => embedding.ActivityId,
            embedding => new StoredActivityEmbedding(
                embedding.ActivityId,
                embedding.ContentHash,
                ParseVector(embedding.VectorJson)));
    }

    public async Task UpsertAsync(
        IReadOnlyList<ActivityEmbeddingUpsertItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var ids = items
            .Select(item => item.ActivityId)
            .Distinct()
            .ToList();
        var existing = await dbContext.ActivityEmbeddings
            .Where(embedding => ids.Contains(embedding.ActivityId))
            .ToDictionaryAsync(embedding => embedding.ActivityId, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            if (existing.TryGetValue(item.ActivityId, out var row))
            {
                row.ContentHash = item.ContentHash;
                row.VectorJson = JsonSerializer.Serialize(item.Vector);
                row.UpdatedAt = now;
                continue;
            }

            await dbContext.ActivityEmbeddings.AddAsync(
                new ActivityEmbedding
                {
                    ActivityId = item.ActivityId,
                    ContentHash = item.ContentHash,
                    VectorJson = JsonSerializer.Serialize(item.Vector),
                    UpdatedAt = now,
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static float[] ParseVector(string vectorJson)
    {
        if (string.IsNullOrWhiteSpace(vectorJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<float[]>(vectorJson) ?? [];
    }
}
