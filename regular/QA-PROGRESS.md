# QA progress -- B1h esc-saved-boxes (regular), build 0ea247e, limit 15 min, start 11:14

## Plan
- Setup: worktree qa-b1h + strict Debug build (~2 min measured on B1d) + tests --no-build (~0.5 min). 3 min.
- C1 (lines 1-4, salary + Studioshoo): one script, one launch; per box 4 inputs (2500, empty, "abc"/spaces, -5) -> Esc, read box/cfg/focus/red, type 9. 1 trial each (Esc is a key handler, not timing-dependent). ~2 min run.
- C2 (line 5 regression, from diff: Cancelled wiring + OnKeyDown): same launch; Enter-save+park on both boxes, new-tag line Esc clears+leaves, Tab out of salary box, blank click out of tag box (edited). 1 trial each. ~1.5 min.
- C3 (line 6): build/tests result + B1d lines touched by OnKeyDown (Enter path covered in C2; minus message stays after Tab covered in C2). 0.5 min.
- Report: 3 min. Total ~10-11 min, fits 15.

## Progress
- 11:16 Setup: worktree qa-b1h at 0ea247e, strict Debug build 0 err (1m17s), tests 229/229. 2.5 min of 15.
- 11:17 C1 lines 1-4: 10 Esc checks (salary + Studioshoo x 2500/empty/abc/spaces/-5), 1 trial each (key handler, no timing), all revert to saved amount, cfg unchanged, focus IncomeFocusPark, type-9 unchanged, minus message gone. PASS. 3.5 min.
- 11:18 C2 line 5: 1 trial each (Enter-save+park x2, Esc after a save, new-tag Esc, Tab out, blank click out, B1d empty-Tab revert and -5 Tab). First run died on a cp1252 print, reran regression part only with UTF-8 output. PASS. 4.5 min.
- 11:20 C3 line 6: build/tests clean; B1d revert/minus paths re-seen in C2. PASS. 6 min. Side note: verify instance reads the real AI key from Credential Manager (observation O1).

# QA progress -- group QA 2026-10-29 (regular), build 262451e, limit 35 min, start 22:51

## Plan (minutes per feature; one trial per distinct action, no timing-dependent cases except the Ask wait = 1 trial each)
- Setup 6: worktree qa-b1h detached at 262451e, strict build (~1.5 measured B1h) + test build/run (~1), seed scratch root (first launch for schema, kill, sqlite seed: 2400 diary rows incl. dashless titles + Telegram "Chat - Telegram" titles, an overdue task), stub HttpListener.
- g1 b1i Reports columns 3: one launch, Reports 4 periods, read table headers/rows/Total via UIA, sum check; 1 narrow-window screenshot. Line 4 (pre-change equality) NOT RUN unless time (needs old build).
- g2 Diary browsing 3: "-" entry on each filter list, field chooser, paging counts. 1 trial each.
- g10 App filter + Ask 3: "Other" entry count = dashless rows; code evidence lines 5-7 by grep (1 min).
- g9a cap + Telegram 3: Settings cap box (default 5, invalid inputs, save), chat-name column on Telegram rows; month boundary = unit test evidence; sibling AI calls grep.
- g3 bulk mark 4: filter > 300 rows, button count, confirm numbers, Cancel, Mark, re-open; backup file on disk; Undo.
- g6 Today dot 2: dot visible on another page with seeded overdue; reschedule/complete removes it.
- g4/g4b Ask wait/cancel 3: stub with delay; counter visible, Cancel stops within ~1 s; 4-minute wording grep.
- g7 scratch safety 1: code evidence + Settings AI/TickTick rows under scratch.
- g5 memory 0: measurement line is the builder's; NOT RUN (no time for 9000-row memory runs) unless time left.
- Report 4. Total 35. Likely overflow: g3/g4 parts -> NOT RUN if the clock says so.

