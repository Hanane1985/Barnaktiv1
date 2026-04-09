using Barnaktiv.Domain.Enums;

namespace Barnaktiv.Domain;

public static class ActivityRegistrationSortPriority
{
    public static int Rank(RegistrationStatus status) =>
        status switch
        {
            RegistrationStatus.Open => 0,
            RegistrationStatus.Upcoming => 1,
            RegistrationStatus.Unknown => 2,
            RegistrationStatus.Full => 3,
            RegistrationStatus.Closed => 4,
            _ => 4,
        };
}
