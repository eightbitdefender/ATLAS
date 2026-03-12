# Data Model

## Entity Relationship Diagram

```
                    ┌───────────────────────────────────────────────────┐
                    │              Asset  (abstract base)               │
                    │  Id · Name · Description · Owner · Department     │
                    │  Location · Status · CreatedAt · LastUpdated      │
                    │  Notes · [AssetVulnerabilities navigation]        │
                    │                                                   │
                    │  Discriminator column identifies concrete type:   │
                    ├──────────┬──────────┬──────────┬──────────────────┤
                    │Computer  │NetworkDev│ Printer  │SoftwareApp  │... │
                    └──────────┴──────────┴──────────┴─────────────┴───┘
                          │
                          │ 1
                          │
                          │ *
              ┌───────────▼────────────┐         ┌──────────────────────┐
              │   AssetVulnerability   │         │    Vulnerability      │
              ├────────────────────────┤         ├──────────────────────┤
              │ Id (PK)                │         │ Id (PK)              │
              │ AssetId (FK → Asset)   ├────────►│ CveId    nullable    │
              │ VulnerabilityId (FK)   │ *     1 │ Title    NOT NULL    │
              │ Status   enum          │         │ Severity enum        │
              │ DetectedAt  datetime   │         │ CvssScore nullable   │
              │ RemediatedAt nullable  │         │ ...                  │
              │ Notes    nullable      │         └──────────────────────┘
              └────────────────────────┘
```

All asset subtypes share one `Assets` table (TPH). The `Discriminator` column
stores the class name (`Computer`, `NetworkDevice`, etc.).

---

## Asset Hierarchy

**File:** `Models/Asset.cs`

### Abstract Base: `Asset`

Fields shared by every asset type:

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | `int` | PK, auto-increment | Surrogate primary key |
| `Discriminator` | `string` | Set by EF Core | Class name identifying the concrete subtype |
| `Name` | `string` | Required, max 200 | Human-readable name / hostname |
| `Description` | `string?` | Max 2000 | Purpose, role, or context |
| `Owner` | `string?` | Max 100 | Person responsible |
| `Department` | `string?` | Max 100 | Business unit or team |
| `Location` | `string?` | Max 200 | Physical or logical location |
| `Status` | `AssetStatus` (enum) | Default: `Active` | Lifecycle state |
| `CreatedAt` | `DateTime` | Set on insert | UTC timestamp |
| `LastUpdated` | `DateTime?` | Set on edit | UTC timestamp |
| `Notes` | `string?` | Max 500 | Free-text notes |

Abstract members on the base class (implemented by each subtype):
- `AssetCategory Category` — high-level category label
- `string SubTypeLabel` — human-readable subtype (e.g. "Server", "Router")
- `string? Identifier` — primary network/location identifier for list views (IP, URL, ARN, etc.)

---

### Concrete Subtype: `Computer`

Additional columns stored in the `Assets` table:

| Column | Type | Description |
|--------|------|-------------|
| `ComputerType` | `ComputerType` enum | Server, Workstation, or Laptop |
| `IpAddress` | `string?` max 45 | IPv4 or IPv6 address |
| `OperatingSystem` | `string?` max 100 | OS name and version |
| `RamGb` | `int?` | RAM in gigabytes |
| `CpuModel` | `string?` max 100 | Processor model string |
| `DomainJoined` | `bool` | Whether joined to an AD/LDAP domain |
| `SerialNumber` | `string?` max 100 | Manufacturer serial number |

**Enum `ComputerType`:** `Server`, `Workstation`, `Laptop`

---

### Concrete Subtype: `NetworkDevice`

| Column | Type | Description |
|--------|------|-------------|
| `NetworkDeviceType` | `NetworkDeviceType` enum | Type of network device |
| `IpAddress` | `string?` max 45 | Management IP address |
| `MacAddress` | `string?` max 20 | Hardware MAC address |
| `Firmware` | `string?` max 50 | Firmware/OS version string |
| `PortCount` | `int?` | Number of physical ports |
| `Managed` | `bool` | Whether the device is actively managed |

**Enum `NetworkDeviceType`:** `Router`, `Switch`, `Firewall`, `WirelessAccessPoint`, `LoadBalancer`, `Other`

---

### Concrete Subtype: `Printer`

| Column | Type | Description |
|--------|------|-------------|
| `PrinterType` | `PrinterType` enum | Technology type |
| `IpAddress` | `string?` max 45 | Network IP (if networked) |
| `Model` | `string?` max 100 | Manufacturer and model string |
| `ColorCapable` | `bool` | Whether the printer supports color output |
| `NetworkConnected` | `bool` | Whether the printer is network-connected |

**Enum `PrinterType`:** `Laser`, `Inkjet`, `Thermal`, `DotMatrix`

---

### Concrete Subtype: `SoftwareApplication`

| Column | Type | Description |
|--------|------|-------------|
| `ApplicationType` | `ApplicationType` enum | Category of application |
| `Vendor` | `string?` max 100 | Software publisher |
| `Version` | `string?` max 50 | Current deployed version |
| `Url` | `string?` max 500 | Primary application URL |
| `LicenseType` | `string?` max 100 | License model (e.g. SaaS, Enterprise, Open Source) |

