using Barnaktiv.Application.Interfaces;
using Barnaktiv.Domain.Entities;
using Barnaktiv.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Repositories;

public sealed class ActivityRepository(ApplicationDbContext dbContext) : IActivityRepository
{
    public async Task<IReadOnlyList<Activity>> GetAllOrderedByDateAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Activities
            .AsNoTracking()
            .OrderBy(activity => activity.Date)
            .ThenBy(activity => activity.Title)
            .ToListAsync(cancellationToken);
    }
}
