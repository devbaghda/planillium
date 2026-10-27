TIME-LIMIT: 14, 5/6 cases done (case f partial: part B done, part C 79 of 144 checks run, not finished)
VERDICT: FAIL - workflow=agentic feature=b1f-focus-jump defectsQa=7 ; commit tested e82135f (Debug, strict build 0 warn/0 err, worktree agent-ae63c0d098943cf5f, scratch MENTOR_ROOT, suffix verify, real SendInput mouse/keys). Round 1 and round 2 cases a-e: PASS (see earlier rows). Round 3 (case f, this run): B1d-9 to end of part B 24/24 PASS; part C 72 PASS / 7 FAIL (all 7 are one behaviour: focus stays in the Previous salary box after Esc or Enter, see f-C).

| Case | Lines | Trials (reason) | Result |
|---|---|---|---|
| a. edge strip after click in New-tag box, holds 0/50/250 ms, typed "9" in same trial | 1, 5 | 54 + 60 (timing-dependent, holds varied) | PASS 0/114 jumps, "9" never landed |
| b. edge strip after tag-amount / salary / Studioshoo | 2, 5 | 72 (2 holds x 2 reps x 6 pts x 3 sources) | PASS 0/72 |
| c. first click after window inactive (edge) | 3, 5 | 24 | PASS 0/24 |
| d. wheel scroll then edge click | 4, 5 | 48 | PASS 0/48 |
| e. interior smoke (12 pts) | 1-5 | 1 pass x 12 points (+ inactive-first-click interior) | PASS |
| f. B1-B1d regression, part A and B up to B1d-8 (earlier round) | 6 | script's own trials, 1 pass each | PASS 114 / 0 FAIL |
| f. B1d-9 to end of part B (own runner qa/b1f_run_b1d9.py, output qa/b1f_reg_f2_B1d9.out) | 6 | 1 trial per line (deterministic loop of 3 inputs x 4 leave paths x 2 boxes) | PASS 24 / 0 FAIL |
| f-C. standing input-box matrix (part C, qa/b1f_reg_f2_C.out) | 6 | 1 trial per check; run killed by timeout at 540 s after 79 of 144 checks (SAL 36, TAG 36, NewPaidTagName 7) | 72 PASS / 7 FAIL (all SALARY FOCUS, see below); 65 checks NOT RUN: time limit |

Case f totals: 210 PASS, 7 FAIL, 65 of 144 part C checks NOT RUN (time limit at 14 min).

Failing lines (part C, Previous salary box, focus check "SALARY FOCUS"):
1. C Previous salary empty esc: FAIL. Typed "", box shows "", "Empty amount not saved; the saved amount" shown, stored 2925 / 1600 correct, focus left in the Previous salary box.
2. C Previous salary spaces esc: FAIL. Same pattern, typed "   ".
3. C Previous salary text esc: FAIL. Same pattern, typed "abc".
4. C Previous salary minus enter: FAIL. Typed "-5", message "Not saved: Amounts can't be negative.", stored 2925, focus stays in the Previous salary box after Enter.
5. C Previous salary minus esc: FAIL. Typed "-5", message shown, focus stays in the box.
6. C Previous salary long esc: FAIL. Typed 12 digits, box shows 999 999 999 999, no message, stored 2925, focus stays in the box.
7. C Previous salary valid esc: FAIL. Typed 2500, box shows 2 500, stored 2925 (valid value not saved after Esc, script expects the check only for non-Esc exits), focus stays in the box.

Note on the 7 FAILs: the script's own rule says focus must not stay in the Previous salary box, except after clicking the other box. All 7 are Esc (plus Enter on a refused minus). Whether Esc should release focus is a behaviour question the checklist does not settle. I did not infer it from code. PM to decide: either a defect (fix: Esc or refused Enter parks focus) or a script-rule change. Stored values were never wrong in any of the 7.

Defects: defectsQa=7 counts the 7 failing lines above. They are one behaviour; if the PM rules Esc may keep focus, defectsQa becomes 0 and the verdict PASS WITH CAVEATS (65 part C checks not run).

Defects: none beyond the above. Security: change is focus/pointer/layout only (SectionContent padding, release claim); no input, path, secret or data surface found in earlier rounds; nothing new in this round.

Untestable: none.

Extra observations:
- Part C not finished. The 144-check matrix needs about 7 s per check (about 17 min), not the 5 min planned; the 540 s timeout stopped it at check 79. The remaining 65 are (NewPaidTagName rest, NewPaidTagRate 36) and were not run.
- Killed by timeout: the script's final kill() did not run, but no Planillium.App.exe from the worktree remained (checked). The live Release Planillium.App.exe (pid 17404, regular\winui) was not touched.
- Previous salary empty + Esc leaves the box empty while stored stays 2925 (message shown). The script excludes this from its check, so it is not a FAIL; noted for the PM.
- Outputs: qa/b1f_reg_f2_B1d9.out (24 PASS), qa/b1f_reg_f2_C.out (72 PASS, 7 FAIL, no SUMMARY line).

New cases (for qa-extra.md):
1. Previous salary box, type "-5" or text or spaces, then press Esc: focus should leave the box (script rule); observed focus stays in the box. Decide expected behaviour first.
2. Previous salary box, empty, then Esc: box shows empty while saved amount is 2925; message says the saved amount is kept. Expected: box shows the saved amount or the message only, PM to decide.
3. Previous salary box, 12 digits, then Esc: no message, box shows 999 999 999 999 with stored unchanged. Expected: refusal message or clamp, PM to decide.

Lessons (round 3):
(1) Budget again short for the regression: part C alone needs about 17 min at 7 s per check; the BUDGET-REQUEST should have said so at start. Put the planned minutes per script item in the brief.
(2) Reusing helpers without editing the regression script worked: exec the helper section of b1f_regress.py up to the "A: controls" marker, then run one part's block with the same launch/kill wrapper (qa/b1f_run_b1d9.py). Reuse this for any item-level start.
(3) Capture output to qa/*.out with PYTHONIOENCODING=utf-8 - done for both runs, nothing lost.
(4) Stop at the limit first: timeout 540 s ended the run at 20:40:29, inside the 14 min limit (20:42:23).
