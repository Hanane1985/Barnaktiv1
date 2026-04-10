using Barnaktiv.API.Auth;
using Barnaktiv.Application;
using Barnaktiv.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddOptions<AdminApiKeyOptions>()
    .BindConfiguration(AdminApiKeyOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.HeaderName),
        "Admin API key header name must be configured.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ApiKey),
        "Admin API key must be configured.")
    .ValidateOnStart();
builder.Services
    .AddAuthentication(AdminApiKeyDefaults.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, AdminApiKeyAuthenticationHandler>(
        AdminApiKeyDefaults.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminApiKeyDefaults.PolicyName, policy =>
    {
        policy.AddAuthenticationSchemes(AdminApiKeyDefaults.SchemeName);
        policy.RequireAuthenticatedUser();
    });
});
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Barnaktiv API",
        Version = "v1",
        Description = "API for collecting and serving children's activities."
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
