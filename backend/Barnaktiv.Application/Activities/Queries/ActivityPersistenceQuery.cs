namespace Barnaktiv.Application.Activities.Queries;

public sealed class ActivityPersistenceQuery
{
    public string? Search { get; init; }

    public string? City { get; init; }

    public string? Organizer { get; init; }

    public string? Sport { get; init; }

    public int? MinAge { get; init; }

    public int? MaxAge { get; init; }

    public ActivityPriceFilterOption PriceFilter { get; init; }

    public ActivitySortOption Sort { get; init; }
}
