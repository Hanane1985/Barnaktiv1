using Barnaktiv.Application.DTOs.Activities;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain.Entities;
using Barnaktiv.Domain.Enums;
using Barnaktiv.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Repositories;

public sealed class ActivityRepository(ApplicationDbContext dbContext) : IActivityRepository
{
    public async Task<IReadOnlyList<Activity>> GetAllAsync(
        ActivityQueryDto query,
        CancellationToken cancellationToken)
    {
        var activities = dbContext.Activities
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var searchPattern = $"%{search}%";

            activities = activities.Where(activity =>
                EF.Functions.Like(activity.Title, searchPattern) ||
                EF.Functions.Like(activity.Description, searchPattern) ||
                EF.Functions.Like(activity.Organizer, searchPattern) ||
                EF.Functions.Like(activity.Location, searchPattern) ||
                EF.Functions.Like(activity.City, searchPattern) ||
                EF.Functions.Like(activity.Sport, searchPattern) ||
                EF.Functions.Like(activity.Category, searchPattern));
        }

        if (HasFilterValue(query.City))
        {
            var city = query.City!.Trim();
            activities = activities.Where(activity => activity.City == city);
        }

        if (HasFilterValue(query.Organizer))
        {
            var organizer = query.Organizer!.Trim();
            activities = activities.Where(activity => activity.Organizer == organizer);
        }

        if (HasFilterValue(query.Sport))
        {
            var sport = query.Sport!.Trim();
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

        if (!string.IsNullOrWhiteSpace(query.Price))
        {
            switch (query.Price.Trim().ToLowerInvariant())
            {
                case "free":
                    activities = activities.Where(activity => activity.Price <= 0m);
                    break;
                case "paid":
                    activities = activities.Where(activity => activity.Price > 0m);
                    break;
            }
        }

        activities = ApplySorting(activities, query.Sort);

        var results = await activities.ToListAsync(cancellationToken);

        if (HasFilterValue(query.Category))
        {
            var category = query.Category!.Trim();
            results = results
                .Where(activity => MatchesCategory(activity.Category, category))
                .ToList();
        }

        return results;
    }

    private static IQueryable<Activity> ApplySorting(
        IQueryable<Activity> activities,
        string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "date-desc" => activities
                .OrderByDescending(activity => activity.Date)
                .ThenBy(activity => activity.Title),
            "created-desc" => activities
                .OrderByDescending(activity => activity.CreatedAt)
                .ThenBy(activity => activity.Date)
                .ThenBy(activity => activity.Title),
            "registration" => activities
                .OrderBy(activity => activity.RegistrationStatus == RegistrationStatus.Open ? 0 :
                    activity.RegistrationStatus == RegistrationStatus.Upcoming ? 1 :
                    activity.RegistrationStatus == RegistrationStatus.Unknown ? 2 :
                    activity.RegistrationStatus == RegistrationStatus.Full ? 3 : 4)
                .ThenBy(activity => activity.Date)
                .ThenBy(activity => activity.Title),
            "price-asc" => activities
                .OrderBy(activity => activity.Price)
                .ThenBy(activity => activity.Title),
            "price-desc" => activities
                .OrderByDescending(activity => activity.Price)
                .ThenBy(activity => activity.Title),
            "title-asc" => activities
                .OrderBy(activity => activity.Title),
            _ => activities
                .OrderBy(activity => activity.Date)
                .ThenBy(activity => activity.Title),
        };
    }

    private static bool MatchesCategory(string categoryValue, string selectedCategory)
    {
        if (string.IsNullOrWhiteSpace(categoryValue))
        {
            return false;
        }

        var normalizedSelectedCategory = NormalizeCategoryToken(selectedCategory);

        return categoryValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeCategoryToken)
            .Any(category => category == normalizedSelectedCategory);
    }

    private static string NormalizeCategoryToken(string category)
    {
        var normalized = category
            .Trim()
            .ToLowerInvariant()
            .Replace(" / ", "/")
            .Replace("/ ", "/")
            .Replace(" /", "/");

        return normalized switch
        {
            "bad" => "bad/simning",
            _ => normalized,
        };
    }

    private static bool HasFilterValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !string.Equals(value.Trim(), "all", StringComparison.OrdinalIgnoreCase);
    }
}
