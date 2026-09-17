<# :
@echo off
setlocal
cd /d "%~dp0"

:: Check for Administrator privileges (Force UAC elevation with passed arguments)
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ==========================================================
    echo  Requesting Administrator Privileges...
    echo ==========================================================
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$a = '%*'; if ($a) { Start-Process cmd -ArgumentList \"/c `\"`\"%~f0`\"`\" $a\" -Verb RunAs } else { Start-Process cmd -ArgumentList \"/c `\"`\"%~f0`\"`\"\" -Verb RunAs }"
    exit /b
)

echo ==========================================================
echo  GabsHybridApp - Web IIS Host Publisher (Web Only)...
echo ==========================================================

powershell -NoProfile -ExecutionPolicy Bypass -Command "$script = [System.IO.File]::ReadAllText('%~f0'); & ([ScriptBlock]::Create($script)) %*"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Process failed with exit code %ERRORLEVEL%.
    echo "%*" | findstr /i "\-NoPause" >nul
    if errorlevel 1 pause
    exit /b %ERRORLEVEL%
)

echo.
echo Process completed successfully.
echo "%*" | findstr /i "\-NoPause" >nul
if errorlevel 1 pause
exit /b
#>

# ==============================================================================
# PowerShell Execution Logic (Web Host Only -> IIS Verification + Build + Publish)
# ==============================================================================
param(
    [switch]$OverwriteDb = $false,
    [string]$SiteName = "",
    [ValidateSet('x64', 'arm64', 'auto')][string]$Arch = 'auto',
    [switch]$Arm64 = $false,
    [switch]$x64 = $false,
    [switch]$NoPrompt = $false,
    [switch]$NoPause = $false
)

$ErrorActionPreference = "Stop"

# Configure Execution Policy so future scripts are never blocked
try {
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force -ErrorAction SilentlyContinue
    Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned -Force -ErrorAction SilentlyContinue
    Set-ExecutionPolicy -Scope LocalMachine -ExecutionPolicy RemoteSigned -Force -ErrorAction SilentlyContinue
} catch {}

Clear-Host
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   GabsHybridApp - Web IIS Host Publisher (Web Only)      " -ForegroundColor Cyan
Write-Host "   Running as Administrator: YES                         " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$Configuration = "Release"

$RepoRoot = $PSScriptRoot
if (-not $RepoRoot) {
    $RepoRoot = (Get-Location).Path
}

# ------------------------------------------------------------------------------
# Resolve Default IIS Site Name from .SLN File
# ------------------------------------------------------------------------------
$defaultSiteName = "GabsHybridApp"
try {
    $slnFile = Get-ChildItem -Path $RepoRoot -Filter "*.sln" -File | Select-Object -First 1
    if ($slnFile) {
        $defaultSiteName = [System.IO.Path]::GetFileNameWithoutExtension($slnFile.Name)
    }
} catch {}

$resolvedSiteName = if ($PSBoundParameters.ContainsKey('SiteName') -and -not [string]::IsNullOrWhiteSpace($SiteName)) {
    $SiteName.Trim()
} else {
    $defaultSiteName
}

# ------------------------------------------------------------------------------
# Resolve Windows Host & Target Architecture
# ------------------------------------------------------------------------------
$detectedHostArch = $null
$rawArch = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
if ($rawArch -match '(?i)arm64') {
    $detectedHostArch = 'arm64'
} elseif ($rawArch -match '(?i)(amd64|x64|x86_64)') {
    $detectedHostArch = 'x64'
}

$resolvedArch = $null
if ($PSBoundParameters.ContainsKey('Arm64') -and $Arm64) {
    $resolvedArch = 'arm64'
} elseif ($PSBoundParameters.ContainsKey('x64') -and $x64) {
    $resolvedArch = 'x64'
} elseif ($PSBoundParameters.ContainsKey('Arch') -and $Arch -ne 'auto') {
    $resolvedArch = $Arch
} elseif ($detectedHostArch) {
    # Default directly to what was detected on the machine
    $resolvedArch = $detectedHostArch
} else {
    # Detection failed: prompt user with default as x64
    if (-not $NoPrompt) {
        try {
            Write-Host -NoNewline "Could not detect Windows architecture. Target architecture [x64/arm64] (Default: x64): "
            $timeoutSeconds = 5
            $startTime = [System.Diagnostics.Stopwatch]::StartNew()
            $chosen = $null
            while ($startTime.Elapsed.TotalSeconds -lt $timeoutSeconds) {
                if ([Console]::KeyAvailable) {
                    $k = [Console]::ReadKey($true)
                    if ($k.Key -eq [ConsoleKey]::A) {
                        $chosen = 'arm64'; Write-Host "arm64" -ForegroundColor Cyan; break
                    } elseif ($k.Key -eq [ConsoleKey]::X -or $k.Key -eq [ConsoleKey]::Enter) {
                        $chosen = 'x64'; Write-Host "x64 (Default)" -ForegroundColor Gray; break
                    }
                }
                Start-Sleep -Milliseconds 100
            }
            if (-not $chosen) {
                Write-Host "x64 (Default)" -ForegroundColor Gray
                $chosen = 'x64'
            }
            $resolvedArch = $chosen
        } catch {
            Write-Host "x64 (Default)" -ForegroundColor Gray
            $resolvedArch = 'x64'
        }
    } else {
        $resolvedArch = 'x64'
    }
}
$targetRid = "win-$resolvedArch"

