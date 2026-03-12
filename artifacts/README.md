# ATLAS — Configuration Management Database

A lightweight web application for tracking IT assets and their associated security vulnerabilities.

## Purpose

This application serves as a centralized record system for:
- **Assets** — servers, workstations, network devices, cloud instances, and applications
- **Vulnerabilities** — a library of known vulnerabilities (CVEs or custom entries) with severity ratings
- **Findings** — linkages between assets and vulnerabilities, each with a tracked remediation status

---

## Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Run

```bash
cd /Users/snickers/ATLAS
dotnet run
```

The app starts at:
- HTTP:  `http://localhost:5130`
- HTTPS: `https://localhost:7141`

The SQLite database (`cmdb.db`) is created automatically on first run. No migrations or setup commands are required.

---

## Project Structure

```
ATLAS/
├── artifacts/                  # This documentation
│   ├── README.md               # Overview and quickstart (this file)
│   ├── architecture.md         # System components and how they fit together
│   ├── data-model.md           # Entities, fields, enums, and relationships
│   ├── request-lifecycle.md    # How an HTTP request flows through the app
│   └── ADR.md                  # Architecture Decision Records
│
├── Controllers/
│   ├── HomeController.cs       # Dashboard: aggregated stats and recent activity
│   ├── AssetsController.cs     # CRUD for Asset records
│   ├── VulnerabilitiesController.cs  # CRUD for Vulnerability records
│   └── AssetVulnerabilitiesController.cs  # Create/Edit/Delete findings (asset–vuln links)
│
├── Data/
│   └── CmdbContext.cs          # EF Core DbContext; defines tables and relationships
│
├── Models/
│   ├── Asset.cs                # Abstract Asset base class + 6 concrete subtypes + all enums
│   │                           #   Subtypes: Computer, NetworkDevice, Printer,
│   │                           #             SoftwareApplication, MobileDevice, CloudResource
│   ├── AssetFormViewModel.cs   # Flat view-model used by Create/Edit forms (all asset types)
│   ├── Vulnerability.cs        # Vulnerability entity + Severity enum
│   └── AssetVulnerability.cs   # Join entity + RemediationStatus enum
│
├── Views/
│   ├── Home/Index.cshtml       # Dashboard page
│   ├── Assets/                 # Index, Details, Create, Edit, Delete
│   ├── Vulnerabilities/        # Index, Details, Create, Edit, Delete
│   ├── AssetVulnerabilities/   # Create, Edit
│   └── Shared/
│       ├── _Layout.cshtml      # Site-wide layout, navigation bar, global CSS
│       └── _ValidationScriptsPartial.cshtml
│
├── wwwroot/                    # Static assets (Bootstrap 5, jQuery, site.css)
├── Program.cs                  # App bootstrap, DI registration, middleware pipeline
├── appsettings.json            # Connection string and logging config
└── ATLAS.csproj                 # Project file, NuGet package references
```

---

## Key Workflows

| Goal | Steps |
|------|-------|
| Add a Computer | Assets → Add Asset → select *Computer* → fill Computer fields → Save |
| Add a Network Device | Assets → Add Asset → select *Network Device* → fill Network fields → Save |
| Add a Cloud Resource | Assets → Add Asset → select *Cloud Resource* → fill Cloud fields → Save |
| Add any other asset type | Assets → Add Asset → choose category → fill type-specific section → Save |
| Log a new CVE | Vulnerabilities → Add Vulnerability → fill CVE details → Save |
| Record a finding | Asset Details page → Assign Vulnerability → select vuln → Assign |
| Mark as remediated | Asset Details → finding row → Edit → set Status to Remediated → Save |
| Get a risk overview | Dashboard — shows critical/high open counts and recent findings |

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.3 | ORM + SQLite provider |
| `Microsoft.EntityFrameworkCore.Design` | 9.0.3 | EF tooling (migrations, scaffolding) |
| Bootstrap | 5.x (bundled) | Responsive UI components |
| jQuery + jQuery Validation | bundled | Client-side form validation |
