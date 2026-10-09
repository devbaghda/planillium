# SPEC - Plans page card layout (agentic side)

**Has a screen**: yes (Plans page, active-plan card). The Designer adds a **Design** section below; this is the Planner half.

## Requirement
Each active-plan card on the Plans page currently lays the plan text and up to six action buttons out in one
horizontal Grid row (one `*` column for text, six `Auto` columns for buttons), so the buttons win the width and
the name wraps one word per line while the meta line, due line and progress bar are clipped. Redesign the card
so the plan text always gets the full card width and is never clipped, at the default and the narrowest
supported window (900 DIP wide, `MinWidthDip` in `MainWindow.NativeInterop.cs`), with every action still
reachable and behaving exactly as today. Pure layout change: no new colours, no behaviour change, no new
settings, no schema change.

## Chosen approach
Stack the card vertically inside the same rounded `Border`:
1. Row 0: the existing `left` StackPanel (name, meta line, due line, progress bar), spanning the full card width.
   Name keeps `SubtitleTextBlockStyle` with `TextWrapping="Wrap"` and `MaxLines="2"` + `TextTrimming="CharacterEllipsis"`
   (acceptance 1 allows at most two lines; set explicitly so a pathological name cannot grow the card). Meta and
   due lines get `TextWrapping="Wrap"` (wrap, never truncate, acceptance 2/3).
2. Row 1: the action buttons in a left-aligned wrapping flow, 8 DIP spacing both ways, ~12 DIP above. Buttons
   keep natural width and wrap to a second line when the card is narrow. Order unchanged:
   Briefing (if any), + Add task, Replace remaining tasks..., Excluded days..., Archive, Teach on-plan apps... (if tools).
   Build the same `Button` objects as today with the same content, enabled state, tooltips, AutomationProperties
   names and Click handlers; only the container and the `Grid.SetColumn` calls change.

Wrapping container: the project has no CommunityToolkit, so no `WrapPanel`. Coder picks, in this order of
preference: (a) a small private custom `Panel` (measure/arrange wrap, ~30 lines) so buttons keep natural widths;
(b) `VariableSizedWrapGrid`; (c) `ItemsRepeater` + `UniformGridLayout`. Whichever is chosen must be verified
at 900 DIP, not assumed.

Rejected alternatives:
- **Overflow "More..." menu (MenuFlyout) for secondary actions**: hides actions behind a click, turns buttons
  into menu items (tooltip/disabled-state/automation-name behaviour differs), and adds a pattern the app
  doesn't use elsewhere. Violates "no behaviour change" in spirit.
- **Keep one row, shrink buttons to icons or shorter labels**: changes labels the user and tests know, and still
  fails at 900 DIP with six buttons.
- **Text left, buttons in a vertical column at the right**: six stacked buttons make the card tall and still
  take a fixed slice of width at narrow sizes.
- **Two-row Grid with fixed button columns**: cannot wrap, so clips at 900 DIP.

## Exact files to change
- `winui-agentic/Planillium.App/Pages/PlansPage.xaml.cs` - `PlanCard`: replace the 7-column Grid with a 2-row layout
  (text row, wrapping action row); add wrap/MaxLines properties to the text blocks; remove `Grid.SetColumn` use.
- (Only if option (a) is chosen) one small new file under `winui-agentic/Planillium.App/` for the wrap panel class
  (e.g. `Controls/WrapPanel.cs`, or a private nested class in `PlansPage.xaml.cs`). Must be reusable-neutral, no styling.
- `winui-agentic/Planillium.App.Tests/` - no new unit tests required (pure UI layout); existing suite must still pass.
- `winui-agentic/CHANGELOG.md` (Unreleased) - one user-visible line. `winui-agentic/context/todos.md` - dated entry.
- No settings schema, SQLite migration, service or view-model change. `PlansPage.xaml` unchanged (`ActiveList` stays).

## Edge cases
- Very long plan name (the 50-char "AI Microsoluti..." example) and a name with no spaces: two-line cap with ellipsis, no overflow past the Border.
- Plan with 0 tools / no briefing: fewer buttons, row still left-aligned, no gap where the missing button was.
- Plan with all actions (6 buttons) at 900 DIP: wraps to two button lines; no horizontal scroll; nothing clipped by the rounded Border (see DECISIONS.md "WinUI / layout": Border corner-clip - the Border must itself be wide enough, so the card must stretch to the list width and not size to content).
- Meta line at its longest ("Day 30 of 30 - 30/30 tasks done - excludes Mon, Tue, Wed, Thu, Fri, Sat, Sun"-style with many excluded days) and a due line with drift suffix ("- now 12d later"): must wrap, not truncate.
- Two or three active plans (MaxActivePlans) stacked: spacing between cards unchanged (10 DIP in `ActiveList`).
- Complete plan: Archive enabled with "Archive checkmark" label; incomplete: disabled with tooltip. Replace remaining disabled when all done is existing behaviour of its dialog/handler - do not alter it here; verify it is unchanged.
- Window resized wider/narrower while page is open: panel re-measures; nothing depends on a one-time width.
- Re-render after any action (`Render()` rebuilds the cards): new layout must be built entirely inside `PlanCard` so this stays valid.
- Colours: due line keeps its existing success/critical brush logic; no new brush resources.

