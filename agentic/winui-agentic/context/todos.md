# Resolved work — agentic workflow (Planillium twin)

Started fresh 2026-10-01 when the two workflows' documents were split. History before this date lives
only in the regular workflow's `context/todos.md` — deliberately not copied: it holds regular-side
implementation write-ups, which would break the blind comparison.

Resolved work only (done, or decided not to do with the reason). Every entry carries an absolute date.

### 2026-10-01 — payable tags

Built directly by the orchestrator (no Planner/Designer/QA pass). `PaidTags` (config `paid_tags`; absent = Studioshoo 1600 EUR/month); paid hours = tagged diary minutes x monthly / 168 h, read-time from `time_diary`; added into the Reports income net and sidebar balance AND shown as its own Earnings card; Settings > Income edits/adds/removes tags (a new name becomes a diary tag). 0 warnings, 243 tests pass. Not QA-tested; visual review pending.

### 2026-10-02 — user's first look at the agentic app: three defects fixed (logged 2026-10-08)

Found on first launch of the agentic build. (1) Reports failed with `no such column: delta`: the fork's `income_ledger` code and table definition used `delta`; the real DB column is `delta_eur` (income posting at startup was broken too). Fixed in `IncomeService.cs`, `Database.cs`, `IncomeServiceTests.cs`. (2) Pie list rows misaligned when a name was long (name and bar were both Star columns, so the row grew to fit its text): name column now fixed 220, bar column fixed 100, ellipsis trims the name. (3) Slices of the same category had identical fills: `SliceBrush` keeps the category hue but steps each further same-category slice lighter (opacity -0.18 per rank, floor 0.35). Release build 0 warnings, 0 errors. Automated tests NOT re-run after the change; visual re-check pending.

### 2026-10-09 — "Replace remaining tasks…" built (spec `pilot-specs/replace-remaining-tasks/SPEC.md`)

Per-plan button on the Plans page (between "+ Add task" and "Excluded days"). Two steps: paste Claude's reply (fenced or bare JSON, `phases` only is read), then a preview and confirm. Unfinished tasks are removed; the new phases start on last done day + 1 (day 1 if none is done), not clamped to today's plan day (user decision 2026-10-09, see `DECISIONS.md` rule 15; this replaces the earlier max(…, today's plan day) rule); `total_days` is set explicitly. Plan file is written first, then the DB clean-up of removed titles' overrides and unticked completions (done rows, score and diary untouched). A failed clean-up is reported, not hidden. Build (Release, -warnaserror) 0 warnings, 0 errors; `dotnet test` 253/253 pass (10 new in `PlanReplacementTests.cs`). UI layout at minimum window width and the live check are QA items, not done.

### 2026-10-09 — Plans page card layout (spec `pilot-specs/plans-card-layout/SPEC.md`)

Active-plan card on the Plans page was one 7-column Grid (name `*`, six `Auto` buttons), so at 900 DIP the buttons took the width and the name wrapped one word per line while meta, due and bar clipped. Now the card is a vertical StackPanel: text block full width (name max two lines, ellipsis; meta and due wrap), then the same six buttons in a new private `Controls/WrapFlowPanel.cs` (8 DIP spacing, 12 DIP above, wraps by whole buttons). Same buttons, labels, tooltips, automation names, order and handlers. Release build (-warnaserror) 0 warnings, 0 errors; `dotnet test` 253/253 pass. UIA BoundingRectangle check at 900 DIP and default width, and the Replace-remaining-disabled reading of ACCEPTANCE 5, are QA items, not done here.

### 2026-10-13 — "Replace remaining tasks…" v2 built (spec `pilot-specs/replace-remaining-v2/SPEC.md`)

The copied prompt lists every unfinished task in full (day, title, detail, mentor note, category, duration, tools; missing values read "(none)"). Step 1 gains "What should change?" (text goes into the prompt, live rebuild on edit, neutral line when empty) and an optional "Words that must be gone" field. Step 2 shows a mentions block between the facts and the consequence text: nothing when no words, a one-line "No mentions" confirmation, or a warning listing every new task still containing a word. The warning is advisory (Replace tasks, default button and Cancel unchanged). Neither new field is persisted. Prompt filling is single-pass so braces in user or task text stay literal. Build (Debug, -warnaserror) 0 warnings, 0 errors; `dotnet test` 264/264 pass (new tests in `PlanReplacementTests.cs`; `PlanTemplates.cs` now linked into the test project). Dialog layout, live prompt, Back restore, warning visuals and the scratch-root Confirm/Cancel byte checks are QA items, not done.

### 2026-10-13 — "Ask Claude" AI layer built (spec `pilot-specs/ai-layer/SPEC.md`)

Settings -> AI section: key box (write-only, stored in Windows Credential Manager, test slot `anthropic_api_key_test` only under the test override), Save (also writes `ai.model`), Test connection, Remove key. "Ask Claude" button in Replace remaining tasks (step 1) and Add plan; the prompt sent is the one the box shows; progress line with Stop; reply fills the reply box and then the existing validation/preview/import path. One-time notice (`ai_notice_accepted`) is inline, not a ContentDialog (WinUI allows one ContentDialog at a time). Failure messages are category-only, no key or response text. Build (Debug, -warnaserror) 0 warnings, 0 errors; `dotnet test` all pass (new `AiClientTests.cs`, linked `AiClient.cs`). No real Credential Manager entry and no real API were used. QA items: live save/test/remove, notice visuals, Stop, narrow-width layout.
