using Barnaktiv.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<RawActivityPayload> RawActivityPayloads => Set<RawActivityPayload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Activity>(builder =>
        {
            builder.Property(activity => activity.SourceKey)
                .HasMaxLength(120);

            builder.Property(activity => activity.ExternalId)
                .HasMaxLength(240);

            builder.HasIndex(activity => new { activity.SourceKey, activity.ExternalId })
                .IsUnique()
                .HasFilter("[SourceKey] <> N'' AND [ExternalId] <> N''");
        });

        modelBuilder.Entity<RawActivityPayload>(builder =>
        {
            builder.Property(rawPayload => rawPayload.SourceKey)
                .HasMaxLength(120);

            builder.Property(rawPayload => rawPayload.ExternalId)
                .HasMaxLength(240);

            builder.Property(rawPayload => rawPayload.ContentHash)
                .HasMaxLength(64);

            builder.HasIndex(rawPayload => new
            {
                rawPayload.SourceKey,
                rawPayload.ExternalId,
                rawPayload.ContentHash
            });
        });
    }
}
