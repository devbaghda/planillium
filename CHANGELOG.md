# Changelog

Newest first. This tracks the WinUI app (`winui/MentorOverseer.App`) — the one app
going forward; the original Python/Tkinter version is retired.

## Unreleased

**Scheduling & scoring**
- Fixed a bug where completing a task today, then pulling a future task to
  today ("get a head start on tomorrow"), silently unmarked today's already-
  completed task and pushed it to tomorrow instead
- Added a multi-task bonus: each task completed beyond the first one on the
  same day now earns extra points on top of the flat per-task credit,
  shown as its own line in the evening review breakdown

**Reports**
- Fixed the diary list's column width visibly resizing depending on that
  day's content — the content column is now sized deterministically from
  the page's own scroll viewport rather than its content, so it holds
  steady across empty and busy days alike; long descriptions truncate with
  the full text available on hover
- Added a date picker to the diary header to jump straight to a specific
  day instead of stepping through one at a time

**Startup**
- Fixed the window opening partially off-screen (or oversized) when a
  previous session's saved size/position no longer fit the current
  monitor — size and position are now both clamped to the current
  display's work area on launch

## v1.0.0 — 2026-07-07

First installable release. The WinUI app now covers everything the Python app did
plus a round of fixes and additions from real daily use:

**Reports**
- Distraction and time-by-app lists now group by application/site instead of one
  row per window title (e.g. every YouTube tab collapses into one "Chrome -
  YouTube" line), formatted as "App - detail" throughout (`Telegram - chat name`,
  `Chrome - YouTube`)
- Day / Week / Month / Year period buttons are back, with matching section titles
- CSV export alongside the HTML weekly report (UTF-8 with BOM, so Cyrillic names
  open correctly in Excel)
- Today's time-diary entries are editable and deletable (start/end/duration/
  category/description) and now list newest-first
- Removed the plan-category tag pill from task rows (was reading as a button)

**Scheduling**
- Schedule rows use real checkboxes, so a task marked done by mistake can be
  unchecked from the schedule view, not just Today
- Overdue tasks can be individually rescheduled to a specific future date via a
  calendar picker, independent of the existing "replan all overdue" bulk action

**Appearance & startup**
- Light / dark / follow-Windows theme choice, light by default
- Window opacity slider (40-100%)
- Rounded-square chip styling throughout, replacing full-oval pills
- Registered for Start with Windows (opt-in, via Settings)
- App icon now actually embeds in the built exe (Explorer/taskbar/shortcuts were
  falling back to a generic icon before this)

**Packaging**
- First versioned, installable release (Inno Setup) — see `release/README.md`
