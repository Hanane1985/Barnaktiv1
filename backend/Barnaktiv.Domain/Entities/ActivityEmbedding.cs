using Barnaktiv.Domain.Common;

namespace Barnaktiv.Domain.Entities;

public class ActivityEmbedding : Entity
{
    public Guid ActivityId { get; set; }

    public Activity Activity { get; set; } = null!;

    public string ContentHash { get; set; } = string.Empty;

    public string VectorJson { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
