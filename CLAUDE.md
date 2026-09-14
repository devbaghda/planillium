# CLAUDE.md — Planillium

Operating instructions for Claude Code in this repository. **Read `CONTEXT.md` first** —
it has the actual project facts (architecture, schema, business rules, history). This file
is about *how* to work here, not what the app does; keep the two separate rather than
letting either one absorb the other's job.

> Machine-wide working rules — communication style, real-data handling, engineering discipline,
> doc currency, skill updates, the `py`/cp1252 environment notes — now live in
> `~/.claude/CLAUDE.md` and load automatically alongside this file. Everything below is what's
> specific to Planillium.

## Session start
**Read `CONTEXT.md` in full at the start of every session, before doing anything else** —
not just when a task seems to need it. As of the 2026-09-14 split it stays thin (current state,
rules, a decisions-highlights list, open items with dates, and a Section Index); it points at
`DECISIONS.md` (business rules, standing lessons) and `context/*.md` (schema/format reference,
full session-log history) for detail, fetched on demand rather than read end-to-end. Skipping
CONTEXT.md itself still risks repeating a mistake or missing that something is already in
progress — that's the part with no substitute.

## Repo & branches
- `winui-rebuild` is the local working branch; `master` is the default branch and, as of
  2026-07-21, the **only** branch that exists on GitHub — `winui-rebuild` was deleted from the
  remote (the two were always identical, fast-forward-only, so keeping both there was just
  clutter for anyone browsing the repo). Locally, keep committing on `winui-rebuild` as before,
  fast-forward local `master` up to it, but only ever push `master` to `origin` — pushing
  `winui-rebuild` by name (e.g. a bare `git push` with its old upstream tracking still
  configured) would silently recreate it on GitHub; push explicitly as `origin master` or push
  `winui-rebuild` and immediately delete the remote branch again if that happens.
