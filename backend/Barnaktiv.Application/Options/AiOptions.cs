namespace Barnaktiv.Application.Options;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;

    public string? Endpoint { get; set; }

    public string ChatModel { get; set; } = "gpt-4o-mini";

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    public bool Enabled { get; set; }

    public int MaxRequestsPerMinute { get; set; } = 10;

    public int MaxQuestionLength { get; set; } = 500;

    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(ApiKey);
}