# ------------------------------------------------------------------------------
# Interactive Prompts (if run standalone without -NoPrompt)
# ------------------------------------------------------------------------------
if (-not $NoPrompt) {
    if (-not $PSBoundParameters.ContainsKey('OverwriteDb')) {
        try {
            $timeoutSeconds = 5
            $startTime = [System.Diagnostics.Stopwatch]::StartNew()
            Write-Host -NoNewline "Overwrite Database (Fresh Seed)? [y/N]: "
            
            while ($startTime.Elapsed.TotalSeconds -lt $timeoutSeconds) {
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)
                    if ($key.Key -eq [ConsoleKey]::Y) {
                        $OverwriteDb = $true
                        Write-Host "Y" -ForegroundColor Magenta
                    } elseif ($key.Key -eq [ConsoleKey]::Enter) {
                        $OverwriteDb = $false
                        Write-Host "N (Default)" -ForegroundColor Gray
                    } else {
                        $OverwriteDb = $false
                        Write-Host "N" -ForegroundColor Gray
                    }
                    break
                }
                Start-Sleep -Milliseconds 100
            }
            if ($startTime.Elapsed.TotalSeconds -ge $timeoutSeconds) {
                Write-Host "N (Default)" -ForegroundColor Gray
                $OverwriteDb = $false
            }
        } catch {
            Write-Host "N (Default)" -ForegroundColor Gray
            $OverwriteDb = $false
        }
    }

    if (-not $PSBoundParameters.ContainsKey('SiteName')) {
        Write-Host -NoNewline "IIS Site Name [$defaultSiteName]: "
        $enteredSite = (Read-Host).Trim()
        if (-not [string]::IsNullOrWhiteSpace($enteredSite)) {
            $resolvedSiteName = $enteredSite
        }
    }
}

$siteName = $resolvedSiteName
$AppPoolName = "${siteName}Pool"

$OutputDir = Join-Path $RepoRoot "_publish"
$WebProjectPath = Join-Path $RepoRoot "GabsHybridApp\GabsHybridApp.Web\GabsHybridApp.Web.csproj"

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Configuration:                                         " -ForegroundColor Cyan
if ($OverwriteDb) {
    Write-Host "   - Database:    OVERWRITE & FRESH SEED (Auto-Backup)" -ForegroundColor Magenta
} else {
    Write-Host "   - Database:    PRESERVE PRODUCTION DB" -ForegroundColor Green
}
Write-Host "   - IIS Site:    $siteName (AppPool: $AppPoolName)" -ForegroundColor Green
Write-Host "   - Target Arch: $targetRid (Detected Host: $(if ($detectedHostArch) { $detectedHostArch } else { 'Unknown' }))" -ForegroundColor Cyan
Write-Host "   - Target:      GabsHybridApp.Web -> Local IIS Server" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# ------------------------------------------------------------------------------
# STEP 1: Verify & Install Required IIS Features (Client or Server)
# ------------------------------------------------------------------------------
Write-Host "[1/8] Checking & Configuring IIS Environment..." -ForegroundColor Yellow

$isServer = (Get-CimInstance -ClassName Win32_OperatingSystem).ProductType -ne 1

$clientFeatures = @(
    "IIS-WebServerRole",
    "IIS-WebServer",
    "IIS-CommonHttpFeatures",
    "IIS-StaticContent",
    "IIS-DefaultDocument",
    "IIS-DirectoryBrowsing",
    "IIS-HttpErrors",
    "IIS-HttpRedirect",
    "IIS-HealthAndDiagnostics",
    "IIS-HttpLogging",
    "IIS-RequestMonitor",
    "IIS-Security",
    "IIS-RequestFiltering",
    "IIS-Performance",
    "IIS-HttpCompressionStatic",
    "IIS-WebServerManagementTools",
    "IIS-ManagementConsole",
    "IIS-ApplicationDevelopment",
    "IIS-WebSockets"
)

$serverFeatures = @(
    "Web-Server",
    "Web-WebServer",
    "Web-Common-Http",
    "Web-Static-Content",
    "Web-Default-Doc",
    "Web-Dir-Browsing",
    "Web-Http-Errors",
    "Web-Http-Redirect",
    "Web-Health",
    "Web-Http-Logging",
    "Web-Request-Monitor",
    "Web-Security",
    "Web-Filtering",
    "Web-Performance",
    "Web-Stat-Compression",
    "Web-Mgmt-Tools",
    "Web-Mgmt-Console",
    "Web-App-Dev",
    "Web-WebSockets"
)

$featuresChanged = $false

