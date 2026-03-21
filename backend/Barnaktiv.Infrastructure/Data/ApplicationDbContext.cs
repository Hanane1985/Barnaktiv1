using Barnaktiv.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Activity> Activities => Set<Activity>();
}
