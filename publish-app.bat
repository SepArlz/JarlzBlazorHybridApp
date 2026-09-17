<# :
@echo off
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$script = [System.IO.File]::ReadAllText('%~f0'); & ([ScriptBlock]::Create($script)) %*"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build and Release failed with exit code %ERRORLEVEL%.
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
# PowerShell Execution Logic (Unified All-in-One Client Releases: Windows + Android)
# ==============================================================================
param(
    [switch]$NoPause = $false
)

$ErrorActionPreference = "Stop"

try {
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force -ErrorAction SilentlyContinue
    Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned -Force -ErrorAction SilentlyContinue
} catch {}

if (-not $NoPause) {
    Clear-Host
}
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   GabsHybridApp - Unified Client App Publisher           " -ForegroundColor Cyan
Write-Host "   Target: Windows SFX (ZIP) + Android ARM64 APK (arm64-v8a) " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

$Configuration = "Release"
$RepoRoot = $PSScriptRoot
if (-not $RepoRoot) {
    $RepoRoot = (Get-Location).Path
}

$MauiProjectPath = Join-Path $RepoRoot "GabsHybridApp\GabsHybridApp.Maui\GabsHybridApp.Maui.csproj"
$ReleasesDir = Join-Path $RepoRoot "_releases"
$WwwrootReleasesDir = Join-Path $RepoRoot "GabsHybridApp\GabsHybridApp.Shared\wwwroot\releases"

$StagingWinDir = Join-Path $ReleasesDir "_staging_win"
$LauncherWorkDir = Join-Path $ReleasesDir "_launcher_win"
$StagingAndroidDir = Join-Path $ReleasesDir "_staging_android"

# ------------------------------------------------------------------------------
# Resilience Helper: Deploy Existing Pre-Built Releases
# ------------------------------------------------------------------------------
function Deploy-ExistingReleases {
    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor DarkYellow
    Write-Host " [RECOVERY] Checking for Existing Pre-Built Client Packages" -ForegroundColor DarkYellow
    Write-Host "==========================================================" -ForegroundColor DarkYellow

    # Ensure target destination directories exist
    foreach ($targetDir in @($ReleasesDir, $WwwrootReleasesDir)) {
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
    }

    $deployedPackages = @()
    $searchLocations = @($ReleasesDir, $WwwrootReleasesDir)
    
    $discoveredFiles = @()
    foreach ($loc in $searchLocations) {
        if (Test-Path $loc) {
            $files = Get-ChildItem -Path $loc -File | Where-Object { $_.Extension -match '^\.(apk|zip|exe|json)$' }
            if ($files) { $discoveredFiles += $files }
        }
    }

    # Group by filename
    $uniqueFiles = $discoveredFiles | Sort-Object -Property Name -Unique

    if ($uniqueFiles -and $uniqueFiles.Count -gt 0) {
        foreach ($file in $uniqueFiles) {
            $destRoot = Join-Path $ReleasesDir $file.Name
            $destWww = Join-Path $WwwrootReleasesDir $file.Name
            
            if ($file.FullName -ne $destRoot) {
                Copy-Item -Path $file.FullName -Destination $destRoot -Force -ErrorAction SilentlyContinue
            }
            if ($file.FullName -ne $destWww) {
                Copy-Item -Path $file.FullName -Destination $destWww -Force -ErrorAction SilentlyContinue
            }
            $sizeMb = "{0:N2} MB" -f ($file.Length / 1MB)
            $deployedPackages += "$($file.Name) ($sizeMb)"
        }

        Write-Host "      [FALLBACK SUCCESS] Picked and deployed existing release package(s):" -ForegroundColor Green
        foreach ($pkg in $deployedPackages) {
            Write-Host "        -> $pkg" -ForegroundColor Green
        }
        Write-Host "      Web application will serve these pre-built releases." -ForegroundColor Green
    } else {
        Write-Host "      [NOTICE] No existing pre-built client packages (.apk / .zip) were found." -ForegroundColor DarkYellow
        Write-Host "               Web application will deploy without client download assets." -ForegroundColor DarkYellow
    }
    Write-Host "==========================================================" -ForegroundColor DarkYellow
    Write-Host ""
}

