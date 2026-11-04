# SPEC: Replace remaining tasks v2 (feature slug `replace-remaining-v2`)

**Has a screen: yes.** Two additions to the existing Replace remaining dialog: step 1 gets two text fields ("What should change?", "Words that must be gone"); step 2 gets a mentions-check result block. The Designer adds a **Design** section below; may return a Conflict with plan.

## Requirement

Builds on `replace-remaining-tasks` (already in this app, see `pilot-specs/replace-remaining-tasks/SPEC.md`; its rules are unchanged: overdue tasks are replaced with every other unfinished task, past score history untouched, start day = last done day + 1, nothing written until the user confirms the preview). Three changes. (1) The copyable Claude prompt must list every unfinished task in full (day, title, detail, mentor note, category, duration, tools), not just the done titles, so Claude can find the thing to remove wherever it sits, for example inside a task's detail text. (2) Step 1 gets a free-text "What should change?" field whose text goes into the prompt. (3) Step 1 gets an optional "Words that must be gone" field (comma-separated). When filled, the step 2 preview lists every new task whose title, detail, mentor note or tools still contain any of the words (case-insensitive) and warns clearly; the user may still confirm or cancel. When empty, no check and no warning section.

## Chosen approach

- **Prompt content.** `PlanReplacement.Situation` gains `IReadOnlyList<RemainingTask> Remaining` (new record: `AssignedDay`, `Title`, `Detail`, `MentorNote`, `Category`, `DurationMin`, `Tools`), built in `Assess` from the `tasks` already loaded there (the not-done entries; `t.Task` is the `PlanTask` with `Detail`, `MentorNote`, `Category`, `DurationMin`, `Tools`). Sorted by assigned day then title. The "assigned day" is override-adjusted, as for done tasks. `PlanTemplates.ReplaceRemainder` takes the remaining list and the change-request text and renders them. The existing `RemovedCount` stays (equals `Remaining.Count`).
- **Remaining-task block format** (plain text, one block per task, blank line between; null/empty fields rendered as `(none)` so the labels are always present and Claude is not left guessing):
  ```
  Day 5: <title>
    Detail: <detail>
    Mentor note: <mentor note>
    Category: <category>
    Duration: <n> min
    Tools: <a, b, c>
  ```
  New template wording, in addition to what exists: a section "Tasks to be replaced (the unfinished ones, in full):" before the JSON-shape request, and a section "What I want changed:" followed by the user's text (or, when the field is empty, the line `No specific change requested; improve and re-plan the remaining work as you see fit.`). The instruction must say: apply the requested change to every place it appears, including inside detail text, mentor notes and tools of tasks not otherwise touched; keep the remaining work otherwise as it is unless the change requires otherwise. Existing "do not repeat ticked tasks" and "titles must be unique" rules stay.
- **Placeholder-injection trap.** `ReplaceRemainder` fills placeholders by `string.Replace`. The user's change text and task content can contain literal `{plan_name}`, `{done_list}`, `{start_day}` etc. Fill the user-controlled values (change text, remaining-task block, done list, plan name) LAST, after the structural placeholders, or use a single-pass replacement, so injected braces are never re-substituted. Test for it.
- **Live prompt.** The prompt box rebuilds its text on every edit of the "What should change?" field (`TextChanged`), so the Copy button always copies the current text (AC 1). Copy still reads `prompt.Text`.
- **Word check, pure logic** in `Services/PlanReplacement.cs`: `ParseWords(string? csv) -> IReadOnlyList<string>` (split on comma, trim, drop empties, de-duplicate case-insensitively; also accept a semicolon? No, comma only as requested) and `FindMentions(Replacement replacement, IReadOnlyList<string> words) -> IReadOnlyList<Mention>` where `Mention(string Title, IReadOnlyList<string> Words)` lists every pasted task with at least one hit, in pasted order, with the matched words (as the user typed them). Matching: `string.Contains(word, StringComparison.OrdinalIgnoreCase)` on `task`, `detail`, `mentor_note`, and each entry of `tools`, read from `Replacement.PhasesJson` by parsing it (a `Replacement` stays the validated paste; no new field needed there). Category and duration are not searched (not in the request). Missing or non-string fields are skipped without throwing. Empty word list returns an empty result and the dialog treats that as "no check" (distinguish "no check" from "checked, none found" by `words.Count == 0` at the call site).
- **Plumbing.** `Preview` is unchanged. The dialog keeps `changeText` and `wordsText` in the same in-memory loop variables as `replyText` (so `Back` from step 2 restores all three). They are NOT written to disk, config, DB or any setting, and a freshly opened dialog starts with both empty (AC 10). Step 2 computes `FindMentions` from the step 1 replacement and words, and shows one of three states: no words entered (block absent, as if the feature did not exist), words entered and hits found (warning block), words entered and no hits (one line "No mentions of <words> found in the new tasks.").
- **No effect on Apply.** The check is advisory. `Apply`, the Confirm button and Cancel behave exactly as before (AC 8): a warning never disables Replace tasks, never changes the default button (stays Cancel), never writes anything.

