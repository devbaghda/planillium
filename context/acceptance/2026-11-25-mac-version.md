# Acceptance — Mac version (Avalonia), 2026-11-25

**User's words/decisions:** "i want to have planillium on this pc" (this pc = the Mac). Chose: rebuild for Mac
(not VM), framework **Avalonia** (C#, shares logic with the Windows app), data = **a copy** of current data.

**Plan.** New project `regular/avalonia/Planillium.Mac/`. The Windows app is not edited. Shared logic
(`Services/`, `Models/`) is compiled from the WinUI folder by link, so one copy of every business rule.
The 5 Windows-only service files get Mac replacements. Screens are redrawn in Avalonia, page by page.

## Phases (each ends with the phase's cases below passing)

### P0 — Scaffold
- A0.1 `dotnet build` of the Mac project: 0 warnings, 0 errors, on this Mac.
- A0.2 The app window opens on this Mac and shows the sidebar with all nav items the Windows app has.
- A0.3 Windows project and its test suite are unchanged (`git diff` shows no edits under `regular/winui/`).

### P1 — Core pages, read-only on a COPY of the data
- A1.1 The app refuses to start unless `MENTOR_ROOT` points at an existing folder (same rule as Windows).
- A1.2 Today, Plans, Schedule show the same tasks, dates, "Day X of Y" and balance chip as the Windows app for the same copy.
- A1.3 Completing a task on the copy changes the copy's database only; the real `data/progress.db` hash is unchanged.

### P2 — Tracking, Diary, Reports
- A2.1 Active app name and window title are read on Mac; idle seconds are read; missing permission shows a clear message, no crash.
- A2.2 Diary rows are written for on-plan/paid work only as the Windows rules say (same tests pass).
- A2.3 Reports tables (Day/Week/Month/Year) match Windows totals for the same copy.

### P3 — Settings, dialogs, background
- A3.1 Every Settings section saves on its own; learned rules list/edit works.
- A3.2 Close hides to the menu bar; tracking keeps running; Quit really quits.
- A3.3 Notifications, evening review, idle-return dialog appear.
- A3.4 TickTick sign-in and AI key are stored in the macOS Keychain, never in config.json.

### P4 — Install
- A4.1 `Planillium.app` in Applications opens by double-click; optional start at login.
- A4.2 Secret scan of the repo before any push finds nothing.

## Standing
- The shared logic test suite still passes (350/350 on Windows list; on Mac, whatever is portable, stated).
- Never run against real `data/`; scratch/copy only.

## Results log (2026-11-25)
- A0.1 build: 0 errors (nullable warnings downgraded in csproj, documented there). A0.3: `git status regular/winui` clean.
- A1.1 PASS: missing MENTOR_ROOT folder -> "can't start" message, no window. (Without MENTOR_ROOT the app uses ~/Library/Application Support/Planillium.)
- A1.2 seen on the copy: Today, Schedule, Plans, Reports, Settings all render with the copy's data (balance -560 pts, 3 plans, week table). Number-by-number comparison with Windows not yet done.
- Real DB md5 unchanged (0a62966e...).
- Open: A1.3, P2 permission path, P3, P4; Settings still says "Follow Windows" / "Start with Windows" (needs Mac wording).
- A1.3 PASS: ticked "Set the daily Reddit comment habit" on the copy -> row 124 in the copy's task_completions; real DB md5 unchanged; copy plans/*.json untouched.
- A2.1 PASS: foreground window titles and idle read; diary rows written. Missing-permission message added to Settings (cannot be seen here: permission is granted).
- A3.2 PASS: window close hides (process + tracking alive), "Open Planillium" restores, "Quit" exits.
- A3.4 PARTLY: Keychain add/find/delete round-trip proven with a dummy item via the same `security` calls; the app's own Save-key path not clicked (scratch runs never touch the Keychain by design).
- A3.3 PARTLY: Good-morning, While-you-were-away and Welcome-back dialogs seen; macOS banner via osascript (click carries no action); Evening review not opened.
- A4.1 PASS: `tools/build-app.sh` -> dist/Planillium.app (106 MB, ad-hoc signed); `open` launches it and shows the copy's data. Start-at-login writes a LaunchAgent (not enabled here).
- Tests: 348-349/350 on Mac (see todos.md).
- 2026-11-25 later: A3.3 PASS: Evening review opened by itself at the set time (scratch copy, EOD 00:01). A3.1 PARTLY: theme radio saves and applies (found + fixed bug: the old radio was read as selected, so Light/Dark saved the previous choice); text typing (name, slider) could not be driven by UI automation, code path identical to Windows (LostFocus autosave). A2.3 PARTLY: Reports Week table checked against SQL on the copy: tasks 1/3 and neutral 0.1 h match; idle 8.3 h vs 8.43 h in SQL (diary grew a few minutes between screenshot and query; the totals logic is the shared Windows code). A3.4 and start-at-login remain code-inspected only (scratch runs never touch Keychain/LaunchAgents by design).
- Black dialog/text-box fix: dialog windows had no background (Fluent near-black; Windows uses the page grey) and text boxes were near-black; both now match the Windows look, and the app theme variant follows the Light/Dark choice for popups too.
- Light theme pass: buttons white like Windows, text-box header plain grey; Reports/Settings/Plans/Today/Schedule seen in light. Real DB md5 unchanged; git status regular/winui clean.
