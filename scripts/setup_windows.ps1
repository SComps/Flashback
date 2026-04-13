# ============================================================================
# Flashback Suite - Automated Windows Installer
# ============================================================================
# This package installs the application binaries to Program Files, configures 
# all background host services, and sets up the System Tray utility.
# ============================================================================

$ErrorActionPreference = "Stop"

# 1. Elevate to Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "Restarting script with Administrator privileges..."
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Flashback Printer System Installer   " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 2. Define Installation Paths
$InstallDir = "$env:ProgramFiles\Flashback"
$SourceDir = $PSScriptRoot

if (-not $SourceDir -or -not (Test-Path "$SourceDir\Flashback.Engine.exe")) {
    Write-Error "Could not find Flashback binaries in the current directory. Please ensure you extracted the full zip before running!"
    exit
}

# 3. Stop Existing Instances
Write-Host "Stopping existing services and processes..." -ForegroundColor Yellow
Stop-Service -Name "FlashbackEngine" -ErrorAction SilentlyContinue
Stop-Service -Name "FlashbackConfig3270" -ErrorAction SilentlyContinue
Get-Process -Name "Flashback.Tray" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Process -Name "Flashback.Config.Console" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# 4. Copy Binaries to Program Files
Write-Host "Installing binaries to $InstallDir..." -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -Path $InstallDir -ItemType Directory -Force | Out-Null
}

Get-ChildItem -Path $SourceDir\* -Recurse | ForEach-Object {
    $targetFile = $_.FullName.Replace($SourceDir.Path, $InstallDir)
    if ($_.PSIsContainer) {
        if (-not (Test-Path $targetFile)) { New-Item -ItemType Directory -Path $targetFile -Force | Out-Null }
    } else {
        # DO NOT overwrite existing configuration files if reinstalling
        if (($_.Extension -eq ".dat" -or $_.Extension -eq ".lic") -and (Test-Path $targetFile)) {
            Write-Host "Preserving existing file: $($_.Name)" -ForegroundColor DarkGray
        } else {
            Copy-Item -Path $_.FullName -Destination $targetFile -Force
        }
    }
}

# 5. Register Windows Services
Write-Host "Registering System Services..." -ForegroundColor Yellow
$EngineExe = Join-Path $InstallDir "Flashback.Engine.exe"
$ConfigExe = Join-Path $InstallDir "Flashback.Config.3270.exe"
$TrayExe = Join-Path $InstallDir "Flashback.Tray.exe"

# Engine
if (Get-Service "FlashbackEngine" -ErrorAction SilentlyContinue) { sc.exe delete "FlashbackEngine" | Out-Null; Start-Sleep 1 }
New-Service -Name "FlashbackEngine" -BinaryPathName "`"$EngineExe`"" -DisplayName "Flashback Printer Engine" -Description "High-performance cross-platform printing service for legacy host systems." -StartupType Automatic | Out-Null
Write-Host " - Flashback Engine Installed" -ForegroundColor Green

# 3270 Config Server
if (Get-Service "FlashbackConfig3270" -ErrorAction SilentlyContinue) { sc.exe delete "FlashbackConfig3270" | Out-Null; Start-Sleep 1 }
New-Service -Name "FlashbackConfig3270" -BinaryPathName "`"$ConfigExe`"" -DisplayName "Flashback 3270 Config Server" -Description "Remote 3270 terminal-based configuration server for Flashback devices." -StartupType Automatic | Out-Null
Write-Host " - Flashback 3270 Server Installed" -ForegroundColor Green

# 6. Configure System Tray Auto-Start
Write-Host "Configuring System Tray..." -ForegroundColor Yellow
$RegistryPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
Set-ItemProperty -Path $RegistryPath -Name "FlashbackController" -Value "`"$TrayExe`""
Write-Host " - Added Flashback Tray to Windows Startup" -ForegroundColor Green

# 7. Create Start Menu Shortcut
$StartMenuPath = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Flashback"
if (-not (Test-Path $StartMenuPath)) { New-Item -ItemType Directory -Path $StartMenuPath -Force | Out-Null }

$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$StartMenuPath\Flashback Controller.lnk")
$Shortcut.TargetPath = $TrayExe
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "Manage Flashback Printer System"
$Shortcut.Save()
Write-Host " - Created Start Menu Shortcut" -ForegroundColor Green

# 8. Finalization
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Installation Successfully Completed! " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting System Tray..." -ForegroundColor DarkGray
Start-Process $TrayExe -WorkingDirectory $InstallDir

Write-Host "Done. You may close this window." -ForegroundColor Green
Start-Sleep -Seconds 5
