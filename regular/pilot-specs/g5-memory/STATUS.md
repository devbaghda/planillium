# G5 memory: status at pause (2026-10-27, ~20:45 UTC)

> **Superseded:** the work was resumed and finished on 2026-10-27 (virtualized diary rows, cause of the Week +55 MB found, docs updated, committed). Final numbers are in `context/todos.md` (G5 entry). The text below is the state at the pause and is kept only for history.

The user paused this work. The code compiles: the strict Debug build is green with 0 warnings. **Nothing is committed.** The test suite has not been run on these edits.

## How it was measured
- Scratch root only: 9,300 diary rows over 93 days, 9,101 of them inside the retention window, with about 3,000 distinct Chrome page titles.
- Env: `MENTOR_INSTANCE_SUFFIX=verify` and `PLANILLIUM_AI_BASE_URL=http://127.0.0.1:9/`.
- Metric: private working set (PWS), read with GetProcessMemoryInfo.
- Scripts, in the session scratchpad `...\93c8c42f-...\scratchpad\g5\`:
  - `measure.py`: full scenario.
  - `cycle.py`: 24 period switches, using cached UI Automation references.
  - `probe_idle.py`: Reports with All time on, left idle.
  - `before\` and `after\` hold copies of the old and new builds.
- **Caveat:** UI Automation tree walks inflate memory themselves. The figures are comparable between builds but not exact absolutes.

## Numbers (PWS, MB): old build (HEAD 0670ecd) vs new build

| Scenario | Old | New |
|---|---|---|
| Empty app: startup / Reports / idle | 41.6 / 55.8 / 51.8 | not rerun |
| Big diary: startup | 44 | 44 |
| Reports, Week | 114 | 112 |
| Reports, All time, 40 rows shown | 158 | 139 |
| After 4 period switches (All time) | 192–230 | 159–167 |
| 24 period switches (`cycle.py`) | 214–235, no plateau | 158–169, plateau |
| Then on Today, Reports hidden | 235 | 142 |
| Then idle 60 s | 216 | 133 |
| Idle on All time: start / 2 min / 4 min | 160 / 154 / 190, reaching 214 at 8 min | 136 / 73 / 79 |
| 140 rows revealed, then back on Reports, then idle on Today | 353 / 346 | 240 / 223 |
| "Show all" (9,101 rows) | 840 MB and climbing toward 2 GB, UI hung | not fixed |

## Causes found
1. **Thrown-away page trees are freed late.** Every control is a small .NET wrapper around a large native XAML object, and .NET does not see the native size. With a managed heap of only 2–8 MB, collection rarely runs, so each Render, diary refresh or period switch leaves tens of MB waiting. Evidence: one forced collection followed by a minute idle took the same process from about 230 MB to 110 MB. This is the largest cause.
2. **The diary refreshes every 30 s even when nothing changed.** When the diary shows today or All time, it rebuilt the list and all four filter dropdowns every 30 seconds. Memory therefore grew while idle: 160 to 214 MB over 8 minutes.
3. **The Page filter built one hidden checkbox per page.** It created thousands of checkboxes on every refresh (8,159 event handlers in the heap dump).
4. **The hidden cached Reports page kept its whole tree alive.** That includes any revealed rows.
5. **Not fixed:** "Show all" builds every row in a list that does not virtualize (about 90 KB per row).
6. **Open:** Reports Week with the big diary costs about +55 MB over an empty database. Not yet broken down.

## Files changed (uncommitted, regular side only)
- `Pages/ReportsPage.DiaryFilters.cs`: MultiPicker keeps its options as data and builds its checkboxes only while the dropdown is open, clearing them on close.
- `Pages/ReportsPage.Diary.cs`:
  - Shared `QueryDiaryRows()`.
  - `DiaryRowsSignature()` fingerprint.
  - The live-refresh tick skips the rebuild when the rows are unchanged.
  - `RenderDiaryResults` calls `ReleaseDroppedTree()`.
- `Pages/ReportsPage.xaml.cs`:
  - `OnNavigatedFrom` clears `Body` and the two timer callbacks.
  - New `ReleaseDroppedTree()`: one coalesced low-priority `GC.Collect()` after a tree is dropped, called from `Render` and `OnNavigatedFrom`.

## Left to do
1. Run `dotnet test --no-build` from `regular/winui/Planillium.App.Tests/`.
2. Prove nothing visible changed:
   - Compare the TEXTS lines in `result_before_big.txt` and `result_after_big.txt` (rows, totals, "Showing N of M", "9101 entries · 694h 54m total").
   - Check the scroll position when returning to Reports.
3. Rerun `measure.py after ./after empty` to get the empty-app baseline on the new build.
4. Sibling sweep (rule): Today and Schedule also rebuild their trees on Render. Decide whether they need `ReleaseDroppedTree` too (measure first).
5. Optional: break down the Week +55 MB. Ask the user whether to virtualize "Show all"; that would change how the list is built and has UI Automation and layout risk.
6. Docs:
   - `regular` CHANGELOG Unreleased.
   - `context/todos.md`: the line 445 "memory footprint accepted" note is superseded.
   - Merge lessons into `regular/LESSONS.md`.
   - Set the outcomes of the two G5 lines in `MODEL-LOG.md`.
7. Commit only the three source files plus my own docs, not the PM's pending edits. No push, no QA.

## How to resume
1. Rebuild Debug.
2. Copy the output to `scratchpad\g5\after`.
3. Run the scripts above with `PYTHONIOENCODING=utf-8 py <script> ...`.
4. Each script kills only the PID it started. Live PID 20748 was never touched.
