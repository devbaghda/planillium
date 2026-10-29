# Planillium Manual

## Overview

Planillium is a desktop app for managing personal plans, staying accountable, and reviewing progress over time. It is designed for people who want more than a checklist and more structure than a loose habit tracker.

## Who it is for

This app is best for people working on:

- major career goals,
- relocation or life-transition plans,
- study or certification paths,
- personal development challenges,
- or any long-running project that benefits from accountability.

## Core workflow

1. Create or import a plan.
2. Review the daily tasks and plan day.
3. Work through the day while the app tracks your activity.
4. Use notes, rescheduling, and reminders when things change.
5. Review weekly reports and reflection summaries.

## Main features

### Plans

You can keep up to three active plans at once. Have another idea before you're ready to drop one
of the current three? "Add Plan" at the limit offers to save it as a queued idea instead of
turning you away — it shows up under "Queued ideas" on the Plans page, where you can start it
the moment a slot frees up (or delete it if it's no longer worth pursuing). Finishing and
archiving a plan also offers to start one of your queued ideas right there.

**Changing the rest of a plan.** "Replace remaining…" on a plan's card keeps everything you've
already ticked done and replaces all other tasks with a new list: say what should change, copy the
prompt into claude.ai, paste the reply back, check the preview, confirm. The new tasks start after
your last done task (even if that day is already past, in which case they show as overdue), and the finish date and "late from plan" figure restart
from the new list. With an API key set (Settings → AI), an **Ask Claude** button in this dialog does
the copy-and-paste for you and goes straight to the preview; it never writes over text already in
the reply box (clear the box first), and nothing is changed until you confirm. Without a key, or if
the request fails, the manual copy-and-paste still works. While Claude is answering, the dialog shows
how long you have been waiting and how many new tasks have arrived so far; a long plan typically takes
a minute or two, almost all of it Claude writing the reply.

Each plan can include:

- a title,
- phases,
- daily tasks,
- mentor notes,
- briefing context,
- and, per task, the specific apps/tools/websites it actually needs.

When you generate a plan through Claude (the "Add Plan" wizard's prompt templates), Claude is
now asked to name the specific apps/tools/websites each task needs. Right after you import the
plan, those get taught automatically to the same on-plan keyword list Settings' activity
classification rules use — so time spent in them starts counting toward staying on-plan without
you having to add each one by hand. A short message tells you what was learned; you can always
edit or remove any of it afterward from Settings.

A plan you were already partway through before this existed isn't left out: if its tasks carry a
tools list (added by hand or by asking Claude to add one), its card on the Plans page shows a
"Teach on-plan apps…" button — click it anytime to run that same teaching step retroactively.

### Today view

The Today page helps you focus on what matters right now.

It shows:

- the current plan day,
- unfinished tasks,
- overdue work,
- and opportunities to start tomorrow’s work early.

**"Day X of Y" counts progress, not calendar days.** The counter stops at the earliest day
that still has unfinished work on it: miss day 10's task and tomorrow still reads "Day 10",
because you haven't actually finished day 10 yet. Working ahead doesn't move it either — only
closing the day behind it does. To deliberately skip a day's task, reschedule it (Reschedule,
or "Replan all overdue"); that moves the task to its new day and the counter carries on. This
is also why a 28-day plan you're running late on reads "Day 27 of 28" rather than an
impossible "Day 30 of 28". How late you are is reported separately, as the overdue list and
the "Xd late from plan" figure. Today, Plans and Schedule all show this same number.

Getting a head start on tomorrow's work doesn't cost anything from today —
a task finished early stays credited to the day it was actually done on,
and finishing more than one task in a day earns a small bonus on top of the
per-task credit (see the evening review for the breakdown). Pulling a task
forward also compresses the rest of the plan: if that was the only task on
its original day, everything after it moves up to close the gap instead of
leaving an empty day behind.

### Schedule view

The Schedule page gives a structured view of planned work over time.

It supports:

- rescheduling tasks,
- moving tasks to today,
- marking days off,
- and reviewing task details.

Each plan's list of day cards can be collapsed by clicking its header (or with Enter/Space on the
header when it's focused), so a plan you're not focused on right now doesn't take up scroll space.
Click the header again to bring it back.

### Notes and details

Each task can carry personal notes. These are useful for:

- reminders,
- context,
- mini reflections,
- and keeping the plan human rather than mechanical.

### Activity tracking

The app can monitor activity patterns and classify time as:

- on plan,
- off plan,
- neutral,
- idle,
- or paid.

This creates a fuller picture of how time is actually spent.

Tracking rests on your recurring days off. On any weekday you've excluded from your plan, nothing is written to the diary and no focus nudges appear — the tracker treats the whole day as time off.

If you step away from the computer, the app asks "where have you been?" when you return, at any time of day, naming the date and the times you were away. And if you finish and stop before your configured end-of-day, the evening review asks about that unaccounted stretch before it closes the day, so time you spent away is still recorded rather than left blank.

The morning "start your day" prompt and the "where have you been?" prompt only open directly inside the app window when that window is actually visible. If it's hidden in the tray (or minimized), you'll get a tray notification instead — click it to open the app and answer.

### Reports

Reporting summarizes progress and highlights where effort was spent. This helps the user see patterns rather than only isolated task completion.

Reports cover the current calendar period: the weekly view runs from this Monday through Sunday, the monthly view spans this calendar month, and the yearly view spans this year — not a rolling look-back over the last several days.

The Day/Week/Month/Year selector governs everything above the diary: the summary
table, top distractions, time by app, and the insights. The table's Score column shows the points that period's activity
added up to — which is not the same as the BALANCE in the sidebar, since that is a running total
across all time and also subtracts anything you've spent on entertainment time. The table's last two columns, INCOME LOST/GAINED and OF WHICH PAID WORK, show the money side: income lost or gained is based on the potential monthly
income you set in Settings, and the paid-work column is the part of it that came from paid work (already inside the first figure, not added to it). Each row shows its own day, week or month; the Total row shows the figure for the whole period. Finishing plan tasks earns money back: on a day you're counted as losing
income, the share of that day's planned tasks you completed (3 of 4 = 75%) is credited against it.
Top Distractions shows the same per-hour value on each row: that app or
site's hours and its EUR cost, in two aligned columns. The diary section at the
bottom is separate and always works one day at a time. Payable tags (Studioshoo by default, 1,600 €/month) count time tagged with them at the monthly rate ÷ 168 h; add or change them in Settings → Income. Money amounts in Settings → Income are typed with a thin space between thousands (2 925); a comma is the decimal, and a dot followed by exactly three digits counts as thousands (14.000 is 14 000). Money boxes save when you leave them (Tab or click elsewhere) or press Enter, not while you type; pressing Enter saves and takes the cursor out of the box (Tab then carries on from that box). A typed minus sign stays in the box, and a red "Amounts can't be negative." stays under it until you remove the minus; nothing is saved while it shows. Pressing Enter in the "New payable tag" line adds the tag, like the Add button. The new tag needs a name and a monthly amount above 0; otherwise nothing is added and a red message under the line says what is missing. Leaving the previous-salary or a payable-tag box empty puts the saved amount back. Pressing Esc in one of those boxes throws away what you typed, shows the saved amount again and takes the cursor out of the box. Pressing Esc, or clicking anywhere else, leaves the "New payable tag" line without adding anything.

The summary table carries the same columns on every period view: tasks done, one column for each
of the five categories your time is sorted into (on-plan, off-plan, neutral, paid and idle), that
row's own Total, its Score, and the two money columns (income lost/gained and of which paid work). The Total is all five categories added together — so it is all the
time tracked in that day or period, not just the productive-versus-distracted pair; neutral time
(anything that is neither) is usually the largest share of it. The table then ends with a Total row
summing every column, including the score, so it reads in both directions without adding anything
up by hand. The money totals are the period's own figures; a row's cents can differ from the Total by one cent because each row is rounded separately.
Days where every plan is off contribute no time to any of it, matching how they're already excluded
from the rest of Reports, but a task you did complete on a day off still earns its score.

Durations on Reports are shown in decimal hours — "5,5 h", not "5h 30m" — everywhere except the
diary at the bottom, which stays in hours and minutes because a diary entry is a clock event rather
than a quantity you add up. Each figure is rounded to one decimal on its own, so a column of rows
can differ from its total by 0,1: the total is the exact one.

On a narrow window the table scrolls sideways inside its card rather than losing its last columns.
The exported HTML and CSV reports carry the same columns in the same order as the screen.

Time by app is a pie: one slice per app (the eight biggest, the rest folded into "Other"). Click a slice, or its name in the list beside it, to turn it into a pie of what is inside it — an app's pages, or the apps inside "Other" — and use "← Back" to return.

The diary's Category, App, Page and Tag filters are tick-lists: tick several to see them together (the button shows what is ticked), and "Clear filters" unticks everything. The diary section of the Reports page has a date picker in its header for jumping straight to a specific day's activity instead of stepping through one day at a time, bounded to how far back diary history is retained. If you leave the page open on today's diary across midnight, it rolls onto the new day by itself; if you'd navigated to a specific past day, it stays there.

Each of those four filter lists starts with a "-" entry: tick it to see the entries that have no value for that field (never categorized, no tag, no page, no app). It combines with the other ticks and filters. Next to the search box, a chooser sets which field the search looks in: All fields (the default), App, Page, Description, Category or Tag.

**Marking many rows at once.** The Mark buttons under the filters (on-plan, off-plan, neutral, and the tags) work on every entry that matches your current filters and search — not only the ones loaded on screen. If you tick some rows, they work on the ticked rows only. Each button shows how many rows it will touch, e.g. "Mark off-plan (2 401)". Before anything changes, a dialog shows how many rows have no value yet (will be filled), how many have another value (will be overwritten), how many already have it (unchanged), and the total; "Mark N rows" goes ahead, Cancel changes nothing. A backup copy of your data is saved first (the data folder's `backups` subfolder keeps the last 5). Afterwards a message says "Marked N rows as X" with an Undo button. Settings ▸ Change history lists your last 20 bulk changes (kept 30 days), each with its own Undo. Undo puts every row back exactly as it was before that change; rows you changed since (edited, split, re-marked or deleted) are left alone and the message says how many. Rows that no longer match your filters after marking leave the list (the message counts them); your ticks stay until you change them. Marking a category on rows you ticked also teaches the tracker each row's app or page as a keyword; marking all matches without ticking teaches no keywords. Undo forgets the keywords that change taught — keywords you already had are kept, and so is any you changed since in Settings ▸ Activity keywords. Each backup is a full copy of your data, including your diary's full window titles, so keep the data folder private.

The diary list itself only loads a batch of entries at a time (40 up front) rather than the whole
matching history at once, so a long search or a busy day still stays responsive. Under the list,
"Showing N of M" says how many are on screen, and the links reveal more: 50, 100, 500 or 1000 more at a
time, or "Show all". When editing or splitting a diary entry, the description field suggests
your most commonly used past descriptions as you type, so recurring ones don't need retyping — and
your most common ones also show as one-tap buttons above the field, for when you don't want to
type at all. When splitting an entry into several activities, tap fills in whichever row you last
clicked into.

## Settings and configuration

Settings is organised as seven sections — General, Hours & reminders, Scoring, Activity keywords,
Idle-answer library, TickTick, AI, and Data — chosen from the menu down the right-hand side. Each menu
entry shows a summary of what that section currently holds, so you can read your whole
configuration off the menu without opening anything, and go straight to the part you want to
change. The page stays the same size and shape whichever section you're on, and it reopens on the
one you used last. Whether tracking is currently running, and the confirmation (or error) from
your last change, stay visible on every section.

Everything saves as you go — there is no Save button. Working hours and the evening-review time
save the moment you enter a valid value, independently of the rest of the page, so a problem
elsewhere in Settings (an invalid reminder or scoring number, say) never costs you a change you've
already made correctly — you'll see exactly what still needs fixing instead.

The app includes configurable settings for:

- working hours,
- reminder behavior,
- idle thresholds,
- scoring rules,
- themes,
- and activity classification rules.

**Working hours do two jobs.** They're when the app expects you to be on-plan, so that's when
the off-plan reminder can nag you — and they're also the window in which your activity is
recorded at all. Outside them nothing is tracked, and time away isn't counted against you or
back-filled as unaccounted time. So if you want early mornings or evenings on the record, widen
your working hours; if you only want your working day logged, leave them as they are.

**Scoring.** Every rule in the score formula is editable: what a completed task is worth, the
bonus for extra tasks the same day, what a missed task costs, points per hour on-plan and
off-plan, the streak and weekly-comeback bonuses, the worst a single day can score, how many
days an overdue task keeps costing you, the flat fee for replanning all overdue work, where a
"great day" starts, and the comeback window. Changes apply to days scored from then on — days
already closed keep the score they were credited with, and are only recalculated if you edit
that day's diary.

Activity classification rules (ACTIVITY KEYWORDS) match on a window title, so they can be as
specific as you need — "Chrome - LinkedIn" and "Chrome - Synology" can be taught as different
categories even though both are Chrome. Whenever the app teaches a keyword for you automatically
(marking a diary entry on/off-plan, or importing a plan's tools list), it refuses a bare browser
name by itself (just "Chrome", "Edge", etc.) — a browser hosts both on-plan and off-plan content
depending on the tab, so treating the whole browser as one category would misclassify everything
in it. Typing directly into the ACTIVITY KEYWORDS boxes here has no such restriction, since that's
a deliberate manual edit rather than an automatic guess.

A separate end-of-day nudge warns you once, a configurable number of hours before the day's review
time (2 hours by default), if any of today's or overdue tasks are still open — a plain toast
notification, not a dialog, so it doesn't interrupt whatever you're doing. It only fires once a day
and stays silent on a day every relevant plan already has off.

These settings can be adjusted from the app’s Settings area.

### Notifications and the tray icon

Any toast notification the app raises (a reminder, a "welcome back" prompt, the evening review)
leaves the tray icon showing a small red dot until you actually bring the app window to the front —
so a prompt that fired while you were away, or while the window was hidden in the tray, isn't lost
track of. Opening the app clears the dot, the same way most other tray-icon badges work.

## Data and privacy

Planillium stores its data locally on your device, in a SQLite database and a couple of local
JSON files — nothing is sent to a cloud service on your behalf. It's designed to be a personal
tool, not a cloud-first collaboration app. Specifically:

- **Activity tracking.** While tracking is running, the app checks the title of whichever window
  is currently active roughly once a minute, to decide whether that stretch of time was on-plan,
  off-plan, neutral, idle, or paid time. That window title — which can include things like a
  document name, a website title, or a chat preview — is stored verbatim in your local activity
  diary, along with the app name and the time range. This pauses automatically while your PC is
  locked or asleep, and stops the moment you quit the app from the tray. You can also pause it
  in place for a short while without closing the app — right-click the tray icon and choose
  "Pause tracking" (the same menu item then reads "Resume tracking"). It also pauses entirely
  on a recurring rest day (a weekday you've excluded for a plan). A single day manually marked
  "Day off" is different: tracking keeps running as normal on that day — only the off-plan
  reminder is silenced, so the diary can still record what you did without nagging you about it.
- **Evening review and idle answers.** Anything you type into the evening review or an idle-time
  prompt is stored as free text, locally, exactly as you wrote it.
- **Retention.** Diary detail (including window titles) is kept for a configurable number of days
  (90 by default, adjustable in Settings) before being rolled up into daily totals and the
  per-entry detail is discarded. A file you create yourself with "Export all my data" is a
  deliberate snapshot and isn't covered by this — it sits untouched until you delete it, even
  after the data it was taken from has aged out.
- **Export and clearing your data.** Settings has an "Export all my data" action that writes
  everything the app has stored into one file, narrower "Clear" actions for just your activity
  history or just your evening reflections, and a "Clear all my data" action that wipes every
  data table the app keeps (completions, reschedules/day-offs, notes, score history,
  reflections, TickTick sync links, the activity diary, and the debug log) in one go, along with
  any report or export file (report.html, report.csv, full-export.json) sitting in your data
  folder. All three "Clear" actions also delete the automatic backups saved before bulk changes
  (they are full copies of your data), and clearing activity history also clears the change
  history. "Clear all my data" never touches your plan definitions themselves, which are archived or removed from
  the Plans page instead.
- **TickTick.** If you connect a TickTick account, your task titles, project names, and due dates
  are sent to and from TickTick's own servers so the two stay in sync — that's the one place data
  leaves your machine. Credentials for TickTick (and any other connected service) are stored
  securely via the operating system's credential store rather than in plain text.
- **Ask Claude (optional).** If you add an Anthropic API key in Settings → AI, the **Ask Claude**
  buttons in Replace remaining and Add plan send the prompt shown in the dialog — the plan's name,
  its task text and what you typed — to Anthropic and fill in the reply. Your diary, scores,
  earnings, tags and settings are never sent. The key is kept in the Windows credential store, never
  in config.json, and "Remove key" in Settings → AI deletes it. "Clear all my data" does not remove
  the key. The first use shows a one-time notice. Add plan still waits for you to press Import.
  While a request runs you see a live "Waiting m:ss" counter, a "N tasks so far" line once Claude
  starts writing, and a Cancel request button; when it ends the counter stays as "Waited m:ss".

## Tips for getting the most out of it

- Keep plans focused and realistic.
- Use notes sparingly but clearly.
- Review the weekly report instead of only looking at the daily task list.
- Treat the app as a coach, not just a checklist.
- Use the reschedule and day-off features when real life changes the plan.

## Uninstalling

Uninstalling the app deliberately leaves your plans and activity history on disk — the same
way most Windows uninstallers avoid silently destroying your files. If you're uninstalling
because you're retiring or handing off the PC, run "Clear all my data" from Settings first to
wipe your activity history and reflections before uninstalling.

## Troubleshooting

### The app does not start

Check that you are running a supported Windows environment and that the required runtime dependencies are present.

### Tasks do not appear as expected

Verify that the plan JSON is valid and that the plan has the expected phases and tasks.

### Sync issues

If TickTick integration is failing, verify the connection settings and ensure the app has permission to access the required local credentials.

## Development note

This manual reflects the current desktop application version and its core experience. As the app evolves, the feature set may expand.
