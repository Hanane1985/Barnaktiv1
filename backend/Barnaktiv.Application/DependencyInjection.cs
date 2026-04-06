using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Barnaktiv.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IActivityIngestionExecutionGate, ActivityIngestionExecutionGate>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IActivityIngestionService, ActivityIngestionService>();
        return services;
    }
}