## Progress
- 22:55 Setup+g1: build 0 err (34 s), tests 229/229, seeded 2400 rows. g1 lines 1,2,3,5,6 read on 4 periods (1 trial each, static view): line 3 MISMATCH Week (-326,64 vs -326,63; 50,80 vs 50,79) and Month (-2368,03 vs -2368,04); Day has no Total row. ~13 min of 35.
- 23:01 g1 line 7 narrow: clipped then shift-wheel scrolls to both columns, PASS (1 trial). g2 lines 1-4 + g10 lines 1-2: dash entries, Other=193, sums 2401, field search x7, paging +50/+1000/show all via Invoke, return to page; 1 trial each (no timing). PASS. Tag list shows Studioshoo twice (obs). ~10 min of 35 by clock.
- 23:03 g9a line 1: cap box default 5, 6 invalid inputs + 4 leave paths, valid 3 saved (1 trial each). Refusals OK; '-5' says 'must be a number'; stale message after leave paths (obs). ~14 of 35.
- 23:04 g9a 1-2 + g7 2: restart keeps cap 3; at cap Test connection refused with message, stub 0 requests; cap 10 -> 1 request, Connected. 1 trial each. PASS. ~14 of 35.
- 23:06 g6 lines 1-3: seeded overdue -> dot on Today+Reports (screenshot), completing it removes banner+dot live, Plans no dot. 1 trial (UI event, not timing). New-overdue-without-restart NOT RUN. ~18 of 35.
- 23:09 g4/g4b/g10 3-4: counter 'Waiting 0:01..0:05', stops 'Waited 0:06', 4-minute wording, Cancel stops in <0.6 s, dialog usable, plan file untouched (1 trial each; Cancel timing 1 trial since stop was immediate). Malformed reply: clean message. VALID compact reply -> 'Something went wrong' + InvalidOperationException in CompactReply.Expand (2 trials, 2 different replies) = DEFECT. ~18 of 35.
- 23:12 Report written to regular/QA-REPORT.md. VERDICT FAIL defectsQa=3 (D1 compact Ask reply exception, D2 Reports cent rounding). Total ~23 of 35 min.

