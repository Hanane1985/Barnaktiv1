using api.Data;
using api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.MapGet("/activities", async (ApplicationDbContext dbContext, CancellationToken cancellationToken) =>
{
    var activities = await dbContext.Activities
        .AsNoTracking()
        .OrderBy(activity => activity.Date)
        .ThenBy(activity => activity.Title)
        .ToListAsync(cancellationToken);

    return Results.Ok(activities);
})
.WithName("GetActivities")
.WithSummary("Gets all activities.")
.WithDescription("Returns all stored activities ordered by date.")
.WithTags("Activities")
.Produces<List<Activity>>(StatusCodes.Status200OK);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapGet("/", () => Results.Redirect("/swagger"))
        .ExcludeFromDescription();
}

app.Run();
