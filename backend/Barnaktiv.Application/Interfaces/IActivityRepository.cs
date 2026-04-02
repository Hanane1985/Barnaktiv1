using Barnaktiv.Application.DTOs.Activities;
using Barnaktiv.Domain.Entities;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> GetAllAsync(
        ActivityQueryDto query,
        CancellationToken cancellationToken);
}
