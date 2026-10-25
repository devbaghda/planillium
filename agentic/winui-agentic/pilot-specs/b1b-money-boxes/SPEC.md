# SPEC -- B1b money boxes (agentic side)

## Requirement, in my words
The three kinds of money box in Settings -> Income (previous salary, each payable tag's monthly net, the new-tag
amount) currently save on every keystroke (`MoneyBox.ValueChanged`). B1b changes three behaviours, nothing else:
1. Save only when the user leaves the box (focus lost) or presses Enter. Typing alone writes nothing.
2. Clicking an Income heading must take typing focus out of any money box, so stray typing cannot edit the salary.
3. A typed (or pasted) minus sign is still removed from the box, but a short hint says amounts cannot be negative.

Unchanged: calculation, thin-space display, no spinners, storage format, the "= EUR x/h" readout (it still follows
every keystroke; it is display only).

## What exists (this side, read)
- `Controls/MoneyBox.cs`: TextBox subclass. `OnTextChanged` normalises via `MoneyFormat.Normalize` and raises
  `ValueChanged` for every change that leaves a valid number. `OnLostFocus` only settles a pending dot.
- `Services/MoneyFormat.cs`: pure parse/format. `Normalize` silently drops '-' (and everything not a digit/separator).
- `Pages/SettingsPage.xaml.cs`:
  - salary box built in the constructor (`MonthlyIncomeBox`, `ValueChanged` -> `Income_Changed` -> `SaveIncome`);
  - payable-tag rate boxes built in `RenderPaidTags` (`ValueChanged` -> `SavePaidTags(..., rerender:false)`);
  - both rows built by `MonthlyRow(box, remove)` (horizontal StackPanel: box, readout, optional Remove);
  - new-tag amount box `NewPaidTagRate` is in XAML (`NewPaidTagLine`); its value is read only in `AddPaidTag_Click`
    (it has no ValueChanged handler, so it already saves nothing before Add -- keep that);
  - `LeavePaidTagLine` already parks focus on `FocusPark` (invisible 1x1 Button in XAML, `IsTabStop="False"`),
    falling back to `SectionList`.
- `Pages/SettingsPage.xaml`: headings are plain TextBlocks: "Income" (`SubtitleTextBlockStyle`) and "Paid work"
  (`BodyStrongTextBlockStyle`) inside `IncomePanel`.

## Chosen approach
Put the commit and minus detection into `MoneyBox` (one place, covers all three kinds), and the hint display in
`SettingsPage` (the page owns layout and the row builder).

### 1. Commit on leave / Enter
- `MoneyBox` gets a new event `ValueCommitted` (EventArgs). Raised when the user leaves the box (after the existing
  Settle step in `OnLostFocus`) or presses Enter (override `OnKeyDown`: Enter -> settle text, raise, mark key handled;
  focus stays in the box). Never raised for programmatic `Value =` sets.
- Raise only if `!double.IsNaN(Value)` AND `Value` differs from the baseline taken on `GotFocus` (store the value at
  focus time; update the baseline after each commit so Enter then Tab does not save twice). This keeps Tab-through
  from rewriting config and from flashing "Saved.".
- A public `Commit()` method does the same check; used by the page (below).
- `ValueChanged` stays (the page no longer uses it for saving). Keep it only if QA's B1 tests reference it; otherwise
  the Coder may leave it in place -- do not delete, nothing gained.
- `SettingsPage`: salary box subscribes `ValueCommitted` (not `ValueChanged`) -> `Income_Changed`; tag rate boxes
  subscribe `ValueCommitted` -> same `SavePaidTags(..., rerender:false)` body, same `_initialising` guard.
  `NewPaidTagRate` subscribes to nothing: line 5 is met because the value is read at Add (Enter there only settles
  the text; it does not add the tag).
- Page `Unloaded`: call `Commit()` on the salary box and every tag rate box (keep them in a list filled by
  `RenderPaidTags`), so navigating to another page while a box is still focused does not lose the typed amount.
  Section switches collapse the panel, which should raise LostFocus; QA to confirm.

