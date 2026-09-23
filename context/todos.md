<!--
  Done, or decided not to do (with reasoning) — resolved work only. An item lives in exactly one
  place at a time: open items stay in CONTEXT.md §7; the instant one resolves, it's cut from
  there and pasted here under its outcome. Never compacted, no size ceiling. Tidy, not
  append-only.

  Migrated 2026-09-14 from the pre-split CONTEXT.md's "Session handoff notes" / "Session log" /
  "Open TODOs" (verbatim content, reorganized under dated headers only — nothing rewritten,
  nothing dropped). Blow-by-blow detail for any entry below still lives in git log; this file is
  the index, same as the source it was migrated from.
-->

# Todos — Planillium

## Done

### Project history (pre-WinUI, Python/Tkinter era — retired 2026-07-07/08)

Built 2026-06-28 through 2026-07-06 as a single-file Tkinter app (`main.py` +
`tracker/`, `ticktick/`). Delivered, in order: plan engine + daily view, TickTick
sync, activity tracker + idle detection, weekly reports, multi-plan support, a
spendable score economy, light/dark theming, a UX/accessibility remediation pass,
and a Claude-assisted plan-generation wizard. A 2026-06-29 security audit fixed
credential storage (moved to keyring/Credential Manager), SQLite thread isolation,
OAuth CSRF, and a plaintext-secret git-history leak (rewritten out via
`filter-branch`). Full blow-by-blow detail for all of this lives in git log — every
commit from that era is still there, the source itself is not. Nothing from this
period needs re-reading to work on the app today; the WinUI rebuild ported every
feature that mattered.

### Session log — through 2026-07-29

(full detail in git log): WinUI 3 rebuild landed 07-07 as v1.0.0. Audit
rounds 1-6 (07-09→07-15): `Database.RunInTransaction`, `JsonFileIO` atomic writes,
`PlanStore.IsValidPlanId`, transactional dialogs — the mechanisms every later round built on —
plus completed-task-shift data-loss fix (`DECISIONS.md` rule 7), move-to-today backward
compaction. 07-16: day-off/reschedule overlap fix. 07-17: `TreatWarningsAsErrors`, "Clear all my
data", day-off scoring (rule 10). 07-18→07-22 (tests 19→83): `asOf`-aware streaks; closed-form
`PlanDayForDate`; 42 overlapping `time_diary` pairs found, only 2 matching the known bug — **user's
call: leave untouched**; personal-data git-history purge (134 commits); repo renamed `planillium`,
**v1.1.0 public**; `DispatcherQueueTimer` root-caused (`DECISIONS.md` — Standing lessons); queued
plan ideas (v1.2.0); `ActiveWindowTitle` process-name fallback. 07-23: internal rename
`MentorOverseer`→`Planillium` (3 legacy-compat exceptions, see `context/domain.md`); "Exclusion
Impact" panel removed (rule 12); 26 `ContentDialog` sites unified onto `DialogControls.Build`; four
5-category audits (0 Critical), 86/86 tests. 07-27: app wouldn't start — bisected to
`SetDefaultDllDirectories` breaking WinRT activation, reverted; `HandleIdleReturn` now always logs
"unaccounted time" on detection. 07-28: Diary Edit/Split unreachable via rounded-clip
(`DECISIONS.md` — Standing lessons); plan tasks gained `tools`. 07-29: `SplitDiaryEntryDialog`'s
"+ Add activity" never wired; Diary filters didn't narrow each other.

### Session log — 08-04 → 08-28

