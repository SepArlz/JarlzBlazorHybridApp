# Agent Instructions: Architecture, Git & Engineering Standards

These guidelines govern how the AI agent operates in this workspace to ensure optimal performance, prevent command execution errors, enforce clean architecture, and keep token consumption extremely efficient.

---

## 🚀 General Policy
- **Token-Light Commands:** The agent can execute these automatically (or propose them directly) without asking for manual user intervention.
- **Token-Heavy Commands:** The agent must **NOT** execute these blindly. Instead, the agent must present the command to the user for copy-pasting, along with structured options on how to proceed.

---

## 🟢 Token-Light Git Commands (Allowed Automatically)
The agent is authorized to run or propose the following lightweight commands directly:
* `git status` — To check current changes and file states.
* `git checkout <branch>` / `git checkout -b <new-branch>` — To switch/create branches.
* `git branch` — To list local branches.
* `git restore <file>` / `git reset <file>` — To discard uncommitted changes.
* `git stash` / `git stash pop` — To temporarily shelter changes.
* `git fetch` — To fetch updates from remote.

---

## 🚫 Git Commit / Add Policy (Do NOT run or propose commits)
- **User Managed:** The user manages all git staging, diff reviews, and commits directly in the Visual Studio 2026 Git Changes panel.
- **Protocol:** The agent must **NEVER** execute `git add`, `git commit`, `git push`, or any staging/committing commands. The agent does not need to present git diffs or prompt the user for permission to commit changes.

---

## 🔴 Token-Heavy Git Commands (Requires Structured Offer)
The following commands produce potentially massive outputs that can bloat context, eat tokens, or trigger auth/merge blockages:
* `git log` (without tight restrictions like `-n 5` or `--oneline`)
* `git diff` (across branches, commits, or whole repository without targeting specific files)
* `git blame <file>` (especially on large source files)
* `git show <commit>` (for commits modifying multiple files or large blocks of code)
* `git merge` / `git rebase` / `git pull` / `git push`

### 📋 The Action Protocol for Token-Heavy Commands:
When the agent needs information from or wants to run one of these heavy commands, it **MUST NOT** execute it immediately. Instead, it must print a dedicated interactive prompt to the user containing:
1. **Rationale:** Why this command/information is needed.
2. **Copy-Paste Section:** The exact Git command formatted in a code block.
3. **Execution Choice Prompt:** Present the user with the following three explicit choices:
   - **Option 1 (Manual Run - Recommended):** *"Run the command in your local terminal, then copy-paste only the relevant portion of the output back to me."*
   - **Option 2 (Constrained Run):** *"Let me run an optimized/constrained version of this command (e.g. adding `--oneline -n 5` or restricting to specific files) to save tokens."*
   - **Option 3 (Full Agent Run):** *"Proceed and run the full command anyway. (Warning: high token cost)."*

---

## 🛠️ Formatting Diffs and Logs
- When outputting diffs, always prefer target file paths to limit output sizes: `git diff -- <file-path>`.
- When retrieving commit history, default to: `git log --oneline -n 5`.

---

## 🔨 Build and Debug Verbosity Control
To optimize token usage and keep build feedback concise:
- **No Automatic dotnet run / App Server Launch:** The agent must **NEVER** execute `dotnet run` or start background web server processes. The user manages application execution and debugging directly inside Visual Studio 2026. The agent may only run `dotnet build` or `dotnet ef` commands for building and EF Core database migrations.
- **Initial Build/Debug Runs:** When there is a high likelihood of a successful run, initially limit or reduce verbosity.
  - For .NET CLI commands (`dotnet build`, `dotnet test`), append `-v q` (quiet) or `-v m` (minimal) verbosity flags (e.g., `dotnet build -v m`).
  - For Docker / Docker Compose, run in standard or quiet modes rather than verbose debugging modes.
- **On Failure:** If the build fails, rerun with normal/detailed verbosity (e.g., `-v n` or `-v d`) to output verbose logging for troubleshooting.

---

