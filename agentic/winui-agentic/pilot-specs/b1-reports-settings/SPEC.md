# SPEC -- b1-reports-settings (Reports EUR figures, nudge wording, Settings -> Income inputs)

Has a screen: yes (Reports page income block; Settings -> Income). The Designer adds a **Design** section below.
Source: `REQUEST.md` + `ACCEPTANCE.md` in this folder. Agentic side only.

## Requirement

Five small changes. (1) On Reports, for each period tab (Today / This week / This month / This year) show the unearned-income
figure and the paid-work income figure, both in EUR, in one clear place. (2) The off-plan nudge says "distracting apps and
activities". (3) Every money box in Settings -> Income (previous salary, each payable tag's monthly net, the new-tag amount)
shows thousands grouped with a thin space (2 925, 1 600, 14 000) as the user types, with no up/down arrows; the saved value
stays a plain number. (4) Previous salary is laid out like a payable-tag row: label "<name> -- monthly net (EUR)", box,
"= EUR x,xx/h" readout. (5) In the "New payable tag" line, Esc or a click outside leaves the line without adding anything.
No income/earnings/scoring calculation changes.

## What exists today (read from this side's source)

- Reports shows ONE period at a time (selector), not four stacked sections; "four sections" in the request = the four period
  tabs. `ReportsPage.Render()` (ReportsPage.xaml.cs ~158-163) already adds `IncomeCard` (big figure = `IncomeService.SumForPeriodWithCredit`
  Net) and `EarningsCard` ("EARNINGS FROM PAID WORK", big figure = sum of `PaidEarningsForPeriod`).
- **Important:** the Net figure in `IncomeCard` already INCLUDES paid-work earnings (`SumForPeriodWithCredit` adds `paid`), and the card
  carries an "Includes EUR x earned from paid work" line. So today the two figures are two separate cards, one of which contains the other.
- Nudge text: `Services/InsightRules.cs` line 29.
- Settings Income: `SettingsPage.xaml` lines 103-117 (`MonthlyIncomeBox` NumberBox with Compact spin buttons, `NewPaidTagName`,
  `NewPaidTagRate`); payable-tag rows built in `SettingsPage.xaml.cs` `RenderPaidTags()` (~194-226), header "<label> -- monthly net (EUR)", Width 240,
  hourly readout `= EUR x/h` at the right (`FormatEur(rate/168)`); add handler `AddPaidTag_Click`; save `SaveIncome()` writes `income.potential_monthly_net_eur`.

## Chosen approach

1. **Reports figures: no calculation change.** Add a compact two-figure strip at the top of the income block in `Render()`, above the
   existing cards: "Unearned income -- <period>: EUR a" and "Income from paid work -- <period>: EUR b", where a = the existing `incomeNet`
   value exactly as `IncomeCard` shows it today (so acceptance 4 holds) and b = `earnings.Sum(e => e.Eur)` (the existing
   `EarningsCard` total; 0,00 EUR when no payable tag has time -- not an error). Existing cards stay untouched underneath for detail.
   Rejected: (a) changing `incomeNet` to exclude paid work so the two figures are disjoint -- alters a calculation and the sidebar
   would then disagree with Reports, forbidden by the request; (b) doing nothing because both cards already exist -- the user asked
   for an at-a-glance pair and did not find them. See Open question 1.
2. **Nudge:** change the string to "Try blocking distracting apps and activities during working hours." Grep tests/docs for the old
   sentence and update.
