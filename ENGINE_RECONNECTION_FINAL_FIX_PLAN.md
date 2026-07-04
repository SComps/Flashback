# Flashback Engine - Final Reconnection Fix Plan

## Date: 2026-07-04

## Problem Summary

After extended downtime (hours/days), the engine continues reporting connection failures even when the remote system is back up. Restarting the service immediately connects successfully.

**Root Cause:** Connection objects and state may persist between attempts, preventing fresh connection attempts.

## Solution Philosophy

**"Destroy and recreate everything on each connection attempt"**

Ensure that each connection attempt is completely fresh, as if the engine were just restarted. No state should persist from previous failed attempts.

## Implementation Plan

### Change 1: Ensure Complete Socket Cleanup Before Creating New One

**File:** `Flashback.Core/Devs.vb`
**Location:** Lines 240-260

**Current Code:**
```vb
Else
    Log($"[{DevName}] DIAGNOSTIC: Creating raw Socket.", ConsoleColor.Cyan)
    socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    
    ' ... configure socket ...
    
    Await socket.ConnectAsync(remoteHost, remotePort)
```

**Problem:** If socket cleanup failed in previous attempt, old socket might still exist.

**New Code:**
```vb
Else
    ' Ensure any existing socket is completely disposed before creating new one
    If socket IsNot Nothing Then
        Try
            socket.Close()
            socket.Dispose()
        Catch
            ' Ignore errors - we're forcing cleanup
        End Try
        socket = Nothing
    End If
    
    ' Ensure any existing client stream is disposed
    If clientStream IsNot Nothing Then
        Try
            clientStream.Close()
            clientStream.Dispose()
        Catch
            ' Ignore errors - we're forcing cleanup
        End Try
        clientStream = Nothing
    End If
    
    Log($"[{DevName}] Creating fresh socket for connection attempt.", ConsoleColor.Cyan)
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

    ' Use explicit 5-second timeout for connection attempt
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
1. **Forces cleanup** of any existing socket/stream before creating new one
2. **Explicit 5-second timeout** prevents hanging on stale network state
3. **Fresh socket every time** - no accumulated state
4. **Clear error messages** - distinguishes timeout from other failures

### Change 2: Update Initial Backoff Delay

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

### Change 3: Update Backoff Reset Value

**File:** `Flashback.Core/Devs.vb`
**Location:** Line 140

**Current Code:**
```vb
_reconnectDelay = TimeSpan.FromSeconds(10)
```

**New Code:**
```vb
_reconnectDelay = TimeSpan.FromSeconds(5)
```

## Why This Fixes the Problem

### The Core Issue

When a connection fails, even though the Finally block cleans up the socket, there could be scenarios where:
1. The cleanup partially fails
2. The socket object exists but is in a bad state
3. Windows has cached "host unreachable" state associated with that socket
4. The next attempt reuses stale state

### The Solution

By **explicitly forcing cleanup** before creating a new socket:
1. Any existing socket is forcibly disposed (even if in bad state)
2. Variables are set to Nothing (null)
3. New socket is created with completely fresh state
4. No possibility of reusing stale socket or stream
5. Each attempt is truly independent

### Why 5-Second Timeout Helps

The explicit timeout with CancellationToken:
1. Prevents relying on system default timeouts
2. Forces fresh connection attempt within 5 seconds
3. Doesn't allow Windows to use cached "unreachable" state
4. Provides consistent, predictable behavior

### Comparison to Service Restart

**Service Restart:**
- New process = all variables are fresh
- No existing socket objects
- Clean network stack state
- Connection succeeds immediately

**Our Fix:**
- Explicitly destroy all socket objects
- Set variables to Nothing
- Create fresh socket
- **Same effect as restart, but without restarting**

## Complete Code Changes

### File: Flashback.Core/Devs.vb

**Change 1 - Line 61:**
```vb
Private _reconnectDelay As TimeSpan = TimeSpan.FromSeconds(5)  ' Changed from 10
```

**Change 2 - Line 140:**
```vb
_reconnectDelay = TimeSpan.FromSeconds(5)  ' Changed from 10
```

**Change 3 - Lines 240-275 (replace entire Else block):**
```vb
Else
    ' Ensure any existing socket is completely disposed before creating new one
    If socket IsNot Nothing Then
        Try
            socket.Close()
            socket.Dispose()
        Catch
            ' Ignore errors - we're forcing cleanup
        End Try
        socket = Nothing
    End If
    
    ' Ensure any existing client stream is disposed
    If clientStream IsNot Nothing Then
        Try
            clientStream.Close()
            clientStream.Dispose()
        Catch
            ' Ignore errors - we're forcing cleanup
        End Try
        clientStream = Nothing
    End If
    
    Log($"[{DevName}] Creating fresh socket for connection attempt.", ConsoleColor.Cyan)
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

    ' Use explicit 5-second timeout for connection attempt
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
    
    OutDest = OutDest.Replace("\"c, Path.DirectorySeparatorChar).Replace("/"c, Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar)
    Try
        If Not Directory.Exists(OutDest) Then
            Log($"[{DevName}] Created output directory {OutDest}", ConsoleColor.Cyan)
            Directory.CreateDirectory(OutDest)
        End If
    Catch ex As Exception
        If Not ex.Message.ToUpper().Contains("PDFSHARP") Then
            Log($"[{DevName}] ERROR creating directory: {ex.Message}", ConsoleColor.Red)
        End If
    End Try
    
    clientStream = New NetworkStream(socket, True)
    Await ReceiveDataAsync(_cancellationTokenSource.Token)
End If
```

## Testing Validation

The critical test is:
1. Remote system down for 2+ hours
2. Remote system comes back up
3. **Engine reconnects within 5-10 seconds WITHOUT service restart**

If this works, the fix is successful.

## Summary

**Three simple changes:**
1. Force cleanup of existing socket/stream before creating new one
2. Add explicit 5-second connection timeout
3. Update backoff delays to 5 seconds

**Result:**
- Each connection attempt is completely fresh
- No stale state persists between attempts
- Behaves as if engine were restarted
- Fixes the reconnection issue after extended downtime

This approach ensures that "once a connection fails, the working client object is destroyed, and on the next attempt it is recreated as if the engine were restarted."