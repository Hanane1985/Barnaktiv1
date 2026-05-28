using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Barnaktiv.Infrastructure.Ai;

public sealed class OpenAiEmbeddingClient(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<OpenAiEmbeddingClient> logger) : IAiEmbeddingClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyList<float[]>> CreateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var aiOptions = options.Value;
        if (!aiOptions.IsConfigured)
        {
            throw new InvalidOperationException("AI is not configured.");
        }

        var provider = aiOptions.Provider.Trim();
        var requestUri = provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)
            ? BuildAzureEmbeddingsUri(aiOptions)
            : "https://api.openai.com/v1/embeddings";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("api-key", aiOptions.ApiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aiOptions.ApiKey);
        }

        var payload = new EmbeddingRequest
        {
            Model = aiOptions.EmbeddingModel,
            Input = inputs,
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "OpenAI embeddings failed with status {StatusCode}",
                (int)response.StatusCode);
            throw new InvalidOperationException($"AI provider returned {(int)response.StatusCode}.");
        }

        var parsed = JsonSerializer.Deserialize<EmbeddingResponse>(body, SerializerOptions);
        var vectors = parsed?.Data?
            .OrderBy(item => item.Index)
            .Select(item => item.Embedding ?? [])
            .ToList();

        if (vectors is null || vectors.Count != inputs.Count)
        {
            throw new InvalidOperationException("AI provider returned an invalid embeddings response.");
        }

        return vectors;
    }

    private static string BuildAzureEmbeddingsUri(AiOptions aiOptions)
    {
        if (string.IsNullOrWhiteSpace(aiOptions.Endpoint))
        {
            throw new InvalidOperationException(
                "Ai:Endpoint must be configured when Ai:Provider is AzureOpenAI.");
        }

        var endpoint = aiOptions.Endpoint.Trim().TrimEnd('/');
        return $"{endpoint}/openai/deployments/{aiOptions.EmbeddingModel}/embeddings?api-version=2024-10-21";
    }

    private sealed class EmbeddingRequest
    {
        public string Model { get; set; } = string.Empty;

        public IReadOnlyList<string> Input { get; set; } = [];
    }

    private sealed class EmbeddingResponse
    {
        public IReadOnlyList<EmbeddingData>? Data { get; set; }
    }

    private sealed class EmbeddingData
    {
        public int Index { get; set; }

        public float[]? Embedding { get; set; }
    }
}