(full detail in git log; first date's commits `e4c4f11`/`ee981c0`/`488424f`/`0ffdde4`):
- **08-04/05**: diary window merged into working hours; midnight rollover fixed; "Day X of Y"→
  `Plan.ProgressDay` (rule 13); all 12 scoring rules made editable. `ActivityTracker` God-Object
  split into `NativeInput`/`WindowTitleResolver`/`ActivityClassifier`/`DiaryWriter`. Reports follow
  the period selector; report tables gained all 5 categories + per-row Total (**meaning changed**,
  Year total 80h10m→294h34m). "Asks about absence twice" (#1 of two related dedup bugs — this one
  in `ActivityTracker.PendingDayGap`, fixed via `_openSessionStart`/`_accountedUntil` clamps;
  #2 is 08-17's idle-return duplicate below). `RescheduleTask` now one compact-then-push formula.
  New descriptive `DiaryTag` axis. 144/144.
- **08-07**: Tag got its own diary column. `IdleReturnDialog` gained explicit Category/Tag
  (auto-guesses until touched, then stops) + a Mark-tag toolbar row on bulk diary actions —
  **not yet live-UIA-verified**. Real bug: a diary edit could pre-empt the evening review's write
  and freeze `daily_score` at 0 — fixed (`PersistReview` recalculates); real `score_ledger` rows
  140/138 corrected (user-confirmed, backed up first). Sibling check hardened Enter-key bypass of
  disabled buttons across 4 dialogs. 147/147.
- **08-13**: Day-offs folded into the score card caption. Real finding: `dotnet test -c Release`
  was silently hitting the REAL db (lesson → `DECISIONS.md`) — 154 bad rows deleted after
  backup+confirmation, fixed via a `PLANILLIUM_TESTS` define. Diary's horizontal-scrollbar/
  column-width fix (`DiaryListWidth` staleness) — **not yet live-UIA-verified**. Surfaced a
  genuine 08:00→11:28 gap with no placeholder row — **cause never found, still open**.
  `MaxActivePlans` 2→3. 151/151.
- **08-14**: an archived plan sitting at only 11/23 tasks (short of the 100% Archive requirement)
  turned up back in active — moved back, DB intact, **how it moved stays unconfirmed**.
  `AddPlanDialog` gained a live mad-libs preview; landed on `DialogContentWidth = DialogWidth-64`,
  the pattern every dialog since has followed (08-17/08-18 below). 152/152.
- **08-17**: Schedule's day lists gained click-to-expand (`_collapsedPlans`) — **not yet
  live-UIA-verified**. `SplitDiaryEntryDialog`/`EditDiaryEntryDialog` got the `DialogWidth-64` fix
  + "Quick pick" suggestion chips — **EditDiaryEntryDialog side not yet live-UIA-verified**.
  `SettingsPage.SaveRules` split into two independent save phases so one invalid field elsewhere
  no longer silently discards an already-valid working-hours edit (standing lesson →
  `DECISIONS.md`). Real bug: idle-return's two entry points (toast + tray) could both fire for one
  gap, inserting a duplicate `time_diary` row — fixed via a `DiaryWriter.HasIdlePlaceholder` guard
  at the one convergence point; the duplicate row (6216) deleted, user-confirmed. 152/152.
- **08-18/19**: `IdleReturnDialog` got the same `DialogWidth-64` + quick-pick treatment;
  "frequent answers hardcoded" was a perception issue (real usage data, just static first-row
  ordering), not a bug; Split dialog's horizontal scrolling removed entirely. All confirmed live
  08-19 once traced to the running exe still being an old Debug build.
- **08-28**: diary's Time column widens 40px whenever a date column shows (search/"All time"),
  which 08-13's `DiaryListWidth` recompute never accounted for. Fixed by narrowing 5 other
  columns; verified live. **Known tradeoff, not a bug**: the 900px minimum window still needs
  horizontal scroll — flagged, pending a request.

### Session log — 09-01

("finished 2 tasks yesterday, not counted" — third active plan
`ai-microsolutions-brand-30`): real bug, confirmed via `score_ledger` — 08-31 had every usual
entry except `daily_score`, which was simply missing; the completions themselves saved fine.
Root cause: `daily_score` is only written by the evening review (if it completes) or
`RunStartupCatchUp`'s `EnsureScoreCaughtUp`, which fires once at process launch only. App hadn't
relaunched since (log showed `HandleSleepGap`, not a restart), and 08-31's evening review looks
interrupted (`DialogGate` log shows ~9 min of contention around the second completion), so
neither path ran. Fixed: `MainWindow.Startup.cs`'s `CheckDayChange` (the once-a-minute midnight
watcher) now also runs `CatchUpScores` off-thread before its UI refresh, so a day closing while
the app stays open no longer needs a restart to get scored. Debug 0 errors, 152/152 tests.
**Live-verified**: stopped Release (10:11, clear of EOD window), rebuilt, relaunched — 08-31's
`daily_score` backfilled to +19 within seconds, confirmed via direct `score_ledger` read.
(Separate, not fixed at the time: same log showed a caught `COMException` from
`FlashContentRefresh` — "multiple animations...same property" — on the first `CheckDayChange` tick
after sleep/wake, harmless so far, after `RefreshScore` and inside a catch — tracked as an open
item, see CONTEXT.md §7.)

### Session log — TickTick widget investigation (started 09-01)

("TickTick's own floating widget disappears, seems to happen during sync, started after
Planillium was created"): checked every TickTick-touching file/Win32 call — Planillium never
enumerates/closes another process's window, its only interaction is HTTPS calls on its own OAuth
token, no shared state with the widget's login. Log had one TickTick line all month (a 15s
timeout, 08-29) and successful calls weren't logged, so frequency couldn't be checked against the
report. **Temporary diagnostic added** (user-approved, "remove together" comments):
`Log.Info` bracketing `TasksDueTodayAsync`/`CompleteTaskAsync`/`RefreshAsync` in
`TickTickService.cs`/`TickTickAuth.cs`. Live-deployed 15:36 (PID 20764). **~17:46 same day**:
widget dropped again, no exact time from user. Log showed exactly one TickTick call since 15:36
(15:37:36-40, succeeded in 4s) — quiet 2h+ either side, so inconclusive but leans away from
Planillium. **User's call: keep the diagnostic running.** **Self-recovered** shortly after, on its
own — no Planillium action, no exact recovery time either. Still inconclusive as of the split —
see CONTEXT.md §7 for the still-open watch item and the diagnostic-removal follow-up.

**Closed 2026-09-22: false alarm, widget now working fine.** User confirmed the disappearances
stopped recurring — no further correlation ever found against a Planillium API call in the three
weeks the diagnostic ran, consistent with the original "no shared state" finding above. Per the
open item's own recommendation ("once confirmed or ruled out, remove the three temporary calls
and rebuild Release"): removed the six `Log.Info` diagnostic lines and their explanatory comments
(`TickTickService.cs`'s `TasksDueTodayAsync`/`CompleteTaskAsync`, `TickTickAuth.cs`'s
`RefreshAsync`) — grepped for "temporary, remove together"/"Temporary diagnostic" first to confirm
no sibling calls were missed. `dotnet build -p:Platform=x64 -c Release`: 0 warnings/errors. Live
instance stopped (PID 10864) → rebuilt → relaunched (new PID 17424, confirmed running) —
`end_of_day_summary_time` (20:00) had already passed, so no risk of skipping that day's
evening-review popup. CONTEXT.md §7's item removed (was item 2).

### Session log — 09-04

("Insights says 'this week' under a THIS YEAR/THIS MONTH header, inconsistent"): real
bug, text-only — `totals`/`distractions` feeding the Insights panel were already correctly scoped
to `_period` (Week/Month/Year), only `ReportExport.Suggestions()`'s three sentences hardcoded "this
week" regardless, because that method was written for the weekly HTML export and later reused
as-is by the Reports page for every period. Fixed: `Suggestions()` takes a `ReportPeriod` and
derives its phrase from the existing `ReportData.PeriodName(period).ToLowerInvariant()` (today/this
week/this month/this year); both call sites (weekly export, Reports page) pass their real period.
152/152 tests, Release rebuilt and relaunched 10:20 (well clear of the 20:00 EOD window). Live
verification across all four period tabs is tracked as a resolved-pending-confirmation item — see
CONTEXT.md §7 if it hasn't been eyeballed yet.

### Session log — 09-16

("yesterday the [sidebar balance chip] showed me 292, today it is 202" — reported as a ~90-point
drop): not a bug, and not a drop. Read-only investigation only — no code/DB changes. Ruled out in
order: WAL-visibility mismatch between a direct DB read and the running app (disproven — both read
the identical live value via a throwaway source-linked console harness, same technique as
`Planillium.App.Tests.csproj`, built/run/deleted in `winui/ScoreCheck/`); a different installed
data folder (disproven — live process's exe path resolves via `AppPaths.Root` to this same repo);
`RescheduleTask` retroactively lowering past days (disproven — completed-task exclusion and the
`closeGap` past-day guard both hold); the Reports period `ScoreCard` (live Week/Month totals didn't
match either number). Root cause found only after the user clarified the source was the sidebar
chip: the live on-screen text (read via the tray-minimized-window UIA technique — `DECISIONS.md`,
Verification discipline) was actually **`-202`**, not `202` — a dropped minus sign. The chip is
`Database.ScoreBalance()`, a plain ledger `SUM(delta)`, currently deep negative from accumulated
`replan_overdue`/`overdue_accrual` penalties (`ai-microsolutions-brand-30`'s heavy reschedule
chain), from `EnsureScoreCaughtUp`'s 2026-09-15 09:56:37 bulk backfill of 15 previously-uncredited
`daily_score` days back to 2026-07-13 (`DECISIONS.md` rule 9a) — legitimate credit for already-done
work, not a scoring bug.

**Follow-up same session, exact ledger reconstruction** (user pushed back asking how/why in
detail): `score_ledger` read directly, event by event. End of 09-14 (after that day's `-10`
replan fee): **-360**. The 09-15 09:56:37 batch (15 `daily_score` rows, `Jul13 +34, Jul16 +3,
Jul20 +7, Jul21 +6, Jul23 +5, Jul30 +18, Jul31 +16, Aug3 +9, Aug4 +6, Aug5 +43, Aug6 +31, Aug14 +0,
Aug17 +9, Aug20 +4, Sep14 +2` = +193): **-167**. Through the morning of 09-16 (`-15`
overdue_accrual for 09-15, `-10` replan fee, `-10` today's day score, `+3` a diary-edit
recalculation of 09-15): **-199**, confirmed as the live DB total by direct re-query. **Never
independently confirmed**: the user's remembered "-292" for "yesterday" — the closest real
snapshot either side of it is -360 (before the 09-15 catch-up) or -167 (after); neither matches
exactly, most likely just an imprecise recollection of the number, not a third data point worth
chasing further.

**Mechanism for the 64-day-deep sweep**: `EnsureScoreCaughtUp`'s lookback is `comeback_lookback_days`
(Settings-editable, default 7) — nowhere in the code does a wider, unconditional history scan
exist (confirmed: `AddLedger` for `daily_score` has exactly one call site,
`RecomputeDayScoreCore`, invoked only from `CreditDayScoreIfMissing`'s `EnsureScoreCaughtUp` loop
or single-date `RecalculateDayScore` calls — nothing else can produce a same-second multi-date
batch). Reaching back to 2026-07-13 requires a ~64-day lookback at that moment, not the
config's current value of 7. Corroborating but not conclusive: `config.json`'s file-modified
timestamp (`2026-09-15 09:56:37.37`) matches the ledger batch's `ts` to the same second — strongly
suggesting the "Comeback window (days)" Settings field was set very high (60+) and saved right
before this ran, then reset back to 7 afterward. Not proven from a single live file with no
history; flagged to the user as the likely explanation, not confirmed.

**New real bug found in the process (fixed 2026-09-22, see Session log below)**: the sidebar chip
went stale by exactly the `+3` diary-edit recalculation above — screen read `-202` while the DB
was already `-199`. Traced to `RecalculateDayScore`'s three call sites (`EditDiaryEntryDialog`,
`SplitDiaryEntryDialog`, `ReportsPage.Diary.MarkSelected`) never calling `RefreshScore()`, unlike
every other score-changing action. Self-heals on the next unrelated refresh or restart; DB was
never wrong, only the display.

### Session log — 09-18

**Diary-duplication bug fixed** (screenshot: two overlapping 2026-09-17 12:32-start rows — an
answered `off_plan`/"housework" row 12:32→20:00 (448 min) and an unanswered `idle`/"unaccounted
time" placeholder 12:32→19:59 (447 min), diary IDs 9655/9656). Root cause, confirmed from
`mentor-winui.log`, not speculated: the evening review (`ReviewDialog.Trigger`, 22:59:58,
window on-screen) opened `IdleReturnDialog` for a ~448-min pending gap via
`ReconcilePendingGap`/`PendingDayGap`, but only called `tracker.MarkAccountedThrough(...)` — the
shared guard stopping the background poll loop from re-detecting the same gap — *after* that
dialog's `await` resolved. The background 60s poll loop (`ActivityTracker.PollOnce`) ticks
regardless of the UI thread; the same user input needed to answer the review dialog also reset
the poll loop's own idle timer, so `HandleIdleReturn` fired independently at 23:01:20 for
substantially the same gap, read `_accountedUntil` as still null, and logged its own placeholder
(447 min — the 1-min difference is `(int)` truncation on a sub-second `_idleSince` vs. the
diary's minute-precision last-row-end). Two independent detectors raced for one gap because the
guard was claimed too late. **Fix**: in `Dialogs/ReviewDialog.cs`'s `ReconcilePendingGap`, moved
the `MarkAccountedThrough` call to *before* `await IdleReturnDialog.ShowAsync(...)` instead of
after — claims the high-water mark the instant the gap is detected, not once the user finishes
answering. Verified: `MarkAccountedThrough` has exactly one call site (checked — no sibling fix
needed); Debug and Release both build clean (0 warnings/errors); live Release instance
stopped/rebuilt/relaunched (new PID confirmed running). `dotnet test`: 151/152 — the one failure
(`ActivityTrackerPendingGapTests.OpenSessionClampsTheGapToItsOwnStart`) is unrelated to this fix
(untouched file, pre-existing) and is itself time-of-day-dependent: it subtracts up to 180 min
from `DateTime.Now` without pinning a fixed instant, so run it inside ~03:00 after midnight and
the shifted timestamp crosses into the previous day, no longer matches today's date, and
`PendingDayGap` finds no gap. Confirmed by re-running at 2026-09-18 02:29; not fixed here (out of
scope for this bug), flagged as a real but separate test-suite flake — CONTEXT.md §6 candidate.

### Session log — 09-22

**Sidebar balance chip stale after diary-category edit fixed** (previously CONTEXT.md §7 item 1,
opened 2026-09-16). Root
cause: `RecalculateDayScore`'s three call sites — `EditDiaryEntryDialog` (save + delete),
`SplitDiaryEntryDialog`, `ReportsPage.Diary.MarkSelected` — called
`ScoreService.TryRecalculateDayScores` to update `score_ledger` but never told the sidebar to
re-read it, unlike every other score-changing action (task completion, reschedule, spend,
day-off, midnight rollover), which all call `MainWindow.RefreshScore()`. Never wrong in the
database, only stale on screen until the next unrelated refresh. **Fix**: added
`(App.MainWindow as MainWindow)?.RefreshScore();` immediately after each of the four
`TryRecalculateDayScores` call sites (`EditDiaryEntryDialog.cs` ×2, `SplitDiaryEntryDialog.cs`,
`ReportsPage.Diary.cs`) — same pattern already used elsewhere in these files
(`TeachPlanTools.cs`/`ReportsPage.Diary.cs`'s existing `(App.MainWindow as MainWindow)?.` calls).
Verified: grepped `TryRecalculateDayScores` for all call sites first (5 results: 4 call sites +
the method definition) so all four got the fix, not just the three CONTEXT.md named — no sibling
left unfixed. `dotnet build -p:Platform=x64 -c Debug`: 0 warnings/errors. `dotnet test`: 160/160.
Not live-UI-verified (would require editing/splitting/mass-recategorizing a real diary entry
against `data/progress.db`) — code-inspection + clean build only, per the real-data rule; folded
into CONTEXT.md §7 item 4's UIA-verification backlog.

**`ActivityTrackerPendingGapTests` time-of-day flake fixed** (previously CONTEXT.md §7 item 8,
opened 2026-09-18, confirmed failing 2026-09-18 02:29 — see Session log 09-18 above). Root cause:
all 4 tests in the file, not just the one that had actually failed, subtracted a fixed number of
minutes from real `DateTime.Now` to build their "last diary end"/"open session start" timestamps
with no pinned reference instant — running within ~3h after midnight could shift a computed
timestamp into the previous calendar day, tripping `PendingDayGap`'s own `lastEnd.Date !=
now.Date`-shaped same-day guard and making it report no gap where the test expected one. A
pin-the-test-to-noon-only fix was considered and rejected: `PendingDayGap` would still internally
call real `DateTime.Now` for `gapEnd`, which could land *earlier* than a synthetic noon-based
`lastEnd`/`openStart` if the suite happened to run in the morning, silently swapping one flake for
another. **Fix**: added an optional `DateTime? asOf = null` parameter to
`ActivityTracker.PendingDayGap` (defaults to real `DateTime.Now` for the one production caller,
`ReviewDialog.cs`, which never passes it) — same override pattern already established by
`ScoreService.CurrentStreak(DateOnly? asOf = null)`. Added a `FixedNow()` helper (`DateTime.Today
.AddHours(12)`) to `ActivityTrackerPendingGapTests.cs` and passed it explicitly as `asOf` at all 5
`PendingDayGap` call sites across the file's 4 tests, fully decoupling the suite's simulated
timeline from the real wall clock rather than just narrowing the flake's window. Verified:
`dotnet build -p:Platform=x64 -c Debug`: 0 warnings/errors. `dotnet test`: 160/160. Not live-rebuilt/
restarted — the only change reaching the shipped app (`ActivityTracker.cs`) is an added optional
parameter with a real-clock default, behaviorally inert for the app's one real caller, which never
passes it; test-only-observable, so the live-verify cycle used for the TickTick and sidebar-chip
fixes above was judged unnecessary here.

**Live UIA verification pass, 2026-09-22** (previously CONTEXT.md §7 items 4 and 6). Restored the
live Release window from tray (`ShowWindow`/`SetForegroundWindow` on its hwnd via `Get-Process`
PID enumeration — `MainWindowHandle` reports 0 while tray-hidden, had to `EnumWindows` by PID to
find the real "Planillium" window) and read it read-only via `System.Windows.Automation`, hidden
back to tray afterward. Confirmed live and correct: sidebar "LOST INCOME" chip (-€25 928,71) and
Reports "UNEARNED INCOME" cards on all four period tabs (Day/Week/Month/Year each showing their
own period's figure, e.g. "-€90,00 today" vs "-€23 580,00" this year) — closes the lost-earnings
Rec from the 09-22 entry above. Insights section correctly relabels itself per tab (THIS
WEEK/MONTH/YEAR/TODAY, each with its own distraction figure) — closes former item 6 outright.
Reports DayOffs figure ("5 days off marked"), narrowed diary columns + Auto scrollbar, and the
diary Mark-tag toolbar row (real tag names, not placeholders) all confirmed rendering. Schedule
page's per-plan cards report `"<plan name>, expanded"` via their `Group` name — collapsible-card
wiring confirmed live. **Blocked, by design, not by oversight**: invoking an "Edit entry" button to
open `EditDiaryEntryDialog` (to check its quick-pick chips) was refused by the auto-mode
classifier itself ("Modify Shared Resources") — simulating a click into a data-editing control is
exactly what this project's own real-data rule says not to do, so `IdleReturnDialog`
Category/Tag and `EditDiaryEntryDialog` quick-pick chips stay code-inspection-only; narrowed
former item 4 to just those two, rather than closing it outright.

**Reports income card: added €/off-plan-hour, 2026-09-22** (user request, same session as the
lost-earnings-counter feature and its UIA pass above). Derived, not a separate calculation:
`sum / (offMin / 60.0)`, reusing `IncomeCard`'s existing period `sum` and `ReportData.PeriodStats`'
existing `OffMin` (already computed once per page load and passed down, no new query). Guarded
against `offMin == 0` (a period with no off-plan time shows no rate line at all, rather than a
misleading €0,00). Files touched: `ReportsPage.xaml.cs` (`IncomeCard` now takes `offMin`, adds the
derived line). `dotnet build -p:Platform=x64 -c Debug`: 0 warnings/errors. `dotnet test`: 160/160.
Live-UIA-verified across all four period tabs post-relaunch (PID 17424 → 8720): Day -€36,99/h,
Week -€16,90/h, Month -€28,32/h, Year -€177,36/h, each consistent with that tab's own lost-income
and off-plan-hours figures. `CHANGELOG.md`/`MANUAL.md` updated — also backfilled the lost-earnings
feature's own entry in both, since it had shipped earlier this session without either being
updated at the time.

**Agentic-workflow pilot, lost-earnings-counter, both sides complete — 2026-09-22** (new session,
started inside `Planillium/` this time, which unblocked the prior session's stuck dispatch —
see `CONTEXT.md` §7 item 7 for the mechanical blocker). Planner → Coder → QA ran end to end:
Planner wrote `winui-agentic/pilot-specs/lost-earnings-counter/SPEC.md` from `DECISIONS.md` rule
14 alone (no regular-side code read); Coder built it into `winui-agentic/Planillium.App/` —
clean build, 156/156 tests; QA (isolated worktree) verdict **PASS WITH CAVEATS** on all 13 spec
acceptance criteria, logged to the shared Dashboard. Caveats: Coder's own tests skipped the
non-retroactive sign-flip regression case (criterion 7) though QA independently confirmed the
behavior itself is correct; SPEC criterion 10 was unsatisfiable as worded for multi-month ledgers
(spec-precision issue, not a Coder defect). Cleaned up QA's leftover empty worktree folder +
unused branch (`worktree-agent-a2e43445a33e4a021`) afterward. **Surfaced, not yet resolved**:
Planner flagged that `CONTEXT.md` §7 item 7 had accumulated post-fork implementation detail about
the regular-workflow side beyond what `PILOT.md`'s contamination rule ("CONTEXT.md is shared
ground") assumed was safe for it to read — asked the user how to handle this before the next
pilot feature runs (keep post-fork write-ups out of `CONTEXT.md`, vs. narrowing what Planner
reads); this session's own doc-currency pass added more such detail to §7 in the same tension,
making the fix more pressing, not less.

### Session log — 09-23

**Agentic-pilot contamination question resolved: narrowed Planner's read access, not just the
write-side rule.** The 09-22 open question above (CONTEXT.md §7 item 7) came back to the user, who
chose narrowing reads over continuing to rely on a "don't write implementation detail in
CONTEXT.md" convention that had already lapsed once, in the same session it was written. Changed:
`.claude/agents/planillium-planner.md` step 2 no longer tells Planner to read `CONTEXT.md`, and
explicitly instructs it not to; `winui-agentic/PILOT.md`'s contamination rule now names only
`DECISIONS.md` and `context/domain.md` as Planner-readable shared ground, with a note explaining
why `CONTEXT.md` was dropped (narrative handoff doc vs. the other two's stable
lookup/reference-register shape). `planillium-coder.md`/`planillium-qa.md` checked — neither ever
instructed reading `CONTEXT.md` (only mention it in their description field, naming where the
pilot itself is documented), so no change needed there. Logged as a general lesson in
`DECISIONS.md`'s "Agentic-pilot isolation" standing-lessons entry: prefer narrowing what can be
read over trusting what won't be written, wherever a boundary actually matters. `CONTEXT.md` §7
item 7 marked resolved with a pointer here.

**Top Distractions now shows a per-row EUR cost** (commit `3a07ddd`, pushed to `origin/master`).
Reused the income card's existing per-off-plan-hour rate (period income sum ÷ period off-plan
minutes) rather than a separate query, applied to each row's own minutes — `ReportsPage.xaml.cs`
lifts `IncomeService.SumForPeriod` up to `Render()` once and passes it into both the income card
and `DistractionList`, so the two figures can't drift apart. `CHANGELOG.md`/`MANUAL.md` updated.

**First side-by-side visual comparison of the pilot vs. regular app, same feature (lost-earnings
counter).** Ran both live at once: regular app as the normal Release instance on real data;
`winui-agentic` built Debug and pointed at a scratch copy of real data via `MENTOR_ROOT`/
`MENTOR_INSTANCE_SUFFIX=agentic_compare` (the existing scratch technique, `DECISIONS.md` "Safety
around real data"), so nothing it does touches the live database. Surfaced a real finding, not a
pilot code bug: both forks independently added their own `income_ledger` table after the fork
point with the same name but a different amount column (`delta_eur` vs `delta`) — feeding the
pilot a copy of the real, `winui`-shaped database made every one of its income queries throw
`no such column: delta` for Week/Month/Year and left the sidebar chip stuck on "—". Fixed by
dropping `income_ledger` from the scratch copy only, letting the pilot recreate it in its own
shape and backfill fresh — not a code change on either side. Logged as a general lesson in
`DECISIONS.md` ("Safety around real data"): a copied live database only works for tables that
predate whatever fork it's being fed into. Also fixed in passing: the regular app's Top
Distractions spacing between hours and the new EUR figure, widened per user request.

### Resolved-and-closed, one-line pointers (prose in git log)

`MentorOverseer`→`Planillium` rename 07-23; diary-tracking-gap bug 07-21 (`PollOnce` order);
Reddit auto-publishing for `posting-plan` dropped 07-22 (dormant tool at
`posting-plan/tools/reddit-publish/`); `PlanDayForDate` closed form 07-18; TickTick secret rotated
07-09, reconnected 08-04; personal-data git-history scrub 07-18; v1.1.0 + GitHub Release + repo
flipped Public 07-21; duplicate repo deleted 07-21; tray icon vanishing — confirmed fine 08-04;
08-18's dialog-width/quick-pick fixes looked unapplied 08-19 only because the live exe was still an
old Debug build — rebuilt Release, confirmed live; **the 08-04 tracker split — confirmed exercised
live**: log shows `HandleSleepGap`/`HandleIdleReturn`/`HandleActiveSession` all firing correctly
through 08-07.

## Decided not to do / settled without action

**42 overlapping `time_diary` pairs, 06-29→07-16** (2026-07-18) — only 2 match
`HandleActiveSession`, the rest unconfirmed; **user's call: leave untouched**.

**`ActivateQueuedPlan`'s non-atomic write-then-delete** — accepted as-is, not worth the added
complexity of a transactional rewrite for the risk it carries.

**~150-230MB memory footprint** — accepted: mostly `NavigationCacheMode="Enabled"` + WinUI3
baseline, no leak found — leave as-is.
