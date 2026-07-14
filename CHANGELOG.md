# Changelog

Newest first. This tracks the WinUI app (`winui/MentorOverseer.App`) — the one app
going forward; the original Python/Tkinter version is retired.

## Unreleased

**Scheduling & scoring**
- Fixed a bug where completing a task today, then pulling a future task to
  today ("get a head start on tomorrow"), silently unmarked today's already-
  completed task and pushed it to tomorrow instead
- "Get a head start on tomorrow?" now offers as soon as any task today is
  done, not only once every task today is cleared
- Pulling a future task to today no longer leaves a dead, empty day behind
  it — if that was the day's only task, everything after it shifts back
  one day to close the gap, so finishing early actually shortens the plan
- Added a multi-task bonus: each task completed beyond the first one on the
  same day now earns extra points on top of the flat per-task credit,
  shown as its own line in the evening review breakdown
- "Reschedule…" (pick any day; whatever's already on that day, and
  everything after it, shifts forward by one) is now available on every
  open task on the Schedule page — previously only overdue tasks had it;
  today's and future tasks were stuck with no way to pick a specific day
- "Replan all overdue" no longer auto-spreads every overdue task across
  the coming days for you — it now shows one date-picker per overdue
  task in a single dialog, so you choose exactly which day each one lands
  on before anything is saved
- The Plans page now shows each plan's originally-due date alongside how
  many days it's since drifted later (from reschedules or excluded days)
  or earlier (from finishing tasks ahead of schedule) — the original date
  never moves once a plan is created, so this is a running "am I still on
  track" readout, not a re-estimate
- The same drift readout now also appears as a short note in the left
  sidebar for any plan that's currently off its original date, so it's
  visible without opening the Plans page

**Reports**
- Fixed the diary list's column width visibly resizing depending on that
  day's content — the content column is now sized deterministically from
  the page's own scroll viewport rather than its content, so it holds
  steady across empty and busy days alike; long descriptions truncate with
  the full text available on hover
- Added a date picker to the diary header to jump straight to a specific
  day instead of stepping through one at a time
- Reports now cover the actual calendar period rather than a rolling
  window: the weekly view is this Monday through Sunday, the monthly view
  is this calendar month, and the yearly view is this year — so a report
  opened on Monday shows Monday's data alone instead of dragging in the
  tail of the previous week
- "Time by app" now shows your three biggest time sinks by default, with a
  "Show more" link that expands the full list on demand
- "Time by app" bars now show a fourth color for time you've manually
  marked as paid work, instead of the bar quietly falling short of its
  own total-minutes label
- The diary search box now waits for a brief pause in typing before
  searching, instead of re-querying on every keystroke
- Selecting "Year" on a brand-new install now shows "No activity logged
  yet" instead of a bare, unexplained empty table

**Activity tracking**
- Your recurring days off are no longer tracked. On any weekday you've
  excluded from your plan, the diary stays blank and no focus nudges fire —
  the tracker simply rests with you
- You're now asked "where have you been?" whenever you return from being
  away, at any hour — previously the question only appeared between 6am and
  8pm, so evening returns went unrecorded
- If you finish and step away before your end-of-day, the evening review
  now reconciles that unaccounted stretch — asking where you were — before
  closing the day, so early finishes don't vanish into an unlabelled gap
- The morning "start your day" prompt, the "where have you been?" prompt,
  and now the evening review too all arrive as a tray notification
  whenever the app window isn't actually on screen (hidden to the tray, or
  minimized), instead of silently opening inside a window nobody's looking
  at; clicking the notification brings the app forward and opens the same
  interactive dialog as before

**Startup**
- Fixed the window opening partially off-screen (or oversized) when a
  previous session's saved size/position no longer fit the current
  monitor — size and position are now both clamped to the current
  display's work area on launch
- If the app can't find its data folder at startup, it now shows a plain
  error message and exits cleanly instead of leaving an invisible, stuck
  process running with no window and no way to reach it

**Reliability & fixes**
- The morning "start your day" prompt no longer marks itself as shown the
  instant a notification is *sent* — it's only recorded once you've
  actually opened the real prompt, so a missed notification doesn't
  silently skip the whole day's kickoff
- Fixed a rare timing issue where the background tracker and the evening
  review could read the same in-progress information at the same instant,
  occasionally showing a stale "Day off" status or an unnecessary "where
  were you" prompt for time already accounted for
- Exported CSV reports now escape any tracked text that starts with a
  formula character, closing the same spreadsheet-safety gap the HTML
  export already had
- "Clear activity history" now offers to delete those saved report/export
  files in the same action, instead of only warning you they still exist
- "Show more apps" now reads correctly whether there's one hidden app or
  several
- Skipped "where have you been?" answers no longer occasionally show up
  as a suggested quick-reply chip
- Saving which weekdays a plan excludes now tells you if it fails to
  save, instead of closing as if it worked
- The evening review could occasionally pop up a duplicate of itself if
  left open for more than a minute — fixed
- Fixed a mismatched card background on the Reports page's reflection box
  when your in-app theme differs from your Windows theme
- Exported CSV reports now also escape a couple of less-common
  spreadsheet-formula characters the earlier fix missed

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
