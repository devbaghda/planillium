2026-10-27 B1h generalist: opus -- key/focus-event class (LESSONS: opus passes in 1, sonnet needed 5 rounds); outcome: ok (QA PASS round 1)
2026-10-27 B1h generalist model check: opus confirmed sufficient (key/focus class, small scope); outcome: ok
2026-10-27 B1h QA: opus -- real-click/key focus checks (LESSONS: QA for real-click focus work = opus); outcome: ok (PASS, 7 of 15 min)
2026-10-27 B1e generalist: sonnet -- explicit spec, mechanical removal of two cards, no key/focus work; outcome: ok
2026-10-27 B1e QA: sonnet -- UIA/screenshot reads of four Reports periods, no focus/key work; outcome: ok (PASS, 5 of 15 min)
2026-10-27 B1i generalist: sonnet -- explicit spec, bounded table+card change; stop-and-report on per-day rule; outcome: ok (built, precheck green 229/229; QA deferred by user ruling)
2026-10-27 B1i colour tweak (paid column light green) regular: pending after agentic finishes
2026-10-27 B1i colour tweak regular: sonnet -- one colour, needs theme-aware choice; outcome: ok (built, precheck green 229/229; QA deferred by user ruling)
2026-10-27 G7 generalist: opus -- security class (real secrets), cross-cutting guard; outcome: ok (built, precheck green 229/229; QA deferred by user ruling)
2026-10-27 G7 generalist model check: opus confirmed sufficient (security class, cross-cutting guard over secret reads); outcome: ok
2026-10-27 G2 generalist: sonnet -- explicit spec, bounded UI work; #8 investigate-only; outcome: ok (built, precheck green 229/229; QA deferred by user ruling)
2026-10-27 G4 generalist: opus -- timing/streaming measurement + counter in async UI (LESSONS: timing class raises difficulty); outcome: ok (built, precheck green 229/229; QA deferred by user ruling)
2026-10-27 G4 generalist model check: opus confirmed sufficient (async timing + streaming measurement); outcome: ok
- 2026-10-27 G4b ask-wait-cancel | PM chose sonnet (small bounded change) | outcome: ok (built, precheck green 229/229; QA deferred)
- 2026-10-27 G5 memory | PM chose opus (cause unknown, needs investigation) | outcome: ok
2026-10-27 G5 generalist model check: opus confirmed sufficient (unknown cause, memory investigation + measurement); outcome: ok
- 2026-10-28 G3 bulk mark + safe door | PM chose opus (new data-change service, touches real-data class, cause of vanishing marks unknown) | outcome: ok (built, precheck green 229/229; QA not run per brief)
2026-10-28 G3 generalist model check: opus confirmed sufficient (new data-change service with backup/undo, unknown cause of vanishing marks); outcome: ok
- 2026-10-29 G3 round 2 regular | PM chose opus (Undo-forgets-keywords, tick-survival cause unknown, touches safe-door data service) | outcome: ok (built, precheck green; QA deferred)
2026-10-29 G3 round 2 generalist model check: opus confirmed sufficient (tick-loss cause unknown, keyword-undo inside safe-door data service); outcome: ok (precheck green first run; QA deferred)
- 2026-10-29 G3 round 3 regular (idle rows skipped when teaching keywords) | PM chose sonnet (small bounded change, explicit spec) | outcome: ok (precheck green 229/229; QA deferred)
- 2026-10-29 G10 regular | PM chose sonnet (three bounded changes, explicit spec; guard modelled on G7) | outcome: ok (built, precheck green 229/229; QA deferred)
- 2026-10-29 G6 regular | PM chose sonnet (small bounded change, explicit rule) | outcome: ok (built, precheck green 229/229; QA deferred)
- 2026-10-29 G9a regular | PM chose sonnet (bounded settings + limit; money-limit logic watched in QA) | outcome: ok (built, precheck green 229/229; QA deferred)
- 2026-10-29 group QA regular | PM chose sonnet for generalist (run precheck, start QA, pass report up; no build); QA opus per user ruling | outcome: ok (precheck green after 1 flaky rerun; QA opus FAIL, 3 defects, ~23 of 35 min; g5 NOT RUN)
- 2026-10-29 group QA launch opus (g1-g10, UIA, 35 min cap) | outcome: ok (report written; verdict FAIL, D1 high)
- 2026-10-30 G11 regular (group-QA fixes) | PM chose opus (serializer failure only visible in-app; secret-store change; earlier sonnet build of this class failed) | outcome: ok (built, precheck green 229/229 first run; QA deferred)
2026-10-30 G11 generalist model check: opus confirmed sufficient (in-app-only serializer failure, secret-store change, rounding rule); outcome: ok
- 2026-10-30 re-QA regular | PM chose sonnet for generalist (precheck, start QA, pass up; no build); QA opus per user ruling | outcome: ok (QA opus FAIL, 2 medium defects D1 AI-box Esc/blank click, D2 salary no max; ~20 of 35 min)
- 2026-10-30 G12 regular (Esc rule, salary ceiling, cent match) | PM chose opus (focus/key-event behaviour, LESSONS class; rounding across three views) | outcome: QA round 1 FAIL (1 medium, D1 blank click -> key box), fixed 30b0e2b; QA round 2 FAIL (lines 1-12 PASS; 1 medium, Tab/Shift+Tab after blank-click park), fixed cb7ba8d (round 3, QA deferred to the PM's batch re-check)
2026-10-30 G12 generalist model check: opus confirmed sufficient (focus/key-event across all Settings boxes, money ceiling, cent rounding across views); round 1 QA opus FAIL D1
- 2026-10-30 G12 regular round 2: same generalist continued (Model box save-on-leave + targeted re-QA of D1) | opus | outcome: ok for the build (lines 1-12 PASS); QA opus FAIL on 1 medium outside the checklist (blank-click park loses the left box for Tab/Shift+Tab); first QA run ended without a report, re-run delivered
- 2026-10-30 G13 regular (key only on save) | PM chose sonnet (one-line fix, defect mostly absent); QA opus per brief (focus/click work) | outcome: ok (precheck green 229/229; QA opus PASS WITH CAVEATS, 0 defects, ~7 of 25 min)
- 2026-10-30 G12 round 3 re-check regular | sonnet generalist (verification only, no build); QA opus per user ruling | outcome: ok (QA opus PASS, 0 defects, ~11 of 25 min)
- 2026-10-30 G14 regular (user rulings: missing data folder, wrapped report headers, Enter leaves AI boxes, too-large message clears/live) | generalist model check: opus confirmed sufficient (focus/key-event class + data-safety guard) | outcome: ok (4429235, precheck green 229/229, QA PASS WITH CAVEATS 0 defects)
- 2026-10-30 G14 QA regular: opus per PM order (real-click focus/key work, data-safety line; LESSONS: focus QA = opus) | outcome: ok (first launch ended at a usage stop with no report; re-run PASS WITH CAVEATS, 0 defects, 15 of 30 min)
