using Barnaktiv.Application;
using Barnaktiv.Infrastructure;
using Barnaktiv.API.Options;
using Barnaktiv.API.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Barnaktiv API",
        Version = "v1",
        Description = "API for collecting and serving children's activities."
    });
});

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.MapControllers();

app.Run();
