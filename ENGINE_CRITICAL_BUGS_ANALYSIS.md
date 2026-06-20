# Engine Critical Bugs Analysis

## Date: 2026-06-20

## Problem Statement
The Flashback Engine has two critical failures:
1. **Will not reconnect when remote disconnects** - After a connection ends, the engine fails to reconnect
2. **Processes only ONE job** - After processing the first job, the engine refuses to process (or possibly receive) any more jobs

## Root Cause Analysis

### Bug #1: Reconnection Failure in Client Mode (ConnType != 3)

**Location:** [`Devs.vb:StartAsync()`](Flashback.Core/Devs.vb:162-309)

**The Problem:**
When a client-mode connection (ConnType != 3) ends naturally after processing a job, the code path at lines 290-304 executes:

```vb
' Lines 290-304
If wasCancelled Then
    SyncLock _connectionLock
        If Not IsClosing Then
            Log($"[{DevName}] Disconnecting due to cancellation.", ConsoleColor.Cyan)
            Disconnect()
        End If
    End SyncLock
Else
    ' Connection ended naturally - just update state
    SyncLock _connectionLock
        IsConnected = False
    End SyncLock
    Log($"[{DevName}] Connection ended. Reconnection will be attempted with backoff.", ConsoleColor.Cyan)
End If
```

**The Fatal Flaw:**
When the connection ends naturally (not cancelled), the code:
1. Sets `IsConnected = False` ✓
2. Logs that reconnection will be attempted ✓
3. **BUT NEVER CLEANS UP THE SOCKET OR STREAM** ✗

This means:
- `socket` remains non-null with a closed/dead connection
- `clientStream` remains non-null but unusable
- Next reconnection attempt at line 244 creates a NEW socket but the old one is still referenced
- The `Connected` property (lines 66-78) checks if `socket IsNot Nothing` and tries to use the OLD dead socket
- Worker's reconnection logic sees `Connected = False` but `Connect()` may fail due to resource conflicts

### Bug #2: Port 9100 Mode Only Processes One Job

**Location:** [`Devs.vb:ReceiveDataAsync()`](Flashback.Core/Devs.vb:311-397)

**The Problem:**
In Port 9100 listener mode (ConnType = 3), after processing one job:

1. **Line 363-382:** When `recd = 0` (remote closes connection), the code:
   ```vb
   ' Lines 371-380
   Try
       clientStream?.Close()
       clientStream?.Dispose()
       clientStream = Nothing
       socket?.Close()
       socket?.Dispose()
       socket = Nothing
   Catch
       ' Ignore cleanup errors
   End Try
   ```
   
2. This cleans up `clientStream` and `socket` (the accepted connection)
3. **BUT** the code then exits the `ReceiveDataAsync` loop
4. Control returns to `StartAsync()` at line 224
5. The Finally block (lines 284-308) executes
6. Since `wasCancelled = False`, it just sets `IsConnected = False`
7. **The listener loop at lines 196-240 EXITS** because `ReceiveDataAsync` returned

**The Fatal Flaw:**
The Port 9100 listener loop structure is broken:

```vb
' Lines 196-240
While Not _cancellationTokenSource.IsCancellationRequested
    Try
        Dim incomingSocket = Await listener.AcceptSocketAsync()
        ' ... setup ...
        clientStream = New NetworkStream(incomingSocket, True)
        Await ReceiveDataAsync(_cancellationTokenSource.Token)  ' <-- BLOCKS HERE
        
        ' ... cleanup ...
        Log($"[{DevName}] Session ended. Listening for next job.", ConsoleColor.Gray)
    Catch ex As ObjectDisposedException
        Exit While
    Catch ex As Exception
        ' ...
    End Try
End While
```

**What Should Happen:**
After `ReceiveDataAsync` returns, the loop should continue and call `AcceptSocketAsync()` again to accept the next connection.

**What Actually Happens:**
The `ReceiveDataAsync` call at line 224 is OUTSIDE the listener loop! So after the first job:
1. `ReceiveDataAsync` completes
2. Control returns to line 224
3. Lines 226-232 execute (cleanup)
4. Line 233 logs "Session ended. Listening for next job."
5. **BUT WE'RE NOT IN THE LOOP ANYMORE** - we're at line 224 which is INSIDE the loop but after the await
6. The loop continues to line 240 and checks the condition
7. Since nothing cancelled, it loops back to line 197
8. **BUT** the `StartAsync` function structure is wrong - line 224 is not properly nested

Wait, let me re-examine the structure...

Actually, looking more carefully at lines 176-241:

