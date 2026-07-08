# Planillium

Planillium is a Windows desktop app built to help one person stay aligned with ambitious goals through planning, daily execution, accountability, and reflective review.

It was created for a very specific purpose: to act less like a generic task manager and more like a personal mentor and operating system for progress.

## Why this app exists

Many productivity tools help people organize tasks. Fewer help them stay on course when the work is difficult, long-term, or emotionally demanding.

Planillium was designed to fill that gap by combining:

- structured plans with phases and tasks,
- daily accountability,
- activity and focus tracking,
- score-based motivation,
- weekly reflection and reporting,
- and a calm desktop experience that stays available without being intrusive.

The app is especially suited for people working on major life or career projects, such as career transitions, relocation plans, study programs, and long-form personal development goals.

## What it does

Planillium helps the user:

- manage up to two active plans at once,
- track daily tasks by plan day,
- reschedule or move tasks when life changes,
- record notes on tasks and progress,
- monitor on-plan vs off-plan activity,
- review weekly performance through reports,
- and stay engaged with a lightweight reward and scoring model.

## Concept

This project is built around a simple idea:

> progress improves when the system does more than collect tasks; it guides behavior, reinforces consistency, and provides feedback.

That is why the product is shaped more like a personal mentor than a traditional to-do list.

## Architecture

The current app is a WinUI 3 desktop application for Windows built with .NET 8.

### Main components

- WinUI frontend: user interface, pages, dialogs, tray experience
- Services layer: plan logic, scoring, settings, reporting, tracking
- SQLite database: progress, notes, diary, reflections, and sync state
- TickTick integration: task synchronization and project mapping
- Windows-native tracking: foreground activity and idle detection

### Key technical choices

- Windows-only desktop experience for reliability and local-first usage
- SQLite for lightweight local persistence
- Windows Credential Manager for secure credentials
- Tray-based interaction for low-friction daily use
- A modular service architecture to keep behavior understandable and extendable

## Project structure

- [winui/MentorOverseer.App](winui/MentorOverseer.App) — main application source code
- [plans](plans) — active and archived plan definitions
- [data](data) — local state and database files
- [release](release) — packaging and installer workflow
- [CONTEXT.md](CONTEXT.md) — detailed development context and project history

## Getting started

### Build

From the project root:

```powershell
dotnet build -p:Platform=x64 -c Release
```

Run from the WinUI project directory:

```powershell
cd winui/MentorOverseer.App
dotnet build -p:Platform=x64 -c Release
```

### Notes

- This app is currently designed for Windows.
- It is intended as a personal productivity and accountability tool, not a general-purpose team collaboration platform.
- The project is open-source and meant to be explored, adapted, and improved.

## Why open source

Publishing this project publicly helps demonstrate:

- end-to-end product design,
- desktop application architecture,
- Windows application development,
- data modeling and local persistence,
- and thoughtful UX decisions for personal productivity tools.

It is also a strong portfolio piece for software engineers who want to show they can build real user-facing systems, not just toy demos.

## License

This repository is provided as an open-source portfolio and learning project. Please review the repository license terms before reuse or redistribution.
