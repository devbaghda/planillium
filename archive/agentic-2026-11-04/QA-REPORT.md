VERDICT: PASS WITH CAVEATS - workflow=agentic feature=G12 defectsQa=0

Build b669eb8, Debug, isolated worktree `.claude/worktrees/qa-ag`. Strict build 0 warnings 0 errors; suite 362/362 green (agentic only; regular side not in this run).
Scratch MENTOR_ROOT under the session scratchpad, MENTOR_INSTANCE_SUFFIX=verify, AI URL to a dead local port, no key. Real mouse (SendInput) and real keys; every click/key refused unless the app was foreground and the point inside its window. Blank points chosen by ElementFromPoint (SectionScroll pane, no control). The user's live regular Release instance (PID 7148) was running and was not touched; only my own processes were killed. Credential Manager: 253 entries / 3 matching before and after. Time: about 14 of 30 minutes.

## Verdict per line (ACCEPTANCE.md)
| # | Result | Evidence (qa/g12_*.out) |
|---|---|---|
| 1 | PASS | Cap 5, typed 7, Esc: focus to the page park (Nav, no box), box 5, nothing saved, no message. |
| 2 | PASS | Model: typed, Esc -> box back to stored (empty), focus parked, nothing saved. Key: typed, Esc -> box empty (0 chars), focus parked, no key stored (summary "No key"), Credential Manager unchanged. |
| 3 | PASS | -5 Enter: "Not saved: the monthly limit must be more than 0..." and focus stays; Esc: box 5, message gone, focus parked. |
| 4 | PASS | Cap 12, blank click: focus parked, config ai.monthly_cap_eur=12. |
| 5 | PASS | Studioshoo tag box 1600, typed 1234, Esc: box 1 600, stored 1600, focus parked. |
| 6 | PASS | Salary 999999999999: "Amount is too large - at most EUR 100 000,00 a month. The saved amount was kept." visible while typed; Tab -> box 2 925, stored 2925, focus to Employed toggle (next control). Also Enter and blank click (100000,01) refused the same way; 100000 exactly is accepted; tag box and new tag (200000) refused with the same text. |
| 7 | PASS | 2925 Enter -> "2 925", stored 2925; 3100,5 Tab -> stored 3100.5. |
| 8 | PASS | Seeded paid rows Mon/Tue/Wed this week (37/37/41 min, 1600/month): Week rows 5,87 + 5,88 + 6,50, Total 18,25; Month row 26 Oct-01 Nov 18,25; Year October 18,25. |
| 9 | PASS | 0 warnings, 362/362. |
| 10 | PASS | Model typed, blank click: saved (config ai.model), focus parked; after Today -> Settings the box shows the stored model. |
| 11 | PASS | Cap 6, blank, then 9: cap 6, model empty, key box 0 chars, config unchanged except cap 6; focus parked. |
| 12 | PASS | Salary click, Esc, Shift+Tab -> nav "Settings" item. The natural Shift+Tab from the salary box (no Esc) goes to the same item, so it is the control before the box. |
| 13 | PASS | AI: cap 8, blank, Shift+Tab -> Model box (also after Esc); blank then Tab -> Save button. Income: tag box, blank, Tab -> tag "Remove"; Shift+Tab -> Employed toggle. |
| 14 | PASS | New payable tag name "abc", Esc: box empty, focus parked (not the section list). Same for the rate box (55, Esc). |
| 15 | PASS | Grace NumberBox: real click on the upper half of the spin popup 15 -> 16 (saved, reminder_grace_minutes=16), lower half -> 15. |

Sibling sweep (Esc after typing, then blank click; one trial each): Your name, Work start, Work end, Day review at, Grace, Repeat every, Idle after, Keep detailed diary, Warn before day ends, Activity keywords On-plan, Idle-answer Off-plan, the first two Scoring boxes. Every box went back to its saved value on Esc and left focus parked; blank click parked focus. PASS.
Money-box regression (salary): -5 Tab -> text stays with "Amounts can't be negative", not saved; empty/spaces/abc -> saved amount back with message; qa-extra B1h (save 3333, type 4444, Esc -> 3 333) PASS.

