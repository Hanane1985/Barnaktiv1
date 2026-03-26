using Barnaktiv.Domain.Enums;

namespace Barnaktiv.Application.Models.Ingestion;

public sealed record ScrapedActivityItem(
    string ExternalId,
    string Title,
    string Description,
    string Organizer,
    string Location,
    string City,
    int AgeFrom,
    int AgeTo,
    string Category,
    DateTime Date,
    decimal Price,
    string WebsiteUrl,
    string ImageUrl,
    string RawPayload,
    bool IsPartial = false,
    string Sport = "",
    ActivityListingType ListingType = ActivityListingType.Event,
    RegistrationStatus RegistrationStatus = RegistrationStatus.Unknown,
    DateTime? RegistrationOpenAt = null,
    DateTime? RegistrationCloseAt = null,
    string SignupUrl = "");
