# Changelog

Newest first. This tracks the WinUI app (`winui/MentorOverseer.App`) — the one app
going forward; the original Python/Tkinter version is retired.

## Unreleased

**New**
- A new end-of-day reminder warns you once, a configurable number of hours before the
  day's review time (2 hours by default, adjustable in Settings), if today's or overdue
  tasks are still open
- Diary entries for a window with a genuinely blank title bar (most commonly the
  desktop itself, briefly focused between switching apps) now show the program's
  name instead of a blank/"-" entry with no information at all
- Fixed File Explorer diary rows always showing bare "File Explorer" with the
  folder/tab name silently dropped, even though it was already being recorded —
  the Reports page's label formatter just didn't know File Explorer was one of
  the apps with a meaningful sub-detail to show (it already handled this for
  Word, Excel, browsers, etc., File Explorer was missing from that list)
- The tray icon now shows a small red dot whenever a notification has fired
  since you last had the app window open — it clears the moment you bring the
  app to the front, the same way most tray-icon badges work

**Diagnostics**
- Added logging around the evening-review prompt and the shared dialog queue
  (`DialogGate`) after a report that the end-of-day review didn't appear at
  all one evening with nothing in the log to explain why — this doesn't fix
  anything by itself, but the next occurrence should leave a clear trail of
  whether the prompt was even attempted and, if so, what blocked it

**Fixes**
- Fixed the recurring bug where a day's activity tracking would silently start
  from whenever the PC was next used instead of the configured day-start time,
  with no idle/gap entry accounting for the missing stretch — a poll that
  resumed after a long gap (e.g. after Windows paused the app's background
  timer overnight) and also had a pending "session was locked" notification
  ran the lock-handling check first, which stamped the gap's start as "right
  now" instead of "whenever the gap actually began" and, as a side effect,
  suppressed the separate check that would have gotten it right. Reordering
  the two checks so the correct one runs first fixes it without changing
  behavior on a normal day

**Reliability & polish**
- Error messages shown in the app (failed saves, exports, or page loads) now
  lead with what to do about it instead of just the raw technical detail —
  the technical detail is still shown alongside it, just not as the whole
  message
- The list of exported files ("Clear activity history" and "Clear all my
  data" both offer to remove `report.html`/`report.csv`/`full-export.json`)
  is now defined once instead of twice, so a future export type can't be
  added to one delete action and forgotten in the other
- Fixed a message that could read as self-contradicting: "Clear activity
  history"/"Clear all my data" no longer say "Couldn't clear..." in the same
  breath as confirming your data actually was cleared and only a leftover
  exported file survived
- Added a "Disconnect TickTick" button in Settings, and folded the same
  cleanup into "Clear all my data" — previously the only way to fully
  remove a saved TickTick connection (client ID, secret, and tokens) was to
  open Windows Credential Manager by hand
- Added a "Your name" field to Settings — previously only settable once, at
  first run
- TickTick sync/connection failures now read in the same voice as every
  other error message in the app
- Reports and exports that need last week's or a whole year's worth of
  streak/task data now reuse work instead of re-querying the database for
  every single day
- For plans with days off (e.g. no weekends), figuring out which calendar
  date a plan-day lands on is now near-instant instead of getting slightly
  slower the longer the plan has been running — it used to check the
  calendar one day at a time from the plan's start date
- "Disconnect TickTick" now shows an error if it doesn't fully succeed,
  instead of silently doing nothing
- The "keep detailed diary" retention setting now has an upper limit, so it
  can't be accidentally set high enough to defeat its own purpose
- "Clear all my data"'s confirmation text now mentions that your name and
  other settings aren't touched by it
- Setting your name in Settings now confirms it saved
- Fixed a rare case where a plan's start date, or the work-hours/day-review
  times, could be misread on a PC set to certain non-English regional
  formats

**Scoring & day-offs**
- A day where every one of your active plans is off (a recurring rest day
  or a day you've marked off) no longer earns or loses any points for
  on-plan/off-plan time, missed tasks, or your streak — it's genuinely a
  day off. If you bring in and finish a task on that day anyway, you still
  get full credit for it
- The same day-off days are now also left out of the weekly/monthly/yearly
  totals, Top Distractions, and Time by App on the Reports page — the
  activity is still tracked and visible in the diary itself, it just
  doesn't skew your totals
- Editing a diary entry's time or category (including bulk-changing
  several at once) now updates that day's score right away, instead of
  leaving a stale number from before the edit
- The "you're off-plan" reminder no longer interrupts you on a day you've
  marked off — tracking still runs normally, you just won't be nagged
- Fixed a bug where a task moved onto a day you'd marked off (e.g. via
  "Move to today") could silently not count toward that day's score at all
