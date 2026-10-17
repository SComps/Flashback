# Config3270 Connection Fix Plan

## Top-Level Overview

After a refactoring of `Flashback.TN3270Framework`, the `NegotiationComplete` and `Disconnected`
events on `TN3270Session` were changed to the standard .NET `EventHandler` delegate type
`(sender As Object, e As EventArgs)`. The `Flashback.Config.3270` callers were not updated
to match, producing compile-time errors that prevent the service from starting — and therefore
prevent any connections from being accepted.

**Scope:** Two small signature fixes in `Flashback.Config.3270`. No changes to the framework.

---

## Sub-Tasks

### Sub-Task 1 — Fix `SessionStateManager.InitSession` signature

**Status:** `[ ] pending`

**Intent**
`InitSession` is wired as a handler for the `NegotiationComplete As EventHandler` event.
`EventHandler` requires `(sender As Object, e As EventArgs)`. The current zero-parameter
`Public Sub InitSession()` does not satisfy that delegate, causing a compile error.

**Expected Outcomes**
- `InitSession` compiles cleanly as an `EventHandler`-compatible handler.
- `AddHandler session.NegotiationComplete, AddressOf stateManager.InitSession` in
  `Config3270Worker` resolves without error.
- On connection, `NegotiationComplete` fires correctly and `InitSession` is invoked.

**Todo List**
1. Open `Flashback.Config.3270/SessionManager.vb`.
2. Change the signature of `InitSession` from:
   ```vb
   Public Sub InitSession()
   ```
   to:
   ```vb
   Public Sub InitSession(sender As Object, e As EventArgs)
   ```
3. No body changes required — the body does not use any parameters.

**Relevant Context**
- File: `Flashback.Config.3270/SessionManager.vb`, line 46
- Wired at: `Flashback.Config.3270/Config3270Worker.vb`, line 50
- Event declaration: `Flashback.TN3270Framework/TN3270Session.vb`, line 83

---

### Sub-Task 2 — Fix `Disconnected` lambda in `Config3270Worker`

**Status:** `[ ] pending`

**Intent**
The `Disconnected As EventHandler` event also requires `(sender As Object, e As EventArgs)`.
The current inline lambda `Sub() _logger.LogInformation(...)` is zero-parameter, causing a
second compile error.

**Expected Outcomes**
- The `Disconnected` handler lambda compiles cleanly.
- Sessions log the disconnect message when the client disconnects.

**Todo List**
1. Open `Flashback.Config.3270/Config3270Worker.vb`.
2. Change line 52 from:
   ```vb
   AddHandler session.Disconnected, Sub() _logger.LogInformation("[ConfigServer] Session {RemoteEndPoint} disconnected.", e.RemoteEndPoint)
   ```
   to:
   ```vb
   AddHandler session.Disconnected, Sub(s, ea) _logger.LogInformation("[ConfigServer] Session {RemoteEndPoint} disconnected.", e.RemoteEndPoint)
   ```
3. Note: the outer variable `e` (from `OnConnection`'s parameter) is already in scope for the
   `RemoteEndPoint` reference, so no further change is needed in the lambda body.

**Relevant Context**
- File: `Flashback.Config.3270/Config3270Worker.vb`, line 52
- Event declaration: `Flashback.TN3270Framework/TN3270Session.vb`, line 81

---

### Sub-Task 3 — Build verification

**Status:** `[ ] pending`

**Intent**
Confirm both projects compile cleanly after the fixes.

**Expected Outcomes**
- `dotnet build` on `Flashback.Config.3270` produces zero errors and zero new warnings.
- The `TN3270Framework` project is unchanged.

**Todo List**
1. Run `dotnet build Flashback.Config.3270/Flashback.Config.3270.vbproj` (or the solution).
2. Confirm zero compile errors.
3. If errors remain, investigate and address before marking done.

**Relevant Context**
- Solution root: `e:\flashback`
