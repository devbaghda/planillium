# SPEC: hour-value-correction

## Requirement
Reports shows an inflated EUR cost per off-plan hour because the rate is the period's whole
calendar-day lost income divided by only the off-plan hours. Replace it with one fixed hour
value: configured monthly net income (`ConfigService.PotentialMonthlyIncomeEur()`) / 168
(21 working days x 8 h; about EUR 16.07/h at 2,700). Use that single value for BOTH the
per-row EUR in "Top distractions" and the "That's EUR X per off-plan hour" line on the
Unearned/Extra income card, so they always agree. The 168 lives in one named constant. The
income card's headline total (calendar-day ledger sum, `IncomeService.SumForPeriod`) and the
sidebar chip are NOT changed.

## Chosen approach
Add to `IncomeService` (plain C#, already test-linked) a public const `WorkingHoursPerMonth = 168`
(doc comment: 21 days x 8 h, user decision 2026-10-01) and a static
`HourValueEur()` => `ConfigService.PotentialMonthlyIncomeEur() / WorkingHoursPerMonth`.
Both Reports builders call `IncomeService.HourValueEur()`; neither derives a rate from
`incomeSum` / `offMin` any more.

Sign: existing display is signed (lost = negative, e.g. "-EUR 5,353"; gained = positive). Keep
that: sign follows the sign of the period income sum already passed to the page (`sum < 0` =>
negative, `sum > 0` => positive). When the sum is exactly 0, show a positive value (no loss/gain
to colour it; flagged in Open question). Magnitude is always hourValue x hours, never
sum-dependent. Signing helper: one small private static in ReportsPage (e.g. `SignedHourValue(double
incomeSum)`) used by both call sites so they cannot drift.

Rejected:
- Add a config key (`hours_per_month`): scope creep; user settled a fixed 168 in code. Not a setting.
- Compute hour value inline in each of the two builders: two places to drift; request demands one place.
- Change `DailyRate`/ledger to working-day basis: explicitly out of scope; the card total stays.

## Exact files to change
- `winui-agentic/Planillium.App/Services/IncomeService.cs`: add `WorkingHoursPerMonth` const and static `HourValueEur()`.
- `winui-agentic/Planillium.App/Pages/ReportsPage.Income.cs`: `IncomeCard` per-hour line = signed `HourValueEur()` (not `sum / (offMin/60)`); keep the `offMin > 0` guard.
- `winui-agentic/Planillium.App/Pages/ReportsPage.TimeByApp.cs`: `DistractionList` uses `HourValueEur()/60 x minutes` (signed); drop the `periodOffMin`-based rate; update the doc comment. Signature may drop `periodOffMin` (keep `periodIncomeSum` for sign); update the call at `ReportsPage.xaml.cs:181`.
- `winui-agentic/Planillium.App/Pages/ReportsPage.xaml.cs`: only the `DistractionList(...)` call-site if the signature changes.
- `winui-agentic/Planillium.App.Tests/IncomeServiceTests.cs` (or a new sibling test file linking the same sources): tests below.
- Docs, same pass: `MANUAL.md` income/per-off-plan-hour paragraph (root MANUAL.md line ~136 describes the old per-hour breakdown; update the agentic copy if one exists in the fork, otherwise note it in the handback), `CHANGELOG.md` Unreleased, `DECISIONS.md` rule 14 gets a line recording 168 h/month hour value (do not renumber).
- No SQLite schema change, no migration, no `config.json` change.

## Edge cases
- Monthly income config changed: hour value follows current config immediately (not retroactive-stored; the display is computed live). Intended.
- Config income missing => default 2700 => 16.0714/h.
- `offMin == 0`: income card hides the per-hour line (as today); distraction list is not shown at all when empty.
- Income sum = 0 (e.g. Day tab where preview is 0 not possible with income>0; only if monthly income = 0): hour value 0 => EUR figures are 0; either show "EUR 0" or omit; choose omit EUR column when hourValue == 0 (matches current "perMinuteEur != 0" guard).
- Sign crossing: employed toggle flip mid-period makes the period sum mixed; sign follows the net sum; magnitude unaffected.
- All four periods (Day/Week/Month/Year): per-hour value is identical across tabs for the same config; only hours vary. Per-row EUR = rounded display of minutes/60 x hourValue.
- Row EURs are not required to sum to anything on the card (the card total is calendar-day based); do not add a reconciliation.
- Rounding: `FormatEur` as today; no extra rounding before formatting.

## Acceptance criteria
1. `IncomeService.WorkingHoursPerMonth == 168` is the only literal 168 in the app code (grep `168` in `Planillium.App/**/*.cs` outside obj/ finds only that line).
2. With monthly income 2700: `HourValueEur()` = 16.0714 (+/-0.001); income card line reads "That's -EUR 16 per off-plan hour" style value (FormatEur of -16.07) when lost, positive when gained.
3. Top distractions row with 37.3 h shows about 37.3 x 16.0714 = 599.5 in magnitude (sign per rule), NOT 5,353.
4. For any period, the card's per-hour figure equals (row EUR / row hours) for every distraction row within rounding.
5. Changing `potential_monthly_net_eur` to 3360 in a scratch config gives exactly 20.00/h on both.
6. The card's headline total, "based on a potential ..." line, and sidebar income chip are byte-for-byte unchanged vs. before for the same data.
7. Unit tests: `HourValueEur` at 2700 and 3360; sign helper for sum <0, >0, ==0; passing `dotnet test` from `winui-agentic/Planillium.App.Tests/`.
8. `dotnet build -p:Platform=x64 -c Debug` (and Release) from `winui-agentic/Planillium.App/` clean, `/warnaserror` clean; no remaining references to a `sum / offMin`-derived rate (grep `perMinuteEur`, `perHour`).
9. No file under `Planillium.App/` references `winui/Planillium.App/`; no schema/config/migration diff.

## Open question
Sign when the period income sum is exactly 0: spec picks positive. Product owner may prefer
following `ConfigService.IsEmployed()` instead. Low impact (only occurs with monthly income 0
where the figure is 0 anyway), so the default stands unless the owner objects.

## Safety notes
- QA works only on scratch copies in its worktree: scratch `data/progress.db` and scratch `config.json`; never the real `data/`, real `config.json`, or the live running instance. Do not kill/relaunch the user's running Planillium.App.exe.
- Change monthly income only in the scratch config.
- Do not write to `income_ledger` outside the app's own code in the real DB; verify display math by unit tests and code inspection, not by mutating real data.
- Do not read or compare against `winui/Planillium.App/` (pilot contamination rule).
