# QA-PROGRESS -- agentic group QA 2026-10-29 (HEAD 262451e, worktree .claude/worktrees/qa-b1h)

Start 23:15, cap 35 min (end 23:50). Plan written before any test. One trial per distinct action.

| Feature | Lines planned | Action / trials | Min |
|---|---|---|---|
| build+suite | g*-build lines | strict Debug + dotnet test, background, once | 0 (parallel) |
| seed | all | one scratch root: ~930 diary rows (15% dashless titles, Telegram titles with dash in chat name), 120-day plan started 60 days ago (overdue tasks), score rows | 3 |
| g1 Reports | 1,2,3,6 | one launch, UIA dump Day/Week/Month/Year, sum rows vs Total from dump, 1 trial (static render) | 4 |
| g2+g10+g10b Diary | g2 1,3; g10 1,2; g10b 1,2,4 | same launch: App filter "-" and "Other", Show all count, Select all count, time Show all; 1 trial each | 6 |
| g6 Today dot | 1 | same launch: dot visible on sidebar with overdue banner; 1 trial | 1 |
| g9a Settings cap | 1 (UI part) | same launch: cap box shows 5 + spend; type -5 / abc -> message; 1 trial each | 3 |
| g7/g10 code lines | g7 1,3,4; g10 5,6,7; g9a 3,5 | code evidence (grep guards) | 4 |
| security | diff of g1..g10b | hand review of the diff touched areas | 3 |
| report | | write QA-REPORT.md | 5 |
| total | | | 29 + 6 slack |

Planned NOT RUN (cap): g3 (bulk mark/backup/undo, ~10 min), g4 (stub 34-task plan + dialogs, ~8 min), g5 (memory, 15+ min per scenario),
g8 (blank-click matrix across pages, ~8 min), g9a 2 (cap refusal with stub), g10 3-4 (compact Ask with stub), g2 2,4, g6 2-3, g10b 3,
qa-extra lines. Order: highest-value UI first; any freed time goes to g9a 2 / g10 4 (stub) then g8.

## Progress
- 23:19 build+suite: strict Debug 0 warn/0 err, 362/362; 1 run. PASS.
- 23:19 g1 1,2,3,6: 1 launch, UIA dump 4 periods (static render, 1 trial). Week/Month/Year sums = Total to the cent; Day has one row and no Total row; no Score/income cards. Screenshots black (PrintWindow) -> line 5 colour not seen. 4 min of 35.
- 23:19 g6 1: overdue banner "10 overdue tasks" shown; InfoBadge not exposed in UIA, screenshot black -> not seen yet. 1 trial.
- 23:24 g10b 1,2,4 + g2 1 (App "-") + g10 1: All time 931 rows, Show all(891) -> all, 221 ms; Select all = 931 ticked; "- (no app)" = 27 = seeded empty titles; "Other" = 146 = 112 dashless + 33 "– Telegram" (no chat name) + 1 idle -> D1; Telegram 63. 1 trial each (static). 9 min used.
- 23:27 g9a 1: cap shows 5; -5/abc/empty refused with message, config untouched; 7 saved, 7 after restart. 1 trial each. PASS.
- 23:27 g3 1,2,4 + g6 1 + g1 5,7: Other filter 146 -> Mark all 146 neutral: dialog 0 filled + 121 overwritten = 121, DB +121 neutral, backup file made; dot seen on Reports; colours ok; "Of which paid work" header clipped at default width (D3); duplicate "Mark all Studioshoo" button (D2). 12 min used.
- 23:27 g3 5 + qa-extra tag: Settings Undo restored DB exactly (121 rows); tag filter lists Studioshoo twice (FAIL). g8 2,3 on Settings AI: blank clicks in page/right strip leave box; Tab order normal; qa-extra cap message stays "cannot be negative" while box shows 6 (FAIL). 1 trial each. 13 min used.
- 23:29 g9a 2: Test connection disabled (no key in scratch; saving one writes the test credential to the real Credential Manager) -> NOT RUN; same blocks g4 1-2, g10 3-4, qa-extra 6. Stub got 0 requests. 14 min used.
- 23:30 g2 2: search chooser All fields/App/Page/Description/Category/Tag; "Quarterly" 31 in All fields and App, 0 in Page/Description. 1 trial. PASS. 15 min.
- 23:31 g6 3: "Replan all overdue" -> banner gone, sidebar dot gone without restart (pixel check 4 -> 0). 1 trial. PASS. 16 min.
- 23:31 code evidence g7/g10/g9a + security diff scan done; writing report. 17 min used.
- 23:33 report written to agentic/QA-REPORT.md; FAIL, 4 defects; about 19 of 35 min used.