3. **Money boxes:** replace the NumberBox on these three inputs by a small reusable `MoneyBox` (TextBox-based control or helper in
   `Controls/`, new pure formatter class `Services/MoneyFormat.cs` for parse/format so it is unit-testable): on every text change it strips
   non-digits/separators, regroups the integer part with a thin space U+2009 (acceptance accepts thin or normal), keeps the
   caret at the same digit position, keeps an optional decimal part with the culture's decimal separator (max 2 digits). Separator rules (comma = decimal, dot + 3 digits = thousands): see D3, "Separator policy". Exposes
   `Value` (double; NaN when empty/unparseable) and a `ValueChanged` event so the existing handlers (`Income_Changed`, tag-row save) keep
   their contract. Non-number / negative / empty behave as before: negative sign is not accepted (previous `Minimum=0` clamp), empty = NaN =
   "needs a number" / not saved (the existing NaN guards stay). Rejected: keeping NumberBox with a `DecimalFormatter` -- NumberBox only
   reformats when it commits (Enter/focus lost), not while typing, and the Windows formatter uses the locale's group separator, not a thin space;
   also setting `SpinButtonPlacementMode=Hidden` is still done conceptually (MoneyBox has no arrows). The 12 scoring NumberBoxes and
   other settings boxes are NOT money and keep their arrows (out of scope).
4. **Previous salary layout:** `MonthlyIncomeBox` becomes a row built like a tag row: header text "<name> -- monthly net (EUR)" where <name> is
   "Previous salary" (confirmed by the orchestrator, see Open question 2), MoneyBox width 240, readout
   `= EUR x,xx/h` from `value / IncomeService.WorkingHoursPerMonth` via `MainWindow.FormatEur`, updating live while typing. Reuse one helper to build
   both the salary row and tag rows so they cannot drift apart. Keep x:Name `MonthlyIncomeBox` and its UIA name so QA/automation still find it.
5. **Leave the add-tag line:** Esc in `NewPaidTagName` or the new MoneyBox clears both fields (name text and amount reset to empty/0 as after a
   successful add), nothing is added, config untouched, focus moves to a neutral element (the section scroller). Click outside: a
   page-level `PointerPressed` handler (registered with `handledEventsToo: true`) that, when focus is inside the line and the pointer is
   outside the line's bounds, performs the same leave. Typed text clearing is chosen (request allows either) so a stray half-typed tag is
   never confusingly kept.

## Exact files to change

- `Planillium.App/Services/InsightRules.cs` -- nudge sentence ("distracting apps and activities").
- `Planillium.App/Pages/ReportsPage.xaml.cs` -- `Render()`: add the two-figure strip above the income cards using existing `incomeNet` and `earnings`.
- `Planillium.App/Pages/ReportsPage.Income.cs` -- new small `IncomeSummaryStrip(...)` builder (same file as `IncomeCard`/`EarningsCard`); existing cards unchanged.
- `Planillium.App/Services/MoneyFormat.cs` -- NEW pure parse/format (thin-space grouping, digit-position caret mapping, Value parsing).
- `Planillium.App/Controls/MoneyBox.cs` -- NEW TextBox-based control (no arrows) with `Value`/`ValueChanged`; create the folder if the project has none (check `Planillium.App` layout; place beside other custom controls if one exists).
- `Planillium.App/Pages/SettingsPage.xaml` -- `MonthlyIncomeBox` and `NewPaidTagRate` switch to `MoneyBox`; new-tag line gets `KeyDown` for Esc; salary row layout like tag row.
- `Planillium.App/Pages/SettingsPage.xaml.cs` -- `RenderPaidTags()` uses MoneyBox; shared row-builder for salary and tags; `LoadIncome`/`SaveIncome`/`AddPaidTag_Click` adapt to `MoneyBox.Value`; Esc and outside-click handlers; thin-space display of loaded values.
- No SQLite change, no migration, no `config.json` schema change (keys and number types unchanged).
- Docs same pass: `CHANGELOG.md` Unreleased and `MANUAL.md` (agentic copies, if present) -- Reports figures, nudge wording, Settings -> Income inputs; `context/todos.md` dated entry.
- Tests: QA owns all tests (no Coder tests). QA's shared suite must cover `MoneyFormat` and `IncomeService.PaidEarnings`/Net unchanged.

## Edge cases

