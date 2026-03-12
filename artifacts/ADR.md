# Architecture Decision Records

This document captures the significant design choices made when building the ATLAS application, the alternatives that were considered, and the reasoning behind each decision.

---

## ADR-001: Standalone Project, Not Added to Existing Solution

**Date:** 2026-03-11
**Status:** Accepted

### Context

The user's existing codebase (`ClassesFields`) is a .NET 8 console application used for learning C# classes and fields. The ATLAS application is a web application with a completely different purpose, runtime model, and set of dependencies.

### Decision

Create the ATLAS as an entirely separate project in its own directory (`/Users/snickers/ATLAS`) rather than adding it to the existing `ClassesFIelds.sln`.

### Alternatives Considered

- **Add as a new project to `ClassesFIelds.sln`** — Would pollute a learning project with production web app scaffolding. The solution file would mix unrelated concerns. Sharing the solution provides no technical benefit since there is no shared code.
- **Add as a class library within the existing project** — Not applicable; web apps require an executable host project.

### Consequences

- The ATLAS is independently buildable, runnable, and versioned
- No risk of breaking the existing learning project
- The user can open, run, or delete either project independently

---

## ADR-002: ASP.NET Core MVC over Alternative Frameworks

**Date:** 2026-03-11
**Status:** Accepted

### Context

A web UI was needed for CRUD operations on assets and vulnerabilities. Several .NET web UI frameworks were available.

### Decision

Use **ASP.NET Core MVC with Razor Views** (the `Microsoft.NET.Sdk.Web` template).

### Alternatives Considered

| Option | Reason Not Chosen |
|--------|-------------------|
| **Blazor Server** | Higher complexity; requires SignalR and persistent connections; overkill for simple CRUD forms |
| **Blazor WebAssembly** | Requires separate API project or BFF layer; larger download footprint; unnecessary for a local tool |
| **Razor Pages** | Valid alternative; MVC was chosen because the controller-per-entity pattern maps more naturally to a ATLAS with multiple domain objects, and makes routing intentions explicit |
| **Minimal API + SPA (React/Vue)** | Would require Node.js, a build pipeline, and a separate frontend; the added complexity is unjustified for a simple internal tool |

### Consequences

- Familiar, well-documented pattern with strong tooling support
- Server-rendered HTML means no client-side JavaScript framework to maintain
- The `{controller}/{action}/{id}` routing convention is self-documenting

---

## ADR-003: SQLite as the Database

**Date:** 2026-03-11
**Status:** Accepted

### Context

The application needs persistent storage. The target environment is a single developer machine with no existing database infrastructure.

### Decision

Use **SQLite** via `Microsoft.EntityFrameworkCore.Sqlite`. The database file (`cmdb.db`) lives in the application's working directory.

### Alternatives Considered

| Option | Reason Not Chosen |
|--------|-------------------|
| **SQL Server / LocalDB** | Requires SQL Server installation or the LocalDB component; adds setup friction with no benefit at this scale |
| **PostgreSQL** | Requires a running server process; appropriate for multi-user/production scenarios but excessive here |
| **In-memory database** | Data would not persist between runs; not suitable for a tracking tool |
| **JSON file storage** | Would require manual serialization and querying; EF Core provides this for free with better type safety |

### Consequences

- Zero-configuration: the database file is created automatically on first run via `EnsureCreated()`
- Single-file deployment: the entire data store travels with the project folder
- No concurrency primitives beyond SQLite's built-in file locking; acceptable for single-user use
- Migrating to SQL Server or PostgreSQL later requires only changing the EF Core provider and connection string

---

## ADR-004: EF Core with `EnsureCreated()` Instead of Migrations

**Date:** 2026-03-11
**Status:** Accepted

### Context

EF Core offers two schema management strategies: explicit migrations (`dotnet ef migrations add`) and `EnsureCreated()`, which creates the schema from the current model if the database does not exist.

### Decision

Use `db.Database.EnsureCreated()` called at application startup in `Program.cs`.

### Alternatives Considered

- **Explicit migrations** — Provides a full history of schema changes and supports incremental upgrades without data loss. This is the correct approach for a production application that evolves over time. It was not chosen here because the application is in its initial form and the added ceremony (running `dotnet ef migrations add` / `dotnet ef database update` after every model change) is unnecessary friction during early development.

### Consequences

