using Barnaktiv.Application;
using Barnaktiv.Infrastructure;
using Barnaktiv.Worker.Options;
using Barnaktiv.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<IngestionAutomationOptions>()
    .BindConfiguration(IngestionAutomationOptions.SectionName)
    .Validate(
        options => !options.Enabled || options.Interval > TimeSpan.Zero,
        "Ingestion automation interval must be greater than zero when automation is enabled.")
    .Validate(
        options => options.StartupDelay >= TimeSpan.Zero,
        "Ingestion automation startup delay cannot be negative.")
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<AutomatedIngestionHostedService>();

var host = builder.Build();
host.Run();