- No payable tags / no paid time in the period: paid-work figure shows EUR 0,00 (existing `EarningsCard` empty state kept), no exception.
- Unemployed with negative net: unearned figure keeps its existing sign/format (`FormatEur(Net)`); do not take Abs in the strip unless `IncomeCard`'s headline does the same (it shows `FormatEur(roundedNet)`, signed -- copy that). Values crossing zero (net from negative to positive) switch the existing caption; the strip should carry a neutral caption plus the signed amount.
- Day tab at midnight rollover and period boundaries: the strip reads the same `_period`/`DateTime.Today` as the cards, rendered in the same `Render()` pass, so day/week/month/year starts cannot disagree with the cards.
- Settings changed mid-period (salary or tag rate edited): history re-prices as today; strip follows automatically (same data path).
- MoneyBox: paste of "14000", "14 000", "14,000.50", "abc", "-5", "" ; caret in the middle of the number when a group boundary is inserted/removed; text longer than ~9 digits (cap, no overflow to Infinity); locale decimal comma ("1 600,50") vs point; leading zeros ("0014").
- Saving while typing: ValueChanged only fires with a valid number (as NaN guard did before); an empty box does not write NaN or 0 to `config.json`.
- `_initialising` guard stays so programmatic load does not trigger saves (and does not rewrite config.json).
- Esc/outside click during an in-progress valid add: only leaves; the Add button still adds when clicked (clicking Add is "inside the line" -- the line's bounds include the Add button).
- Narrow window: salary row (240 + readout) must not overflow; follow the tag row's existing width.

## Acceptance criteria (verbatim from ACCEPTANCE.md)

Fixture: config with previous salary 2925, one payable tag "Studioshoo" 1600/month, user not employed; a
diary with off-plan time this month.

1. Reports, Today section: shows an unearned-income figure and a paid-work-income figure, both with EUR.
2. Same for This week, This month, This year (four sections, two figures each).
3. The paid-work figure for a period equals the payable tags' earnings for that period (Studioshoo only here);
   with no payable tag it shows 0 EUR (or an equivalent empty state), not an error.
4. The unearned-income figure equals what the existing sidebar/Reports earnings logic produced before this
   change for the same period (no calculation changed).
5. The off-plan nudge text contains "distracting apps and activities".
6. Typing 14000 into a money box displays "14 000" (thin or normal space) after typing; the saved value is
   the number 14000.
7. Salary 2925 displays "2 925"; payable tag 1600 displays "1 600"; the new-tag amount box formats the same.
8. None of those boxes shows up/down arrows (spin buttons).
9. Previous salary row: label "monthly net (EUR)", number box, and an "= EUR x,xx/h" readout, laid out like a tag row.
10. Esc in the New payable tag line leaves it without adding a tag; clicking outside does the same.
11. After 10, no tag was added and config.json is unchanged.
12. Entering a non-number, a negative number and an empty box in a money box behaves as it did before (no crash).
13. Build clean (0 warnings with /warnaserror); tests pass; QA's shared suite covers lines 3, 4, 6, 7.

Added by Planner (mine, not in ACCEPTANCE.md):
14. The nudge sentence no longer contains the old phrase "distracting apps during working hours" anywhere (code, tests, MANUAL).
15. The Reports pair of figures follows the period tab: switching tabs changes the captions' period name and amounts accordingly.
16. Saving a money-box value writes a plain number to `config.json` (14000, not "14 000"); reloading Settings shows the grouped form again.
17. Money boxes outside Settings -> Income (scoring, reminders) are unchanged and still have their arrows.

## Safety notes

- QA runs only against a scratch `MENTOR_ROOT` (fixture above); never the real `data/progress.db`, root `config.json`, or the live app.
  Items 6/7/10/11 write `config.json`: use the scratch one and diff it before/after for line 11.
- No Coder/QA step may read or write `regular/winui/` or its documents.
- Do not edit the real `config.json` to produce the fixture; build the scratch config by hand.
- Before any live check compare the Release dll timestamp with the newest source (stale-build trap noted 2026-10-23).

## Open question

1. RESOLVED by orchestrator: Option A. Headline unchanged, paid work shown separately, no calculation change; the two figures must be
   labelled so a reader is not misled into adding them (see Design, strip copy).
2. RESOLVED by orchestrator: the salary label name is "Previous salary".

## Design

Designed with `ui-ux-pro-max` (WinUI stack, existing-look rule: match, do not invent) and `ui-design-rules.md`. This is a
reviewed-by-rules layout, not a user-approved visual; treat as placeholder until the user has seen it.

### D1. Reports: the two-figure strip

**Where.** First element of the income block in `Render()`, directly above the existing `IncomeCard` (which keeps its 44 px headline and
all its lines unchanged) and `EarningsCard`. Wrapped in the same `Card(...)` helper the other cards use, so the border, padding and
corner radius match. It follows the period selector, so there is one strip, not four (acceptance 1/2 = the same strip on each tab).

**Layout.** One card, a two-column `Grid`, columns `*` and `*` with a 16 DIP gap, `ColumnDefinitions` fixed so both columns are
reserved at all times (a column is never collapsed or hidden, even when its figure is 0,00). Both columns top-aligned. Each column is a
`StackPanel Spacing=2`, three lines:

| Line | Style | Left column | Right column |
|---|---|---|---|
| 1 caption | existing `Caption(...)` helper (small, uppercase, secondary) | `INCOME LOST OR GAINED — {period}` | `OF WHICH PAID WORK — {period}` |
| 2 figure | bold, FontSize 28 (smaller than the 44 px headline below, so the strip reads as a summary and the card below stays the headline), `MainWindow.FormatEur(...)` | `FormatEur(Math.Round(incomeNet,2))`, signed exactly as `IncomeCard` | `FormatEur(earnings.Sum(e=>e.Eur))` |
| 3 note | existing `Dim(...)` helper, FontSize 12, TextWrapping Wrap | `Total for the period. Paid work is already inside this figure.` | `Part of the total on the left, not added to it.` |

`{period}` is the same `periodName` string the existing cards receive (Today / This week / This month / This year), so captions follow
the tab (acceptance 15). A thin vertical divider (1 DIP, `DividerStrokeColorDefaultBrush`) between the columns, no "+" or "=" sign
between them, nothing that suggests addition. At narrow widths (< 560 DIP) the two columns stack vertically (left above right), same
copy; use a `VisualState`/`SizeChanged` switch or `ItemsRepeater`-free two-row fallback, but never horizontal scroll and never clipping.

**Colour.** No new hue. Both figures use the existing `IncomeBrush(value)` (the app's one accent for >= 0 and one alert colour for
< 0, already used by the cards). Sign and meaning are carried by text too: the negative left figure shows the minus sign from `FormatEur`,
and the note line is neutral. Captions, notes and divider are the neutral secondary scale. The strip never uses colour alone: the caption
names what the number is.

**States.**
- Normal: as above.
- Paid work empty (no payable tag or no tagged time): right column shows `0,00 €` formatted by `FormatEur` (never "-", never blank), the note
  line is replaced by `No time tagged with a payable tag in this period.` The existing EarningsCard keeps its longer pointer to Settings.
  Figure uses the neutral (non-alert) brush; `IncomeBrush(0)` as today.
- Negative left figure (unemployed, lost earnings): shows the signed negative amount in the alert brush; same caption (neutral, does not
  switch to "gained"). The existing IncomeCard below carries the "lost/gained" wording; the strip stays neutral on purpose.
- Zero left figure: `0,00 €` in the neutral/accent brush as `IncomeBrush(0)` returns; no special text.
- Left figure smaller than right (net negative while paid work positive): still correct, since it is "of which"; do not reorder or
  hide either.
- Error: the strip reads the same data as the cards in the same `Render()` pass; there is no separate error state. If the income service
  throws, the existing behaviour for the cards applies (the strip is not rendered separately from them).
- Loading: none (synchronous render, like the cards).

**Copy is exact** (note the em dash with spaces, as in the existing captions; euro formatting only via `MainWindow.FormatEur`, never a
hand-typed "EUR"):
- `INCOME LOST OR GAINED — {period}`
- `Total for the period. Paid work is already inside this figure.`
- `OF WHICH PAID WORK — {period}`
- `Part of the total on the left, not added to it.`
- `No time tagged with a payable tag in this period.`

Rationale for the wording (Option A): the left figure is the unchanged headline that already contains paid work, so it must not be called
"unearned income" next to a second figure that is part of it; "of which" and the notes make the relationship explicit. The old card captions
(`UNEARNED INCOME — ...` etc.) are untouched underneath.

### D2. Nudge text

Exact sentence: `You spent {h} h (~{eur}) off-plan {period}. Try blocking distracting apps and activities during working hours.`
Only the second sentence changes; the first sentence and its number formatting stay as in `InsightRules.cs`. No other wording changes.

### D3. Settings -> Income: the money boxes

**Order of the section (top to bottom), unchanged except the first item:**
1. Subtitle `Income`.
2. **Previous salary row** (replaces the NumberBox with header "Potential net monthly income (€)"), laid out exactly like a tag row.
3. `Currently employed` toggle (unchanged).
4. `Paid work` heading, helper text, tag rows (`PaidTagsHost`), then the `New payable tag` line, then the closing note (unchanged).

**The shared row (salary and every tag): one builder, same visual.** Horizontal `StackPanel`, Spacing 8, items:
- Money box, Width 240, Header (the label above the box) `Previous salary — monthly net (€)` for the salary row; `{tag label} — monthly net (€)` for tags. The existing code uses "(€)" with the euro sign, keep it in the header; the readout uses `FormatEur`.
- Hourly readout TextBlock, `= {FormatEur(value / IncomeService.WorkingHoursPerMonth)}/h`, bottom-aligned with the box, `Margin 0,0,0,6`, secondary brush. Updates live on every valid keystroke; when the box is empty or invalid it shows `= —/h` (an em dash in place of the amount), never `NaN`.
- Tag rows only: `Remove` button, bottom-aligned. The salary row has **no** Remove button and no placeholder for one (the salary row is never deleted). The readout column therefore ends the row; this is the one deliberate difference.

**Money box visuals.** A plain single-line text box, right-aligned digits would differ from the tag row today, so keep **left-aligned** text as the
existing NumberBoxes show. No spin buttons, no clear glyph that looks like one. Placeholder (empty state): `0`. Input scope: Number (numeric
on-screen keyboard). Display: integer part grouped with U+2009 thin space every three digits from the right; decimal part only if the user types one,
max 2 digits, culture decimal separator. Examples: `2925` -> `2 925`, `1600` -> `1 600`, `14000` -> `14 000`, `1600,5` -> `1 600,5`.

**Separator policy (decided by the user 2026-10-23; supersedes any earlier reading of "." as a decimal):**
- A **comma is always the decimal separator**, in every culture.
- A **dot followed by exactly three digits is a thousands separator**, in every culture: `14.000` -> `14 000`,
  `1.600` -> `1 600`, `14.000.000` -> `14 000 000`, `14.000,5` -> `14 000,5`. It is dropped on display.
- A **dot followed by 1 or 2 digits is a decimal**: `14.5` -> `14,5` (culture separator once settled).
- While a decimal dot is still being typed (one or two digits, or none, and nothing after them) it stays a dot on
  screen, so typing `1.000` does not turn into `1,00` on the way; when focus leaves the box it becomes the culture
  separator (`MoneyFormat.Settle`). Without this, the third digit could never reach the thousands rule.
- A dot followed by four or more digits is treated as a decimal and cut to two decimal digits, as before.
- The saved value is the plain number in every case (`14.000` saves `14000`).
Grouping is applied as the user types and the caret stays after the same digit it was after before regrouping. On load, values are shown grouped (2 925, not 2925).
Whole amounts are shown without a trailing `,00`.

**Money box states.**
- Normal: grouped digits.
- Empty: placeholder `0`; readout `= —/h`; nothing is written to config; no error banner (matches previous NaN behaviour).
- Invalid characters typed or pasted (letters, `-`, symbols): silently dropped, only digits and one decimal separator survive; if nothing remains the box is empty. No error text, no beep, no red border (this matches the old NumberBox, which simply clamped).
- Negative: the minus sign is never accepted (same effect as the old `Minimum=0`).
- Too long: input beyond 9 integer digits is dropped.
- Focus: standard WinUI TextBox focus rail (accent underline). Nothing custom.
- Saved feedback: unchanged — the existing `SaveStatus` text shows `Saved.` after a valid edit. No new toast.

**Accessible names (for UIA, unchanged identity).** The salary box keeps `x:Name="MonthlyIncomeBox"`; its UIA Name equals its header text, `Previous salary — monthly net (€)`. Tag boxes: `{label} — monthly net (€)`. New-tag amount box: header `Monthly net (€)` as today. No arrows means no `Increase`/`Decrease` child buttons in the UIA tree for these boxes (QA check M1).

### D4. Settings -> Income: the "New payable tag" line

Layout unchanged: horizontal row of name text box (header `New payable tag`, placeholder `Name`, width 160), amount money box (header `Monthly net (€)`, width 150, placeholder `0`), `Add` button bottom-aligned. Everything stays on one line, same order, same widths; the only visual change is that the amount box has no arrows and shows grouped digits.

**Leaving the line.**
- Esc in either field: the name field is emptied, the amount field is emptied (placeholder `0` shows), nothing is added, nothing is saved, focus moves to the section's scroll area (no field keeps a focus rail). No message of any kind appears (leaving is not an error).
- Click anywhere outside the line (including empty page space, the section heading, another section): same result, if and only if focus was inside the line. A click on `Add` is inside the line and adds as before.
- A click on another input (for example the salary box) both leaves the line and focuses that input; the click is not swallowed.
- Tab out of the line: unchanged (does not clear, does not add), because that is keyboard navigation, not "leave".
- Add with an invalid line (empty name or empty amount) keeps today's behaviour and message; do not change the copy.

### D5. Must not improvise

- No new hues, no new icons, no emoji, no coloured badges, no tooltip on the strip.
- No "+" or "=" glyphs between the two Reports figures, no combined total beyond the existing headline.
- Do not change the existing IncomeCard / EarningsCard text, sizes or order; the strip is added above them only.
- Do not change the other NumberBoxes in Settings (scoring, reminders, retention): they keep their arrows.
- No grouping inside the saved JSON or in any export; the thin space is display only.
- Do not add a Remove button to the salary row and do not rename the salary setting key.
- Do not add a confirmation dialog for Esc or outside click.
- The thin space character is U+2009 only (not a normal space, not U+00A0); copy examples above are that character.
- Do not show `NaN`, `Infinity`, `-` alone, or an empty euro amount anywhere.

### D6. Design checks for QA (extra, mechanical, not from ACCEPTANCE.md)

- M1. UIA tree: the salary box, each tag box and the new-tag amount box have no descendant Button named `Increase` or `Decrease`; the scoring NumberBoxes still do.
- M2. Reports on each of four tabs: the strip has exactly two figure TextBlocks whose texts contain the euro sign; the captions contain the tab's period name; the left figure text equals the headline text of `IncomeCard` on the same tab.
- M3. With no payable-tag time (scratch config without the tag): right figure text equals `FormatEur(0)` and the line `No time tagged with a payable tag in this period.` is present; no element text contains `NaN`.
- M4. Strip caption texts match D1 copy exactly; no element between the two figures has text `+` or `=`.
- M5. Salary row: its header text is `Previous salary — monthly net (€)`; a readout TextBlock to its right matches `= .*/h`; UIA bounding rectangles of salary box and a tag box have the same Width (240 DIP) and the same Left edge; the salary row has no Button named `Remove`.
- M6. Typing `14000` into the salary box: displayed text is `14 000` with U+2009 (or U+0020 accepted by acceptance), caret at end; typing `abc` leaves it unchanged; empty -> readout `= —/h` and `config.json` unchanged.
- M7. The new-tag line: UIA bounding rectangles of name box, amount box and Add button share a row (tops within 2 DIP of each other, except the button which is bottom-aligned within the same band); after Esc and after an outside click both fields are empty, the tag list is unchanged and `config.json` is byte-identical.
- M8. At a window width that forces the narrow layout (< 560 DIP content width), no Reports strip text is clipped (every figure's bounding rectangle lies inside the card's), and the salary row readout is fully visible (right edge inside the page viewport).
- M9. Nudge text found anywhere on screen contains `distracting apps and activities`.

## Conflict with plan

none

## Lessons

none
