# SPEC: plan progress counts as earned money

## Requirement
While the employment toggle is OFF, each unemployed day currently costs one daily rate in the income counter. A day on which plan tasks were done should win back part of that: credit = `that day's daily rate x (tasks completed / tasks due that day)`, over all active plans together. All done = the day nets to zero; nothing due = no credit. It applies to every closed day in the ledger (history included), to today as a live preview, and shows in the sidebar figure and the Reports day/week/month/year income card, with wording that stays sign-consistent and names the credit. Daily-rate rule and start date (2025-12-04) are unchanged.

## Chosen approach
**Derive the credit on read from task data; no new table, column, or migration. `income_ledger` rows and their posting stay exactly as they are.**

- Credit for a closed day D = `-delta x done/total` for ledger rows with `employed = 0` only, where `(total, done) = ScoreService.DayTaskCounts(D)` (existing, public: all active plans summed, skips recurring-excluded weekdays, counts by assigned day after overrides) and `total > 0`.
  - Using the ledger row's own `delta` reuses the stored daily rate (no change to the rate rule; a monthly-income change mid-period doesn't rewrite history).
  - Using the row's own `employed` flag keeps the existing "toggle flips forward only" rule: employed days never get credit and historical unemployed days keep it after a later flip to ON.
- Today (live preview, Reports only): if `!ConfigService.IsEmployed()`, credit = `DailyRate(today) x done/total` from `DayTaskCounts(today)`, total > 0. Employed: 0.
- **Late-completion attribution (decided here): by the task's assigned (due) day, retroactively.** Completions are keyed by `(plan, assigned_day, task)` and `DayTaskCounts` already attributes that way for streaks and the daily score, so a task finished late counts for the day it was due, and that older day's credit grows (the figures update on next refresh). A task pulled forward with "Move to today" changes its assigned day to today, so it credits today. Rationale: one definition of "done on day D" app-wide; a day's ratio can never exceed 100%; it also means catching up on overdue work visibly recovers money.
- Net figure = posted ledger sum + credit (+ today's preview + today's credit for Reports). Credit can never push a day above 0 while unemployed (max 100% of the loss).
- Sidebar remains posted-closed-days only (no today); Reports adds today's live preview. Both rules from DECISIONS.md rule 14 are kept.

Rejected:
1. **Freeze the credit into the ledger at close (new `credit` column / second ledger row).** Needs a migration plus a backfill that would still have to be derived from task data for history, and it freezes a figure that late completions should be able to improve. Also `UNIQUE(date)` on `income_ledger` blocks a second row.
2. **Attribute to the completion timestamp (`completed_at` date).** Numerator could include other days' tasks, so ratio could exceed 100% and the "tasks completed that day / tasks due that day" definition breaks; un-ticking also nulls the timestamp.
3. **Credit when employed too.** Employed days already add the full rate; out of scope per the request.

Known limit (state in docs): only currently-active plans are visible, so history for archived/removed plans yields no credit.

## Exact files to change
- `Services/IncomeService.cs` - add a constructor-independent way to get task counts: take a `ScoreService` as a parameter (e.g. `CreditRange(DateOnly from, DateOnly to, ScoreService score)` iterating ledger rows `WHERE employed=0`; `TodayCredit(ScoreService)`); change `SumForPeriod` to return net plus credit separately (e.g. a small record `(double Net, double Credit)`); add `PostedBalanceWithCredit(ScoreService)` for the sidebar. Keep `EnsureIncomeCaughtUp`/`DailyRate`/`TodayPreview` behaviour unchanged.
- `Pages/ReportsPage.xaml.cs` (~line 148) - pass the existing `score` into the income call; hand net + credit to the card.
- `Pages/ReportsPage.Income.cs` - `IncomeCard` takes the credit; wording below.
- `MainWindow.Startup.cs` `RefreshIncome` (~line 426) - build `PlanStore.LoadActivePlans()` + `ScoreService` (same pattern as `CatchUpScores`) and use the credited balance; label by rounded sign.
- `Services/Database.cs` - none (`IncomeBalance` stays as the raw ledger sum; used by the credited balance as its base).
- No XAML, settings (`config.json`), or SQLite schema changes.
- `Planillium.App.Tests/` - add tests for the pure ratio/credit arithmetic (link the source file per project convention; do not add a ProjectReference).
- Docs: add a sub-bullet to `DECISIONS.md` rule 14 (credit rule, late attribution, employed-days rule) and a `CHANGELOG.md` Unreleased line / `MANUAL.md` mention if those exist for this side.

