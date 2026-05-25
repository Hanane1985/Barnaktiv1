using Barnaktiv.Application.DTOs.Ai;

namespace Barnaktiv.Application.Interfaces;

public interface IActivityAiService
{
    Task<AskResponseDto> AskAsync(string question, CancellationToken cancellationToken);
}
