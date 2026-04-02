using Barnaktiv.Application.DTOs.Activities;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Services;

public sealed class ActivityService(IActivityRepository repository) : IActivityService
{
    public async Task<IReadOnlyList<ActivityDto>> GetAllAsync(
        ActivityQueryDto query,
        CancellationToken cancellationToken)
    {
        var activities = await repository.GetAllAsync(query, cancellationToken);
        return activities.Select(Map).ToList();
    }

    private static ActivityDto Map(Activity activity) =>
        new(
            activity.Id,
            activity.Title,
            activity.Description,
            activity.Organizer,
            activity.Location,
            activity.City,
            activity.AgeFrom,
            activity.AgeTo,
            activity.Sport,
            activity.Category,
            activity.ListingType.ToString(),
            activity.Date,
            activity.Price,
            activity.WebsiteUrl,
            activity.SignupUrl,
            activity.ImageUrl,
            activity.Source,
            activity.RegistrationStatus.ToString(),
            activity.RegistrationOpenAt,
            activity.RegistrationCloseAt,
            activity.CreatedAt);
}
