VERDICT: FAIL - workflow=regular feature=G12 (Settings Esc on every box, salary ceiling, Week/Month/Year cents) defectsQa=1

G12 acceptance: 9/9 lines PASS (line 4 with a caveat). The FAIL comes from qa-extra lines 21/23 (blank-area click in the AI section): the click leaves the box but drops the cursor into the API key box.
Build: Debug of 687a151 in isolated worktree `D:\CLAUDE\Planillium\.claude\worktrees\agent-a8a9395131ec69578` (detached; `git rev-parse --git-dir` = `.git/worktrees/agent-a8a9...`).
Scratch: NEW empty MENTOR_ROOT `...\scratchpad\mroot-g12` (nothing copied; the app created DB and config), MENTOR_INSTANCE_SUFFIX=verify, PLANILLIUM_AI_BASE_URL=http://127.0.0.1:8765 (no AI call was made), PLANILLIUM_SCRATCH_AI_KEY=sk-ant-FAKE-qa-scratch-0000. Seeded 21 diary rows (26-30 Oct, 45 min paid "Studio" per day, plus one empty-window row).
Every click and keystroke went through a check that the foreground window belonged to the app PID QA started (18824); no aborts. The live app was not touched. Killed only PID 18824.
Time: 11:18-11:30, about 12 of 35 minutes.

## Acceptance (pilot-specs/g12-esc-salary-cents/ACCEPTANCE.md)

| Line | Result | Evidence |
|---|---|---|
| 1 AI cap: type, Esc | PASS | Saved 8 (Enter -> config ai.cap_eur 8). Typed 12 + Esc -> box shows 8, focus on IncomeFocusPark, no message; typing 9 afterwards changes nothing |
| 2 Model and key: Esc | PASS | Model: typed "xyz-typed" + Esc -> "claude-sonnet-5-5", focus parked, typing 9 changes nothing. Key: typed sk-ant-TYPED... + Esc -> focus parked, box shows the empty placeholder "Saved - type a new key to replace it" (`qa/g12_a2_key.png`), status still "Key saved"; cmdkey unchanged |
| 3 Cap -5, Enter, Esc | PASS | Enter -> "The cap must be more than 0 ..." with -5 kept, cursor stays; Esc -> box 8, message gone, focus parked, config still 8 |
| 4 Cap: blank-area click saves | PASS (caveat) | Typed 6, clicked blank space right of and below the boxes (633,577) -> config 6, "Saved.". Caveat: focus lands in the **API key box** (see D1) |
| 5 Income Esc (no regression) | PASS | Salary 4000 + Esc -> 2 925, focus parked, typing 9 changes nothing; tag box 2000 + Esc -> 1 600 |
| 6 Salary 999999999999 + Tab | PASS | Box takes "999 999 999" while typing (no message yet); Tab -> "Amount is too large — at most €100 000,00 a month. The saved amount was kept.", box 2 925, config income unchanged (`qa/g12_b6_tab.png`) |
| 7 Salary normal amount | PASS | 3100 + Tab -> "3 100", "Saved.", config 3100; 2925 + Enter -> "2 925", saved, focus parked |
| 8 Week/Month/Year cents | PASS | Paid 4,4643 €/day x 5 days: Week rows 4,46 x5, Total €22,30; Month row for the week €22,30; Year October €22,30 (old code would give 22,32). Income: Week Total -449,45 = Month row -449,45; every view's rows add to its Total |
| 9 Strict build, tests | PASS | `-warnaserror --no-incremental` 0 warnings 0 errors (37 s); `dotnet test` 229/229 |

## qa-extra.md cases dated 2026-10-30

