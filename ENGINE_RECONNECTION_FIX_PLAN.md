# Flashback Engine - Reconnection Fix Plan

## Date: 2026-07-04

## Problem Summary

After extended downtime (hours/days), the engine continues reporting connection failures even when the remote system is back up. Restarting the service immediately connects successfully.

**Root Cause:** The `socket.ConnectAsync()` call relies on system default timeouts and may be affected by Windows network stack caching of previous failures.

## Solution: Add Explicit Connection Timeout

Since these are local/known network connections (user-controlled mainframe emulators), we should use a short, explicit timeout of **3-5 seconds**.

## Implementation Plan

### Change 1: Add Connection Timeout to StartAsync()

**File:** `Flashback.Core/Devs.vb`
**Location:** Lines 240-260 (client mode connection)

**Current Code:**
```vb
Else
    Log($"[{DevName}] DIAGNOSTIC: Creating raw Socket.", ConsoleColor.Cyan)
    socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

    ' Configure OS-level Keep-Alives with error handling
    Try
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, True)
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 120)
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10)
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3)
        Log($"[{DevName}] TCP keep-alive configured successfully.", ConsoleColor.Gray)
    Catch ex As Exception
        Log($"[{DevName}] Warning: Could not configure TCP keep-alive: {ex.Message}", ConsoleColor.DarkYellow)
    End Try

    Log($"[{DevName}] Attempting to connect to {remoteHost}:{remotePort} (Socket)...", ConsoleColor.Yellow)
    Await socket.ConnectAsync(remoteHost, remotePort)
    IsConnected = True
    Log($"[{DevName}] Connection successful.", ConsoleColor.Green)
```

**New Code:**
```vb
Else
    Log($"[{DevName}] DIAGNOSTIC: Creating raw Socket.", ConsoleColor.Cyan)
    socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

    ' Configure OS-level Keep-Alives with error handling
    Try
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, True)
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 120)
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10)
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3)
        Log($"[{DevName}] TCP keep-alive configured successfully.", ConsoleColor.Gray)
    Catch ex As Exception
        Log($"[{DevName}] Warning: Could not configure TCP keep-alive: {ex.Message}", ConsoleColor.DarkYellow)
    End Try

    ' Use explicit 5-second timeout for local network connections
    Log($"[{DevName}] Attempting to connect to {remoteHost}:{remotePort} (5s timeout)...", ConsoleColor.Yellow)
    Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(5))
        Try
            Await socket.ConnectAsync(remoteHost, remotePort, cts.Token)
            IsConnected = True
            Log($"[{DevName}] Connection successful.", ConsoleColor.Green)
        Catch ex As OperationCanceledException
            ' Connection timed out after 5 seconds
            Throw New TimeoutException($"Connection to {remoteHost}:{remotePort} timed out after 5 seconds")
        End Try
    End Using
```

**Benefits:**
1. **Explicit 5-second timeout** - doesn't rely on system defaults
2. **Fresh attempt each time** - no accumulated state from previous failures
3. **Fast failure detection** - knows within 5 seconds if remote is down
4. **Appropriate for local networks** - 5 seconds is plenty for LAN connections
5. **Clear timeout errors** - distinguishes timeout from other connection failures

### Change 2: Update Backoff Reset Value

**File:** `Flashback.Core/Devs.vb`
**Location:** Line 140

**Current Code:**
```vb
' Success - reset backoff delay
SyncLock _connectionLock
    _reconnectDelay = TimeSpan.FromSeconds(10)
End SyncLock
```

**New Code:**
```vb
' Success - reset backoff delay
SyncLock _connectionLock
    _reconnectDelay = TimeSpan.FromSeconds(5)
End SyncLock
```

**Rationale:** Since we're using a 5-second connection timeout, the initial backoff should also be 5 seconds for consistency.

### Change 3: Update Initial Backoff Value

**File:** `Flashback.Core/Devs.vb`
**Location:** Line 61

**Current Code:**
```vb
Private _reconnectDelay As TimeSpan = TimeSpan.FromSeconds(10)
```

**New Code:**
```vb
Private _reconnectDelay As TimeSpan = TimeSpan.FromSeconds(5)
```

**Rationale:** Start with 5-second backoff to match the connection timeout.

## Backoff Strategy After Changes

With these changes, the reconnection timing will be:

| Attempt | Backoff Delay | Connection Timeout | Total Time |
|---------|---------------|-------------------|------------|
| 1       | 0s (initial)  | 5s                | 5s         |
| 2       | 5s            | 5s                | 10s        |
| 3       | 10s           | 5s                | 15s        |
| 4       | 20s           | 5s                | 25s        |
| 5       | 40s           | 5s                | 45s        |
| 6       | 80s           | 5s                | 85s        |
| 7       | 160s          | 5s                | 165s       |
| 8+      | 300s (max)    | 5s                | 305s       |

