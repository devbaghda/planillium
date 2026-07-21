# CLAUDE.md — Planillium / Mentor-Overseer

Operating instructions for Claude Code in this repository. **Read `CONTEXT.md` first** —
it has the actual project facts (architecture, schema, business rules, history). This file
is about *how* to work here, not what the app does; keep the two separate rather than
letting either one absorb the other's job.

## Session start
**Read `CONTEXT.md` in full at the start of every session, before doing anything else** —
not just when a task seems to need it. It carries the Session handoff notes (what the last
session left mid-flight, standing lessons from past bugs, open TODOs) and the business
rules that aren't derivable from the code alone. Skipping it risks repeating a mistake
that's already documented there or missing that something is already in progress.

## Repo & branches
- `winui-rebuild` is the working branch; `master` is the default branch. History shows they're
  kept in fast-forward-only sync (`git merge --ff-only`, never a merge commit) — check
  `git log master..winui-rebuild` / the reverse before assuming they've diverged.
- Push fixes to `origin` (both branches) when the user asks for it ("transfer to public
  version," "publish this," etc.) — this has been the pattern every session so far, but it's
  still been on request each time, not automatic. Don't push without being asked.
- GitHub repo `devbaghda/mentor-overseer` is **Public** (flipped 2026-07-21, alongside the first
  real installable release, `v1.1.0` — see `release/`). Never add secrets/personal data; there is
  now a live public-exposure risk if something slips through. `.gitignore` excludes `data/`,
  `config.json`, and `plans/active/*.json`; full-history scrub done 2026-07-18 (see CONTEXT.md).
- A stray public duplicate, `devbaghda/planillium` (a single stale curated-snapshot commit from
  2026-07-08, superseded by the real repo above), was found 2026-07-21 and flagged for deletion —
  check CONTEXT.md's Open TODOs before assuming it's gone; deleting a GitHub repo needs a token
  scope (`gh auth refresh -h github.com -s delete_repo`) this environment's auto-mode classifier
  won't grant automatically, so it may still be pending the user's own action.
- The `mentor-overseer-test`/`mentor-overseer-theme-test`/`code-refinement` worktrees this note
  used to mention no longer exist on this machine as of a 2026-07-21 disk check (`git worktree
  list` shows only this checkout) — don't assume they're still out there without checking first.

## Build & verify
- `dotnet build -p:Platform=x64 -c Debug` (or `-c Release`) from `winui/MentorOverseer.App/` —
  no Visual Studio needed.
- The user's live instance normally runs the **Release** exe
  (`bin\x64\Release\net8.0-windows10.0.19041.0\Planillium.App.exe`), not Debug. Before calling a
  fix "verified," confirm which build is actually running:
  `wmic process where "name='Planillium.App.exe'" get ProcessId,ExecutablePath`.
- To test against the live app: check the current time against `config.json`'s
  `end_of_day_summary_time` before stopping the live instance — stopping it early can skip that
  day's evening-review popup entirely. Then stop → rebuild Release → relaunch → bring to
  foreground.
- `dotnet test` from `winui/MentorOverseer.App.Tests/` runs the automated suite (added
  2026-07-09, covers `ScoreService`'s schedule-shifting logic — the one area that's changed
  repeatedly with a real regression that shipped). **It is a source-file link, not a
  `ProjectReference`** to `MentorOverseer.App` — that project's `UseWinUI=true` pulls in
  MSIX/PRI-resource-generation targets that need Visual Studio's Windows App SDK workload
  installed, which this environment doesn't have; a plain `ProjectReference` fails to build here.
  If you add a test that needs another plain-C# file from the main app, link it the same way
  (`<Compile Include="..\MentorOverseer.App\...\File.cs" Link="App\File.cs" />`) — don't add a
  `ProjectReference` without first confirming `dotnet build` on this machine can actually handle
  a `UseWinUI=true` transitive reference (it couldn't as of 2026-07-09).

## Verifying UI fixes without breaking things
- **Never simulate clicks/keystrokes that would mutate the user's real plan/score data**
  (completing a task, moving a task, marking a day off, etc.). Verify data-mutating logic by
  code inspection plus a clean build — not by clicking it live.
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
When a session ships a real fix or feature change, update in the same pass:
- `CONTEXT.md`'s Session handoff notes — append tersely, it's an index not an archive.
- `CHANGELOG.md` (Unreleased section) and `MANUAL.md`, if the change is user-visible.

**Compacting `CONTEXT.md` is automatic, not something to ask permission for.** After a
significant milestone (a shipped fix/feature, a completed audit-and-remediation pass) or
whenever the Session handoff notes section has grown long (rule of thumb: once it's pushing
the file past ~300 lines, or an individual entry is re-explaining something better left to git
log), compress it in the same pass — don't wait to be asked, and don't ask first. There's
precedent for this (852→224 lines, then further compressed again). **No information may be
lost in compaction** — condense narrative prose into terse facts/decisions/open-items, but
every open TODO, unresolved bug, and standing lesson must survive the compression. When in
doubt about whether a detail is safe to drop, keep it; git log is the fallback for *narrative*
detail, not for facts that only live in this file (open TODOs, business-rule rationale).

**Updating the global skills is automatic, not something to ask permission for.** Whenever a
session produces a significant *error finding and correction* — a real bug found in review, an
audit finding confirmed and fixed, a wrong assumption caught and corrected — check whether the
lesson generalizes beyond this specific app (a pattern any Windows/WinUI app could have, a
testing/verification technique, a class of regression) and fold it into the relevant skill
under `~/.claude/skills/` (`windows-app-auditor`, `windows-app-tester`, etc.) in the same pass.
This repo has sharpened those skills more than once already (WinUI layout quirks, UIA
verification technique, the shift-vs-completion-keying bug class) — keep doing it without
waiting to be asked each time.

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
justify different shift behavior in each. Two standing lessons from this:

1. **When fixing a bug pattern that appears in more than one function doing similar work,
   check every sibling for the same pattern before calling the fix complete.** Grep for the
   pattern name/shape across the file, not just the one call site the bug report pointed at.
2. **Don't generalize a design principle from one code comment or one screenshot into a rule
   applied elsewhere without confirming actual product intent first**, especially before
   changing shared scheduling/business logic. "I saw two tasks under one day once and the user
   didn't complain" is not the same as "multiple tasks per day is the intended steady state." If
   a fix depends on a business-rule assumption that isn't already stated in `CONTEXT.md`, ask
   before applying it broadly — this is exactly the kind of decision-only-the-user-can-make
   case, not one to resolve by inference.
3. **After confirming and fixing something a static/compiler check flagged (like
   `/warnaserror`), re-run that exact check to close the loop** — don't just trust that the fix
   looks right by inspection. This audit caught a build regression (`ReportsPage.xaml.cs:369`)
   that three prior sessions had been treating as a pre-existing, harmless warning without ever
   re-running a clean `/warnaserror` build to check.

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
means the report doesn't meet this project's bar — redo it before presenting.

## Communication style
**Write all explanations of issues, fixes, and findings as if for a non-technical person** —
not just in formal audit reports, but whenever explaining why something is a problem, what's
being changed, or why a fix works. Avoid unexplained jargon, code-level framing, or assuming
the reader already knows the mechanism; explain the real-world effect first, the mechanism
second (and only as much of the mechanism as is needed to trust the fix). This doesn't mean
dumbing down the actual technical work — the code changes stay precise — it means the
*narration around* the work stays accessible.
