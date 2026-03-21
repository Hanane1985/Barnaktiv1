using Barnaktiv.Application.DTOs.Activities;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Services;

public sealed class ActivityService(IActivityRepository repository) : IActivityService
{
    public async Task<IReadOnlyList<ActivityDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var activities = await repository.GetAllOrderedByDateAsync(cancellationToken);
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
            activity.Category,
            activity.Date,
            activity.Price,
            activity.WebsiteUrl,
            activity.ImageUrl,
            activity.Source,
            activity.CreatedAt);
}
