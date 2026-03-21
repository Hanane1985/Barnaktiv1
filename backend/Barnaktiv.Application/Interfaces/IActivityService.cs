using Barnaktiv.Application.DTOs.Activities;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityService
{
    Task<IReadOnlyList<ActivityDto>> GetAllAsync(CancellationToken cancellationToken);
}
