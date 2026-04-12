# Flashback Suite - Windows Publish Script (AOT / Single File)
# NOTE: Flashback.LicenseGenerator is EXCLUDED from this script to prevent shipping to end users.
$ErrorActionPreference = "Stop"

$PublishDir = "..\publish\windows"
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
New-Item -ItemType Directory -Force $PublishDir | Out-Null

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
