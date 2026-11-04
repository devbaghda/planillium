# SPEC -- B1c Enter, focus and minus sign in Settings -> Income money boxes (agentic side)

## Requirement, in my words
Follow-up to B1b, from the user's eyes-on check:
1. Enter in the add-paid-job line = the Add button; afterwards no box holds the cursor.
2. Enter in a money box (salary, tag monthly net) saves it; afterwards no box holds the cursor.
3. Leaving a money box (Tab or click elsewhere) saves it, and the cursor ends nowhere -- never in the salary box.
4. REVERSES B1b line 7: a typed/pasted minus is KEPT in the box ("-500" stays "-500"). "Amounts can't be negative."
   stays visible as long as the content is negative/invalid, disappears when it becomes valid. While invalid nothing
   is saved and no tag can be added. Leaving an invalid box keeps text AND message; saved value stays as it was
   (user answer 2026-10-25: no revert, no clear).
5. The message must be clearly noticeable (agentic's B1b hint is the baseline; do not make it weaker).
Unchanged: Esc or a click outside the add line clears it without adding a tag; thin-space format; no spinners;
"= EUR x/h" readout.

## What exists (this side, read)
- `Controls/MoneyBox.cs`: `ValueCommitted` (on leave/Enter if valid and changed vs focus baseline), `Commit()`,
  `NegativeAttempt` + `NegativeAttemptChanged` (true only for the keystroke that contained a minus; reset on leave),
  `_writtenBack` echo guard (D1 fix: WinUI delivers the TextChanged for our own `Text = cleaned` write-back AFTER
  `_normalising` is already false, so the echo used to clear the hint). Enter is handled and swallowed in `OnKeyDown`
  (focus stays in the box). `Value` = `MoneyFormat.Parse(Text)`.
- `Services/MoneyFormat.cs`: `Normalize` drops '-'; `Parse` ignores '-' (so "-500" would parse as 500!);
  `ContainsMinus` (U+002D, U+2212, U+2013).
- `Pages/SettingsPage.xaml.cs`: salary box + tag boxes subscribe `ValueCommitted`; `MonthlyRow`/`MakeMinusHint` (hint
  under each row, `SystemFillColorCriticalBrush`, 12 px); `ParkFocus(caller, onlyIfInside)` (FocusPark button made a
  tab stop only while focused, else SectionList); heading PointerPressed parks focus; `NewPaidTag_KeyDown` handles
  Esc only; `AddPaidTag_Click` reads `NewPaidTagRate.Value`; `LeavePaidTagLine` clears both fields and parks;
  `NewPaidTag_LostFocus` leaves the line on any focus move out of it except Tab; `Unloaded` commits all boxes.
- XAML: `NewPaidTagLine` (name TextBox, `NewPaidTagRate` MoneyBox, Add button), `NewPaidTagHint` below it, `FocusPark`.

## Chosen approach
Make "negative" a real, derived state of the box (computed from its text), and make `Value` honour it, so every
existing consumer (readout, `Commit`, `SaveIncome`, tag save, `AddPaidTag_Click`) refuses invalid input with no
per-consumer check. Focus is handled in the page with one deliberate rule instead of ad-hoc parking.

### A. MoneyFormat (pure, WinUI-free so QA can unit-test)
- Add `NormalizeKeepingSign(string raw, int caret, char sep)` -> `(Text, Caret)`: runs the existing `Normalize`; if
  `ContainsMinus(raw)` prefixes one "-" and shifts the caret by 1 (never before the sign: caret >= 1). Any of the three
  minus characters (and several, or one in mid-text like "5-0") collapse to ONE leading "-" ("-50"). `Normalize`
  itself is unchanged (existing tests on it stay valid).
- `Parse` unchanged. `FromNumber` unchanged (still never emits a sign).

### B. MoneyBox
- Replace `NegativeAttempt`/`NegativeAttemptChanged` by `IsNegative`/`IsNegativeChanged` (derived: `ContainsMinus(Text)`
  of the final, normalised text; raised only on change). Because the written-back text now KEEPS its minus, the echo
  TextChanged yields the same answer as the keystroke pass: **delete the `_writtenBack` echo machinery** (it was only
  there to protect a flag the echo used to clear). Compute `IsNegative` from the text about to be written
  (`NormalizeKeepingSign` result) inside the non-`_normalising` pass, before writing it back; the echo pass then
  recomputes the identical value (no change, no event).
- `Value` getter: `IsNegative ? NaN : MoneyFormat.Parse(Text)`. A lone "-" is also NaN (no digits). Consequences, all
  wanted: `ValueChanged` not raised, `Commit()` does nothing (nothing saved), readout shows "= --/h", `SaveIncome`
  already skips NaN, `AddPaidTag_Click` already refuses NaN.
- `OnLostFocus`: still `Settle` the pending dot; NO reset of `IsNegative`, NO text revert; `Commit()` (a no-op while
  negative); clear `_hasBaseline`.
- Enter (`OnKeyDown`): settle, `Commit()`, `e.Handled = true`, then raise new event `EnterPressed` ONLY IF the box is
  valid (not negative, not empty-NaN). With an invalid value Enter does nothing visible: focus stays so the user can fix
  it (my decision, see Open question 2). The page parks focus on `EnterPressed`. The event must be raised AFTER Commit
  so the save happens before focus moves.
- Baseline nit: take the baseline at `OnGotFocus` as today. If the box held an invalid value at focus time the baseline
  is NaN, so correcting back to the old number re-saves the same number once. Harmless; do not add state for it.
- Paste: `-500` -> box "-500", message shown (same path as typing).

### C. SettingsPage
- Rename wiring to `IsNegativeChanged` in `MakeMinusHint` and for `NewPaidTagRate`/`NewPaidTagHint`. Hint visibility
  = `box.IsNegative` (persistent). Because state is derived from text, setting `Text = ""` (LeavePaidTagLine) or
  `Value = 0` (after Add) hides it automatically. Style and wording unchanged
  (`Amounts can't be negative.`, 12 px, critical brush, live region Polite, automation name). Line 12 asks for
  "at a glance": keep at least this; a persistent message directly under the box, same as B1b, meets it. Optional
  hardening for the Coder if the screenshot looks weak: `FontSize` 13 / `SemiBold`; do not invent more.
- Salary + tag boxes: subscribe `EnterPressed` -> `ParkFocus("SettingsPage.MoneyEnter")` (park unconditionally: focus is
  in the box that raised it).
- Add line:
  - `NewPaidTag_KeyDown` (name TextBox) gets Enter -> `TryAddPaidTag(fromEnter:true)`. `NewPaidTagRate` raises
    `EnterPressed` (its KeyDown is swallowed by `MoneyBox.OnKeyDown`, so the XAML KeyDown handler never sees Enter; and
    `EnterPressed` is only raised when valid) -> same `TryAddPaidTag`. For an invalid or empty rate on Enter in the
    rate box nothing happens (box swallows, hint visible); for Enter in the NAME box with an invalid rate, the add path
    must itself refuse (below) so line 6 holds from either field.
  - Extract the body of `AddPaidTag_Click` into `bool TryAddPaidTag()`; the Click handler calls it. New refusal first
    in it: `if (NewPaidTagRate.IsNegative) { SaveStatus.Text = "Amounts can't be negative."; return false; }` (the inline hint
    is already visible; the strip text is a second cue). Existing checks (empty name, NaN, duplicate) unchanged.
  - On success from the Enter path only: `ParkFocus("SettingsPage.AddEnter", onlyIfInside: true)` after the existing
    reset (`Name = ""`, `Rate.Value = 0`). `RenderPaidTags()` rebuilds the tag list during Add; the add-line controls
    are not rebuilt, so their focus is the thing to park. Add-button click behaviour is untouched.
  - Esc / click outside / focus leaving the line (`NewPaidTag_LostFocus`, `Page_PointerPressed`): unchanged; they clear
    the line, which clears the hint. This is the one place where "leaving keeps the text" does NOT apply (Open question 1).
- Leaving-focus rule ("cursor ends nowhere", never the salary box): see Focus below.

### D. Focus (the risky part)
Goal: after Enter or leaving by Tab/click, no MoneyBox holds focus unless the user deliberately clicked/Tabbed into it.
- Enter: handled by B/C -> `ParkFocus`.
- Leave by click on blank space: WinUI may move focus on to the first focusable element in the page, which is the
  salary box (suspected source of the user's report; NOT confirmed -- the cause has to be reproduced first).
- Leave by Tab: normally lands on the next stop (Employed toggle / next box). Acceptance 3 only requires "not the
  salary box"; Tab from the last box, or Shift+Tab wrap, may still wrap to it.
- Rule to implement in `SettingsPage`: a page-level `GotFocus` handler (tunnelled via `AddHandler(GotFocusEvent,
  ..., handledEventsToo:true)`) that, when focus arrives in a `MoneyBox`, checks a "user intent" record: (a) the last
  `PointerPressed` hit-tested onto that box (`VisualTreeHelper.FindElementsInHostCoordinates`) within the last
  ~500 ms, or (b) the last key was Tab/Shift+Tab (box reached by keyboard order, except when that wraps onto the
  salary box from a box that is not directly above it -- keep simple: allow). If neither holds, `ParkFocus` on the
  dispatcher (deferred). Record pointer and Tab via the existing page `PointerPressed` handler and a page `KeyDown`.
- The Coder must FIRST reproduce with UIA (log `FocusManager.GetFocusedElement` right after LostFocus and 100 ms
  later for: Tab, click blank, click heading, Enter) and only then keep whichever part of the rule is needed; delete
  parts that the reproduction shows are unnecessary. Do not ship the guard on a guess without that evidence.

## Focus-timing risks (explicit)
1. **TextChanged is delivered after the write-back, asynchronously.** B1b D1 (hint vanished) came from this. The spec
   avoids it by deriving `IsNegative` from the text itself so the echo cannot disagree. If a Coder keeps any
   "flag set on keystroke, cleared on next change" logic, D1 returns. Test: type "-", "-5", paste "-500", type
   "-" then digits; message must stay at every step.
2. **ParkFocus is deferred (`DispatcherQueue.TryEnqueue`).** Between the Enter/Tab and the park, a typed "9" can still
   reach the box (acceptance 1 types "9" afterwards: harness delay matters, but a human typing fast could win). Commit
   happens synchronously first, so the saved value is correct; the stray "9" would only edit the unsaved text.
   Accept, document, do not use a synchronous park from inside KeyDown (WinUI refuses focus changes in the same key
   event inconsistently).
3. **Enter KeyUp lands on the park Button.** A Button fires Click on Enter KeyDown only, but verify no Click/UIA
   invoke happens on the park button; `FocusPark` has no handler, so harmless if it does.
4. **FocusPark refuses focus while `IsTabStop=false`.** Existing code handles it (tab stop on only while focused; the
   SectionList fallback; Log.Warn). Keep the return-value check. After `Text = settled` in `OnLostFocus` WinUI queues
   another TextChanged; it must not call anything that re-focuses.
5. **Park vs Tab race.** `ParkFocus` on a Tab-leave would fight the user's own next target. Therefore the rule in D
   parks only when focus lands in a MoneyBox without matching intent, never unconditionally on LostFocus.
6. **Add-line LostFocus handler** (`NewPaidTag_LostFocus`) enqueues a check; after Enter-add the park (inside-line)
   and that check both run: the second must see focus already outside the line and do nothing (it does: it tests
   `!IsFocusInsideNewPaidTagLine()` then `LeavePaidTagLine`, which would run a harmless second clear + park no-op).
   Verify no double "Saved." and no exception.
7. **Rebuild loses invalid text.** `RenderPaidTags` (Add, Remove) recreates tag boxes; an invalid "-500" in a tag box
   disappears with them (box shows the saved value, hint gone). Same for Settings section switch? (panels collapse,
   not rebuilt: text stays). Accepted edge; QA to note, not a defect.

## Files to change
- `Planillium.App/Services/MoneyFormat.cs` -- `NormalizeKeepingSign`.
- `Planillium.App/Controls/MoneyBox.cs` -- `IsNegative`/`IsNegativeChanged`, `Value` NaN when negative, remove
  `_writtenBack`, `EnterPressed`, `OnLostFocus` without reset.
- `Planillium.App/Pages/SettingsPage.xaml.cs` -- renames, `EnterPressed` wiring, `TryAddPaidTag`, Enter in name box,
  focus-intent guard, `ParkFocus` reuse.
- `Planillium.App/Pages/SettingsPage.xaml` -- only if the name box needs nothing new (KeyDown handler already there):
  likely no change; `NewPaidTagHint` stays.
- Docs (this side only): `CONTEXT.md`, `context/todos.md` (dated entry), `CHANGELOG.md` (Unreleased).
- No test files (QA owns tests). Note for QA: `qa/B1bMinusTests.cs` and `ContainsMinus`/`NegativeAttempt` tests assert
  the OLD behaviour (minus dropped, flag cleared on leave) and must be rewritten by QA; the renamed members will
  break compilation of the old file.

## Edge cases
- "-" alone: shown, message, Value NaN, nothing saved. "5-0" / "--5" / U+2212 / U+2013 -> "-50", "-5", "-5".
- "-0": negative state, nothing saved (not treated as zero).
- Negative then delete the minus: valid, message gone; then leave/Enter saves it.
- Empty box on leave: as B1b (no save, text stays empty, no message; empty is not "negative").
- Salary box invalid, user navigates to another Settings section or page: `Unloaded` commit is a no-op (NaN).
- Enter in an invalid box: nothing saved, focus stays, message stays.
- Add line: invalid/empty rate, Enter in either field: nothing added; message visible (hint for minus; strip text
  "Monthly net needs a number." for empty).
- Real data: QA uses scratch `MENTOR_ROOT` only.

## Open questions (product, for the user)
1. Add-paid-job line and "leaving keeps the text": the standing B1 rule (Esc/click outside/focus out clears the line)
   would wipe a typed "-500" and its message when the user leaves the new-tag amount box. I kept that rule (brief
   says it is unchanged). Say so if the add line should instead keep the invalid text on Tab/click-away.
2. Enter in a box holding an invalid value: I keep the cursor there (nothing saved). Alternative: park focus anyway
   (text and message stay). Not stated in the brief.
3. Minus position: any minus anywhere becomes ONE leading "-" ("5-0" -> "-50"). Brief only covers a leading minus.

## Acceptance (verbatim from ACCEPTANCE.md)
# Acceptance -- B1c (scratch MENTOR_ROOT only; never real data)

Fixture: salary 2925, tag Studioshoo 1600/month.

1. Type 3000 in the salary box, press Enter: config.json holds 3000; focus is on no money box (and not on the
   salary box); typing "9" afterwards changes nothing.
2. Same with the Studioshoo box.
3. Type 3100 in the salary box, press Tab: config.json holds 3100; focus does not land in the salary box.
4. Same by clicking blank space, and by clicking another box (focus then goes there, as expected).
5. Add line: name "zed", amount 500, press Enter in the name box and, separately, in the amount box: tag zed with
   500 is added exactly as with the Add button; afterwards no box holds the cursor.
6. Add line with an invalid amount (minus/empty) and Enter: nothing is added, message visible.
7. Type "-500" in any money box: the box shows "-500" (sign kept); message visible; config.json unchanged.
8. Continue typing digits after the minus: box keeps the sign and digits; message still visible.
9. Delete the minus: the box holds a valid number; message disappears.
10. With "-500" in the box, press Tab / click elsewhere: text and message stay; config.json holds the old value.
11. Esc or click outside the add line still clears it without adding a tag.
12. Message is readable at a glance in both apps (screenshot compared with the agentic B1b one as baseline).
13. Thin-space formatting, no spinners (B1 lines 6-8) still hold.
14. Build clean (/warnaserror), tests pass, QA covers lines 1-10.
