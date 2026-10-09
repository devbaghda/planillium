# SPEC: Replace remaining tasks (feature slug `replace-remaining-tasks`)

**Has a screen: yes.** A new button on the Plans page active-plan card plus a two-step dialog (paste, then preview/confirm). The Designer adds a **Design** section below; may return a Conflict with plan.

## Requirement

On an active plan, one action "Replace remaining tasks…" lets the user keep every task already ticked complete (untouched, same days) and swap every other task (overdue, today, future) for a new list pasted as JSON from Claude. The dialog supplies a copyable prompt that lists the done tasks so Claude rewrites only the rest. Before anything changes, a preview states unfinished tasks removed, new tasks added, and old vs new finish date; nothing is written until the user confirms. New tasks start on (assigned day of the last done task) + 1, or day 1 when no task is done, keeping the pasted relative spacing. The start is not clamped to today's plan day (user decision 2026-10-09): on a far-behind plan the new tasks start at the old backlog's next day and show as overdue. Plan length, finish date, "Day X of Y" and the drift figure all follow the new list, and drift restarts at 0.

## Chosen approach

- **Surgical JsonNode patch of the plan file** (same pattern as `PlanStore.AddTask`/`SetExcludedWeekdays`), never a Plan-model round trip, so briefing and unmodelled phase fields survive (AC 9). Steps: classify each task done/not-done; remove not-done tasks from their phases; drop phases left empty; append the pasted phases (tasks passed through verbatim, only `day` rewritten); set `total_days` to the last new task's day; write atomically with `JsonFileIO.WriteAllTextAtomic`.
- **"Done"** = `Database.LoadCompletions()` has `(planId, assignedDay, text) == true`, where assignedDay is the override-adjusted day (same rule as `PlanStore.TasksFor`). Done tasks' own overrides and completion rows are not touched, so they do not move. "Last done day" = max assigned day among done tasks (0 if none).
- **New-task day mapping:** `start = lastDoneDay + 1` (lastDoneDay = 0 when nothing is done, so start = 1); `newDay = start + (pastedDay - minPastedDay)`. `plan.PlanDay` is not used. Pasted days may begin at 1 or anywhere; only relative spacing counts.
- **`total_days` must be set explicitly.** `TotalDaysComputed` prefers the file's `total_days` over the max task day, and `DriftDays` compares against it, so leaving the old value would break "Y" and the drift reset. Set it to the last new task's day (also covers the case where the file had no `total_days`). Drift then reads 0, since the last assigned day equals the new total, provided no override on a kept done task exceeds it. `start` guarantees every new day is greater than every done assigned day.
- **Database cleanup of removed tasks** (after the file write succeeds): delete `task_overrides` rows for the removed titles in this plan (AC 11: a stale override would otherwise relocate a new task that reuses a removed title, a phantom shift), and delete `task_completions` rows with `completed = 0` for removed titles in this plan. Do NOT delete `completed = 1` rows, `score_ledger`, or `time_diary` (AC 10). Run in `Database.RunInTransaction`. Order is file first, DB second, so a failed file write leaves everything unchanged; if the DB cleanup then fails, log it and tell the user.
- **Pasted JSON** is parsed like `AddPlanDialog.TryImport` (accept a ```json fence or a bare `{...}`) and reads only `phases` from it; `id`/`name`/`briefing`/`color` in the paste are ignored (the plan's own are kept). Required: `phases` non-empty with at least one task; each task needs a non-empty `task` and integer `day` >= 1.
- **Two-step UI:** step 1 dialog (prompt + Copy, reply box, inline error, primary "Preview"); an invalid paste sets `args.Cancel = true`, shows the error, nothing changes. Step 2 is a confirmation dialog with the preview text; Cancel there writes nothing (file byte-identical, AC 5). Use `DialogControls.Build` and `DialogGate.ShowAsync` like the other dialogs; reuse the AddPlanDialog width fix (`ContentDialogMaxWidth` override, content width = dialog width - 64).
- **Prompt text** lives in `PlanTemplates.cs` as a new template/builder taking plan name, the done tasks (day + title) and the "start day" so Claude knows what is fixed. It asks for the same JSON shape as Add Plan (including `mentor_note`, `tools`), phases and tasks for the remainder only, and not to repeat done tasks.
- **All pure logic goes in a new plain-C# file, `Services/PlanReplacement.cs`** (no WinUI types), so it can be source-linked into the test project like the other services. The dialog and page only call it.

**Rejected alternatives**
- *Per-task editing UI*: ruled out by the user (bulk replace only).
- *Rewrite whole file from the Plan model*: would drop briefing/phase extras (AC 9).
- *Put all new tasks into the last phase*: loses the phase structure Claude produces and the pasted phase names; appending the pasted phases is as cheap.
- *Leave `total_days` alone and rely on task max day*: wrong whenever the file carries `total_days` (most Claude-generated plans do).
- *Keep stale overrides*: unsafe, see above.

## Exact files to change

1. `Planillium.App/Services/PlanReplacement.cs` (new): `ParseReplacement` (paste to validated phases, or an error string), `BuildPreview` (removed count, added count, start day, old finish date, new finish date), `Apply`; includes the refuse-if-nothing-to-replace check.
2. `Planillium.App/Services/PlanStore.cs`: a small helper if needed to read the raw file as JsonObject (reuse private `PlanFilePath`); no change to existing methods.
3. `Planillium.App/Services/Database.cs`: new method to delete overrides and incomplete completion rows for a plan and a set of titles (no schema change, no migration).
4. `Planillium.App/Services/PlanTemplates.cs`: new replace-remainder prompt builder.
5. `Planillium.App/Dialogs/ReplaceRemainingDialog.cs` (new): step 1 and step 2 dialogs, returns true if applied.
6. `Planillium.App/Pages/PlansPage.xaml.cs`: in `PlanCard`, add the "Replace remaining tasks…" button right after "+ Add task" (insert a grid column; shift Excluded days/Archive/tools column indices accordingly), `AutomationProperties.SetName` = `Replace remaining tasks: {plan.Name}`, on success `Render()` and `(App.MainWindow as MainWindow)?.RefreshScore()`. No XAML file change (cards are built in code). No settings or config.json change.
7. `Planillium.App.Tests/Planillium.App.Tests.csproj`: add `<Compile Include="..\Planillium.App\Services\PlanReplacement.cs" Link="App\PlanReplacement.cs" />`; do not add a ProjectReference.
8. `Planillium.App.Tests/PlanReplacementTests.cs` (new): covers the fixture and the edge cases below against a scratch temp dir/DB (`MENTOR_ROOT`/`PLANILLIUM_TESTS` conventions in the existing tests).
9. Docs for this side: `context/todos.md` dated entry, `CONTEXT.md` section 2 line, `MANUAL.md`/`CHANGELOG.md` if the agentic side keeps them.

## Edge cases the build must survive

- Plan far behind (59 days old, 4 done): start = 5 (last done day + 1), not today's plan day (60). The new tasks are therefore overdue on arrival. This is the user's decision (2026-10-09), reaffirmed after being warned.
- Zero done tasks: lastDoneDay = 0, so start = 1 regardless of the plan's age (AC 12). Overdue tasks follow from the same rule.
- All tasks done: refused with a clear message ("Nothing to replace: every task is already done"); no dialog change, no write (AC 13).
- Done task rescheduled (override): its assigned day is what counts for "last done day"; its override row stays.
- Done task whose original day is later than a not-done task's day (done out of order): last done day is the max, so new tasks start after it.
- Pasted title identical to any done task's title in this plan: reject (AC 14). Compare trimmed, case-insensitive. Planner addition: also reject duplicate titles inside the pasted list (overrides are keyed by plan+title only). Also reject if a pasted title equals a removed title? No, allowed.
- Pasted days not starting at 1, unsorted, or with gaps: relative spacing kept via `minPastedDay`; sort not required.
- Invalid or incomplete JSON, missing `phases`, zero tasks, task without `task`/`day`, non-integer or < 1 day: inline error, dialog stays open, nothing changed (AC 3).
- Plan has `excluded_weekdays`: use `plan.DateForPlanDay` for the finish dates in the preview; plan-day arithmetic is already exclusion-aware.
- Today is an excluded weekday: no effect on the start day (it no longer depends on `PlanDay`); finish dates still go through `DateForPlanDay`.
- Plan not yet started (`PlanDay <= 0`): no effect; start = lastDoneDay + 1 (= 1 when nothing is done).
- New list shorter or longer than old: `total_days` set to the new last day either way; "Day X of Y", Plans card "Originally due", sidebar "Finishes" and drift line all derive from it (check `PlansPage`, `TodayPage`, `SchedulePage` line 137, `PlanDriftCard` all read `TotalDaysComputed`/`CurrentEndDate`; no other cache).
- Plan file without `total_days`: set it anyway.
- Done task rows with `completed = 0` history for removed titles are removed; `completed = 1` rows, score ledger and diary untouched (AC 10).
- Marked days off (`plan_days_off`) for future days are left as is (they are day numbers, not tasks); noted, not changed.
- App not run in a while / settings changed mid-period: nothing time-based is stored; the start is computed from the ticked tasks at confirm time (compute again at confirm, not when the dialog opened, in case a task was ticked or unticked in the meantime). The preview figure must match what Apply writes.
- File write failure: no partial change; DB cleanup failure after a successful file write: log and show an error (stale overrides are the risk).
- Do not touch or reorder done tasks inside their phases; phases that keep a done task keep all their unmodelled fields.

## Acceptance criteria (copied verbatim from ACCEPTANCE.md)

Acceptance — replace remaining tasks (run only against a scratch MENTOR_ROOT, never real data)

Fixture: a plan with 10 tasks on days 1-10; tasks 1-4 ticked complete, 5-10 not; task 7 has a
reschedule override; pasted replacement JSON contains 5 tasks.

1. An active plan's page shows a "Replace remaining tasks…" button next to "+ Add task".
2. The dialog shows a copyable Claude prompt that lists the 4 done tasks.
3. Pasting invalid JSON shows an error in the dialog; nothing is changed; dialog stays open.
4. Pasting valid JSON shows a preview "6 unfinished tasks removed, 5 new tasks added" before applying.
5. Cancel at the preview leaves the plan file byte-identical.
6. Confirm: tasks 1-4 are still present, on the same days, still ticked complete.
7. Confirm: tasks 5-10 no longer exist in the plan file; the 5 new ones exist.
8. The 5 new tasks have days starting at (day of last done task) + 1, keeping the pasted relative spacing. (Fixture: last done is day 4, so start = 5.) This holds even for a plan that is far behind (59 days old: start is still 5; the new tasks then show as overdue — user decision 2026-10-09).
8a. After confirm, the plan's total length equals the last new task's day (shorter or longer than before); "Day X of Y" shows that Y; the sidebar "Finishes" date is the calendar date of that last day; the sidebar drift line reads on-track (0 d late), not the old lateness.
8b. The preview shows the new finish date and the old one.
9. Plan file fields the app doesn't model (briefing, extra phase fields) survive untouched.
10. Past score history (daily_score, completed-task rows for 1-4) is unchanged.
11. The removed tasks' reschedule overrides no longer affect anything (no phantom task appears).
12. A plan with zero done tasks: new tasks start at day 1.
13. A plan with all tasks done: action is refused with a clear message (nothing to replace).
14. Pasted JSON with a task title identical to a done task's title is rejected (would collide with its completion record).
15. Queued/archived plans do not offer the button.
16. Build clean (0 warnings with /warnaserror); existing tests still pass.

**Planner additions (labelled as mine):**
- P1. Pasted JSON with a duplicate title inside the pasted list is rejected with an error.
- P2. After confirm, the plan file still parses and loads in the Plans page without a "failed to load" InfoBar; `briefing` and `excluded_weekdays` are unchanged.
- P3. The preview figures equal what is actually written (start day recomputed at confirm time).
- P4. Overrides and incomplete completion rows for the removed titles are gone from the scratch DB; overrides for done tasks remain.
- P5. New unit tests for `PlanReplacement` are added and pass.

## Safety notes (for QA)

- Never run against the real `data/progress.db`, root `config.json`, `plans/active/*.json`, or the live app instance. Use a scratch `MENTOR_ROOT` in the worktree with a fixture plan file and scratch DB (see `DECISIONS.md` standing lessons, scratch-`MENTOR_ROOT` technique).
- This feature rewrites plan files and deletes DB rows, so it must only ever be exercised on scratch copies; confirm `AppPaths` resolves to the scratch root before clicking Confirm.
- Tests must use the `PLANILLIUM_TESTS` define / scratch paths so `dotnet test -c Release` cannot hit real data.
- Do not touch `winui/` or the regular side's documents.
- Only one of the two apps should run at a time, and neither against real data.

## Open question

1. **Zero done tasks on an older plan. SETTLED 2026-10-09 (user decision):** start = day 1 regardless of plan age, same as AC 12. The earlier "never earlier than today's plan day" rule is withdrawn.
2. **Task notes (`task_notes`) for removed titles** are left in place (a new task that reuses a title would show the old personal note). *(Orchestrator: decided, task_notes stay untouched. Do not delete them.)*

---

# Design

*Designer. Skill used: `ui-ux-pro-max` (WinUI stack rules: visible labels, error next to the field, destructive action never the default, text plus colour for state). Matched to existing code: `AddPlanDialog.cs` (prompt box + Copy + reply box + inline error, 640 px dialog), `PlansPage.PlanCard` (button row), `DialogControls.Build` confirmations (Archive). Nothing here is a new visual language. Visual layer is a placeholder until the user has reviewed it.*

## D1. The button (Plans page, active-plan card)

- Plain default-style `Button`, same size, font and vertical alignment as its siblings. No accent fill, no icon, no emoji.
- Visible text exactly: `Replace remaining tasks…` (single ellipsis character U+2026, matches "Excluded days…").
- Position: immediately right of `+ Add task`, before `Excluded days…`. Order in the row left to right: Briefing (when present), + Add task, Replace remaining tasks…, Excluded days…, Archive/other existing items unchanged.
- ToolTip: `Keep the ticked tasks and replace all the others with a new list from Claude`.
- Always enabled on an active plan, including when everything is done (the click then shows the refusal, D4). Cards for queued ideas and archived plans never get it (AC 15).
- Width: buttons never shrink or wrap their label; the plan-name/meta column (the star column) is what gives way. Coder checks the card at the app's minimum window width and confirms no button is clipped; if the row cannot fit, wrap the whole button group to a second line under the plan name rather than truncating a label.

## D2. Step 1 dialog: "Replace remaining tasks"

Same construction as Add Plan: `DialogControls.Build`, `ContentDialogMaxWidth` = 640, panel `MinWidth` = 576 (640 - 64), body in a `ScrollViewer` with `MaxHeight = 560`, vertical `StackPanel` `Spacing = 10`. Do not invent other widths.

Title: `Replace remaining tasks`

Body, top to bottom (all one column, full width, left-aligned):

1. **Summary line** (13 px, secondary text brush, wrapping, `MaxWidth` 536):
   - Normal: `{plan name}: {D} tasks done stay exactly as they are. The other {N} tasks will be replaced.`
   - Singular forms: `1 task done stays...` / `The other 1 task will be replaced.`
   - Zero done: `{plan name}: no tasks are done yet, so the whole plan will be replaced.`
2. **Step caption** (12 px, opacity 0.7): `1. Copy this prompt into claude.ai.`
3. **Prompt box**: read-only multi-line `TextBox`, `Height = 110`, wrapping, `Header = "Prompt for Claude"`. Filled the moment the dialog opens (no "Generate" button; there is nothing to fill in). It lists every done task, one per line as `Day {n}: {title}` in day order; with none done the list reads `(none yet)`. UIA name `Prompt for Claude`.
4. **Copy button** directly under the prompt box, left-aligned: `Copy`; after click `Copied ✓` (same as Add Plan). Always enabled. UIA name `Copy`.
5. **Step caption** (12 px, opacity 0.7): `2. Paste Claude's reply below.`
6. **Reply box**: multi-line `TextBox`, `Height = 110`, wrapping, `Header = "Claude's reply"`, placeholder `Paste Claude's whole reply (with the ```json block) here`. UIA name `Claude's reply`.
7. **Error line** directly under the reply box (reserved only when shown: `Visibility.Collapsed` otherwise, as in Add Plan): `TextBlock`, `SystemFillColorCriticalBrush`, wrapping. Cleared when the user edits the reply box.

Buttons: primary `Preview`, close `Cancel`; default button = Primary. Primary never disabled (an empty paste gets the error below, as Add Plan does). Invalid paste: `args.Cancel = true`, the error shows, dialog and typed text stay exactly as they were.

**Exact error copy** (shown in the error line; the text itself carries the meaning, colour is only reinforcement):

| Cause | Message |
|---|---|
| Empty reply | `Paste Claude's reply first.` |
| No `{...}` found | `Couldn't find valid JSON — paste the whole reply including the ```json block.` |
| Does not parse | `That JSON doesn't parse — check it's the complete reply including the ```json fence.` |
| No `phases`, empty, or zero tasks | `The reply has no tasks. It needs a 'phases' list with at least one task.` |
| Task without title | `Task {k} has no 'task' title.` ({k} = 1-based position across the whole reply) |
| Bad day | `'{title}' needs a whole-number 'day' of 1 or more.` |
| Title equals a done task | `'{title}' is already a done task in this plan. Give the new task a different title.` |
| Duplicate inside the reply | `'{title}' appears twice in the reply. Titles must be unique.` |

Show only the first problem found, in the order of the table. `{title}` is shown as the user pasted it (trimmed).

## D3. Step 2 dialog: preview and confirm

Opens after a valid paste (the step 1 dialog closes first; same width constants). Title: `Replace remaining tasks?`

Body, top to bottom:

1. **Headline** (SemiBold, 16 px, primary text): `{R} unfinished tasks removed, {A} new tasks added`. Singular: `1 unfinished task removed`, `1 new task added`. Example for the fixture: `6 unfinished tasks removed, 5 new tasks added` (AC 4: exact text).
2. **Facts block**, a two-column grid (label column Auto, value column Star, `ColumnSpacing` 16, `RowSpacing` 4, 13 px), always these four rows in this order, nothing omitted when "unchanged":
   - `Kept as they are` | `{D} done tasks` (`1 done task`; `no done tasks` when 0)
   - `New tasks start` | `day {start}, {dd.MM.yyyy}`
   - `Old finish date` | `{dd.MM.yyyy}`
   - `New finish date` | `{dd.MM.yyyy} ({delta})`
   - `{delta}` is words, never a sign alone: `{n} days later`, `{n} days earlier`, `1 day later`, `1 day earlier`, `same day`. Labels in the secondary brush, values in the primary brush. No red/green here: a later finish is not an error.
   - Dates use the app's existing numeric date helper (`ToDisplayDateNumeric`, the same format as the sidebar "Finishes" line).
3. **Consequence note** (12 px, secondary brush, wrapping): `Ticked tasks, your score history and your notes are not changed. The "late from plan" count starts again from 0. This can't be undone.`
4. **Error line** (same style as step 1, collapsed unless needed) for apply failures, see D5.

Buttons: primary `Replace tasks`, secondary `Back`, close `Cancel`. **Default button = Close (Cancel)**, because this step is destructive; Enter must not confirm it.
- `Back` reopens step 1 with the prompt and the pasted reply exactly as left (keep the text in memory for the duration of the flow).
- `Cancel` (or Esc) ends the flow, nothing written, file byte-identical (AC 5).
- `Replace tasks` runs Apply with `args.Cancel = true` until it finishes, so a failure keeps the dialog open; on success the dialog closes and the flow returns true.
- The start day and the finish dates shown are those recomputed from the ticked tasks when the step opens, and Apply recomputes again when `Replace tasks` is pressed (P3). If the recomputed start differs from what was shown, do not apply: keep the dialog open, refresh the figures in place and show `The date changed while this was open. The figures above are updated — check them and press Replace tasks again.`

## D4. Refused: nothing to replace (all tasks done)

No step 1. A small confirmation-style dialog via `DialogControls.Build`:
- Title: `Nothing to replace`
- Body: `Every task in '{plan name}' is already done, so there is nothing left to replace.`
- Single close button `OK`. Nothing written.

## D5. Failure states

- **Plan file write fails** (inline, step 2, dialog stays open): `Couldn't write the plan file — nothing was changed. Check the log and try again.`
- **File written, database clean-up fails**: close step 2, return true (the plan did change, so the page must re-render), then show a one-button (`OK`) dialog titled `Plan updated, clean-up incomplete` with body `The plan was replaced, but old reschedule data for the removed tasks could not be cleared. Check the log. Some new tasks may appear on the wrong day until it is.`
- No new InfoBar or toast. Success is silent: the dialog closes and the card re-renders with the new "Day X of Y" and "Originally due" lines (the visible proof).

## D6. States on the card after success

No new elements. The existing meta line (`Day {X} of {Y} · {done}/{total} tasks done`) and due line (`Originally due ...`, success-green when drift is 0) simply show the new numbers; the drift suffix (`— now Nd later`) must be absent because drift is 0.

## D7. Colour, copy, behaviour the Coder must not improvise

- Hues: existing neutral text brushes, the app accent only through default control states, and `SystemFillColorCriticalBrush` for errors. No success/warning/new colours, no danger-red fill on `Replace tasks` (the destructive signal is the wording and the non-default position).
- No emoji or icons beyond the existing `Copied ✓` pattern.
- Do not add a "Generate" step, a task list/table editor, a file picker, or a per-task preview list. The preview is counts and dates only.
- Do not add a "don't ask again" or auto-confirm option.
- Quotes: use the straight apostrophe `'` in all copy above; `Claude's` as written.
- Do not change any text of existing buttons or dialogs; column index shifts on the card must leave their order and behaviour untouched.
- Every control above has the UIA name given; the new button's name is `Replace remaining tasks: {plan.Name}` (per section "Exact files to change", item 6). Step 2's headline `TextBlock` carries `AutomationProperties.Name` equal to its own text so QA can read it.

## D8. Extra design checks for QA (Designer's, not from ACCEPTANCE.md)

Run on a scratch `MENTOR_ROOT` only.

- DC1. On the Plans page the new button sits between `+ Add task` and `Excluded days…` (compare `BoundingRectangle.Left` order via UIA), and no button on the card is clipped at the minimum window width (each button's right edge is inside the card's right edge).
- DC2. Button visible text is exactly `Replace remaining tasks…`; UIA name `Replace remaining tasks: {plan name}`; with two active plans the two names differ.
- DC3. Step 1: elements with UIA names `Prompt for Claude`, `Copy`, `Claude's reply` exist; the dialog's rendered width is about 640 px and no child's right edge exceeds the dialog's content area (nothing clipped).
- DC4. Step 1 prompt text contains `Day 1: ` through `Day 4: ` lines with the four done titles in order, and none of the six removed titles.
- DC5. Each error message in D2's table appears verbatim for its cause; the error line is not visible before the first failed attempt; the reply text is still present after an error.
- DC6. Step 2 headline equals `6 unfinished tasks removed, 5 new tasks added` for the fixture; the four fact rows are all present in order, with `Old finish date` and `New finish date` both formatted like the sidebar "Finishes" date.
- DC7. Step 2 default button is `Cancel` (pressing Enter on step 2 writes nothing); `Back` returns to step 1 with the reply text intact.
- DC8. All-done plan: click shows `Nothing to replace` with only an `OK` button; plan file byte-identical afterwards.
- DC9. After confirm, the card's due line has no `— now ... later` suffix and uses the success colour (same brush as an on-track plan).
- DC10. Zero-done plan: step 1 summary reads `{plan name}: no tasks are done yet, so the whole plan will be replaced.` and the prompt list reads `(none yet)`; step 2 row `Kept as they are` reads `no done tasks`.

## Conflict with plan

None. The Planner's two-dialog approach, `args.Cancel` handling and width reuse support this design as written.

## Open question (Design)

None.
