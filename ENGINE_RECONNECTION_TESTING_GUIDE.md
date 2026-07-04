# Flashback Engine - Reconnection Fix Testing Guide

## Date: 2026-07-04

## Overview

This guide provides comprehensive testing procedures for validating the reconnection timeout fix in the Flashback Engine.

## Test Environment Setup

### Prerequisites
1. Flashback Engine service installed and configured
2. Access to remote system (mainframe emulator) or ability to simulate one
3. Ability to start/stop remote system or block network traffic
4. Access to engine logs for verification

### Test Configuration
- **Connection Type:** Client mode (ConnType != 3)
- **Remote System:** Known mainframe emulator on local network or internet
- **Expected Timeout:** 5 seconds
- **Expected Backoff:** 5s → 10s → 20s → 40s → 80s → 160s → 300s (max)

## Test Cases

### Test 1: Normal Connection and Reconnection

**Objective:** Verify engine connects normally and reconnects quickly after brief outage

**Steps:**
1. Ensure remote system is running
2. Start Flashback Engine service
3. Verify connection succeeds within 5 seconds
4. Check logs for: `"Connection successful"`
5. Stop remote system
6. Wait 10 seconds
7. Verify engine detects disconnect
8. Start remote system
9. **Verify engine reconnects within 5-10 seconds**

**Expected Results:**
- ✅ Initial connection: < 5 seconds
- ✅ Disconnect detected: within 1-2 minutes (TCP keep-alive)
- ✅ Reconnection after remote up: 5-10 seconds
- ✅ No service restart required

**Log Verification:**
```
[DeviceName] Attempting to connect to host:port (5s timeout)...
[DeviceName] Connection successful.
[DeviceName] Connection ended naturally. Cleaning up resources...
[DeviceName] Attempting to connect to host:port (5s timeout)...
[DeviceName] Connection successful.
```

---

### Test 2: Extended Downtime (Critical Test)

**Objective:** Verify engine reconnects after hours of downtime WITHOUT service restart

**Steps:**
1. Start engine with remote system running
2. Verify connection succeeds
3. Stop remote system
4. **Wait 2-4 hours** (simulate extended maintenance)
5. Verify engine continues retry attempts with exponential backoff
6. Check logs show backoff reaching 300 seconds (5 minutes)
7. Start remote system
8. **Verify engine reconnects within 5-10 seconds**
9. **DO NOT restart service** - this is the critical test

**Expected Results:**
- ✅ Engine continues retrying during downtime
- ✅ Backoff reaches maximum of 300 seconds
- ✅ After remote comes up, reconnection occurs within 5-10 seconds
- ✅ **No service restart required** (this is the bug fix validation)

**Log Verification:**
```
[DeviceName] Connection failed: [error]. Next retry in 5s
[DeviceName] Connection failed: [error]. Next retry in 10s
[DeviceName] Connection failed: [error]. Next retry in 20s
... (hours pass) ...
[DeviceName] Connection failed: [error]. Next retry in 300s
... (remote comes back up) ...
[DeviceName] Attempting to connect to host:port (5s timeout)...
[DeviceName] Connection successful. Backoff delay reset.
```

**Failure Indicators:**
- ❌ Engine continues reporting connection failures after remote is up
- ❌ Requires service restart to connect
- ❌ Connection attempts take > 10 seconds

---

### Test 3: Connection Timeout Validation

**Objective:** Verify connection attempts timeout at exactly 5 seconds

**Steps:**
1. Configure firewall to DROP (not reject) packets to remote port
   - Windows: `netsh advfirewall firewall add rule name="Block Flashback Test" dir=out action=block remoteport=9000 protocol=TCP`
2. Start engine
3. Monitor logs and time the connection attempts
4. Verify each attempt times out after 5 seconds
5. Remove firewall rule
6. Verify engine reconnects within 5-10 seconds

**Expected Results:**
- ✅ Each connection attempt times out at exactly 5 seconds
- ✅ Error message indicates timeout: `"Connection to host:port timed out after 5 seconds"`
- ✅ After firewall rule removed, reconnection occurs quickly

