Backend services for Barnaktiv.

## Projects


- `Barnaktiv.API`: public activities API and protected admin ingestion endpoints.
- `Barnaktiv.Worker`: scheduled ingestion service.
- `Barnaktiv.Application`: use cases, DTOs and service interfaces.
- `Barnaktiv.Infrastructure`: EF Core repositories, SQL Server configuration and scrapers.
- `Barnaktiv.Domain`: entities and domain rules.
- `Barnaktiv.Domain.Tests`: current automated test project.

## Required Configuration

Set these values in your hosting environment or local user secrets:

- `ConnectionStrings__DefaultConnection`
- `AdminApiKey__ApiKey`
- `AdminApiKey__HeaderName`
- `Cors__AllowedOrigins__0` when browser clients call the API directly.

See `.env.example` for a complete example. The `.env.example` file is documentation only; .NET does not load it automatically.

## Local Commands

```powershell
dotnet restore ..\Barnaktiv.sln
dotnet test ..\Barnaktiv.sln
dotnet run --project Barnaktiv.API
dotnet run --project Barnaktiv.Worker
```

From the repository root, `.\scripts\start-dev.ps1` starts API, Worker and frontend together. Use that when you want local data ingestion to run automatically while using the web app.

## Database

Run EF Core migrations before starting against a new database:

```powershell
dotnet ef database update --project Barnaktiv.Infrastructure --startup-project Barnaktiv.API
```

## Operations

- `GET /health` verifies that the API process is alive.
- `GET /api/activities` returns public activities.
- `POST /api/admin/ingestion/run` triggers ingestion and requires the configured admin API key header.
- `Barnaktiv.Worker` must be running continuously in staging/production to keep activities fresh for users.