## 🎨 Component Styling Policy & MudBlazor Color Guidelines
- **Prefer Standard MudBlazor Styling:** Avoid introducing custom styling/custom CSS classes/custom `<style>` tags when standard MudBlazor styling options (e.g., helper classes, components, variables, elevation, padding attributes) can achieve the same outcome.
- **Goal:** Minimize custom CSS to facilitate smooth, future upgrades of MudBlazor and CodeBeam extensions without breaking overrides.
- **Semantic Theme Colors (Primary Preference):** Stick to standard MudBlazor semantic colors (`Color.Primary`, `Color.Secondary`, `Color.Tertiary`, `Color.Info`, `Color.Success`, `Color.Warning`, `Color.Error`, `Color.Dark`, `Color.Default`) as the primary choice to ensure seamless integration with theme and dark mode toggles.
- **Extended Material Palette (Do Not Be Constrained):**
  - When specialized dashboards, status indicators, badges, role chips, or complex categories require greater visual distinction than the standard enum allows, do not feel limited—use the comprehensive [MudBlazor Material Colors](https://mudblazor.com/features/colors#material-colors-csharp-and-material-colors) system.
  - **C# Material Colors (`MudBlazor.Colors`):** Access typed palette values such as `Colors.Teal.Default`, `Colors.Amber.Darken2`, `Colors.DeepOrange.Accent3`, `Colors.Cyan.Default`, `Colors.Indigo.Lighten1`, `Colors.BlueGrey.Default`.
  - **CSS Color Classes & Contrast Styling:**
    - Use MudBlazor color utility classes (e.g., `mud-theme-teal`, `mud-text-cyan`).
    - For high-contrast scannability on badges and chips, pair lightened backgrounds with darkened text:
      ```razor
      Style="@($"background-color: {Colors.Teal.Lighten5}; color: {Colors.Teal.Darken3};")"
      ```

---

## 🧠 Razor Markup & AI-Agent-Friendly Commenting Standards
All markup and template comments across `.razor` files must strictly adhere to these principles:

### 1. Mandatory Compile-Time Razor Comments (`@* ... *@`)
- **Zero HTML Comments:** Standard HTML comments (`<!-- ... -->`) are strictly forbidden in `.razor` files.
- **Always Use Razor Comments:** Every comment within template/markup sections must use compile-time Razor comment syntax: `@* ... *@`.
- **Rationale (Production DOM Hygiene & Stealth):** In Blazor, HTML comments are serialized over SignalR/HTTP and rendered directly into the browser DOM, leaking internal developer notes, section labels, and component structures to browser DevTools (F12). Razor comments are stripped completely at compile time by the Razor compiler, ensuring zero DOM bloat and maximum stealth in production builds.

### 2. AI-Agent-Friendly Comment Architecture
- **Semantic Landmark Tokens:** Comments must serve as high-signal landmark anchors for AI coding agents and human developers (e.g., `@* SECTION: Unified Filter & Action Bar *@`, `@* DRAWER: Order Form Edit Mode *@`, `@* KPI CARD: Active Devices *@`).
- **Efficient Agentic Editing:** Clear, descriptive semantic comments enable AI agents to perform fast grep targeting, lock onto exact replacement chunks without context drift, and minimize token overhead during automated modifications.
- **Clean Hygiene:** Keep comments concise, factual, and structurally relevant. Avoid dead commented-out code blocks or vague, uninformative notes.

### 3. Design Language Landmark Comments (`@* DESIGN LANGUAGE: ... *@`)
Whenever implementing a reusable, canonical UI pattern that is or should be replicated across other pages/components, tag it with a design language landmark:
- **Format:** `@* DESIGN LANGUAGE: [Pattern Name] — [Standard & Responsive Behavior] *@`
- **Core Standard Patterns:**
  - `Responsive Table Container`: Zero mobile elevation/padding, desktop surface paper (`<MudPaper Elevation="0" Class="pa-0 pa-sm-4 mud-elevation-sm-3 bg-transparent bg-sm-surface">`).
  - `Unified Action Bar`: Search debounce + Filter menu + Action button cluster (`flex-column flex-sm-row align-stretch align-sm-center justify-space-between gap-3`).
  - `Page Hero Header`: Operational title, icon badge, and quick action toolbar.
  - `KPI Metric Grid`: Responsive 2x2 mobile wrap / 4-column desktop dashboard cards with icon badges.
  - `Sticky Modal Footer`: Mobile full-width stacked / Desktop right-aligned action buttons (`<MudDialogActions Class="pa-4 border-t-1">`).
- **Agent Instruction:** When authoring new UI pages or refactoring components, search for `@* DESIGN LANGUAGE: ... *@` landmarks in canonical pages (`Orders.razor`, `UserAccounts.razor`, `Home.razor`) to copy exact structural classes and maintain system-wide UI cohesion.

### 4. Button Text & Icon Hygiene (No Redundant '+' Prefixes)
- **Zero Redundant Prefixes:** Never prefix button, chip, or action labels with `"+ "` (e.g. `"+ Add Item"`, `"+ Add Order"`) when the component already defines an addition/creation icon (such as `StartIcon="@Icons.Material.Filled.Add"`, `Icons.Material.Filled.AddComment`, `Icons.Material.Filled.AddAlert`).
- **Standard Format:** Always use clean, professional action phrases without symbol duplication: `"Add Item"`, `"Add Order"`, `"Create Account"`.

---

## 📱 Responsive MudTable Authoring Mindset & Mobile-First Standards
When building, refactoring, or updating MudBlazor data tables (`<MudTable>`), design with a **receipt-card mental model** for mobile viewports (`<= 959.98px`). On small screens, tables stop being two-dimensional grids and transform into vertically stacked, tactile cards managed automatically by the global design system (`wwwroot/css/MudBlazor.app.css`).

### 1. Zero Mobile Bulkiness (Outer Container Discipline)
* **Eliminate Double Padding & Elevation:** Never trap table cards inside nested, elevated mobile boxes.
* **Outer `MudPaper` Standard:** Container papers wrapping search bars and tables must shed elevation and padding on small screens:
  ```razor
  <MudPaper Elevation="0" Class="pa-0 pa-sm-4 mud-elevation-sm-3 bg-transparent bg-sm-surface">
  ```
  This allows table cards to utilize full mobile width without looking crammed, boxed-in, or double-padded against viewport margins.

### 2. Cell Content & Razor Markup Hygiene
* **Descriptive `DataLabel` Everywhere:** Every `<MudTd>` must have a clean, human-readable `DataLabel="..."`. In mobile mode, this label becomes the left-anchored field descriptor in the receipt card. Missing or generic labels break the card readability.
* **Keep Cell Markup Lightweight:** Avoid nesting inner `MudGrid`, `MudCard`, or heavy layout wrappers inside a `<MudTd>`. Place items (chips, badges, text, icon buttons) directly inside `<MudTd>`. The global CSS automatically clusters and right-anchors them.
* **Compact Mobile Actions:** Keep action buttons compact (`Size="Size.Small"`, icon buttons, or tight clusters) to avoid inflating the card's vertical height.

### 3. Human-Centric Text & Name Protection
* **No Mid-Name Wrapping:** Never allow individual names in multi-person lists or line items to break across lines mid-word. Use non-breaking spaces and hyphens:
  ```csharp
  string.Join(", ", names.Select(n => n.Replace(' ', '\u00A0').Replace('-', '\u2011')))
  ```
  Line wraps must only occur naturally at comma `, ` boundaries.
* **Long Text Scannability:** Keep remarks, notes, and addresses readable without pushing the label out of view.

### 4. Anti-Patterns (What to Avoid)
* ❌ **No Inline Cell Styling:** Do not add one-off `style="..."` or ad-hoc responsive overrides in `.razor` files for row/cell alignment—rely on `MudBlazor.app.css`.
* ❌ **No Rigid Column Widths:** Avoid hardcoded pixel widths (`Width="250px"`) on columns that compromise mobile flexibility.
* ❌ **No Excessive Inner Padding:** Avoid adding inner padding (`pa-4`) inside table cells that create bulky mobile cards.

---

## 🗄️ Entity Framework Core Migrations Policy
- **Automatic Migration Execution:** Whenever there is any change to an **EF Core Entity Model** (e.g., models in `GabsHybridApp.Shared/Models`) or **Data Seed / DbContext configuration** (e.g., `OrderDataSeeder.cs`, `HybridAppDbContext.cs`), the agent MUST automatically scaffold and apply EF Core migrations (`dotnet ef migrations add <MigrationName>` & `dotnet ef database update`).
- **Database Reset & Migration Reset Procedure (Local Development / Debugging ONLY):**
  - **Strict Local Scope & Production Guardrail:** This operation applies **EXCLUSIVELY** to the local developer / debugging database (the target active in `appsettings.Local.json` / `appsettings.Development.json`). It MUST **NEVER** target or wipe the production database (`192.168.0.200:5432` in `appsettings.json`).
  - Whenever the user instructs to **"delete db and reset ef migration"** (or similar reset phrasing), the agent MUST execute the full-reset protocol across **BOTH** Web (Local) and Client databases:
  1. **Web Database Wipe (Local Active Provider Dependent):**
     - **If PostgreSQL (Default / Active Standard - Local, Docker, or Cloud like Neon/Supabase):**
       - **Wipe Schema & Tables:** Execute a schema-level cascade drop to cleanly obliterate all tables, foreign keys, sequences, views, and migration tracking without requiring superuser `DROP DATABASE` permissions (100% compatible with cloud hosts like Neon/Supabase and local Docker/native):
         ```sql
         DROP SCHEMA IF EXISTS "GabsHybridApp" CASCADE;
         CREATE SCHEMA "GabsHybridApp";
         ```
       - **Reset Migration Files:** Remove all files in `GabsHybridApp/GabsHybridApp.Web/Migrations/*` (INCLUDING `HybridAppDbContextModelSnapshot.cs` so EF Core creates a clean initial snapshot rather than an empty diff).
       - **Scaffold Fresh Initial Migration:**
         ```cmd
         dotnet ef migrations add InitialCreate --project GabsHybridApp/GabsHybridApp.Web/GabsHybridApp.Web.csproj --startup-project GabsHybridApp/GabsHybridApp.Web/GabsHybridApp.Web.csproj
         ```
       - **Immediate Migration Apply (Optimal Standard):**
         ```cmd
         dotnet ef database update --project GabsHybridApp/GabsHybridApp.Web/GabsHybridApp.Web.csproj --startup-project GabsHybridApp/GabsHybridApp.Web/GabsHybridApp.Web.csproj
         ```
     - **If SQLite (Fallback / Legacy Mode):**
       - **File Deletion:** Delete local SQLite database files on disk: `GabsHybridApp/GabsHybridApp.Web/Data/hybrid_webDb.db*` (including `-wal` and `-shm`).
       - **Reset Migration Files:** Remove all files in `GabsHybridApp/GabsHybridApp.Web/Migrations/*` (INCLUDING `HybridAppDbContextModelSnapshot.cs`).
       - **Scaffold Fresh Initial Migration:**
         ```cmd
         dotnet ef migrations add InitialCreate --project GabsHybridApp/GabsHybridApp.Web/GabsHybridApp.Web.csproj --startup-project GabsHybridApp/GabsHybridApp.Web/GabsHybridApp.Web.csproj
         ```
       - **Immediate Migration Apply:**
         ```cmd
         dotnet ef database update --project GabsHybridApp/GabsHybridApp.Web/GabsHybridApp.Web.csproj --startup-project GabsHybridApp/GabsHybridApp.Web/GabsHybridApp.Web.csproj
         ```
  2. **MAUI (Client Database - Always SQLite):**
     - Clear/delete all local MAUI SQLite database files (`hybrid_mauiDb.db`, `hybrid_mauiDb.db-shm`, `hybrid_mauiDb.db-wal`) located in output/bin directories, Windows unpackaged AppData (`$env:LOCALAPPDATA\*\com.bernardgabon.gabshybridapp.maui\Data\hybrid_mauiDb.db*`), and packaged AppData (`$env:LOCALAPPDATA\Packages\*\LocalState\hybrid_mauiDb.db*`) so MAUI recreates a clean local database on the next app startup.

### 3. PostgreSQL Schema Isolation Policy
- **Schema-Scoped History Table:** EF Core migration tracking MUST target `"GabsHybridApp"."__EFMigrationsHistory"` using:
  ```csharp
  sql.MigrationsHistoryTable("__EFMigrationsHistory", schema);
  ```
- **Rationale:** Non-superusers in PostgreSQL 15+ and cloud providers (Neon, AWS RDS) have revoked `CREATE` permissions on `public`. Scoping the history table to `"GabsHybridApp"` guarantees 100% permission safety with zero public schema collisions.
- **Pre-Migration Safety:** `CREATE SCHEMA IF NOT EXISTS "GabsHybridApp";` must be executed before EF Core attempts history checks at runtime.

---

## 🌐 Unified Release & IIS Deployment Policy
Whenever the user requests deployment or publishing:
- **Standard Enterprise Release (Single Master Script):** (e.g., *"publish"*, *"deploy to iis"*, *"publish iis"*, *"publish all"*, *"update iis site"*, *"publish web"*, *"publish with db overwrite"*)
  - The agent MUST execute or invoke: **`.\publish.bat`** (or `.\publish-iis.bat`).
  - **Modular Architecture:** `publish.bat` orchestrates execution by delegating to `publish-app.bat` (if requested) and `publish-iis.bat`.
  - **Shared 5-Second Initial Wait Window & Interactive Handshake:**
    1. **Prompt 1 (DB Overwrite):** Default `N` (Preserve production DB); press `'Y'` to overwrite DB & deploy fresh seed; press `Enter` to select default `NO`.
    2. **Prompt 2 (Client Apps):** Shares the initial 5s wait window; if Prompt 1 times out unattended, Prompt 2 immediately defaults to `NO` without extra delay; if user interacts with Prompt 1, Prompt 2 waits for explicit user input (`Enter` = default `NO`, `'Y'` = rebuild).
    3. **Prompt 3 (IIS Site Name):** Automatically defaults to the solution filename (`.sln`) located in the root directory (e.g. `GabsHybridApp`); press `Enter` to keep default or type a custom site name.
  - **Architecture Auto-Detection & Overrides:** Automatically detects host architecture (`ARM64` or `x64`). If detection fails, prompts with default `x64`. Parameter overrides are supported: `.\publish.bat -SiteName <Name>`, `.\publish.bat -Arch arm64`, `.\publish.bat -Arch x64`, `.\publish.bat -Arm64`, `.\publish.bat -OverwriteDb`, `.\publish.bat -BuildApps`, `.\publish.bat -SkipApps`, `.\publish.bat -NoPrompt`.
- **Standalone Client App Only:** (e.g., *"publish app"*, *"build apk"*, *"publish maui"*)
  - The agent MUST execute or invoke: **`.\publish-app.bat`**.
- **Mandatory Response Disclaimer & Copy-Paste Fallback:**
  - In every deployment response, the agent MUST include a clear disclaimer advising that if the IIS deployment is not reflecting or failed due to environment/permission/UAC restrictions, the developer should run the batch script manually in their local terminal, and always provide separate, clean copy-pastable code blocks with explanations outside the boxes:

    Master Publisher (Interactive 5-Second Prompts for DB Overwrite & Client Apps):
    ```cmd
    .\publish.bat
    ```

    Client Apps Only (Android APK + Windows Single-File SFX):
    ```cmd
    .\publish-app.bat
    ```

---

## 🏷️ Automated Commit-Height Versioning Protocol (Web & Client Unified)
The solution automatically derives display versions, assembly versions, and Android `versionCode` from the Git commit height (`git rev-list --count HEAD`) unified across both the Web app and Client apps (Android APK + Windows SFX) without requiring manual Git tags.

### 1. Unified Version Architecture
- **Base Version:** Defined as `1.0` (with MinVer baselined at `1.0` in `Directory.Build.props`).
- **Build / Commit Code (`versionCode`):** Automatically derived from `git rev-list --count HEAD` (e.g., `154`, `155`, `156`...).
- **Display Version:** `1.0.{commitCount}` (e.g., `1.0.154`), displayed identically in both Web and Mobile (`v1.0.154 (abcdef1)` via `AppVersionInfo`).
- **Android Package Upgradability:** Because `versionCode` increments on every commit, Android OS always permits upgrades without version conflicts.
- **Package Filenames:** Client distribution packages are automatically named `GabsHybridApp-v1.0.{commitCount}.apk`, `GabsHybridApp-v1.0.{commitCount}.exe`, and `GabsHybridApp-Windows-v1.0.{commitCount}.zip`, preventing browser download cache collisions.

### 2. Optional Milestone Tag Triggers
Git tags are completely optional and not required for normal daily publishing. However, if the user explicitly instructs or mentions creating a formal milestone tag (e.g., *"git tag"*, *"make a git release"*, *"Tag release v1.1.0"*):
1. **Verify Git State:** Check `git status` to ensure all necessary changes are committed cleanly.
2. **Determine & Propose Target Tag:** Recommend the appropriate SemVer tag (e.g. `v1.0.1`, `v1.1.0`, `v2.0.0`) based on recent changes.
3. **Structured Offer (Manual Command vs. Agent Execution):** The agent MUST provide:
   - **Copy-Paste Section:** The exact Git command formatted in a code block for the user to run in their local terminal:
     ```cmd
     git tag v1.0.1
     ```
   - **Dual-Choice Selection:**
     - **Option 1 (Manual Run - Default):** Run the command in your local terminal.
     - **Option 2 (Agent Run):** Reply to let the AI agent execute the `git tag` command, rebuild/publish (`publish-app.bat` or `publish.bat`), and proceed with the push handshake automatically.

---

## 🔑 Primary Key & GUID Strategy (Sequential UUIDv7 vs Non-Sequential Identity)
To balance B-Tree index performance, offline synchronization, and human privacy/security, the solution enforces a strict differentiation between entity ID generation strategies:

### 1. Personal Identity, Privacy & Security-Sensitive Entities (`Guid.NewGuid()`)
* **Applicable Models:** `UserAccount` and session credentials.
* **Generation Method:** Cryptographically random UUIDv4 (`Guid.NewGuid()`).
* **Rationale:**
  * **Enumeration Resistance & Anti-Scraping:** UUIDv7 reveals exact creation timestamps in plaintext and allows sequence guessing (`Id + 1` or timing probes). Random UUIDv4 prevents attackers or curious users from deducing account registration timing or iterating personnel.
  * **Data Privacy:** Personal accounts must never use sequential keys.
  * **Low Write Volume:** User rosters do not create high-frequency disk page splits, making index sorting benefits irrelevant for accounts.

### 2. Operational, Transactional & Asset Entities (`Guid.CreateVersion7()`)
* **Applicable Models:** `Order`, `OrderItem`, `BackupRecord`, `RegisteredDevice`.
* **Generation Method:** `Guid.CreateVersion7()` (native .NET 9/10 UUIDv7).
* **Rationale:**
  * **B-Tree Index Locality:** Sequential UUIDv7 embeds a 48-bit Unix millisecond timestamp at the front, ensuring append-mostly insertion and eliminating B-Tree page splits and disk fragmentation in PostgreSQL and SQLite.
  * **Offline-First Independence:** Allows mobile tablets to mint primary and foreign keys in the field without central sequence generators.
  * **Zero Privacy Penalty:** Non-human transactions and audit events have no personal identity exposure. Sequential ordering provides pure database performance benefits with zero downsides.

---

## 🐘 Local Debugging & PostgreSQL Environment Setup
- **Production Architecture:** Production PostgreSQL runs on the host server (`192.168.0.200:5432;Database=GabsHybridAppDb`). Production settings in `appsettings.json` remain untouched as-is.
- **Local Developer Environment:** The developer works natively in Windows. Local machine debugging uses `appsettings.Local.json` targeting `localhost:5432` or active LAN server with native PostgreSQL.

When assisting the developer with local development and debugging of `GabsHybridApp.Web`:

### 1. The Universal PostgreSQL Defaults
| Setting | Default Value | Notes |
| :--- | :--- | :--- |
| **Port** | `5432` | The standard port every library, framework, and GUI assumes. |
| **Superuser** | `postgres` | The root/admin username of PostgreSQL. |
| **Password** | `postgres` | Local development default password convention. |
| **Initial Database** | `postgres` | The default maintenance database created during initialization. |
| **Default Schema** | `GabsHybridApp` | Solution operational schema (`public` if unassigned). |

### 2. Local Connection String Specification
Configured in `GabsHybridApp/GabsHybridApp.Web/appsettings.Local.json`:
```json
"ConnectionStrings": {
  "Schema": "GabsHybridApp",
  "DefaultConnection": "Host=192.168.0.200;Port=5432;Database=GabsHybridAppDb;Username=pguser;Password=pgpwd;",
  "DefaultConnectionSqlite": "Data/hybrid_webDb.db"
}
```

### 3. Automated Silent Detection & Installation Policy
Whenever the agent detects the absence of PostgreSQL (e.g. EF fails to connect, `localhost:5432` port check fails, or the user expresses the need for local database/debugging), the agent MUST perform the setup **silently and automatically without prompting for confirmation**, then notify the user that it's good to go:

1. **Silent Detection:**
   ```powershell
   (Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue).TcpTestSucceeded
   ```
2. **Autonomous Silent Provisioning (Native Windows via Winget):**
   Install native PostgreSQL service for Windows silently:
   ```cmd
   winget install PostgreSQL.PostgreSQL.17 --silent --accept-package-agreements --accept-source-agreements
   ```
   *(Ensure Windows service `postgresql-x64-17` is running).*
3. **Silent Migration Apply:** Once port 5432 is live, silently apply EF Core migrations:
   ```cmd
   dotnet ef database update --project GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj --startup-project GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj
   ```
4. **User Confirmation:** Simply report to the user that local PostgreSQL is configured, seeded/migrated, and good to go.
