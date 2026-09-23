# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project summary

Acme is a CRUD app (product/order/customer management) built as an ASP.NET Core Minimal
API on .NET 10 with a React 19 + Vite SPA, using **Insight.Database** for data access.
Three entities — Product, Customer, Order (with OrderItem lines) — each get repository,
service and endpoint layers on the server and list/form/view routes in the client.

It is a port of **BrandX** (`../BrandX`), a Spring Boot 4.1 / Java 21 / Thymeleaf app,
and BrandX is still the reference: when a behaviour here looks arbitrary, it is usually
because the Java version did it that way on purpose. Run both side by side and diff the
screens. The seed data is identical in both for exactly that reason.

**Authentication is a cookie session against a local `app_users` table, with two roles.**
Every screen requires a signed-in user; `ADMIN` may change anything, `STAFF` may read
everything and create or edit orders. This is a proof-of-concept front door, inherited
from BrandX — the intended path is OIDC/SSO later, which replaces the sign-in endpoint
and leaves the roles, the policies and the client guards in place.

## Commands

```powershell
# Server (migrates and seeds on the Development profile)
dotnet run --project src/Acme.Server

# Client dev server, proxying /api to the server above
npm --prefix src/web.client run dev        # http://localhost:5173

# Tests
dotnet test                                # 55 integration tests over a temp SQLite file
npm --prefix src/web.client run test       # Vitest

# Production-shaped: build the SPA into the server's wwwroot, then run the server alone
npm --prefix src/web.client run build
dotnet run --project src/Acme.Server       # serves the SPA and the API on one origin
```

The dev profile seeds three throwaway accounts: `adoyle`/`admin` (ADMIN),
`sokonkwo`/`staff` and `jdoe`/`staff` (STAFF). Use `sokonkwo` when checking that a change
is correctly hidden *and* refused — hiding a control and refusing the request behind it
are separate mechanisms.

Unlike BrandX's in-memory H2, the dev database is a file (`acme-dev.db`, gitignored), so
records survive a restart. Delete the file to start clean.

No linter or static-analysis config exists in this repo, by choice — don't add one
unasked, and don't act as one in review. (Inherited from BrandX.)

## Hard-won facts about Insight.Database

Two failures cost real time. Both are silent until they are not, and neither is caught by
anything that does not touch a database.

**1. Use `System.Data.SQLite`, not `Microsoft.Data.Sqlite`.** With
`Microsoft.Data.Sqlite`, Insight binds every `decimal` and `DateTime` parameter as an
**empty string** — prices and timestamps persist as `''` and the next read throws on the
way back out. Integers, strings and booleans are unaffected, so nothing looks wrong until
a money or date column is read. `System.Data.SQLite` is the driver Insight's own
documentation names as tested. See `Data/Dialect/SqliteDialect.cs`.

**2. Never pass `IDictionary<string, object>` as parameters.** Insight caches a
parameter-generator delegate per SQL string on that path, and the cached delegate holds
the command it was built from. The **first** call with a given SQL succeeds; every later
one throws `ObjectDisposedException` on a disposed `SQLiteCommand`. Pass a typed or
anonymous object instead, of constant shape — Insight only binds the parameters it finds
in the SQL text, so an object carrying properties the current statement does not
reference is fine. For variable-length `IN` lists see `SqlHelpers.InClause`.

## Architecture

**Layering.** endpoint (`Api/`) → service (`Services/`, the transaction and authorization
boundary) → repository (`Data/Repositories/`, hand-written SQL via Insight) → POCO
(`Domain/`). The client mirrors it: route → TanStack Query hook → `api/resources.ts`.

**The dialect seam.** `Data/Dialect/ISqlDialect.cs` is the only place that knows which
engine is in use: paging syntax, identity retrieval, the DbUp script folder, and whether
money can be summed in SQL. BrandX kept engine-independence by letting Hibernate infer a
dialect; Insight is SQL-first, so the differences have to be named — they are named there
and nowhere else. **If you write `if (provider == DbProvider.X)` outside that namespace,
add a member to the interface instead.** The one exception is the composition root in
`Program.cs`, which has to pick an implementation.

Four dialects ship with migration scripts: `sqlite` (dev default), `postgres`, `mysql`,
`sqlserver`. Switch with `Database:Provider` and `Database:ConnectionString`.

