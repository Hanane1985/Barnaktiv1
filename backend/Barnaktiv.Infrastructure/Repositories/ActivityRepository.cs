using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Activities.Queries;
using Barnaktiv.Domain;
using Barnaktiv.Domain.Entities;
using Barnaktiv.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Repositories;

public sealed class ActivityRepository(ApplicationDbContext dbContext) : IActivityRepository
{
    public async Task<IReadOnlyList<Activity>> GetAllAsync(
        ActivityPersistenceQuery query,
        CancellationToken cancellationToken)
    {
        var activities = dbContext.Activities
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchPattern = $"%{query.Search}%";

            activities = activities.Where(activity =>
                EF.Functions.Like(activity.Title, searchPattern) ||
                EF.Functions.Like(activity.Description, searchPattern) ||
                EF.Functions.Like(activity.Organizer, searchPattern) ||
                EF.Functions.Like(activity.Location, searchPattern) ||
                EF.Functions.Like(activity.City, searchPattern) ||
                EF.Functions.Like(activity.Sport, searchPattern) ||
                EF.Functions.Like(activity.Category, searchPattern));
        }

        if (query.City is { } city)
        {
            activities = activities.Where(activity => activity.City == city);
        }

        if (query.Organizer is { } organizer)
        {
            activities = activities.Where(activity => activity.Organizer == organizer);
        }

        if (query.Sport is { } sport)
        {
            activities = activities.Where(activity => activity.Sport == sport);
        }

        if (query.MinAge.HasValue)
        {
            var minAge = query.MinAge.Value;
            activities = activities.Where(activity => activity.AgeTo >= minAge);
        }

        if (query.MaxAge.HasValue)
        {
            var maxAge = query.MaxAge.Value;
            activities = activities.Where(activity => activity.AgeFrom <= maxAge);
        }

        activities = query.PriceFilter switch
        {
            ActivityPriceFilterOption.FreeOnly => activities.Where(activity => activity.Price <= 0m),
            ActivityPriceFilterOption.PaidOnly => activities.Where(activity => activity.Price > 0m),
            _ => activities,
        };

        activities = ApplySorting(activities, query.Sort);

        return await activities.ToListAsync(cancellationToken);
    }

    private static IQueryable<Activity> ApplySorting(
        IQueryable<Activity> activities,
        ActivitySortOption sort) =>
        sort switch
        {
            ActivitySortOption.DateDescending => activities
                .OrderByDescending(activity => activity.Date)
                .ThenBy(activity => activity.Title),
            ActivitySortOption.CreatedDescending => activities
                .OrderByDescending(activity => activity.CreatedAt)
                .ThenBy(activity => activity.Date)
                .ThenBy(activity => activity.Title),
            ActivitySortOption.Registration => activities
                .OrderBy(activity => ActivityRegistrationSortPriority.Rank(activity.RegistrationStatus))
                .ThenBy(activity => activity.Date)
                .ThenBy(activity => activity.Title),
            ActivitySortOption.PriceAscending => activities
                .OrderBy(activity => activity.Price)
                .ThenBy(activity => activity.Title),
            ActivitySortOption.PriceDescending => activities
                .OrderByDescending(activity => activity.Price)
                .ThenBy(activity => activity.Title),
            ActivitySortOption.TitleAscending => activities
                .OrderBy(activity => activity.Title),
            _ => activities
                .OrderBy(activity => activity.Date)
                .ThenBy(activity => activity.Title),
        };
}
