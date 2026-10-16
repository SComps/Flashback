# -----------------------------------------------------------------------------
# Build Flashback.TN3270Framework and copy the DLL (and deps) to an output folder.
#
# Usage:
#   .\scripts\publish_tn3270framework.ps1
#   .\scripts\publish_tn3270framework.ps1 -PublishDir C:\out\tn3270
# -----------------------------------------------------------------------------
param(
    [string]$PublishDir = "",
    [string]$Configuration = "Release",
    [string]$Framework = "net10.0"
)

$ErrorActionPreference = "Stop"

$RepoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$Project  = Join-Path $RepoRoot "Flashback.TN3270Framework\TN3270Framework.vbproj"
$DefaultPublishDir = Join-Path (Split-Path $RepoRoot -Parent) "TN3270Framework-Publish"

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    Write-Host ""
    Write-Host "Where should the TN3270Framework DLL be placed?" -ForegroundColor White
    Write-Host "Default: $DefaultPublishDir" -ForegroundColor Gray
    $InputPath = Read-Host "Path [Enter for default]"
    $PublishDir = if ([string]::IsNullOrWhiteSpace($InputPath)) {
        $DefaultPublishDir
    } else {
        $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InputPath)
    }
}

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null

Write-Host ""
Write-Host "=== TN3270Framework build ===" -ForegroundColor Cyan
Write-Host "  Project : $Project"
Write-Host "  Config  : $Configuration"
Write-Host "  TFM     : $Framework"
Write-Host "  Output  : $PublishDir"
Write-Host ""

if (-not (Test-Path $Project)) {
    Write-Error "Project not found: $Project"
}

Write-Host "-> Restoring..." -ForegroundColor Yellow
dotnet restore $Project
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

Write-Host "-> Building ($Configuration / $Framework)..." -ForegroundColor Yellow
dotnet build $Project -c $Configuration -f $Framework --no-restore -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Output: $PublishDir"
Get-ChildItem -Path $PublishDir -Filter "*.dll" -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host "  $($_.Name)" }
Write-Host ""
