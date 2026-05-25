using Barnaktiv.Application.Interfaces;
using Barnaktiv.Infrastructure.Ai;
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
        services.AddHttpClient<OpenAiChatClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<IAiChatClient>(sp => sp.GetRequiredService<OpenAiChatClient>());
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IActivityIngestionRepository, ActivityIngestionRepository>();
        services.AddSingleton<IIngestionSourceProvider, IngestionSourceProvider>();
        services.AddScoped<IActivityScraper, JsonFeedActivityScraper>();
        services.AddScoped<IActivityScraper, GoteborgKalendariumHtmlScraper>();
        services.AddScoped<IActivityScraper, PassalenMecCalendarScraper>();
        services.AddScoped<IActivityScraper, BKHackenStartPlayingScraper>();
        services.AddScoped<IActivityScraper, BKHackenSportAdminBookingScraper>();
        services.AddScoped<IActivityScraper, IfkGoteborgSportAdminBookingScraper>();
        services.AddScoped<IActivityScraper, SlsGoteborgSportAdminBookingScraper>();

        return services;
    }
}
