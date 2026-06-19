# Flashback.Engine Client Mode Reconnection Fix

## Problem Statement

When Flashback.Engine connects to remote hosts (Client Mode, ConnType != 3), it constantly disconnects and reconnects, leaving old connections open and violating the "one connection per printer" rule.

## Root Cause Analysis - Client Mode Only

### The Flow for Client Mode

**File**: [`Flashback.Core/Devs.vb`](Flashback.Core/Devs.vb)

1. **Connect() called** (line 115)
2. **StartAsync() called** (line 139)
3. **Socket created and connected** (lines 244-259)
4. **ReceiveDataAsync() called** (line 276)
5. **ReceiveDataAsync() loops** waiting for data (lines 314-371)
6. **When data arrives**: Process it, continue looping
7. **When ReceiveDataAsync() exits**: Returns to StartAsync()
8. **StartAsync() Finally block executes** (lines 284-304)
9. **Finally block calls Disconnect()** - THIS IS THE BUG
10. **Worker detects Not Connected** (line 50)
11. **Worker calls Connect()** again
12. **INFINITE LOOP**

### Why ReceiveDataAsync() Exits

Looking at [`ReceiveDataAsync()`](Flashback.Core/Devs.vb:307-381), it exits when:

1. **Line 321**: `clientStream.WriteByte(0)` throws exception (connection check fails)
2. **Line 360**: `recd = 0` (remote closed connection - EOF)
3. **Line 314**: `cancellationToken.IsCancellationRequested` (explicit cancellation)
4. **Lines 374-377**: Any exception occurs

### The Critical Issue

**Line 321**: `clientStream.WriteByte(0)` is a **keep-alive check**

```vb
Try
    If ConnType <> 3 Then 
        clientStream.WriteByte(0)  ' ❌ THIS THROWS EXCEPTION IF CONNECTION LOST
    End If
Catch ex As Exception
    ' Connection closed by peer
    Log($"[{DevName}] {ex.Message}", ConsoleColor.Gray)
    Exit While  ' ❌ EXIT THE LOOP
End Try
```

**What happens**:
1. Remote host is idle (no data to send)
2. ReceiveDataAsync() checks connection with `WriteByte(0)`
3. If remote closed connection OR network issue: Exception thrown
4. Catch block exits the While loop
5. ReceiveDataAsync() returns
6. StartAsync() Finally block executes
7. **Finally block calls Disconnect()** even though connection might still be valid
8. Worker immediately tries to reconnect
9. **New connection created while old one still exists**

### The Real Problem: Line 321 Keep-Alive Check

The `clientStream.WriteByte(0)` is meant to detect dead connections, but:

1. ❌ It's too aggressive - runs every 100ms when no data available
2. ❌ It can throw exceptions for transient network issues
3. ❌ When it throws, ReceiveDataAsync() exits
4. ❌ StartAsync() Finally then calls Disconnect()
5. ❌ Worker immediately reconnects
6. ❌ **Result**: Constant churn

### Why Old Connections Stay Open

**Timing Issue**:
```
T+0ms:   WriteByte(0) throws exception
T+1ms:   Exit While loop in ReceiveDataAsync
T+2ms:   ReceiveDataAsync returns
T+3ms:   StartAsync Finally block starts
T+5ms:   Disconnect() called
T+10ms:  Socket.Close() called
T+50ms:  IsConnected = False
T+100ms: Worker detects Not Connected
T+101ms: Worker calls Connect()
T+102ms: New socket created
T+103ms: New socket.ConnectAsync() called
T+150ms: Old socket still closing
T+200ms: New connection established
```

**Result**: Two connections exist simultaneously - old one closing, new one opening.

## The Fix

### Option 1: Remove WriteByte(0) Keep-Alive (Recommended)

The TCP keep-alive options (lines 248-251) already handle connection detection. The `WriteByte(0)` is redundant and causes problems.

**Change** [`Devs.vb:318-340`](Flashback.Core/Devs.vb:318-340):

```vb
' Keep-alive / check for disconnected
Try
    If ConnType <> 3 Then 
        ' ❌ REMOVE THIS: clientStream.WriteByte(0)
        ' TCP keep-alive options handle connection detection
        ' No need for application-level keep-alive
    Else
        ' Silent connection check for JetDirect using raw Socket Poll
        If socket.Poll(0, SelectMode.SelectRead) AndAlso socket.Available = 0 Then
            If dataBuilder.Length > 0 Then
                ProcessDocumentData(dataBuilder.ToString())
                dataBuilder.Clear()
            End If
            Exit While
        End If
    End If
Catch ex As Exception
    ' Connection closed by peer
    If dataBuilder.Length > 0 Then
        ProcessDocumentData(dataBuilder.ToString())
        dataBuilder.Clear()
    End If
    Log($"[{DevName}] {ex.Message}", ConsoleColor.Gray)
    Exit While
End Try
```

**Better yet, simplify to**:

```vb
' No keep-alive check needed for client mode
' TCP keep-alive options handle connection detection
' Only check for Port 9100 mode
If ConnType = 3 Then
    Try
        ' Silent connection check for JetDirect using raw Socket Poll
        If socket.Poll(0, SelectMode.SelectRead) AndAlso socket.Available = 0 Then
            If dataBuilder.Length > 0 Then
                ProcessDocumentData(dataBuilder.ToString())
                dataBuilder.Clear()
            End If
            Exit While
        End If
    Catch ex As Exception
        If dataBuilder.Length > 0 Then
            ProcessDocumentData(dataBuilder.ToString())
            dataBuilder.Clear()
        End If
        Log($"[{DevName}] {ex.Message}", ConsoleColor.Gray)
        Exit While
    End Try
End If
```

