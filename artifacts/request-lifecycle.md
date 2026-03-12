# Request Lifecycle

This document traces how an HTTP request travels through the application from browser to database and back.

---

## Middleware Pipeline

Defined in `Program.cs`, every request passes through these middleware components in order:

```
Incoming HTTP Request
         │
         ▼
 ┌───────────────────┐
 │ Exception Handler  │  (non-Development only) catches unhandled exceptions → /Home/Error
 └────────┬──────────┘
          │
          ▼
 ┌───────────────────┐
 │  HSTS             │  adds Strict-Transport-Security header (non-Development only)
 └────────┬──────────┘
          │
          ▼
 ┌───────────────────┐
 │ HTTPS Redirect    │  upgrades HTTP → HTTPS
 └────────┬──────────┘
          │
          ▼
 ┌───────────────────┐
 │  Static Files     │  short-circuits here if path matches wwwroot (CSS, JS, images)
 └────────┬──────────┘
          │  (only dynamic requests continue)
          ▼
 ┌───────────────────┐
 │  Routing          │  matches URL to a controller action using route table
 └────────┬──────────┘
          │
          ▼
 ┌───────────────────┐
 │  Authorization    │  placeholder; no auth configured (all routes are open)
 └────────┬──────────┘
          │
          ▼
 ┌───────────────────┐
 │  Controller Action │
 └───────────────────┘
```

---

## Example 1: Viewing the Asset List (GET)

**URL:** `GET /Assets`

```
Browser
  │
  │  GET /Assets
  ▼
Routing middleware
  │  Matches pattern: {controller=Home}/{action=Index}/{id?}
  │  controller = "Assets", action = "Index"
  ▼
AssetsController.Index() is instantiated
  │  CmdbContext injected by DI container
  │
  ▼
db.Assets
  .Include(a => a.AssetVulnerabilities)
  .OrderBy(a => a.Hostname)
  .ToListAsync()
  │
  │  EF Core translates to SQL:
  │  SELECT * FROM Assets ORDER BY Hostname;
  │  SELECT * FROM AssetVulnerabilities WHERE AssetId IN (...);
  ▼
SQLite (cmdb.db)
  │  returns rows
  ▼
EF Core materializes List<Asset> (each with populated AssetVulnerabilities collection)
  │
  ▼
return View(assets)
  │
  ▼
Razor engine renders Views/Assets/Index.cshtml
  │  Iterates each Asset
  │  Computes open finding count: a.AssetVulnerabilities.Count(av => av.Status == Open)
  │  Emits HTML table rows
  │  Injects _Layout.cshtml wrapper (navbar, Bootstrap, footer)
  ▼
HTTP 200 response with HTML body sent to browser
```

---

## Example 2: Creating an Asset (GET + POST)

### Step 1 — Render the Form (GET)

**URL:** `GET /Assets/Create`

```
Browser
  │  GET /Assets/Create
  ▼
AssetsController.Create() [GET]
  │  No DB query needed
  │  return View()  ← empty Asset model
  ▼
Razor renders Views/Assets/Create.cshtml
  │  asp-for tag helpers bind form fields to Asset properties
  │  asp-action="Create" generates action="/Assets/Create"
  │  Hidden __RequestVerificationToken field injected by tag helper
  ▼
Browser receives form HTML
```

### Step 2 — Submit the Form (POST)

**URL:** `POST /Assets/Create`

```
Browser
  │  POST /Assets/Create
  │  Body: Hostname=web01&IpAddress=10.0.0.1&Type=0&Status=0&...
  │        + __RequestVerificationToken=<token>
  ▼
Anti-forgery middleware validates token
  │  Mismatch → 400 Bad Request
  ▼
Model binding
  │  ASP.NET Core reads form fields and populates an Asset instance
  ▼
AssetsController.Create(Asset asset) [POST]
  │
  ├─ ModelState.IsValid?
  │    NO  → return View(asset)  (re-render with validation errors)
  │    YES ↓
  │
  ├─ asset.CreatedAt = DateTime.UtcNow
  │
  ├─ _db.Assets.Add(asset)        ← marks entity as Added in EF change tracker
  │
  ├─ await _db.SaveChangesAsync()
  │     │
  │     │  EF Core generates SQL:
  │     │  INSERT INTO Assets (Hostname, IpAddress, ...) VALUES (?, ?, ...);
  │     ▼
  │  SQLite executes insert; new Id assigned
  │
  └─ return RedirectToAction("Index")
       │
       ▼
HTTP 302 redirect to /Assets
  │
  ▼
Browser follows redirect → GET /Assets (see Example 1)
```

