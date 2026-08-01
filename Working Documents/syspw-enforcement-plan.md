# Syspw Enforcement Fix Plan

## Top-Level Overview

The system password (`syspw`) gates administrative access across four surfaces:
`Flashback.Engine` (HTTP admin routes), `Flashback.Config.3270` (TN3270 terminal login),
`Flashback.Config.WinUI` (WinUI3 desktop), and `Flashback.Config.WPF` (WPF desktop).

**Four bugs exist:**

1. **Wrong filename** — All surfaces hardcode `"syspw.txt"`. The intended filename is `SYSPW`
   (no extension, case-insensitive). A file named `SYSPW` or `syspw` is never found, silently
   bypassing password enforcement everywhere.

2. **Relative path in WinUI and WPF** — `pwFile` is a bare filename with no path. When these
   apps are launched from a shortcut, service runner, or any working directory other than the
   exe folder, `File.Exists` returns false and `_syspw` stays empty — open access granted even
   though `SYSPW` exists next to the executable.

3. **3270 password field truncated at 8 characters** — The TN3270 login field `txtPw` is
   declared with `length=8`, which means any password longer than 8 characters is silently
   truncated at the terminal before it reaches the comparison. This causes permanent lockout
   or forces users to keep passwords unreasonably short. Maximum password length is standardised
   to **25 characters** across all surfaces.

4. **No `.Trim()` on decoded HTTP password in Engine** — `inputPass` from the decoded Basic
   Auth header is compared without trimming, inconsistent with every other syspw comparison in
   the codebase (all of which trim both sides).

**Approach:**
- Add a single shared `ReadSyspw(baseDir)` function to `Flashback.Core\SecurityUtils.vb`. It
  tries `SYSPW`, `syspw`, `SYSPW.txt`, `syspw.txt` in order, covering the intended name, Linux
  lowercase variant, and both `.txt` fallbacks for existing deployments.
- All four consumers call this shared function, eliminating duplicated file-reading logic and
  fixing bugs 1 and 2 at once.
- The 3270 password field length is raised to 25, fixing bug 3.
- `inputPass` is trimmed in the Engine comparison, fixing bug 4.
- `USER_MANUAL.md` is updated to document `SYSPW` as the canonical filename and 25 as the
  maximum password length.

---

## Sub-Tasks

---

### Sub-Task 1 — Add `ReadSyspw` to `Flashback.Core\SecurityUtils.vb`

**Status:** [ ] pending

**Intent**
Centralise password-file resolution so every consumer has identical lookup behaviour.
Tries `SYSPW`, `syspw`, `SYSPW.txt`, `syspw.txt` (in that order) within the supplied base
directory. Returns `String.Empty` when none exist (open access mode).

**Expected Outcomes**
- `SecurityUtils.ReadSyspw(baseDir As String) As String` is publicly callable from VB.NET and C#.
- A file named `SYSPW`, `syspw`, `SYSPW.txt`, or `syspw.txt` is accepted.
- Returns trimmed content or `String.Empty`.

**Todo List**
1. Open `Flashback.Core\SecurityUtils.vb`.
2. Add the following shared function inside the `SecurityUtils` class (after existing members):
   ```vb
   Public Shared Function ReadSyspw(baseDir As String) As String
       Dim candidates = {"SYSPW", "syspw", "SYSPW.txt", "syspw.txt"}
       For Each name In candidates
           Dim path = Path.Combine(baseDir, name)
           If File.Exists(path) Then
               Return File.ReadAllText(path).Trim()
           End If
       Next
       Return String.Empty
   End Function
   ```

**Relevant Context**
- File: `Flashback.Core\SecurityUtils.vb` — already imports `System.IO`
- All four consumer projects already have a `<ProjectReference>` to `Flashback.Core`

---

### Sub-Task 2 — Update `Flashback.Engine\WebWorker.vb`

**Status:** [ ] pending

**Intent**
Replace the private `ReadSyspw()` function with a call to `SecurityUtils.ReadSyspw()`.
Fix the missing `.Trim()` on `inputPass` at the same time.

**Expected Outcomes**
- The private `ReadSyspw()` function in `WebWorker.vb` is removed.
- `IsAdminAuthorized` calls `SecurityUtils.ReadSyspw(AppDomain.CurrentDomain.BaseDirectory)`.
- `inputPass` is trimmed before the equality comparison.