if ($isServer) {
    Import-Module ServerManager -ErrorAction SilentlyContinue
    foreach ($feature in $serverFeatures) {
        $result = Install-WindowsFeature -Name $feature -ErrorAction SilentlyContinue
        if ($result.Success -and $result.Installed) {
            Write-Host "      Installed server feature: $feature" -ForegroundColor Green
            $featuresChanged = $true
        }
    }
    Write-Host "      IIS Server features verified (including WebSockets)." -ForegroundColor Gray
} else {
    try {
        foreach ($feature in $clientFeatures) {
            $featInfo = Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction SilentlyContinue 2>$null
            if (-not $featInfo -or $featInfo.State -ne 'Enabled') {
                Write-Host "      Enabling feature: $feature..." -ForegroundColor DarkYellow
                Enable-WindowsOptionalFeature -Online -FeatureName $feature -NoRestart -All -ErrorAction SilentlyContinue 2>$null | Out-Null
                $featuresChanged = $true
            }
        }
        Write-Host "      IIS Windows features verified (including WebSockets)." -ForegroundColor Gray
    } catch {
        Write-Host "      Notice: Could not query DISM features directly ($($_.Exception.Message)). Continuing..." -ForegroundColor DarkYellow
    }
}

# ------------------------------------------------------------------------------
# STEP 2: Check & Configure ASP.NET Core Hosting Bundle
# ------------------------------------------------------------------------------
Write-Host "[2/8] Checking ASP.NET Core Module V2 (AspNetCoreModuleV2)..." -ForegroundColor Yellow

$ancmPath = "${env:ProgramFiles}\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
$ancmInstalled = Test-Path $ancmPath

if ($ancmInstalled) {
    Write-Host "      AspNetCoreModuleV2 is installed." -ForegroundColor Gray
} else {
    Write-Host "      WARNING: ASP.NET Core Module V2 was NOT found on this system." -ForegroundColor DarkYellow
    Write-Host "      The ASP.NET Core Hosting Bundle (.NET 10) is required to host on IIS." -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "      Would you like to automatically download and install the Hosting Bundle now? (Y/N)"
    if ($response -match "^[Yy]$") {
        $downloadUrl = "https://aka.ms/dotnet/10.0/dotnet-hosting-win.exe"
        $installerPath = Join-Path $env:TEMP "dotnet-hosting-10.0-win.exe"
        Write-Host "      Downloading .NET 10 Hosting Bundle..." -ForegroundColor Gray
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
            Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath -UseBasicParsing
            Write-Host "      Installing hosting bundle silently..." -ForegroundColor Gray
            $installProc = Start-Process -FilePath $installerPath -ArgumentList "/install /quiet /norestart" -Wait -PassThru
            if ($installProc.ExitCode -eq 0 -or $installProc.ExitCode -eq 3010) {
                Write-Host "      Hosting Bundle installed successfully!" -ForegroundColor Green
                $featuresChanged = $true
            }
        } catch {
            Write-Host "      Auto-download failed: $_" -ForegroundColor Red
        }
    }
}

if ($featuresChanged) {
    Write-Host "      Restarting IIS services to refresh active modules..." -ForegroundColor Yellow
    & net stop was /y 2>$null | Out-Null
    & net start w3svc 2>$null | Out-Null
}

# ------------------------------------------------------------------------------
# STEP 3: Clean Output Directory (Handling DB Backup or Preservation)
# ------------------------------------------------------------------------------
Write-Host "[3/8] Preparing output directory..." -ForegroundColor Yellow
$ResolvedOutputDir = [System.IO.Path]::GetFullPath($OutputDir)
Write-Host "      Target Directory: $ResolvedOutputDir" -ForegroundColor Gray

# Place app_offline.htm to safely release file locks if the IIS site is currently running
$offlineFile = Join-Path $ResolvedOutputDir "app_offline.htm"
$dataDir = Join-Path $ResolvedOutputDir "Data"
$backupDir = Join-Path $dataDir "_backups"

if (Test-Path $ResolvedOutputDir) {
    try {
        Set-Content -Path $offlineFile -Value "<html><body style='font-family:sans-serif;text-align:center;padding-top:100px;'><h2>Site Update in Progress</h2><p>Please refresh in a few seconds.</p></body></html>" -Encoding UTF8 -Force
        Start-Sleep -Milliseconds 300
    } catch {}

    if ($OverwriteDb) {
        # OVERWRITE MODE: Clean up legacy DB files if present
        if (Test-Path $dataDir) {
            $existingDbFiles = Get-ChildItem -Path $dataDir -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -match '^\.(db|db-shm|db-wal)$' -and (Test-Path $_.FullName) }
            if ($existingDbFiles -and $existingDbFiles.Count -gt 0) {
                if (-not (Test-Path $backupDir)) {
                    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
                }
                $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
                $zipPath = Join-Path $backupDir "legacy_sqlite_backup_${timestamp}.zip"
                try {
                    $filesToZip = @($existingDbFiles | Select-Object -ExpandProperty FullName)
                    Compress-Archive -Path $filesToZip -DestinationPath $zipPath -Force -ErrorAction SilentlyContinue
                    if (Test-Path $zipPath) {
                        Write-Host "      [BACKUP CREATED] Saved previous database to:" -ForegroundColor Green
                        Write-Host "      -> $zipPath" -ForegroundColor Green
                    }
                } catch {}
                try {
                    $existingDbFiles | Remove-Item -Force -ErrorAction SilentlyContinue
                    Write-Host "      Cleared previous database files for clean overwrite." -ForegroundColor Gray
                } catch {}
            }
        }
    } else {
        # SAFE MODE: Preserve .db, .db-shm, .db-wal files
        if (Test-Path $dataDir) {
            Get-ChildItem -Path $dataDir | Where-Object {
                $_.FullName -ne $backupDir -and $_.Extension -notmatch '^\.(db|db-shm|db-wal)$'
            } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "      Preserved existing SQLite database in Data/ directory." -ForegroundColor Gray
        }
    }

    # Remove all root items except Data and app_offline.htm
    Get-ChildItem -Path $ResolvedOutputDir | Where-Object {
        $_.FullName -ne $dataDir -and $_.FullName -ne $offlineFile
    } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $ResolvedOutputDir -Force | Out-Null
}

