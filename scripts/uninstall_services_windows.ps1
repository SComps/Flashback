# Flashback Suite - Windows Service Uninstaller
# Run this script as Administrator to remove the Engine and 3270 Config services.

$ErrorActionPreference = "Stop"

# Check for Administrator privileges
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script MUST be run as an Administrator."
    exit
}

$services = @("FlashbackEngine", "FlashbackConfig3270")

foreach ($svc in $services) {
    if (Get-Service $svc -ErrorAction SilentlyContinue) {
        Write-Host "Removal: $svc..." -ForegroundColor Cyan
        Stop-Service $svc -Force -ErrorAction SilentlyContinue
        sc.exe delete $svc
        Write-Host "$svc has been removed." -ForegroundColor Green
    } else {
        Write-Host "$svc is not installed." -ForegroundColor Gray
    }
}

Write-Host "`nUninstallation Complete." -ForegroundColor Cyan
