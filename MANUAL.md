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

You can keep up to two active plans at once. Have a third idea before you're ready to drop one
of the current two? "Add Plan" at the limit offers to save it as a queued idea instead of
turning you away — it shows up under "Queued ideas" on the Plans page, where you can start it
the moment a slot frees up (or delete it if it's no longer worth pursuing). Finishing and
archiving a plan also offers to start one of your queued ideas right there.

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

If you step away from the computer, the app asks "where have you been?" when you return, at any time of day. And if you finish and stop before your configured end-of-day, the evening review asks about that unaccounted stretch before it closes the day, so time you spent away is still recorded rather than left blank.

The morning "start your day" prompt and the "where have you been?" prompt only open directly inside the app window when that window is actually visible. If it's hidden in the tray (or minimized), you'll get a tray notification instead — click it to open the app and answer.

### Reports

Reporting summarizes progress and highlights where effort was spent. This helps the user see patterns rather than only isolated task completion.

Reports cover the current calendar period: the weekly view runs from this Monday through Sunday, the monthly view spans this calendar month, and the yearly view spans this year — not a rolling look-back over the last several days.

The Day/Week/Month/Year selector governs everything above the diary: the score card, the summary
table, top distractions, time by app, and the insights. The score card reads "SCORE EARNED —
THIS WEEK" (or whichever period you've picked) and shows the points that period's activity
added up to — which is not the same as the BALANCE in the sidebar, since that is a running total
across all time and also subtracts anything you've spent on entertainment time. The diary section
at the bottom is separate and always works one day at a time.

The summary table carries the same columns on every period view: tasks done, one column for each
of the five categories your time is sorted into (on-plan, off-plan, neutral, paid and idle), that
row's own Total, and its Score. The Total is all five categories added together — so it is all the
time tracked in that day or period, not just the productive-versus-distracted pair; neutral time
(anything that is neither) is usually the largest share of it. The table then ends with a Total row
summing every column, including the score, so it reads in both directions without adding anything
up by hand. That score total is the same figure as the score card above the table, by construction.
Days where every plan is off contribute no time to any of it, matching how they're already excluded
from the rest of Reports, but a task you did complete on a day off still earns its score.

Durations on Reports are shown in decimal hours — "5,5 h", not "5h 30m" — everywhere except the
diary at the bottom, which stays in hours and minutes because a diary entry is a clock event rather
than a quantity you add up. Each figure is rounded to one decimal on its own, so a column of rows
can differ from its total by 0,1: the total is the exact one.

On a narrow window the table scrolls sideways inside its card rather than losing its last columns.
The exported HTML and CSV reports carry the same columns in the same order as the screen.

The "time by app" breakdown shows your three biggest time sinks by default, with a "Show more" link to reveal the full list.

The diary section of the Reports page has a date picker in its header for jumping straight to a specific day's activity instead of stepping through one day at a time, bounded to how far back diary history is retained. If you leave the page open on today's diary across midnight, it rolls onto the new day by itself; if you'd navigated to a specific past day, it stays there.

The diary list itself only loads a batch of entries at a time (40 up front, 50 more each time you
click "Show more") rather than the whole matching history at once, so a long search or a busy day
still stays responsive. When editing or splitting a diary entry, the description field suggests
your most commonly used past descriptions as you type, so recurring ones don't need retyping.

## Settings and configuration

Settings is organised as seven sections — General, Hours & reminders, Scoring, Activity keywords,
Idle-answer library, TickTick, and Data — chosen from the menu down the right-hand side. Each menu
entry shows a summary of what that section currently holds, so you can read your whole
configuration off the menu without opening anything, and go straight to the part you want to
change. The page stays the same size and shape whichever section you're on, and it reopens on the
one you used last. Whether tracking is currently running, and the confirmation (or error) from
your last change, stay visible on every section.

Everything saves as you go — there is no Save button.

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
  folder — it never touches your plan definitions themselves, which are archived or removed from
  the Plans page instead.
- **TickTick.** If you connect a TickTick account, your task titles, project names, and due dates
  are sent to and from TickTick's own servers so the two stay in sync — that's the one place data
  leaves your machine. Credentials for TickTick (and any other connected service) are stored
  securely via the operating system's credential store rather than in plain text.

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