### 2. Focus park on heading click
- Name the two headings (`IncomeHeading`, `PaidWorkHeading`) and handle `PointerPressed` on each with one handler.
  Handler: enqueue (DispatcherQueue.TryEnqueue, so it runs after the click's own focus handling) a call that, if the
  focused element is a `TextBox` (MoneyBox included), moves focus to `FocusPark` (`FocusState.Programmatic`), falling
  back to `SectionList`, logging via `Log.Warn` if both refuse -- exactly the pattern in `LeavePaidTagLine`. Extract
  that pattern into one private `ParkFocus()` and have `LeavePaidTagLine` call it too (no second copy).
- Parking moves focus off the box, so the box's LostFocus commits whatever was typed (requirement 1) before any later
  typing goes anywhere. Typing "9" afterwards goes to the park button and changes nothing.
- Risk: `FocusPark` is `IsTabStop="False"`; if WinUI refuses programmatic focus on it, `Focus()` returns false and
  the SectionList fallback must take it. The Coder must check the return value and test the click-then-type case,
  not assume.

### 3. Minus hint
- Pure helper `MoneyFormat.ContainsMinus(string raw)`: true if raw has '-' (U+002D), U+2212 or U+2013.
  (Pure and WinUI-free so QA can unit-test requirement 7.)
- `MoneyBox.OnTextChanged`: on the real (non-`_normalising`) pass, compute `ContainsMinus(Text)` BEFORE normalising.
  Set property `NegativeAttempt` to that result and raise event `NegativeAttemptChanged` when it changes. The
  re-entrant pass caused by writing the normalised text back is skipped (existing `_normalising` return), so it does
  not clear the flag. Result: typing "-" -> flag true, box text unchanged (minus dropped by Normalize, as today);
  next keystroke without a minus -> flag false.
- `OnLostFocus`: set `NegativeAttempt = false` (raises the event if it was true).
- `SettingsPage`: one private helper `MakeMinusHint()` returns a TextBlock; `MonthlyRow` wraps its existing
  horizontal row and the hint in a vertical StackPanel (Spacing 2) and returns that (callers only `Children.Add` it,
  so no caller change). The new-tag line gets `NewPaidTagHint`, a TextBlock added in XAML right below
  `NewPaidTagLine`, wired to `NewPaidTagRate.NegativeAttemptChanged`.

### Hint line spec (no designer)
- Wording: `Amounts can't be negative.` (contains "can't be negative", acceptance 7).
- Placement: directly under the box's row, left-aligned with the box, inside the same row's wrapper so it belongs
  to that box (three possible hints: salary, each tag, new tag). Not in the bottom status strip: that strip is
  shared, overwritten by save messages and far from the box.
- Style: `FontSize="12"` (same as the page's explanatory TextBlocks), foreground
  `SystemFillColorCriticalBrush` (the app's existing validation-message colour, used in AddTaskDialog,
  ReplaceRemainingDialog, etc.), `TextWrapping="Wrap"`, `AutomationProperties.LiveSetting="Polite"` and
  `AutomationProperties.Name` equal to its text so UIA/screen readers see it. Plain text, no icon.
- Visibility: `Collapsed` by default; `Visible` while `NegativeAttempt` is true. Showing/hiding moves the rows below
  by one line; accepted (the Settings pane scrolls; only one hint can be active since only one box has focus).
- Clears: on the next keystroke that contains no minus, and when the box loses focus. It does not time out.

## Files to change
- `Planillium.App/Controls/MoneyBox.cs` -- `ValueCommitted`, `Commit()`, Enter handling, focus baseline,
  `NegativeAttempt` + `NegativeAttemptChanged`.
- `Planillium.App/Services/MoneyFormat.cs` -- `ContainsMinus`.
- `Planillium.App/Pages/SettingsPage.xaml.cs` -- subscribe `ValueCommitted` for salary and tag boxes; `MonthlyRow`
  hint wrapper + `MakeMinusHint`; `ParkFocus()` extracted; heading handler; `Unloaded` commit; new-tag hint wiring.
- `Planillium.App/Pages/SettingsPage.xaml` -- names + `PointerPressed` on the two headings; `NewPaidTagHint`.
- Docs (this side only): `CONTEXT.md` (section 2 / pilot status), `context/todos.md` (dated entry),
  `CHANGELOG.md` (Unreleased). No `MANUAL.md` exists on this side: nothing to update.
- No test files: QA owns all tests. No other source files.

## Edge cases
- Empty or invalid entry on leave (acceptance 8): `Commit` does nothing for NaN; box text stays as the user left it,
  config keeps the previous value -- same as before B1b (previously ValueChanged was simply never raised for empty).
- Typed-but-unchanged value (e.g. focus then Tab): no save, no "Saved." message.
- Value reaches the same number via different text ("3 000" vs "3000"): compared as numbers, no save.
- Enter then Tab: one save (baseline updated). Enter in the new-tag box: settles text only.
- Paste "-500": hint shown, box shows "500". Minus in the middle ("5-0"): hint, box "50".
- Minus typed, then a digit: hint clears on the digit keystroke.
- Remove button click while a tag box is focused: LostFocus commits that box (if changed) and then Remove runs;
  `RenderPaidTags` rebuilds, so the committed-rate list must be rebuilt there too.
- `RenderPaidTags` after add/remove recreates boxes: old boxes' handlers go with them; the commit list is cleared
  and refilled.
- `_initialising` guard retained so the load-time `Value =` never saves.
- Closing the whole app while a box is focused and unsaved: not handled beyond whatever LostFocus the window raises;
  consistent with "saves on leaving". QA to observe and report, not a defect by itself.
- Locale: decimal separator logic untouched; the Italian comma and thin space behave as in B1.
- Real data: QA runs against a scratch `MENTOR_ROOT` only.

## Open question
- On leaving a box with an empty/invalid entry, config keeps the old value but the box stays empty (as before B1b).
  Reverting the box to the saved number would be friendlier; the brief says "behaves as before", so I did not add it.
  Decide if wanted.

## Acceptance (verbatim from ACCEPTANCE.md)
# Acceptance -- B1b money boxes (run only against a scratch MENTOR_ROOT, never real data)

Fixture: config with previous salary 2925, one payable tag "Studioshoo" 1600/month.

1. Type 3000 into the previous-salary box without leaving it: config.json still holds 2925.
2. Leave the box (Tab or click elsewhere): config.json holds 3000; the box shows "3 000".
3. Type 3100 and press Enter: config.json holds 3100.
4. Same three behaviours for a payable tag's monthly-net box.
5. Same for the new-tag amount box (value is used when the tag is added; no save before that).
6. Click the Income heading, then type "9": no money box changes; config.json unchanged.
7. Type "-" in a money box: a hint containing "can't be negative" (or equivalent wording) appears; the box does
   not keep the minus sign; the hint disappears on the next valid keystroke or when the box is left.
8. Leaving a box with an empty or invalid entry behaves as before B1b (no crash, previous value kept).
9. Thin-space formatting and no spinner arrows (B1 lines 6-8) still hold.
10. Build clean (0 warnings with /warnaserror); tests pass; QA's shared suite covers lines 1-4 and 7.