# ------------------------------------------------------------------------------
# STEP 4: Publish Web Application (and EF Migration update in Overwrite Mode)
# ------------------------------------------------------------------------------
Write-Host "[4/8] Publishing GabsHybridApp.Web ($Configuration, $targetRid, Framework-Dependent)..." -ForegroundColor Yellow

# ------------------------------------------------------------------------------
# EF Core Migrations (Fail-Fast Verification & Interactive Recovery)
# ------------------------------------------------------------------------------
Write-Host "      [EF MIGRATIONS] Verifying and applying latest EF Core database migrations..." -ForegroundColor Gray

# Ensure production environment and connection string from appsettings.json
$env:ASPNETCORE_ENVIRONMENT = "Production"
$webDir = Split-Path $WebProjectPath
$prodSettingsFile = Join-Path $webDir "appsettings.json"
$prodConn = $null
if (Test-Path $prodSettingsFile) {
    try {
        $rawSettings = (Get-Content -Path $prodSettingsFile -Raw) -replace '(?m)^\s*//.*$', ''
        $prodConn = ($rawSettings | ConvertFrom-Json).ConnectionStrings.DefaultConnection
    } catch {}
}

# Auto-detect active database provider from Program.cs
$programCsPath = Join-Path $webDir "Program.cs"
$programCsContent = if (Test-Path $programCsPath) { Get-Content $programCsPath -Raw } else { "" }
$isSqliteActive = $programCsContent -match '(?m)^\s*builder\.Services\.AddDbContextFactory<HybridAppDbContext>\(option\s*=>\s*option\.UseSqlite'

$connArg = @()
if ($isSqliteActive) {
    Write-Host "      [PROVIDER DETECTED] SQLite (Target: Data/hybrid_webDb.db)" -ForegroundColor Yellow
    $connArg = @()
} else {
    $displayConn = if ($prodConn) { $prodConn -replace '(?i)(Username|User Id|User)\s*=\s*[^;]+', 'Username=...' -replace '(?i)Password\s*=\s*[^;]+', 'Password=...' } else { "None" }
    Write-Host "      [PROVIDER DETECTED] PostgreSQL (Target: $displayConn)" -ForegroundColor DarkCyan
    if (-not [string]::IsNullOrWhiteSpace($prodConn)) {
        $connArg = @("--connection", $prodConn)
    }
}

# If OverwriteDb was requested, revert all migrations to 0 before migrating fresh
if ($OverwriteDb) {
    Write-Host "      [DB OVERWRITE] OverwriteDb selected: Reverting database schema to 0 for clean slate (Zero Seed in Production)..." -ForegroundColor Magenta
    try {
        & dotnet ef database update 0 --project "$WebProjectPath" --startup-project "$WebProjectPath" @connArg
        Write-Host "      [DB OVERWRITE SUCCESS] Database reverted to clean slate. Applying fresh migrations..." -ForegroundColor Green
    } catch {
        Write-Host "      Notice: Could not revert migrations to 0: $_" -ForegroundColor DarkYellow
    }
}

$migrationSuccess = $false
while (-not $migrationSuccess) {
    & dotnet ef database update --project "$WebProjectPath" --startup-project "$WebProjectPath" @connArg
    if ($LASTEXITCODE -eq 0) {
        $migrationSuccess = $true
        Write-Host "      [EF MIGRATIONS SUCCESS] Database schema is up-to-date." -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "==========================================================" -ForegroundColor Red
        Write-Host " [ERROR] EF Core database migration failed!              " -ForegroundColor Red
        Write-Host "==========================================================" -ForegroundColor Red
        Write-Host "Options:" -ForegroundColor Yellow
        Write-Host " [R] Retry migration" -ForegroundColor Cyan
        Write-Host " [W] Wipe database & re-apply from clean slate" -ForegroundColor Magenta
        Write-Host " [A] Abort deployment (Default - keep existing IIS site)" -ForegroundColor Gray
        Write-Host ""

        $choice = $null
        if ($NoPrompt) {
            $choice = 'A'
        } else {
            Write-Host -NoNewline "Enter choice [r/w/A] (Timeout 15s -> Abort): "
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            while ($sw.Elapsed.TotalSeconds -lt 15) {
                if ([Console]::KeyAvailable) {
                    $k = [Console]::ReadKey($true)
                    if ($k.Key -eq [ConsoleKey]::R) { $choice = 'R'; Write-Host "R (Retry)" -ForegroundColor Cyan; break }
                    elseif ($k.Key -eq [ConsoleKey]::W) { $choice = 'W'; Write-Host "W (Wipe & Re-scaffold)" -ForegroundColor Magenta; break }
                    elseif ($k.Key -eq [ConsoleKey]::A -or $k.Key -eq [ConsoleKey]::Enter) { $choice = 'A'; Write-Host "A (Abort)" -ForegroundColor Red; break }
                }
                Start-Sleep -Milliseconds 100
            }
            if (-not $choice) {
                Write-Host "A (Timeout -> Abort)" -ForegroundColor Red
                $choice = 'A'
            }
        }

        if ($choice -eq 'R') {
            Write-Host "      Re-attempting database migration..." -ForegroundColor Yellow
            continue
        } elseif ($choice -eq 'W') {
            Write-Host "      [WIPE SCHEMA] Reverting all migrations to 0 and re-applying..." -ForegroundColor Yellow
            try {
                & dotnet ef database update 0 --project "$WebProjectPath" --startup-project "$WebProjectPath" @connArg
                Write-Host "      [WIPE SUCCESS] Cleaned database to clean state. Re-applying migrations..." -ForegroundColor Green
            } catch {
                Write-Host "      [WIPE NOTICE] Could not revert database to 0: $_" -ForegroundColor DarkYellow
            }
            continue
        } else {
            Write-Host ""
            Write-Host "ERROR: Deployment aborted due to database migration failure." -ForegroundColor Red
            if (Test-Path $offlineFile) { Remove-Item -Path $offlineFile -Force -ErrorAction SilentlyContinue }
            exit 1
        }
    }
}

