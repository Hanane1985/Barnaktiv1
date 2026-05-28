namespace Barnaktiv.Application.Interfaces;

public interface IAiEmbeddingClient
{
    Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken);
}
