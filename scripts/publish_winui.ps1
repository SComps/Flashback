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

# Stop running processes that might lock the publish directory
Write-Host "Closing running Flashback components..." -ForegroundColor Yellow
Stop-Process -Name "Flashback.Config.WinUI" -Force -ErrorAction SilentlyContinue

# We won't wipe the directory completely here like the primary script to avoid killing other apps' binaries, 
# although we could if we wanted this scripts to be entirely standalone. 
# Just forcing the directory creation.
New-Item -ItemType Directory -Force $PublishDir | Out-Null

Write-Host "Publishing Flashback.Config.WinUI..." -ForegroundColor Cyan

# Publish WinUI application
dotnet publish ..\Flashback.Config.WinUI\Flashback.Config.WinUI.csproj -c Release -r win-x64 -f net10.0-windows10.0.19041.0 --self-contained true /p:PublishDir=$PublishDir /p:WindowsAppSDKSelfContained=true /p:SatelliteResourceLanguages=en

Write-Host "`nPublish complete! Files located in: $PublishDir" -ForegroundColor Green
