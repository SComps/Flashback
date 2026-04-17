# Flashback Suite - Windows Publish Script (AOT / Single File)
# NOTE: Flashback.LicenseGenerator is EXCLUDED from this script to prevent shipping to end users.
$ErrorActionPreference = "Stop"

# Define default path (outside the git tree) and prompt user
$RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$DefaultPublishDir = Join-Path (Split-Path $RepoRoot -Parent) "Flashback-Publish"
Write-Host "`nWhere should the publish output be located?" -ForegroundColor White
Write-Host "Default: $DefaultPublishDir" -ForegroundColor Gray
$InputPath = Read-Host "Path [Enter for default]"

if ([string]::IsNullOrWhiteSpace($InputPath)) {
    $PublishDir = $DefaultPublishDir
} else {
    $PublishDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InputPath)
}

# Stop running processes/services that might lock the publish directory
Write-Host "Closing running Flashback components..." -ForegroundColor Yellow
$Services = @("FlashbackEngine", "FlashbackConfig3270")
foreach ($svc in $Services) {
    if (Get-Service $svc -ErrorAction SilentlyContinue) {
        Stop-Service $svc -Force -ErrorAction SilentlyContinue
    }
}
Stop-Process -Name "Flashback.Tray" -Force -ErrorAction SilentlyContinue

# Selective cleanup: Preserve .dat and .lic files, but purge all binaries/debug symbols
if (Test-Path $PublishDir) { 
    Write-Host "Cleaning publish directory (preserving config and licenses)..." -ForegroundColor Gray
    # Ensure any previous session handles are released
    Start-Sleep -Seconds 1
    Get-ChildItem -Path $PublishDir -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -notin @('.dat', '.lic') } | Remove-Item -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force $PublishDir | Out-Null

Write-Host "Publishing Flashback Suite for Windows..." -ForegroundColor Cyan

# Engine (Console App - AOT Compatible)
Write-Host "-> Publishing Flashback.Engine (Native AOT)..."
dotnet publish ..\Flashback.Engine\Flashback.Engine.vbproj -c Release -r win-x64 -f net10.0-windows --self-contained true /p:PublishAot=true /p:PublishDir=$PublishDir

# Console Config (Console App - AOT Compatible)
Write-Host "-> Publishing Flashback.Config.Console (Native AOT)..."
dotnet publish ..\Flashback.Config.Console\Flashback.Config.Console.vbproj -c Release -r win-x64 -f net10.0-windows --self-contained true /p:PublishAot=true /p:PublishDir=$PublishDir

# 3270 Config (Console App - AOT Compatible)
Write-Host "-> Publishing Flashback.Config.3270 (Native AOT)..."
dotnet publish ..\Flashback.Config.3270\Flashback.Config.3270.vbproj -c Release -r win-x64 -f net10.0-windows --self-contained true /p:PublishAot=true /p:PublishDir=$PublishDir

# Tray Controller (WinForms - NOT AOT Compatible, using SingleFile)
Write-Host "-> Publishing Flashback.Tray (Single File)..."
dotnet publish ..\Flashback.Tray\Flashback.Tray.vbproj -c Release -r win-x64 -f net10.0-windows --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishDir=$PublishDir

Write-Host "`nPublish complete! Files located in: $PublishDir" -ForegroundColor Green