| Case | Result | Evidence |
|---|---|---|
| 21 (user) Settings box Esc / blank click releases focus | Esc PASS; blank click FAIL in the AI section | Esc leaves every box (cap, Model, key, salary, tag). The line's "keeping the typed value" is overruled by DECISIONS (G12 user ruling: Esc reverts), so tested as revert. Blank click: Income boxes park focus (PASS); AI cap and Model boxes send focus to the API key box = **D1** |
| 22 (agentic) | n/a | agentic line |
| 23 (QA) Esc and blank click release cap, Model, key | FAIL (blank click) | as above, 3 blank spots (633,577), (1300,800), (900,300): all -> AiKeyBox |
| 24 (QA) Salary 999999999999 | PASS | = line 6 |
| 25 (QA) week paid cents Week/Month/Year | PASS | = line 8 |
| 26 (QA) App filter "-" | PASS (on this data) | Entries -, Firefox, Telegram, Zoom, Other; "-" matches 1 row (the empty-window row, 10 min); 1+1+2+1+1 = 6 = day total. The earlier "matches nothing" data set was not rebuilt (not a G12 change) |

## Regression (diff scope)
- Salary blank-area click: 3000 saved, focus parked, typing 9 changes nothing. PASS.
- Tab after park (from salary) -> Employment toggle (the next control). PASS.
- Payable-tag rate 150000 + Tab -> "Amount is too large...", box 1 600, config unchanged. PASS (`qa/g12_tag_big.png`).
- New payable tag "Big" 200000 + Add -> "Amount is too large — at most €100 000,00 a month.", not added (`qa/g12_newtag_big.png`); "Studio" 1000 + Enter -> added, config has it, focus parked. PASS.
- AI cap -5 + Tab -> text and message kept, nothing saved; Esc -> saved 4. PASS.
- Reports money columns: rows = Total in Week, Month and Year, both columns. PASS.
- mentor-winui.log: 0 ERROR lines.

## Defects
**D1 (medium) - A click on blank space in Settings -> AI puts the cursor in the API key box.**
Repro: Settings -> AI, click Monthly AI cap, type 6, click any blank part of the page (tried 633,577 / 1300,800 / 900,300) -> cap saves, but focus is now in the API key box; the next keys typed go into the key box (seen: a typed "7" landed there). Same from the Model box (its typed text also stays in the box, unsaved).
Why it matters: the user asked that a blank click releases the box; instead keystrokes go into the key field without the user noticing, and a later Save would store them as the API key.
Fix: send a blank-area click in the AI section to the same page-level park the Income section already uses (IncomeFocusPark) instead of letting the panel hand focus to its first input; then no box holds the cursor, so stray typing reaches nothing.

## NOT RUN
None.

## Security
- No real key: only `sk-ant-FAKE-...`; no AI request was made; base URL pointed at localhost.
- Credential Manager: `cmdkey /list` before = after (28 lines, diff empty). The typed key was discarded by Esc, never saved.
- No real data read or written; scratch root created empty for this run.

## Extra observations
- Shift+Tab right after a park from the salary box keeps focus on the park (nothing happens); Tab does continue forward. Minor; DECISIONS says "Tab after a park continues from the box that was left" - unclear whether that covers Shift+Tab.
- Model box left by a blank click keeps the typed text visible but unsaved (Model saves only via Save, per the build notes) - a box showing a value that is not stored.
- The 12-digit salary is cut to 9 digits while typing ("999 999 999"), then refused on leaving.
- Harness note: on a fresh scratch root a "Good morning" dialog was open; real clicks into Settings did nothing until it was dismissed.

## New cases
- Settings -> AI: type in the cap box, click blank page space -> the cursor is in no box (typing 9 changes no box, including the API key box). (regular: lands in the API key box)
- Settings -> AI: type in the Model box, click blank page space -> cursor in no box; the Model box does not keep showing an unsaved value (or says it is unsaved).
- Settings -> Income: click salary, Esc, Shift+Tab -> focus moves to the control before the salary box (regular: stays parked).

## Lessons
- A fresh empty MENTOR_ROOT shows three dialogs at first launch (name welcome, Welcome back, Good morning); real-mouse clicks fail silently until all are dismissed - dismiss them (Later / Skip by name) before the first real click.
- When checking "focus leaves the box", read which element got focus, not only that the box lost it: here focus jumped into another input.