**Todo List**
1. Delete the private `ReadSyspw()` function (lines 691–701) including its `<summary>` comment.
2. In `IsAdminAuthorized`, change:
   ```vb
   Dim syspw = ReadSyspw()
   ```
   to:
   ```vb
   Dim syspw = SecurityUtils.ReadSyspw(AppDomain.CurrentDomain.BaseDirectory)
   ```
3. Change line 724:
   ```vb
   Return inputUser.Equals("admin", StringComparison.OrdinalIgnoreCase) AndAlso inputPass = syspw
   ```
   to:
   ```vb
   Return inputUser.Equals("admin", StringComparison.OrdinalIgnoreCase) AndAlso inputPass.Trim() = syspw
   ```

**Relevant Context**
- File: `Flashback.Engine\WebWorker.vb`, lines 691–728
- `Flashback.Core` is already referenced by `Flashback.Engine.vbproj`

---

### Sub-Task 3 — Update `Flashback.Config.3270\Program.vb`

**Status:** [ ] pending

**Intent**
Replace the inline `File.Exists` / `File.ReadAllText` block with a call to `SecurityUtils.ReadSyspw()`.

**Expected Outcomes**
- Lines 47–48 are replaced with a single call.
- All four candidate filenames are accepted automatically.

**Todo List**
1. Replace lines 47–48:
   ```vb
   If String.IsNullOrEmpty(syspw) AndAlso System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syspw.txt")) Then
       syspw = System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "syspw.txt")).Trim()
   End If
   ```
   with:
   ```vb
   If String.IsNullOrEmpty(syspw) Then
       syspw = Flashback.Core.SecurityUtils.ReadSyspw(AppDomain.CurrentDomain.BaseDirectory)
   End If
   ```

**Relevant Context**
- File: `Flashback.Config.3270\Program.vb`, lines 47–48
- `Flashback.Core` is already referenced by `Flashback.Config.3270.vbproj`

---

### Sub-Task 4 — Fix 3270 password field length in `Flashback.Config.3270\SessionManager.vb`

**Status:** [ ] pending

**Intent**
The TN3270 login screen password input field is currently declared with `length=8`, which caps
password input at 8 characters at the terminal level — the terminal silently discards any
characters beyond this before sending the field back to the host. This permanently breaks
authentication for any password longer than 8 characters. Raise the field length to 25 to
match the standardised maximum across all surfaces.

**Expected Outcomes**
- The `txtPw` field accepts up to 25 characters.
- The label `"SYSPW ===>"` aligns correctly with the widened field.
- The field still sits on row 12 and does not overflow the 80-column screen (col 36 + 25 = col 61, well within bounds).

**Todo List**
1. In `ShowLogin()`, change the `AddField` call at line 399:
   ```vb
   _session.AddField(12, 36, 8, "", False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtPw", TN3270Intensity.Hidden)
   ```
   to:
   ```vb
   _session.AddField(12, 36, 25, "", False, TN3270Color.White, TN3270Color.Neutral, TN3270Highlight.Underline, "txtPw", TN3270Intensity.Hidden)
   ```

**Relevant Context**
- File: `Flashback.Config.3270\SessionManager.vb`, line 399
- Screen is 80 columns wide; col 36 + length 25 = col 61 — no overflow
- The existing `Chr(0)` strip + `.Trim()` in `ProcessLoginInput` (line 106) is correct and unchanged
- Framework pre-arms `Modified=True` on Hidden fields in `AddField` so the terminal always
  returns the field content in its Read Modified response — no other changes needed

---

### Sub-Task 5 — Update `Flashback.Config.WinUI\MainWindow.xaml.cs`

**Status:** [ ] pending

**Intent**
Replace the bare relative `pwFile` field and its manual `File.Exists` / `File.ReadAllText` call
with `SecurityUtils.ReadSyspw()`, fixing both the wrong filename and missing `BaseDirectory`
anchor simultaneously. Add `MaxLength="25"` to the login `PasswordBox`.

**Expected Outcomes**
- `pwFile` field is removed.
- `LoadSecurity()` calls `SecurityUtils.ReadSyspw(AppDomain.CurrentDomain.BaseDirectory)`.
- Login `PasswordBox` enforces 25-character maximum at the UI level.