## Wording
Round the net to cents (`Math.Round(x, 2)`) before choosing sign/wording, so float dust never shows "-€0.00".
- Net < 0: caption `UNEARNED INCOME - {period}`, text "You've lost {abs} {timeWord}, based on a potential ..." (unchanged).
- Net > 0 (employed): caption `EXTRA INCOME`, "gained" (unchanged).
- Net == 0 with credit > 0: caption `NO INCOME LOST - {period}`; text "Plan tasks you completed earned back all of it." Net == 0 with credit == 0: show as today.
- If credit > 0, add a Dim line: "Includes {EUR credit} earned back by completing plan tasks ({timeWord})." (Net < 0 case: the "lost" amount is already net of this credit.)
- Per-off-plan-hour line keeps using the net.
- Sidebar label: `LOST INCOME` if rounded balance < 0; `EXTRA INCOME` if > 0; zero -> `INCOME BALANCE`.

## Edge cases
- App not run for days: catch-up posts ledger rows as before; credit is derived at read time from current completions, so nothing to backfill and nothing lost.
- Toggle flipped mid-period: employed days (row `employed=1`) get 0 credit; unemployed days keep theirs. Today follows the current toggle.
- Monthly income changed mid-period: past days use their stored `delta`; today uses the current rate.
- Values crossing zero: net can reach exactly 0 (all tasks done, whole period) but never go positive from credit; zero-case wording above.
- Day with total == 0 (no tasks due, plan not started, recurring-excluded weekday, all tasks moved off a day-off): no credit, no divide-by-zero.
- A task rescheduled away from D reduces D's total; a task completed late raises D's done; un-ticking reverses it.
- Multiple plans: counts summed across plans, then one ratio (not an average of per-plan ratios).
- Period boundaries: Day = today only (credit = today preview); Week/Month/Year follow `ReportData.PeriodStart`; closed days through yesterday from the ledger plus today, no double-count of today.
- Days before 2025-12-04 never in the ledger, so never credited. Plan dates before a plan's start produce no due tasks.
- Performance: ~300+ days x `DayTaskCounts`; reuse one `ScoreService` per refresh, do not construct per day.

## Acceptance criteria (QA, scratch DB/config only)
1. Unemployed, one closed day, 4 tasks due, 3 done, rate R: that day's contribution = `-R + 0.75R = -0.25R`; all 4 done = 0; 0 done = `-R`; 0 due = `-R`.
2. Two active plans (2 due/1 done and 2 due/2 done) on the same day: ratio 3/4.
3. Employed-flag row (`employed=1`) day with all tasks done: contribution unchanged (`+R`, no extra credit). Flipping the toggle ON afterwards does not change prior unemployed days' credited values.
4. Late completion: task due day D completed on D+2 -> D's contribution improves after refresh; today's figure is not affected by it.
5. "Move to today" of a future task and completing it credits today, not the original day.
6. Sidebar: equals ledger sum + credit for closed days through yesterday only; completing a task today does not change the sidebar until the day closes; Reports Day tab does show today's credit immediately.
7. Reports Day/Week/Month/Year: net = posted + credit + today preview/credit; wording matches the Wording section for net<0, net==0 with credit, net>0 employed; the credit line appears iff credit > 0; no "-€0.00" anywhere.
8. Day with no tasks due gets no credit; no exception thrown; `dotnet build -p:Platform=x64 -c Debug` clean (`/warnaserror` re-run) and `dotnet test` passes.
9. No change to `income_ledger` schema, rows, or posted deltas after a catch-up (diff row dump before/after: identical).
10. Daily-rate formula and start date unchanged (full month of unemployed days with zero tasks still sums to the configured monthly figure).

## Safety notes
- QA works only on scratch copies in its own worktree: never the real `data/progress.db`, real `config.json`, `plans/active/*.json`, or the live running Planillium instance (do not stop/restart it).
- Seed scratch `income_ledger`/`task_completions` rows directly in the scratch DB; delete only the scratch copy's `income_ledger` before first launch if table shape conflicts (per DECISIONS.md rule on the two forks' ledgers).
- Verify mutating logic by inspection + build + scratch DB; no live click-through of completions on real data.

## Open question
None blocking. Product note for the user to confirm later: late completion retroactively improves older days' figures (chosen for consistency with the existing score/streak attribution); and archived-plan history gives no credit.
