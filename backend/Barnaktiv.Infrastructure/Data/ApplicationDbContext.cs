using Barnaktiv.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Barnaktiv.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<ActivityEmbedding> ActivityEmbeddings => Set<ActivityEmbedding>();

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

            builder.Property(activity => activity.Sport)
                .HasMaxLength(80);

            builder.Property(activity => activity.SignupUrl)
                .HasMaxLength(1000);

            builder.Property(activity => activity.ListingType)
                .HasConversion<string>()
                .HasMaxLength(40);

            builder.Property(activity => activity.RegistrationStatus)
                .HasConversion<string>()
                .HasMaxLength(40);

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

        modelBuilder.Entity<ActivityEmbedding>(builder =>
        {
            builder.Property(embedding => embedding.ContentHash)
                .HasMaxLength(64);

            builder.Property(embedding => embedding.VectorJson)
                .HasColumnType("nvarchar(max)");

            builder.HasIndex(embedding => embedding.ActivityId)
                .IsUnique();

            builder.HasOne(embedding => embedding.Activity)
                .WithMany()
                .HasForeignKey(embedding => embedding.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
