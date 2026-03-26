using Barnaktiv.Application.Interfaces;
using Barnaktiv.Infrastructure.Configuration;
using Barnaktiv.Infrastructure.Data;
using Barnaktiv.Infrastructure.Repositories;
using Barnaktiv.Infrastructure.Scrapers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Barnaktiv.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddSingleton(new HttpClient());
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IActivityIngestionRepository, ActivityIngestionRepository>();
        services.AddSingleton<IIngestionSourceProvider, IngestionSourceProvider>();
        services.AddScoped<IActivityScraper, JsonFeedActivityScraper>();
        services.AddScoped<IActivityScraper, GoteborgKalendariumHtmlScraper>();
        services.AddScoped<IActivityScraper, BKHackenStartPlayingScraper>();

        return services;
    }
}