# ------------------------------------------------------------------------------
# STEP 1: Verify Environment, Workloads & Project Files
# ------------------------------------------------------------------------------
Write-Host "[1/7] Verifying environment & project files..." -ForegroundColor Yellow

if (-not (Test-Path $MauiProjectPath)) {
    Write-Host "ERROR: Maui project file not found at $MauiProjectPath" -ForegroundColor Red
    Deploy-ExistingReleases
    return
}

$dotnetCmd = Get-Command "dotnet" -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    Write-Host "ERROR: .NET SDK ('dotnet') not found in PATH." -ForegroundColor Red
    Deploy-ExistingReleases
    return
}

# ------------------------------------------------------------------------------
# Resolve Version automatically from Git commit height (No Git tags required)
# ------------------------------------------------------------------------------
$baseVersion = "1.0"
$versionCode = 1

try {
    $commitCount = & git rev-list --count HEAD 2>$null
    if ($commitCount) {
        $versionCode = [int]$commitCount.Trim()
    }
} catch {}

$appVersion = "$baseVersion.$versionCode"

Write-Host "      Project: GabsHybridApp.Maui" -ForegroundColor Gray
Write-Host "      Detected Version: v$appVersion (Build Code: $versionCode)" -ForegroundColor Green
Write-Host "      Root Releases Folder: $ReleasesDir" -ForegroundColor Gray
Write-Host "      Web Download Folder: $WwwrootReleasesDir" -ForegroundColor Gray

# Check for required .NET MAUI workloads
$workloadList = ""
try {
    $workloadList = (& dotnet workload list 2>$null | Out-String)
} catch {}

$hasAndroidWorkload = ($workloadList -match '(?m)^\s*android\b') -or ($workloadList -match 'maui-android') -or ($workloadList -match 'maui\s') -or (Test-Path "${env:ProgramFiles}\dotnet\packs\Microsoft.Android.Sdk.Windows")
$hasWindowsWorkload = ($workloadList -match 'maui-windows') -or ($workloadList -match '(?m)^\s*windows\b') -or ($workloadList -match 'maui\s') -or (Test-Path "${env:ProgramFiles}\dotnet\packs\Microsoft.WindowsAppSDK")

if (-not $hasAndroidWorkload -or -not $hasWindowsWorkload) {
    Write-Host ""
    Write-Host "      [WARNING] Required .NET MAUI workloads are not installed on this system." -ForegroundColor Yellow
    if (-not $hasAndroidWorkload) { Write-Host "        - Missing: maui-android" -ForegroundColor DarkYellow }
    if (-not $hasWindowsWorkload) { Write-Host "        - Missing: maui-windows" -ForegroundColor DarkYellow }
    Write-Host "      To install workloads in an elevated prompt, run:" -ForegroundColor Cyan
    Write-Host "        dotnet workload install maui-android maui-windows" -ForegroundColor Cyan
    Write-Host ""
    Deploy-ExistingReleases
    return
}

# ------------------------------------------------------------------------------
# STEP 2: Clean & Prepare Destination Directories
# ------------------------------------------------------------------------------
Write-Host "[2/7] Preparing release and staging directories..." -ForegroundColor Yellow

