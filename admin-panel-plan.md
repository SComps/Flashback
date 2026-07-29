# Admin Panel & Retry Fix — Plan

## Overview

Add an HTTP administration panel to the Flashback Engine's existing web server (`WebWorker`).
The panel lists all defined printers, shows their live connection status, and provides
Start / Stop actions for each, plus engine-level Stop and Restart controls. Authentication
mirrors the 3270 configuration: if `syspw.txt` is present the panel requires HTTP Basic Auth
(`admin` / `<syspw>`); if the file is absent the panel is open with no authentication.

The same work also fixes the printer retry bug: after a disconnection, the engine currently
waits up to 30 seconds before attempting to reconnect. The fix introduces an aggressive
short-interval retry phase (every 5 seconds for the first 2 minutes) followed by the
existing 30-second steady-state interval.

**Scope** — Engine only (`Flashback.Engine`, `Flashback.Core`). No changes to the Spooler,
port-9100 listeners, or any configuration front-end.

---

## Sub-Tasks

---

### Sub-Task 1 — Introduce `PrinterRegistry` shared-state singleton

**Status:** [ ] pending

**Intent**
`Worker` owns the live `_devList` and `WebWorker` is a separate `BackgroundService` with no
reference to it. A tiny singleton registered in DI gives both services a common view of the
printer list without coupling them directly.

**Expected Outcomes**
- A new class `PrinterRegistry` exists in `Flashback.Engine/`.
- `PrinterRegistry` exposes a thread-safe snapshot method (`GetSnapshot()`) and methods
  `Register` / `Unregister` that `Worker` calls.
- `Program.vb` registers it as a singleton before both hosted services.
- `Worker` receives it via constructor injection and calls `Register`/`Unregister` as devices
  are added to or removed from `_devList`.
- `WebWorker` receives it via constructor injection (used in Sub-Task 3).

**Todo List**
1. Create `Flashback.Engine/PrinterRegistry.vb` — a `Public Class PrinterRegistry` with:
   - `Private ReadOnly _lock As New Object`
   - `Private ReadOnly _devices As New List(Of Devs)`
   - `Public Sub Register(d As Devs)` — adds under lock
   - `Public Sub Unregister(d As Devs)` — removes under lock
   - `Public Function GetSnapshot() As IReadOnlyList(Of Devs)` — returns shallow copy under lock
2. In `Program.vb`, add `builder.Services.AddSingleton(Of PrinterRegistry)()` before the
   hosted services.
3. In `Worker.vb`, add `PrinterRegistry` constructor parameter; call `Registry.Register(d)`
   wherever a device is added to `_devList`, and `Registry.Unregister(dev)` in
   `OnDeviceDisconnected` and anywhere a device is removed from `_devList`.
4. In `WebWorker.vb`, add `PrinterRegistry` constructor parameter (no further use yet —
   consumed in Sub-Task 3).

**Relevant Context**
- `Flashback.Engine/Program.vb` — DI registration (lines 77–83)
- `Flashback.Engine/Worker.vb` — `_devList` mutations in `LoadDevices` (lines 135–275),
  `OnDeviceDisconnected` (lines 114–120), `CleanupDevices` / `Cleanup` (lines 327–376)
- `Flashback.Engine/WebWorker.vb` — constructor (lines 16–22)
- `Flashback.Core/Devs.vb` — `Connected`, `Connecting`, `DevName`, `DevDescription` properties

---

### Sub-Task 2 — Fix printer retry logic

**Status:** [ ] pending

**Intent**
After a device disconnects, `RecreateDisconnectedDevices()` runs every 30 seconds. If the
remote host is temporarily unreachable the engine stays silent for up to 30 seconds between
each attempt with no escalation. The fix adds an aggressive short-poll phase immediately
after any disconnection.

**Expected Outcomes**
- A new field `_lastDisconnectTime As DateTime` (or similar) in `Worker` tracks when the
  most-recent disconnection occurred.
- For 2 minutes after the last disconnection, `RecreateDisconnectedDevices()` is called
  every 5 seconds instead of waiting for the 30-second main loop.
- After 2 minutes with no new disconnection the cadence returns to 30 seconds.
- All retry attempts are logged so the operator can see activity in `printers.log`.

**Todo List**
1. Add `Private _lastDisconnectTime As DateTime = DateTime.MinValue` field to `Worker`.
2. In `OnDeviceDisconnected`, set `_lastDisconnectTime = DateTime.Now` after removing the
   device from `_devList`.
3. Add a new `WithEvents _retryTimer As System.Timers.Timer` with a 5-second interval,
   enabled only while `DateTime.Now - _lastDisconnectTime < TimeSpan.FromMinutes(2)`.