- `EnsureCreated()` is a one-time operation: it creates the schema only if the database does not exist. It does **not** apply schema changes to an existing database.
- **Implication:** If a model change is made (e.g., adding a new column) after the database has been created, the developer must either delete `cmdb.db` to let it be recreated, or switch to migrations at that point.
- This trade-off is acceptable for a local tool but would need to be revisited before any multi-user or production deployment.

---

## ADR-005: Direct DbContext in Controllers (No Repository Layer)

**Date:** 2026-03-11
**Status:** Accepted

### Context

A common pattern in .NET applications is to introduce a repository or service layer between the controller and the database context, abstracting data access behind interfaces.

### Decision

Inject `CmdbContext` directly into controllers and use it without an intermediate repository or service layer.

### Alternatives Considered

- **Repository pattern** (`IAssetRepository`, `IVulnerabilityRepository`, etc.) — Adds a layer of abstraction that enables unit testing controllers without a real database. The cost is significant boilerplate: one interface and one concrete class per entity, all duplicating what EF Core already provides.
- **Generic repository** (`IRepository<T>`) — Reduces boilerplate but leaks EF Core concerns (e.g., `IQueryable`) through the abstraction, largely defeating its purpose.
- **Service layer** — Appropriate when business logic needs to be shared across multiple entry points (e.g., API + web + background jobs). This application has a single entry point and minimal business logic.

### Consequences

- Less code to write and maintain
- EF Core's `DbContext` already acts as a unit-of-work and repository; wrapping it adds duplication
- Controllers are not independently unit-testable without a database; integration tests against a real SQLite instance are the appropriate testing strategy if tests are added later
- If the application grows significantly in complexity, the service layer can be introduced incrementally without structural changes to the controllers

---

## ADR-006: Separate Join Entity for Asset–Vulnerability Relationship

**Date:** 2026-03-11
**Status:** Accepted

### Context

The relationship between assets and vulnerabilities is many-to-many. EF Core supports implicit many-to-many (using a hidden join table) or explicit many-to-many (using a named join entity class).

### Decision

Use an **explicit join entity** (`AssetVulnerability`) with its own `DbSet`.

### Alternatives Considered

- **Implicit many-to-many** (`Asset.Vulnerabilities` ↔ `Vulnerability.Assets` with EF Core managing the join table automatically) — This would be appropriate if the relationship carried no additional data. However, the core purpose of a ATLAS is to track the *state* of each finding (remediation status, detection date, remediation date, notes). These fields cannot be placed on either side of an implicit join.

### Consequences

- The join entity is a first-class domain object with its own controller and views
- Each finding has its own lifecycle: `Open → InProgress → Remediated`
- Queries against findings are straightforward (`db.AssetVulnerabilities.Where(...)`)
- Slightly more code compared to implicit many-to-many, but the payload data makes this unavoidable

---

## ADR-007: Bootstrap 5 via Bundled Static Files (No CDN)

**Date:** 2026-03-11
**Status:** Accepted

### Context

A CSS framework was needed to produce a usable, responsive UI without writing custom CSS. Bootstrap 5 files were included in `wwwroot/lib/` by the `dotnet new mvc` template.

### Decision

Use the **bundled Bootstrap 5** files already present in `wwwroot/lib/` rather than loading from a CDN.

### Alternatives Considered

- **CDN delivery** — Reduces payload size but introduces a network dependency. The application is intended as a local tool; requiring an internet connection to load the UI would be a poor experience.
- **Tailwind CSS** — Requires a Node.js build step; adds toolchain complexity inconsistent with a zero-dependency .NET project.
- **No framework / hand-written CSS** — Would require significantly more styling effort for negligible benefit.

### Consequences

- Application works fully offline
- Bootstrap 5 files add approximately 300 KB to the project (already present in the template; no additional download)
- Upgrading Bootstrap requires manually replacing files in `wwwroot/lib/`

---

## ADR-008: Severity Enum Ordered Highest to Lowest

**Date:** 2026-03-11
**Status:** Accepted

### Context

The `Severity` enum (`Critical`, `High`, `Medium`, `Low`, `Informational`) needed an integer representation for EF Core storage and ordering.

### Decision

Declare enum members in **descending severity order** so that `Critical = 0` and `Informational = 4`.

### Alternatives Considered

- **Ascending order** (`Informational = 0`, `Critical = 4`) — Would require `OrderByDescending` when displaying most-critical-first, which is the default expected view.
- **Non-contiguous integers** (e.g., matching CVSS ranges) — Unnecessary complexity; the relative ordering is what matters for sorting.

