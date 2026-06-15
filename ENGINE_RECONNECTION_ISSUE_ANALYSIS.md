# Flashback.Engine Reconnection Issue Analysis

## Problem Statement

After configuration changes or remote disconnects, Flashback.Engine sometimes has issues reconnecting to remote hosts.

## Root Cause Analysis

### Issue #1: Race Condition in IsClosing Flag

**Location**: [`Devs.vb:373-395`](Flashback.Core/Devs.vb:373)

The `Disconnect()` method sets `IsClosing = True` at the start and resets it to `False` in the Finally block. However, there's a race condition:

```vb
Public Sub Disconnect()
    IsClosing = True  ' ← Set immediately
    Try
        _cancellationTokenSource?.Cancel()
        clientStream?.Close()
        socket?.Close()
        ' ... cleanup ...
    Finally
        IsClosing = False  ' ← Reset in Finally
    End Try
End Sub
```

**Problem**: If `Connect()` is called while `Disconnect()` is still executing, the check at line 98 will block the connection:

```vb
Public Async Sub Connect()
    If IsConnected OrElse IsConnecting OrElse IsClosing Then Exit Sub  ' ← Blocks!
    ' ...
End Sub
```

### Issue #2: IsConnected Not Reset Properly After Disconnect

**Location**: [`Devs.vb:213-221`](Flashback.Core/Devs.vb:213)

In `StartAsync()`, the Finally block calls `Disconnect()` and then sets `IsConnected = False`:

```vb
Finally
    Try
        Disconnect()  ' ← This may take time
    Catch disconnectEx As Exception
        ' ...
    End Try
    IsConnected = False  ' ← Set after Disconnect completes
End Try
```

**Problem**: There's a window where `IsConnected` might still be `True` even though the connection is dead, preventing reconnection attempts.

### Issue #3: Configuration Change Detection Creates New Objects

**Location**: [`Worker.vb:99-111`](Flashback.Engine/Worker.vb:99)

When configuration changes are detected, the logic checks if DevDest or OS changed:

```vb
If existing IsNot Nothing AndAlso existing.DevDest = p(4) AndAlso existing.OS = CType(Val(p(5)), OSType) Then
    ' Reuse existing object
Else
    ' Create new object and destroy old one
End If
```

**Problem**: Even minor config changes (like email settings) that don't affect DevDest or OS will trigger object recreation, which:
1. Calls `Disconnect()` on the old object
2. Creates a new object
3. Calls `Connect()` on the new object

This can fail if the old object's `Disconnect()` hasn't completed, leaving ports/sockets in use.

### Issue #4: No Reconnection Backoff Strategy

**Location**: [`Worker.vb:37-50`](Flashback.Engine/Worker.vb:37)

The main loop attempts reconnection every 5 seconds without any backoff:

```vb
While Not stoppingToken.IsCancellationRequested
    For Each d In _devList
        If d.Enabled AndAlso Not d.Connected AndAlso Not d.Connecting Then
            d.Connect()  ' ← Immediate retry every 5 seconds
        End If
    Next
    Await Task.Delay(5000, stoppingToken)
End While
```

**Problem**: If a connection fails due to temporary issues (network glitch, remote host busy), it immediately retries without giving the system time to recover.

### Issue #5: Socket/Listener Not Fully Cleaned Up

**Location**: [`Devs.vb:373-395`](Flashback.Core/Devs.vb:373)

The `Disconnect()` method doesn't dispose of the `_cancellationTokenSource`:

```vb
Public Sub Disconnect()
    IsClosing = True
    Try
        _cancellationTokenSource?.Cancel()  ' ← Cancels but doesn't dispose
        clientStream?.Close()
        socket?.Close()
        socket = Nothing
        listener?.Stop()
    ' ...
End Sub
```

**Problem**: The `CancellationTokenSource` should be disposed after cancellation to free resources. Not doing so can lead to resource leaks over time.