4. In the `_retryTimer.Elapsed` handler, call `RecreateDisconnectedDevices()` and disable
   the timer once `DateTime.Now - _lastDisconnectTime >= TimeSpan.FromMinutes(2)`.
5. Start `_retryTimer` inside `OnDeviceDisconnected` (enable it when a disconnect occurs).
6. Dispose `_retryTimer` in `Cleanup()` alongside the existing timers.
7. Add a log line in `RecreateDisconnectedDevices()` when it finds a device to recreate,
   noting whether it is in the aggressive or steady-state phase.

**Relevant Context**
- `Flashback.Engine/Worker.vb` — `RecreateDisconnectedDevices` (lines 60–108),
  `OnDeviceDisconnected` (lines 114–120), `Cleanup` (lines 340–376), timer setup (lines 30–40)

---

### Sub-Task 3 — Add `/admin` routes and HTML panel to `WebWorker`

**Status:** [ ] pending

**Intent**
Add the admin panel to the existing HTTP server. The panel reads printer status from
`PrinterRegistry` and drives connect/disconnect actions by writing to `commands.dat`
(the existing mechanism already polled by `Worker` every 500 ms).

**Expected Outcomes**
- `GET /admin` — returns a styled admin panel HTML page listing all printers defined in
  `devices.dat` (including disabled ones) with their live status and Start/Stop buttons.
- `POST /admin/action?cmd=connect&dev=NAME` — writes `CONNECT||NAME` to `commands.dat`
  and redirects back to `/admin`.
- `POST /admin/action?cmd=disconnect&dev=NAME` — writes `DISCONNECT||NAME` to `commands.dat`
  and redirects back to `/admin`.
- Authentication: if `syspw.txt` is present in the base directory, the two `/admin` routes
  require HTTP Basic Auth with username `admin` and the syspw as password. If the file is
  absent, requests pass through unauthenticated.
- The HTML design is consistent with the existing spool management pages: same IBM Carbon
  CSS from `WebAssets.Css`, same header structure, same `.section` / `.file-card` /
  `.btn btn-primary` / `.btn btn-secondary` patterns.
- Printer status is shown with a coloured badge: green = Connected, yellow = Connecting,
  red = Disconnected.
- Printers listed in `devices.dat` but not currently in `PrinterRegistry` (i.e. disabled
  or not yet started) are shown with status Disabled / Stopped.

**Todo List**
1. In `WebWorker.vb`, add a helper `Private Function ReadSyspw() As String` that reads
   `syspw.txt` from `AppDomain.CurrentDomain.BaseDirectory` the same way the 3270 config
   does; returns `String.Empty` if the file does not exist.
2. Add a helper `Private Function IsAdminAuthorized(context As HttpListenerContext) As Boolean`
   that: (a) calls `ReadSyspw()`; if empty returns `True`; otherwise checks the
   `Authorization` header for Basic Auth with username `admin` and the syspw.
3. In `ProcessRequest`, add routing for `/admin` (GET) and `/admin/action` (POST) before
   the existing `404` fallback, each guarded by `IsAdminAuthorized`; return a `401` with
   `WWW-Authenticate: Basic realm="Flashback Administration"` header on failure.
4. Add `Private Function GenerateAdminHtml() As String` that:
   - Reads `devices.dat` to get the full configured printer list (name, description,
     connection type, destination, enabled flag).
   - Calls `_registry.GetSnapshot()` to get live status for each printer.
   - Emits a `<!DOCTYPE html>` page using the same CSS (`WebAssets.Css`), same header
     template, and same `.section` / `.file-card` layout as `GenerateHtml`.
   - For each printer row: name, description, destination, status badge, and
     Start (`/admin/action?cmd=connect&dev=NAME`) / Stop
     (`/admin/action?cmd=disconnect&dev=NAME`) buttons as an HTML form POST.
   - Includes an "Engine Controls" section at the top with a **Stop Engine** button
     (`cmd=stop`) and a **Restart Engine** button (`cmd=restart`), visually separated
     from the printer list (e.g. using a distinct section header or a danger-styled button).
5. Add `Private Sub HandleAdminAction(context As HttpListenerContext)` that:
   - Reads `cmd` and `dev` query-string parameters.
   - For `cmd=connect` or `cmd=disconnect`: validates `dev` is non-empty, writes the
     appropriate line to `commands.dat` (appending so concurrent actions are not lost),
     then issues a `302` redirect to `/admin`.
   - For `cmd=stop`: calls `_lifetime.StopApplication()` and returns a plain confirmation
     page ("Engine is shutting down...") — no redirect since the server will stop.
   - For `cmd=restart`: writes a sentinel file `restart.req` to
     `AppDomain.CurrentDomain.BaseDirectory`, then calls `_lifetime.StopApplication()` and
     returns a plain confirmation page ("Engine is restarting...").