### Consequences

- `OrderBy(v => v.Severity)` naturally produces `Critical` first, matching the expected display order in all tables and lists
- The dashboard's critical/high filter queries compare against enum values directly, which is readable and type-safe

---

## ADR-009: `[ValidateAntiForgeryToken]` on All Mutating Actions

**Date:** 2026-03-11
**Status:** Accepted

### Context

Any POST action that modifies the database is a potential CSRF target if tokens are not validated.

### Decision

Apply `[ValidateAntiForgeryToken]` to every `[HttpPost]` action, including the inline delete actions on the Asset Details page (which use `@Html.AntiForgeryToken()` directly in the form).

### Alternatives Considered

- **Global filter** — Could be applied once in `Program.cs` via `builder.Services.AddControllersWithViews(options => options.Filters.Add<AutoValidateAntiforgeryTokenAttribute>())`. This would be the preferred approach at scale. The per-action attribute was used here to keep each action self-documenting for educational clarity.
- **No CSRF protection** — Acceptable only for purely read-only routes. Any write operation without CSRF protection is vulnerable.

### Consequences

- All state-changing operations require a valid token embedded in the form, preventing cross-site request forgery
- Adds one attribute and one hidden field per form; the overhead is negligible

---

## ADR-010: EF Core Table-Per-Hierarchy (TPH) for Asset Polymorphism

**Date:** 2026-03-12
**Status:** Accepted

### Context

Assets are not all computers. The ATLAS needs to represent computers, network devices, printers, software applications, mobile devices, and cloud resources. Each type has a distinct set of relevant properties. A design decision was needed for how to model this in EF Core and SQLite.

### Decision

Use **EF Core Table-Per-Hierarchy (TPH)** inheritance: a single `Assets` table stores all asset subtypes, with a `Discriminator` string column identifying the concrete C# class name (`Computer`, `NetworkDevice`, etc.).

The `Asset` base class is declared `abstract` and holds all common fields. Each concrete subtype inherits from it and adds its own properties as nullable columns in the shared table.

### Alternatives Considered

| Option | Reason Not Chosen |
|--------|-------------------|
| **Table-Per-Type (TPT)** — separate table per subtype | Each query for a concrete type would require a JOIN to the base table; SQLite's JOIN performance is fine at this scale but the schema complexity is higher. EF Core TPT also has a history of performance issues in earlier versions. |
| **Table-Per-Concrete-Type (TPC)** — each subtype in its own table with all base fields repeated | No single `Assets` table to query across all types; cross-type queries (e.g. the Index list or dashboard counts) would require UNION ALL. Managing foreign keys from `AssetVulnerability` to "any asset" becomes complex. |
| **Flat single table with all fields** — keep the original `Asset` class but add every possible subtype-specific column | This was effectively the pre-refactor state. It has no inheritance benefit, gives false impressions about which fields apply to which assets, and provides no type safety. |
| **Separate entity tables with no inheritance** (`Computers`, `Printers`, etc.) | `AssetVulnerability` would need a FK to each possible asset table, or a polymorphic FK (not natively supported by EF Core). Cross-type vulnerability queries would be complex. |

### Consequences

- All assets live in one table — the Index page, dashboard counts, and vulnerability joins all use a single simple query
- The `Discriminator` column is set automatically by EF Core on insert; the concrete type can never be changed after creation (see ADR-011)
- Unused columns for inapplicable fields are `NULL` in the database (accepted trade-off for TPH)
- Adding a new asset subtype requires: a new C# class, a discriminator mapping in `CmdbContext`, a new form section, and new controller switch branches — no schema changes are needed

---

## ADR-011: Flat `AssetFormViewModel` for Create/Edit Forms

**Date:** 2026-03-12
**Status:** Accepted

### Context

The `Asset` base class is abstract. ASP.NET Core MVC model binding cannot instantiate an abstract class directly from a form POST. A mechanism was needed to bind form data to the correct concrete subtype.

### Decision

Introduce a **flat `AssetFormViewModel`** that contains all fields from all subtypes as optional properties. The controller reads `vm.Category` and dispatches to the correct concrete `Asset` subclass on save.

### Alternatives Considered