## Re-QA after G11 -- 2026-10-30 (regular, HEAD bf64e55, cap 35 min, start 10:36)
Plan (one trial per distinct action; no timing-dependent case, so no repeats):
- Setup: worktree + strict build (~2, measured last round) + first launch, kill, seed diary/ledger, relaunch (~3) = 5
- G11 1, 2, 8, 9 + 0b AI: one session -- cmdkey before; Settings AI shows env key; Test connection; spend line; Ask Replace remaining + Add plan with compact stub reply; log grep; spend line again; error reply -> plan file byte-identical; cmdkey after = 8
- G11 3: Reports Week/Month/Year rows vs Total (4 x -81,66 seeded) = 4
- G11 4, 5: Diary tag filter (paid tag also tag), App filter Telegram counts = 4
- G11 6 + priority 4: Reports headers at 1180x780 via UIA = 2
- G11 7 + qa-extra Esc/click-blank on AI cap and Income boxes = 4
- qa-extra money box 999999999999 / Esc-after-save / scratch credentials row = 4 (B1e kept-strip line: needs pre-change build -> checked only if an old exe exists)
- G11 10: suite run in background during UI work = 0 extra
- Priority 3 (prior NOT RUN lines): only if time remains; otherwise NOT RUN.
Total 35.
- 10:39 setup done (build 0 err 44 s, suite 229/229, seeded 35 diary rows; ledger auto-filled with equal -94,3548 rows). 3 of 35.
- 10:39 G11 8, 9 (Test connection): stub hit with env key, spend €0,00 -> €0,01 live. 1 trial (not timing). PASS. 3 of 35.
- 10:41 G11 7 + qa-extra cap/Income leave paths: -5 Save -> 'more than 0' PASS; cap box Esc and blank-area click keep focus (2 blank spots) = FAIL; Income salary: Esc reverts+parks, blank click saves+parks; 999999999999 saves 999 999 999 with no 'too large' message = FAIL. 1 trial each. 5 of 35 (clock re-read: earlier lines were guessed, now from `date`).
- 10:44 G11 1, 2 + 0b: Replace remaining Ask (notice shown, Accept) -> confirm shows '1 new task(s)', no ERROR; HTTP 500 -> clean message, plan file sha1 identical; slow reply -> 'Waiting 0:0n', inline Cancel -> 'Stopped after 0:09 -- request cancelled', file identical; Add plan Ask -> reply expanded, Import works, no ERROR. Salary Esc-after-Enter-save shows new amount. 1 trial each. PASS. 8 of 35.
- 10:46 G11 3, 4, 5, 6: Week/Month/Year rows sum to Total in both income columns (MATCH x6); Week paid total €99,20 vs Month/Year €99,21 for the same 5 days (obs, new case); tag list Studioshoo once, one Mark button; App filter Telegram 3 incl. '- Telegram' and en-dash rows, Other 2, sum 8 = total; headers clipped at rest ('Of which paic') but table scrolls sideways (seen) -> PASS. 1 trial each. 10 of 35.
- 10:50 prior NOT RUN: g3 4 keep-5 (6 marks -> 5 backups) PASS; g3 5 Undo from Settings restores 8, older change 'restored 0, skipped 8 changed since' PASS; g3 3 restart keeps marks PASS; 0b blocked message (no base URL) 'AI is off in this test copy... Nothing was sent', stub 0 hits; Remove key / Save typed key work; cmdkey before/after identical. g3 9 first try aborted: app lost foreground to the user's mail client, clicks went there (selection only). Foreground guard added. 1 trial each. 15 of 35.
- 10:54 g3 8, 9 (with foreground guard): tick 5 of 8 rows -> 'Mark on-plan (5)', after Mark and after Undo the same 5 ticked; Undo from message bar 'Restored 5 rows... Forgot the 5 keywords', pre-existing 'Word' kept. 1 trial. PASS (filter-stops-matching sub-check NOT RUN). App killed. 18 of 35. Writing report.
- 10:53 Report written to regular/QA-REPORT.md. VERDICT FAIL defectsQa=2 (D1 AI cap box Esc/blank click, D2 salary 999 999 999 saved); G11 10/10 PASS. ~20 of 35 min.

## G12 QA (build 687a151, Debug) - start 11:18, cap 11:53 (35 min)
Plan (1 trial per distinct action; none of these is timing-dependent):
- Setup: strict build + suite in background (~1.5 min measured last run), NEW empty MENTOR_ROOT, first launch, kill, seed diary (paid rows 5 days this week), relaunch, foreground guard = 4 min
- A: ACC 1-4 + qa-extra 21/23 (cap/Model/Key: Esc reverts+leaves; -5 Enter then Esc; blank click saves) one script = 6 min
- B: ACC 5-7 + qa-extra 24 + regression (salary Esc, 999999999999 Tab, 2925 save, Tab/Shift+Tab after park, tag rate too large, New tag Add too large, blank click saves) = 7 min
- C: ACC 8 + qa-extra 25 Week/Month/Year paid cents = 4 min
- D: qa-extra 26 App filter "-" = 3 min
- ACC 9 from background build = 0; cmdkey after + report = 5 min
- Buffer 6 min. Total 35.
- 11:21 setup: strict build 0 warn/0 err (37 s), suite 229/229; new empty MENTOR_ROOT mroot-g12, first launch, kill, seeded 21 diary rows (26-30 Oct, 45 min paid 'Studio'/day), relaunch; welcome + welcome-back + Good-morning dialogs dismissed. 3 of 35.
- 11:23 A (ACC 1-4, qa-extra 21/23): 1 trial each (not timing). Cap Esc reverts 12->8 and parks PASS; -5 Enter msg, Esc -> 8, msg gone PASS; Model Esc reverts PASS; key Esc clears typed key, parks PASS; blank click saves cap BUT focus lands in the API key box (3 spots, 2nd and 3rd added to characterise the defect) -> typed keys go into the key box. FAIL (ACC 4 focus part). 5 of 35.
- 11:25 B (ACC 5-7, qa-extra 24, Income regression): 1 trial each. Salary Esc reverts+parks; 999999999999 Tab -> 'Amount is too large... saved amount was kept', box 2 925, config unchanged; 3100 Tab / 2925 Enter save+format; blank click saves+parks; tag rate 150000 refused; tag Esc reverts; New tag 200000 Add refused; Studio 1000 Enter added. Tab after park -> next box. PASS. 7 of 35.
- 11:26 C (ACC 8, qa-extra 25): 1 trial (deterministic). Paid 5 x 4,46 (=4,4643 each): Week Total 22,30 = Month row 22,30 = Year Oct 22,30; income Week -449,45 = Month row; all views rows sum to Total. PASS. 8 of 35.
- 11:27 D (qa-extra 26): 1 trial. App filter: -, Firefox, Telegram, Zoom, Other; '-' matches 1 row (empty window), 1+1+2+1+1 = 6 = day total. PASS on this data. 9 of 35.
- 11:28 extra regression: Shift+Tab after park from salary stays on the park (obs); cap -5 Tab keeps text + message, nothing saved, Esc reverts. App killed (own PID), cmdkey before = after, log 0 ERROR. 10 of 35. Writing report.

