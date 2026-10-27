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