| Option | Reason Not Chosen |
|--------|-------------------|
| **Custom model binder** that reads a hidden `_type` field and instantiates the correct subclass | Works, but the framework integration is fragile and the implementation is non-obvious to future maintainers |
| **Separate Create/Edit views and actions per subtype** (`/Assets/CreateComputer`, etc.) | Clean and explicit, but requires 12 separate actions (Create+Edit × 6 types) and 12 separate views — high repetition since 80% of the form is identical across types |
| **Abstract base class binding via `[Bind]` attribute tricks** | Not supported by ASP.NET Core; results in null object or type mismatch exceptions |

### Consequences

- A single `/Assets/Create` and `/Assets/Edit` action handles all asset types
- The flat ViewModel means some unused properties are posted with every form submission (e.g., `CloudProvider` when creating a Computer) — these are silently ignored by the controller
- The Category is locked after creation on the Edit form; changing asset type requires deleting and re-creating the asset to avoid EF Core discriminator mutation issues
- Type safety is enforced in the controller's switch expression; adding a new category without a matching case throws `InvalidOperationException` at runtime

---

## ADR-012: JavaScript Show/Hide Sections vs. Separate Pages per Type

**Date:** 2026-03-12
**Status:** Accepted

### Context

With 6 asset types each having distinct fields, a decision was needed on how to present the form to the user.

### Decision

Use a **single form page** with all type-specific field sections rendered in the HTML as hidden `div`s (`d-none` Bootstrap class). A small vanilla JavaScript IIFE listens for changes to the Category `<select>` and toggles visibility by matching the selected option's text to a `section-{TypeName}` element ID.

### Alternatives Considered

- **Redirect to a type-specific Create URL** (e.g. `/Assets/Create/Computer`) with a separate view per type — Clean, but requires 6 views and means the user must decide before they start the form. If they pick the wrong type, they'd have to navigate away and start over.
- **AJAX partial view loading** — Load only the relevant section via fetch on category change. More complex, requires an additional controller action, and introduces a network round-trip.
- **Single form showing all fields always** — Too noisy; many irrelevant fields presented to the user for every asset type.

### Consequences

- Zero network requests on category change; section toggling is instant
- All sections are rendered in HTML even when hidden — slight increase in page size, acceptable since sections are small
- The JavaScript is minimal (~10 lines) and has no framework dependency
- On form validation failure (POST + redirect back to GET), the previously selected category is preserved via the ViewModel's `Category` property, and the correct section is re-shown via the `categoryVal` Razor variable injected into the script

---

## ADR-013: Discriminator Column Check as Schema Migration Strategy

**Date:** 2026-03-12
**Status:** Accepted

### Context

The OO refactor changed the `Assets` table schema significantly: it added ~20 new columns and a `Discriminator` column. Existing databases created before the refactor do not have this column. The application uses `EnsureCreated()` (see ADR-004), which does not modify existing schemas.

### Decision

At application startup in `Program.cs`, **attempt to `SELECT Discriminator FROM Assets LIMIT 1`**. If this throws (the column doesn't exist), the old schema is detected and `EnsureDeleted()` is called before `EnsureCreated()`, causing a full schema rebuild.

```csharp
try { db.Database.ExecuteSqlRaw("SELECT Discriminator FROM Assets LIMIT 1"); }
catch { db.Database.EnsureDeleted(); }
db.Database.EnsureCreated();
```

### Alternatives Considered

| Option | Reason Not Chosen |
|--------|-------------------|
| **EF Core migrations** | The correct production approach; would allow data-preserving upgrades. Not adopted here because migrations were intentionally avoided (ADR-004) and this is a single-user local tool where data loss on refactor is acceptable. |
| **Manual `ALTER TABLE` statements** | The new schema adds ~20 columns including `Discriminator`, whose absence vs. presence is the key signal. A targeted `ALTER TABLE` for each new column would require 20+ wrapped try/catch blocks, and still wouldn't handle column type changes. |
| **Version number in a settings table** | Clean approach, but requires maintaining a schema version number manually and writing upgrade code. Overkill for a one-time migration on a local tool. |

### Consequences

- **Data loss**: any assets or findings in the old schema are deleted on first startup after the update. This is intentional and acceptable for a local tool at this stage.
- The check is idempotent: after the first run with the new schema, the `SELECT Discriminator` succeeds every time and `EnsureDeleted()` is never called again
- A future addition of another new column would require a similar check or a proper migration strategy (at that point, switching to EF Core migrations would be the recommended path)
