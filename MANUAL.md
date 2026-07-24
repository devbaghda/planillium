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
- and briefing context.

### Today view

The Today page helps you focus on what matters right now.

It shows:

- the current plan day,
- unfinished tasks,
- overdue work,
- and opportunities to start tomorrow’s work early.

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

The "time by app" breakdown shows your three biggest time sinks by default, with a "Show more" link to reveal the full list.

The diary section of the Reports page has a date picker in its header for jumping straight to a specific day's activity instead of stepping through one day at a time, bounded to how far back diary history is retained.

## Settings and configuration

The app includes configurable settings for:

- working hours,
- reminder behavior,
- idle thresholds,
- themes,
- and activity classification rules.

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