**Log Verification:**
```
[DeviceName] Attempting to connect to host:port (5s timeout)...
[DeviceName] Connection failed: Connection to host:port timed out after 5 seconds. Next retry in 5s
```

---

### Test 4: DNS Resolution Issues

**Objective:** Verify engine handles DNS failures gracefully

**Steps:**
1. Configure device with invalid hostname (e.g., "nonexistent.local")
2. Start engine
3. Verify connection fails within 5 seconds
4. Check error message
5. Update device configuration with correct hostname
6. Verify engine reconnects within 5-10 seconds

**Expected Results:**
- ✅ DNS failure detected within 5 seconds
- ✅ Clear error message about hostname resolution
- ✅ After hostname fixed, reconnection occurs quickly

---

### Test 5: Rapid Remote Restarts

**Objective:** Verify engine handles multiple quick reconnections

**Steps:**
1. Start engine with remote running
2. Restart remote system 5 times in quick succession (30 seconds apart)
3. Verify engine reconnects after each restart
4. Check for resource leaks or stuck states
5. Verify final connection is stable

**Expected Results:**
- ✅ Engine reconnects after each remote restart
- ✅ No stuck states (IsConnecting, IsClosing)
- ✅ No resource leaks (check memory usage)
- ✅ Final connection stable

---

### Test 6: Multiple Devices

**Objective:** Verify fix works with multiple configured devices

**Steps:**
1. Configure 3-5 devices pointing to different remotes
2. Start engine
3. Stop all remote systems
4. Wait 1 hour
5. Start all remote systems
6. Verify all devices reconnect within 5-10 seconds

**Expected Results:**
- ✅ All devices reconnect independently
- ✅ No interference between devices
- ✅ Backoff timers independent per device

---

### Test 7: Network Partition

**Objective:** Verify engine handles network partition scenarios

**Steps:**
1. Start engine with remote running
2. Simulate network partition (disconnect network adapter or block all traffic)
3. Wait 30 minutes
4. Restore network connectivity
5. Verify engine reconnects within 5-10 seconds

**Expected Results:**
- ✅ Engine detects network partition
- ✅ Continues retry attempts
- ✅ Reconnects quickly after network restored

---

### Test 8: Backoff Reset Validation

**Objective:** Verify backoff delay resets after successful connection

**Steps:**
1. Start engine with remote down
2. Wait until backoff reaches 300 seconds (5 minutes)
3. Start remote system
4. Verify connection succeeds
5. Stop remote system immediately
6. **Verify next retry attempt is after 5 seconds (not 300 seconds)**

**Expected Results:**
- ✅ Backoff resets to 5 seconds after successful connection
- ✅ Log shows: `"Connection successful. Backoff delay reset."`
- ✅ Next failure uses 5-second backoff, not 300-second

---

## Performance Benchmarks

### Connection Timing Expectations

| Scenario | Expected Time | Acceptable Range |
|----------|---------------|------------------|
| Initial connection (remote up) | < 2 seconds | 0-5 seconds |
| Connection timeout (remote down) | 5 seconds | 4.5-5.5 seconds |
| Reconnection after brief outage | 5-10 seconds | 5-15 seconds |
| Reconnection after extended outage | 5-10 seconds | 5-15 seconds |
| DNS resolution failure | < 5 seconds | 0-5 seconds |

### Resource Usage

Monitor these metrics during extended testing:

- **Memory Usage:** Should remain stable (no leaks)
- **Handle Count:** Should remain stable (no handle leaks)
- **CPU Usage:** Should be minimal during backoff periods
- **Network Connections:** Should show clean connect/disconnect cycles

---

## Automated Testing Script (PowerShell)