Write-Host "      Command: dotnet publish `"$WebProjectPath`" -c $Configuration -r $targetRid -o `"$ResolvedOutputDir`" --no-self-contained" -ForegroundColor DarkGray

& dotnet publish "$WebProjectPath" -c $Configuration -r $targetRid -o "$ResolvedOutputDir" --no-self-contained

if ($LASTEXITCODE -ne 0) {
    if (Test-Path $offlineFile) { Remove-Item -Path $offlineFile -Force -ErrorAction SilentlyContinue }
    Write-Host ""
    Write-Host "ERROR: dotnet publish failed with exit code $LASTEXITCODE." -ForegroundColor Red
    exit $LASTEXITCODE
}

# ------------------------------------------------------------------------------
# STEP 5: Prepare Runtime Folders, Permissions & Database Deployment
# ------------------------------------------------------------------------------
Write-Host "[5/8] Initializing runtime folders & setting IIS NTFS permissions..." -ForegroundColor Yellow
$logsDir = Join-Path $ResolvedOutputDir "logs"
$uploadsDir = Join-Path $ResolvedOutputDir "uploads"

# 1. Create essential runtime directories
foreach ($dir in @($logsDir, $dataDir, $uploadsDir, $backupDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}
Write-Host "      Runtime directories verified: Data, logs, uploads" -ForegroundColor Gray

# 2. Deploy SQLite Database (Enabled dynamically when SQLite is active)
if ($isSqliteActive) {
    $sourceDevDb = Join-Path (Split-Path -Parent $WebProjectPath) "Data\hybrid_webDb.db"
    $targetPublishedDb = Join-Path $dataDir "hybrid_webDb.db"
    if ($OverwriteDb) {
        if (Test-Path $sourceDevDb) {
            Copy-Item -Path $sourceDevDb -Destination $targetPublishedDb -Force
            Write-Host "      [DB OVERWRITE SUCCESS] Copied fresh development SQLite database into _publish\Data\." -ForegroundColor Green
        }
    } else {
        if (-not (Test-Path $targetPublishedDb) -and (Test-Path $sourceDevDb)) {
            Copy-Item -Path $sourceDevDb -Destination $targetPublishedDb -Force
            Write-Host "      [INITIAL DEPLOY] Copied SQLite database into _publish\Data\." -ForegroundColor Green
        } else {
            Write-Host "      [PRESERVE DB] Preserved existing SQLite database in _publish\Data\." -ForegroundColor Gray
        }
    }
} else {
    Write-Host "      [POSTGRESQL ACTIVE] SQLite file copy skipped (database is hosted on PostgreSQL server)." -ForegroundColor Gray
}

# 3. Synchronize App Releases for Web Download
$rootReleasesDir = Join-Path $RepoRoot "_releases"
$sharedReleasesDir = Join-Path $RepoRoot "GabsHybridApp\GabsHybridApp.Shared\wwwroot\releases"
$pubWwwrootReleases = Join-Path $ResolvedOutputDir "wwwroot\releases"
$pubContentReleases = Join-Path $ResolvedOutputDir "wwwroot\_content\GabsHybridApp.Shared\releases"

# Ensure target release directories exist
foreach ($targetRel in @($pubWwwrootReleases, $pubContentReleases, $sharedReleasesDir)) {
    if (-not (Test-Path $targetRel)) {
        New-Item -ItemType Directory -Path $targetRel -Force | Out-Null
    }
}

# Sync from _releases to shared releases if present
if (Test-Path $rootReleasesDir) {
    Get-ChildItem -Path $rootReleasesDir -File | Where-Object { $_.Extension -match '^\.(apk|zip|exe|json)$' } | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $sharedReleasesDir -Force -ErrorAction SilentlyContinue
    }
}