## qa-extra.md cases
| Case | Result |
|---|---|
| B1g 999999999999 visible while typed; Enter/Tab/blank keep saved amount | PASS (Esc for this exact value not run; Esc revert of the same box seen in line 5 and B1h) |
| B1g -5 Enter then Esc (regular only) | not for this side |
| B1h new save then Esc shows new amount | PASS |
| O1 scratch shows no real credentials | PASS (summary "No key - copy and paste" at start) |
| AI cap -5 -> message says above 0 | PASS (via Enter) |
| User Esc rule / QA Esc+blank on cap, Model, key | PASS |
| Salary 999999999999 Tab | PASS |
| Paid work week cents Week/Month/Year | PASS |
| Week rows add up to Total to the cent | PASS on 3 non-zero rows (4+ equal rows not seeded) |
| AI blank then Shift+Tab -> Model | PASS |
| Income tag blank, Tab -> Remove; Shift+Tab -> Employed | PASS |
| New tag name/rate Esc | PASS |
| B1e strip values vs pre-change, Ask Claude compact reply, Diary tag listed once, Telegram grouping, Reports headers at 1180x780, App filter "-" | NOT RUN |

## Defects
None against ACCEPTANCE or REQUEST.md.

## Observations (need a PM ruling; not counted)
1. **Medium - an abandoned API key gets saved by Enter in another AI box.** Repro: Settings -> AI, type a key in the API key box, click blank space (the typed key stays in the box, nothing saved, summary "No key"), then type a model or cap value and press Enter. Result: the summary changes to "Key saved - <model>". Seen twice (qa/g12_probe.out, then the screenshot qa/g12_spin.png; qa/g12_run.out). Cause: Enter in either box runs the shared Save (SaveAi), which saves the key box contents too. Why it matters: in the real app this writes a key the user abandoned to Windows Credential Manager without them pressing Save. In this run it stayed in memory only (scratch); Credential Manager was unchanged. Possible fix: Enter in the Model/cap box saves only that box, or a blank click on the key box empties it the way Esc does. Either way the key would only be stored by a deliberate Save.
2. Low - Enter with a valid value in the cap or Model box saves but keeps the cursor in the box. The addendum says "leave by click-away or Enter"; the side's rule for money boxes says Enter saves and focus ends nowhere. Needs a ruling.
3. Low - after a refused tag amount, its "Amount is too large" message stays under the tag box while other boxes are used (two copies were seen at once). The wording "The saved amount was kept" stays true.
4. Info - salary 0 is saved without a message (allowed: only a tag at 0 is refused per DECISIONS); 3100,5 shows as "3 100,5" (no pre-change build to compare).
5. Info - the three first-launch dialogs did not appear: the kit pre-writes user_name and today's kickoff/review into the scratch root.

## NOT RUN
The qa-extra cases above marked NOT RUN. Reason: the G12 diff does not touch them (Settings, MoneyBox, Reports paid column, EurCents, IncomeService), the 2026-10-30 group QA covered them, and the brief limits this run to lines 1-15 under a 30-minute cap.

## Security (by hand, G12 diff)
Injection: none found (no new SQL or command text; DailyPaid uses parameters). Secrets: none found in code or logs; the key box is cleared on Esc and never shown back. The abandoned-key save is listed under Observation 1 (business logic). Input validation: the money ceiling is enforced on salary, tag and new tag (boundary 100 000,00 accepted, 100 000,01 refused). Resource abuse, supply chain, deserialisation: no change.

## New cases
- AI: type a key, click blank, then Enter in the Model or cap box -> the key must not be saved (summary stays "No key") unless the user presses Save.
- AI: valid cap or Model value + Enter -> the value is saved and focus ends where the ruling says (currently it stays in the box).
- Salary 100000 Enter -> saved; 100000,01 click blank -> "Amount is too large", nothing saved.
- NumberBox spin popup: one real click on the upper half raises the value by one step and saves it; the lower half lowers it.

## Lessons
- Read the status and summary texts outside the box you are testing after every step (the AI "Key saved" summary in the section list): that is how the abandoned-key save showed up. A focus/value check alone would have passed it.
- Settings boxes: find them with fe(automation id or header) (Edit-only walk); byid() on the whole tree returned None for AiCapBox at first and cost one rerun.
- A Compact NumberBox's spin buttons are not in the window's UIA tree; ElementFromPoint over the box's right edge returns the "UpDownPopup" window, and a real click on its upper/lower half steps the value.
- Plan by script, not by line: 4 launches (AI+Income+siblings, probe, spin+seeded Reports, money regression) covered 15 lines plus siblings in about 13 minutes.
- Before reading "focus parked" as nowhere, check the id: on this side the park reports as "Nav" (custom). Typing afterwards changed nothing, which proves it.
