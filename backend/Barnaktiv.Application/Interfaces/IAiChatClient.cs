namespace Barnaktiv.Application.Interfaces;

public interface IAiChatClient
{
    Task<string> CompleteAsync(
        IReadOnlyList<AiChatMessage> messages,
        bool jsonObjectResponse,
        CancellationToken cancellationToken);
}

public sealed record AiChatMessage(string Role, string Content);
