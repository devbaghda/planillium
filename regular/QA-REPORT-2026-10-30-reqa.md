VERDICT: FAIL - workflow=regular feature=Re-QA after G11 (group QA 2026-10-30) defectsQa=2

G11 itself: PASS WITH CAVEATS (10/10 lines). The FAIL comes from two qa-extra lines (Settings boxes), not from G11.
Build: Debug of HEAD bf64e55 in isolated worktree `D:\CLAUDE\Planillium\.claude\worktrees\qa-g11-reg` (detached, `git rev-parse --git-dir` = `.git/worktrees/qa-g11-reg`).
Scratch: MENTOR_ROOT `...\56fe036f-...\scratchpad\mroot-reg-qa` (empty DB, no progress.db copied; config.json + plans/ copied from the previous scratch run), MENTOR_INSTANCE_SUFFIX=verify,
PLANILLIUM_AI_BASE_URL=http://127.0.0.1:8765 (own Python SSE stub), PLANILLIUM_SCRATCH_AI_KEY=sk-ant-FAKE-qa-scratch-0000. The live app (PID 7148) ran throughout and was never touched.
Time: 10:36-10:55, about 19 of 35 minutes (clock read with `date`).

## G11 group-QA fixes -- PASS WITH CAVEATS

| Line | Result | Evidence |
|---|---|---|
| 1 Replace remaining, compact reply | PASS | Ask -> AI notice -> Accept -> stub hit (key from env) -> preview "1 new task(s) will be added, days 1-1"; no "Something went wrong"; `mentor-winui.log` has 0 ERROR lines |
| 2 Add plan, compact reply | PASS | "Waited 0:00 - Claude answered."; reply box shows the expanded JSON (id qa-new-plan, 2 tasks); Import plan created `plans/active/qa-new-plan.json`; 0 ERROR |
| 3 Reports rows = Total to the cent | PASS | Ledger rows of -94,3548 each (fractions of a cent). Week: income 5 rows sum -372,55 = Total; paid 99,20 = Total. Month: -2 731,43 / 99,21 match. Year: -29 056,44 / 99,21 match (sum computed from the on-screen cells) |
| 4 Paid tag listed once | PASS | Tag filter: -, Routine, Documents, Studioshoo, Selfdev, Procrastination (Studioshoo once); one "Mark Studioshoo (8)" button |
| 5 App filter Telegram | PASS | Telegram = 3 entries (Family chat, "- Telegram", "– Telegram"); Other = 2 (Some random window, Planillium); Excel 1 + Firefox 1 + Zoom 1 + Telegram 3 + Other 2 = 8 = day total |
| 6 Headers at 1180x780 | PASS (caveat) | At rest the last header is cut to "Of which paic"; the table scrolls sideways (seen: after a wheel on the table the header reads "Of which paid work" in full, screenshot `q\rep_hscroll.png`). Caveat: no scroll bar is visible until the mouse is over the table |
| 7 AI cap box message | PASS | "-5" + Save -> "The cap must be more than 0 (it is the most the AI may cost in a month)."; typing 6 clears it; after leaving, no stale message |
| 8 Spend line format and refresh | PASS | "€0,00 of €5,00" -> after Test connection "€0,01" with no restart; later "€0,03"; kept after restart |
| 9 Scratch key, no Credential Manager change | PASS | Test connection "Connected ✓" and both Asks reached the stub with the env key; Save of a typed key and Remove key worked; `cmdkey /list` before = after (diff empty) |
| 10 Build and suite | PASS | Strict build `-warnaserror --no-incremental` 0 errors (44 s); `dotnet test` 229/229 |

## qa-extra.md cases -- FAIL (2)

