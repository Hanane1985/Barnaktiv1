namespace Barnaktiv.Application.DTOs.Activities;

/// <summary>Public list item for GET /api/activities (camelCase JSON). Kept aligned with <c>frontend/web/lib/activity-api-schema.ts</c>.</summary>
public sealed record ActivityDto(
    Guid Id,
    string Title,
    string Description,
    string Organizer,
    string Location,
    string City,
    int AgeFrom,
    int AgeTo,
    string Sport,
    string Category,
    string ListingType,
    DateTime Date,
    decimal Price,
    string WebsiteUrl,
    string SignupUrl,
    string ImageUrl,
    string Source,
    string RegistrationStatus,
    DateTime? RegistrationOpenAt,
    DateTime? RegistrationCloseAt,
    DateTime CreatedAt);
