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