**Rejected alternatives**
- *Block the confirm when words are found*: contradicts the settled decision that the user may still confirm.
- *Send only a longer title list and rely on Claude to ask for details*: Claude cannot see detail text, which is exactly where the Reddit mention lived.
- *Store the words field in config.json*: the request and AC 10 say it is per-use; persisting it would silently carry a word list into unrelated future replacements.
- *Search category and duration too*: not requested; adds false hits (a category word matching ordinary text).
- *Run the check on the old (removed) tasks*: they are being discarded; only the new list can still contain the words.
- *Whole-word matching*: would miss "Reddit's" and "r/reddit"; the request says "contain".

## Exact files to change

1. `agentic/winui-agentic/Planillium.App/Services/PlanReplacement.cs`: new `RemainingTask` record; `Situation` gains `Remaining`; `Assess` fills it; new `ParseWords`, `Mention` record, `FindMentions`; optional helper for the step 2 summary line text (for example `MentionsSummary`) so wording is unit-testable.
2. `agentic/winui-agentic/Planillium.App/Services/PlanTemplates.cs`: `ReplaceRemainderTemplate` gains the "tasks to be replaced" and "what I want changed" sections; `ReplaceRemainder` signature takes the remaining list and the change text; injection-safe filling.
3. `agentic/winui-agentic/Planillium.App/Dialogs/ReplaceRemainingDialog.cs`: step 1 adds the two fields and the live prompt rebuild, `Step1Result` carries the change text and words text; step 2 gains the mentions block; loop variables for Back; no persistence.
4. `agentic/winui-agentic/Planillium.App.Tests/PlanReplacementTests.cs`: update existing calls broken by the signature changes (the file has about 16 uses of `ReplaceRemainder`/`ParseReplacement`/`Assess`); add the new tests (see acceptance 12).
5. `agentic/winui-agentic/Planillium.App.Tests/Planillium.App.Tests.csproj`: no change expected (`PlanReplacement.cs` and `PlanTemplates.cs` are already linked; confirm `PlanTemplates.cs` is, since the template tests need it).
6. No XAML file (dialogs are built in code), no `PlansPage.xaml.cs` change, no settings schema, no `config.json` key, no SQLite schema or migration.
7. Docs for this side: `context/todos.md` dated entry, `CONTEXT.md` section 2 line, `MANUAL.md`/`CHANGELOG.md` if kept.

## Edge cases the build must survive

