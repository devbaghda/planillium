# CLAUDE.md — Planillium / Mentor-Overseer

Operating instructions for Claude Code in this repository. **Read `CONTEXT.md` first** —
it has the actual project facts (architecture, schema, business rules, history). This file
is about *how* to work here, not what the app does; keep the two separate rather than
letting either one absorb the other's job.

## Repo & branches
- `winui-rebuild` is the working branch; `master` is the default branch. History shows they're
  kept in fast-forward-only sync (`git merge --ff-only`, never a merge commit) — check
  `git log master..winui-rebuild` / the reverse before assuming they've diverged.
- Push fixes to `origin` (both branches) when the user asks for it ("transfer to public
  version," "publish this," etc.) — this has been the pattern every session so far, but it's
  still been on request each time, not automatic. Don't push without being asked.
- GitHub repo `devbaghda/mentor-overseer` is currently **Private**. Treat it as pre-public —
  still avoid adding secrets/personal data (see CONTEXT.md's Open TODOs) — but there's no live
  public-exposure risk today if something slips through.
- Other worktrees/branches exist elsewhere for this repo (`mentor-overseer-test`,
  `mentor-overseer-theme-test`, `code-refinement`) — this directory isn't the only checkout.

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
  Compress it once it gets long (there's precedent: 852→224 lines, then further down again)
  rather than letting entries accumulate forever.
- `CHANGELOG.md` (Unreleased section) and `MANUAL.md`, if the change is user-visible.
- Global skills under `~/.claude/skills/` (`windows-app-auditor`, `windows-app-tester`, etc.)
  when a lesson generalizes beyond this specific app — this repo has sharpened those skills
  more than once (WinUI layout quirks, UIA verification technique, the
  shift-vs-completion-keying bug class).
