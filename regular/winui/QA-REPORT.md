VERDICT: PASS WITH CAVEATS - workflow=regular feature=b1c-focus-enter defectsQa=0

Build under test: 71a0038 (winui-rebuild), isolated worktree, Debug. Scratch MENTOR_ROOT (salary 2925, Studioshoo 1600, empty data/), MENTOR_INSTANCE_SUFFIX=verify, driven by UIA (pywinauto), window maximised so no box scrolls out of view. Real data and the live app untouched; only my own process was killed.

Build: strict build 0 warnings / 0 errors. Tests: 229 passed / 0 failed (regular side; the agentic side was not read).

## Per-line results

| Line | Result | Evidence |
|---|---|---|
| 1 | PASS | Typed 3000 + Enter: config.json 2925 -> 3000; focus = `IncomeFocusPark` (invisible button), not the salary box. Typing "9" afterwards: config and all three boxes unchanged. |
| 2 | PASS | Studioshoo 2000 + Enter: config 2000, focus on `IncomeFocusPark`, "9" changed nothing. |
| 3 | PASS | 3100 + Tab: config 3100; focus went to the next control (Employment toggle), not the salary box. A second Tab continued to the Studioshoo box (continues from where it was, not the salary box). Caveat: "nothing has the cursor" (BRIEF) is read as "not the box"; Tab still moves to the next control, which is normal Tab behaviour. |
| 4 | PASS | Blank-space click after 3200: saved 3200, focus on `IncomeFocusPark`. Clicking the Studioshoo box after 3300: saved 3300, focus on that box. |
| 5 | PASS | name "zed" + amount 500 + Enter in the name box: tag zed 500 added, boxes reset ('' / 0), focus on `IncomeFocusPark`. Same with "yan" 700 and Enter in the amount box. Add button on "btn" 900 gives an identical config entry (tag/label/amount); focus stays on the Add button (pre-existing button behaviour, no box holds the cursor). |
| 6 | PASS | "-500" + Enter: nothing added, red message visible, cursor stays in amount box, text kept. Empty amount + Enter and empty amount + Add button: nothing added, "Monthly net needs a number." visible. No name + Enter: nothing added, cursor stays in the name box. |
| 7 | PASS | Salary, Studioshoo and new-tag amount boxes each: "-500" stays "-500", red message with error icon visible, config.json unchanged. |
| 8 | PASS | Adding digits gives "-500 123" (thin space), sign kept, message still visible, config unchanged, all three boxes. |
| 9 | PASS | Deleting the minus leaves "500 123", message disappears (all three boxes). |
| 10 | PASS (see caveat 1) | Salary and Studioshoo boxes: Tab and blank-space click and Enter with "-500 123" in the box: text stays, message stays, config unchanged. Add-line amount box: Tab keeps text and message, config unchanged. Click outside clears the line (conflicts with line 11, see caveat 1). |
| 11 | PASS | Esc in the add line clears name and amount, no tag added. Click outside clears the line (amount "-5" and name removed), no tag added. |
| 12 | PASS | Screenshots `qa/b1c_neg_salary.png`, `qa/b1c_neg_newtag.png`: red text with a red circled "!" icon directly under the box, readable at a glance on the dark theme. No agentic baseline compared (cannot read agentic/); judged on its own. |
| 13 | PASS | Thin-space grouping while typing (14 000, 1 234 567); no spin/increase/decrease buttons found. |
| 14 | PASS | Build clean, 229/229 tests; lines 1-10 driven live (above). |

## Defects
None.

## Security (diff reviewed: SettingsPage.xaml/.cs, MoneyBox.cs; UI-only change)
Injection, auth, secrets/crypto, deserialisation, data exposure, supply chain (no dependency changes), resource abuse: none found. Input validation: negative, empty, letter-only and huge values are refused or settled without being saved; nothing negative reached config.json in any test. `/security-review` was not available here; checklist applied by hand.

## Untestable
None.

## Caveats and extra observations
1. Lines 10 and 11 conflict for the add-line amount box: line 10 says click-elsewhere keeps the text, line 11 (and the BRIEF's "rule kept") says click outside clears the line. The build follows line 11 / kept rule; Tab keeps the text. Graded PASS; the user should confirm this is what he wants.
2. A tag amount very large (500 123) shows "= --/h" in the hourly readout (appeared for 500123 only; 1600 and 2925 show a figure). Probably a cap, outside this feature; not investigated.
3. After a refused entry the green/neutral "Saved." status at the bottom of the page still shows from the previous successful save (stale, harmless).
4. Pressing Add with a button click leaves focus on the Add button, not "nowhere"; the brief only asks it for Enter.

## Files
QA scripts and screenshots: D:\CLAUDE\Planillium\regular\winui\qa\b1c_*.py (b1c_h.py is the helper; paths inside point at my scratch folder) and b1c_neg_*.png. The temporary worktree was removed.

## Lessons
- A small window scrolls the Income boxes out of view after tags are added; my first run typed into nothing and logged false results. Rule: maximise the window and assert the target box `is_visible()` before typing.
- A click-away coordinate outside the window hits another application (AnyDesk took focus). Rule: compute click-away points from the window rectangle, inside it.
- My first message filter searched only for "negative", so the empty-amount message ("Monthly net needs a number.") looked absent. Rule: dump all visible text near the control before concluding a message is missing.
