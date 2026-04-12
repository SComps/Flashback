# Flashback Suite - Windows Service Installer
# Run this script as Administrator to register the Engine and 3270 Config services.

$ErrorActionPreference = "Stop"

# Check for Administrator privileges
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script MUST be run as an Administrator."
    exit
}

$InstallDir = Read-Host -Prompt "Enter the full path to the Flashback binaries (Default: $PSScriptRoot\..\publish\windows)"
if ([string]::IsNullOrWhiteSpace($InstallDir)) { $InstallDir = "$PSScriptRoot\..\publish\windows" }
$InstallDir = Resolve-Path $InstallDir

# --- Flashback Engine Service ---
$EngineSvcName = "FlashbackEngine"
$EngineExe = Join-Path $InstallDir "Flashback.Engine.exe"

if (Test-Path $EngineExe) {
    Write-Host "Registering $EngineSvcName..." -ForegroundColor Cyan
    if (Get-Service $EngineSvcName -ErrorAction SilentlyContinue) {
        Write-Host "Service already exists. Re-registering..." -ForegroundColor Yellow
        Stop-Service $EngineSvcName -ErrorAction SilentlyContinue
        sc.exe delete $EngineSvcName
        Start-Sleep -Seconds 2
    }
    
    New-Service -Name $EngineSvcName `
                -BinaryPathName "`"$EngineExe`"" `
                -DisplayName "Flashback Printer Engine" `
                -Description "High-performance cross-platform printing service for legacy host systems." `
                -StartupType Automatic
    
    Write-Host "$EngineSvcName registered successfully." -ForegroundColor Green
} else {
    Write-Warning "Flashback.Engine.exe not found at $EngineExe. Skipping."
}

# --- Flashback 3270 Config Service ---
$ConfigSvcName = "FlashbackConfig3270"
$ConfigExe = Join-Path $InstallDir "Flashback.Config.3270.exe"

if (Test-Path $ConfigExe) {
    Write-Host "Registering $ConfigSvcName..." -ForegroundColor Cyan
    if (Get-Service $ConfigSvcName -ErrorAction SilentlyContinue) {
        Write-Host "Service already exists. Re-registering..." -ForegroundColor Yellow
        Stop-Service $ConfigSvcName -ErrorAction SilentlyContinue
        sc.exe delete $ConfigSvcName
        Start-Sleep -Seconds 2
    }
    
    New-Service -Name $ConfigSvcName `
                -BinaryPathName "`"$ConfigExe`"" `
                -DisplayName "Flashback 3270 Config Server" `
                -Description "Remote 3270 terminal-based configuration server for Flashback devices." `
                -StartupType Automatic
    
    Write-Host "$ConfigSvcName registered successfully." -ForegroundColor Green
} else {
    Write-Warning "Flashback.Config.3270.exe not found at $ConfigExe. Skipping."
}

Write-Host "`nInstallation Complete. Use the Flashback Tray Controller to start the services." -ForegroundColor Cyan