6. Wire `_cmdFile` path into `WebWorker` — `commands.dat` in
   `AppDomain.CurrentDomain.BaseDirectory`.
7. Inject `IHostApplicationLifetime` into `WebWorker` via its constructor for Stop/Restart.

**Relevant Context**
- `Flashback.Engine/WebWorker.vb` — `ProcessRequest` (lines 77–200), `GenerateHtml`
  (lines 264–404), constructor (lines 16–22)
- `Flashback.Engine/WebAssets.vb` — CSS (full file, `WebAssets.Css` property)
- `Flashback.Config.3270/Program.vb` — syspw.txt read pattern (lines 47–48)
- `Flashback.Engine/Worker.vb` — `CmdTimer` / `commands.dat` format (lines 398–435)
- `Flashback.Core/Devs.vb` — `Connected`, `Connecting`, `Enabled`, `DevName`,
  `DevDescription`, `DevDest`, `ConnType` properties
- `Microsoft.Extensions.Hosting.IHostApplicationLifetime` — `StopApplication()` method

---

### Sub-Task 4 — Handle restart sentinel in `Program.vb`

**Status:** [ ] pending

**Intent**
`IHostApplicationLifetime.StopApplication()` gracefully shuts the .NET Generic Host down,
but cannot re-launch the process from within the same process (the mutex would block a new
instance). Instead, `Program.vb` checks for a `restart.req` sentinel file after
`engineHost.Run()` returns. If present it deletes the file and re-launches the engine
executable with the original arguments before exiting, releasing the mutex.

**Expected Outcomes**
- After `engineHost.Run()` returns, if `restart.req` exists in the base directory:
  - The file is deleted.
  - A new engine process is started with the same command-line arguments.
  - The current process exits (releasing the mutex so the new instance can acquire it).
- If `restart.req` does not exist, the process exits normally (Stop behaviour).
- The restart works whether the engine is running as a console app, a Windows Service
  managed by SCM, or a Linux systemd unit (note: for Windows Service / systemd, the service
  manager will typically restart the process on its own if configured to do so; the
  sentinel-based self-relaunch is primarily useful in direct/console mode).

**Todo List**
1. After `engineHost.Run()` in `Program.vb`, add a check:
   ```
   Dim restartFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "restart.req")
   If File.Exists(restartFile) Then
       File.Delete(restartFile)
       Dim psi As New ProcessStartInfo(Environment.ProcessPath)
       psi.Arguments = String.Join(" ", args)   ' original args (includes -w port, etc.)
       psi.UseShellExecute = False
       Process.Start(psi)
   End If
   mutex.Dispose()
   ```
2. Ensure `mutex.Dispose()` is called before the process exits in both the restart and
   normal-exit paths so the new instance can acquire the mutex immediately.

**Relevant Context**
- `Flashback.Engine/Program.vb` — mutex setup (lines 11–16), daemon relaunch pattern
  (lines 49–61), `engineHost.Run()` (line 86)

---

---

### Sub-Task 5 — Fix 3270 input field handling

**Status:** [ ] pending

**Intent**
In several screens of the 3270 configuration tool, typing into an input field has no effect —
the value read back by the host is always empty or the original default. This affects the
delete confirmation prompt (typing "Y"), password/hidden fields, and potentially all short
unprotected fields. Two independent bugs cause this:

**Bug A — `ParseInboundFields` never marks normal fields as Modified**
`ScrapeEditFields` calls `GetModifiedFields()` first (line 262 of `SessionManager.vb`).
`GetModifiedFields()` returns only fields where `f.Modified = True`. `ParseInboundFields`
updates `f.Content` when the terminal sends data back, but it never sets `f.Modified = True`.
So `GetModifiedFields()` always returns an empty list for normal fields, which correctly
triggers the full-scrape fallback — but the fallback reads stale `Content` values if the
terminal did not send those fields back (because their MDT was still 0 from the perspective
of the read-modified response filtering done by the terminal firmware). The fix is to have
`ParseInboundFields` set `f.Modified = True` whenever it receives content for a field, so
`GetModifiedFields()` accurately reflects what the terminal actually sent.