- **Large plans.** All unfinished tasks in full can be long (a 60-task plan with long detail text). The prompt box stays a fixed-height scrolling read-only TextBox; Copy copies the entire text, not what is visible. No truncation of any task field.
- **Task fields null or empty** (many hand-written plans have no mentor note, category, duration or tools): rendered as `(none)`, no crash, no "null" text.
- **Newlines and quotes inside detail text**: kept as is in the prompt (it is plain text, not JSON); multi-line detail is indented under its label so the structure stays readable.
- **Braces in user text or task text** (`{start_day}`, JSON snippets in a detail): must appear literally in the prompt, not be substituted (see injection trap).
- **Empty "What should change?"**: allowed; the prompt carries the neutral line. Not a validation error. Whitespace-only counts as empty.
- **Words field**: leading/trailing spaces trimmed; `Reddit,` or `,,Reddit` ignore empty entries; `reddit, Reddit` de-duplicated; a field of only commas/spaces counts as empty (no check, no block). Case-insensitive including non-ASCII letters (ordinal ignore-case).
- **A word matching in several fields of one task**: the task is listed once, with the word listed once.
- **A task matching several different words**: listed once, all matched words shown.
- **Pasted task with `tools` as a non-array, or `detail` as a number**: skipped for that field, no exception (the paste already passed `ParseReplacement`, which does not validate these).
- **The words appear in the plan name, a phase name, or a kept done task**: not searched or flagged (only the new tasks, per the request).
- **Done-task titles containing the words**: not flagged (they are fixed and cannot be changed by this action); not in scope.
- **Back from step 2** restores reply, change text and words exactly; **Cancel** at either step writes nothing and discards them.
- **Start day or finish date changing while open** (existing `StartChanged` path): the mentions block is unaffected (it depends only on the pasted content).
- **App not run in a while / settings changed mid-period / period boundaries**: nothing time-based is stored or read by the new code; the existing recompute-at-confirm behaviour is untouched. No change to scoring, so zero-crossing is not relevant.
- **Existing rules unchanged**: all-done plan refused with "Nothing to replace"; duplicate-title and done-title rejections; excluded weekdays.

## Acceptance criteria (copied verbatim from ACCEPTANCE.md)

Acceptance — replace remaining tasks v2 (run only against a scratch MENTOR_ROOT, never real data)

Fixture: plan of 10 tasks, days 1-10, tasks 1-4 done; tasks 5-10 unfinished, each with detail, mentor note,
category, duration and tools; task 8's detail text mentions "Reddit" (its title does not); task 6's title does.

1. The dialog has a "What should change?" field; its text appears in the copied prompt.
2. The copied prompt lists all 6 unfinished tasks with day, title, detail, mentor note, category, duration, tools.
3. The prompt still lists the 4 done tasks as fixed, and still shows the reply format.
4. The dialog has an optional "Words that must be gone" field. Empty: preview shows no warning section.
5. Reply JSON where a new task's detail contains "reddit" (lower case) and field = "Reddit": preview warns and
   names that task.
6. Same field matched in a title, a mentor note and a tools entry: each is caught.
7. Field "Reddit, LinkedIn": a task containing only LinkedIn is caught too (comma list works, whitespace trimmed).
8. A warning does not block: Confirm still applies the replacement; Cancel leaves the plan file byte-identical.
9. A reply with no match for the words: preview shows "no mentions found" (or equivalent), no warning.
10. The words field is not saved anywhere (reopening the dialog starts empty).
11. All acceptance lines of replace-remaining-tasks still pass (old lines 1-16).
12. Build clean (0 warnings with /warnaserror); existing tests pass; new tests cover the word check.

**Planner additions (labelled as mine):**
- P1. Braces typed in "What should change?" (for example `{start_day}`) appear literally in the copied prompt.
- P2. A remaining task with no mentor note / tools shows `(none)` for them in the prompt, never blank or "null".
- P3. A task matching two different words is listed once and names both words; a task matching one word in three fields is listed once.
- P4. Back from step 2 returns to step 1 with reply, change text and words intact; a freshly opened dialog has all three empty.
- P5. The change text and the words are not present in `config.json`, the scratch DB or any file in the scratch root afterwards (grep after a full Confirm run).
- P6. Words field containing only commas/spaces behaves as empty (no block at all in the preview).
- P7. New unit tests include: `ParseWords` (trim, empties, case-insensitive de-dupe), `FindMentions` per field (title, detail, mentor note, tools, case-insensitive), the two-words case, the no-match case, the empty-words case, `ReplaceRemainder` listing all remaining tasks with every field plus `(none)` for missing ones, and the brace-injection case.

## Safety notes (for QA)

- Never run against the real `data/progress.db`, the root `config.json`, `plans/active/*.json` or the live app. Use a scratch `MENTOR_ROOT` in the worktree with a fixture plan file and scratch DB (`DECISIONS.md` standing lessons, scratch-`MENTOR_ROOT` technique); confirm `AppPaths` resolves to the scratch root before clicking Confirm. Confirm rewrites the plan file and deletes DB rows.
- Tests must use the `PLANILLIUM_TESTS` define / scratch paths, so `dotnet test -c Release` cannot touch real data.
- The words/change text fields must never be pasted with real personal content into a file QA keeps in the repo; the repo is public.
- Do not touch the regular app (`regular/`) or its documents. Run only one app at a time, neither against real data.
- Cancel must be verified by byte-comparing the scratch plan file before and after.