### Issue #6: StartAsync Finally Block Always Calls Disconnect

**Location**: [`Devs.vb:214-228`](Flashback.Core/Devs.vb:214)

```vb
Finally
    Try
        Disconnect()  ' ← Always called, even if already disconnecting
    Catch disconnectEx As Exception
        ' ...
    End Try
    IsConnected = False
    
    If ConnType = 3 Then
        listener?.Stop()  ' ← Redundant - already done in Disconnect()
        listener = Nothing
    End If
End Try
```

**Problem**: This can cause double-disconnect scenarios where `Disconnect()` is called while it's already running, potentially causing race conditions.

## Reconnection Failure Scenarios

### Scenario 1: Configuration Change During Active Connection

1. Device is connected and processing jobs
2. User changes configuration (e.g., email settings)
3. `Worker.LoadDevices()` detects change
4. Creates new device object, calls `Connect()`
5. Old object's `Disconnect()` is called via `Cleanup()`
6. **Race condition**: New object tries to bind to same port while old object is still cleaning up
7. **Result**: "Address already in use" error, connection fails

### Scenario 2: Remote Disconnect + Immediate Reconnect

1. Device is connected
2. Remote host disconnects (network issue, host restart, etc.)
3. `StartAsync()` Finally block executes, calls `Disconnect()`
4. `IsConnected` set to `False`
5. Main loop detects `Not d.Connected` and calls `Connect()`
6. **Race condition**: `IsClosing` might still be `True` from previous `Disconnect()`
7. **Result**: `Connect()` exits early, no reconnection attempt

### Scenario 3: Rapid Configuration Changes

1. User makes multiple configuration changes in quick succession
2. Each change triggers `LoadDevices()`
3. Multiple `Disconnect()` and `Connect()` calls overlap
4. **Race condition**: Flags (`IsConnected`, `IsConnecting`, `IsClosing`) in inconsistent state
5. **Result**: Device stuck in limbo - not connected but can't reconnect

## Recommended Fixes

### Fix #1: Add Connection State Lock

Add a lock object to synchronize connection state changes:

```vb
Private ReadOnly _connectionLock As New Object()

Public Async Sub Connect()
    SyncLock _connectionLock
        If IsConnected OrElse IsConnecting OrElse IsClosing Then Exit Sub
        IsConnecting = True
    End SyncLock
    
    Try
        ' ... connection logic ...
    Finally
        SyncLock _connectionLock
            IsConnecting = False
        End SyncLock
    End Try
End Sub

Public Sub Disconnect()
    SyncLock _connectionLock
        If IsClosing Then Return  ' Already disconnecting
        IsClosing = True
    End SyncLock
    
    Try
        ' ... disconnect logic ...
    Finally
        SyncLock _connectionLock
            IsClosing = False
            IsConnected = False
        End SyncLock
    End Try
End Sub
```

### Fix #2: Dispose CancellationTokenSource Properly

```vb
Public Sub Disconnect()
    IsClosing = True
    Try
        _cancellationTokenSource?.Cancel()
        _cancellationTokenSource?.Dispose()  ' ← Add disposal
        _cancellationTokenSource = Nothing
        clientStream?.Close()
        socket?.Close()
        socket = Nothing
        listener?.Stop()
        listener = Nothing
    ' ...
End Sub
```

### Fix #3: Add Reconnection Backoff