**Money on SQLite.** SQLite has no exact decimal type, so `SupportsDecimalAggregate` is
false there and order totals are read as `decimal` and summed in C# rather than with
`SUM()`. Reading a column converts through `decimal` first, which recovers the intended
2dp value; summing in SQL would accumulate float error instead. The other three dialects
get an exact `decimal(19, 2)` and use the aggregate. There is a round-trip test per price
shape in `CatalogApiTests.PricesRoundTripExactly`.

**Authorization is enforced at the service layer, not the endpoints or the client.**
`IAccessGuard.RequireAsync` sits at the top of each mutating service method because that
is the transactional boundary — a second endpoint or a background job reaching the same
method is checked too. Endpoints only `RequireAuthorization()` (signed in), and the
client's `<AdminOnly>` only hides controls. **A guard that exists only in the client is
missing.** This is the rule BrandX's own notes are most insistent about.

**Error handling is intentionally split three ways** (`Api/ApiExceptionHandler.cs`):
`NotFoundException` → 404 and a dedicated screen; `DuplicateValueException` → 400 keyed to
the field, so it renders next to the input the user was typing in;
`EntityInUseException` → 409, shown as a banner on the screen they were already on, never
an error page. `ForbiddenException` → 403 with its own screen, kept distinct from 404 so a
refusal never implies the record is missing.

**Order-specific domain rules.** Line prices are snapshotted at write time rather than
re-read from the current product price, so editing a product later does not rewrite order
history. The snapshot map is keyed by **product id**, built **before** the lines are
cleared, and first-wins if the same product appears twice. Every edit deletes and
reinserts all of an order's lines, so line ids do not survive an edit — which is why the
request payload carries none. Order numbers are a separate human-friendly identifier from
the primary key, generated with a retry-on-collision scheme backed by a unique constraint,
and allocated **before** any foreign key is resolved. `ordered_at` and the order number
are set once on create and never touched by an update.

**There are no stock rules.** Nothing decrements `stock_quantity`, reserves inventory or
checks availability, and the active-product filter applies only to the dropdown. Inherited
from BrandX deliberately: the requirements do not exist yet, and inventing them during a
port would be inventing business rules.

**List state lives in the URL**, not in component state: `?search=&customerId=&page=&size=&sort=`.
Pages are zero-based, the default size is 10, and `sort` is `property,direction` — the
same contract Spring Data produced, so the React links behave like the Thymeleaf ones did.
The server echoes the *resolved* sort back, in its canonical spelling, because the client's
column toggle compares it against a literal.

**Sorting is allow-listed** (`Data/SortMap.cs`). BrandX had no allow-list — `sort` went
straight into the JPA query, so an unknown property was a 500 and a dotted one traversed a
relation. Only the columns the list screens actually render are accepted.

**Schema is versioned SQL, not generated.** `Data/Migrations/{dialect}/*.sql`, embedded in
the assembly and applied by DbUp. BrandX had no migration story at all. Adding a column
means adding a script to all four folders.

## Deliberate differences from BrandX

Everything else is meant to match. These do not:

- H2 is gone — it is a Java database with no ADO.NET driver. SQLite replaces it.
- Mutations are REST verbs (`POST`/`PUT`/`DELETE`) rather than form POSTs, and CSRF is an
  `X-CSRF-TOKEN` header rather than a hidden field. **The antiforgery token is bound to
  the caller's identity**, so it must be re-fetched after sign-in; the client and the test
  helper both do.
- A real `customerId` filter on orders, replacing the customer screen's trick of pasting
  the customer's email into the order search box.
- A sort allow-list, indexes on the foreign keys and `orders.ordered_at`, and a stable
  `id` tiebreaker on every paged query (without it, paging over equal sort keys can show a
  row twice or not at all).
- `--create-admin <username>` closes the gap BrandX flagged as unsolved: outside
  development there was no way to create a first account.
- Validation error keys are PascalCase (`Sku`), matching what the framework's own
  validation emits; the client looks them up case-insensitively via `ApiError.fieldError`.
- The login screen's hint text is corrected — BrandX advertised `admin`/`staff` as
  usernames, but those are the passwords.

## Testing

`tests/Acme.Server.Tests` runs the real application over a throwaway SQLite file with
migrations applied and demo data seeded. These are integration tests rather than
mock-based unit tests **on purpose**: every query is hand-written SQL, so a test that
substitutes the repository proves the service's arithmetic and nothing about the statement
that actually runs — and both Insight failures above were invisible to anything that did
not touch a database.

`SecurityRulesTests` is the port of BrandX's `SecurityRulesTest` and carries the same
intent: it asserts the refusals, not the hidden buttons.
