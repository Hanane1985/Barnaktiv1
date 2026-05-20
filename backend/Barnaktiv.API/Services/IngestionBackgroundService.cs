using System.Threading.Channels;
using Barnaktiv.Application.DTOs.Ingestion;
using Barnaktiv.Application.Interfaces;

namespace Barnaktiv.API.Services;

public sealed class IngestionBackgroundService(
    Channel<Guid> workQueue,
    IngestionJobStore jobStore,
    IServiceScopeFactory scopeFactory,
    ILogger<IngestionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in workQueue.Reader.ReadAllAsync(stoppingToken))
        {
            var snapshot = jobStore.TryGet(jobId);

            if (snapshot is null)
            {
                continue;
            }

            jobStore.MarkRunning(jobId);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var ingestionService = scope.ServiceProvider.GetRequiredService<IActivityIngestionService>();
                var result = await ingestionService.RunAsync(stoppingToken, snapshot.SourceKey);
                jobStore.MarkCompleted(jobId, result);

                logger.LogInformation(
                    "Ingestion job {JobId} completed for source {SourceKey}. SourcesProcessed: {SourcesProcessed}, Errors: {ErrorCount}.",
                    jobId,
                    snapshot.SourceKey ?? "(all)",
                    result.SourcesProcessed,
                    result.Errors.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                jobStore.MarkFailed(jobId, exception.Message);
                logger.LogError(
                    exception,
                    "Ingestion job {JobId} failed for source {SourceKey}.",
                    jobId,
                    snapshot.SourceKey ?? "(all)");
            }
        }
    }
}

public sealed class IngestionJobStore
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, IngestionJobDto> jobs = [];

    public IngestionJobDto Create(string? sourceKey)
    {
        var job = new IngestionJobDto(
            Guid.NewGuid(),
            "Queued",
            sourceKey,
            DateTime.UtcNow,
            null,
            null,
            null,
            null);

        lock (sync)
        {
            jobs[job.JobId] = job;
        }

        return job;
    }

    public IngestionJobDto? TryGet(Guid jobId)
    {
        lock (sync)
        {
            return jobs.TryGetValue(jobId, out var job) ? job : null;
        }
    }

    public void MarkRunning(Guid jobId)
    {
        Update(jobId, job => job with
        {
            Status = "Running",
            StartedAt = DateTime.UtcNow,
        });
    }

    public void MarkCompleted(Guid jobId, IngestionRunDto result)
    {
        Update(jobId, job => job with
        {
            Status = "Completed",
            CompletedAt = DateTime.UtcNow,
            Result = result,
        });
    }

    public void MarkFailed(Guid jobId, string error)
    {
        Update(jobId, job => job with
        {
            Status = "Failed",
            CompletedAt = DateTime.UtcNow,
            Error = error,
        });
    }

    private void Update(Guid jobId, Func<IngestionJobDto, IngestionJobDto> update)
    {
        lock (sync)
        {
            if (jobs.TryGetValue(jobId, out var current))
            {
                jobs[jobId] = update(current);
            }
        }
    }
}

public sealed class IngestionJobQueue(
    Channel<Guid> workQueue,
    IngestionJobStore jobStore) : IIngestionJobQueue
{
    public async ValueTask<IngestionJobDto> EnqueueAsync(
        string? sourceKey,
        CancellationToken cancellationToken = default)
    {
        var job = jobStore.Create(sourceKey);
        await workQueue.Writer.WriteAsync(job.JobId, cancellationToken);
        return job;
    }

    public IngestionJobDto? TryGetJob(Guid jobId) => jobStore.TryGet(jobId);
}
