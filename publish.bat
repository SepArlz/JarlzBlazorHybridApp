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

powershell -NoProfile -ExecutionPolicy Bypass -Command "$script = [System.IO.File]::ReadAllText('%~f0'); & ([ScriptBlock]::Create($script)) %*"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Process failed with exit code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Process completed successfully.
pause
exit /b
#>

# ==============================================================================
# PowerShell Execution Logic (Master Orchestrator -> App Publisher + IIS Host)
# ==============================================================================
param(
    [switch]$OverwriteDb = $false,
    [switch]$BuildApps = $false,
    [switch]$SkipApps = $false,
    [string]$SiteName = "",
    [ValidateSet('x64', 'arm64', 'auto')][string]$Arch = 'auto',
    [switch]$Arm64 = $false,
    [switch]$x64 = $false,
    [switch]$NoPrompt = $false
)

$ErrorActionPreference = "Stop"

# Configure Execution Policy
try {
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force -ErrorAction SilentlyContinue
    Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned -Force -ErrorAction SilentlyContinue
    Set-ExecutionPolicy -Scope LocalMachine -ExecutionPolicy RemoteSigned -Force -ErrorAction SilentlyContinue
} catch {}

Clear-Host
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   GabsHybridApp - Unified Master Release Orchestrator   " -ForegroundColor Cyan
Write-Host "   Running as Administrator: YES                         " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$RepoRoot = $PSScriptRoot
if (-not $RepoRoot) {
    $RepoRoot = (Get-Location).Path
}

# ------------------------------------------------------------------------------
# Resolve Default IIS Site Name from .SLN File in Script Directory
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

# Determine client app build decision (Default: NO build)
$shouldBuildApps = $false
if ($PSBoundParameters.ContainsKey('BuildApps') -and $BuildApps) {
    $shouldBuildApps = $true
} elseif ($PSBoundParameters.ContainsKey('SkipApps') -and $SkipApps) {
    $shouldBuildApps = $false
}

# ------------------------------------------------------------------------------
# Interactive Prompts (Shared wait window: 5s timeout; Enter = Default NO)
# ------------------------------------------------------------------------------
$interacted = $false

