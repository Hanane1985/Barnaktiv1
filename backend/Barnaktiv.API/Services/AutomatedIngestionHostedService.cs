using Barnaktiv.API.Options;
using Barnaktiv.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Barnaktiv.API.Services;

public sealed class AutomatedIngestionHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<IngestionAutomationOptions> options,
    ILogger<AutomatedIngestionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var automationOptions = options.Value;

        if (!automationOptions.Enabled)
        {
            logger.LogInformation("Automated ingestion is disabled.");
            return;
        }

        if (automationOptions.StartupDelay > TimeSpan.Zero)
        {
            logger.LogInformation(
                "Automated ingestion will start after an initial delay of {StartupDelay}.",
                automationOptions.StartupDelay);

            await Task.Delay(automationOptions.StartupDelay, stoppingToken);
        }

        if (automationOptions.RunOnStartup)
        {
            await RunOnceAsync(stoppingToken);
        }

        logger.LogInformation(
            "Automated ingestion is scheduled to run every {Interval}.",
            automationOptions.Interval);

        using var timer = new PeriodicTimer(automationOptions.Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var runStartedAt = DateTimeOffset.UtcNow;

        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IActivityIngestionService>();

            logger.LogInformation("Automated ingestion run started at {StartedAtUtc}.", runStartedAt);

            var result = await ingestionService.RunAsync(cancellationToken);

            logger.LogInformation(
                "Automated ingestion completed. SourcesProcessed: {SourcesProcessed}/{ConfiguredSources}, ActivitiesCreated: {ActivitiesCreated}, ActivitiesUpdated: {ActivitiesUpdated}, PayloadsStored: {PayloadsStored}, Errors: {ErrorCount}.",
                result.SourcesProcessed,
                result.SourcesConfigured,
                result.ActivitiesCreated,
                result.ActivitiesUpdated,
                result.PayloadsStored,
                result.Errors.Count);

            foreach (var error in result.Errors)
            {
                logger.LogWarning("Automated ingestion reported an error: {Error}", error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Automated ingestion was cancelled.");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Automated ingestion failed after starting at {StartedAtUtc}.",
                runStartedAt);
        }
    }
}
