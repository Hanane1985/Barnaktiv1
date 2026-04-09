using Barnaktiv.Application.Activities.Queries;
using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> GetAllAsync(
        ActivityPersistenceQuery query,
        CancellationToken cancellationToken);
}