# Sync from sharedReleasesDir to published output directories
$releaseFiles = Get-ChildItem -Path $sharedReleasesDir -File | Where-Object { $_.Extension -match '^\.(apk|zip|exe|json)$' }
if ($releaseFiles -and $releaseFiles.Count -gt 0) {
    foreach ($file in $releaseFiles) {
        Copy-Item -Path $file.FullName -Destination $pubWwwrootReleases -Force -ErrorAction SilentlyContinue
        Copy-Item -Path $file.FullName -Destination $pubContentReleases -Force -ErrorAction SilentlyContinue
    }
    $fileList = ($releaseFiles | Select-Object -ExpandProperty Name) -join ', '
    Write-Host "      [OFFLINE RELEASES] Synchronized ($($releaseFiles.Count)) package(s) into published wwwroot: $fileList" -ForegroundColor Green
} else {
    Write-Host "      [OFFLINE RELEASES] Notice: No pre-built client packages (.apk / .zip) found. Skipping release sync." -ForegroundColor DarkYellow
}

# 4. Comprehensive IIS Identities
$iisIdentities = @(
    "IIS_IUSRS",
    "IUSR",
    "IIS AppPool\$AppPoolName",
    "IIS AppPool\DefaultAppPool",
    "NT AUTHORITY\NetworkService"
)

# 4. Apply Read & Execute (RX) to App Root
Write-Host "      Setting Read/Execute permissions on Application Root..." -ForegroundColor Gray
foreach ($identity in $iisIdentities) {
    try {
        & icacls "$ResolvedOutputDir" /grant "${identity}:(OI)(CI)RX" /c /q 2>$null | Out-Null
    } catch {}
}

# 5. Apply Full Modify/Write (M) to Writable Runtime Folders
Write-Host "      Setting Modify/Write permissions on Data, logs, and uploads..." -ForegroundColor Gray
$writableFolders = @($dataDir, $logsDir, $uploadsDir)

foreach ($wFolder in $writableFolders) {
    foreach ($identity in $iisIdentities) {
        try {
            & icacls "$wFolder" /grant "${identity}:(OI)(CI)M" /t /c /q 2>$null | Out-Null
        } catch {}
    }
}

Write-Host "      [SUCCESS] NTFS Permissions successfully configured for:" -ForegroundColor Green
Write-Host "        - IIS_IUSRS (All IIS Worker Processes)" -ForegroundColor Green
Write-Host "        - IUSR (Anonymous Web Requests)" -ForegroundColor Green
Write-Host "        - IIS AppPool\$AppPoolName (Target Application Pool)" -ForegroundColor Green
Write-Host "        - IIS AppPool\DefaultAppPool (Default Application Pool)" -ForegroundColor Green
Write-Host "        - NetworkService" -ForegroundColor Green

# ------------------------------------------------------------------------------
# STEP 6: Automated Self-Signed SSL Certificate Generation (for HTTPS)
# ------------------------------------------------------------------------------
Write-Host "[6/8] Checking & Generating Self-Signed SSL Certificate for HTTPS..." -ForegroundColor Yellow

$certFriendlyName = "$siteName IIS Self-Signed SSL"
$certThumbprint = ""