### Option 2: Fix StartAsync() Finally Block (Also Needed)

Even with Option 1, the Finally block should only disconnect on cancellation:

**Change** [`Devs.vb:284-304`](Flashback.Core/Devs.vb:284-304):

```vb
Finally
    Try
        Log($"[{DevName}] StartAsync exiting. Cancellation: {_cancellationTokenSource?.IsCancellationRequested}", ConsoleColor.Cyan)
        
        ' Only disconnect if explicitly cancelled
        If _cancellationTokenSource?.IsCancellationRequested Then
            SyncLock _connectionLock
                If Not IsClosing Then
                    Log($"[{DevName}] Disconnecting due to cancellation.", ConsoleColor.Cyan)
                    Disconnect()
                End If
            End SyncLock
        Else
            ' Connection lost - just update state, don't call Disconnect()
            SyncLock _connectionLock
                IsConnected = False
            End SyncLock
            Log($"[{DevName}] Connection lost, state updated.", ConsoleColor.Cyan)
        End If
    Catch disconnectEx As Exception
        Log($"[{DevName}] Error in Finally: {disconnectEx.Message}", ConsoleColor.Red)
    End Try
End Try
```

### Option 3: Increase Worker Retry Delay

**Change** [`Worker.vb:61`](Flashback.Engine/Worker.vb:61):

```vb
Await Task.Delay(10000, stoppingToken)  ' 10 seconds instead of 5
```

### Option 4: Increase Initial Backoff

**Change** [`Devs.vb:63`](Flashback.Core/Devs.vb:63):

```vb
Private _reconnectDelay As TimeSpan = TimeSpan.FromSeconds(10)  ' 10 instead of 5
```

**And** [`Devs.vb:142`](Flashback.Core/Devs.vb:142):

```vb
_reconnectDelay = TimeSpan.FromSeconds(10)  ' 10 instead of 5
```

## Complete Fix Summary

### Three Changes Required

1. **Remove WriteByte(0) keep-alive** in ReceiveDataAsync() (lines 318-340)
   - Rely on TCP keep-alive options instead
   - Prevents premature connection termination

2. **Fix StartAsync() Finally block** (lines 284-304)
   - Only call Disconnect() on cancellation
   - Don't call Disconnect() when connection naturally ends

3. **Increase retry delays** to 10 seconds
   - Worker.vb line 61: Change 5000 to 10000
   - Devs.vb line 63: Change FromSeconds(5) to FromSeconds(10)
   - Devs.vb line 142: Change FromSeconds(5) to FromSeconds(10)

## Why This Fixes the Problem

### Before Fix
```
[Printer] Attempting to connect to host:9000
[Printer] Connection successful
[Printer] receiving data from remote host
[Printer] received 1024 lines
[Printer] Waiting for next data...
[Printer] WriteByte(0) keep-alive check
[Printer] Exception: Unable to write to stream  ← TRANSIENT ISSUE
[Printer] ReceiveDataAsync finished
[Printer] StartAsync entering Finally
[Printer] Calling Disconnect() from StartAsync Finally
[Printer] Disconnect() starting cleanup...
DIAGNOSTIC: Printer appears offline. Attempting connect...
[Printer] Attempting to connect to host:9000  ← NEW CONNECTION
[REPEATS EVERY 10 SECONDS]
```

### After Fix
```
[Printer] Attempting to connect to host:9000
[Printer] Connection successful
[Printer] receiving data from remote host
[Printer] received 1024 lines
[Printer] Waiting for next data...
[Printer] receiving data from remote host
[Printer] received 512 lines
[Printer] Waiting for next data...
[STAYS CONNECTED - NO RECONNECTION]
```

## Testing Plan

### Test 1: Normal Operation
1. Configure printer in client mode
2. Start Engine
3. Send data from remote
4. Verify data received
5. Wait 5 minutes
6. Send more data
7. Verify no reconnection occurred

**Expected**: Connection stays stable, no disconnect/reconnect

### Test 2: Remote Disconnect
1. Configure printer in client mode
2. Start Engine
3. Establish connection
4. Remote closes connection
5. Verify disconnect detected
6. Verify reconnection with 10s backoff

**Expected**: Clean disconnect, reconnection after 10s

### Test 3: Network Failure
1. Configure printer in client mode
2. Start Engine
3. Establish connection
4. Simulate network failure
5. Verify disconnect detected
6. Restore network
7. Verify reconnection with exponential backoff

**Expected**: Reconnection with 10s, 20s, 40s delays

## Files to Modify

1. **Flashback.Core/Devs.vb**
   - Lines 318-340: Remove WriteByte(0) keep-alive
   - Lines 284-304: Fix Finally block
   - Line 63: Change backoff from 5s to 10s
   - Line 142: Change backoff reset from 5s to 10s

2. **Flashback.Engine/Worker.vb**
   - Line 61: Change retry delay from 5s to 10s

## Estimated Effort

- Implementation: 20 minutes
- Testing: 1 hour
- Total: ~1.5 hours

## Success Criteria

- ✅ Connections stay stable for hours
- ✅ No constant disconnect/reconnect
- ✅ Only one connection per printer to remote
- ✅ Clean disconnect when remote closes
- ✅ Proper reconnection with backoff
- ✅ No "connection refused" or "address in use" errors

## Conclusion

The root cause is the **aggressive WriteByte(0) keep-alive check** in ReceiveDataAsync() combined with the **unconditional Disconnect() call** in StartAsync() Finally block.

Removing the WriteByte(0) check and fixing the Finally block will eliminate the infinite reconnection loop and maintain stable connections to remote hosts.