## G13 QA (b669eb8 + uncommitted SettingsPage.xaml.cs fix, Debug) - start 12:59, cap 13:24 (25 min)
Plan (1 trial per distinct action; nothing here is timing-dependent):
- Setup: worktree qa-g13-reg + copy fix, strict build + suite in background (~1 min), new empty MENTOR_ROOT mroot-g13, suffix verify-g13, no scratch key (summary must start "No key"), dismiss first-launch dialogs = 5 min
- A: ACC 1, 2, 5 (Tab and click-other-box), 3 (blank+Save; Enter in key box), 4 (Esc) in one script = 7 min
- B: sibling Enter paths (Model Enter, cap Enter, key-box Enter) folded into A; qa-extra AI 18 (-5 msg), 23 (Esc/blank on cap/Model/key), 27 (blank + Shift+Tab -> Model) = 4 min
- ACC 6 (cmdkey after), ACC 7 (background build) + report = 6 min
- Buffer 3 min. Total 25.
- 13:02 setup: worktree qa-g13-reg (b669eb8 + working-tree SettingsPage.xaml.cs), strict build 0 warn/0 err (38 s), suite 229/229; empty mroot-g13, suffix verify-g13, no scratch key; Welcome + Good morning dialogs dismissed (Good morning came late and blocked the first click: script aborted on its focus assert, no input lost). cmdkey 57 before.
- 13:04 A (ACC 1, 2, 3, 4, 5 + sibling Enter in Model/cap/key box): 1 trial each (not timing). Every abandon path kept "No key"; Model/cap Enter saved only their box; Save probe after each proved the key box empty; Tab/click-other-box keeps the key text unsaved until blank click/Esc; Enter in key box does nothing (focus stays, nothing stored); blank+Save stores nothing (fix), type+Save stores ("Key saved"). PASS (ACC 3 caveat). ~5 of 25 min.
- 13:05 B (qa-extra 18, 23, 27): 1 trial each. -5 Save -> 'cap must be more than 0', 6 Tab -> message gone, saved; cap/Model Esc revert + park; cap/Model/key blank click park (cap/Model save); blank park then Shift+Tab -> Model box. PASS. App killed (own PID 15368); cmdkey 57 = 57, sorted target names identical (raw order differs between runs); log 0 ERROR. ~6 of 25 min. Writing report.
- 13:06 report written to regular/QA-REPORT.md: PASS WITH CAVEATS defectsQa=0 (ACC 3 wording vs REQUEST rule 2). ~7 of 25 min.

# QA progress -- G12 round 3 re-check (regular), build bc18037 (winui-rebuild HEAD; round-3 code cb7ba8d), limit 25 min, start 13:26