| Case | Result | Evidence |
|---|---|---|
| 2026-10-30 user: Settings box Esc / click on blank page area releases focus | FAIL (AI cap box) / PASS (Income salary box) | AI cap: type 7 + Esc -> focus stays, a following "9" makes "79"; click on blank page area (two spots: below the AI section and the right-hand blank under the section list) -> focus stays (screenshot `q\cap_blank.png`). Clicking the page title releases it. Salary box: Esc and blank click both park focus (IncomeFocusPark). = **D1** |
| B1g: saved money box, 999999999999 | FAIL | Salary box: no "Amount is too large" message ever shows; the box ends at "999 999 999" and Tab/Enter/click-other **save it** (`config.json` income.monthly_net_eur = 999999999, status "Saved."). Esc reverts to the saved amount correctly. = **D2** |
| B1g: -5, Enter, then Esc | Does not match the line; not counted | Enter keeps "-5" with "Amounts can't be negative." and parks focus; Esc then changes nothing (box still -5, message stays). The line applies to regular only if the Esc rule is extended; see "Needs a ruling" |
| B1h: save new amount with Enter, then Esc | PASS | 2925 + Enter saved; 4000 + Esc -> box shows 2 925 (the newly saved value), focus parked |
| B1h: scratch Settings must not show real credentials | PASS | Scratch run shows "TickTick: Not connected"; AI row shows "Key saved" only from the env scratch key; cmdkey unchanged |
| B1e: Reports kept-strip values equal pre-change values | UNTESTABLE | needs the pre-change build side by side; no pre-B1e regular exe exists (and the strip was removed by B1i, per the previous report) |
| 2026-10-29 Ask Claude compact reply (Replace + Add plan) | PASS | = G11 lines 1, 2 |
| 2026-10-29 Reports rows to the cent | PASS | = G11 line 3 |
| 2026-10-29 Tag listed once | PASS | = G11 line 4 |
| 2026-10-29 AI cap -5 message | PASS | = G11 line 7 |
| 2026-10-29 (agentic) "- Telegram" under Telegram | PASS on regular | = G11 line 5 |
| 2026-10-29 (agentic) headers at 1180x780 | PASS (caveat) | = G11 line 6 |
| 2026-10-30 (agentic) Esc on Settings boxes | n/a here | agentic line; regular result is in the first row |

## 0b live AI checks (standing ruling) -- PASS
- Notice: first Ask shows the "sends this prompt to Anthropic..." notice with Accept / No thanks.
- Stub HTTP 500: "Claude is unavailable right now (HTTP 500)..." and the plan file's sha1 stays the same (49825d69...).
- Slow stub: "Asking Claude... gives up after 4 minutes", "Waiting 0:02/0:03"; inline Cancel -> "Stopped after 0:09 - request cancelled."; plan file the same.
- Cancel on the Replace confirm closes the dialog; plan file the same.
- Blocked: relaunched without PLANILLIUM_AI_BASE_URL -> Test connection says "AI is off in this test copy (scratch data folder)... Nothing was sent."; the stub got no request.
- Save typed key / Remove key: "Saved." / "Key removed."; Credential Manager unchanged (cmdkey diff empty).

## Prior NOT RUN lines (group QA 2026-10-29)
| Line | Result | Evidence |
|---|---|---|
| g3 3 restart | PASS | 6 tag marks; after kill + relaunch the diary rows show "Routine" (the value after the Undo below) |
| g3 3 cause statement | NOT RUN | report line (builder write-up), not read |
| g3 4 keep last 5 backups | PASS | 6 bulk writes -> exactly 5 files in `data\backups`; still 5 after two more Undo backups |
| g3 5 Undo from Settings, edited-since skipped | PASS | Change history Undo of the newest change: "Restored 8 rows", DB back to routine; Undo of an older change on the same rows: "restored 0, skipped 8 changed since" |
| g3 8 Undo forgets taught keywords only | PASS | Mark on-plan (5) taught 5 keywords; Undo from the message bar: "Forgot the 5 keywords..."; the earlier "Word" keyword stays |
| g3 9 ticks kept | PASS (partial) | 5 of 8 rows ticked (not 12), "Mark on-plan (5)"; the same 5 ticked after Mark and after Undo. Filter-stops-matching sub-check NOT RUN |
| g4 1 Add plan / g4b real compact reply | PASS | = G11 lines 1, 2 and 0b |
| g1 4, 8; g2 5 (and 1, 4 other lists); g3 6, 7 (single row, scores, income); g5 1, 2, 4; g6 3 new overdue live, 4; g8 other pages; g9a 2, 3; g10 3, 4 | NOT RUN | stopped to keep the run safe after the misclick below; g1 4 also needs a pre-change build (none exists -> UNTESTABLE) |
| Priority 4: Reports headers at 1180x780 | PASS (caveat) | = G11 line 6 |