foreach ($dir in @($ReleasesDir, $WwwrootReleasesDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

foreach ($dir in @($StagingWinDir, $LauncherWorkDir, $StagingAndroidDir)) {
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

try {
    # ------------------------------------------------------------------------------
    # STEP 3: Restore Dependencies
    # ------------------------------------------------------------------------------
    Write-Host "[3/7] Restoring project dependencies..." -ForegroundColor Yellow
    & dotnet restore "$MauiProjectPath" -v m
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet restore failed with exit code $LASTEXITCODE."
    }

    # ------------------------------------------------------------------------------
    # STEP 4: Build & Package Android ARM64 Release APK
    # ------------------------------------------------------------------------------
    Write-Host "[4/7] Compiling & Packaging Android ARM64 Release APK..." -ForegroundColor Yellow
    Write-Host "      Target ABI: arm64-v8a (Lightweight, optimized for modern phones & tablets)" -ForegroundColor Gray
    Write-Host "      Signing: mdrrmo-release.keystore (Permanent Release Key)" -ForegroundColor Gray

    & dotnet publish "$MauiProjectPath" `
        -f net10.0-android `
        -c $Configuration `
        -r android-arm64 `
        --no-restore `
        -p:ApplicationDisplayVersion="$appVersion" `
        -p:ApplicationVersion="$versionCode" `
        -p:AndroidPackageFormat=apk `
        -p:AndroidKeyStore=true `
        -p:AndroidSigningKeyStore="mdrrmo-release.keystore" `
        -p:AndroidSigningStorePass="mdrrmo12345" `
        -p:AndroidSigningKeyAlias="mdrrmo" `
        -p:AndroidSigningKeyPass="mdrrmo12345" `
        -o "$StagingAndroidDir" `
        -v m

    if ($LASTEXITCODE -ne 0) {
        throw "Android publish failed with exit code $LASTEXITCODE."
    }

    $apkCandidates = Get-ChildItem -Path $StagingAndroidDir -Filter "*.apk" | Sort-Object { if ($_.Name -like "*-Signed.apk") { 0 } else { 1 } }
    if (-not $apkCandidates -or $apkCandidates.Count -eq 0) {
        throw "No .apk generated in $StagingAndroidDir."
    }

    $sourceApk = $apkCandidates[0].FullName
    $finalApkName = "MDRRMO-Quezon-v${appVersion}.apk"
    $destApkRoot = Join-Path $ReleasesDir $finalApkName
    # Clean up older versioned APKs so only the latest is served
    Get-ChildItem -Path $ReleasesDir -Filter "MDRRMO-Quezon-*.apk" -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destApkRoot } | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $WwwrootReleasesDir -Filter "MDRRMO-Quezon-*.apk" -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destApkWww } | Remove-Item -Force -ErrorAction SilentlyContinue

    Copy-Item -Path $sourceApk -Destination $destApkRoot -Force
    Copy-Item -Path $sourceApk -Destination $destApkWww -Force
    Remove-Item -Recurse -Force $StagingAndroidDir -ErrorAction SilentlyContinue

    $apkSize = "{0:N2} MB" -f ((Get-Item $destApkRoot).Length / 1MB)
    Write-Host "      [ANDROID APK READY] $finalApkName ($apkSize)" -ForegroundColor Green

    # ------------------------------------------------------------------------------
    # STEP 5: Build WinUI Core Standalone Binaries
    # ------------------------------------------------------------------------------
    Write-Host "[5/7] Compiling core WinUI 3 application (win-x64 Standalone)..." -ForegroundColor Yellow

    & dotnet publish "$MauiProjectPath" `
        -f net10.0-windows10.0.19041.0 `
        -c $Configuration `
        -r win-x64 `
        --no-restore `
        -p:PublishReadyToRun=false `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -o "$StagingWinDir" `
        -v m

    if ($LASTEXITCODE -ne 0) {
        throw "Windows core publish failed with exit code $LASTEXITCODE."
    }

    # ------------------------------------------------------------------------------
    # STEP 6: Package Compressed Payload & Compile Single-File SFX Wrapper
    # ------------------------------------------------------------------------------
    Write-Host "[6/7] Building Windows Single-File SFX Executable..." -ForegroundColor Yellow
    $payloadZip = Join-Path $LauncherWorkDir "app_payload.zip"

    Compress-Archive -Path "$StagingWinDir\*" -DestinationPath "$payloadZip" -CompressionLevel Optimal -Force

    $iconSource = Join-Path $StagingWinDir "appicon.ico"
    $iconDest = Join-Path $LauncherWorkDir "appicon.ico"
    $hasIcon = $false
    if (Test-Path $iconSource) {
        Copy-Item -Path $iconSource -Destination $iconDest -Force
        $hasIcon = $true
    }

    $launcherCs = @"
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MDRRMOQuezon.Launcher;

static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var baseDir = Path.Combine(appData, "GabsHybridApp", "App");
            var versionFolder = "v_${appVersion}";
            var targetDir = Path.Combine(baseDir, versionFolder);
            var readyMarker = Path.Combine(targetDir, ".ready");
            var exePath = Path.Combine(targetDir, "GabsHybridApp.Maui.exe");

            if (!File.Exists(readyMarker) || !File.Exists(exePath))
            {
                if (Directory.Exists(targetDir))
                {
                    try { Directory.Delete(targetDir, true); } catch { }
                }
                Directory.CreateDirectory(targetDir);

                var assembly = Assembly.GetExecutingAssembly();
                var resNames = assembly.GetManifestResourceNames();
                string targetRes = null;
                foreach (var name in resNames)
                {
                    if (name.EndsWith("app_payload.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        targetRes = name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(targetRes))
                {
                    MessageBox(IntPtr.Zero, "Embedded application payload was not found.", "Launch Error", 0x10);
                    return;
                }

                using (var stream = assembly.GetManifestResourceStream(targetRes))
                {
                    if (stream == null)
                    {
                        MessageBox(IntPtr.Zero, "Failed to open embedded stream: " + targetRes, "Launch Error", 0x10);
                        return;
                    }
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    archive.ExtractToDirectory(targetDir, overwriteFiles: true);
                }

                File.WriteAllText(readyMarker, DateTime.UtcNow.ToString("o"));
            }

            if (!File.Exists(exePath))
            {
                MessageBox(IntPtr.Zero, "Target executable not found at: " + exePath, "Launch Error", 0x10);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = targetDir,
                UseShellExecute = true
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, "Failed to launch GabsHybridApp:\n\n" + ex.Message, "GabsHybridApp Error", 0x10);
        }
    }
}
"@

    Set-Content -Path (Join-Path $LauncherWorkDir "Program.cs") -Value $launcherCs -Encoding UTF8

    $iconProperty = if ($hasIcon) { "<ApplicationIcon>appicon.ico</ApplicationIcon>" } else { "" }

    $launcherCsproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <ApplicationTitle>GabsHybridApp</ApplicationTitle>
    <Product>GabsHybridApp Emergency System</Product>
    <Company>Gabs Company</Company>
    <Version>${appVersion}</Version>
    $iconProperty
  </PropertyGroup>
  <ItemGroup>
    <EmbeddedResource Include="app_payload.zip" LogicalName="app_payload.zip" />
  </ItemGroup>
</Project>
"@

    $launcherCsprojPath = Join-Path $LauncherWorkDir "MDRRMO-Launcher.csproj"
    Set-Content -Path $launcherCsprojPath -Value $launcherCsproj -Encoding UTF8

    $sfxPublishDir = Join-Path $LauncherWorkDir "_out"

    & dotnet publish "$launcherCsprojPath" `
        -c $Configuration `
        -r win-x64 `
        -o "$sfxPublishDir" `
        -v m

    if ($LASTEXITCODE -ne 0) {
        throw "Single-file SFX compilation failed with exit code $LASTEXITCODE."
    }

    $generatedExe = Join-Path $sfxPublishDir "MDRRMO-Launcher.exe"
    $finalExeName = "MDRRMO-Quezon-v${appVersion}.exe"
    $destExeRoot = Join-Path $ReleasesDir $finalExeName
    $destExeWww = Join-Path $WwwrootReleasesDir $finalExeName

    Get-ChildItem -Path $ReleasesDir -Filter "MDRRMO-Quezon-*.exe" -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destExeRoot } | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $WwwrootReleasesDir -Filter "MDRRMO-Quezon-*.exe" -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destExeWww } | Remove-Item -Force -ErrorAction SilentlyContinue

    Copy-Item -Path $generatedExe -Destination $destExeRoot -Force
    Copy-Item -Path $destExeRoot -Destination $destExeWww -Force
    $exeSize = "{0:N2} MB" -f ((Get-Item $destExeRoot).Length / 1MB)

    # ------------------------------------------------------------------------------
    # STEP 7: Create Windows Distribution ZIP & Deploy to wwwroot
    # ------------------------------------------------------------------------------
    Write-Host "[7/7] Zipping Windows executable for antivirus-safe web downloads..." -ForegroundColor Yellow

    $finalZipName = "MDRRMO-Quezon-Windows-v${appVersion}.zip"
    $destZipRoot = Join-Path $ReleasesDir $finalZipName
    $destZipWww = Join-Path $WwwrootReleasesDir $finalZipName

    Get-ChildItem -Path $ReleasesDir -Filter "MDRRMO-Quezon-Windows-*.zip" -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destZipRoot } | Remove-Item -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $WwwrootReleasesDir -Filter "MDRRMO-Quezon-Windows-*.zip" -File -ErrorAction SilentlyContinue | Where-Object { $_.FullName -ne $destZipWww } | Remove-Item -Force -ErrorAction SilentlyContinue

    # Create a clean temporary zip directory containing only the renamed .exe
    $tempZipFolder = Join-Path $LauncherWorkDir "_zip_pkg"
    New-Item -ItemType Directory -Path $tempZipFolder -Force | Out-Null
    Copy-Item -Path $destExeRoot -Destination (Join-Path $tempZipFolder $finalExeName) -Force

    Compress-Archive -Path "$tempZipFolder\*" -DestinationPath "$destZipRoot" -CompressionLevel Optimal -Force
    Copy-Item -Path $destZipRoot -Destination $destZipWww -Force
    $zipSize = "{0:N2} MB" -f ((Get-Item $destZipRoot).Length / 1MB)
    Write-Host "      [WINDOWS ZIP READY] $finalZipName ($zipSize)" -ForegroundColor Green

    # ------------------------------------------------------------------------------
    # STEP 8: Generate Release Manifest (version.json for In-App Updates)
    # ------------------------------------------------------------------------------
    Write-Host "      Generating version.json manifest for in-app updates..." -ForegroundColor Yellow

    $apkLength = if (Test-Path $destApkRoot) { (Get-Item $destApkRoot).Length } else { 0 }
    $exeLength = if (Test-Path $destExeRoot) { (Get-Item $destExeRoot).Length } else { 0 }

    $versionManifest = [ordered]@{
        version = $appVersion
        buildNumber = $versionCode
        releaseNotes = "GabsHybridApp Emergency & Disaster Risk Reduction System v$appVersion."
        releaseDateUtc = [DateTime]::UtcNow.ToString("o")
        androidDownloadUrl = "/download/android"
        windowsDownloadUrl = "/download/windows-exe"
        windowsZipDownloadUrl = "/download/windows"
        androidApk = $finalApkName
        windowsZip = $finalZipName
        windowsExe = $finalExeName
        androidSizeBytes = $apkLength
        windowsSizeBytes = $exeLength
        isMandatory = $false
    }

    $versionJson = $versionManifest | ConvertTo-Json -Depth 4
    $destVersionJsonRoot = Join-Path $ReleasesDir "version.json"
    $destVersionJsonWww = Join-Path $WwwrootReleasesDir "version.json"

    Set-Content -Path $destVersionJsonRoot -Value $versionJson -Encoding UTF8
    Set-Content -Path $destVersionJsonWww -Value $versionJson -Encoding UTF8
    Write-Host "      [VERSION MANIFEST READY] version.json emitted to release endpoints." -ForegroundColor Green

    # Clean temporary folders
    Remove-Item -Recurse -Force $StagingWinDir -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $LauncherWorkDir -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "   All Releases Successfully Generated & Published!      " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "   [1] Android ARM64 Release APK (arm64-v8a):" -ForegroundColor Cyan
    Write-Host "       -> Root:    $destApkRoot ($apkSize)" -ForegroundColor Cyan
    Write-Host "       -> Web App: $destApkWww" -ForegroundColor Cyan
    Write-Host "       -> Link:    /download/android" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   [2] Windows Single-File SFX (Direct EXE & Safe ZIP):" -ForegroundColor Cyan
    Write-Host "       -> EXE:     $destExeRoot ($exeSize)" -ForegroundColor Cyan
    Write-Host "       -> ZIP:     $destZipRoot ($zipSize)" -ForegroundColor Cyan
    Write-Host "       -> Stream:  /download/windows-exe" -ForegroundColor Yellow
    Write-Host "       -> Browser: /download/windows" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   [3] Release Manifest:" -ForegroundColor Cyan
    Write-Host "       -> API:     /api/app-version" -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "      [WARNING] Client application packaging failed: $($_.Exception.Message)" -ForegroundColor Yellow
    Deploy-ExistingReleases
    Remove-Item -Recurse -Force $StagingWinDir -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $StagingAndroidDir -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $LauncherWorkDir -ErrorAction SilentlyContinue
    return
}
