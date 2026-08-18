# Planillium — Decisions & Standing Lessons

> **Lookup register, not a read-through.** `CONTEXT.md` is the file to read start to finish every
> session; this one is what you consult when you are about to change something in these areas.
> Split out of `CONTEXT.md` on 2026-08-04, when that file had reached ~535 lines against a 400
> threshold and the remaining bulk was no longer narrative to condense but two registers that
> simply belong in their own file — the same move the DigiFlow project made for the same reason.
>
> **Nothing here was rewritten in the move.** Every business rule and lesson is the text that was
> in `CONTEXT.md`, relocated verbatim.
>
> **Read the relevant section before changing anything it covers.** These rules cost real bugs to
> learn; several record a decision the user made after I argued the opposite.

## Key business rules
1. **Working hours (`working_hours.start`/`.end`, config.json + Settings; default 08:00–20:00)
   are the diary tracking window too** — one pair of hours governing both, since 2026-08-04.
   They decide when the off-plan nag may fire *and* when activity is recorded at all
   (`ActivityTracker.InWorkingHours`, the single predicate both use). Outside them nothing is
   tracked and no gap is counted against the user.
2. The diary window used to be a pair of hardcoded 06:00/20:00 statics in `ActivityTracker`,
   unrelated to working hours and unreachable from the UI — so moving the working day to 08:00
   left 06:00–08:00 still tracked and back-filled as "unaccounted time" every morning (the
   2026-08-04 report). It spent a few hours as its own `diary_hours` config block with its own
   Settings pair before the user's call to merge the two: one pair of hours is the whole idea,
   a second pair is just another thing to keep in sync. **A `diary_hours` block in an existing
   config.json is inert** — not read at all, and stripped on the next Settings save.
   `SettingsPage.SaveRules` rejects work start ≥ end, which only became worth enforcing once
   these hours gated tracking: inverted, that isn't a short day, it's no tracking at all.
3. Reminder grace: 15 min off-plan before first alert; escalates every 5 min after
4. Idle threshold: 10 min; a wall-clock gap much larger than the poll interval is
   treated as sleep, not idle
5. Plan day: `(today − start_date).days + 1`, exclusion-aware (`excluded_weekdays`,
   `plan_days_off`)
6. Overdue: any incomplete task from a past day resurfaces; `task_overdue_penalty`
   applies once for the day-of miss (folded into that day's `daily_score`) and again
   *every subsequent day* it stays outstanding (`overdue_accrual`, capped at 3 days).
   Rescheduling doesn't refund penalties already taken.
7. **The user's steady-state rule: one task per day, and no dead days either.** A day
   holding two tasks is only ever a transient fact ("I did two things today"), never a
   permanent state a scheduling action should create; a day left holding zero once
   something moves off it isn't meant to be permanent either. Reschedule/Day-off and
   Move-to-today still differ on the *doubling-up* half of this (confirmed 2026-07-09,
   after an audit flagged the difference and the user clarified it wasn't an
   inconsistency), but as of 2026-08-05 (bug report: two upcoming weekdays showed empty
   in Schedule after individual reschedules) they no longer differ on the *gap* half:
   - **Reschedule / Day-off** use the "insert, don't overlap" forward shift: whatever's
     already on the target day (and everything after it) shifts forward one day first,
     rather than doubling up — because these are "place this specific task on this
     specific day" actions, and the one-task-per-day rule must hold going forward.
   - **Move-to-today** does *not* shift forward: pulling a future task to today just adds
     it alongside today's own task (a deliberate, transient exception to the rule — you
     really did finish two things today).
   - **Gap-closing now applies to both.** If a move empties out the task's old day,
     everything after it shifts *back* one day to close the gap — Move-to-today already
     did this (2026-07-09: finishing ahead of schedule compresses the remaining plan);
     Reschedule now does too (2026-08-05), computed as one combined formula per task
     rather than two independent shift passes, so a task caught between the old and new
     day isn't fought over by both — see `ScoreService.RescheduleTask`'s own doc comment
     for the worked-through cases. **Exception: an overdue task's own (already past) day
     is never compacted** — that would pull a currently-future task backward across
     today, silently making it overdue too, rather than filling a hole in the *upcoming*
     schedule. `ReplanOverdueDialog` relies on this: every day it reschedules off of is
     by definition already overdue/past.
   - **Already-completed tasks are never shifted** by any of these four operations
     (fixed 2026-07-09; see Session handoff notes — shifting a completed task orphaned
     its `task_completions` row, keyed by assigned day, silently unmarking it and
     moving it to tomorrow).
8. Archive: plan moves to `plans/archive/` when ALL tasks done; frees a slot (max 2
   active plans). Archiving now also offers to immediately start a queued idea if one
   exists (2026-07-22) — see business rule 11.