**Bug B — Screen layout collision on single-character and short fields**
`ShowScreen` (line 560 of `TN3270Listener.vb`) writes a "terminator" attribute byte at
`(f.Address + f.Length + 1) % ScreenSize` for each field that has no following field at
that position. For a 1-character field like `txtConfirm` (row 14, col 44, length 1), the
terminator is placed at `Address + 2`. However, `WriteText` calls that precede it on the
same row can produce a protected field whose own terminator lands at the same address as
`txtConfirm`'s data cell (address + 1), effectively overwriting the field start. The
terminator check at line 564 (`Fields.FirstOrDefault(Function(other) other.Address = termAddr)`)
only checks against field *attribute* addresses — it does not check whether `termAddr` falls
*inside* another field's data range. If a preceding field's terminator lands at `txtConfirm`'s
attribute address (1082), the SF written there resets the field attribute and makes the
field protected/invisible to the terminal. The fix is to also suppress the terminator when
`termAddr` falls within the address range of any other field (`termAddr > other.Address`
and `termAddr <= other.Address + other.Length`).

**Expected Outcomes**
- Typing "Y" in the delete confirmation field and pressing Enter correctly registers the
  confirmation.
- Password fields on the Add User and Login screens correctly capture typed content and
  return it to the host.
- Short (1–5 character) unprotected fields on all screens accept and return typed input.
- The Edit screen's field scraping correctly identifies which fields were actually changed.
- Hidden/password fields continue to have their MDT pre-armed (existing behaviour preserved).

**Todo List**
1. In `TN3270Listener.vb`, `ParseInboundFields` method (line 262): after setting
   `currentField.Content = ""` in the `seenFields` block, also set `currentField.Modified = True`
   so that `GetModifiedFields()` accurately reflects fields returned by the terminal.
2. In `TN3270Listener.vb`, `ShowScreen` method (lines 560–571): update the terminator
   suppression condition to also suppress when `termAddr` falls within the data range of
   any existing field — i.e. change the check from:
   ```
   Fields.FirstOrDefault(Function(other) other.Address = termAddr)
   ```
   to also exclude cases where `termAddr` is between `other.Address + 1` and
   `other.Address + other.Length` (inclusive) for any field `other`. This prevents
   the terminator from landing inside another field's content area.
3. In `TN3270Listener.vb`, `ClearModifiedTags` method (line 694): after clearing `Modified`,
   re-arm `Modified = True` for any field with `Intensity = TN3270Intensity.Hidden`, so
   that calling `ClearModifiedTags` after a save does not inadvertently break hidden-field
   tracking on the next read cycle (before `ClearFields` rebuilds them).
4. Verify the fix by tracing through the `ShowConfirmDelete` → user types "Y" → Enter →
   `ProcessDeleteInput` → `GetFieldValue("txtConfirm")` code path mentally to confirm
   `Content = "Y"` is returned.

**Relevant Context**
- `Flashback.TN3270Framework/TN3270Listener.vb` — `ParseInboundFields` (lines 246–288),
  `ShowScreen` terminator logic (lines 560–571), `AddField` MDT pre-arm (lines 442–451),
  `ClearModifiedTags` (lines 694–698)
- `Flashback.Config.3270/SessionManager.vb` — `ShowConfirmDelete` (lines 618–628),
  `ProcessDeleteInput` (lines 366–385), `ScrapeEditFields` `GetModifiedFields` path
  (lines 254–295), `ShowAddUser` hidden field (line 848)

---

## Notes for Implementation

- Sub-Tasks proceed in order: 1 → 2 → 3 → 4 → 5. Sub-Task 3 depends on `PrinterRegistry`
  from Sub-Task 1; Sub-Task 4 is independent but logically follows Sub-Task 3; Sub-Task 5
  is entirely independent (different project) and can be done at any point.
- `commands.dat` writes from `WebWorker` should use `File.AppendAllText` (not
  `WriteAllText`) to avoid race conditions with any simultaneous write from another tool.
- The admin panel should list printers from `devices.dat` directly (not only from
  `PrinterRegistry`) so that disabled/stopped printers are visible and can be started.
- No new NuGet packages are required.
- The `-w` / `--web` flag and `FLASHBACK_WEB_PORT` env-var are the only way to enable the
  web server; the admin panel is part of the same server, not a separate process or port.
- For Stop/Restart, the confirmation page returned to the browser should be a complete
  HTML page (same CSS/header) with a short explanatory message — not a bare text response.
- The **Restart Engine** button should be styled with a warning colour (e.g. `#f1c21b`
  text on a white background, or a distinct `btn-warning` class) and the **Stop Engine**
  button should use the error red (`#da1e28`) to make the destructive nature clear.
