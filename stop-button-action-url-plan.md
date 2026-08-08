# Plan: Fix Form Action URLs and Printer Name Styling in Web Admin Panel

## Overview

Clicking "Stop" or "Start" on a printer, or clicking "Restart Engine" / "Stop Engine",
submits the form to the wrong URL. The browser navigates to
`http://<host>:<port>/action` instead of `http://<host>:<port>/admin/action`, which
is not a valid route — causing the page to fall through to the root dashboard.

**Root cause:** Every `<form>` in `GenerateAdminHtml()` uses the bare relative URL
`action="action"`. HTML relative-URL resolution strips the last path segment from the
current URL before appending, so from `/admin` the browser resolves it as `/action`.

**The fix:** Change `action="action"` to `action="admin/action"` on every form.
This relative URL resolves from the current URL's directory:

| Accessed at | Directory base | Resolves to |
|---|---|---|
| `/admin` | `/` | `/admin/action` ✓ |
| `/printer/admin` | `/printer/` | `/printer/admin/action` ✓ |
| `http://zospi.nmare.net/printer/admin` | `.../printer/` | `.../printer/admin/action` ✓ |

No host or port is hardcoded; works correctly behind any reverse-proxy prefix.

**Scope:** `Flashback.Engine/WebWorker.vb` and `Flashback.Engine/WebAssets.vb`.

---

## Sub-Tasks

### Sub-Task 1 — Replace `action="action"` with `action="admin/action"` on every form

**Intent**
All 6 forms in `GenerateAdminHtml()` use `action="action"` which misdirects POST
submissions to the wrong URL. Changing every occurrence to `action="admin/action"`
fixes resolution for both the default path (`/admin`) and any reverse-proxy prefix
(`/prefix/admin`). This covers the printer Start/Stop buttons and both Engine Control
buttons.

**Expected Outcomes**
- Clicking "Stop" or "Start" on a printer POSTs to the correct `/admin/action` endpoint.
- "Restart Engine" and "Stop Engine" buttons also POST to the correct endpoint.
- The fix works regardless of hostname, port, or proxy path prefix.
- No other behaviour changes.

**Todo List**
1. In `GenerateAdminHtml()` (~line 847): change the **Restart Engine** form `action=""action""` → `action=""admin/action""`.
2. (~line 855): change the **Stop Engine** form `action=""action""` → `action=""admin/action""`.
3. (~line 929): change the **printer Stop** form (Connected state) `action=""action""` → `action=""admin/action""`.
4. (~line 933): change the **printer Stop** form (Connecting state) `action=""action""` → `action=""admin/action""`.
5. (~line 937): change the **printer Start** form (Disconnected state) `action=""action""` → `action=""admin/action""`.
6. (~line 942): change the **printer Start** form (Stopped/not-in-registry state) `action=""action""` → `action=""admin/action""`.

**Relevant Context**
- `Flashback.Engine/WebWorker.vb` — `GenerateAdminHtml` (~line 735).
- The router matches on `urlPath.EndsWith("/admin/action")` (~line 165), so any path
  ending in `/admin/action` — with or without a proxy prefix — is correctly routed to
  `HandleAdminAction`.
- All 6 occurrences are the string `action=""action""` (VB.NET interpolated string with
  doubled quotes), so a simple find-and-replace across those lines is sufficient.

**Status:** [x] done

---

### Sub-Task 2 — Stop printer name in admin panel from looking like a clickable link

**Intent**
The `.file-name` CSS class is shared between the main dashboard (where it styles actual
`<a>` tags — correct) and the admin panel printer list (where it is applied to a plain
`<span>` — incorrect). Because the class sets `color: #0f62fe` (IBM blue) with an
underline-on-hover rule, the printer name looks like a hyperlink even though nothing
happens when clicked. A new `.file-name-static` class should be added that renders the
same text in plain neutral colour with no hover effect, and used on the admin panel span.

**Expected Outcomes**
- Printer names in the admin panel render in normal body text colour with no underline on hover.
- Printer names in the main dashboard are unaffected (still styled as blue links).
- The change is purely cosmetic.

**Todo List**
1. In `WebAssets.vb`, after the existing `.file-name` / `.file-name:hover` block (~line 145),
   add a `.file-name-static` rule: same font-size and font-weight as `.file-name` but
   `color: #161616` (IBM neutral-100) and no hover styling.
2. In `WebWorker.vb` (~line 952), change the printer name span from
   `class=""file-name""` → `class=""file-name-static""`.

**Relevant Context**
- `Flashback.Engine/WebAssets.vb` — `.file-name` at line 133, `.file-name:hover` at line 142.
- `Flashback.Engine/WebWorker.vb` — the one `<span class=""file-name"">` at line 952.
- Lines 355, 391, 436 in `WebWorker.vb` use `class=""file-name""` on real `<a>` tags —
  these must not be changed.

**Status:** [x] done
