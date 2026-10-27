VERDICT: PASS WITH CAVEATS - workflow=agentic feature=B1g money-box keys defectsQa=0

Build: worktree `D:\CLAUDE\Planillium\.claude\worktrees\qa-b1g` = HEAD 81c1535 + coder's uncommitted MoneyBox.cs, MoneyFormat.cs, SettingsPage.xaml.cs. Debug `-warnaserror --no-incremental`: 0 errors (warnings are errors). Tests not re-run (precheck 362/362 on coder tree, brief). Run: scratch MENTOR_ROOT (scratchpad\qa_b1g\root, empty DB, fixture salary 2925, Studioshoo 1600), MENTOR_INSTANCE_SUFFIX=verify, AI URL to dead local port. Real SendInput clicks and keys. Script `qa/b1g_run.py`, output `qa/b1g_run.out`, screenshot `qa/b1g_end.png`. Time used: about 8 of 20 min.
Caveat: line 8 only partly run (full B1-B1f A/B/C matrix not run, by PM scope).

## Acceptance (SAL = Previous salary, TAG = Studioshoo box)

| Line | Result | Evidence |
|---|---|---|
| 1 Type 2500 + Esc | PASS (SAL, TAG) | box 2 925 / 1 600, focus on Nav (no money box), stored unchanged, status "Change discarded; ..." |
| 2 Empty + Esc | PASS (SAL, TAG) | same as 1 |
| 3 abc + Esc; spaces + Esc | PASS (SAL, TAG) | same as 1 |
| 4 -5 + Enter | PASS (SAL, TAG) | box -5, focus still in the box, "Amounts can't be negative." under box and "Not saved: ..." in status, stored unchanged |
| 5 -5 + Esc (direct, and after the refused Enter) | PASS (SAL, TAG) | box shows saved amount, focus Nav, negative message gone, stored unchanged |
| 6 12 digits, exits Enter / Esc / Tab / click blank / click other box | PASS (SAL, TAG, 5 exits each) | while typed the box shows 999 999 999 999 and "Amount is too large (9 digits at most)."; after every exit the box shows the saved amount, stored unchanged; status "Not saved: Amount is too large ... saved amount is back" (Esc: "Change discarded"). Enter/Esc/click blank park focus on Nav; Tab goes to next control (Employed toggle / Remove), click other box goes to that box |
| 7 valid + Enter saves and parks; Esc in add line clears and leaves | PASS | SAL 3025 and TAG 1700 saved, box shows them, focus Nav; add line Esc from name and from amount field: both fields empty, focus Nav, no tag added |
| 8 earlier lines / build / standing checks | PASS WITH CAVEATS | build clean; standing checks below all PASS; full B1-B1f regression not run (scope) |

## Standing input-box checks (app-qa-checks 5), scope from diff

| Check | Result | Evidence |
|---|---|---|
| empty + Tab (SAL, TAG) | PASS | saved amount back, focus to next control, "Empty amount not saved" |
| abc + click blank (SAL, TAG) | PASS | saved amount back, focus Nav |
| empty + Enter, abc + Enter (B1d Enter) (SAL, TAG) | PASS | saved amount back, focus Nav, stored unchanged |
| -5 + Tab, -5 + click blank (SAL, TAG) | PASS | old rule holds: -5 kept with message, not saved |
| Add line: refused mouse Add (empty amount) then Esc | PASS | hint "Monthly net needs a number." shown, then line cleared, hint gone, no tag |
| Add line: Enter adds and parks | PASS | tag zed 500 added, focus Nav |

44/44 checks PASS, one trial each (key-handling logic, not timing-dependent).

## Defects
None.

## Security
No real data touched (scratch root asserted inside scratchpad); no key or network call (AI URL to 127.0.0.1:9); only own PID killed. The live user instance (PID 17404) was left alone. Diff adds no file, network or secret handling.

## Untestable
None. Not run by scope: full b1f_regress A/B/C matrix.

## Extra observations
- First launch aborted after 3 checks: another app (non-ASCII edit window, not Planillium) took the foreground; the guard refused the click. Script now re-fronts the app before each box entry; the whole run was repeated clean.
- "Nowhere" focus is reported by UIA as the Nav element, as in earlier rounds.
- Esc in an unedited box (no change) not separately checked; code shows no status message in that case.

## New cases
- Saved money box, type 999999999999: message "Amount is too large" visible while typed; after Enter/Esc/Tab/click blank/click other box the box shows the saved amount, nothing saved.
- Saved money box, type -5, Enter (stays with message), then Esc: box shows saved amount, cursor leaves, negative message gone.

## Lessons
- During a run another app can take the foreground: re-front the app before each case, not only at launch; the fg guard turns it into an abort, not a stray keystroke (good), but costs a re-run.
- One script, one launch, both boxes x all exits fit in under 3 min (~3.5 s per check): a scoped matrix from the diff is cheap enough to always cover every exit.