- Fixed a bug where editing an old diary entry (recategorizing it, splitting
  it, or bulk-changing several from Reports) could silently erase a streak
  bonus that day had genuinely earned — the recalculation now correctly
  looks at what your streak actually was on that day, not today's
- The evening review's preview of how many points you'll lose for overdue
  tasks now always matches what actually gets recorded, including on a day
  where one of your plans is off but another still has work due

**Privacy & data**
- Added a "Clear all my data" button in Settings that wipes everything the
  app has stored about you in one action — task completions, reschedules
  and day-offs, notes, score history, reflections, TickTick sync links, the
  activity diary, and the debug log. Previously only the activity diary and
  your evening reflections could be cleared; everything else had no delete
  option in the app at all
- Connecting TickTick now tells you up front what gets shared (your task
  titles, projects, and due dates going to and from TickTick's servers)
  instead of only showing the technical setup fields
- The manual's privacy section now actually describes what's tracked
  (window titles, idle-time answers, evening reflections) and how long it's
  kept, instead of two vague sentences
- "Clear all my data" now also removes any report or backup file
  (report.html, report.csv, full-export.json) you'd previously exported to
  your data folder, not just the database — and if one of those files can't
  be removed (e.g. it's open in another program), you're told so instead of
  seeing "cleared" when a copy of your data is still sitting on disk
- The manual now explains the difference between a recurring rest day
  (tracking pauses entirely) and a single day manually marked off (tracking
  keeps running as normal — only the off-plan reminder is silenced)

**Settings**
- Work hours, reminder timing, idle threshold, retention days, and the
  keyword lists now save automatically as you change them, the same way
  Theme and Opacity already did — no more separate "Save settings" click
  that was easy to forget before navigating away

**Accessibility**
- The per-task note box and the diary search box are now properly labeled
  for screen readers — they used to announce as unlabeled edit fields
- The evening review's "what moved the needle today" box is now labeled for
  screen readers too

**Reports**
- The tray icon's status color and the Reports page's colors for the same
  activity type could disagree (most noticeably "Paid" time); both now
  always show the same color for the same thing
- A day off now shows a "Day off" label in the Reports day/week table
  instead of a bare 0m/0m that looked identical to tracking having failed

**Scheduling & scoring**
- Rescheduling an overdue or due task now lets you pick today as the new
  date — it previously only let you pick tomorrow or later, even for a task
  that was already overdue
- Fixed a bug where rescheduling a task, or marking/unmarking a day off,
  could shift another day's task right on top of a day you'd already marked
  off, while a perfectly ordinary working day nearby was left looking empty
  instead
- Undoing "day off" on a day now correctly pulls the task that had rolled
  onto the next day back onto the day you just un-marked, instead of leaving
  it stranded
- Fixed a bug where stepping away from the computer could get double-counted:
  the last few minutes before you were marked idle used to be logged as both
  still on-plan/off-plan *and* as idle, so the same stretch of time (up to
  10 minutes, however long "idle" is set to in Settings) could count toward
  your score twice. On-plan/off-plan time now stops exactly when you
  actually stepped away, not when the app happened to notice
- A plan whose name contains an underscore now imports correctly instead of
  crashing the first time you mark a day off or add a task to it
- Editing a task's personal note now only shows it as saved once the save
  has actually gone through — a failed save no longer looks identical to a
  successful one
- Closing the day ("Evening review") is now one all-or-nothing save, and
  shows a notification if it fails, instead of silently risking a
  half-recorded day with no warning
- Splitting an idle-time gap into several answers, and bulk-marking several
  diary entries at once, are now each one all-or-nothing save too
- Rescheduling a task, adding a task, replanning overdue tasks, and editing
  a diary entry now show the same "couldn't save" warning as everything
  else if the save itself fails, instead of looking identical to clicking
  Cancel
- Splitting a diary entry now shows the same "couldn't save" warning too, if
  the save itself fails — it used to be the one action left that still
  looked identical to clicking Cancel
- Rescheduling a single task now tells you up front that it'll shift
  whatever's already on the target day (and everything after it) forward by
  one, the same disclosure "Replan all overdue" already showed
- Deleting a diary entry now names exactly what's being removed and notes
  that day's score will be recalculated, instead of a bare yes/no prompt
- Archiving, restoring, or adding a plan now updates the sidebar summary
  immediately, and shows a clear error if the file operation itself fails
- Splitting a diary entry into several activities no longer risks losing the
  original if the save is interrupted partway through — it's now one
  all-or-nothing save instead of delete-then-insert-one-at-a-time
- Rescheduling a task, marking a day off, and "replan all overdue" are now
  each one all-or-nothing save too, so an interrupted save can't leave the
  schedule half-shifted
- Fixed a bug where the morning "Good morning" prompt could occasionally
  show twice in a row back to back
- A task checkbox that fails to save now snaps back to its real state
  immediately, instead of sitting there showing the wrong thing until you
  navigate away and back
- "Day off," "Move to today," and "Start now" now show the same "couldn't
  save" warning as a failed task checkbox if the save fails, instead of
  looking identical to a successful click
- The "couldn't save that change" warning on Today and Schedule now clears
  itself once things are working again, instead of staying up indefinitely
  after a single failed save
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
- The same drift readout now also appears as a status line per active plan
  in the left sidebar, without opening the Plans page: green "On track" or
  "Nd ahead of plan" when you're keeping pace or running ahead, red "Nd
  late from plan" when reschedules/excluded days have pushed it behind;
  a long plan name shows its full text on hover, and the Plans page now
  colors "on track" green too, matching the sidebar
- The sidebar's plan status now actually updates after marking a day off,
  moving a task to today, rescheduling, or changing a plan's excluded
  weekdays — it used to only refresh after a few other actions, so it
  could keep showing an out-of-date status after these
- The sidebar now also shows each active plan's currently-expected finish
  date under its drift status, so you can see it at a glance without
  opening the Plans page

**Reports**
- The Day/Week/Month/Year selector now reads correctly to screen readers as
  one set of options ("2 of 4, Week selected") instead of four separate,
  unrelated switches
- The diary's edit/split buttons, and Schedule's move/reschedule/day-off
  links, now announce which specific task or entry they act on to a screen
  reader instead of an identical generic name on every row
- The time diary now updates itself while you're looking at today — new
  entries appear within about 30 seconds as they're tracked, instead of
  only showing up after you leave the page and come back
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
- Diary search now actually reaches every day still on file — it was
  silently excluding the single oldest day within the retention window
- Selecting "Year" on a brand-new install now shows "No activity logged
  yet" instead of a bare, unexplained empty table
- Weekly on-plan/off-plan totals no longer risk quietly coming out wrong on
  a non-English Windows install
- "Time by app" and the time diary now agree on what each color means —
  Paid and Neutral used to be swapped between the two views on the same
  page
- "Time by app" bars now include idle time in their own color, instead of
  a row's bar visibly falling short of its own minutes label
- Loading your TickTick tasks while quickly switching pages (or hitting
  Reconnect) no longer risks showing duplicate or outdated task rows
- The disabled "Restore" button on an archived plan now explains why it's
  disabled, matching "Archive"'s existing tooltip
- Renamed the "SCHEDULE DRIFT" card to "EXCLUSION IMPACT" — it measures
  something different from the Plans page/sidebar's "drift" status (how
  much excluded days have pushed the calendar back, not how the plan's
  finish date compares to its original one), and sharing the word read
  like the two were contradicting each other

**Activity tracking**
- The morning "Good morning" card can now be dismissed with a "Later"
  button — previously it was the only recurring prompt with no way out
  besides "Start the day"
- Fixed a bug where the evening review's automatic pop-up (and its tray
  notification) could silently fail to appear for the rest of the day if
  you'd opened "Evening review" earlier just to check your progress — that
  daytime glance was incorrectly treated the same as the real end-of-day
  offer, so the actual automatic reminder never came
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
- Microsoft Teams is now recognized as a messaging app while you're
  actually using it, not just after the fact in your weekly report