**Todo List**
1. In `MainWindow.xaml.cs`, remove the `pwFile` field (line 17).
2. Replace `LoadSecurity()` body:
   ```cs
   if (File.Exists(pwFile)) _syspw = File.ReadAllText(pwFile).Trim();
   ```
   with:
   ```cs
   _syspw = Flashback.Core.SecurityUtils.ReadSyspw(AppDomain.CurrentDomain.BaseDirectory);
   ```
3. In `MainWindow.xaml`, add `MaxLength="25"` to the login `PasswordBox` (`pbLogin`) at line 141:
   ```xml
   <PasswordBox x:Name="pbLogin" Header="Enter Password" PasswordRevealMode="Hidden"
                MaxLength="25" KeyDown="pbLogin_KeyDown"/>
   ```

**Relevant Context**
- Files: `Flashback.Config.WinUI\MainWindow.xaml.cs` lines 17, 115–118; `MainWindow.xaml` line 141
- `Flashback.Core` is already referenced by `Flashback.Config.WinUI.csproj`

---

### Sub-Task 6 — Update `Flashback.Config.WPF\MainWindow.xaml.vb` and `MainWindow.xaml`

**Status:** [ ] pending

**Intent**
Identical to Sub-Task 5 but for the WPF surface.

**Expected Outcomes**
- `pwFile` field is removed.
- `LoadSecurity()` calls `SecurityUtils.ReadSyspw(AppDomain.CurrentDomain.BaseDirectory)`.
- Login `PasswordBox` enforces 25-character maximum at the UI level.

**Todo List**
1. In `MainWindow.xaml.vb`, remove the `pwFile` field (line 8).
2. Replace `LoadSecurity()` body:
   ```vb
   If File.Exists(pwFile) Then _syspw = File.ReadAllText(pwFile).Trim()
   ```
   with:
   ```vb
   _syspw = SecurityUtils.ReadSyspw(AppDomain.CurrentDomain.BaseDirectory)
   ```
3. In `MainWindow.xaml`, add `MaxLength="25"` to `pbLogin` at line 91:
   ```xml
   <PasswordBox x:Name="pbLogin" Padding="10" MaxLength="25" KeyDown="pbLogin_KeyDown"/>
   ```

**Relevant Context**
- Files: `Flashback.Config.WPF\MainWindow.xaml.vb` lines 8, 112–114; `MainWindow.xaml` line 91
- `Flashback.Core` is already referenced by `Flashback.Config.WPF.vbproj`

---

### Sub-Task 7 — Update `USER_MANUAL.md`

**Status:** [ ] pending

**Intent**
Document `SYSPW` as the canonical password filename, `SYSPW.txt` as the accepted alternative,
and state the 25-character maximum password length.

**Expected Outcomes**
- Line 51 clarifies the SYSPW file convention.
- Line 199 references `SYSPW` (not `syspw.txt`) and notes the 25-character limit.

**Todo List**
1. In `USER_MANUAL.md` at line 51, update:
   ```
   - **Security**: Can be protected with a `SYSPW` (System Password).
   ```
   to:
   ```
   - **Security**: Can be protected with a `SYSPW` (System Password). Place a plain-text file
     named `SYSPW` (or `SYSPW.txt`) in the application directory containing the password
     (maximum 25 characters).
   ```
2. At line 199, change:
   ```
   - **SYSPW**: Always set a system password for the 3270 server using the `--password` flag or a `syspw.txt` file.
   ```
   to:
   ```
   - **SYSPW**: Always set a system password using the `--password` flag or a `SYSPW` file
     (also accepted: `SYSPW.txt`) in the application directory. Maximum password length is
     **25 characters**, enforced across all configuration surfaces.
   ```

**Relevant Context**
- File: `USER_MANUAL.md`, lines 51 and 199

---

## Notes

- Password comparisons are case-sensitive in all modules. This is intentional and unchanged.
- Empty / missing file = open access. This is intentional and unchanged.
- `Flashback.Config.Console` does not use syspw — no changes needed there.
- WinUI and WPF `pbSysPw` (Security tab "Update" button) has no click handler — this is
  pre-existing dead UI and is out of scope for this fix.
- The `txtNewPass` field on the 3270 AddUser screen (length=20) is for web user passwords,
  not syspw — it is out of scope for this fix.
