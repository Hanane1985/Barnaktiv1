# Barnaktiv Web

Next.js frontend for Barnaktiv.

## Getting Started

Install dependencies and start the development server:

```powershell
npm ci
npm run dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the result.

The app reads activities from `BARNAKTIV_API_BASE_URL`. If the variable is not set, development falls back to `http://localhost:5289`.

## Release Checks

```powershell
npm run lint
npm run build
```

## Environment Variables

Copy the values from `.env.example` into your hosting provider:

- `BARNAKTIV_API_BASE_URL`: backend API base URL used by server-side rendering.
