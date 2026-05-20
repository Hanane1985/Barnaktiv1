using Barnaktiv.Application.Interfaces;
using Barnaktiv.Worker.Options;
using Microsoft.Extensions.Options;

namespace Barnaktiv.Worker.Services;

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

            // Run each enabled source separately so the global ingestion lock is not held for the
            // entire sweep. Otherwise admin triggers (e.g. GitHub Actions) can block on the gate
            // until the full automated run finishes, often exceeding HTTP client timeouts.
            var sources = await ingestionService.GetSourcesAsync(cancellationToken);
            var enabledSources = sources.Where(source => source.IsEnabled).ToList();

            if (enabledSources.Count == 0)
            {
                logger.LogInformation("Automated ingestion skipped: no enabled sources.");
                return;
            }

            var totalErrors = 0;
            var totalCreated = 0;
            var totalUpdated = 0;
            var totalPayloads = 0;
            var totalProcessed = 0;

            foreach (var source in enabledSources)
            {
                logger.LogInformation("Automated ingestion starting source {SourceKey}.", source.SourceKey);

                var result = await ingestionService.RunAsync(cancellationToken, source.SourceKey);

                totalProcessed += result.SourcesProcessed;
                totalCreated += result.ActivitiesCreated;
                totalUpdated += result.ActivitiesUpdated;
                totalPayloads += result.PayloadsStored;
                totalErrors += result.Errors.Count;

                logger.LogInformation(
                    "Automated ingestion finished source {SourceKey}. SourcesProcessed: {SourcesProcessed}/{ConfiguredSources}, ActivitiesCreated: {ActivitiesCreated}, ActivitiesUpdated: {ActivitiesUpdated}, PayloadsStored: {PayloadsStored}, Errors: {ErrorCount}.",
                    source.SourceKey,
                    result.SourcesProcessed,
                    result.SourcesConfigured,
                    result.ActivitiesCreated,
                    result.ActivitiesUpdated,
                    result.PayloadsStored,
                    result.Errors.Count);

                foreach (var error in result.Errors)
                {
                    logger.LogWarning(
                        "Automated ingestion reported an error for {SourceKey}: {Error}",
                        source.SourceKey,
                        error);
                }
            }

            logger.LogInformation(
                "Automated ingestion sweep completed. SourcesRun: {SourcesRun}, TotalSourcesProcessed: {TotalSourcesProcessed}, ActivitiesCreated: {ActivitiesCreated}, ActivitiesUpdated: {ActivitiesUpdated}, PayloadsStored: {PayloadsStored}, TotalErrors: {TotalErrors}.",
                enabledSources.Count,
                totalProcessed,
                totalCreated,
                totalUpdated,
                totalPayloads,
                totalErrors);
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
