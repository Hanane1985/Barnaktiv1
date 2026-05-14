# Hosting

Barnaktiv can be hosted without a paid plan by splitting the app across free tiers:

- Frontend: Vercel Hobby for `frontend/web`.
- Backend API and database: MonsterASP.NET free ASP.NET/.NET hosting with MSSQL.
- Ingestion: GitHub Actions cron that calls the protected API endpoint.

This setup is suitable for a prototype or early public test. Free tiers can sleep, throttle, suspend, delete inactive resources, or change limits, so keep exports/backups of production data.

## 1. Backend on MonsterASP.NET

Create a free MonsterASP.NET account and create an ASP.NET Core site with .NET 9 support.

Configure these production values in the hosting control panel:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<monster-mssql-connection-string>
AdminApiKey__HeaderName=X-Barnaktiv-Admin-Key
AdminApiKey__ApiKey=<long-random-secret>
Cors__AllowedOrigins__0=https://www.barnaktiv.se
```

Deploy `backend/Barnaktiv.API` as the web application.

After the database is created, run EF Core migrations against the production connection string:

```powershell
dotnet ef database update `
  --project backend/Barnaktiv.Infrastructure `
  --startup-project backend/Barnaktiv.API
```

Verify the API:

```powershell
Invoke-RestMethod https://<api-domain>/health
```

## 2. Frontend on Vercel

Create a Vercel project from the repository.

Add `www.barnaktiv.se` as the production domain in Vercel and point the domain DNS records to Vercel.

Use these settings:

```text
Root Directory: frontend/web
Build Command: npm run build
Install Command: npm ci
Output: Next.js default
```

Set this environment variable in Vercel:

```text
BARNAKTIV_API_BASE_URL=https://<api-domain>
```

After Vercel gives you the production URL, update the API host setting:

```text
Cors__AllowedOrigins__0=https://www.barnaktiv.se
```

## 3. Scheduled ingestion

The repository includes `.github/workflows/ingestion.yml`. It calls:

```text
POST /api/admin/ingestion/run
```

Add these GitHub repository secrets:

```text
BARNAKTIV_API_BASE_URL=https://<api-domain>
BARNAKTIV_ADMIN_API_KEY=<same value as AdminApiKey__ApiKey>
BARNAKTIV_ADMIN_HEADER_NAME=X-Barnaktiv-Admin-Key
```

The workflow runs every six hours and can also be started manually from the GitHub Actions tab.

## Launch check

Before making the site public:

```powershell
dotnet test Barnaktiv.sln
cd frontend/web
npm run lint
npm run build
```

Then run the `Scheduled ingestion` workflow manually once and confirm activities appear on the frontend.
