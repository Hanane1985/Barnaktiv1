namespace Barnaktiv.API.Auth;

public sealed class AdminApiKeyOptions
{
    public const string SectionName = "AdminApiKey";

    public string HeaderName { get; set; } = "X-Barnaktiv-Admin-Key";

    public string ApiKey { get; set; } = string.Empty;
}
