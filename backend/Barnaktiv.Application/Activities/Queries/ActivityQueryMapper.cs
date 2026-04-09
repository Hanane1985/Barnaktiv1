using Barnaktiv.Application.DTOs.Activities;

namespace Barnaktiv.Application.Activities.Queries;

public static class ActivityQueryMapper
{
    public static ActivityPersistenceQuery ToPersistenceQuery(ActivityQueryDto query)
    {
        return new ActivityPersistenceQuery
        {
            Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            City = NormalizeFilter(query.City),
            Organizer = NormalizeFilter(query.Organizer),
            Sport = NormalizeFilter(query.Sport),
            MinAge = query.MinAge,
            MaxAge = query.MaxAge,
            PriceFilter = MapPriceFilter(query.Price),
            Sort = MapSortOption(query.Sort),
        };
    }

    public static string? CategoryFilterOrNull(ActivityQueryDto query)
    {
        var category = query.Category?.Trim();
        if (string.IsNullOrEmpty(category) ||
            string.Equals(category, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return category;
    }

    private static string? NormalizeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    private static ActivityPriceFilterOption MapPriceFilter(string? price)
    {
        if (string.IsNullOrWhiteSpace(price))
        {
            return ActivityPriceFilterOption.Any;
        }

        return price.Trim().ToLowerInvariant() switch
        {
            "free" => ActivityPriceFilterOption.FreeOnly,
            "paid" => ActivityPriceFilterOption.PaidOnly,
            _ => ActivityPriceFilterOption.Any,
        };
    }

    private static ActivitySortOption MapSortOption(string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "date-desc" => ActivitySortOption.DateDescending,
            "created-desc" => ActivitySortOption.CreatedDescending,
            "registration" => ActivitySortOption.Registration,
            "price-asc" => ActivitySortOption.PriceAscending,
            "price-desc" => ActivitySortOption.PriceDescending,
            "title-asc" => ActivitySortOption.TitleAscending,
            _ => ActivitySortOption.DateAscending,
        };
    }
}
