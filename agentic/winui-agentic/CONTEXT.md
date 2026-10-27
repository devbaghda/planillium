# Planillium (agentic twin) — Context

> Handoff for the **agentic workflow only**. Created 2026-10-01 as an adapted copy of the project's
> `CONTEXT.md`; from then on edited only by the agentic side. The regular workflow has its own
> `CONTEXT.md` one level up — never read or written from here. The orchestrating session records
> user decisions into this file when they apply to this side.

## 1. What this is

A desktop personal mentor and accountability companion: tracks progress across up to 3 active
life/career plans, monitors activity, keeps the user on-plan, logs the working day (06:00–20:00),
generates weekly reports. This folder is an independent duplicate of the WinUI app
(`Planillium.App/` + `Planillium.App.Tests/`, .NET 8 / WinUI 3), forked 2026-09-22 from the
regular app and built by Designer + Planner → Coder → QA agents. **Nothing here ships.** Pilot
rules and fork point: `PILOT.md`.

## 2. Current state

B1b money boxes (2026-10-25): built, QA PASS (7a5b58d) — save on leave/Enter, focus park on Income headings, negative-amount hint; see `context/todos.md` 2026-10-25.
B1d input fixes (2026-10-25): built; QA round 1 FAIL (2 defects: Enter in an emptied box left it blank and focused; after a refused mouse Add, Esc/click-outside did not clear the line), fix pass done 2026-10-25, awaiting QA round 2 — empty/0/letters amount refused in the add line, Tab keeps the line, mouse Add parks focus, focus guard on Income boxes, empty box reverts to the saved amount, no "= —/h"; see `context/todos.md` 2026-10-25 B1d. QA must drive focus with real clicks/keys: the guard cancels programmatic (UIA SetFocus) moves into Income boxes.
B1f focus jump (2026-10-26): fix 1 d2d30b0 failed QA (4 px right-edge padding strip still jumped to the salary box); fix pass 1 e82135f moved the padding inside the claiming content and made it fill the viewport height; awaiting QA re-run; see `context/todos.md` 2026-10-26.
B1g money-box keys (2026-10-27): built, not committed, awaiting QA — Esc in the salary/tag boxes reverts to the saved amount and parks the cursor; more than 9 digits refused with "Amount is too large (9 digits at most).", reverted on every exit, never saved (was a silent cut to 9 digits that saved); see `context/todos.md` 2026-10-27.
B1e Reports cleanup (2026-10-27): built, not committed, awaiting QA — removed the large Unearned-income and Earnings-from-paid-work cards from Reports (all periods); kept strip and calculations; see `context/todos.md` 2026-10-27 B1e.
B1i Reports income columns + A2 pie (2026-10-27): built (strict build clean, tests 362/362), not committed, QA deferred to the end of the group sequence — "Income lost/gained" and "Of which paid work" (light green, theme-aware) columns in the Reports summary table, Score card and income strip removed, a one-row table (Day, one-bucket Week/Month/Year) shows no Total row (user preference 2026-10-27, DECISIONS.md), Time-by-app chevrons restored and pie slices in a distinct-colour palette; see `context/todos.md` 2026-10-27 B1i.
G7 scratch-run safety (2026-10-27): built (strict build 0 warnings, tests 362/362), committed, no QA by brief — closes the open decision "scratch app reaches the real AI key / TickTick credentials" (0d). With `MENTOR_ROOT` set, `CredentialStore` Read/Write/Delete refuse every name except `anthropic_api_key_test` (guard `AppPaths.IsScratchRun`, not `#if`-gated, fail-closed); no real secret is read, written or deleted and no AI/TickTick request or browser sign-in can start. Clear messages in Ask Claude hint, Settings -> AI Save and the TickTick Connect dialog. Start-with-Windows (registry Run value) deliberately NOT covered (lead ruling); TickTick status text unchanged ("Not connected."). See `context/todos.md` 2026-10-27 G7.
G2 diary browsing (2026-10-27): built (strict build 0 warnings, tests 362/362), committed, no QA by brief — "-" (no value) entry in the Category/App/Page/Tag filter lists, "Search in" field chooser beside the search box, paging row "Showing N of M" + Show 50/100/500/1000 more + Show all (sticky); 300-row cap kept; item 4 (why App holds window titles) investigated only, decision open (section 5, item 4); see `context/todos.md` 2026-10-27 G2.
B1c Enter/focus/minus (2026-10-25): built and QA'd through two fix passes (D1 hint scroll) — Enter saves and parks focus, Enter in the new-tag line adds, blank-space click leaves a money box, minus kept as "-" with a persistent message (reverses B1b's drop-the-minus rule); see `context/todos.md` 2026-10-25 B1c. `B1bMinusTests` still pass (`MoneyFormat.Normalize` itself is unchanged and still drops '-'), but the box now uses `NormalizeKeepingSign`, so they no longer describe what the user sees — QA to rewrite.
Built so far through this pipeline: lost-earnings-counter (2026-09-22), an eight-item batch
(2026-10-01), and "Replace remaining tasks…" (2026-10-09, built and unit-tested, not yet QA'd; new tasks start on last done day + 1, day 1 if none done, not clamped to today — `DECISIONS.md` rule 15), and its v2 (2026-10-13, built and unit-tested, not yet QA'd: full unfinished-task list in the prompt, "What should change?" field, advisory "Words that must be gone" check in the preview), and "Ask Claude" (ai-layer, 2026-10-13, built and unit-tested, not yet QA'd; defect D1 — reason cleared right after an AI fill — fixed the same day by `ReplyErrorState`, Add plan's reply check by `PlanReplyCheck`, both in `Services/AiClient.cs`). Product architecture, schema and settings: `context/domain.md`. Business rules and
standing lessons: `DECISIONS.md`.