## Open question

1. **Claude's reply may rewrite tasks that did not need to change.** The prompt will ask Claude to keep unaffected remaining tasks as they are, but the replacement is still a whole new list. Not a product decision to make here; flagged only so the user knows the preview's "N removed, M added" counts are always the whole remainder.
2. **Does "What should change?" need a minimum length or a required state?** Assumed no (optional, empty allowed with a neutral prompt line). If the user wants it required, say so.

---

## Design

Skill used: `ui-ux-pro-max` (form/feedback rules, WinUI stack); `ui-design-rules.md` read. No new screen, nav item or colour: both additions live inside the existing code-built `ContentDialog`s and reuse the controls, brushes and widths already in `ReplaceRemainingDialog.cs`. Hue budget stays: neutral text brushes plus the one existing alert brush (`SystemFillColorCriticalBrush`). No new accent, no caution/yellow brush.

### D1. Step 1 layout ("Replace remaining tasks", primary Preview, close Cancel, default Preview)

Same `StackPanel` (Spacing 10, MinWidth `DialogContentWidth` = 576) inside the existing `ScrollViewer` (MaxHeight 560). Dialog width 640 unchanged. Top to bottom, in this order, nothing else added:

1. `summary` line (unchanged).
2. `caption1`: **"1. Say what should change."** (12 pt, Opacity 0.7, wrap; same style as existing captions).
3. **Change field**: multi-line `TextBox`, `AcceptsReturn`, `TextWrapping.Wrap`, `Height = 72`, `Header = "What should change?"`, `PlaceholderText = "For example: remove everything about Reddit. Leave empty to let Claude improve the remaining work."`, `Text = changeText` (restored on Back). `AutomationProperties.Name = "What should change?"`. Full width, same as the reply box.
4. `caption2`: **"2. Copy this prompt into claude.ai."**
5. Prompt box (unchanged control; `Header "Prompt for Claude"`, read-only, `Height = 110`, fixed, scrolls) and `Copy` button (left aligned). Do not enlarge the prompt box for the longer text; it scrolls.
6. `caption3`: **"3. Paste Claude's reply below."**
7. Reply box (unchanged, `Height = 110`).
8. **Words field**: single-line `TextBox`, `Header = "Words that must be gone"`, `PlaceholderText = "Reddit, LinkedIn"`, `Text = wordsText` (restored on Back), `AutomationProperties.Name = "Words that must be gone"`. Directly beneath it, a helper `TextBlock` (12 pt, `TextFillColorSecondaryBrush`, wrap): **"Optional. Separate with commas. If any of them is still in Claude's new tasks, the preview warns you."** The helper is always visible (it is the "optional" cue, not a placeholder that vanishes while typing).
9. `error` TextBlock (unchanged, critical brush, collapsed until a bad paste).

Order rationale: the change field comes first because the prompt depends on it; the words field comes after the reply because it only concerns checking the reply. No section headers, no extra dividers; numbered captions only for the three actions the user must perform in sequence. Words field is deliberately not numbered (optional).

Reserved space: nothing is reserved for the error line beyond what exists today (collapsed). The dialog will scroll at small window heights through the existing `ScrollViewer`; the Preview and Cancel buttons stay pinned outside it.

### D2. Step 1 behaviour and states

- **Live prompt**: every `TextChanged` of the change field sets `prompt.Text` to the rebuilt prompt. If `copyBtn.Content` is "Copied ✓" it resets to "Copy" on that change (a stale "Copied" would mislead). Copy still copies `prompt.Text` in full.
- **Change field empty or whitespace**: normal state, no message, no red. Prompt carries the neutral line from the Chosen approach.
- **Words field empty / only commas and spaces**: normal state, no message, no check later.
- **No inline validation on either new field.** Nothing the user types in them is invalid, so neither shows an error; the only error line remains the reply-paste problem line, which is cleared when the reply is edited (unchanged; editing the new fields does not clear it, because it is about the reply).
- **Long text**: change field scrolls inside itself (fixed height); words field does not wrap (single line, scrolls horizontally inside the box). No character limit.
- **Keyboard**: Tab order is top to bottom: change, Copy, reply, words, Preview, Cancel. Enter inside the change and reply boxes inserts a newline (AcceptsReturn) and does not trigger Preview; Enter in the words box triggers the default Preview button (single-line field, standard dialog behaviour). Focus on open: the change field (set `Loaded` to focus it) so the user types first.
- **Back from step 2**: all three (change, reply, words) are restored exactly as typed; the prompt is rebuilt from the restored change text; the Copy button shows "Copy".