```vb
Private _lastConnectAttempt As DateTime = DateTime.MinValue
Private _reconnectDelay As TimeSpan = TimeSpan.FromSeconds(5)
Private _maxReconnectDelay As TimeSpan = TimeSpan.FromMinutes(5)

Public Async Sub Connect()
    ' Implement exponential backoff
    Dim timeSinceLastAttempt = DateTime.Now - _lastConnectAttempt
    If timeSinceLastAttempt < _reconnectDelay Then
        Return  ' Too soon to retry
    End If
    
    _lastConnectAttempt = DateTime.Now
    
    If IsConnected OrElse IsConnecting OrElse IsClosing Then Exit Sub
    IsConnecting = True
    
    Try
        SplitDestination(DevDest)
        If remotePort > 0 Then
            Await StartAsync()
            ' Success - reset delay
            _reconnectDelay = TimeSpan.FromSeconds(5)
        End If
    Catch ex As Exception
        ' Failure - increase delay (exponential backoff)
        _reconnectDelay = TimeSpan.FromSeconds(Math.Min(_reconnectDelay.TotalSeconds * 2, _maxReconnectDelay.TotalSeconds))
        Log($"[{DevName}] Connection failed. Next retry in {_reconnectDelay.TotalSeconds:F0} seconds.", ConsoleColor.Yellow)
    Finally
        IsConnecting = False
    End Try
End Sub
```

### Fix #4: Improve Configuration Change Detection

Only recreate device objects when connection-critical settings change:

```vb
' In Worker.LoadDevices()
If existing IsNot Nothing Then
    ' Check if connection-critical settings changed
    Dim needsReconnect = (existing.DevDest <> p(4)) OrElse 
                        (existing.OS <> CType(Val(p(5)), OSType)) OrElse
                        (existing.ConnType <> Val(p(3)))
    
    If Not needsReconnect Then
        ' Just update non-critical settings
        existing.DevDescription = p(1)
        existing.PDF = (p(7) = "True")
        existing.Orientation = Val(p(8))
        existing.OutDest = p(9)
        ' ... update email settings ...
        existing.JobNumber = Val(If(p.Length >= 12, p(11), "0"))
        activeDevices.Add(existing)
        _devList.Remove(existing)
        Continue For
    End If
    
    ' Critical settings changed - need to recreate
    _logger.LogInformation("Connection settings changed for {Dev}. Reconnecting...", existing.DevName)
    existing.Disconnect()
    ' Wait for disconnect to complete
    Await Task.Delay(1000)
End If
```

### Fix #5: Remove Redundant Disconnect Call

```vb
' In StartAsync()
Finally
    Try
        ' Only disconnect if not already disconnecting
        If Not IsClosing Then
            Disconnect()
        End If
    Catch disconnectEx As Exception
        Log($"[{DevName}] Disconnection error: {disconnectEx.Message}", ConsoleColor.Red)
    End Try
    
    IsConnected = False
    
    ' Don't redundantly stop listener - Disconnect() already does this
End Try
```

### Fix #6: Add Connection State Validation

```vb
Public ReadOnly Property CanConnect As Boolean
    Get
        Return Not (IsConnected OrElse IsConnecting OrElse IsClosing)
    End Get
End Property

Public Async Sub Connect()
    If Not CanConnect Then
        Log($"[{DevName}] Connect() skipped. State: Connected={IsConnected}, Connecting={IsConnecting}, Closing={IsClosing}", ConsoleColor.DarkYellow)
        Exit Sub
    End If
    ' ...
End Sub
```

## Testing Strategy

1. **Test Configuration Changes**:
   - Change non-critical settings (email, PDF, orientation) - should not disconnect
   - Change critical settings (DevDest, OS, ConnType) - should disconnect and reconnect cleanly

2. **Test Remote Disconnects**:
   - Simulate network failure
   - Verify reconnection with backoff
   - Ensure no "address in use" errors

3. **Test Rapid Changes**:
   - Make multiple configuration changes quickly
   - Verify no race conditions or stuck states

4. **Test Resource Cleanup**:
   - Monitor for resource leaks over time
   - Verify CancellationTokenSource disposal

## Priority

**HIGH** - This affects core functionality and can leave devices in non-functional states requiring service restart.

## Impact

- Devices fail to reconnect after configuration changes
- Devices fail to reconnect after network issues
- Resource leaks over time
- "Address already in use" errors
- Requires manual service restart to recover