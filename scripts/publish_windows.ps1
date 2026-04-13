# Flashback Suite - Windows Publish Script (AOT / Single File)
# NOTE: Flashback.LicenseGenerator is EXCLUDED from this script to prevent shipping to end users.
$ErrorActionPreference = "Stop"

$PublishDir = "..\publish\windows"

# Stop running processes/services that might lock the publish directory
Write-Host "Closing running Flashback components..." -ForegroundColor Yellow
$Services = @("FlashbackEngine", "FlashbackConfig3270")
foreach ($svc in $Services) {
    if (Get-Service $svc -ErrorAction SilentlyContinue) {
        Stop-Service $svc -Force -ErrorAction SilentlyContinue
    }
}
Stop-Process -Name "Flashback.Tray" -Force -ErrorAction SilentlyContinue

if (Test-Path $PublishDir) { 
    Write-Host "Cleaning publish directory (preserving config and licenses)..." -ForegroundColor Gray
    # Give a moment for file handles to release
    Start-Sleep -Seconds 2
    try {
        Get-ChildItem -Path $PublishDir -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -notin @('.dat', '.lic') } | Remove-Item -Force -ErrorAction Stop
    } catch {
        Write-Host "Critical: Publishing directory files are locked by another process (likely a terminal, Explorer, or IDE)." -ForegroundColor Red
        Write-Host "Please close any running apps using $PublishDir and try again." -ForegroundColor White
        exit
    }
} else {
    New-Item -ItemType Directory -Force $PublishDir | Out-Null
}

Write-Host "Publishing Flashback Suite for Windows..." -ForegroundColor Cyan

# Engine (Console App - AOT Compatible)
Write-Host "-> Publishing Flashback.Engine (Native AOT)..."
dotnet publish ..\Flashback.Engine\Flashback.Engine.vbproj -c Release -r win-x64 --self-contained true /p:PublishAot=true /p:PublishDir=$PublishDir

# Console Config (Console App - AOT Compatible)
Write-Host "-> Publishing Flashback.Config.Console (Native AOT)..."
dotnet publish ..\Flashback.Config.Console\Flashback.Config.Console.vbproj -c Release -r win-x64 --self-contained true /p:PublishAot=true /p:PublishDir=$PublishDir

# 3270 Config (Console App - AOT Compatible)
Write-Host "-> Publishing Flashback.Config.3270 (Native AOT)..."
dotnet publish ..\Flashback.Config.3270\Flashback.Config.3270.vbproj -c Release -r win-x64 --self-contained true /p:PublishAot=true /p:PublishDir=$PublishDir

# Tray Controller (WinForms - NOT AOT Compatible, using SingleFile)
Write-Host "-> Publishing Flashback.Tray (Single File)..."
dotnet publish ..\Flashback.Tray\Flashback.Tray.vbproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishDir=$PublishDir

Write-Host "`nPublish complete! Files located in: $PublishDir" -ForegroundColor Green
