VERDICT: PASS - workflow=agentic feature=b1b-money-boxes defectsQa=0

Re-run after fix commit e3993aa (D1 from the previous report). Isolated worktree `.claude/worktrees/qa-ag` (removed after), Debug x64, scratch MENTOR_ROOT, MENTOR_INSTANCE_SUFFIX=verify, driven by UI Automation + real keystrokes/clipboard paste, config.json read from disk after each step. No real data touched. Full checklist re-run, not only line 7.

## Build and tests
- Strict build `dotnet build -p:Platform=x64 -c Debug -warnaserror --no-incremental`: 0 warnings, 0 errors.
- `dotnet test`: 346/346 passed (includes `B1bMinusTests.cs`, now in the test project; content identical to QA's file apart from line endings).

## Per-line results (all observed live)
| Line | Result | Evidence |
|---|---|---|
| 1 Type 3000, don't leave: config still 2925 | PASS | box "3 000", config 2925 after 1 s |
| 2 Tab: config 3000, box "3 000" | PASS | config 3000, box thin-space "3 000" |
| 3 Type 3100 + Enter: config 3100 | PASS | config 3100; Tab after Enter did not rewrite the file (mtime unchanged) |
| 4 Same for payable tag box | PASS | typing: 1600 unchanged; Tab: 1700; Enter: 1800 |
| 5 New-tag amount box: no save before Add | PASS | typed 700, Enter, Tab: config unchanged, tag not added; after Add the new tag holds 700 |
| 6 Click Income heading, type "9": nothing changes | PASS | salary box then Income heading then "9": config unchanged, box unchanged; typed 3200 then heading then 9: config 3200, no 9 in box; tag box + "Paid work" heading: unchanged |
| 7 Minus: hint, no minus kept, hint clears on next valid key / leave | PASS | see D1 re-test below; hint "Amounts can't be negative." visible in UIA tree and in screenshot (`qa/d1_hint_visible.png`) in all three box kinds |
| 8 Empty/invalid entry on leave | PASS | empty salary, "abc", empty tag box: values kept in config, app alive, no crash |
| 9 Thin-space, no spinners | PASS | "14 000" shown, 0 spinner buttons |
| 10 Build clean, tests pass, suite covers 1-4, 7 | PASS | 0 warnings, 346/346; UI scripts `qa/run_b1b.py`, `qa/run_line5.py`, `qa/run_d1.py` cover 1-8 |

## D1 re-test (line 7), run for salary box, payable-tag box and new-tag amount box
- Type "5" then "-": hint visible at 0.1 s, 0.5 s, 1.5 s and 3.5 s (stays), box shows "5": PASS (all three boxes).
- Next minus-free keystroke ("0"): hint gone, box "50": PASS (all three).
- Minus then Backspace: hint shown, then cleared by the Backspace: PASS (all three).
- Double minus "--": hint shown, no minus in box: PASS (all three).
- Leaving via Tab: hint cleared: PASS. Leaving by clicking the Income heading: hint cleared: PASS (all three).
- Paste "-500" -> box "500" with hint; paste "5-0" -> "50" with hint; paste "--" into empty box -> "" with hint; paste of a valid "123" after a minus -> hint cleared, box "123": PASS.
- Typing "-1-2-" (ends with minus): box "12", hint shown; typing "-1-2" (ends with digit): hint clear: PASS.
- Info only: an arrow key (Left) with the hint showing keeps the hint (no text change); minus then Enter in an empty box keeps the hint and does not crash or change the saved value. Both are reasonable and not against the checklist.
- Harness sanity: a plain text box accepted "a-b", so the minus keystroke is really delivered.

## Defects
None.

## Coder's suggested extra checks (re-run)
Click heading then type 9: PASS. Enter then Tab saves once: PASS. Section switch commits the box (typed 3333, switched to General, config 3333): PASS. Remove while tag box focused: PASS (tag removed, app alive). Close window while a box is focused (typed 4444, Close): config 4444, app alive in tray: PASS.

## Security (diff e3993aa on top of 5d2a46a, then touched file `Controls/MoneyBox.cs`; applied by hand, `/security-review` not run)
- Injection, auth, secrets/crypto, deserialisation, supply chain, data exposure: none found (a one-field UI flag fix; no input reaches SQL, shell, paths or network; no dependency or config change).
- Input validation: none found; garbage, pasted minus signs and empty entries leave saved values untouched.
- Business-logic abuse, resource abuse: none found.

## Untestable
None.

## Extra observations
- Not a defect, harness-related: a fresh scratch root shows a "Good morning" dialog about 50 s after launch that steals keyboard focus; it made two paste lines look failed in an early QA run until the dialog was dismissed (screenshot `qa/d1_fail_after_dashdash.png`). Later runs wait for and dismiss it first. The app itself behaved correctly.
- Empty/invalid entry on leave leaves the box empty while config keeps the old value, until the page is reopened. Matches "as before B1b"; not counted.
- Process kill with unsaved text not tested (loses it by design).

## Files
`D:\CLAUDE\Planillium\agentic\winui-agentic\qa\` : `common.py`, `run_b1b.py`, `run_line5.py`, `run_d1.py` (D1 edge cases; env `LOOPS` limits boxes), `dbg_paste.py`, `B1bMinusTests.cs`, screenshots `d1_hint_visible.png`, `d1_paste.png`, `d1_fail_after_dashdash.png`. Scripts expect worktree `D:\CLAUDE\Planillium\.claude\worktrees\qa-ag`, removed.

## Lessons
- Two paste lines failed intermittently only in long runs; the cause was a timed "Good morning" dialog stealing focus, not the product. Rule: when a UIA step fails only late in a run, take a screenshot and check for popups before calling a defect; wait for and dismiss timed dialogs at the start of a scratch run.
- Bisecting with an env-var switch (`LOOPS`) isolated the trigger quickly; note substring matching on the switch value ("tag" matched "newtag") nearly misled the bisect, so use exact list membership.
- Reading the fix in code looked plausible; only the live hint timing check (0.1 s to 3.5 s polling plus screenshot) proves a "show while X" line.
- Windows path length: keep the worktree short under `.claude/worktrees/`.
