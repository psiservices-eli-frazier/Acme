# Acme

Acme is a CRUD app (product/order/customer management) built as an ASP.NET Core
Minimal API on .NET 10, with a React 19 + Vite SPA. Data access is hand-written SQL via
Insight.Database, and the schema ships as versioned SQL migrations.

Authentication is a cookie session against a local `app_users` table, with two roles:
`ADMIN` (change anything) and `STAFF` (read everything, create/edit orders). Every
screen requires a signed-in user.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) 20 or later, with npm

No database server is required for local development — the dev profile uses a SQLite
file that's created and migrated automatically.

## Getting the code

```powershell
git clone <this repository's URL>
cd Acme
```

## Running it locally

Two processes, run in separate terminals:

```powershell
# Terminal 1 — API server. Migrates the schema and seeds demo data on the
# Development profile; creates src/Acme.Server/acme-dev.db on first run.
dotnet run --project src/Acme.Server

# Terminal 2 — client dev server, proxying /api to the server above
npm --prefix src/web.client install
npm --prefix src/web.client run dev
```

Open the client dev server's URL (printed by Vite, typically `http://localhost:5173`).

The dev profile seeds three throwaway accounts:

| Username  | Password | Role  |
|-----------|----------|-------|
| `adoyle`  | `admin`  | ADMIN |
| `sokonkwo`| `staff`  | STAFF |
| `jdoe`    | `staff`  | STAFF |

The dev database is a real file (`src/Acme.Server/acme-dev.db`, gitignored), so data
survives a restart. Delete the file to start clean.

## Running it as a single, production-shaped process

Build the SPA into the server's `wwwroot`, then run the server alone — it serves both
the API and the built client on one origin:

```powershell
npm --prefix src/web.client run build
dotnet run --project src/Acme.Server
```

Outside development there's no seeded account, so create the first admin user with:

```powershell
dotnet run --project src/Acme.Server -- --create-admin <username>
```

## Running a CI build artifact

Every CI run (`.github/workflows/ci.yml`) uploads a `publish`-job artifact named
`acme-server-<commit-sha>` — a `dotnet publish` of the server with the built SPA baked
into its `wwwroot/`, and the database already migrated and seeded with the same demo
accounts and data as local dev (the publish job runs `dotnet Acme.Server.dll --seed`,
which applies migrations and runs the seeders, then exits without starting the server).
Download it from the run's Actions page and unzip it, then just:

```powershell
cd <extracted folder>
dotnet Acme.Server.dll
```

Open the URL it prints (typically `http://localhost:5000`) and sign in with one of the
seeded accounts (see the table above).

The CI runner is `ubuntu-latest`, so the publish output's native apphost
(`Acme.Server`, no extension) is a Linux binary and won't run directly on Windows —
invoke it through `dotnet Acme.Server.dll` instead, which works on whichever platform
has a matching .NET 10 runtime installed. (A Windows-built artifact, with a real
`.exe`, would need its own `runs-on: windows-latest` publish job.)

This bakes the same throwaway demo credentials from local dev into every build
artifact, which is fine for a proof-of-concept demo app but worth remembering if this
pipeline ever starts publishing artifacts somewhere less contained than a private
repo's Actions run.

## Deploying with Docker

A multi-stage `Dockerfile` at the repo root builds the SPA, publishes the server with
the built SPA already in `wwwroot/`, and runs it framework-dependent (no SDK) in the
final image — the same single-process shape as "Running it as a single,
production-shaped process" above, just containerized.

```powershell
docker build -t acme-server .
docker run -p 10000:10000 `
  -e Database__Provider=Postgres `
  -e "Database__ConnectionString=Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" `
  -e Database__MigrateOnStartup=true `
  acme-server
```

Notes:

- **No SQLite in containers.** The image's filesystem is ephemeral on most PaaS hosts
  (Render included), so a SQLite file would vanish on every restart or redeploy — set
  `Database__Provider=Postgres` (or `MySql`/`SqlServer`) with a real connection string.
  This is exactly the dialect seam described under Architecture in [CLAUDE.md](CLAUDE.md);
  no code change is needed to switch, only configuration.
- **Migrations run automatically.** `Database__MigrateOnStartup=true` runs the
  already-embedded migration scripts for whichever dialect is configured, every time the
  container starts — safe to leave on permanently, since DbUp tracks what's already
  applied and only runs what's new.
- **Demo data is opt-in**, not baked into the image the way the CI artifact's `acme.db`
  is: add `Seed__Enabled=true` if you want the same throwaway accounts and sample
  products/customers/orders as local dev.
