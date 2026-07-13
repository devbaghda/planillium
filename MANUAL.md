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

You can keep up to two active plans at once.

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

These settings can be adjusted from the app’s Settings area.

## Data and privacy

Planillium stores data locally on the device. It is designed to be a personal tool rather than a cloud-first collaboration app.

Credentials for services such as TickTick are stored securely via the operating system credential store rather than in plain text.

## Tips for getting the most out of it

- Keep plans focused and realistic.
- Use notes sparingly but clearly.
- Review the weekly report instead of only looking at the daily task list.
- Treat the app as a coach, not just a checklist.
- Use the reschedule and day-off features when real life changes the plan.

## Troubleshooting

### The app does not start

Check that you are running a supported Windows environment and that the required runtime dependencies are present.

### Tasks do not appear as expected

Verify that the plan JSON is valid and that the plan has the expected phases and tasks.

### Sync issues

If TickTick integration is failing, verify the connection settings and ensure the app has permission to access the required local credentials.

## Development note

This manual reflects the current desktop application version and its core experience. As the app evolves, the feature set may expand.
