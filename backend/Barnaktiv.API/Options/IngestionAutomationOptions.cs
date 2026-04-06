namespace Barnaktiv.API.Options;

public sealed class IngestionAutomationOptions
{
    public const string SectionName = "Ingestion:Automation";

    public bool Enabled { get; init; }

    public bool RunOnStartup { get; init; } = true;

    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(6);
}