## Acceptance criteria
(copied verbatim from ACCEPTANCE.md)

1. A plan with a long name ("AI Microsolutions Specialist - 30-Day Brand Build") shows its name on at most two lines at the default window width.
2. The meta line ("Day X of Y - n/m tasks done"), the "Originally due" line and the progress bar are not clipped or truncated.
3. Same at the narrowest supported window width: no horizontal scrolling, no text cut mid-word.
4. Every action is still reachable: Briefing (only if the plan has one), + Add task, Replace remaining..., Excluded days..., Archive, Teach on-plan apps... (only if the plan has tools).
5. Each action still opens the same dialog / does the same thing as before; Archive stays disabled until all tasks are done; Replace remaining stays disabled when all are done.
6. Accessibility names still include the plan name; tooltips preserved.
7. No new colours. Build clean with /warnaserror; tests still pass.

Added by Planner (mine, not in ACCEPTANCE.md):
8. Button order, labels and tooltip texts are identical to before; Briefing/Teach remain conditional.
9. "Narrowest supported width" means the 900 DIP window minimum; verify with UIA bounding rectangles (all button and text rects inside the Border's rect), not by eye.
10. Card Border still stretches to the full list width with its rounded corners; no content clipped by the corners.

## Safety notes
- QA runs only against a scratch `MENTOR_ROOT` copy in its worktree (DECISIONS.md "Safety around real data"); never the real `data/progress.db`, real `config.json`, or the live running app. Do not run the agentic and regular apps at the same time against the same data.
- Do not click Archive, Add task, Replace remaining, or Excluded days confirm buttons on real data. On scratch data, opening the dialogs and cancelling is enough to prove reachability; Archive needs a scratch complete plan.
- Seed the long-name plan and the many-excluded-days plan only in the scratch plans folder.
- Do not touch `winui/` or any regular-side document.

## Open question
None. (Wrapping container choice is left to the Coder within the stated preference order.)

---

## Design

Skill used: `ui-ux-pro-max` (WinUI/Layout and Accessibility rules) plus `ui-design-rules.md`. This is a layout-only
repair of an existing card, so the look of the rest of the app is the reference, not a new style. Not a
placeholder visual layer: nothing new is styled.

### Layout (one card, top to bottom)
Card = existing `Border` (same Background/BorderBrush/1 DIP border/CornerRadius 8), stretched to the full
`ActiveList` width (`HorizontalAlignment=Stretch`). Child = a vertical `StackPanel` with `Spacing=0`, Padding
`18,14,18,14` (unchanged from today; moves from the old Grid onto the new container).

1. **Text block** (the existing `left` StackPanel, `Spacing=4`, full card width, never shares a row with buttons):
   - Name: `SubtitleTextBlockStyle`, `Wrap`, `MaxLines=2`, `CharacterEllipsis`. No tooltip needed.
   - Meta line: `TextFillColorSecondaryBrush`, FontSize 13, `Wrap`, no MaxLines.
   - Due line: FontSize 12, `Wrap`, no MaxLines; brush logic unchanged (drift > 0 = `SystemFillColorCriticalBrush`, else `SystemFillColorSuccessBrush`).
   - Progress bar: unchanged, `Margin 0,6,0,0`, `HorizontalAlignment=Stretch`.
2. **Action row**, `Margin 0,12,0,0` above it (so 12 DIP clear of the progress bar):
   - Wrapping flow, `HorizontalSpacing 8`, `VerticalSpacing 8`, left-aligned, buttons at natural width (no stretching, no equal widths).
   - Order and conditionals exactly as today: Briefing (if any), + Add task, Replace remaining tasks…, Excluded days…, Archive, Teach on-plan apps… (if tools).
   - Buttons keep default WinUI `Button` style and default height (32 DIP min). Keep `VerticalAlignment=Center` is irrelevant now; use `Left/Top`.
   - No divider line between the text and the buttons (a new stroke is not needed; the 12 DIP gap is enough). No separator between buttons.
   - Do not make any button an accent/filled button. All six stay the neutral default style; no action is promoted, because this change does not decide which action matters most.
3. Reserved space: nothing is reserved for absent buttons (Briefing/Teach). The row is left-aligned and simply shorter; there is no placeholder gap. The text block always occupies the full card width regardless of the buttons.

### Widths and wrap behaviour (what two Coders must both draw)
- Default window and wider: buttons fit on one line if the card is wide enough; otherwise they wrap. Wrap is by whole buttons, never inside a button label and never mid-word (a button label is a single line, `TextWrapping=NoWrap`, which is the Button default).
- 900 DIP window (card is roughly 600-650 DIP wide after sidebar and page padding): expect the six-button case to break onto two lines. Which button lands on line two is decided by measurement, not fixed; the order is preserved reading left-to-right, line one then line two.
- A single button wider than the row (cannot happen at 900 DIP) must not be truncated: the panel gives it its own line.
- No horizontal scrollbar on the page or card at any supported width.
- Cards of different plans are independent: one card may wrap its buttons while the next does not. Do not equalise heights or button columns across cards.

### States
- **Normal incomplete plan**: Archive shown as `Archive`, disabled (default disabled styling), tooltip `Enabled once every task is complete`.
- **Complete plan**: `Archive ✓`, enabled, tooltip `All tasks done — free the slot`. State is label plus enabled/disabled, never colour alone (already true).
- **Replace remaining tasks…**: stays enabled and unchanged on the card, with whatever the existing handler/dialog does when nothing remains. See Open question 1.
- **Empty / zero tasks** (`total = 0`): meta reads `Day X of Y · 0/0 tasks done`, bar at 0, layout identical. No special empty-state panel; a plan with no tasks is not a case this card has to teach.
- **No briefing / no tools**: those buttons are simply absent; no disabled placeholder.
- **Error**: the card has no error state of its own. Save failures keep using the page's existing `SaveErrorBar`. Do not add per-card messages.
- **Negative values / drift**: `driftDays > 0` -> `Originally due {date} — now {N}d later` in the critical brush; `driftDays < 0` -> `— now {N}d earlier` in the success brush; `0` -> no suffix, success brush. The words "later"/"earlier" carry the meaning, so it never rests on colour alone. Copy unchanged.
- **Disabled/hover/focus/pressed**: default WinUI button states; do not override focus visuals. Keyboard Tab order = visual order = the order above (this is why the wrap panel must add children in order and not reorder them).

### Copy (all unchanged; Coder must not edit any string)
- Buttons: `📋 Briefing`, `+ Add task`, `Replace remaining tasks…`, `Excluded days…`, `Archive` / `Archive ✓`, `Teach on-plan apps…`.
- Automation names: `Briefing: {plan}`, `Add task: {plan}`, `Replace remaining tasks: {plan}`, `Excluded days: {plan}`, `Archive: {plan}`, `Teach on-plan apps: {plan}`.
- Tooltips: only on Replace remaining, Archive and Teach, text exactly as in the current code.
- Meta/due lines: exact current format strings.

### Colour
No new brush, no new hue. Surfaces: `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`. Text: default plus `TextFillColorSecondaryBrush`. The only status colours remain the existing success (on track / earlier) and critical (later) on the due line, each paired with words. No accent fills on buttons.

### Do not improvise
- No "More…" menu, no icon-only buttons, no shortened labels, no horizontal scrolling, no fixed pixel card width, no equal-width/uniform button grid.
- No change to padding, corner radius, card spacing (10 DIP in `ActiveList`) or fonts other than adding wrap/MaxLines.
- Do not add dividers, headers, section labels or an "Actions" caption.
- Do not move any button out of the card or reorder them.
- Do not touch the Queued / Archived card layouts further down the page (they use their own 3-column rows; out of scope). Check only that they were not affected.

### Extra design checks for QA (Designer's, in addition to ACCEPTANCE.md)
D1. At 900 DIP, with a 6-button plan: every button's UIA `BoundingRectangle` lies fully inside the card Border's rectangle (inset at least 18 DIP from left and right edges), and the name, meta, due and progress bar rectangles are each at least (card width - 36 DIP) wide, i.e. the text block spans the full card width, not a shrunken column.
D2. Buttons' tops: at most two distinct `Top` values among the six at 900 DIP; vertical gap between the two button lines is 8 DIP (+/-2); gap from the progress bar bottom to the first button line is 12 DIP (+/-2).
D3. Reading order: sorting buttons by (Top, Left) gives exactly the specified order, and the Tab order matches.
D4. Name TextBlock height is no more than two lines of the Subtitle style (no third line, ellipsis visible for the 50-char name when it exceeds two lines).
D5. Widening the window from 900 to about 1600 DIP re-flows the buttons (fewer or no wraps) without re-opening the page; narrowing back re-wraps. Neither shows a clipped control.
D6. Left edges: the name, meta, due line, progress bar and first button share the same left x (the card's inner padding edge).
D7. Hue count: the card introduces no brush beyond the ones listed under Colour (grep the diff for new `Brush`/`Color` resource keys: expect none).
D8. Empty/absent buttons: a plan with no briefing and no tools shows exactly four buttons with no leading gap (first button's left x equals the text's left x).

### Conflict with plan
None blocking. The Planner's approach is the right one for this design.

Notes on the plan (non-blocking):
- The plan's Padding is implied on the old Grid. The Coder must carry `18,14,18,14` onto the new container, not drop it.
- The plan lists the Edge Case "Replace remaining disabled when all done" and ACCEPTANCE 5 says it "stays disabled when all are done". In the current code the card creates this button always enabled (no `IsEnabled` set); any disabling happens inside `ReplaceRemainingDialog`/its handler. See Open question 1.

### Open question
1. ACCEPTANCE.md item 5 says "Replace remaining stays disabled when all are done", but in `PlanCard` today the button is never disabled. The Design therefore keeps it exactly as is (enabled) and treats item 5 as "behaves as before". The orchestrator should confirm that reading, or state the intended product behaviour; the Designer will not invent a disabled state.
