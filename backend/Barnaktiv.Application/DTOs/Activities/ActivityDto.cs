namespace Barnaktiv.Application.DTOs.Activities;

public sealed record ActivityDto(
    Guid Id,
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
    string Source,
    DateTime CreatedAt);
