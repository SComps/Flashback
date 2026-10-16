# Test script for printer configuration validation
$ErrorActionPreference = "Stop"

$engineDir = "e:\Flashback\Flashback.Engine\bin\Debug\net10.0"
$engineExe = Join-Path $engineDir "Flashback.Engine.exe"
$devicesDat = Join-Path $engineDir "devices.dat"
$printersLog = Join-Path $engineDir "printers.log"

Write-Host "=== FLASHBACK PRINTER CONFIG VALIDATION TESTS ===" -ForegroundColor Cyan

# Cleanup helper
function Cleanup-Files {
    if (Test-Path $devicesDat) { Remove-Item $devicesDat -Force }
    if (Test-Path $printersLog) { Remove-Item $printersLog -Force }
}

$passCount = 0
$failCount = 0

function Assert-Test($condition, $testName) {
    if ($condition) {
        Write-Host "  [PASS] $testName" -ForegroundColor Green
        $script:passCount++
    } else {
        Write-Host "  [FAIL] $testName" -ForegroundColor Red
        $script:failCount++
    }
}

# -------------------------------------------------------------
# Test 1: Duplicate Output Directories (Direct Match) -> Should Fail Fast
# -------------------------------------------------------------
Write-Host "`nTest 1: Direct Duplicate Output Directories..." -ForegroundColor Yellow
Cleanup-Files
$cfgContent = @"
PRT1||Printer 1||0||0||127.0.0.1:9001||0||False||True||0||Output||0||0||True
PRT2||Printer 2||0||0||127.0.0.1:9002||0||False||True||0||Output||0||0||True
"@
[System.IO.File]::WriteAllText($devicesDat, $cfgContent)

$p = Start-Process -FilePath $engineExe -NoNewWindow -PassThru -Wait -RedirectStandardError "$engineDir\test1_err.txt" -RedirectStandardOutput "$engineDir\test1_out.txt"
$exitCode = $p.ExitCode
$errText = [System.IO.File]::ReadAllText("$engineDir\test1_err.txt")
$logContent = if (Test-Path $printersLog) { [System.IO.File]::ReadAllText($printersLog) } else { "" }

Assert-Test ($exitCode -eq 1) "Exit code should be 1 on duplicate directories (Got: $exitCode)"
Assert-Test ($errText -match "duplicate output directories detected") "Stderr should report duplicate output directories"
Assert-Test ($errText -match "PRT1") "Stderr should mention PRT1"
Assert-Test ($errText -match "PRT2") "Stderr should mention PRT2"
Assert-Test ($logContent -match "duplicate output directories detected") "printers.log should record fatal configuration error"

# -------------------------------------------------------------
# Test 2: Path Variations (Casing, Slashes, Relative Dots) -> Should Fail Fast
# -------------------------------------------------------------
Write-Host "`nTest 2: Path Variations (Casing, Slashes, Relative Dots)..." -ForegroundColor Yellow
Cleanup-Files
$cfgContent = @"
PRT1||Printer 1||0||0||127.0.0.1:9001||0||False||True||0||Output||0||0||True
PRT2||Printer 2||0||0||127.0.0.1:9002||0||False||True||0||.\OUTPUT\||0||0||True
"@
[System.IO.File]::WriteAllText($devicesDat, $cfgContent)

$p = Start-Process -FilePath $engineExe -NoNewWindow -PassThru -Wait -RedirectStandardError "$engineDir\test2_err.txt" -RedirectStandardOutput "$engineDir\test2_out.txt"
$exitCode = $p.ExitCode
$errText = [System.IO.File]::ReadAllText("$engineDir\test2_err.txt")

Assert-Test ($exitCode -eq 1) "Exit code should be 1 on canonical path match (Got: $exitCode)"
Assert-Test ($errText -match "duplicate output directories detected") "Stderr should report duplicate output directories"

# -------------------------------------------------------------
# Test 3: Disabled Printer with Duplicate Directory -> Should Fail Fast
# -------------------------------------------------------------
Write-Host "`nTest 3: Disabled Printer with Duplicate Directory..." -ForegroundColor Yellow
Cleanup-Files
$cfgContent = @"
PRT1||Printer 1||0||0||127.0.0.1:9001||0||False||True||0||Output||0||0||True
PRT2||Printer 2||0||0||127.0.0.1:9002||0||False||True||0||Output||0||0||False
"@
[System.IO.File]::WriteAllText($devicesDat, $cfgContent)

$p = Start-Process -FilePath $engineExe -NoNewWindow -PassThru -Wait -RedirectStandardError "$engineDir\test3_err.txt" -RedirectStandardOutput "$engineDir\test3_out.txt"
$exitCode = $p.ExitCode
$errText = [System.IO.File]::ReadAllText("$engineDir\test3_err.txt")

Assert-Test ($exitCode -eq 1) "Exit code should be 1 even when one printer is disabled (Got: $exitCode)"
Assert-Test ($errText -match "duplicate output directories detected") "Stderr should report duplicate output directories"

# -------------------------------------------------------------
# Test 4: Valid Configuration (Distinct Output Directories) -> Engine Starts
# -------------------------------------------------------------
Write-Host "`nTest 4: Valid Configuration (Distinct Output Directories)..." -ForegroundColor Yellow
Cleanup-Files
$cfgContent = @"
PRT1||Printer 1||0||0||127.0.0.1:9001||0||False||True||0||Output1||0||0||True
PRT2||Printer 2||0||0||127.0.0.1:9002||0||False||True||0||Output2||0||0||True
"@
[System.IO.File]::WriteAllText($devicesDat, $cfgContent)

$p = Start-Process -FilePath $engineExe -NoNewWindow -PassThru -RedirectStandardError "$engineDir\test4_err.txt" -RedirectStandardOutput "$engineDir\test4_out.txt"
Start-Sleep -Seconds 2
$hasExited = $p.HasExited
Assert-Test (-not $hasExited) "Engine should start successfully and remain running"
if (-not $hasExited) {
    Stop-Process -Id $p.Id -Force
    $p.WaitForExit()
}
Start-Sleep -Milliseconds 500
$errText = if (Test-Path "$engineDir\test4_err.txt") { [System.IO.File]::ReadAllText("$engineDir\test4_err.txt") } else { "" }
Assert-Test ($errText -notmatch "FATAL") "No fatal configuration errors reported"

# -------------------------------------------------------------
# Test 5: Missing devices.dat -> Starts Normally
# -------------------------------------------------------------
Write-Host "`nTest 5: Missing devices.dat..." -ForegroundColor Yellow
Cleanup-Files

$p = Start-Process -FilePath $engineExe -NoNewWindow -PassThru -RedirectStandardError "$engineDir\test5_err.txt" -RedirectStandardOutput "$engineDir\test5_out.txt"
Start-Sleep -Seconds 2
$hasExited = $p.HasExited
Assert-Test (-not $hasExited) "Engine should start successfully when devices.dat does not exist"
if (-not $hasExited) {
    Stop-Process -Id $p.Id -Force
    $p.WaitForExit()
}

# Cleanup test files
Cleanup-Files
Remove-Item "$engineDir\test*.txt" -Force -ErrorAction SilentlyContinue

Write-Host "`n=== SUMMARY: $passCount passed, $failCount failed ===" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
if ($failCount -gt 0) {
    exit 1
}
