# Engine Client Mode Critical Bugs Fix Plan

## Date: 2026-06-20

## Expected Behavior

1. **Only create device objects when ENABLED in configuration**
2. **Remove device objects when disabled or removed from configuration**
3. **In CLIENT MODE:** Connect, receive multiple jobs, reconnect on disconnect
4. **All dependent on device being ENABLED**

## Current Broken Behavior

1. ❌ Device objects created even when disabled (wastes resources)
2. ❌ After first job, NO MORE jobs are received
3. ❌ When remote disconnects, Engine does NOT reconnect
4. ❌ Configuration changes not handled properly

## The Fixes

### Fix #1: Proper Device Lifecycle Management

**Location:** [`Worker.vb:LoadDevices()`](Flashback.Engine/Worker.vb:81-237)

**Current Logic Issues:**
- Creates device objects even when disabled
- Doesn't properly handle devices removed from config
- Doesn't properly handle new devices added to config

**New Logic:**

```vb
For Each line In lines
    ' ... parse line ...
    
    Dim devName = p(0)
    Dim newEnabled = If(p.Length >= 13, (p(12) = "True"), True)
    Dim existing = _devList.FirstOrDefault(Function(x) x.DevName.Equals(devName, StringComparison.OrdinalIgnoreCase))
    
    ' CASE 1: Device exists and is being disabled
    If existing IsNot Nothing AndAlso Not newEnabled Then
        _logger.LogInformation("{Dev} is being disabled. Disconnecting and removing.", devName)
        existing.Disconnect()
        _devList.Remove(existing)
        Continue For
    End If
    
    ' CASE 2: Device doesn't exist and is disabled - skip it
    If existing Is Nothing AndAlso Not newEnabled Then
        _logger.LogInformation("{Dev} is disabled in config, skipping creation.", devName)
        Continue For
    End If
    
    ' CASE 3: Device exists and is enabled - check for config changes
    If existing IsNot Nothing AndAlso newEnabled Then
        ' Check if connection-critical settings changed
        Dim needsReconnect = (existing.DevDest <> newDevDest) OrElse
                            (existing.OS <> newOS) OrElse
                            (existing.ConnType <> newConnType)
        
        If Not needsReconnect Then
            ' Update non-critical settings in place
            ' ... update properties ...
            activeDevices.Add(existing)
            _devList.Remove(existing)
            loadedCount += 1
            Continue For
        Else
            ' Connection settings changed - disconnect and recreate
            _logger.LogInformation("Connection settings changed for {Dev}. Recreating...", devName)
            existing.Disconnect()
            Threading.Thread.Sleep(500)
            ' Fall through to create new device
        End If
    End If
    
    ' CASE 4: Create new device (either new or being recreated) - ONLY IF ENABLED
    If newEnabled Then
        _logger.LogInformation("Creating device object for {Dev}.", devName)
        Dim d As New Devs()
        ' ... set all properties ...
        d.Enabled = True  ' We know it's enabled
        
        ' ... setup handlers ...
        d.Connect()  ' Connect immediately since it's enabled
        activeDevices.Add(d)
        loadedCount += 1
    End If
Next

' Clean up devices that are no longer in config
CleanupDevices()
_devList.AddRange(activeDevices)
```

**Key Changes:**
1. Check if device is enabled BEFORE creating it
2. If device exists but is now disabled → disconnect and remove
3. If device doesn't exist and is disabled → skip entirely
4. If device exists and enabled → update or recreate as needed
5. If device doesn't exist and is enabled → create and connect
6. Devices not in config anymore are cleaned up by `CleanupDevices()`

### Fix #2: Client Mode Resource Cleanup

**Location:** [`Devs.vb:StartAsync()`](Flashback.Core/Devs.vb:290-304) - Finally block

**Replace lines 298-304 with:**

