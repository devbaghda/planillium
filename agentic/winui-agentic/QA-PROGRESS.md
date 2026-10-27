# B1g money-box keys -- QA plan (start 10:32, limit 20 min hard, ends 10:52)
- P build Debug in worktree qa-b1g (HEAD + 3 copied files): 1 run, ~2 min (measured earlier rounds).
- C1 qa/b1g_run.py, one launch: salary + Studioshoo box: L1-L3 Esc (4 inputs), L4 -5 Enter, L5 -5 Esc (after Enter and direct), L6 12 digits x 5 exits (Enter/Esc/Tab/click blank/click other box), standing empty/abc/-5 x Tab/click/Enter, L7 valid Enter; then add-line Esc (name, amount), refused mouse Add+Esc, Enter in add line. 1 trial each (key logic, not timing-dependent); ~3 s/step x ~60 steps + 30 s launch = ~4 min.
- C2 re-run of failed lines only if any, ~2 min. Report ~4 min. Total ~12 min. Full A/B/C matrix NOT run (PM scope).

19:35 start
case a (run edge c1 tag box, holds 0/.05/.25, 54 trials, 0 fail, 9-check incl) PASS ~6min
case a2 (probe_edge, 60 trials right-1..-10 px, 0 fail) PASS
case b (edge, newamt/salary/Studioshoo, 72 trials, 0 fail) PASS ~4min; total ~10min
case c+d (edge, inactive-first-click 24, wheel 24, wheel+random 24, 0 fail) PASS ~6min; total ~16min
case e (interior smoke 12 pts, c3 interior away=True, all OK) PASS ~3min; total ~19min
case f (B1-B1d regression) NOT RUN: time limit reached (20 min)
20:26 case f (B1-B1d regression, b1f_regress.py A,B; C not reached) lines 6; 1 trial per script item (script's own holds) because budget; result so far 114 PASS 0 FAIL, stopped at B1d-9 and killed by QA at time limit; minutes elapsed ~9 of 7 (over limit, build+dotnet test ~1.5 min)
20:31:29 case f part B (B1d-9 to end of part B, 24 lines, 1 trial each via b1f_run_b1d9.py, the two-leave-path loop is deterministic: 1 pass each) | lines B1d-9 x24 | 24 PASS / 0 FAIL, no ABORT/ERROR | minutes elapsed 3.0 of 14 (start 20:28:23)
20:40:54 case f part C (b1f_regress.py C, standing input-box matrix, 144 checks planned) | lines C x79 reached (SAL 36, TAG 36, NewPaidTagName 7) | 72 PASS / 7 FAIL (all 7 SALARY FOCUS after Esc or Enter in Previous salary box), timed out at 540 s before end, no SUMMARY line; 65 of 144 not run | minutes elapsed 12.5 of 14 (start 20:28:23), STOPPED at limit
10:35 P build Debug -warnaserror worktree: 0 errors, 1 run (needed for the binary), 1m35s; elapsed 3 of 20
10:38 C1 b1g_run.py: L1-L7 both boxes + standing exits + add-line/B1d regression, 44 checks, 1 trial each (key logic, not timing-dependent; first launch aborted by a foreground steal from another app, re-run whole script 2m43s); 44/44 PASS; elapsed 7 of 20. C2 not needed.
