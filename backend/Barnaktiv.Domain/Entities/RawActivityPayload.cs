using Barnaktiv.Domain.Common;

namespace Barnaktiv.Domain.Entities;

public class RawActivityPayload : Entity
{
    public string SourceKey { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ImportedAt { get; set; }

    public string? ErrorMessage { get; set; }
}