if (-not $NoPrompt) {
    # PROMPT 1: Database Overwrite Option (Default: No)
    if (-not $PSBoundParameters.ContainsKey('OverwriteDb')) {
        try {
            $timeoutSeconds = 5
            $startTime = [System.Diagnostics.Stopwatch]::StartNew()
            Write-Host -NoNewline "Overwrite Database (Fresh Seed)? [y/N]: "
            
            while ($startTime.Elapsed.TotalSeconds -lt $timeoutSeconds) {
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)
                    $interacted = $true
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
            if (-not $interacted) {
                Write-Host "N (Default)" -ForegroundColor Gray
                $OverwriteDb = $false
            }
        } catch {
            Write-Host "N (Default)" -ForegroundColor Gray
            $OverwriteDb = $false
        }
    } else {
        $interacted = $true
    }

    # PROMPT 2: Client App Packaging Option (Default: No)
    if (-not $PSBoundParameters.ContainsKey('BuildApps') -and -not $PSBoundParameters.ContainsKey('SkipApps')) {
        if (-not $interacted) {
            # Shared timer expired on first prompt: user is unattended -> default to NO immediately
            Write-Host "Package Client Apps (Android APK + Windows SFX)? [y/N]: N (Default)" -ForegroundColor Gray
            $shouldBuildApps = $false
        } else {
            # User interacted with first prompt: wait for explicit user response to continue
            Write-Host -NoNewline "Package Client Apps (Android APK + Windows SFX)? [y/N]: "
            try {
                while ($true) {
                    if ([Console]::KeyAvailable) {
                        $key = [Console]::ReadKey($true)
                        if ($key.Key -eq [ConsoleKey]::Y) {
                            $shouldBuildApps = $true
                            Write-Host "Y" -ForegroundColor Green
                        } elseif ($key.Key -eq [ConsoleKey]::Enter) {
                            $shouldBuildApps = $false
                            Write-Host "N (Default)" -ForegroundColor Gray
                        } else {
                            $shouldBuildApps = $false
                            Write-Host "N" -ForegroundColor Gray
                        }
                        break
                    }
                    Start-Sleep -Milliseconds 100
                }
            } catch {
                $response = Read-Host
                if ($response -match '^[Yy]') {
                    $shouldBuildApps = $true
                } else {
                    $shouldBuildApps = $false
                }
            }
        }
    }

    # PROMPT 3: IIS Site Name Option (Default: Solution file name)
    if (-not $PSBoundParameters.ContainsKey('SiteName')) {
        if (-not $interacted) {
            # Shared timer expired on first prompt: user is unattended -> immediately default
            Write-Host "IIS Site Name [$defaultSiteName]: $defaultSiteName (Default)" -ForegroundColor Gray
            $resolvedSiteName = $defaultSiteName
        } else {
            # User interacted with previous prompt(s): wait for user input (Enter picks default)
            Write-Host -NoNewline "IIS Site Name [$defaultSiteName]: "
            $enteredSite = (Read-Host).Trim()
            if (-not [string]::IsNullOrWhiteSpace($enteredSite)) {
                $resolvedSiteName = $enteredSite
            } else {
                $resolvedSiteName = $defaultSiteName
            }
        }
    }
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   Master Execution Plan:                                " -ForegroundColor Cyan
if ($OverwriteDb) {
    Write-Host "   - Database:     OVERWRITE & FRESH SEED (Auto-Backup)" -ForegroundColor Magenta
} else {
    Write-Host "   - Database:     PRESERVE PRODUCTION DB (Default)" -ForegroundColor Green
}
if ($shouldBuildApps) {
    Write-Host "   - Client Apps:  FULL REBUILD (Android APK + Windows SFX)" -ForegroundColor Green
} else {
    Write-Host "   - Client Apps:  SKIP BUILD (Reuse Existing Releases)" -ForegroundColor DarkYellow
}
Write-Host "   - IIS Site:     $resolvedSiteName (AppPool: ${resolvedSiteName}Pool)" -ForegroundColor Green
Write-Host "   - Target Arch:  win-$resolvedArch (Detected Host: $(if ($detectedHostArch) { $detectedHostArch } else { 'Unknown' }))" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# ------------------------------------------------------------------------------
# PHASE 1: Package Client Apps (Optional - Android APK + Windows SFX)
# ------------------------------------------------------------------------------
$appPublishScript = Join-Path $RepoRoot "publish-app.bat"

if ($shouldBuildApps) {
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host " [PHASE 1/2] Packaging Client Applications (Android + Win) " -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan
    
    if (-not (Test-Path $appPublishScript)) {
        Write-Host "ERROR: publish-app.bat not found at $appPublishScript" -ForegroundColor Red
        exit 1
    }

    $scriptContent = [System.IO.File]::ReadAllText($appPublishScript)
    $marker = '# =============================================================================='
    $idx = $scriptContent.IndexOf($marker)
    try {
        if ($idx -ge 0) {
            $psCode = $scriptContent.Substring($idx)
            & ([ScriptBlock]::Create($psCode)) -NoPause
        } else {
            & "$appPublishScript" -NoPause
        }
    } catch {
        Write-Host "Notice: Client app packaging completed with notice: $_" -ForegroundColor DarkYellow
    }

    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
        Write-Host "Notice: publish-app.bat completed with status $LASTEXITCODE. Proceeding with Web deployment..." -ForegroundColor DarkYellow
    }
} else {
    Write-Host "[PHASE 1/2] Skipping Client App build. Existing release packages will be synced." -ForegroundColor Gray
}

Write-Host ""

# ------------------------------------------------------------------------------
# PHASE 2: Publish Web Application & Deploy to IIS Server
# ------------------------------------------------------------------------------
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [PHASE 2/2] Publishing Web Application & Hosting on IIS   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$iisPublishScript = Join-Path $RepoRoot "publish-iis.bat"
if (-not (Test-Path $iisPublishScript)) {
    Write-Host "ERROR: publish-iis.bat not found at $iisPublishScript" -ForegroundColor Red
    exit 1
}

$iisScriptContent = [System.IO.File]::ReadAllText($iisPublishScript)
$iisMarker = '# =============================================================================='
$iisIdx = $iisScriptContent.IndexOf($iisMarker)

if ($iisIdx -ge 0) {
    $iisPsCode = $iisScriptContent.Substring($iisIdx)
    & ([ScriptBlock]::Create($iisPsCode)) -OverwriteDb:$OverwriteDb -SiteName $resolvedSiteName -Arch $resolvedArch -NoPrompt -NoPause
} else {
    & "$iisPublishScript" -OverwriteDb:$OverwriteDb -SiteName $resolvedSiteName -Arch $resolvedArch -NoPrompt -NoPause
}

if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
    Write-Host "ERROR: publish-iis.bat failed with exit code $LASTEXITCODE." -ForegroundColor Red
    exit $LASTEXITCODE
}
