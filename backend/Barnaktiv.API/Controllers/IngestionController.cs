using Barnaktiv.API.Auth;
using Barnaktiv.Application.DTOs.Ingestion;
using Barnaktiv.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barnaktiv.API.Controllers;

[ApiController]
[Authorize(Policy = AdminApiKeyDefaults.PolicyName)]
[Route("api/admin/ingestion")]
public sealed class IngestionController(
    IActivityIngestionService ingestionService,
    IIngestionJobQueue ingestionJobQueue) : ControllerBase
{
    /// <summary>Returns configured ingestion sources.</summary>
    [HttpGet("sources")]
    [ProducesResponseType(typeof(IReadOnlyList<IngestionSourceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<IngestionSourceDto>>> GetSources(
        CancellationToken cancellationToken)
    {
        var sources = await ingestionService.GetSourcesAsync(cancellationToken);
        return Ok(sources);
    }

    /// <summary>Returns status for a background ingestion job.</summary>
    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(IngestionJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IngestionJobDto> GetJob(Guid jobId)
    {
        var job = ingestionJobQueue.TryGetJob(jobId);
        return job is null ? NotFound() : Ok(job);
    }

    /// <summary>Queues ingestion for all enabled sources (runs in the background).</summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(IngestionJobDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<IngestionJobDto>> Run(CancellationToken cancellationToken)
    {
        var job = await ingestionJobQueue.EnqueueAsync(sourceKey: null, cancellationToken);
        return AcceptedAtAction(nameof(GetJob), new { jobId = job.JobId }, job);
    }

    /// <summary>Queues ingestion for one enabled source (runs in the background).</summary>
    [HttpPost("run/{sourceKey}")]
    [ProducesResponseType(typeof(IngestionJobDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<IngestionJobDto>> RunSource(
        string sourceKey,
        CancellationToken cancellationToken)
    {
        var job = await ingestionJobQueue.EnqueueAsync(sourceKey, cancellationToken);
        return AcceptedAtAction(nameof(GetJob), new { jobId = job.JobId }, job);
    }
}