- The "where have you been?" prompt now shows the actual clock time you were
  away (e.g. "10:04–10:46"), not just the number of minutes, so you don't
  have to do the math yourself to place it in your day

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
- Long "now doing" text in the sidebar now ends with "…" when it's cut
  off, instead of stopping mid-word with no sign it was shortened
- The TickTick "Connect" dialog no longer shows your already-saved client
  secret back to you (and WinUI's own "reveal password" button no longer
  exposes it) — leave the field blank to keep the existing one, or type a
  new value to replace it
- Patched a known vulnerability in the bundled SQLite database engine
  (CVE-2025-6965); nothing in this app ever triggered it, but the fix
  costs nothing to take
- A failed TickTick connection no longer writes TickTick's raw response
  text into the local debug log — only the two standard error fields are
  recorded now
- The debug log file is now capped at 5 MB instead of growing forever, and
  Settings now mentions it explains what it does (and doesn't) contain
- Fixed several places where typed times (e.g. saving your work hours, or
  editing a diary entry's start/end time) could be read incorrectly on a
  non-English Windows install
- Fixed a bug in all three "Add Plan" wizard prompt templates where the
  generated JSON's phase names silently went missing on import — the
  templates asked Claude to label each phase with the wrong field name, so
  the phase name never made it into the app

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