---

## Example 3: Assigning a Vulnerability to an Asset (POST)

**URL:** `POST /AssetVulnerabilities/Create`

```
Browser submits form from Views/AssetVulnerabilities/Create.cshtml
  │  AssetId=3&VulnerabilityId=7&Status=0&Notes=...
  ▼
AssetVulnerabilitiesController.Create(AssetVulnerability av) [POST]
  │
  ├─ ModelState.IsValid check
  │
  ├─ av.DetectedAt = DateTime.UtcNow
  │
  ├─ _db.AssetVulnerabilities.Add(av)
  │
  ├─ await _db.SaveChangesAsync()
  │     INSERT INTO AssetVulnerabilities (AssetId, VulnerabilityId, Status, DetectedAt, Notes)
  │     VALUES (3, 7, 0, '2026-03-11 ...', '...');
  │
  └─ return RedirectToAction("Details", "Assets", new { id = av.AssetId })
       │
       ▼
HTTP 302 → /Assets/Details/3
```

---

## Example 4: Inline Delete from Asset Details Page (POST)

The delete form on `Views/Assets/Details.cshtml` submits directly without a confirmation page:

```html
<form asp-controller="AssetVulnerabilities" asp-action="Delete"
      asp-route-id="@av.Id" method="post"
      onsubmit="return confirm('Remove this finding?')">
    @Html.AntiForgeryToken()
    <button type="submit">Remove</button>
</form>
```

```
User clicks Remove → browser confirm() dialog
  │  User confirms
  ▼
POST /AssetVulnerabilities/Delete/12
  │
  ▼
AssetVulnerabilitiesController.Delete(int id) [POST]
  │
  ├─ var av = await _db.AssetVulnerabilities.FindAsync(id)
  │     SELECT * FROM AssetVulnerabilities WHERE Id = 12;
  │
  ├─ int assetId = av.AssetId   ← capture before removal
  │
  ├─ _db.AssetVulnerabilities.Remove(av)
  │
  ├─ await _db.SaveChangesAsync()
  │     DELETE FROM AssetVulnerabilities WHERE Id = 12;
  │
  └─ return RedirectToAction("Details", "Assets", new { id = assetId })
       │
       ▼
HTTP 302 → /Assets/Details/3
```

---

## Dependency Injection Lifecycle

`CmdbContext` is registered as **scoped** (the default for `AddDbContext`), meaning one instance is created per HTTP request and disposed at the end of the request. Controllers are also scoped. The DI container wires everything together:

```
Request begins
  │
  ▼
DI container creates scoped scope
  ├─ Instantiates CmdbContext (opens SQLite connection)
  └─ Instantiates AssetsController, injects CmdbContext
       │
       ▼
  Action executes (uses shared CmdbContext instance)
       │
       ▼
Request ends → scope disposed → CmdbContext disposed → SQLite connection closed
```

---

## Model Validation Flow

ASP.NET Core's model binding and validation runs before the action body executes:

```
POST body received
  │
  ▼
Model Binder: maps form fields → C# properties
  │
  ▼
Validation: evaluates Data Annotations
  │  [Required] on Hostname → error if empty
  │  [MaxLength(100)] → error if > 100 chars
  │  [Range(0.0, 10.0)] on CvssScore → error if out of range
  │
  ▼
ModelState populated with any errors
  │
  ▼
Controller checks: if (!ModelState.IsValid) return View(model)
  │                ↑
  │                Re-renders form; Razor tag helpers render
  │                validation error messages via <span asp-validation-for>
  │
  └─ (if valid) → continue to DB operation
```

Client-side validation (jQuery Validate + Unobtrusive) mirrors these same rules in the browser, providing immediate feedback before the form is submitted, powered by the `data-val-*` attributes that ASP.NET Core tag helpers emit.
