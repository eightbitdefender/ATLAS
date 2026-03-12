# Architecture Overview

## Pattern: MVC (Model-View-Controller)

The application follows the ASP.NET Core MVC pattern. Each layer has a single responsibility:

```
Browser Request
      │
      ▼
┌─────────────────────────────────────────────────────────┐
│  ASP.NET Core Middleware Pipeline  (Program.cs)         │
│  HTTPS Redirect → Static Files → Routing → Auth        │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│  Controller Layer                                        │
│                                                          │
│  HomeController          ← dashboard stats               │
│  AssetsController        ← asset CRUD                    │
│  VulnerabilitiesController ← vuln CRUD                   │
│  AssetVulnerabilitiesController ← finding CRUD           │
└────────────────┬─────────────────┬──────────────────────┘
                 │                 │
          (queries)          (returns model)
                 │                 │
                 ▼                 ▼
┌───────────────────────┐   ┌──────────────────────────────┐
│  Data Layer           │   │  View Layer                   │
│                       │   │                               │
│  AtlasContext          │   │  Razor Views (.cshtml)        │
│  (EF Core DbContext)  │   │  _Layout.cshtml (shared)      │
│        │              │   │  Home/Index.cshtml            │
│        ▼              │   │  Assets/{CRUD views}          │
│  SQLite (cmdb.db)     │   │  Vulnerabilities/{CRUD views} │
└───────────────────────┘   │  AssetVulnerabilities/        │
                             └──────────────────────────────┘
```

---

## Component Descriptions

### Program.cs — Application Entry Point

Bootstraps the entire application. Responsibilities:

1. **Service registration** — registers MVC controllers+views, and `AtlasContext` with SQLite via dependency injection
2. **Schema migration check** — probes `SELECT Discriminator FROM Assets LIMIT 1`; if the column is missing (pre-OO schema), calls `EnsureDeleted()` before `EnsureCreated()` to rebuild the database with the new TPH layout. See ADR-013.
3. **Database initialization** — calls `db.Database.EnsureCreated()` on startup, creating `cmdb.db` and all tables if they do not exist
4. **Middleware pipeline** — configures HTTPS redirect, static file serving, routing, and authorization in the correct order
5. **Route mapping** — establishes the default `{controller}/{action}/{id?}` route convention

### Models — Domain Entities

C# classes decorated with Data Annotations for validation and EF Core column constraints.

**Asset hierarchy (EF Core Table-Per-Hierarchy / TPH)**

`Asset` is an `abstract` base class. All six concrete subtypes share a single `Assets` table; EF Core writes the class name into a `Discriminator` column to reconstruct the right C# object on read.

```
Asset (abstract)
├── Computer              — IpAddress, OS, RAM, CPU, DomainJoined, SerialNumber
├── NetworkDevice         — IpAddress, DeviceType, MAC, Firmware, PortCount, Managed
├── Printer               — IpAddress, PrinterType, Model, ColorCapable, NetworkConnected
├── SoftwareApplication   — AppType, Vendor, Version, URL, LicenseType
├── MobileDevice          — OS, DeviceType, IMEI, PhoneNumber, Carrier
└── CloudResource         — Provider, ResourceType, Region, AccountId, ResourceId/ARN
```

Three abstract/virtual members on the base class enable polymorphic display in the Index and Home views without casting:

| Member | Purpose |
|--------|---------|
| `abstract AssetCategory Category` | Enum identifying which subtype family |
| `abstract string SubTypeLabel`    | Human-readable subtype string (e.g. `"Laptop"`, `"Firewall"`) |
| `virtual string? Identifier`      | Type-specific primary identifier (IP, URL, ARN, IMEI, …) |

**`AssetFormViewModel`** is a flat class (not a domain entity) containing every field from every subtype. The controller inspects `vm.Category` after POST and dispatches to the correct concrete constructor. This avoids the MVC model-binder's inability to instantiate abstract classes directly. See ADR-011.

**Other entities:**

- `Vulnerability` — represents a known vulnerability (may or may not have a CVE ID)
- `AssetVulnerability` — join table linking one asset to one vulnerability; carries remediation state

See `data-model.md` for full field-level documentation.

### Data/AtlasContext.cs — Database Context

The single EF Core `DbContext` for the application. It:

- Exposes three `DbSet<T>` properties: `Assets`, `Vulnerabilities`, `AssetVulnerabilities`
- Configures the TPH discriminator in `OnModelCreating`, registering all six concrete asset subtypes under string discriminator values matching their class names
- Configures the two foreign key relationships in `OnModelCreating` using Fluent API, explicitly linking `AssetVulnerability` to both `Asset` and `Vulnerability`
- Receives its SQLite connection string via constructor injection from `Program.cs`