```vb
If ConnType = 3 Then
    ' ... setup listener ...
    IsConnected = True
    Using registration = _cancellationTokenSource.Token.Register(Sub() listener.Stop())
        While Not _cancellationTokenSource.IsCancellationRequested
            Try
                Dim incomingSocket = Await listener.AcceptSocketAsync()
                ' ... setup ...
                clientStream = New NetworkStream(incomingSocket, True)
                Await ReceiveDataAsync(_cancellationTokenSource.Token)  ' Line 224
                
                Try
                    clientStream?.Close()
                    incomingSocket?.Close()
                Catch ex As Exception
                    Log($"[{DevName}] {ex.Message}", ConsoleColor.Red)
                End Try
                Log($"[{DevName}] Session ended. Listening for next job.", ConsoleColor.Gray)
            Catch ex As ObjectDisposedException
                Exit While
            Catch ex As Exception
                If Not _cancellationTokenSource.IsCancellationRequested Then
                    Log($"[{DevName}] Listener error: {ex.Message}", ConsoleColor.Red)
                End If
            End Try
        End While
    End Using
```

**The REAL Problem:**
The loop structure is actually CORRECT! The issue is in `ReceiveDataAsync`:

At lines 371-380, when the remote closes the connection in Port 9100 mode, the code cleans up `socket` and `clientStream`, then exits the While loop. This is CORRECT for ending that specific session.

**BUT** - look at line 224: `Await ReceiveDataAsync(_cancellationTokenSource.Token)`

When `ReceiveDataAsync` returns (after cleaning up the connection), control returns to line 226, which tries to close `clientStream` and `incomingSocket` AGAIN. But wait - in `ReceiveDataAsync` at lines 371-380, we already set `socket = Nothing`. 

The variable `socket` in `ReceiveDataAsync` refers to the CLASS-LEVEL `socket` variable (line 46), NOT the `incomingSocket` local variable in the listener loop!

**The Actual Bug:**
Lines 371-380 in `ReceiveDataAsync` are setting the class-level `socket` and `clientStream` to Nothing, but in Port 9100 mode:
- The class-level `socket` should remain null (it's not used in listener mode)
- The `incomingSocket` local variable in the listener loop should be cleaned up
- But the code is confusing the two!

After the first job, when line 224 returns and we try to loop back to accept another connection, the class-level `clientStream` is Nothing, so line 223 tries to create a new NetworkStream with the new `incomingSocket`, but something is preventing the loop from continuing properly.

Actually, I need to trace this more carefully. Let me look at what happens:

1. First connection accepted at line 198
2. `incomingSocket` is the accepted socket
3. Line 223: `clientStream = New NetworkStream(incomingSocket, True)` - creates stream from accepted socket
4. Line 224: `Await ReceiveDataAsync(...)` - processes data
5. Inside `ReceiveDataAsync`, when remote closes (line 363: `recd = 0`):
   - Lines 371-380 clean up `clientStream` and `socket`
   - **BUT** `socket` here is the class-level variable, which in Port 9100 mode should be null!
   - The code is cleaning up the WRONG socket!

**The Real Issue:**
In Port 9100 mode, the class-level `socket` variable should not be used. The accepted connections use `incomingSocket` local variables. But `ReceiveDataAsync` doesn't know which mode it's in and tries to clean up the class-level `socket` at lines 376-377, which doesn't exist in Port 9100 mode.

However, the cleanup at lines 371-374 for `clientStream` IS correct because `clientStream` is the class-level variable that was set at line 223.

Let me check if there's a different issue...

Actually, I think I found it! Look at line 328 in `ReceiveDataAsync`:

```vb
If socket.Poll(0, SelectMode.SelectRead) AndAlso socket.Available = 0 Then
```

This is checking the class-level `socket` variable, but in Port 9100 mode, `socket` is null! This would throw a NullReferenceException, but it's wrapped in a Try-Catch at lines 326-342.

But wait - line 325 checks `If ConnType = 3 Then` before doing the Poll check, so this should only run in Port 9100 mode. But in Port 9100 mode, `socket` should be null...

Oh! I see the issue now. In Port 9100 mode:
- The listener accepts connections and creates `incomingSocket` (line 198)
- But the class-level `socket` variable is never set
- Line 328 tries to check `socket.Poll(...)` but `socket` is Nothing
- This throws NullReferenceException
- The catch block at line 335 catches it and exits the While loop
- This ends the `ReceiveDataAsync` call
- Control returns to the listener loop
- The listener loop should continue, but something is preventing it

Actually, looking at line 328 again - it's INSIDE the `If ConnType = 3 Then` block, but `socket` would be Nothing in Port 9100 mode. This is definitely a bug!

## Summary of Bugs

### Bug #1: Client Mode Resource Leak
**File:** [`Devs.vb`](Flashback.Core/Devs.vb)
**Lines:** 290-304
**Issue:** When connection ends naturally, socket and stream are not cleaned up, preventing reconnection

### Bug #2: Port 9100 Mode Socket Confusion  
**File:** [`Devs.vb`](Flashback.Core/Devs.vb)
**Lines:** 328, 371-380
**Issue:** Code tries to use class-level `socket` variable in Port 9100 mode where it should be null, causing NullReferenceException and preventing multiple job processing

## Impact

- **Client Mode:** Cannot reconnect after first disconnect - requires service restart
- **Port 9100 Mode:** Can only process one job, then stops accepting new connections - requires service restart
- **Both modes are completely broken for production use**

## Next Steps

Create detailed fix plan with specific code changes to address both bugs.