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
2026-10-27 B1e lead: sonnet -- explicit spec, mechanical removal; outcome: ok (coder sonnet; precheck green 0 warnings, 362/362; commit 054a660; QA deferred by user ruling)
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
2026-10-27 G7 lead: sonnet -- lead only routes; members chosen by lead (security class -> opus advised); outcome: ok (coder sonnet; precheck green 0 warnings, 362/362; commit 054a660; QA deferred by user ruling)
2026-10-27 G7 planner: opus -- security class, real secrets; outcome: ok (SPEC.md, guard in CredentialStore, 7 files)
2026-10-27 G7 coder: sonnet -- SPEC explicit (files, allow-list, messages), bounded 7-file edit; rulings: autostart out of scope, TickTick status text unchanged; outcome: ok (coder sonnet; precheck green 0 warnings, 362/362; commit 054a660; QA deferred by user ruling)
2026-10-27 G2 lead: sonnet -- routes only; outcome: ok (planner opus, coder sonnet; precheck green 0 warnings, 362/362; commit cac0f66; QA deferred by user ruling)
2026-10-27 G2 designer: not launched -- chooser + paging buttons reuse existing Diary controls; planner specs wording/placement.
2026-10-27 G2 planner: opus -- item 4 is a root-cause investigation (cross-cutting, capture path) + ambiguous "-" semantics per field; outcome: ok (SPEC.md + item 4 finding)
2026-10-27 G2 coder: sonnet -- SPEC explicit (2 files, pure logic in DiaryFilter.cs), no focus/timing class; lead rulings: keep 300-row cap (lifting widens Select all = marking), category "-" strictly empty, Clear filters resets chooser; outcome: ok (precheck green 0 warnings, 362/362; commit cac0f66)
2026-10-27 G4 lead: sonnet -- routes; A3 may stop for a question; outcome: ok (A3 skipped, question to PM; planner reran once after silent exit)
2026-10-27 G4 designer: not launched -- A4/A5 are size tweaks to existing dialogs; A3 skipped (no derivable target look, question to PM)
2026-10-27 G4 planner: sonnet -- bounded: one constant + two dialog sizes, explicit brief; outcome: ok (first run ended without handback, rerun delivered SPEC.md)
2026-10-27 G4 coder: sonnet -- SPEC explicit (cap 16000 + timeout 360 s, notice font sizes, dialog height clamp); layout sizes are estimates, no QA this batch; outcome: ok (precheck green 0 warnings, 362/362; commit 065fed7; QA deferred by user ruling)
2026-10-27 G4 round 2 planner: sonnet -- A3 from screenshot (bounded look change on one page), timeout/cancel verify; outcome: ok (SPEC-2.md)
2026-10-27 G4 round 2 coder: sonnet -- SPEC-2 explicit (button row to DropDownButton, one constant, tooltip); UIA names kept; outcome: ok (precheck green 0 warnings, 362/362; commit d6e53c7; QA deferred)
- 2026-10-27 G5 memory | PM chose opus for lead (cause unknown; regular STATUS: do not share) | outcome: ok (planner opus run 2 after a stall, coder opus; precheck green 0 warnings, 362/362; commit 603dcb4; no QA by brief)
## G5 memory 2026-10-27
- designer: not launched -- no new screen, nothing visible may change.
- planner: opus -- cause unknown, needs measurement + cross-cutting root-cause (diary list virtualization, Reports caches, live refresh); costly if wrong.
- planner run 1 outcome: stalled (interrupted, no SPEC.md, no files); relaunched unchanged on opus with background-measuring instruction.
- planner run 2 outcome: ok (SPEC.md + qa/g5_measure.py; 5 ranked causes; ~32 min)
- coder: opus -- ItemsRepeater virtualization + split render + hidden-page release: layout/timing class, cross-cutting on Reports (LESSONS: layout is opus); lead rulings: measure at this side's default page (40), no forced GC, fix 5 lazy flyouts kept (repairs empty-flyout defect; flagged to PM).
- coder outcome: ok (fixes 1-5; precheck green 0 warnings, 362/362; All time 128 -> 87 MB private WS, 16 switches 1428 -> 80 MB, hidden page 933 -> 101 MB)
- round 2 coder (24 MB private-bytes creep): opus -- root-cause hunt, unknown cause, timing/lifetime class; planner skipped (bounded, same files/script); fix only if clear and low-risk.
- round 2 coder outcome: ok (not a leak: GC sawtooth from All-time refresh + native rise from switching that levels off; 4 x 15-min scenario runs, qa/g5_creep.py; no code change; SPEC section 10)
## G3 bulk marking + safe door 2026-10-29
- designer: sonnet -- bounded UI additions (button count, confirm dialog, message with Undo, Settings list) reusing existing look; no timing/focus class.
- planner: opus -- root-cause investigation (disappearing marks, vanishing ticks) + cross-cutting data-change service with backup/undo/edited-since skip; data-integrity, costly if wrong.
- coder: opus -- writes to past data in bulk, backup/undo/keyword-forget logic and tick state across re-render; irreversible on real data if wrong.
- designer outcome: ok (SPEC.md "Screen design"; 4 open questions, lead rulings passed to planner)
- planner outcome: ok (SPEC.md Plan sections 1-8; causes: idle-placeholder cleanup deletes marked rows + 300-row reach; ticks cleared on Mark and kept in a rebuild-local set; 10 open questions, defaults ruled by lead)
- coder outcome: ok (commit 9b8e864; precheck green 0 warnings, 362/362; no MANUAL.md on this side)
- qa: opus -- new data-writing feature, scope/trial choice is judgement (2 400-row scratch seed, dialog counts, restart persistence, backups x5, undo skip, history 20/30 days, ticks). Not started: no QA budget in the brief; BUDGET-REQUEST sent to PM (proposed 45 min).
## G3 round 2 (Addendum 2) 2026-10-29
- designer/planner: not launched -- Addendum 2 is a bounded change list on an existing service and SPEC.
- coder: sonnet -- explicit list of 5 answers on top of 9b8e864 (tag marks through the same door, Settings Undo confirm, Show all lifts the cap, no keyword teaching without ticks, Settings text); no focus/timing class; data door already built and specified.
- qa: not started -- user deferred QA to the end of the group.
- coder outcome: ok (commit 67fc0f8; precheck green 0 warnings, 362/362; answers 1-5 built, docs recorded; tag row put in a horizontal scroller, untested visually)
- 2026-10-29 G3 round 3 agentic (idle rows skipped when teaching keywords) | PM chose sonnet (small bounded change, explicit spec) | outcome: ok (precheck green 0 warnings, 362/362)
- coder: sonnet -- G3 round 3 (Addendum 3 item 1): skip idle rows when a category mark teaches keywords; one bounded service change, explicit spec.
- coder outcome: ok (G3 round 3; precheck green 0 warnings, 362/362; one flaky AiClientTests log test on an earlier run, passed on rerun)
- 2026-10-29 G10 agentic | PM chose sonnet for lead (same brief as regular, which sonnet built green); lead picks members | outcome: ok
- 2026-10-29 G10 coder | all three parts, no planner/designer | sonnet | bounded, explicit spec, but Ask-format change has some judgement; guard pattern known from G7 | outcome: ok (green precheck, commit dd82ad4)
- 2026-10-29 G10b agentic | PM chose sonnet for lead (small bounded change, explicit spec) | outcome: ok
- 2026-10-29 G10b coder | remove Diary 300 cap, paging to Show all, Select all covers all; no planner/designer (spec already bounded) | sonnet | bounded change, explicit acceptance; perf judgement | outcome: ok (green precheck, commit 1a71023)
- 2026-10-29 G8 agentic | PM chose opus for lead (focus/timing class: B1f needed 2 fixes and a missed edge strip per LESSONS) | outcome: ok (built, QA deferred by brief)
- 2026-10-29 G8 planner | find B1f fix, inventory scrolling panes with text boxes, specify per-page protection | opus | focus/timing class, cross-cutting, no clear spec; B1f needed 2 fixes (missed edge strip) | outcome: ok (SPEC.md, 6 open questions with defaults)
- 2026-10-29 G8 coder | implement SPEC.md, precheck, own docs, local commit | opus | focus/timing class across many pages; a lower model's miss is costly (B1c/B1f history) | outcome: ok (green precheck 0 warnings, 362/362, commit 1d2dae8; QA deferred)
- 2026-10-29 G6 agentic | PM chose sonnet for lead (small bounded change; same brief sonnet built green on regular) | outcome: ok (built, QA deferred by brief)
- 2026-10-29 G6 coder | find dot code, make it follow the overdue-banner condition, sibling check, precheck, own docs, local commit; no planner/designer (brief+acceptance already bounded) | sonnet | small bounded change, explicit spec; refresh-without-restart has mild judgement | outcome: ok (green precheck 0 warnings, 362/362, commit e393e3d)
- 2026-10-29 G9a agentic | PM chose sonnet for lead (same brief sonnet built green on regular) | outcome: ok (built, QA deferred by brief)
- 2026-10-29 G9a coder | find AI call sites + cost/token counting, add monthly cap (default EUR 5, Settings row, refuse before call), Telegram chat name from title, sibling check, precheck, own docs, local commit; no planner/designer (brief+acceptance bounded; Settings row follows existing pattern) | sonnet | bounded, explicit acceptance; cost estimate needs mild judgement, single AiClient funnel found | outcome: ok (green precheck 0 warnings, 362/362, commit 262451e)
- 2026-10-29 group QA agentic | PM chose sonnet for lead (precheck, start QA, pass report up; no build); QA opus per user ruling | outcome: ok (precheck green 362/362 after rebuilding stale test DLL; QA opus ran ~19 of 35 min, verdict FAIL, 4 defects, lines NOT RUN listed in report)
- 2026-10-29 group QA agentic QA launch | full-group QA, 11 features + qa-extra, 35 min cap | opus | user ruling; timing/focus/UI judgement and scope choice | outcome: ok (report written, FAIL D1-D4)
- 2026-10-30 G11 agentic (group-QA fixes) | PM chose sonnet for lead (lead picks member models; coder expected opus for the in-app serializer/secret-store class per LESSONS) | outcome: ok (built, QA deferred by brief)
- 2026-10-30 G11 coder | all 7 items, no planner/designer (bounded list + QA evidence) | opus | items 1 and 7 are in-app serializer / secret-store class never exercised on this side; wrong fix costly (QA blocked again) | outcome: ok (green precheck 0 warnings, 362/362, commit bf64e55; QA deferred)
- 2026-10-30 G12 agentic lead (Esc rule, salary ceiling, cent match) | PM chose sonnet for lead; lead picks member models (focus/key-event class -> raise difficulty) | outcome: ok (built, precheck green, QA PASS WITH CAVEATS)
- 2026-10-30 G12 coder | Esc rule on all Settings boxes + blank-click park + Model save + salary ceiling + Week/Month cent match; no planner/designer (brief+addenda+acceptance bounded) | opus | focus/key-event behaviour is a raising signal (B1d/B1f/G8 history), several exit-path rows; wrong fix costs a QA round | outcome: ok (green precheck 0 warnings, 362/362)
- 2026-10-30 G12 QA | ACCEPTANCE 1-15 + standing rules, 30 min cap | opus | PM/user ruling; focus/key-event judgement | outcome: ok (PASS WITH CAVEATS, 0 defects, 15 of 30 min; 1 medium observation for PM ruling)
- 2026-10-30 G13 coder | Enter in Model/cap saves only its box; blank click/Esc discards unsaved key; sibling check; precheck; own docs; commit; no planner/designer (bounded) | opus | focus/key-event class + secret store, wrong fix costs a QA round
- 2026-10-30 G13 QA | ACCEPTANCE 1-7 + one-trial sibling check on other AI Enter paths, 25 min cap | opus | focus/key-event + secret store judgement; PM brief | outcome: ok (PASS, 0 defects, ~16 of 25 min)
