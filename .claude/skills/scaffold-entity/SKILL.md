---
name: scaffold-entity
description: Use when adding a new CRUD entity/feature to Acme (a new domain object with its own repository, service, endpoint group and list/form/view routes in the React client). Generates the new stack by cloning the codebase's vetted reference patterns instead of whichever existing entity gets copy-pasted next, so the new code doesn't reintroduce mistakes the port already had to find the hard way.
---

# Scaffolding a new entity

Acme has three parallel CRUD stacks (`Product`, `Customer`, `Order`), each with a
repository, a service, an endpoint group and `List` / `Form` / `View` routes. This skill
names, for each part of the stack, which existing file is the reference to copy and which
is the cautionary example to avoid.

Ported from the BrandX skill of the same name; the reasoning is mostly stack-independent,
the file names are not.

## Repository — the two Insight.Database traps

**Copy:** `Data/Repositories/ProductRepository.cs` for a flat entity, or
`Data/Repositories/OrderRepository.cs` for one with child rows.

Two things will silently corrupt data or throw on the second request if you copy the
wrong example. Both are documented in CLAUDE.md; the short version:

1. **Never pass `IDictionary<string, object>` as parameters.** Insight caches a
   parameter-generator delegate per SQL string on that path and the cached delegate holds
   a disposed command, so the first call works and every later one throws
   `ObjectDisposedException`. Build a typed or anonymous object of **constant shape**,
   even when the current SQL only references some of its properties — see how
   `ProductRepository.ListAsync` passes `new { Term = ... }` whether or not a search is
   active.
2. **Alias every column explicitly** (`stock_quantity AS StockQuantity`). Do not rely on
   a global `ColumnMapping` rule; we write the SQL anyway and an alias has no
   startup-ordering hazard.

**Paging.** Go through `dialect.Paginate(...)` — never write `LIMIT` or `OFFSET … FETCH`
directly. Always include a stable `, <alias>.id ASC` tiebreaker after the sort column, or
paging over equal sort keys can show a row twice or not at all.

**Anything engine-specific goes in `ISqlDialect`**, not in the repository. If you are
about to branch on the provider, add an interface member instead.

## Service — `Create` must never be able to name an existing row

**Copy:** `Services/ProductService.cs` (flat) or `Services/OrderService.cs` (child rows).

The request type in `Contracts/` must have **no `Id` property at all**. The identity of
the thing being written comes from the route; the body cannot name a row. `Create` builds
a fresh object from whitelisted fields and `Update` loads by the path id and copies the
same whitelisted set onto it. This was the top finding of BrandX's own review and the
reason its scaffold skill existed — the Java hazard was `save()` treating a non-null id as
a merge, and the hazard here is the same shape: an UPDATE keyed off body data.

**Guard ordering matters.** Report not-found *before* the uniqueness check and *before*
any in-use count, so editing or deleting a missing row is a 404 rather than a confusing
duplicate-value or in-use error. There are tests pinning this in `CatalogApiTests`.

**Authorization goes here, at the top of every mutating method:**

```csharp
await access.RequireAsync(Policies.AdminOnly);
```

Match the existing split: `AdminOnly` for create/update/delete, unless the new entity is
order-like day-to-day work, in which case `AdminOrStaff` for create/update with delete
still admin-only. Reads are unannotated; the endpoint group already requires a signed-in
user.

**Transactions:** open with `connections.OpenWithTransactionAsync(ct)` and call
`Commit()` explicitly. Disposing without committing rolls back.

## Endpoints

**Copy:** `Api/ProductEndpoints.cs`.

```csharp
var group = routes.MapGroup("/api/things")
    .RequireAuthorization()
    .AddEndpointFilter<AntiforgeryFilter>();
```

Both are required. `RequireAuthorization()` with no policy means "signed in" — the role
check is the service's job, not the endpoint's. The antiforgery filter is not optional:
`UseAntiforgery()` only validates endpoints that accept form data, so a JSON API gets no
protection without it.

Add a `SortMap` for the new list screen listing **only** the columns the client actually
renders as sortable headers, and parse the page request through
`PageRequestParsing.Parse`. Never pass a raw sort string into SQL.

## Client

**Copy:** `pages/products/ProductList.tsx`, `ProductForm.tsx`, `ProductView.tsx`.

- Add an `icon-<entity>` glyph and a badge tint to `components/Icons.tsx` and the
  `:root` block in `styles/app.css`, following the three existing entity hues. Don't
  inline raw SVG per row.
- Role-gated controls use `<AdminOnly>`. **This only hides things.** A new stack with
  only the client guard looks correct in a browser and is wide open to curl — verify with
  the seeded `sokonkwo`/`staff` account, and assert the refusal in a test, not just the
  absence of a button.
- Deletes go through `useDeleteAction`, which carries the confirm text, the success
  banner and the 409-refusal-as-banner path. Write the confirm string to match the
  existing ones exactly in tone: *"Delete this thing? This cannot be undone."*
- Field errors come from `ApiError.fieldError(name)`, which is case-insensitive — the
  server's keys are PascalCase and your inputs are camelCase.
- List state belongs in the query string, read through `useListState`. Don't put search,
  page or sort in component state.

## Migrations

Add a numbered script to **all four** folders under `Data/Migrations/`. They are embedded
resources picked up by prefix, so a new file needs no project change. Name unique
constraints and foreign keys explicitly (`uk_things_code`, `fk_things_owner`), and index
foreign keys and whatever column the list sorts by default — PostgreSQL does not index
foreign keys for you.

## After scaffolding

Add tests to `tests/Acme.Server.Tests` in the shape of the existing ones: the refusals
(anonymous, staff-write, missing antiforgery token), the guard ordering, and a round-trip
of any money column. Then run both suites:

```powershell
dotnet test
npm --prefix src/web.client run test
```