## Defects
**D1 (medium) - AI cap box keeps the cursor on Esc and on a click in blank page space.**
Repro: Settings -> AI, click the Monthly AI cap box, type 7, press Esc -> the cursor stays (type 9 -> "79"). Click any blank part of the page -> the cursor stays.
Why it matters: the user reported exactly this on 2026-10-30; keys go into the box when the user thinks they left it. The Income boxes already do it right (focus moves to a hidden parking spot), so the fix is to give the AI boxes the same leave-paths: Esc and a blank-area click move focus to a harmless parking element; that releases the box the same way the Income boxes already do.

**D2 (medium) - Salary box saves 999 999 999 €/month with no warning.**
Repro: Settings -> Income, click "Previous salary", type 999999999999, press Tab (or Enter, or click another box) -> "Saved.", config holds 999999999.
Why it matters: a slipped key turns every lost-income figure into nonsense, silently. Fix: refuse amounts above a sane maximum with "Amount is too large" while typing and never save them (as the qa-extra line asks); the leave paths then revert to the saved amount, so nothing wrong is stored.

## Needs a ruling (not counted)
- The 2026-10-30 user line says Esc "leaves the box keeping the typed value"; the B1h rule (LESSONS, Income) says Esc reverts a saved box to the saved amount. Regular's Income boxes revert (B1h). Which applies to the Income boxes, and does "keep the typed value" mean save it?
- -5 + Enter + Esc (B1g line): regular keeps -5 and the message after Esc. Matches the line only if the Esc rule is extended to this case.

## Security
- No real key used: only `sk-ant-FAKE-...`; all AI traffic went to 127.0.0.1:8765; the blocked path sends nothing.
- Credential Manager: `cmdkey /list` identical before and after (28 lines).
- No real data read or written; scratch config/plans only. The scripts read only scratch files.
- No new input surface beyond the boxes tested above.

## Extra observations
- Week paid-work Total €99,20 vs Month/Year €99,21 for the same five days (26-30 Oct). Each view adds up within itself (G11 line 3 holds), but the same money differs by 1 cent between views.
- App and tag filters list a "-" entry; in the App filter it matches no entries on the day ("No entries match").
- A wheel over the bottom edge of the Reports table scrolls the table sideways instead of the page.
- Replace remaining with a day-1 reply says "The plan will now finish on 19.10.2026 (was 17.11.2026)" - an end date in the past for a plan shown as "Day 1 of 30" (fixture plan from the previous run; not examined further).
- **Misclick into the user's mail client:** during the first g3 9 attempt the app lost the foreground (the user was working in the mail client) and 6 clicks landed there (message selection only, no keys typed). From then on every click was guarded by a foreground check. Tell the user in case a message was marked read.
- Stopping the stub with a command-line match (`*stub.py*`) also ended the shell running the command (exit 255); only own processes were hit; the live app stayed up.

## New cases
- Settings -> AI cap box (and Model, API key boxes): type, press Esc -> cursor leaves the box; type, click blank page area -> cursor leaves the box (regular: cap box fails both).
- Income salary box: type 999999999999, Tab -> "Amount is too large" shown, nothing saved, box shows the saved amount (regular saves 999 999 999).
- Reports with paid-tag time on 5 days of one week: the paid-work figure for that week is the same in Week (Total) and Month/Year (that week's row) to the cent (regular: 99,20 vs 99,21).
- Diary App filter: every listed app entry matches at least one entry, or "-" is explained (regular shows "-" matching nothing).

## Lessons
- Before every real click, check the foreground window belongs to the app's PID and abort if not: the user works at the same PC and a lost foreground sends clicks into their apps (happened in this run).
- Never kill by command-line match on a string the killing command itself contains; save the stub's PID at launch and kill that PID.
- In this app `top_window().child_window(title=...)` fails once a dialog is open; find controls by walking `descendants()` instead.
- A clipped header with a horizontal ScrollViewer only shows its bar on hover: confirm sideways scroll by wheel/scroll and a re-read of the header width, not by a resting screenshot.
- Take the clock from `date` for progress lines; guessed timestamps were wrong by 10+ minutes.