```vb
Else
    ' Connection ended naturally - clean up resources to allow reconnection
    Log($"[{DevName}] Connection ended naturally. Cleaning up resources...", ConsoleColor.Cyan)
    
    ' Clean up clientStream
    Try
        If clientStream IsNot Nothing Then
            clientStream.Close()
            clientStream.Dispose()
            clientStream = Nothing
            Log($"[{DevName}] Client stream cleaned up.", ConsoleColor.Gray)
        End If
    Catch ex As Exception
        Log($"[{DevName}] Error closing client stream: {ex.Message}", ConsoleColor.DarkYellow)
        clientStream = Nothing  ' Force null even if cleanup failed
    End Try
    
    ' Clean up socket (only in client mode)
    If ConnType <> 3 Then
        Try
            If socket IsNot Nothing Then
                socket.Close()
                socket.Dispose()
                socket = Nothing
                Log($"[{DevName}] Socket cleaned up.", ConsoleColor.Gray)
            End If
        Catch ex As Exception
            Log($"[{DevName}] Error closing socket: {ex.Message}", ConsoleColor.DarkYellow)
            socket = Nothing  ' Force null even if cleanup failed
        End Try
    End If
    
    ' Update connection state
    SyncLock _connectionLock
        IsConnected = False
    End SyncLock
    
    Log($"[{DevName}] Resources cleaned up. Reconnection will be attempted with backoff.", ConsoleColor.Cyan)
End If
```

### Fix #3: Better Cleanup in ReceiveDataAsync

**Location:** [`Devs.vb:ReceiveDataAsync()`](Flashback.Core/Devs.vb:371-380)

**Replace lines 371-380 with:**

```vb
' Clean up resources before exiting
Try
    If clientStream IsNot Nothing Then
        clientStream.Close()
        clientStream.Dispose()
        clientStream = Nothing
    End If
Catch ex As Exception
    Log($"[{DevName}] Error closing client stream: {ex.Message}", ConsoleColor.DarkYellow)
    clientStream = Nothing
End Try

' Only clean up socket in client mode
If ConnType <> 3 Then
    Try
        If socket IsNot Nothing Then
            socket.Close()
            socket.Dispose()
            socket = Nothing
        End If
    Catch ex As Exception
        Log($"[{DevName}] Error closing socket: {ex.Message}", ConsoleColor.DarkYellow)
        socket = Nothing
    End Try
End If
```

## Implementation Steps

1. **Rewrite LoadDevices() logic** (Worker.vb)
   - Handle disabled devices properly (don't create)
   - Handle devices being disabled (disconnect and remove)
   - Handle devices being enabled (create and connect)
   - Handle new devices (create if enabled)
   - Handle removed devices (cleanup via CleanupDevices)

2. **Fix Finally Block** (Devs.vb)
   - Add proper resource cleanup
   - Force variables to Nothing on error
   - Add detailed logging

3. **Improve ReceiveDataAsync Cleanup** (Devs.vb)
   - Better error handling
   - Separate cleanup operations
   - Force variables to Nothing on error

## Testing Plan

### Test 1: Device Lifecycle
- Start with device disabled → verify not created
- Enable device → verify created and connects
- Disable device → verify disconnects and removed
- Re-enable → verify recreated and connects

### Test 2: Configuration Changes
- Add new enabled device → verify created
- Add new disabled device → verify not created
- Remove device from config → verify cleaned up
- Change connection settings → verify reconnect

### Test 3: Multiple Jobs
- Connect to remote
- Send 5 jobs without disconnecting
- Verify all process correctly

### Test 4: Reconnection
- Connect and send job
- Disconnect remote
- Verify reconnects within ~10 seconds
- Send another job
- Verify processes correctly

### Test 5: Stability
- Run for hours
- Send 50+ jobs
- Include disconnects
- Include config changes
- Verify no leaks

## Success Criteria

✅ Disabled devices NOT created
✅ Devices created when enabled
✅ Devices removed when disabled
✅ New devices added properly
✅ Removed devices cleaned up
✅ Multiple jobs process correctly
✅ Reconnection works
✅ No resource leaks
✅ Proper error logging
