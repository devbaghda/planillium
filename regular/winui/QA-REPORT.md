VERDICT: PASS - workflow=regular feature=b1b-money-boxes defectsQa=0

Round 5. Build under test: a4275c9 (winui-rebuild), isolated worktree (removed afterwards), Debug, scratch MENTOR_ROOT (fixture: salary 2925, tag Studioshoo 1600, empty DB), MENTOR_INSTANCE_SUFFIX=verify, driven by UI Automation (pywinauto) with real mouse/keyboard. No real data touched; agentic/ not touched; only my own process (PID 22512) killed.
Build: `dotnet build -p:Platform=x64 -c Debug -warnaserror --no-incremental` (app) 0 warnings / 0 errors; test project strict build 0/0; `dotnet test --no-build` 229/229 pass.

D2 (round 4) is fixed. All ten acceptance lines pass as written; no new defects.

| Line | Result | Evidence |
|---|---|---|
| 1 salary: type 3000, no leave | PASS | box "3 000", config.json still 2925 |
| 2 salary: Tab | PASS | config.json 3000, box "3 000" (thin space U+2009) |
| 3 salary: 3100 + Enter | PASS | config.json 3100 |
| 4 payable-tag box, same three | PASS | 1600 while typing; 3000 after Tab; 3100 after Enter |
| 5 new-tag amount box | PASS | 777 + Tab and 778 + Enter: paid_tags unchanged; name "Zed" + Add saved the tag with 778 |
| 6 click Income heading, type "9" | PASS | 12 trials (headings "Income", "Paid work", "Diary time carrying" x 4 boxes incl. new-tag amount): focus read before typing is the `IncomeFocusPark` button every time; all box values and config.json unchanged. Also in the first full run (salary, tag box, new-tag box then heading): unchanged |
| 7 minus hint | PASS | In salary, tag and new-tag boxes "-" shows "Amounts can't be negative.", box stays empty; hint gone after "5", back on a second "-", gone after Tab |
| 8 empty/invalid + leave | PASS | empty+Tab and "abc"+Tab keep previous config value (4000); empty+Enter no crash; app alive. "." + leave stores 0 and shows "0," (as pre-B1b) |
| 9 thin-space, no spinners | PASS | "2 925", "3 100" with thin space; no spin/increase/decrease buttons in UIA tree |
| 10 build/tests | PARTIAL (unchanged) | Build clean, 229/229. "Shared suite covers lines 1-4 and 7" cannot be unit tests: MoneyBox derives from a WinUI TextBox and the test project may not reference the WinUI app. Lines 1-8 are covered by live UIA scripts in qa/ |

D2 re-check (focus in new-tag NAME box, name "qq", amount 55, then click heading, then type "9"): 4/4 trials (focus in name or amount box x heading "Income" or "Paid work"): line cleared (name '', amount 0), focus = `IncomeFocusPark`, salary box 5 000 -> 5 000, config.json unchanged. Evidence qa/nl.py.

B1 checks (re-run)
- New-tag-line click-outside: PASS. Name "qq" + amount 55: click inside the name box and inside the amount box keep both; click on the heading, on a blank caption-row area inside the page, and on the Income card in the right-hand section list each clear the line (name '', amount 0). Heading and page-area clicks park focus; the section-list click lands focus on the Income list item. Evidence qa/r5b.py.
- Esc: PASS. Esc clears the line and focus goes to the "Income" list item.
- Tab order: PASS, undisturbed. Salary box, Shift+Tab x2 gives Settings, then "Log spend (no regrets)", both before any park and after park/Tab round trips; Tab x4 from salary gives Employment toggle, Studioshoo box, Remove, Zed box. From the park, Tab x4 goes salary, Employment toggle, Studioshoo, Remove; Shift+Tab x2 gives Settings, Log spend; after leaving the park, Shift+Tab from salary skips it. Parking three times in a row works. Evidence qa/tab5.py.

## Defects
None.

## Security (diff 9572f5e..a4275c9, SettingsPage code, plus earlier B1b MoneyBox.cs)
Injection none found; authz/flag bypass none found; secrets none; deserialisation none; data exposure none; input validation: minus/garbage/empty handled without crash; business-logic: save only on leave/Enter holds, D2 route closed; supply chain: no dependency change; resource abuse none. /security-review not run (no network use); applied by hand.

## Untestable
None.

## Extra observations (not defects, not in ACCEPTANCE)
- After empty+Tab or "abc"+Tab the box shows empty while config.json keeps the previous value (display and stored value disagree until the page is reopened). Candidate for a later batch.
- "." + leave stores 0 and shows "0," (pre-B1b behaviour).
- Blank space immediately beside a box (round 4: right of the salary box) is not a heading and keeps focus in the box; typing there still edits it. Not re-driven this round; same family as D2, outside the checklist.
- The park button is an unnamed Button (id IncomeFocusPark) in the UIA tree, Opacity 0.01; a screen reader could encounter it. Minor.
- Tag names are stored lower-cased ("Zed" -> "zed"); existing behaviour.

## Lessons
- A hard-coded click coordinate outside the app window lands on whatever is behind it (here an AnyDesk window stole focus and made Esc/Tab results meaningless): compute click points from the app window rectangle, call set_focus first, and print the focused element's process/name so a stolen foreground shows up at once.
- Window position moves between runs (left 355 -> 485 -> 303 -> 26): select elements by name and width, never by absolute left edge; update hardcoded paths (worktree name, pid file) every round.
- Clicking a section-list card navigates; the "click outside" test must use the card of the section already open.
- Run scripts from the scratchpad folder so the relative pid.txt resolves; set PYTHONIOENCODING=utf-8.

Files: D:/CLAUDE/Planillium/regular/winui/qa/ (b1b_launch.py, b1b_t.py, b1b_full.py, l6r4.py, nl.py, r5b.py, tab5.py, rest.py, lib.py, older round scripts and screenshots).
