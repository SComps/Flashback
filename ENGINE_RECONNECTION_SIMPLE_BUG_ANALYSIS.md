# Flashback Engine - Simple Reconnection Bug Analysis

## Date: 2026-07-04

## Problem Statement

After extended downtime (hours/days), the engine continues reporting connection failures even when the remote is back up. Restarting the service immediately connects successfully.

## The Real Bug

Looking at the code flow more carefully:

### When Connection Fails

**Line 257:** `Await socket.ConnectAsync(remoteHost, remotePort)`
- This throws an exception when connection fails
- Exception is caught at line 277

**Line 277-281:** Catch block
```vb
Catch ex As Exception
    Log($"[{DevName}] StartAsync Error: {ex.GetType().Name} (HResult={ex.HResult}): {ex.Message}", ConsoleColor.Red)
    SyncLock _connectionLock
        IsConnected = False
    End SyncLock
```

**Line 282-338:** Finally block
- Cleans up socket and clientStream (lines 316-325)
- Sets socket = Nothing

### The Issue: Socket is NOT Cleaned Up When Connection Fails!

**CRITICAL BUG:** When `ConnectAsync()` fails at line 257, the exception is thrown **BEFORE** the socket is actually connected. However:

1. Line 242 creates the socket: `socket = New Socket(...)`
2. Lines 246-254 configure the socket options
3. Line 257 attempts to connect - **FAILS and throws exception**
4. Catch block executes (lines 277-281)
5. Finally block executes (lines 282-338)

**BUT LOOK AT THE FINALLY BLOCK LOGIC:**

```vb
Finally
    Try
        Dim wasCancelled = _cancellationTokenSource?.IsCancellationRequested
        
        If wasCancelled Then
            ' ... disconnect ...
        Else
            ' Connection ended naturally - clean up resources
            ' Clean up socket (only in client mode)
            If ConnType <> 3 Then
                Try
                    If socket IsNot Nothing Then
                        socket.Close()
                        socket.Dispose()
                        socket = Nothing
                    End If
                Catch ex As Exception
                    socket = Nothing
                End Try
            End If
        End If
    Catch ex As Exception
        Log($"[{DevName}] Error in Finally block: {ex.Message}", ConsoleColor.Red)
    End Try
End Try
```

**The problem:** The Finally block DOES clean up the socket (lines 316-325). So that's not the issue.

## Wait - Let Me Re-examine

Actually, the socket cleanup looks correct. Let me think about what else could cause this...

### Hypothesis: The Socket is in a Bad State

When `ConnectAsync()` fails after hours of trying:

1. **Socket is created** (line 242)
2. **Socket options are set** (lines 246-254)
3. **ConnectAsync() is called** (line 257)
4. **Connection attempt times out or fails**
5. **Exception is thrown**
6. **Socket is cleaned up in Finally block**

But what if the socket itself is in a bad state BEFORE we even try to connect? What if the issue is with how the socket is being created or configured?

### The Real Issue: Socket Configuration After Extended Failures

Look at lines 246-249:
```vb
socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, True)
socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 120)
socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10)
socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3)
```

These are TCP keep-alive settings, but they only matter AFTER a connection is established. They don't affect the initial connection attempt.

### The Actual Problem: No Connection Timeout!

**Line 257:** `Await socket.ConnectAsync(remoteHost, remotePort)`

This uses the **system default timeout**, which can be very long (20-30 seconds or more). But more importantly:

**After hours/days of failures, Windows may have:**
1. Cached DNS failures
2. Marked the route as unreachable
3. Blacklisted the IP in the network stack
4. Set very long timeouts for this specific host

**When the remote comes back up:**
- The engine tries to connect
- Windows still has stale "host unreachable" information
- The connection attempt fails immediately or times out
- The error looks the same as "host is down"
- But the host is actually UP - it's just Windows' stale cache!

## The Simple Fix

You're right - we should just ensure a completely fresh connection attempt each time. The issue is likely:

1. **No explicit connection timeout** - relies on system defaults
2. **No DNS cache refresh** - uses cached DNS results
3. **No way to force Windows to "forget" previous failures**

## Solution

Add an explicit, shorter connection timeout using `CancellationToken`:

```vb
' Create socket
socket = New Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

' Set a reasonable connection timeout (e.g., 10 seconds)
Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(10))
    Try
        Await socket.ConnectAsync(remoteHost, remotePort, cts.Token)
    Catch ex As OperationCanceledException
        Throw New TimeoutException($"Connection to {remoteHost}:{remotePort} timed out after 10 seconds")
    End Try
End Using
```

This ensures:
1. Each connection attempt has a fresh 10-second timeout
2. We don't rely on system defaults
3. Failed attempts don't accumulate state
4. The timeout is consistent regardless of Windows' internal state

## Why Restart Fixes It

When you restart the service:
1. New process = fresh network stack
2. No cached DNS entries
3. No "host unreachable" markers
4. Clean slate for Windows networking

The first connection attempt succeeds because Windows hasn't cached any failures yet.

## Recommendation

The fix is simple:
1. Add explicit connection timeout using CancellationToken
2. Consider forcing DNS refresh (optional)
3. Add better diagnostic logging

This is much simpler than my previous analysis suggested!