9. Score floor: daily score floors at −10; a `weekly_comeback_bonus` (20 pts) rewards
   a full week back on track after a bad stretch; a `multi_task_bonus_per_extra_task`
   (3 pts, added 2026-07-09) rewards each task completed beyond the first one on the
   same day, on top of the flat per-task rate.
10. **Day-off scoring (added 2026-07-17)**: when EVERY active plan considers a day off
    (recurring exclusion or manual day-off — `ScoreService.AllPlansScoringExempt`; one
    plan off while another still has real work due does NOT trigger this), that day's
    on/off-plan minutes, missed-task penalty, and streak bonus are all suppressed —
    "no points calculation" is the default for a day off. The one exception: a task
    actually brought in and completed that day (e.g. via Move-to-today onto an
    off day) still earns its own task-completion + multi-task-bonus credit — being off
    doesn't forfeit credit for real work done anyway. The same day-off dates are also
    excluded from Reports' aggregate totals (weekly/monthly/yearly summary, Time-by-App,
    Top Distractions) — tracked and still visible in the raw Diary list, just not
    counted toward any total. Editing a diary entry's category/time (or bulk-marking
    several) now recalculates that date's `daily_score` via `RecalculateDayScore`
    (unlike `CreditDayScoreIfMissing`, this overwrites an already-credited day — it has
    to, since the whole point is a stale figure needs updating). The off-plan nag alert
    also doesn't fire on a manually-off day (`ActivityTracker.IsFullyOffToday`) — but
    unlike a recurring rest day, tracking itself still runs normally on a manual day off
    (diary rows keep getting written); only scoring and the alert are suppressed.
11. **Queued plan ideas (2026-07-22)**: hitting the 2-active-plan limit in "Add Plan" no
    longer just blocks — it offers to save the new plan to `plans/queued/` instead. A
    queued plan is completely inert (not loaded into scoring/Today/Schedule, no
    `start_date` set at creation) until `PlanStore.ActivateQueuedPlan` moves it to
    `plans/active/` and resets `start_date` to the activation date. Two entry points to
    activate one: the Plans page's own "Queued ideas" section ("Start now," enabled only
    when a slot is free), or a suggestion dialog offered automatically right after
    archiving a plan frees one up.
