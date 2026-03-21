using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> GetAllOrderedByDateAsync(CancellationToken cancellationToken);
}