try {
    $existingCert = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { 
        $_.FriendlyName -eq $certFriendlyName -or $_.Subject -eq "CN=$siteName" 
    } | Select-Object -First 1

    if ($existingCert -and ($existingCert.NotAfter -gt (Get-Date).AddDays(30))) {
        $certThumbprint = $existingCert.Thumbprint
        Write-Host "      Found existing valid SSL certificate: '$certFriendlyName'" -ForegroundColor Gray
        Write-Host "      Thumbprint: $certThumbprint (Expires: $($existingCert.NotAfter.ToShortDateString()))" -ForegroundColor Gray
    } else {
        Write-Host "      Creating new 5-year Self-Signed SSL certificate for HTTPS..." -ForegroundColor Gray
        
        $sanList = [System.Collections.Generic.List[string]]::new()
        $sanList.Add("localhost")
        $sanList.Add($env:COMPUTERNAME)
        $sanList.Add("127.0.0.1")
        
        try {
            $localIps = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { 
                $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" 
            }).IPAddress
            foreach ($ip in $localIps) {
                if (-not $sanList.Contains($ip)) { $sanList.Add($ip) }
            }
        } catch {}

        $dnsNames = $sanList | ForEach-Object { "$_" }

        $newCert = New-SelfSignedCertificate `
            -DnsName $dnsNames `
            -CertStoreLocation "Cert:\LocalMachine\My" `
            -NotAfter (Get-Date).AddYears(5) `
            -FriendlyName $certFriendlyName `
            -KeyExportPolicy Exportable `
            -KeySpec Signature `
            -KeyUsage DigitalSignature,KeyEncipherment `
            -Type SSLServerAuthentication `
            -ErrorAction Stop

        $certThumbprint = $newCert.Thumbprint

        try {
            $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
            $rootStore.Open("ReadWrite")
            $rootStore.Add($newCert)
            $rootStore.Close()
            Write-Host "      [SUCCESS] Certificate installed in Personal Store and Trusted Root Authority." -ForegroundColor Green
        } catch {
            Write-Host "      Notice: Installed in Personal store. (Root store notice: $_)" -ForegroundColor DarkYellow
        }

        Write-Host "      SSL Certificate Thumbprint: $certThumbprint" -ForegroundColor Green
        Write-Host "      Configured SAN Names: $($sanList -join ', ')" -ForegroundColor Gray
    }
} catch {
    Write-Host "      Notice: Could not automatically generate SSL certificate: $_" -ForegroundColor DarkYellow
}

# ------------------------------------------------------------------------------
# STEP 7: Automatic IIS Application Pool & Website Provisioning
# ------------------------------------------------------------------------------
Write-Host "[7/8] Provisioning Application Pool & Site in IIS..." -ForegroundColor Yellow

$appCmd = "$env:SystemRoot\system32\inetsrv\appcmd.exe"

if (Test-Path $appCmd) {
    try {
        # 1. Ensure DefaultAppPool and GabsHybridAppPool are set to No Managed Code
        & $appCmd set apppool /apppool.name:"DefaultAppPool" /managedRuntimeVersion:"" /managedPipelineMode:"Integrated" 2>$null | Out-Null
        
        $existingPool = & $appCmd list apppool /name:"$AppPoolName" 2>$null
        if (-not $existingPool) {
            Write-Host "      Creating IIS AppPool '$AppPoolName' (No Managed Code)..." -ForegroundColor Gray
            & $appCmd add apppool /name:"$AppPoolName" /managedRuntimeVersion:"" /managedPipelineMode:"Integrated" 2>$null | Out-Null
        } else {
            & $appCmd set apppool /apppool.name:"$AppPoolName" /managedRuntimeVersion:"" /managedPipelineMode:"Integrated" 2>$null | Out-Null
            Write-Host "      Verified IIS AppPool '$AppPoolName' set to 'No Managed Code'." -ForegroundColor Gray
        }

        # 2. Ensure Website exists and points to _publish folder on Port 8080
        $httpPort = "8080"
        $existingSite = & $appCmd list site /name:"$siteName" 2>$null
        if (-not $existingSite) {
            Write-Host "      Creating IIS Website '$siteName' on port $httpPort (HTTP)..." -ForegroundColor Gray
            & $appCmd add site /name:"$siteName" /bindings:"http/*:${httpPort}:" /physicalPath:"$ResolvedOutputDir" 2>$null | Out-Null
            & $appCmd set site /site.name:"$siteName" /[path='/'].applicationPool:"$AppPoolName" 2>$null | Out-Null
        } else {
            & $appCmd set site /site.name:"$siteName" /[path='/'].physicalPath:"$ResolvedOutputDir" 2>$null | Out-Null
            & $appCmd set site /site.name:"$siteName" /[path='/'].applicationPool:"$AppPoolName" 2>$null | Out-Null
            
            # Ensure port 8080 binding exists on the site
            try {
                Import-Module WebAdministration -ErrorAction SilentlyContinue
                if (Test-Path "IIS:\Sites\$siteName") {
                    $httpBinding = Get-WebBinding -Name "$siteName" -Protocol "http" -ErrorAction SilentlyContinue
                    if (-not ($httpBinding | Where-Object { $_.bindingInformation -match ":$($httpPort):" })) {
                        New-WebBinding -Name "$siteName" -Protocol "http" -Port $httpPort -IPAddress "*" -ErrorAction SilentlyContinue | Out-Null
                    }
                }
            } catch {}
            Write-Host "      Updated IIS Website '$siteName' (Port $httpPort, AppPool '$AppPoolName')." -ForegroundColor Gray
        }

        # 3. Configure HTTPS binding if certificate is available (with Conflict Detection)
        $httpsPort = 443
        if ($certThumbprint) {
            try {
                Import-Module WebAdministration -ErrorAction SilentlyContinue
                if (Test-Path "IIS:\Sites\$siteName") {
                    # Check if port 443 is already occupied by a DIFFERENT website in IIS
                    $conflictingHttps = Get-WebBinding -Protocol "https" -ErrorAction SilentlyContinue | Where-Object {
                        $_.ItemXPath -notmatch "name='$siteName'" -and $_.bindingInformation -match ":443:"
                    }

                    if ($conflictingHttps) {
                        $httpsPort = 8443
                        Write-Host "      Notice: Port 443 is used by another IIS site. Using fallback HTTPS Port $httpsPort." -ForegroundColor DarkYellow
                    }

                    $httpsBinding = Get-WebBinding -Name "$siteName" -Protocol "https" -ErrorAction SilentlyContinue | Where-Object {
                        $_.bindingInformation -match ":$($httpsPort):"
                    }
                    if (-not $httpsBinding) {
                        New-WebBinding -Name "$siteName" -Protocol "https" -Port $httpsPort -IPAddress "*" -ErrorAction SilentlyContinue | Out-Null
                    }
                    $binding = Get-WebBinding -Name "$siteName" -Protocol "https" -ErrorAction SilentlyContinue | Where-Object {
                        $_.bindingInformation -match ":$($httpsPort):"
                    }
                    if ($binding) {
                        $binding.AddSslCertificate($certThumbprint, "My")
                        Write-Host "      Bound Self-Signed SSL Certificate to HTTPS (Port $httpsPort)." -ForegroundColor Green
                    }
                }
            } catch {}
        }

        # 4. Start AppPool and Site
        & $appCmd start apppool /apppool.name:"$AppPoolName" 2>$null | Out-Null
        & $appCmd start site /site.name:"$siteName" 2>$null | Out-Null
        Write-Host "      [SUCCESS] IIS AppPool '$AppPoolName' and Site '$siteName' are ACTIVE in IIS!" -ForegroundColor Green
    } catch {
        Write-Host "      Notice: Could not automatically provision IIS Site: $_" -ForegroundColor DarkYellow
    }
}

# ------------------------------------------------------------------------------
# STEP 8: Web Configuration & Documentation
# ------------------------------------------------------------------------------
Write-Host "[8/8] Generating configuration and deployment documentation..." -ForegroundColor Yellow

# Verify and enable stdout diagnostic logging + static content MIME mappings in web.config
$webConfigFile = Join-Path $ResolvedOutputDir "web.config"
if (Test-Path $webConfigFile) {
    try {
        $xmlContent = Get-Content -Path $webConfigFile -Raw
        if ($xmlContent -match 'stdoutLogEnabled="false"') {
            $xmlContent = $xmlContent -replace 'stdoutLogEnabled="false"', 'stdoutLogEnabled="true"'
        }
        if ($xmlContent -notmatch '\.apk') {
            $mimeConfig = @"
    <staticContent>
      <remove fileExtension=".apk" />
      <mimeMap fileExtension=".apk" mimeType="application/vnd.android.package-archive" />
      <remove fileExtension=".zip" />
      <mimeMap fileExtension=".zip" mimeType="application/zip" />
      <remove fileExtension=".exe" />
      <mimeMap fileExtension=".exe" mimeType="application/octet-stream" />
    </staticContent>
  </system.webServer>
"@
            $xmlContent = $xmlContent -replace '</system\.webServer>', $mimeConfig
        }
        Set-Content -Path $webConfigFile -Value $xmlContent -Encoding UTF8
        Write-Host "      Configured web.config (stdout logging + APK/ZIP MIME mappings)." -ForegroundColor Gray
    } catch {}
    Write-Host "      Verified 'web.config' generated for AspNetCoreModuleV2." -ForegroundColor Gray
}

# Database seeding remains disabled per project configuration

# Generate Deployment Guide
$readmePath = Join-Path $ResolvedOutputDir "README_IIS_DEPLOYMENT.md"
$readmeContent = @"
# IIS Deployment Guide for GabsHybridApp.Web

This directory contains the published files for **GabsHybridApp.Web** built for hosting on **Internet Information Services (IIS)** via the **ASP.NET Core Hosting Bundle**.

---

## 1. IIS Configuration Summary
- **AppPool:** `$AppPoolName` (.NET CLR: No Managed Code, Integrated)
- **Site Name:** `$siteName`
- **HTTP URL:** `http://localhost:$httpPort`
- **HTTPS URL:** `https://localhost` (Port $httpsPort with `$certFriendlyName`)
- **Database Backups:** `Data\_backups\hybrid_webDb_backup_*.zip`

---

## 2. Diagnostics & Troubleshooting
- **Check Stdout Logs:**
  Edit `web.config` inside this folder and set `stdoutLogEnabled="true"`. Restart the App Pool and inspect `logs\stdout_*.log`.
- **HTTP 500.19 / 500.30 Error:**
  - Verify that the ASP.NET Core Hosting Bundle is installed and IIS was restarted.
  - Verify Application Pool has `.NET CLR Version: No Managed Code`.
- **Blazor Reconnection Loop / SignalR Errors:**
  - Verify that the **WebSocket Protocol** IIS feature is installed (handled automatically by `publish-iis.bat`).
"@
Set-Content -Path $readmePath -Value $readmeContent -Encoding UTF8
Write-Host "      Generated 'README_IIS_DEPLOYMENT.md' instructions." -ForegroundColor Gray

# Bring IIS site back online if app_offline.htm was placed
if (Test-Path $offlineFile) {
    Remove-Item -Path $offlineFile -Force -ErrorAction SilentlyContinue
    Write-Host "      Released 'app_offline.htm' - Site is now ACTIVE." -ForegroundColor Green
}

# ------------------------------------------------------------------------------
# STEP 8: Completion Summary
# ------------------------------------------------------------------------------
$httpsUrl = if ($httpsPort -eq 443) { "https://localhost" } else { "https://localhost:$httpsPort" }

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   IIS WEB PUBLISH COMPLETED SUCCESSFULLY!                " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Published to: $ResolvedOutputDir" -ForegroundColor Cyan
Write-Host "HTTP URL:     http://localhost:$httpPort" -ForegroundColor Cyan
Write-Host "HTTPS URL:    $httpsUrl" -ForegroundColor Cyan
if ($OverwriteDb) {
    Write-Host "Backup Dir:   $backupDir" -ForegroundColor Magenta
}
Write-Host ""
Write-Host "IIS Quick Checklist:" -ForegroundColor Yellow
Write-Host " 1. IIS AppPool: '$AppPoolName' (.NET CLR: No Managed Code, $targetRid)" -ForegroundColor White
Write-Host " 2. IIS Website: '$siteName' on Port $httpPort (HTTP) and Port $httpsPort (HTTPS)" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
