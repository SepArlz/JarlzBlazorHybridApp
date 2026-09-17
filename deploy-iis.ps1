# Run this script in PowerShell as Administrator to deploy GabsHybridApp.Web to IIS

$ErrorActionPreference = "Continue"

$siteName = "GabsHybridApp"
$appPoolName = "GabsHybridAppPool"
$port = 8080
$sourceDir = "c:\Users\Edwill\Documents\Coding\GabsBlazorHybridApp\publish\web"
$targetDir = "C:\inetpub\GabsBlazorHybridApp"

Write-Host "=== 1. Stopping IIS services & releasing locks ===" -ForegroundColor Cyan
& net stop was /y 2>$null
& net stop w3svc 2>$null

Get-Process -Name "w3wp", "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "=== 2. Copying published files to $targetDir ===" -ForegroundColor Cyan
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}
if (!(Test-Path "$targetDir\logs")) {
    New-Item -ItemType Directory -Path "$targetDir\logs" -Force | Out-Null
}
if (!(Test-Path "$targetDir\Data")) {
    New-Item -ItemType Directory -Path "$targetDir\Data" -Force | Out-Null
}

Copy-Item -Path "$sourceDir\*" -Destination $targetDir -Recurse -Force

Write-Host "=== 3. Setting permissions for IIS & SQLite ===" -ForegroundColor Cyan
# Grant permissions using icacls for guaranteed application
& icacls "$targetDir" /grant "IIS_IUSRS:(OI)(CI)F" /T /Q
& icacls "$targetDir" /grant "IUSR:(OI)(CI)F" /T /Q
& icacls "$targetDir" /grant "IIS AppPool\$appPoolName":(OI)(CI)F /T /Q 2>$null

Write-Host "=== 4. Starting IIS Services ===" -ForegroundColor Cyan
& net start w3svc 2>$null

Write-Host "=== 5. Configuring IIS App Pool and Website ===" -ForegroundColor Cyan
Import-Module WebAdministration -ErrorAction SilentlyContinue

# Setup App Pool
if (!(Test-Path "IIS:\AppPools\$appPoolName")) {
    Write-Host "Creating App Pool: $appPoolName"
    New-WebAppPool -Name $appPoolName | Out-Null
}

# Configure App Pool for ASP.NET Core
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "enable32BitAppOnWin64" -Value $false
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "failure.rapidFailProtection" -Value $false

# Also ensure DefaultAppPool has No Managed Code to avoid conflicts
if (Test-Path "IIS:\AppPools\DefaultAppPool") {
    Set-ItemProperty "IIS:\AppPools\DefaultAppPool" -Name "managedRuntimeVersion" -Value ""
}

# Setup Site
if (Test-Path "IIS:\Sites\$siteName") {
    Write-Host "Updating site configuration for $siteName..."
    Set-ItemProperty "IIS:\Sites\$siteName" -Name "physicalPath" -Value $targetDir
    Set-ItemProperty "IIS:\Sites\$siteName" -Name "applicationPool" -Value $appPoolName
} else {
    Write-Host "Creating new IIS Website on port $port..."
    New-WebSite -Name $siteName -Port $port -PhysicalPath $targetDir -ApplicationPool $appPoolName | Out-Null
}

# Start Pool & Site
Start-WebAppPool -Name $appPoolName -ErrorAction SilentlyContinue
Start-WebSite -Name $siteName -ErrorAction SilentlyContinue

Write-Host "`n✅ Deployment complete! Access your app at: http://localhost:$port" -ForegroundColor Green
Start-Process "http://localhost:$port"