EF Core translates LINQ queries against the `DbSet` properties into SQL and executes them against `cmdb.db`.

### Controllers — Request Handlers

Each controller is injected with `AtlasContext` and uses it directly. No service or repository layer sits between them (see ADR-003).

| Controller | Actions | Notes |
|------------|---------|-------|
| `HomeController` | `Index` | Runs multiple `CountAsync` and `ToListAsync` queries to build `DashboardViewModel` |
| `AssetsController` | `Index`, `Details`, `Create`, `Edit`, `Delete` | Create/Edit use `AssetFormViewModel`; `BuildAssetFromViewModel` dispatches to correct concrete subtype; `Details` eager-loads `AssetVulnerabilities → Vulnerability` |
| `VulnerabilitiesController` | `Index`, `Details`, `Create`, `Edit`, `Delete` | `Details` eager-loads `AssetVulnerabilities → Asset` |
| `AssetVulnerabilitiesController` | `Create`, `Edit`, `Delete` | Always redirects back to `Assets/Details` after mutation |

**Anti-forgery tokens** are applied to all `[HttpPost]` actions via `[ValidateAntiForgeryToken]`.

**Optimistic concurrency** is not implemented; `IsModified = false` on timestamp properties prevents accidental overwriting of `CreatedAt` and `DetectedAt` during edits.

**Asset type dispatch in `AssetsController`** — `BuildAssetFromViewModel(vm)` is a `switch` expression on `vm.Category` that `new`s the correct concrete class and populates its type-specific properties. The reverse path, `MapToViewModel(asset)`, uses `switch` on the concrete type to round-trip data back to `AssetFormViewModel` for Edit GET requests. Changing an asset's category is explicitly rejected with a `ModelState` error to prevent data corruption.

### Views — Presentation Layer

All views use Razor syntax (`.cshtml`) and are strongly typed via `@model` directives. The shared `_Layout.cshtml` provides:

- Site-wide navigation bar with links to Dashboard, Assets, and Vulnerabilities
- Bootstrap 5 CSS (bundled in `wwwroot/lib/`)
- Inline `<style>` block defining severity color classes and stat-card border styles
- jQuery + Bootstrap JS at the bottom of `<body>`
- A `@RenderSection("Scripts", required: false)` slot that Create/Edit views use to inject client-side validation scripts

### wwwroot — Static Assets

Served directly by the static files middleware without hitting the controller layer. Contains:

- `lib/bootstrap/` — Bootstrap 5 CSS and JS
- `lib/jquery/` — jQuery 3.x
- `lib/jquery-validation/` + `lib/jquery-validation-unobtrusive/` — client-side validation
- `css/site.css` — empty placeholder for custom styles
- `js/site.js` — empty placeholder for custom scripts

---

## Data Flow: Dashboard

```
GET /
  │
  └─► HomeController.Index()
        │
        ├─ db.Assets.CountAsync()
        ├─ db.Assets.CountAsync(a => a.Status == Active)
        ├─ db.Vulnerabilities.CountAsync()
        ├─ db.AssetVulnerabilities.CountAsync(av => av.Status == Open)
        ├─ db.AssetVulnerabilities.CountAsync(av => Critical && Open)
        ├─ db.AssetVulnerabilities.CountAsync(av => High && Open)
        ├─ db.Assets.OrderByDescending(CreatedAt).Take(5).ToListAsync()
        └─ db.AssetVulnerabilities
              .Include(Asset).Include(Vulnerability)
              .Where(Open).OrderByDescending(DetectedAt).Take(5).ToListAsync()
                │
                ▼
        DashboardViewModel populated
                │
                ▼
        Views/Home/Index.cshtml renders stat cards + tables
```

---

## Security Considerations

| Concern | Mitigation |
|---------|-----------|
| CSRF | `[ValidateAntiForgeryToken]` on all POST actions; `@Html.AntiForgeryToken()` in inline delete forms |
| SQL Injection | All queries use EF Core LINQ — parameterized by default |
| XSS | Razor `@` expressions HTML-encode output by default |
| Mass Assignment | `Edit` POST actions only modify expected columns; `CreatedAt`/`DetectedAt` explicitly excluded from updates |
| HTTPS | Enforced via `app.UseHttpsRedirection()` in non-development environments |