**Enum `ApplicationType`:** `WebApp`, `Desktop`, `Mobile`, `API`, `Database`, `Middleware`, `Other`

---

### Concrete Subtype: `MobileDevice`

| Column | Type | Description |
|--------|------|-------------|
| `MobileDeviceType` | `MobileDeviceType` enum | Phone or Tablet |
| `OperatingSystem` | `string?` max 100 | OS name and version (e.g. iOS 17) |
| `Imei` | `string?` max 20 | IMEI hardware identifier |
| `PhoneNumber` | `string?` max 20 | Assigned phone number |
| `Carrier` | `string?` max 50 | Mobile network carrier |

**Enum `MobileDeviceType`:** `Phone`, `Tablet`

---

### Concrete Subtype: `CloudResource`

| Column | Type | Description |
|--------|------|-------------|
| `CloudProvider` | `CloudProvider` enum | Cloud platform |
| `ResourceType` | `string?` max 100 | Service type (e.g. EC2, App Service, S3) |
| `Region` | `string?` max 100 | Deployment region (e.g. `us-east-1`) |
| `AccountId` | `string?` max 100 | Cloud account / subscription ID |
| `ResourceId` | `string?` max 200 | Resource ID or ARN |

**Enum `CloudProvider`:** `AWS`, `Azure`, `GCP`, `Oracle`, `Other`

---

### Enum: `AssetStatus`

| Value | Description |
|-------|-------------|
| `Active` | In production use |
| `Inactive` | Powered off or not in use |
| `Decommissioned` | Retired from service |
| `Unknown` | Status not yet determined |

---

## AssetFormViewModel

**File:** `Models/AssetFormViewModel.cs`

A flat view-model used exclusively by the Create and Edit forms. Contains all
fields from all subtypes as optional properties. The `Category` property
determines which concrete `Asset` subclass the controller instantiates on save.

This class is **not** persisted to the database directly; it is a form-binding
intermediary only.

---

## Entity: Vulnerability

**File:** `Models/Vulnerability.cs`

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | `int` | PK, auto-increment | Surrogate primary key |
| `CveId` | `string?` | Max 30 | Standard CVE identifier (e.g. `CVE-2024-12345`). Nullable for internal/custom vulnerabilities |
| `Title` | `string` | Required, max 200 | Short descriptive name |
| `Description` | `string?` | Max 2000 | Full description |
| `Severity` | `Severity` (enum) | Required | Risk rating |
| `CvssScore` | `double?` | Range 0.0–10.0 | CVSS base score |
| `AffectedSoftware` | `string?` | Max 500 | Software name and version range affected |
| `RemediationGuidance` | `string?` | Max 500 | Recommended fix or workaround |
| `DiscoveredAt` | `DateTime` | Set on insert | UTC timestamp when added to the library |

### Enum: Severity

Ordered from highest to lowest risk. EF Core stores as integer.

| Value | Int | CVSS Range (approximate) |
|-------|-----|--------------------------|
| `Critical` | 0 | 9.0–10.0 |
| `High` | 1 | 7.0–8.9 |
| `Medium` | 2 | 4.0–6.9 |
| `Low` | 3 | 0.1–3.9 |
| `Informational` | 4 | 0.0 |

---

## Entity: AssetVulnerability

**File:** `Models/AssetVulnerability.cs`

The join entity recording a specific vulnerability on a specific asset.

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `Id` | `int` | PK, auto-increment | Surrogate primary key |
| `AssetId` | `int` | FK → `Asset.Id` | The affected asset (any concrete subtype) |
| `VulnerabilityId` | `int` | FK → `Vulnerability.Id` | The vulnerability |
| `Status` | `RemediationStatus` (enum) | Default: `Open` | Current remediation state |
| `DetectedAt` | `DateTime` | Set on insert | When the finding was recorded |
| `RemediatedAt` | `DateTime?` | Set when Status → Remediated | When remediation was confirmed |
| `Notes` | `string?` | Max 500 | Context-specific notes |

### Enum: RemediationStatus

| Value | Description |
|-------|-------------|
| `Open` | Confirmed, no action taken |
| `InProgress` | Remediation underway |
| `Remediated` | Patch or fix applied and verified |
| `Accepted` | Risk accepted; no fix planned |
| `FalsePositive` | Finding determined to be incorrect |

---

## Database

SQLite database `cmdb.db` in the application working directory.

**Schema management:** `EnsureCreated()` at startup. On first launch after the OO
refactor, `Program.cs` detects the absence of the `Discriminator` column and calls
`EnsureDeleted()` before `EnsureCreated()` to rebuild the schema (see ADR-013).

### Table Layout

| Table | Notes |
|-------|-------|
| `Assets` | Single table for all asset subtypes (TPH). Columns for all subtypes present; inapplicable columns are NULL. |
| `Vulnerabilities` | One row per known vulnerability. |
| `AssetVulnerabilities` | Join table with remediation payload. |

### Foreign Key Cascade Behavior

Both FKs on `AssetVulnerability` use EF Core's default cascade delete:
- Deleting an `Asset` automatically deletes all its `AssetVulnerability` rows
- Deleting a `Vulnerability` automatically deletes all its `AssetVulnerability` rows