## Plan (one trial per distinct action; none timing-dependent -> 1 trial each)
- Setup 5: fresh worktree qa-g12r3 (old qa-g13-reg held a dirty file), strict Debug build (38 s measured G13), no test run (precheck 229/229 given), scratch MENTOR_ROOT mroot-g12r3, suffix verify-g12r3, in-memory keys, cmdkey names before/after.
- C1 lines 1-2 + G12-12 + qa-extra (round-2 park cases): one script. Reference = Tab / Shift+Tab straight from the focused box (no park); then per box (salary, tag amount, cap, Model): click box, blank click, Tab -> same target; click, blank, Shift+Tab -> same target; plus salary Esc, Shift+Tab. ~3 min.
- C2 line 3 + qa-extra new-tag Esc: type in new-tag name, Esc, read focus/value, Tab -> next after new-tag line. ~1 min.
- C3 line 4: cap NumberBox spin up/down by position, read value/focus/config; app alive. ~2 min.
- C4 line 5: G12 1,3,5,6,10,11 + G13 2 (scripts adapted from g12_a/g13_a). ~4 min.
- Report 4. Total ~19 of 25. Other qa-extra lines: NOT RUN (brief limits scope to lines 1-5).

## Progress
- 13:30 Setup + C1 lines 1-2 + G12-12: fresh worktree qa-g12r3 at bc18037, strict build 0 warn (36 s), scratch launch PID 10196. Salary, Studioshoo tag, AI cap, Model: blank click parks (IncomeFocusPark), park+Tab and park+Shift+Tab land exactly on the reference targets from a plain Tab/Shift+Tab (8/8 SAME); salary Esc+Shift+Tab = reference (Settings nav item, true tab order); type 9 after park: nothing. 1 trial each (key/focus handlers, not timing); tag box re-run once because a 'Good morning' dialog popped mid-run. PASS. 4 of 25 min.
- 13:31 C2 line 3: new-tag name and amount: type, Esc -> both boxes empty, focus IncomeFocusPark, 9 goes nowhere; Tab/Shift+Tab after the Esc = reference targets (name: Tab->amount, ShTab->Remove; amount: Tab->Add, ShTab->name). 1 trial each. PASS. 5 of 25 min.
- 13:35 C3 line 4: Hours & reminders NumberBoxes (Grace, Repeat; AI cap has no spin buttons): up/down by position = +1/-1 each (15->16, 16->15->16, Repeat 5->4->5), saved to config, focus stays in the box whose arrow was clicked; from a park the first arrow click only focuses that same box (WinUI shows the arrows only on a focused box), 2nd click steps. No crash. 1 trial per direction/start state; 2 script retries were harness positioning (hover re-entry needed), not app. PASS. Note: stray user keystrokes ('are') reached the scratch Grace box once while it was foreground; reverted, no real data. 9 of 25 min.
- 13:36 C4 line 5: G12 1,3,5(salary+tag),6,10(+reopen),11 and G13-2, 1 trial each, all PASS; positive control (key+Save -> Key saved) proves the Save probes; killed own PID 10196; cmdkey 57 names identical before/after. 10 of 25 min. Report next.
- 13:37 Report written to regular/QA-REPORT.md. VERDICT PASS defectsQa=0. 11 of 25 min.