## G12 QA (agentic, b669eb8) -- start 12:38, cap 30 min
Plan (one trial per distinct action; timing-dependent focus lines get 1 trial each with real input, no repeats unless a result is ambiguous):
- L9 strict build + suite: done in worktree qa-ag (1 min).
- Script A, one launch on scratch root with salary 2925 + tag Studioshoo 1600 (approx 6 min incl. 1 min launch): AI section: L1 (cap Esc), L3 (-5 Enter Esc), L4 (cap type, blank click, saved), L11 (6, blank, then 9), L13a (6, blank, Shift+Tab -> Model), L2 Model Esc, L2 key Esc, L10 Model blank click saved (+reopen Settings), sibling sweep: every other Edit in AI/other sections Esc + blank click. Income section: L5 tag box Esc revert, L6 salary 999999999999 Tab, L7 2925->3000 then back, L12 salary Esc Shift+Tab, L13b tag box blank click Tab -> Remove, L14 new tag name Esc, L15 NumberBox spin click.
- Script B L8: seed diary paid rows Mon-Wed this week (37,37,41 min at 1600/mo), Reports Week/Month/Year, read paid figures (approx 4 min).
- Security diff scan by grep (2 min). Report (4 min). Total approx 18 min + 6 min slack for fixing script errors.
- 12:45 Script A (1 launch, 1 trial per action, real input): L1 L2 L3 L4 L5 L6 L7 L10 L11 L13 L14 PASS; siblings (Your name, 3 time boxes, 5 hour NumberBoxes, keyword/idle boxes, 2 scoring boxes) Esc revert+park and blank park PASS; L12 Shift+Tab after Esc went to nav "Settings" item (needs natural-order check); L15 spin button not found in UIA; "Key saved - <model>" status appeared with no key saved (probe). 7 of 30 min.
- 12:49 L8 (seeded 37/37/41 min paid, 1 trial): Week total 18,25 = Month row 18,25 = Year Oct 18,25 PASS; L15 spin click up 15->16 saved, down ->15 PASS (1 trial); probes: L12 natural Shift+Tab from salary also goes to nav Settings item -> consistent PASS; Enter in cap/model box saves an abandoned key from the key box (observation). 12 of 30 min.
- 12:50 money-box regression (salary -5/empty/abc/spaces/0, B1h Esc after new save; 1 trial each): all as before (0 saves; allowed per DECISIONS). Security diff scan: none. Writing report. 13 of 30 min.
- 12:53 report written (PASS WITH CAVEATS, 0 defects, 1 medium observation). 15 of 30 min.

## G13 key-only-on-save (agentic, commit d1fd45c) - QA plan, start 13:11, limit 25 min
- Build: worktree qa-ag -> d1fd45c, strict build + dotnet test (L7), measured ~2-3 min in earlier rounds; runs in background.
- One script, one launch, scratch MENTOR_ROOT (empty, in-memory key store by IsScratchRun), real clicks/keys, foreground check before each input. One trial per distinct action (no timing dependence claimed by the rule; the park path is focus-event driven, so L1 blank-click path gets 2 trials: L1 and L3b both exercise it).
  Order: L4 (key, Esc) -> L1 (key, blank, Model Enter) -> L2 (key, blank, cap Enter) -> L3b (key, blank, Save) -> L5 + qa-extra G13 (key, Tab, model, Enter; then click cap box, cap Enter; read key-box state) -> sibling: key then click on Model box (other box) -> L3c (Enter in key box stores) -> L3a (key + Save stores). After each: summary text (SumAI), status line, Test/Remove enabled state, key box length, config ai fields, focus. ~3 min run.
- L6: cmdkey /list full text before (taken 13:15, 28 lines) and after, diff. ~0.5 min.
- Security: diff scan of d1fd45c (secret never logged/written to config). ~1 min.
- Script writing ~5 min, report ~4 min. Total ~16 min + slack.
- 13:13 L7 strict build 0 warnings/0 errors, dotnet test 362/362 (worktree qa-ag at d1fd45c, 1 trial: deterministic). PASS. ~3 of 25 min.
- 13:14 UI script g13_run.py (1 launch, 1 trial per action; blank-click discard exercised 3x in L1/L2/L3b): L4, L1, L2, L3b, L3c, L5 (+qa-extra G13 Tab path), sibling (key then click Model box, Esc) PASS; L3a passed but ran after L3c so 'Key saved' was already shown -> 1 extra fresh-launch trial for L3a. ~9 of 25 min.
- 13:15 L3a fresh launch (1 trial): 'No key' -> key + Save -> 'Key saved for this test run only', summary 'Key saved', box empty PASS. L6 cmdkey /list before/after 28 lines, byte-identical PASS. Secret scan of scratch root: no typed key on disk. Writing report. ~14 of 25 min.
- 13:15 Report written: PASS, 0 defects. ~16 of 25 min.
