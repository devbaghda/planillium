# MODEL-LOG -- B1d (agentic)
- designer: not launched -- no new screen.
- planner: not launched -- brief and acceptance are implementation-ready fix list.
- coder: opus -- focus/key-event/leave timing, failed at lower levels in B1b/B1c; user found stray-focus after QA PASS.
- coder outcome: ok (commit 9ad951c; all 7 items, build clean, 362 tests)
- qa: sonnet -- fixed ACCEPTANCE lines 1-11 with real clicks/keys; note guard blocks UIA SetFocus into Income boxes, QA must use real clicks/keys. Guard QA must not click outside app or send keys unless app is foreground; no Notepad.
- qa outcome: round 1 FAIL (Enter in emptied box; refused mouse Add vs Esc) -> fix pass
- coder (fix pass 1): opus -- same focus/key-event class; two interacting paths (Enter leave, keep-state vs Esc).
- qa (re-run 1): sonnet -- same checklist, focus on line 9 every way out and add-line Esc after refused mouse Add.
- coder (fix pass 1) outcome: ok (58597a2)
- qa (re-run 1) outcome: ok -- PASS, 0 defects
- qa round 1 outcome: ok (found 2 defects)

# MODEL-LOG -- B1f (agentic)
- designer: not launched -- no new screen.
- planner: not launched -- bounded defect; root-cause debugging sits with the coder.
- coder: opus -- focus/timing class, lower-level and scripted-QA misses in B1b-B1d; root cause must be found, not guarded.
- coder outcome: ok (root cause: unclaimed pointer release on SectionScroll refocuses first child; fix = claim blank releases on SectionContent; precheck green, 362 tests)
- qa: opus -- previous sonnet QA passed a focus-jump the user then found; real-input timing reproduction (held button) and 20+ trials per case need judgement.
- qa outcome: ok (round 1 FAIL, found D1: rightmost 4 px padding strip of SectionScroll still jumps; root cause confirmed by A/B)
- coder (fix pass 1): sonnet -- cause and fix location are named by QA (scroller padding outside SectionContent); bounded XAML change.
- coder (fix pass 1) outcome: ok (commit below; precheck green 362/362; self-check not run)
- qa (re-run 1): sonnet -- same checklist, known edge-strip probe exists; user decision 2026-10-26: ~5 real-mouse trials per case, more only for the held-button blank-click/edge case.
- qa (re-run 1, sonnet): not run -- stopped before start; superseded by resume below.
## B1f resume 2026-10-26
- coder (docs + precheck): haiku -- mechanical: rule text given verbatim, status-line edits, run known build/test commands.
- coder (docs + precheck) outcome: ok (2105c4f; precheck green 0 warnings, 362/362)
- qa (re-run 1, resume): sonnet -- cases are fixed by the addendum and the edge probe/regression scripts exist; mostly re-running known checks within a 20-min budget. Own git worktree (not tool isolation: it blocked the QA-REPORT.md write last round).
- qa (re-run 1, resume) outcome: TIME-LIMIT at 20 min -- cases a-e PASS (0 jumps in 234 edge trials), case f regression not run; returned to PM
- qa (case f only, regression re-run): haiku -- mechanical re-run of a known script (b1f_regress.py A,B,C) + strict build/test, 7-min budget.
- qa (case f only) outcome: TIME-LIMIT at 9 min (limit 7) -- build/test green 362/362, regression 114 PASS 0 FAIL through B1d-9; rest of B1d and part C not run; returned to PM
- qa (case f remainder: B1d-9 onward + part C): haiku -- same known script, mechanical; prior haiku run results were sound, only the budget was short.
- qa (case f remainder) outcome: BUDGET-REQUEST 14 min at start (nothing run); relayed to PM
- qa (case f remainder, 14 min granted): haiku -- same known script; its budget estimate was sound.
- qa (case f remainder, 14 min) outcome: TIME-LIMIT at 14 min -- part B rest 24/24 PASS; part C 72 PASS / 7 FAIL (Previous salary box keeps focus after Esc / refused Enter), 65 checks not run; returned to PM
## B1g money-box keys 2026-10-27
- planner/designer: not launched -- brief is a bounded fix list (REQUEST 1-4 + ACCEPTANCE).
- coder: opus -- key/focus class, repeated misses in B1b-B1f (LESSONS); must cover both box kinds and all exits.
- coder outcome: ok (precheck green 0 warnings, 362/362; MoneyBox.cs, MoneyFormat.cs, SettingsPage.xaml.cs; not committed)
- qa: opus planned (key/focus class, sonnet missed focus defects before) -- not launched: brief has no `QA budget:`; BUDGET-REQUEST relayed to PM.
- qa: opus -- key/focus class; scope picked from diff. Budget 20 min granted by PM (acceptance 1-7 in salary box + one tag box, every exit, short Add-line/B1d-Enter regression; no full A/B/C matrix).
- qa outcome: ok (PASS WITH CAVEATS, 44 checks, 0 defects, ~8 of 20 min; full A/B/C matrix scoped out)
2026-10-27 B1e lead: sonnet -- explicit spec, mechanical removal; outcome: pending
## B1e reports duplicate cards 2026-10-27
- planner/designer: not launched -- bounded removal, no new screen; BRIEF + ACCEPTANCE are implementation-ready.
- coder: sonnet -- explicit removal spec, no timing/focus class; keep calculations, remove dead card code only.
- coder outcome: ok (precheck green 0 warnings, 362/362; ReportsPage.xaml.cs, ReportsPage.Income.cs; docs)
- qa: sonnet -- visual/UIA read of 4 periods + build/tests, no focus class; scope fixed from diff, 15 min budget.
- qa outcome: ok (PASS, 0 defects, ~8 of 15 min; values checked on empty DB only)
2026-10-27 B1i lead: sonnet -- explicit spec, regular side showed no blocker; outcome: ok
## B1i reports income columns + A2 pie legend 2026-10-27
- designer: not launched -- columns appended to existing tables, legend/colour change is bounded; planner specs it.
- planner: opus -- business-rule caution (per-day income must sum to period figure incl. today/credit/paid), A2 wording ambiguous (what "legend chevrons" means).
- planner outcome: ok (SPEC.md; per-day income feasible, no stop)
- coder: sonnet -- SPEC is explicit (files, rounding helper, colour index fix); not a timing/focus class. Gets lead rulings on SPEC open questions + paid-column light-green addendum.
- planner outcome: ok; coder outcome: ok (precheck green 0 warnings, 362/362; paid-column light green addendum included). QA deferred by user ruling.
2026-10-27 B1i Day total-row removal agentic lead: sonnet -- one-rule revert; outcome: ok (coder sonnet; precheck green 0 warnings, 362/362; QA deferred by user ruling)