- **The listen port comes from `$PORT`** (default `10000`, matching Render's own
  Docker default) — the entrypoint substitutes it into `ASPNETCORE_URLS` at container
  start, so no rebuild is needed to change it.

## Continuous deployment (CI → GHCR → Render)

On every push to `main` that passes both test jobs, the `docker` job in
`.github/workflows/ci.yml`:

1. Builds the image from the root `Dockerfile`.
2. Pushes it to GitHub Container Registry as `ghcr.io/<owner>/acme:latest` and
   `:<commit-sha>`, authenticating with the workflow's built-in token — no registry
   secrets to create or manage.
3. Curls a Render *deploy hook* URL (once it exists as the `RENDER_DEPLOY_HOOK_URL` repo
   secret — see the one-time setup below) so Render pulls the new image and restarts.

Until that secret is added, step 3 is skipped rather than failing the run — the image
still gets built and pushed on every push to `main` regardless.

### One-time Render setup

1. Push this workflow to `main` once so the first image exists in GHCR.
2. Make that GHCR package pullable by Render: on GitHub, go to your profile/org →
   **Packages** → `acme` → package **Settings** → change visibility to **Public**.
   (Public is consistent with this app's own seeded demo credentials already being
   public in this README; keep it private instead and give Render a registry
   credential if that matters for your case.)
3. Create a free Render account (no credit card required).
4. **New → Web Service → Deploy an existing image from a registry**, image URL
   `ghcr.io/<owner>/acme:latest` (all lowercase — check the exact name the `docker`
   job's "Set lowercase image name" step logged, if unsure).
5. In the service's **Environment** tab, set `Database__Provider=Postgres`,
   `Database__ConnectionString=<from a free Postgres like Neon or Supabase>`,
   `Database__MigrateOnStartup=true`, and optionally `Seed__Enabled=true`.
6. Set **Health Check Path** to `/api/health` — the one route that doesn't require a
   signed-in session (see below), so Render can poll it without credentials.
7. Leave the service port at Render's default (`10000`) — it matches this image's
   `$PORT` default, so nothing to change there.
8. Deploy once manually to create the service, then open its **Settings → Deploy
   Hook** and copy the URL.
9. Back on GitHub: repo **Settings → Secrets and variables → Actions → New repository
   secret**, name it `RENDER_DEPLOY_HOOK_URL`, and paste that URL in.

From then on, every push to `main` that passes CI rebuilds the image, pushes it to
GHCR, and triggers Render to redeploy — no manual steps after that. The free web-service
tier also spins the container down after inactivity and back up on the next request
(cold start), and can be restarted on demand from the Render dashboard without waiting
on a rebuild.

## Health check

`GET /api/health` confirms the app can reach its configured database and returns JSON,
e.g.:

```json
{ "status": "Healthy", "checks": [{ "name": "database", "status": "Healthy", "description": "Database connection succeeded." }] }
```

It responds `200` when healthy and `503` when the database check fails, and — unlike
every other route — it does not require a signed-in session, so it's safe to point a
load balancer or container orchestrator's liveness probe at it.

## Logs

The server writes to two rolling daily files under `src/Acme.Server/Logs/` (gitignored),
in addition to the console:

- `access-YYYYMMDD.log` — one line per HTTP request (method, path, status, elapsed time).
- `errors-YYYYMMDD.log` — everything logged at Warning level or above, including
  unhandled exceptions.

Files older than 31 days are pruned automatically.

## Switching database engines

Four dialects ship with migration scripts: `sqlite` (dev default), `postgres`, `mysql`,
`sqlserver`. Switch with the `Database:Provider` and `Database:ConnectionString`
settings in `src/Acme.Server/appsettings.json`, an environment-specific
`appsettings.{Environment}.json`, or environment variables
(`Database__Provider`, `Database__ConnectionString`).

## Tests

```powershell
dotnet test                                # integration tests over a temp SQLite file
npm --prefix src/web.client run test       # Vitest
```

The server tests run the real application end to end (migrations applied, demo data
seeded) rather than mocking the database, because the data-access layer is hand-written
SQL — a test that substitutes the repository wouldn't prove the query that actually runs.

## Project layout

```
src/Acme.Server/     ASP.NET Core Minimal API (endpoints -> services -> repositories)
src/web.client/       React 19 + Vite SPA (routes -> TanStack Query hooks -> api/resources.ts)
tests/Acme.Server.Tests/  Integration tests over the real application
```

See [CLAUDE.md](CLAUDE.md) for the architecture rationale, the port's deliberate
differences from its reference implementation, and hard-won notes on the data-access
layer.