- Push fixes to `origin` when the user asks for it ("transfer to public version," "publish
  this," "github is ready for publication," etc.) — this has been the pattern every session so
  far, but it's still been on request each time, not automatic. Don't push without being asked.
- GitHub repo is `devbaghda/planillium` (renamed from `mentor-overseer` 2026-07-21, matching the
  app's actual display name — `git remote` already points here, no action needed). It's **Public**
  (flipped 2026-07-21, alongside the first real installable release, `v1.1.0` — see `release/`).
  Never add secrets/personal data; there is a live public-exposure risk if something slips
  through. `.gitignore` excludes `data/`, `config.json`, and `plans/active/*.json`; full-history
  scrub done 2026-07-18 (see CONTEXT.md).
- A stray public duplicate that briefly held the `devbaghda/planillium` name (a single stale
  curated-snapshot commit from 2026-07-08) was found and deleted 2026-07-21, which is what freed
  up that name for the rename above — see CONTEXT.md's Open TODOs for the full story.
- The `mentor-overseer-test`/`mentor-overseer-theme-test`/`code-refinement` worktrees this note
  used to mention no longer exist on this machine as of a 2026-07-21 disk check (`git worktree
  list` shows only this checkout) — don't assume they're still out there without checking first.

## Build & verify
- `dotnet build -p:Platform=x64 -c Debug` (or `-c Release`) from `winui/Planillium.App/` —
  no Visual Studio needed.
- The user's live instance normally runs the **Release** exe
  (`bin\x64\Release\net8.0-windows10.0.19041.0\Planillium.App.exe`), not Debug. Before calling a
  fix "verified," confirm which build is actually running:
  `wmic process where "name='Planillium.App.exe'" get ProcessId,ExecutablePath`.
- To test against the live app: check the current time against `config.json`'s
  `end_of_day_summary_time` before stopping the live instance — stopping it early can skip that
  day's evening-review popup entirely. Then stop → rebuild Release → relaunch → bring to
  foreground.
- `dotnet test` from `winui/Planillium.App.Tests/` runs the automated suite (added
  2026-07-09, covers `ScoreService`'s schedule-shifting logic — the one area that's changed
  repeatedly with a real regression that shipped). **It is a source-file link, not a
  `ProjectReference`** to `Planillium.App` — that project's `UseWinUI=true` pulls in
  MSIX/PRI-resource-generation targets that need Visual Studio's Windows App SDK workload
  installed, which this environment doesn't have; a plain `ProjectReference` fails to build here.
  If you add a test that needs another plain-C# file from the main app, link it the same way
  (`<Compile Include="..\Planillium.App\...\File.cs" Link="App\File.cs" />`) — don't add a
  `ProjectReference` without first confirming `dotnet build` on this machine can actually handle
  a `UseWinUI=true` transitive reference (it couldn't as of 2026-07-09).

## Verifying UI fixes without breaking things
- The global no-mutating-real-data rule applies here as: **never simulate clicks/keystrokes that
  would change the user's real plan/score data** — completing a task, moving a task, marking a day
  off. Verify that logic by code inspection plus a clean build, not by clicking it live.
- Read-only UI Automation (`System.Windows.Automation` via PowerShell,
  `Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes`) is safe and precise for
  verifying layout/values: `BoundingRectangle` for exact positions, `ValuePattern` for
  displayed text, `SelectionItemPattern`/`TogglePattern` for navigation. Beats screenshot
  diffing for anything pixel-exact.
- For visual evidence when it's still needed: `PrintWindow` with `PW_RENDERFULLCONTENT`
  (flag `2`) against the app's HWND — plain `CopyFromScreen`/GDI `BitBlt` does not capture
  WinUI3's Mica/DirectComposition rendering correctly and can show stale content.
- Cached `AutomationElement` references go stale across any `Render()` call that rebuilds the
  visual tree (common in this app's pages) — re-query fresh inside loops, don't reuse a
  reference captured before a rebuild.

## Direct database access
`data/progress.db` is the user's real, live data — not a fixture. Any direct write to it
outside the app's own code (e.g. a one-off correction script fixing data a since-patched bug
wrote incorrectly) requires the user's **explicit confirmation naming the specific table and
change** before running it. This isn't just good practice here — the harness's auto-mode
classifier enforces it and will reject a vague "yes, go ahead."

## Keeping docs current
The global rules on doc currency and the `CONTEXT.md` + `context/*.md` split scheme apply
(`~/.claude/CLAUDE.md` → Keeping knowledge current). Project specifics:
- **Three registers, different jobs** (`DECISIONS.md` split 2026-08-04; `context/*.md` split
  2026-09-14): `CONTEXT.md` is the read-through handoff — current state, rules, a
  decisions-highlights list, dated open items, and a Section Index. `DECISIONS.md` is the lookup
  register — the 13 numbered business rules with their rationale, and the standing lessons.
  `context/domain.md` is architecture/schema/format reference; `context/todos.md` is resolved work
  (done, or decided not to do), dated. Read `CONTEXT.md` at session start; consult `DECISIONS.md`
  before changing anything in the areas it covers; fetch `context/*.md` on demand by grepping for a
  header, not read end-to-end. Keep them separate: a new business rule or a lesson learned the hard
  way goes in `DECISIONS.md`; a shipped fix or feature, once it resolves, goes in
  `context/todos.md`; an open item stays in `CONTEXT.md` §7 until it resolves.
- Docs to update in the same pass as a shipped fix or feature: add a dated entry to
  `context/todos.md` (terse — it's an index, not an archive) and cut the matching item from
  `CONTEXT.md` §7 if it was tracked there; plus `CHANGELOG.md` (Unreleased) and `MANUAL.md` if the
  change is user-visible.
- **`CONTEXT.md`'s compaction threshold is 400 lines**, declared in its own header where the Stop
  hook reads it (must stay on one line — `Compaction threshold: 400 lines` — for the hook's regex
  to match). Count with `wc -l`, not PowerShell's `Measure-Object -Line` (it skips blank lines and
  under-reported this file by ~60). Over threshold means compact **in the same pass**. Since the
  2026-09-14 split, the file should stay well under 400 by design — bulk content lives in
  `DECISIONS.md`/`context/*.md`, which are never compacted. If it's approaching 400 again, the
  first move is checking whether something that grew inline (e.g. §7 Still open) belongs in
  `context/todos.md` once resolved, not shrinking prose that's still current.
- Skills this repo has sharpened and should keep sharpening: `windows-app-auditor`,
  `windows-app-tester` — WinUI layout quirks, UIA verification technique, the
  shift-vs-completion-keying bug class.

## Regression-prevention lesson (2026-07-09 audit, finding #1)

`MoveTaskToToday`'s forward-shift-to-avoid-overlap logic was removed in one session because
multiple tasks per day had become normal — but the *sibling* function `RescheduleTask`, which
implements the same "insert, don't overlap" pattern for a different user action, was left
untouched. A later audit caught the inconsistency and initially framed it as a straightforward
"regression to fix by matching the two functions" — but on closer discussion with the user, the
right fix might not be "make them match" at all: the two functions serve different actions
(pulling a *future* task to *today*, vs. manually relocating an *overdue* task to an
*arbitrary* future day) and the user's actual mental model — strict one-task-per-day as the
steady state, with multiple tasks on a day only as a transient "I did extra today" fact — may
justify different shift behavior in each.

This episode is the origin of three rules now held globally: **check every sibling** for the same
pattern before calling a fix complete; **don't infer a business rule** from one comment or one
screenshot; **re-run the exact check** that flagged something after fixing it. Project-specific
detail worth keeping: that last one caught a build regression (`ReportsPage.xaml.cs:369`) which
three prior sessions had treated as a pre-existing harmless warning without ever re-running a
clean `/warnaserror` build. And the shift-behaviour question above is **still unresolved** — do
not "make the two functions match" without asking.

## Audit report format
When running `windows-app-auditor` (or reporting findings from one) for this project, use this
table structure instead of the skill's default one — the user reads these findings, not just
engineers:

| # | Severity | Category | Finding | Suggested fix | Explanation | Location |

The **Explanation** column is mandatory and must be written for a non-technical reader (see
Communication style below). It must always cover, explicitly, all three of:
1. **Why this is an issue** — the real-world consequence if left alone.
2. **What the suggested fix is** — described in plain terms, not just a code snippet.
3. **Why that fix solves the issue** — the causal link between the fix and the problem going
   away.

Skipping any of the three, or writing the Explanation column in engineer-to-engineer language,
means the report doesn't meet this project's bar — redo it before presenting. The global
communication rules say the same thing in general; this table is the specific form they take here.