```powershell
# Test 2: Extended Downtime Test
# This script automates the critical test case

Write-Host "Starting Extended Downtime Test..." -ForegroundColor Cyan

# Configuration
$serviceName = "FlashbackEngine"
$remoteHost = "mainframe.local"
$remotePort = 9000
$downtimeHours = 2

# Step 1: Verify service is running
Write-Host "Checking service status..." -ForegroundColor Yellow
$service = Get-Service -Name $serviceName
if ($service.Status -ne "Running") {
    Write-Host "ERROR: Service is not running!" -ForegroundColor Red
    exit 1
}

# Step 2: Verify initial connection
Write-Host "Verifying initial connection..." -ForegroundColor Yellow
Start-Sleep -Seconds 10
# Check logs for "Connection successful"

# Step 3: Simulate remote down
Write-Host "Simulating remote system down (blocking port $remotePort)..." -ForegroundColor Yellow
netsh advfirewall firewall add rule name="FlashbackTest_Block" dir=out action=block remoteport=$remotePort protocol=TCP

# Step 4: Wait for extended downtime
Write-Host "Waiting $downtimeHours hours to simulate extended downtime..." -ForegroundColor Yellow
Write-Host "Monitor logs to verify backoff reaches 300 seconds..." -ForegroundColor Cyan
Start-Sleep -Seconds ($downtimeHours * 3600)

# Step 5: Simulate remote back up
Write-Host "Simulating remote system back up (removing firewall rule)..." -ForegroundColor Yellow
netsh advfirewall firewall delete rule name="FlashbackTest_Block"

# Step 6: Wait for reconnection
Write-Host "Waiting for reconnection (should occur within 5-10 seconds)..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Step 7: Verify reconnection
Write-Host "Checking logs for successful reconnection..." -ForegroundColor Yellow
# Check logs for "Connection successful" within last 15 seconds

Write-Host "Test complete! Check logs to verify reconnection occurred without service restart." -ForegroundColor Green
```

---

## Log Analysis

### Success Indicators

Look for these patterns in the logs:

```
✅ "Attempting to connect to host:port (5s timeout)..."
✅ "Connection successful. Backoff delay reset."
✅ "Connection ended naturally. Cleaning up resources..."
✅ "Socket cleaned up."
✅ "Resources cleaned up. Reconnection will be attempted with backoff."
```

### Failure Indicators

Watch for these problems:

```
❌ Connection attempts taking > 5 seconds
❌ "Connect() skipped - backoff active" continuing after remote is up
❌ No "Connection successful" after remote comes back up
❌ Errors about socket already in use
❌ Resource leak warnings
```

---

## Troubleshooting

### Issue: Engine still not reconnecting after fix

**Possible Causes:**
1. Fix not properly applied - verify code changes
2. Service not restarted after fix - restart service
3. Firewall blocking connections - check firewall rules
4. Remote system actually still down - verify remote is accessible

**Diagnostic Steps:**
1. Check engine logs for timeout messages
2. Verify connection timeout is 5 seconds (check log timestamps)
3. Test connection manually: `Test-NetConnection -ComputerName host -Port port`
4. Check Windows Event Viewer for network errors

### Issue: Connection timeout too short/long

**Possible Causes:**
1. Code change not applied correctly
2. Different code path being used

**Diagnostic Steps:**
1. Verify line 257 in Devs.vb uses CancellationToken with 5-second timeout
2. Check logs for "(5s timeout)" in connection attempt messages
3. Time the actual connection attempts with stopwatch

---

## Success Criteria Summary

The fix is considered successful if:

1. ✅ Engine reconnects within 5-10 seconds after extended downtime (hours)
2. ✅ No service restart required for reconnection
3. ✅ Connection attempts timeout consistently at 5 seconds
4. ✅ Backoff resets to 5 seconds after successful connection
5. ✅ No resource leaks during extended testing
6. ✅ Multiple devices reconnect independently
7. ✅ Clear timeout error messages in logs

## Conclusion

The most critical test is **Test 2: Extended Downtime**. This directly validates the bug fix. If the engine reconnects within 5-10 seconds after hours of downtime WITHOUT requiring a service restart, the fix is successful.