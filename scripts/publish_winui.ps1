# Flashback WinUI Configuration Tool - Standalone Publish Script
# NOTE: WinUI 3 / Windows App SDK does not support SingleFile or NativeAOT.
#       The output will always be a folder of files — this is unavoidable for WinUI.
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

# Stop running processes that might lock the publish directory
Write-Host "Closing running Flashback components..." -ForegroundColor Yellow
Stop-Process -Name "Flashback.Config.WinUI" -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force $PublishDir | Out-Null

Write-Host "Publishing Flashback.Config.WinUI..." -ForegroundColor Cyan

# WinUI 3 cannot be single-file or AOT; publishes as a folder
dotnet publish ..\Flashback.Config.WinUI\Flashback.Config.WinUI.csproj -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained true /p:PublishDir=$PublishDir /p:WindowsAppSDKSelfContained=true /p:SatelliteResourceLanguages=en

Write-Host "`nPublish complete! Files located in: $PublishDir" -ForegroundColor Green
