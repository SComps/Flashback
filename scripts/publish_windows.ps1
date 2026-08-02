# Flashback Suite - Windows Publish Script (Single File / Self-Contained)
# NOTE: Flashback.LicenseGenerator is EXCLUDED from this script to prevent shipping to end users.
# NOTE: VB.NET does not support Native AOT on Windows; PublishSingleFile is used instead,
#       which bundles the runtime into a single .exe — equivalent output size to AOT on Linux.
param(
    [string]$PublishDir = ""
)
$ErrorActionPreference = "Stop"

# Define default path (outside the git tree) and prompt user if not supplied
$RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$DefaultPublishDir = Join-Path (Split-Path $RepoRoot -Parent) "Flashback-Publish"

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    Write-Host "`nWhere should the publish output be located?" -ForegroundColor White
    Write-Host "Default: $DefaultPublishDir" -ForegroundColor Gray
    $InputPath = Read-Host "Path [Enter for default]"
    $PublishDir = if ([string]::IsNullOrWhiteSpace($InputPath)) {
        $DefaultPublishDir
    } else {
        $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InputPath)
    }
}

# Stop running processes/services that might lock the publish directory
Write-Host "Closing running Flashback components..." -ForegroundColor Yellow
$Services = @("FlashbackEngine", "FlashbackConfig3270", "FlashbackSpooler")
foreach ($svc in $Services) {
    if (Get-Service $svc -ErrorAction SilentlyContinue) {
        Stop-Service $svc -Force -ErrorAction SilentlyContinue
    }
}
Stop-Process -Name "Flashback.Tray" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "Flashback.Spooler" -Force -ErrorAction SilentlyContinue

# Selective cleanup: Preserve .dat and .lic files, but purge all binaries/debug symbols
if (Test-Path $PublishDir) {
    Write-Host "Cleaning publish directory (preserving config and licenses)..." -ForegroundColor Gray
    Start-Sleep -Seconds 1
    Get-ChildItem -Path $PublishDir -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -notin @('.dat', '.lic') } | Remove-Item -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force $PublishDir | Out-Null

Write-Host "Publishing Flashback Suite for Windows..." -ForegroundColor Cyan

# Shared single-file flags for all console/service apps
$SingleFileFlags = @(
    "--self-contained", "true",
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:EnableCompressionInSingleFile=true",
    "/p:PublishDir=$PublishDir"
)

# Engine (cross-platform service host — net10.0, Single File)
Write-Host "-> Publishing Flashback.Engine (Single File)..."
dotnet publish ..\Flashback.Engine\Flashback.Engine.vbproj -c Release -r win-x64 -f net10.0 @SingleFileFlags

# Console Config (cross-platform — net10.0, Single File)
Write-Host "-> Publishing Flashback.Config.Console (Single File)..."
dotnet publish ..\Flashback.Config.Console\Flashback.Config.Console.vbproj -c Release -r win-x64 -f net10.0 @SingleFileFlags

# 3270 Config (cross-platform — net10.0, Single File)
Write-Host "-> Publishing Flashback.Config.3270 (Single File)..."
dotnet publish ..\Flashback.Config.3270\Flashback.Config.3270.vbproj -c Release -r win-x64 -f net10.0 @SingleFileFlags

# Spooler Service (cross-platform — net10.0, Single File)
Write-Host "-> Publishing Flashback.Spooler (Single File)..."
dotnet publish ..\Flashback.Spooler\Flashback.Spooler.vbproj -c Release -r win-x64 -f net10.0 @SingleFileFlags

# Tray Controller (WinForms — net10.0-windows, Single File, no AOT)
Write-Host "-> Publishing Flashback.Tray (Single File)..."
dotnet publish ..\Flashback.Tray\Flashback.Tray.vbproj -c Release -r win-x64 -f net10.0-windows --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true /p:PublishDir=$PublishDir

Write-Host "`nPublish complete! Files located in: $PublishDir" -ForegroundColor Green
