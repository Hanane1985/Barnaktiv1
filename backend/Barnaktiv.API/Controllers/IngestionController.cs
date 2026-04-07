using Barnaktiv.API.Auth;
using Barnaktiv.Application.DTOs.Ingestion;
using Barnaktiv.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Barnaktiv.API.Controllers;

[ApiController]
[Authorize(Policy = AdminApiKeyDefaults.PolicyName)]
[Route("api/admin/ingestion")]
public sealed class IngestionController(IActivityIngestionService ingestionService) : ControllerBase
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

    /// <summary>Runs all enabled ingestion sources and stores payloads plus normalized activities.</summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(IngestionRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IngestionRunDto>> Run(CancellationToken cancellationToken)
    {
        var result = await ingestionService.RunAsync(cancellationToken);
        return Ok(result);
    }
}
