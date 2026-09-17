# GabsBlazorHybridApp 🌐📱

[![.NET 10](https://img.shields.io/badge/.NET-10.0-blueviolet.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![Blazor Hybrid](https://img.shields.io/badge/Blazor-Hybrid%20%26%20Web-brightgreen.svg?style=flat-square)](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
[![MudBlazor 9](https://img.shields.io/badge/UI-MudBlazor%209-blue.svg?style=flat-square)](https://mudblazor.com/)
[![MinVer Versioning](https://img.shields.io/badge/Versioning-MinVer%20Commit--Height-orange.svg?style=flat-square)](https://github.com/adamralph/minver)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%2015%2B%20%7C%20SQLite-blue.svg?style=flat-square)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?style=flat-square&logo=docker&logoColor=white)](https://www.docker.com/)
[![UUIDv7 Keys](https://img.shields.io/badge/Keys-Sequential%20UUIDv7-teal.svg?style=flat-square)](https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7)

An enterprise-grade, high-performance, cross-platform **Blazor Hybrid & Web** base solution built on **.NET 10.0** and **C# 14**. It demonstrates maximum code reuse by sharing UI layouts, responsive receipt-card data tables, domain models, and application services across both an interactive web application and native client applications (.NET MAUI for Android & Windows). The platform features host-agnostic hardware abstractions, PostgreSQL schema isolation, sequential UUIDv7 primary keys, adaptive disaster recovery, and fully automated build and deployment pipelines.

---

## 🛠️ Architecture Overview

The solution follows clean architectural separation across three core projects:

```mermaid
graph TD
    Shared[GabsHybridApp.Shared RCL]
    Web[GabsHybridApp.Web Host]
    Maui[GabsHybridApp.Maui Host]

    Web -->|References| Shared
    Maui -->|References| Shared
    
    subgraph Web App (Interactive Server Blazor)
        WebServices[WebHostCapabilities, WebLocationService, WebCameraService, ServerCookieAuthService]
        WebBackup[WebBackupStorageProvider, WebAppUpdateService, CircuitBackpressureCoordinator]
        PostgreSQL[(PostgreSQL 15+ / SQL Server - GabsHybridApp Schema)]
    end

    subgraph MAUI Native Client (Offline-First Cross-Platform)
        MauiServices[MauiHostCapabilities, MauiLocationService, MauiCameraService, MauiLocalAuthService]
        MauiTheme[MauiAppThemeService, MauiDeviceIdProvider, MauiBackupStorageProvider]
        SQLite[(Local SQLite db with WAL & Offline Fallback)]
    end

    Shared -.->|Injects Abstractions| WebServices
    Shared -.->|Injects Abstractions| MauiServices
```

### Projects in the Solution
1. **[`GabsHybridApp.Shared`](file:///GabsHybridApp/GabsHybridApp.Shared)** (Razor Class Library)
   - Targets `net10.0` with unified browser and native device compatibility.
   - Contains all shared Razor Components, Pages (Home, Orders, Products, User Accounts, Backup/Restore, Component Gallery), and Domain Models.
   - Powered by **MudBlazor 9** and **CodeBeam.MudBlazor.Extensions** with custom Dark Hero design tokens and responsive receipt cards (`<= 959.98px`).
   - Employs **Blazor-State** (Flux state management) and **Blazored.LocalStorage**.
2. **[`GabsHybridApp.Web`](file:///GabsHybridApp/GabsHybridApp.Web)** (ASP.NET Core Web Host)
   - Interactive Server Blazor with loopback-safe in-process SignalR notifications.
   - Centralized database factory dynamically supporting PostgreSQL and SQL Server with strict schema isolation (`"GabsHybridApp"`).
   - In-app update distribution hub and binary download routes (`/download/android`, `/download/windows`, `/api/app-version`).
   - Authenticated database backup and disaster recovery endpoints (`/download-db`).
3. **[`GabsHybridApp.Maui`](file:///GabsHybridApp/GabsHybridApp.Maui)** (.NET MAUI Client Host)
   - Native application hosting a Blazor WebView for Android and Windows.
   - Offline-first SQLite database (`hybrid_mauiDb.db`) with Write-Ahead Logging (WAL) and automatic migration.
   - Hardware integrations: camera photo picker, location services, flashlight control, network connectivity monitoring, and station hardware device ID binding.

---

## ✨ Core Features & Enterprise Capabilities

### 🛍️ Product Order Lifecycle & POS Counter
- **Comprehensive CRUD & Data Entry:** Built-in multi-line order management with automated numbering (`ORD-YYYYMMDD-XXXX`), line-item discounts, inventory calculation, tax/shipping computation, and order status workflow (`Pending` &rarr; `Confirmed` &rarr; `Processing` &rarr; `Shipped` &rarr; `Delivered` &rarr; `Cancelled`).
- **Quick Order POS Counter:** Single-click point-of-sale modal for fast order creation with live cart state.
- **Commercial Invoice Print View:** Dedicated `@media print` layout for professional invoice and packing slip generation.
- **Seed Data Pipeline:** Pre-seeded with realistic multi-line orders and a comprehensive 15-item product catalog.

### 📱 Responsive MudTable Receipt-Card Design
- On desktop viewports (`> 960px`), data tables render as spacious, sortable data grids with filters and search debounce.
- On mobile viewports (`<= 959.98px`), tables transform into vertically stacked, tactile **receipt cards** without bulky double-padding, ensuring readability and ergonomics on mobile phones and field tablets.
- **Zero Mobile Bulkiness:** Outer paper containers shed elevation and padding automatically (`mud-elevation-sm-3 bg-transparent bg-sm-surface`).

### 🛡️ User & Station Terminal Administration
- **Personnel Roster:** Multi-role user accounts (`Admin`, `User`, `MobileSync`), status toggles, locked default administrator protection, and password reset flows.
- **Batch CSV Import:** Built-in CSV parser and bulk user importer with real-time preview, role mapping, and duplicate validation.
- **Station Mode & Terminal Registration:** Hardware-bound device identity system (`IDeviceIdProvider`) for field tablets and workstation authorization.

### 💾 Disaster Recovery & Adaptive Schema Engine
- **Automated Backup Archives:** Generates compressed ZIP archives containing SQLite snapshots or formatted JSON entity dumps with SHA-256 integrity verification.
- **Adaptive Schema Introspection:** Introspects incoming database archives and maps mismatched column aliases dynamically via `SchemaIntrospectionService` and `AdaptiveTableReader` without throwing schema mismatch exceptions.
- **Safe Restore Verification:** Guarded restore flow requiring explicit confirmation before replacing local state.

### 🎨 Component Showcase Gallery (`ComponentsDemo.razor`)
A 5-tab gallery demonstrating MudBlazor 9 capabilities:
1. **Dialog Suite:** Alert modals, confirm dialogs, and full-screen mobile modal sheets.
2. **MudBlazor Charts:** Donut, bar, and line charts leveraging generic `.NET 10` typed models (`MudChart T="double"`).
3. **Hardware Keyframe Animations:** Fade, zoom, slide, and pulse CSS micro-interactions.
4. **Executive AI & Markdown Reporting:** Automated report generator with markdown/JSON export and clipboard copy.
5. **Mobile Grid Sandbox:** Interactive layout tester for responsive flex layouts.

### 🔑 Primary Key & GUID Strategy
The solution enforces a deliberate differentiation between entity ID generation strategies:
- **`Guid.NewGuid()` (UUIDv4):** Cryptographically random keys for security/privacy-sensitive entities (`UserAccount`). Prevents timing attacks, account enumeration, and registration date guessing.
- **`Guid.CreateVersion7()` (UUIDv7):** Time-ordered, sequential keys for operational/transactional entities (`Order`, `OrderItem`, `BackupRecord`, `RegisteredDevice`). Optimizes B-Tree index locality and eliminates page splits on PostgreSQL and SQLite.

### 🧠 Compile-Time Razor Markup & Stealth DOM Standards
- **Zero HTML Comments:** Standard `<!-- ... -->` comments are strictly avoided in `.razor` templates to prevent SignalR/HTTP payload leakage and DOM inspection bloat.
- **Razor Comments (`@* ... *@`):** Stripped completely at compile time by the Razor compiler for 100% production DOM hygiene.
- **Landmark Anchors:** Tagged with structured comments (e.g., `@* DESIGN LANGUAGE: Responsive Table Container *@`, `@* SECTION: Unified Filter Bar *@`) for rapid navigation and token-efficient agentic maintenance.

---

## 🗄️ PostgreSQL Schema Isolation & EF Core Policy

### 1. Schema-Level Isolation (`"GabsHybridApp"`)
To guarantee compatibility with modern PostgreSQL 15+ and cloud hosts (Neon, Supabase, AWS RDS) where non-superusers lack `CREATE` permissions on `public`:
- All application entities are mapped to the dedicated `"GabsHybridApp"` schema.
- EF Core migration history is explicitly scoped to `"GabsHybridApp"."__EFMigrationsHistory"`:
  ```csharp
  sql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
  ```
- Startup routines execute `CREATE SCHEMA IF NOT EXISTS "GabsHybridApp";` before EF Core attempts migration checks, preventing `42501 permission denied` errors.
- Baseline stamping protects existing schemas from re-running initial create scripts.

### 2. Local Database & Migration Reset Procedure
For local development and debugging (targeting `localhost:5432` or local SQLite):
```cmd
REM 1. Cascade wipe the dedicated schema in PostgreSQL:
psql -U postgres -d GabsHybridAppDb -c "DROP SCHEMA IF EXISTS \"GabsHybridApp\" CASCADE; CREATE SCHEMA \"GabsHybridApp\";"

REM 2. Reset migration snapshot and files:
del /q GabsHybridApp\GabsHybridApp.Web\Migrations\*

REM 3. Scaffold fresh initial migration:
dotnet ef migrations add InitialCreate --project GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj --startup-project GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj

REM 4. Apply migration cleanly:
dotnet ef database update --project GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj --startup-project GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj
```

---

## 📶 Host-Agnostic Device Service Architecture

Interface abstractions declared in `Shared` are implemented natively by each host:

| Service | Web App Implementation | MAUI Client Implementation |
| :--- | :--- | :--- |
| **`ILocationService`** | Browser Geolocation API | Native Geolocation (`Microsoft.Maui.Devices.Sensors.Geolocation`) |
| **`ICameraService`** | HTML5 Camera Stream Capture | MAUI Community Toolkit Camera & Media Picker |
| **`IFlashlightService`**| No-op (Browser fallback) | Native Torch API (`Microsoft.Maui.Devices.Flashlight`) |
| **`INetworkService`** | Browser Network Status API | Native Connectivity API with LAN offline protection |
| **`IDeviceIdProvider`** | Session/Browser Storage | Persistent Native Preferences (`Preferences.Default`) |
| **`IAppThemeService`** | LocalStorage + CSS Root Classes | Blazor Theme Sync + Native Android/iOS Status Bar Styling |
| **`IBackupStorageProvider`**| Server AppData Storage | Mobile FileSystem Storage (`FileSystem.AppDataDirectory`) |

---

## 🏷️ Automated Commit-Height Versioning

The solution automatically derives display versions, assembly versions, and Android `versionCode` from the Git commit height without requiring manual version bumps:
- **Base Version:** Defined as `1.0` via MinVer in `Directory.Build.props`.
- **Commit Code (`versionCode`):** Automatically derived from `git rev-list --count HEAD` (e.g., `158`).
- **Display Version:** `1.0.{commitCount} (hash)` displayed identically across Web and Mobile via `AppVersionInfo`.
- **Package Names:** Release binaries are automatically named `GabsHybridApp-v1.0.{commitCount}.apk`, `GabsHybridApp-v1.0.{commitCount}.exe`, and `GabsHybridApp-Windows-v1.0.{commitCount}.zip`.

---

## 🌐 Dual Deployment Architecture: IIS & Docker

The web host supports two first-class production deployment targets: **Windows IIS Host Automation** and **Linux / Multi-Arch Docker Containers**.

### 🐳 Option A: Docker Containerized Deployment
The repository includes a production-tuned, multi-stage **Alpine Linux Dockerfile** with full multi-architecture support (Apple Silicon / ARM64 and x64), non-root security context (`appuser:10001`), ICU globalization, and built-in healthchecks.

#### 1. Quick Launch with Docker Compose
```bash
# Set your connection string in .env or pass directly in the shell
export CONNECTION_STRING="Host=192.168.0.200;Port=5432;Database=GabsHybridAppDb;Username=postgres;Password=postgres;"

# Build and start container (mapped to host port 9081)
docker compose up -d --build
```
- **Port:** Maps container port `8080` to host `9081` (`http://localhost:9081`).
- **Healthcheck:** Automatic polling on `http://127.0.0.1:8080/` with 30s interval and 5s timeout.
- **Restart Policy:** `unless-stopped`.

#### 2. Standalone Docker Build & Run
```bash
# Build multi-stage Alpine image
docker build -t gabshybridapp-web:latest -f Dockerfile .

# Run with environment injection
docker run -d \
  --name gabshybridapp-web \
  -p 9081:8080 \
  -e ConnectionStrings__DefaultConnection="Host=192.168.0.200;Port=5432;Database=GabsHybridAppDb;Username=postgres;Password=postgres;" \
  --restart unless-stopped \
  gabshybridapp-web:latest
```

---

### 🪟 Option B: Windows IIS Host Automation

The repository includes enterprise-grade Windows IIS deployment scripts:

#### 1. Master Release Orchestrator (`publish.bat`)
Orchestrates client compilation and Web IIS publication with a 5-second interactive countdown handshake:
```cmd
.\publish.bat
```
- **Prompt 1 (5s Timeout):** Overwrite database & deploy fresh seed (`Y/N`, default `N`).
- **Prompt 2 (Interactive):** Build client applications (`Y/N`, default `N`).
- **Prompt 3:** IIS Site Name (defaults to solution name).
- **Auto-Detection & CLI Flags:** Auto-detects host architecture (`ARM64` / `x64`). Supports non-interactive overrides:
  ```cmd
  .\publish.bat -SiteName GabsHybridApp -Arch x64 -SkipApps -NoPrompt
  ```

#### 2. IIS Host Automation Scripts (`publish-iis.bat` & `deploy-iis.ps1`)
Automates AppPool lifecycle, `app_offline.htm` placement, directory permission grants (`icacls`), and IIS site bindings.

---

### 📱 Option C: Standalone Client Packager (`publish-app.bat`)
Compiles and packages native client distributions with SHA-256 checksums and `version.json` generation:
```cmd
.\publish-app.bat
```
- Produces: Android APK (`arm64-v8a` / `armeabi-v7a`) + Windows Single-File Self-Extracting Executable (`.exe`) & `.zip`.
- Automatically stages release binaries directly into the Web host's `wwwroot/releases/` folder for client auto-updates (`/download/android`, `/download/windows`).

---

## 🚀 Getting Started

### Prerequisites
- **.NET 10.0 SDK** (with ASP.NET Core & .NET MAUI workloads)
- **Visual Studio 2026** (recommended development environment)
- **PostgreSQL 15+** (or SQL Server / SQLite)
- **Docker Desktop** (optional, for containerized workflows)

### 🗄️ Database Configuration
Configure your local connection string in `GabsHybridApp/GabsHybridApp.Web/appsettings.Local.json`:
```json
{
  "ConnectionStrings": {
    "Schema": "GabsHybridApp",
    "DefaultConnection": "Host=localhost;Port=5432;Database=GabsHybridAppDb;Username=postgres;Password=postgres;"
  }
}
```

Apply migrations:
```powershell
dotnet ef database update --project GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj --startup-project GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj
```

### 🔨 Building the Solution
Always use minimal verbosity (`-v m`) to keep build feedback concise:
```powershell
dotnet build GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj -v m
```

### 🏃 Running the Application
- **Web App (Visual Studio):** Open `GabsHybridApp.sln` in Visual Studio 2026, set `GabsHybridApp.Web` as the startup project, and press **F5**.
- **Web App (Docker):** Run `docker compose up -d` and navigate to `http://localhost:9081`.
- **MAUI App (Windows):** Select `GabsHybridApp.Maui` with target framework `net10.0-windows10.0.19041.0` and press **F5**.
- **MAUI App (Android):** Select an Android Emulator or connected physical device and press **F5**.