## 3. Section index

- `DECISIONS.md` — the numbered business rules with full rationale (working hours · plan-day
  arithmetic · overdue accrual · one-task-per-day · archiving · score floor and bonuses · day-off
  scoring · `DriftDays` · progress-based "Day X of Y" · the lost/gained-earnings counter's rules)
  plus standing lessons (verification discipline, real-data safety incl. the scratch-`MENTOR_ROOT`
  technique, WinUI layout traps, timer/state rules, design lessons). Grep the topic; don't read it through.
- `context/domain.md` — headers: display name and rename history · app architecture · tech stack ·
  Plan JSON format · database schema · config.json key fields.
- `context/todos.md` — resolved work for this side only, dated.

## 4. Rules that are not negotiable

- **`data/progress.db` and `config.json` (at the repo root, outside this folder) are the user's real
  data.** Never read, write or run anything against them or the live app. Verify against a scratch
  database inside a worktree.
- **Never touch `regular/winui/` (the regular app) or the regular workflow's documents** — read or write.
- The repo is public: no secrets, no personal data.
- Check every sibling of a fix: grep for the shape, not just the reported call site.

## 5. Still open

1. **(opened 2026-10-02; updated 2026-10-27) Fixes awaiting the user's visual re-check.** Reports column fix, pie alignment (`context/todos.md` 2026-10-02). Pie slice shading was replaced by a distinct colour per slice in A2 (2026-10-27, B1i) and the chevrons restored; QA of B1i/A2 pending (end of group sequence).
2. **(opened 2026-10-13) "Ask Claude" (ai-layer) built and unit-tested, not QA'd.** Settings -> AI plus
   Ask Claude in Replace remaining tasks and Add plan (`pilot-specs/ai-layer/SPEC.md`, `DECISIONS.md`
   rule 16, `context/domain.md` "AI layer"). Build (Debug, -warnaserror) 0 warnings; `dotnet test` all
   pass, including `AiClientTests.cs` against a local HttpListener stub. Not done: live Credential
   Manager save/test (only the test slot may be used), the one-time notice visuals, Stop and the
   dialog layout at narrow width. Deviation: the notice is inline, not a ContentDialog.
   **2026-10-13 follow-up:** QA defect D1 (garbage AI reply: reason vanished) fixed in Replace remaining
   tasks; Add plan now reports a non-plan reply honestly. Tests 310/310. Needs a live UIA re-check of
   both (`pilot-specs/ai-layer/SPEC.md`, "Follow-up 2026-10-13").
3. **(opened 2026-10-24; updated 2026-10-25) B1 follow-up.** Items (1)-(3) built in B1b/B1c; B1d (input fixes) built 2026-10-25, QA round 1 FAIL, fix pass done, awaiting QA round 2. Open questions for the user: zero refused in existing tag boxes too (assumed yes), salary 0 still allowed. **B1f sibling (opened 2026-10-26, not fixed, out of B1f scope):** the same ScrollViewer focus-first-child-on-release behaviour exists in every other scroller holding text boxes (Plans/Reports/Schedule/Today page scrollers, AddPlanDialog, ReplaceRemainingDialog and other dialog scrollers); a blank click there may focus their first box. Needs a user decision on whether to extend the fix. (Settled by the fix pass: Enter on an emptied box puts the saved amount back and the cursor leaves, like leaving; Enter on a minus/zero keeps the cursor and the message.)
4. **(opened 2026-10-27) G2 item 4: App filter lists window titles ("74% complete", "Add a device").** Cause: the diary stores only the window title and `AppNames.Group` takes the last " - " part as the app. Investigated only, nothing changed. User decides: A record the exe per row (schema + recording change), B known-app list at read time else "Other", C no-separator title is not an app; recommended A for new rows plus B for old. Detail: `context/todos.md` 2026-10-27 G2.
