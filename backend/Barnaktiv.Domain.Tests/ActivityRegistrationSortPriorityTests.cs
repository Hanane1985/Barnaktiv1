using Barnaktiv.Domain.Enums;

namespace Barnaktiv.Domain.Tests;

public sealed class ActivityRegistrationSortPriorityTests
{
    [Fact]
    public void Rank_orders_open_before_closed()
    {
        var open = ActivityRegistrationSortPriority.Rank(RegistrationStatus.Open);
        var closed = ActivityRegistrationSortPriority.Rank(RegistrationStatus.Closed);
        Assert.True(open < closed);
    }

    [Fact]
    public void Rank_orders_upcoming_before_unknown()
    {
        var upcoming = ActivityRegistrationSortPriority.Rank(RegistrationStatus.Upcoming);
        var unknown = ActivityRegistrationSortPriority.Rank(RegistrationStatus.Unknown);
        Assert.True(upcoming < unknown);
    }
}