12. The sidebar/Plans-page "Xd late from plan" (`Plan.DriftDays`) measures reschedule/overdue
    slip *beyond* the plan's own excluded-weekday pattern (current last-task date vs. the
    originally-due date, both already mapped through the exclusion pattern) — this is now the
    only "days late" figure in the app. Reports used to also show a second, differently-computed
    figure (the "Exclusion Impact" panel); removed 2026-07-23 for showing a different-looking
    number for the same plan that read as a bug (it wasn't — see the 2026-07-23 session note)
    and for not earning its keep otherwise.
13. **"Day X of Y" is a progress counter, not a calendar counter** (`Plan.ProgressDay`, added
    2026-08-04) — **display only**, on Today/Plans/Schedule. Every scheduling, overdue and
    scoring decision still uses `Plan.PlanDay` (the pure calendar count); the two are
    deliberately different numbers for different questions. The rule: stop at the earliest plan
    day still holding an incomplete task, never run ahead of the calendar day, never exceed the
    plan length shown after the "of". So missing day 10's task keeps the header on day 10 the
    next morning, and working ahead doesn't release it — only closing the day behind it does.
    That can't strand the counter, because **the user's stated way to skip a day's task is to
    reschedule it** (Reschedule / "Replan all overdue"), which moves its `AssignedDay` forward
    and releases the counter by itself; a task nobody reschedules and nobody finishes is exactly
    the case that should hold it. The plan-length clamp is what stops a 28-day plan reading
    "Day 30 of 28" once it overruns — lateness is reported by the overdue list and `DriftDays`
    (rule 12), not by inflating this number. All three screens read the same value on purpose
    (see the 2026-07-23 DriftDays lesson on two screens showing different-looking numbers for
    the same thing).

---


### Standing lessons
_The durable ones. Every entry here cost a real bug to learn; none may be dropped in a
compaction. General versions of several now also live in the global `windows-app-auditor` /
`windows-app-tester` skills._

**Verification discipline**
- **Re-run the exact check that flagged something, after fixing it** — don't trust that it looks
  right. A build regression (`ReportsPage.xaml.cs:369`) survived three sessions as an assumed
  "pre-existing harmless warning" because nobody re-ran a clean `/warnaserror` build.
- **A change to process bootstrap or native interop isn't verified until the app has actually
  been relaunched** — not just built clean and unit-tested. The 07-24 `SetDefaultDllDirectories`
  hardening passed both and then sat in `App.xaml.cs` for 3 days, having broken WinUI's native
  activation so the app wouldn't start at all (found 07-27, reverted).
- **A remediation's re-audit must re-check the fix's own new code, not just confirm the original
  finding is gone.** Round-8's R8-05 fix reproduced the very "silent failure" shape it was
  written to close, inside itself — caught only because the full checklist was re-run rather
  than "is R8-05 gone".
- **Check every sibling** before calling a fix complete. Grep for the *shape*, not the reported
  call site. This project's uncaught instance: round-5's shared colour table
  (`ReportsPage.Styling.CategoryBrushKey`) never got the tray pill (`MainWindow.Tracker
  .UpdatePill`) migrated onto it, and a doc comment falsely claiming "both now read from this one
  table" stood for two rounds.
- **Don't infer a business rule** from one comment or one screenshot. If a fix depends on a rule
  that isn't written down, ask.
- **A `#if DEBUG`-gated test-only safety hook is only as safe as "tests always build Debug."**
  `TestRootFixture`'s real-DB-isolation override compiled out under `dotnet test -c Release`
  (2026-08-13), silently writing 112+42 real rows before anyone noticed. Gate test-only behaviour
  on a constant the test project defines unconditionally in every configuration, not on `DEBUG`.
- **A settings/save page split across independent sections must save each section on its own
  success, not gate the whole page behind one shared validation pass.** `SettingsPage.SaveRules`
  used to validate scoring/reminder fields before writing *anything*, including the unrelated
  working-hours pair the user had actually just changed — one invalid reminder box silently
  discarded a correct working-hours edit with no error naming which field blocked it. Split into
  independent phases (2026-08-17), each with its own validate-then-write and its own error
  message; a failure in one phase never blocks a different phase's already-valid write.

**Safety around real data**
- Never simulate input (clicks/keystrokes) that would mutate real plan/score data — verify
  data-mutating logic by code inspection plus a clean build. Any direct write to
  `data/progress.db` outside the app's own code needs explicit confirmation naming the table and
  change (the harness's auto-mode classifier enforces this and rejects a vague "yes"). Read-only
  queries against it are always fine.
- Check `end_of_day_summary_time` before killing a live instance near EOD — killing it early can
  skip that day's evening-review popup entirely.
- The live instance runs from `bin\x64\Release\...`, not Debug — confirm before trusting a Debug
  rebuild as verification.
- **To exercise the UI without touching real data, run a second Debug instance against a scratch
  `MENTOR_ROOT`** (copy config.json + plan files, empty DB) with `MENTOR_INSTANCE_SUFFIX=verify`
  — the DEBUG-only mutex suffix exists for exactly this. Back-dating the scratch plans' start
  date stages date-dependent behaviour that is otherwise very hard to reach (2026-08-04). Where
  realistic *content* is needed (Reports layout, bar widths), copy the real `progress.db` into the
  scratch root — reading real data is fine, and every write then lands on the copy.
- **Copying a SQLite database is not copying one file.** `progress.db-wal` and `-shm` hold recent
  writes; copying only the `.db` into a root that already has an older instance's sidecars makes
  SQLite replay *those* over your copy, and the app opens what looks like an empty database with
  no error anywhere. Delete `progress.db*` in the destination first, then copy. Cost 20 minutes
  and one wrong conclusion ("the copy failed") on 2026-08-05.
- **`git filter-repo` must never run in-place in a repo with other live worktrees attached** —
  it refuses unless the repo looks freshly cloned, and forcing it risks corrupting them via the
  shared object store. Rewrite in an isolated scratch clone (`git init` + `git fetch <path>
  branch:branch`, which also excludes local-only branches), verify with a git *blob* diff
  (`git diff <old> <new> --stat` — a raw `diff -rq` falsely flags nearly every file, because two
  clones differ in line endings), push from there, reset the real working copy after. Also:
  `--replace-text` only rewrites blob content — a separate `--replace-message` pass is needed to
  get a string out of "history" in the sense a user means it.

**WinUI / layout**
- **A `Border` with a non-zero `CornerRadius` corner-clips its content to its own *arranged*
  bounds**, regardless of the child's MinWidth, DesiredSize or alignment — no layout lever on a
  descendant can compensate; the Border itself must be wide enough. A clipped element's UI
  Automation `BoundingRectangle` reads `Empty`, identical to "never given layout space", so that
  signature must not rule clipping out. When a dynamically-built row that used to fit stops
  fitting after a sibling column grew, check every rounded-corner Border between the content and
  its ScrollViewer.
- **`Expander` needs `HorizontalContentAlignment="Stretch"` or its content is arranged at its
  own minimum width**, no matter how wide the Expander itself is. A three-column grid inside one
  collapses to the width of its text instead of filling the row — the same silent under-sizing
  class as the rounded-`Border` clip above, with the same absence of any error. Set it on every
  Expander, and verify with UIA rects rather than by eye (2026-08-04).
- **`MaxWidth` on a control centres it inside its cell; `MaxWidth` on the `ColumnDefinition` packs
  it left.** Capping the controls looks right in isolation and wrong in a form — consecutive rows
  stop sharing a left edge, because each control is centred in whatever width its own column
  happened to get. Cap the column. Related: `MaxWidth` + `HorizontalAlignment="Left"` on a *panel*
  makes it size to its content rather than to its cell, which collapses every star column inside it
  — the same trap as the `Expander` one above. A capped star column has neither problem (2026-08-05).
- **A `ListViewItem` whose content is a panel rather than a string has no accessible name** — a
  screen reader announces nothing at all for it. Set `AutomationProperties.Name` on every such
  item. Invisible on screen and in code review; only reading the UIA tree back finds it
  (2026-08-05, all seven Settings menu entries).
- **`BoundingRectangle.Empty` inside a ScrollViewer usually means below the fold, not clipped** —
  resolve the ambiguity rather than guessing, by calling `ScrollItemPattern.ScrollIntoView()` on
  the furthest element and re-measuring: the far items acquire rects and the near ones go Empty in
  turn. Note `ScrollPattern.SetScrollPercent` was a silent no-op on this app's ScrollViewer, so
  don't read "nothing moved" as "nothing to scroll" — check `VerticallyScrollable`/`VerticalViewSize`
  first (2026-08-05).
- `CopyFromScreen`/GDI `BitBlt` doesn't capture WinUI3 Mica/DirectComposition content — use
  `PrintWindow` with `PW_RENDERFULLCONTENT` (flag `2`). For exact layout comparisons, UIA
  `BoundingRectangle` beats pixel-diffing.
- Cached `AutomationElement` references go stale across any `Render()` that rebuilds the visual
  tree — re-query fresh inside loops.
- `DispatcherQueueTimer.Tick` was confirmed to silently never fire while `IsRunning` read true
  (60+ missed intervals, diagnostics in place). All watchers use `System.Threading.Timer` +
  `_dq.TryEnqueue`, each **stored in a field** — an unrooted one is GC-eligible and stops firing.
- Subscribe to `AppNotificationManager` events *before* `.Register()`; the reverse order throws
  at the WinRT layer.

**Prompts, timers and state**
- Any "show a prompt on a timer" trigger needs BOTH the `IsOnScreen()`-check-then-toast-fallback
  pattern AND an "already showing, don't reopen" guard — confirmed missing on `StartEodWatcher`,
  `ReviewDialog`, `KickoffDialog`. A once-per-day "don't re-offer" flag must be set only by the
  *automatic* path: a manual preview button sharing the same show-and-persist function silently
  burns the automatic offer, with no error to catch it.
- **A surface that repeats another prompt's copy ("click to X") must carry that prompt's action
  too, not just its text.** They drift apart the moment a prompt is shown through a second
  surface (a recap, a log, a history view) that wasn't part of its original click path.
- **When a background poll first *notices* a state change, the poll's timestamp is not when the
  change happened** — it lags by up to the full poll/threshold interval. Close events out at the
  back-computed real moment, not "now", or two records meant to be back-to-back overlap instead.
- **State that must survive a rebuild has to live outside the thing being rebuilt.** A `static`
  field seeded once at class load is not the same as state that tracks "today" — Reports' diary
  date was static and so stayed pinned to yesterday overnight even though the day-change watcher
  faithfully re-rendered the page (2026-08-04).

**Design / simplicity**
- **Prefer the closed form over a cache when the repetition has structure.** `PlanDayForDate`'s
  O(days-elapsed) walk could have been memoized — but that adds an invalidation surface for a
  problem the maths dissolves: the exclusion pattern is weekly-periodic, so skipping whole weeks
  in closed form is O(1) with no state to keep in sync. A cache is right when the work is
  genuinely irreducible (e.g. `ScoreService.DaysOff`), not when it's unexploited structure.
- **A defaults table beats a default retyped at each call site.** `("task_overdue_penalty", -5)`
  appeared three times and five more rules were consts with no config presence at all; adding a
  Settings UI would have made a sixth copy. One `ScoringRules` table now feeds the formula, the
  config lookup and the Settings section (2026-08-04).
- **A code comment anticipating a confusion doesn't prevent the confusion.** The 2026-07-23
  DriftDays/shiftDays report is a round-4 finding's *predicted* confusion happening for real, a
  year of comments later. Put the clarification where the user looks — the UI — not only in a
  doc comment a future session reads.
- A dedup helper (e.g. `ToIsoDate()`) needs a second pass to check *adjacent* formats (e.g.
  timestamps) weren't left uncovered by the same helper.