### D3. Step 2 layout ("Replace remaining tasks?", primary Replace tasks, secondary Back, close Cancel, default **Cancel**, all unchanged)

Existing order kept; the new block is inserted between the `facts` grid and the `consequence` text:

1. `headline` (unchanged).
2. `facts` grid (unchanged, 4 rows).
3. **Mentions block** (new; exactly one of three states below, or absent).
4. `consequence` text (unchanged).
5. `error` line (unchanged).

Panel Spacing stays 10. The block is full panel width and left aligned with the facts labels (no extra indent on the block itself).

**State A: no usable words entered** (field empty or only commas/spaces). The block is not created at all (Visibility Collapsed, no reserved space). Step 2 is identical to today. No "check skipped" text.

**State B: words entered, no new task contains any** (neutral confirmation). One `TextBlock`, 13 pt, `TextFillColorSecondaryBrush`, wrap:
`No mentions of {words} found in the new tasks.`
Prefixed by the glyph `✓ ` (the app already uses "Copied ✓"; the sentence itself carries the meaning, so the glyph is not load-bearing). No border, no fill, no accent colour.

**State C: words entered, one or more new tasks contain them** (warning). A single bordered block (1 px border `SystemFillColorCriticalBrush`, CornerRadius 4, Padding 12, background transparent; no filled red background), containing a vertical `StackPanel` Spacing 6:
- Heading `TextBlock`, 13 pt SemiBold, `SystemFillColorCriticalBrush`, wrap: `⚠ Warning: {n} new task(s) still mention {words}.`
  - `n` = 1 gives "1 new task still mentions"; n >= 2 gives "{n} new tasks still mention". The `⚠ Warning:` prefix is always present so the warning does not rely on colour.
- List, inside a `ScrollViewer` with `MaxHeight = 150`, vertical scroll auto, no horizontal scroll. One `TextBlock` per hit (13 pt, `TextFillColorPrimaryBrush`, wrap), in pasted order: `• {task title}  (contains: {matched words})`. Matched words are joined with ", " in the order the user typed them. Titles are shown in full, never truncated; the whole list is reachable by scrolling (no "and N more" cut-off, so no information is hidden).
- Footer `TextBlock`, 12 pt, `TextFillColorSecondaryBrush`, wrap: `You can still replace the tasks, or press Back and ask Claude to remove these.`

The block never changes the buttons: Replace tasks stays enabled, Cancel stays the default, nothing is pre-focused on the warning.

`{words}` formatting (used in State B and C), from the parsed list as the user typed it (first spelling kept when de-duplicated):
- 1 word: `Reddit`
- 2 words: `Reddit or LinkedIn`
- 3 or more: `Reddit, LinkedIn or Slack` (commas, then " or " before the last)
- Wrapped in typographic-neutral plain text, no quotes added. Truncation: none (a very long word list simply wraps).

