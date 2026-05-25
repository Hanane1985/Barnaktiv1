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

When the AI assistant is deployed (see [ai-assistant.md](ai-assistant.md)), add:

```text
Ai__Enabled=true
Ai__Provider=OpenAI
Ai__ApiKey=<openai-or-azure-openai-key>
Ai__ChatModel=gpt-4o-mini
Ai__MaxRequestsPerMinute=10
```

Leave `Ai__Enabled=false` until fas 1 is ready; the API starts without an AI key when AI is disabled.

Deploy `backend/Barnaktiv.API` as the web application.

### Automatic deploy (GitHub Actions)

The repository includes `.github/workflows/deploy-api.yml`. On every push to `main` or `master` that changes files under `backend/`, GitHub Actions will build, run tests, publish the API for **win-x86** (MonsterASP requirement), and deploy with **Web Deploy**.

1. In the [MonsterASP control panel](https://admin.monsterasp.net/), enable **Web Deploy** and copy the four values shown there.
2. In your GitHub repo → **Settings → Secrets and variables → Actions**, add:

```text
WEBSITE_NAME
SERVER_COMPUTER_NAME
SERVER_USERNAME
SERVER_PASSWORD
```

Example shape (values come from Monster, not from this doc):

```text
WEBSITE_NAME: siteXXXX
SERVER_COMPUTER_NAME: https://siteXXXX.siteasp.net:8172
SERVER_USERNAME: siteXXXX
SERVER_PASSWORD: *********
```

You can also run **Deploy API to MonsterASP** manually under the Actions tab (**workflow_dispatch**).

Until these secrets exist, the deploy workflow will fail at the Web Deploy step (build and tests still validate the branch).

### First-time database setup

After the database is created, run EF Core migrations against the production connection string:

```powershell
$env:ConnectionStrings__DefaultConnection="<monster-mssql-connection-string>"

dotnet ef database update `
  --project backend/Barnaktiv.Infrastructure `
  --startup-project backend/Barnaktiv.API
```

Verify the API:

```powershell
Invoke-RestMethod http://barnaktiv.runasp.net/health
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
BARNAKTIV_API_BASE_URL=http://barnaktiv.runasp.net
```

After Vercel gives you the production URL, update the API host setting:

```text
Cors__AllowedOrigins__0=https://www.barnaktiv.se
```

## 3. Scheduled ingestion

The repository includes `.github/workflows/ingestion.yml`. It calls:

```text
GET /api/admin/ingestion/sources
POST /api/admin/ingestion/run/{sourceKey}   → 202 Accepted (job queued; work runs on the API host)
GET /api/admin/ingestion/jobs/{jobId}     → optional status check
```

GitHub Actions only **queues** one job per source (a few seconds total). Slow scrapers such as IFK SportAdmin run in the **background** on Monster, so the workflow does not wait 30+ minutes.

Add these GitHub repository secrets:

```text
BARNAKTIV_API_BASE_URL=http://barnaktiv.runasp.net
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