# G14 user rulings - QA run (regular, build 4429235, limit 30 min, start 14:07)
## Plan (1 trial per distinct action; nothing here is timing-dependent)
- Setup ~3: worktree detach 4429235, strict Debug build + test build + dotnet test (line 7), data/ file list + cmdkey names snapshot.
- C1 line 1 ~3: launch with missing MENTOR_ROOT -> read message box text, process exit, no progress.db in scratch path / exe dir / real data/ unchanged (metadata only); then launch with existing empty folder -> main window opens.
- C2 line 2 ~4: same instance, window 1180x780, Reports Day/Week/Month/Year: header text elements' rectangles (height <= 2 lines, inside table) + PrintWindow screenshot each.
- C3 line 3 ~4: AI cap valid+Enter, Model+Enter (focus, then leave/reopen Settings, values kept); cap -5 + Enter (message, focus stays).
- C4 lines 4+5 ~4: salary 999999999999 typed (message while typing, config unchanged), Tab (message, box shows saved amount), click AI cap (message gone). Agentic half of line 5: not this side (contamination rule).
- C5 line 6 ~3: Esc reverts salary; blank click parks; typed key + blank click -> summary stays "No key", cmdkey names unchanged.
- Report ~4. Total ~25 of 30. qa-extra lines: NOT RUN beyond what lines 2/4/5 cover (brief: lines 1-7 only).
## Progress
- 14:13 Setup + line 7: strict Debug build 0 warnings 0 errors (59 s), dotnet test 229/229 passed. 1 trial (deterministic). PASS. 6 of 30 min (5 lost to a bad wait loop).
- 14:16 C1 line 1: missing MENTOR_ROOT -> dialog 'Planillium — data folder missing' naming the exact path, OK -> process exit code 1; path still absent; no new progress.db anywhere under D:/CLAUDE/Planillium except the live app's own WAL. Real data/: only mentor-winui.log (writes every minute at :49, also before my launch) and progress.db-wal (14:10:51, before my launch) changed = live Release PID 21532. 2 trials: the 2nd to separate the live app's writes from mine. Existing empty folder: starts, creates mroot/data. PASS so far. 9 of 30 min.
- 14:19 C2 line 2: 1180x780 (window 1180x780 measured), Day/Week/Month/Year: 'Income / lost/gained' and 'Of which / paid work' on 2 lines (30 px high), right edge max 1235 < table edge; all one-word headers single line; screenshots rep_*.png show every header in full. 1 trial per period (layout, deterministic). PASS. 12 of 30 min.
- 14:21 C3 line 3: cap ^a7 Enter -> config cap_eur 7, focus on park button (no box), typed 9 changes nothing; Model ^a + name Enter -> config model saved, focus parked, 9 nothing; leave to Reports and back: 7 / model still there; cap -5 Enter -> inline 'The cap must be more than 0 ...', focus stays in AiCapBox, config unchanged. 1 trial each (key handlers). PASS. 14 of 30 min.
- 14:24 C4 lines 4+5: salary ^a 999999999999 (box keeps 9 digits: 999 999 999) -> red 'Amount is too large — at most €100 000,00 a month.' under the box while still in it (screenshot c4_typing.png), config has no income key (nothing saved); Tab -> box shows 2 925 (saved), status 'Amount is too large ... The saved amount was kept.'; select AI section: message still there; click AI cap box -> message gone. Variant: click Studioshoo box -> gone. 1 trial + 1 variant. PASS. Agentic half of line 5: not run (other side). 17 of 30 min.
- Time correction: the 14:21 and 14:24 stamps above were guessed; clock shows C3 ended ~14:18, C4 ~14:19.
- 14:20 C5 line 6: salary ^a3000 Esc -> 2 925, focus park, 9 nothing, config no income key; true blank click (under point = SectionScroll) -> IncomeFocusPark, then Tab -> EmployedToggle = plain-Tab reference; key typed + blank click -> summary 'No key', Save probe still 'No key', cmdkey 57 names same. 1 trial each. PASS. Outside checklist: click on page text (title 'Settings' / tracker line) from salary -> focus on unnamed Pane, Tab then goes to nav 'Today' (not EmployedToggle). 13 of 30 min.
- 14:21 Extra (REQUEST 4/5 tag boxes): Studioshoo amount 999999999999 -> live message, Tab -> 1 600 + status refusal, click salary -> gone; new-tag amount -> live message, Esc -> gone; config unchanged. 1 trial. PASS. Killed own PID 20716; live PID 21532 untouched; cmdkey same; data/ file list same, only live app's minute log (:49) and WAL (:51) writes. 14 of 30 min. Report next.
- 14:22 Report written to regular/QA-REPORT.md. VERDICT PASS WITH CAVEATS defectsQa=0. 15 of 30 min.
