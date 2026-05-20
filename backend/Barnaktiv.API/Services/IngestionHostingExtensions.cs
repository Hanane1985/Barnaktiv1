using System.Threading.Channels;
using Barnaktiv.Application.Interfaces;

namespace Barnaktiv.API.Services;

public static class IngestionHostingExtensions
{
    public static IServiceCollection AddBackgroundIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IngestionJobStore>();
        services.AddSingleton(Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        }));
        services.AddSingleton<IIngestionJobQueue, IngestionJobQueue>();
        services.AddHostedService<IngestionBackgroundService>();
        return services;
    }
}
