# Flashback WPF Configuration Tool - Standalone Publish Script
$ErrorActionPreference = "Stop"

# Define paths
$RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$DefaultPublishDir = Join-Path (Split-Path $RepoRoot -Parent) "Flashback-Publish"

Write-Host "`nFlashback WPF Publish" -ForegroundColor Cyan
Write-Host "Where should the output be located?" -ForegroundColor White
Write-Host "Default: $DefaultPublishDir" -ForegroundColor Gray
$InputPath = Read-Host "Path [Enter for default]"

$PublishDir = If ([string]::IsNullOrWhiteSpace($InputPath)) { $DefaultPublishDir } Else { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InputPath) }
if (-not (Test-Path $PublishDir)) { New-Item -ItemType Directory -Force $PublishDir | Out-Null }

# Ensure the application isn't running (avoids UnauthorizedAccessException)
$RunningApp = Get-Process "Flashback.Config.WPF" -ErrorAction SilentlyContinue
if ($RunningApp) {
    Write-Host "`nWARNING: Flashback Configuration is currently running." -ForegroundColor Yellow
    Write-Host "Attempting to close the application for update..." -ForegroundColor Gray
    $RunningApp | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

Write-Host "`n-> Publishing Flashback.Config.WPF (Single File)..." -ForegroundColor Yellow

# WPF doesn't support NativeAOT like console apps, so we use SingleFile instead
dotnet publish "E:\Flashback\Flashback.Config.WPF\Flashback.Config.WPF.vbproj" `
    -c Release `
    -r win-x64 `
    -f net10.0-windows `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishDir=$PublishDir

Write-Host "`nPublish complete! WPF Config located in: $PublishDir" -ForegroundColor Green