**Key Points:**
- Fast initial attempts (5s, 10s, 15s)
- Exponential backoff prevents connection storms
- Maximum 5-minute delay between attempts
- Each attempt has a fresh 5-second timeout
- Total time to detect "remote is up" = 5 seconds (one attempt)

## Why This Fixes the Problem

### Before Fix:
1. Connection attempt uses system default timeout (20-30+ seconds)
2. Windows may cache "host unreachable" state
3. Subsequent attempts may fail immediately due to cached state
4. No way to force fresh connection attempt
5. Appears as if remote is still down even when it's up

### After Fix:
1. **Explicit 5-second timeout** - fresh attempt every time
2. **CancellationToken** - forces new connection state
3. **Fast failure detection** - knows within 5 seconds
4. **No reliance on system defaults** - consistent behavior
5. **Clear timeout errors** - better diagnostics

### Why Restart Currently Fixes It:
- New process = fresh network stack
- No cached DNS or routing information
- Clean slate for Windows networking
- First attempt succeeds because no cached failures

### Why This Fix Works Without Restart:
- Each attempt creates a new socket with fresh timeout
- CancellationToken forces fresh connection attempt
- 5-second timeout prevents long waits on stale state
- Explicit timeout overrides any system caching

## Testing Plan

### Test 1: Normal Reconnection
1. Start engine with remote system up
2. Verify connection succeeds within 5 seconds
3. Stop remote system
4. Verify engine detects disconnect
5. Start remote system
6. **Verify engine reconnects within 5-10 seconds**

### Test 2: Extended Downtime
1. Start engine with remote system up
2. Stop remote system for 2+ hours
3. Verify engine continues retry attempts with backoff
4. Start remote system
5. **Verify engine reconnects within 5-10 seconds** (not requiring restart)

### Test 3: Connection Timeout
1. Configure firewall to drop packets to remote port
2. Start engine
3. Verify connection attempt times out after exactly 5 seconds
4. Verify error message indicates timeout
5. Remove firewall rule
6. **Verify engine reconnects within 5-10 seconds**

### Test 4: DNS Issues
1. Configure invalid hostname (non-existent)
2. Start engine
3. Verify connection fails within 5 seconds
4. Fix hostname
5. **Verify engine reconnects within 5-10 seconds**

### Test 5: Rapid Reconnection
1. Start engine with remote up
2. Restart remote system multiple times quickly
3. Verify engine reconnects each time
4. Verify no stuck states or resource leaks

## Files to Modify

1. **Flashback.Core/Devs.vb**
   - Line 61: Change initial backoff from 10s to 5s
   - Line 140: Change backoff reset from 10s to 5s
   - Lines 256-259: Add explicit 5-second timeout with CancellationToken

## Backward Compatibility

✅ **Fully backward compatible:**
- No API changes
- No configuration file changes
- Only internal timeout behavior changes
- Faster reconnection is an improvement, not a breaking change

## Performance Impact

✅ **Positive impact:**
- Faster failure detection (5s vs 20-30s)
- Faster reconnection after remote comes back up
- Less time waiting on stale network state
- More predictable behavior

## Risk Assessment

**Risk Level:** LOW

**Risks:**
1. 5-second timeout might be too short for some networks
   - **Mitigation:** 5 seconds is plenty for LAN connections
   - **Mitigation:** User can restart service if needed (same as current workaround)

2. More frequent connection attempts during downtime
   - **Mitigation:** Exponential backoff still in place
   - **Mitigation:** Maximum backoff still 5 minutes

**Benefits:**
1. Fixes critical production issue
2. No restart required after extended downtime
3. Faster reconnection detection
4. More predictable behavior
5. Better diagnostics (timeout vs other errors)

## Implementation Steps

1. ✅ Analyze problem and create fix plan
2. Make code changes to Devs.vb
3. Build and test locally
4. Test with extended downtime scenario
5. Deploy to production
6. Monitor logs for successful reconnections

## Success Criteria

✅ **Fix is successful if:**
1. Engine reconnects within 5-10 seconds after remote comes back up
2. No service restart required after extended downtime
3. Connection attempts timeout consistently at 5 seconds
4. No resource leaks or stuck states
5. Logs show clear timeout vs connection failure messages

## Conclusion

This is a simple, focused fix that addresses the core issue: **lack of explicit connection timeout**. By adding a 5-second timeout with CancellationToken, we ensure fresh connection attempts that aren't affected by Windows network stack caching or stale state.

The fix is:
- ✅ Simple (3 small changes)
- ✅ Low risk
- ✅ Backward compatible
- ✅ Addresses root cause
- ✅ Improves performance
- ✅ Better diagnostics