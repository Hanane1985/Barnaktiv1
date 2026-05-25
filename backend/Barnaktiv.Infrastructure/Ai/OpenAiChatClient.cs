using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Barnaktiv.Infrastructure.Ai;

public sealed class OpenAiChatClient(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<OpenAiChatClient> logger) : IAiChatClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<string> CompleteAsync(
        IReadOnlyList<AiChatMessage> messages,
        bool jsonObjectResponse,
        CancellationToken cancellationToken)
    {
        var aiOptions = options.Value;

        if (!aiOptions.IsConfigured)
        {
            throw new InvalidOperationException("AI is not configured.");
        }

        var provider = aiOptions.Provider.Trim();
        var requestUri = provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)
            ? BuildAzureChatCompletionsUri(aiOptions)
            : "https://api.openai.com/v1/chat/completions";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

        if (provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("api-key", aiOptions.ApiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aiOptions.ApiKey);
        }

        var payload = new ChatCompletionRequest
        {
            Model = aiOptions.ChatModel,
            Messages = messages
                .Select(message => new ChatMessagePayload(message.Role, message.Content))
                .ToList(),
            Temperature = 0.2f,
            ResponseFormat = jsonObjectResponse
                ? new ResponseFormatPayload("json_object")
                : null,
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
                "OpenAI chat completion failed with status {StatusCode}",
                (int)response.StatusCode);
            throw new InvalidOperationException(
                $"AI provider returned {(int)response.StatusCode}.");
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(body, SerializerOptions);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI provider returned an empty response.");
        }

        return content;
    }

    private static string BuildAzureChatCompletionsUri(AiOptions aiOptions)
    {
        if (string.IsNullOrWhiteSpace(aiOptions.Endpoint))
        {
            throw new InvalidOperationException(
                "Ai:Endpoint must be configured when Ai:Provider is AzureOpenAI.");
        }

        var endpoint = aiOptions.Endpoint.Trim().TrimEnd('/');
        return $"{endpoint}/openai/deployments/{aiOptions.ChatModel}/chat/completions?api-version=2024-10-21";
    }

    private sealed class ChatCompletionRequest
    {
        public string Model { get; set; } = string.Empty;

        public IReadOnlyList<ChatMessagePayload> Messages { get; set; } = [];

        public float Temperature { get; set; }

        [JsonPropertyName("response_format")]
        public ResponseFormatPayload? ResponseFormat { get; set; }
    }

    private sealed record ChatMessagePayload(string Role, string Content);

    private sealed record ResponseFormatPayload(string Type);

    private sealed class ChatCompletionResponse
    {
        public IReadOnlyList<ChatChoicePayload>? Choices { get; set; }
    }

    private sealed class ChatChoicePayload
    {
        public ChatMessagePayload? Message { get; set; }
    }
}