Suggested testable helper (Coder's choice of name): `MentionsSummary(words, mentions)` returns the heading / the no-mention sentence, so wording is unit-tested.

### D4. Accessibility and automation

- Both new fields have `AutomationProperties.Name` equal to their header text (exact strings above) so QA can find them by UIA.
- The State B sentence and the State C heading are plain `TextBlock`s with `AutomationProperties.Name` set to their own text (like `headline` already does). Each list line is its own `TextBlock` with its text as name. The bordered container gets `AutomationProperties.Name = "Words that must be gone check"`.
- Warning contrast: the critical brush on the dialog background meets 4.5:1 in both themes (system brush); the list text itself is primary text colour, not red.
- Everything is reachable by keyboard (Tab); the inner list `ScrollViewer` is focusable and scrolls with arrow keys.

### D5. Colour and glyph use

One accent (system accent, only on the default Preview button as today) and one alert (`SystemFillColorCriticalBrush`: used for the existing error lines and now the warning heading and border). State B uses no colour at all. Warning is carried by the text `Warning:` plus the `⚠` glyph plus the border, never by colour alone. No amber/caution brush.

### D6. Exact copy (verbatim; Coder must not reword)

| Where | Text |
|---|---|
| Caption 1 | `1. Say what should change.` |
| Change field header / name | `What should change?` |
| Change field placeholder | `For example: remove everything about Reddit. Leave empty to let Claude improve the remaining work.` |
| Caption 2 | `2. Copy this prompt into claude.ai.` |
| Caption 3 | `3. Paste Claude's reply below.` |
| Words field header / name | `Words that must be gone` |
| Words placeholder | `Reddit, LinkedIn` |
| Words helper | `Optional. Separate with commas. If any of them is still in Claude's new tasks, the preview warns you.` |
| State B | `✓ No mentions of {words} found in the new tasks.` |
| State C heading | `⚠ Warning: {n} new task still mentions {words}.` / `⚠ Warning: {n} new tasks still mention {words}.` |
| State C list line | `• {title}  (contains: {matched words})` |
| State C footer | `You can still replace the tasks, or press Back and ask Claude to remove these.` |

Existing strings (summary, error messages, consequence text, button labels, titles) are unchanged. The old caption "1. Copy this prompt into claude.ai." and "2. Paste Claude's reply below." are renumbered as shown above (the only change to existing copy).

### D7. Must not improvise

- No new colours, fills, icons beyond the two glyphs `⚠` and `✓`, no Infobar/Expander/TeachingTip controls, no new dialog or third step.
- Do not disable, rename, recolour or re-default any button because of a warning.
- No validation errors on the two new fields; no required marker; no character counter.
- Do not show the block, a heading or "check skipped" text when no words were entered.
- Do not search or list anything except the new tasks; do not list done tasks or old tasks.
- Do not persist either field (no `ApplicationData`, config, DB, file, settings).
- Do not enlarge the prompt box, widen the dialog, or move the Copy button.
- Do not show a task's day in the warning list (the paste's days are not guaranteed to be meaningful); title plus matched words only.

### D8. Design checks for QA (extra to ACCEPTANCE.md; labelled Designer additions)

- DC1. Step 1 control order by UIA tree: summary, caption "1. Say what should change.", field named "What should change?", caption "2. ...", "Prompt for Claude", "Copy", caption "3. ...", "Claude's reply", field named "Words that must be gone", helper text, then (collapsed) error.
- DC2. The helper sentence under the words field is present with the words field empty, and also while it has text.
- DC3. Typing in the change field with Copy already pressed changes the Copy button text back to "Copy".
- DC4. Empty words: step 2 UIA tree contains no element named "Words that must be gone check" and no text starting `No mentions` or `⚠ Warning`; layout otherwise identical to v1 (same element order).
- DC5. Hits found: heading starts with `⚠ Warning:`, contains the correct count and singular/plural verb ("1 new task still mentions" vs "2 new tasks still mention"); one list line per hit task, each ending with `(contains: ...)`; footer present; the "Replace tasks" button is enabled and Cancel is still the default.
- DC6. Word-list wording: input `Reddit` gives "mentions Reddit"; `Reddit, LinkedIn` gives "Reddit or LinkedIn"; `A,B,C` gives "A, B or C".
- DC7. A fixture reply with 15 hits: block height is capped (list scrolls), all 15 lines are reachable, none truncated, dialog buttons still visible.
- DC8. Warning heading and border use the same brush as the existing error text (`SystemFillColorCriticalBrush`); list line text is not that brush; no other new brush appears in the dialog (grep the diff for new `Brush` literals / colour hex: none).
- DC9. The two glyphs are not the only carrier of meaning: the strings `Warning:` and `No mentions of` are present verbatim.
- DC10. Dark and light theme: both blocks readable (screenshot via `PrintWindow` flag 2 in each theme).

### Conflict with plan

None. The plan's three-state step 2 and live prompt map directly onto the layout above. One small addition for the Coder: the prompt box needs the Copy button reset (D2) and the heading singular/plural helper (D3); both are display-only and need no plan change.

### Open question (Design)

1. Should step 2 show the matched task's new day number next to the title? Not requested, and the pasted days may not be meaningful, so the design omits it. Say so if wanted.
