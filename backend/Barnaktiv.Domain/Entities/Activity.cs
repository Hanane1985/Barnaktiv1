using Barnaktiv.Domain.Common;
using Barnaktiv.Domain.Enums;

namespace Barnaktiv.Domain.Entities;

public class Activity : Entity
{
    public string SourceKey { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Organizer { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public int AgeFrom { get; set; }

    public int AgeTo { get; set; }

    public string Sport { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public ActivityListingType ListingType { get; set; } = ActivityListingType.Event;

    public DateTime Date { get; set; }

    public decimal Price { get; set; }

    public string WebsiteUrl { get; set; } = string.Empty;

    public string SignupUrl { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public RegistrationStatus RegistrationStatus { get; set; } = RegistrationStatus.Unknown;

    public DateTime? RegistrationOpenAt { get; set; }

    public DateTime? RegistrationCloseAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastSeenAt { get; set; }
}
