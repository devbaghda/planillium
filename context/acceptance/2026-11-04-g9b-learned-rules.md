# Acceptance — G9b learned combination rules (2026-11-04)

## User's words and decisions (top, verbatim intent)
- Combination = app + title words, general for every app (Excel was only an example); save *alike* combinations, not exact words.
- Learned from marks (Bulk Mark); proposed first, user confirms. MOST SPECIFIC WINS (more conditions beats fewer).
- Act alone only with >=10 examples AND Wilson 95% lower bound >=85% (DECISIONS 19c); otherwise a suggestion in the Diary, one-click accept, nothing asks live.
- After hours: nothing tracked, except activity a confident rule marks as work (on-plan -> points, paid work -> income, same as in-hours; amends rule 1).
- Settings: list of rules with examples + range, searchable, delete only (no hand editing). Diary needs a "Rule status" filter.
- Learning counts the whole Diary history, all categorised rows (user, 2026-11-04). Known effect: keyword-made rows make a rule look surer than hand marks alone would.
- Counts table kept forever (user, 2026-11-04); pre-existing pruned days cannot be recovered.
- NOT in this feature: AI helper, merging synonyms across languages, hand-written rules.

## Cases (PASS only if seen; scratch MENTOR_ROOT, no real data, no AI)
**A. Learning proposal**
- A1 After a Bulk Mark of rows from >1 app, one proposal line per app group: "App + word(s) -> category (N examples, range)".
- A2 Words are the most shared across the marked rows of that app and rare in other rows of that app; app name, numbers, dates, file extensions are never proposed.
- A3 "Quotation_Acme.xlsx", "Quotations 2026", "quotation beta" all yield the word "quotation" (case, plural, digits ignored).
- A4 Confirm saves the rule; Skip saves nothing; editing the words before confirming saves the edited words.
- A5 Nothing ticked, or only idle rows marked: no proposal (DECISIONS 19i, j).
- A6 No shared word exists: proposal offers app-only rule or none; never an empty-word rule.

- A7 Examples and ranges are computed from ALL past Diary rows with a category (hand-marked or keyword-classified, entire history), not only the rows in the current mark. Rows without a category (idle, empty) are not counted. Proposal counts must equal a count done directly on the data (scratch DB with known rows).

- A8 Raw Diary rows are kept 90 days. Before a row is pruned its (app, normalised title words, category) is added to a permanent counts table; learning and ranges use live rows + that table, so no example is counted twice and none is lost when pruned. Rows already pruned before this feature exist only as daily totals and cannot contribute.
- A9 Settings Clear actions that delete Diary history also delete the counts table (DECISIONS 19k); Export-all-my-data and backup include or knowingly exclude it (state which).

**B. Matching**
- B1 Whole-word match: rule word "quote" does not match "quotefree".
- B2 Rule applies only when both app and word match.
- B3 Rule "Excel + quotation" beats rule "Excel" and beats the old keyword boxes; rule with more words beats fewer; equal -> older rule.
- B4 Existing Settings keyword boxes still classify exactly as before when no rule matches.

**C. Confidence**
- C1 Rule with 9 examples, or lower bound <85%: tracker does NOT apply it; matching Diary rows show "suggested: <category> (N examples, range)" with one-click accept.
- C2 Rule with >=10 examples and lower bound >=85%: applied automatically to new activity.
- C3 Accepting a suggestion marks that row (and only through the data-change door: preview, backup, Undo).
- C4 A user mark that contradicts a rule lowers its range; shown range updates.

**D. Diary filter**
- D1 "Rule status" filter with: no rule / acted automatically / suggested / marked by hand; combines with other filters (AND), "Show all" and paging still work.
- D2 Counts shown match the visible rows.

**E. Settings**
- E1 List of learned rules: app, words, category, N examples, range, status.
- E2 Delete asks confirmation, removes the rule; rows it had acted on keep their stored values.
- E4 A search box filters the rules list (app, words, category), live, case-insensitive; empty search shows all.
- E3 Rule creation/deletion is covered by Undo and by the pre-change backup (19b).

**F. After hours**
- F1 Outside working hours with no confident work rule: nothing is recorded (unchanged).
- F2 Outside working hours, a confident rule saying on-plan: recorded and adds score points as in-hours on-plan time does.
- F2b Outside working hours, a confident rule saying paid work: recorded and adds income as in-hours paid time does (and points where paid time earns them).
- F3 Outside working hours with a suggestion-only rule: not recorded (no points, no income).
- F4 Off-plan or neutral rule after hours: not recorded.

**G. Safety / regression**
- G1 Scratch run never touches real data or Credential Manager; no network call.
- G2 Strict build 0 warnings; existing suite green; new unit tests for normalising, whole-word match, specificity, Wilson bar.
- G3 Sweep siblings: every place that classifies titles (tracker, bulk mark, reports, idle text) uses the same classifier.

---
## Verdict (2026-11-04, solo flow, isolated worktree, scratch root, no AI, no real data)
Build: strict Debug 0 warnings / 0 errors. Suite: 268/268 (new: engine 19, storage 10, classifier 6, after-hours 4).

UI (seen happening in the app, UIA-driven, scratch DB):
- A1 PASS: ticked 4 rows, Mark on-plan -> "Learn this rule?" dialog, "Drafter + quotation", evidence "30 of 30 agree (range 89-100%)".
- A4 PASS: editing the words live-updated evidence (4 of 4, 51-100%); restoring words restored 30 of 30.
- A5 PASS: Save rules -> rule row created; notice "1 rule saved (Undo removes them too)".
- C3 PASS: suggested row -> Accept button -> row on_plan, rule_state manual, backup + Undo entry in Change history.
- D1 PASS: Rule status filter offers Auto / Suggested / Marked by you / No rule.
- D2 PASS: counts match data: Auto 1, Suggested 1, Marked by you 5, No rule 26.
- E1 PASS: Settings list shows "Drafter + quotation -> on-plan, acts alone, 32 of 32 agree, range 89-100%".
- E4 PASS: search live, case-insensitive, AND of terms; no match -> message.
- E2 PASS: Delete asks confirmation (Cancel keeps rule); Delete removes it; marked rows keep rule_state.
- E3 PASS: Change history Undo brought the deleted rule back.
- Marking all filter matches without ticks teaches no rule (existing G3 behaviour, observed).

Covered by unit tests (PASS by test evidence): A2, A3, A6-A9, B1-B4, C1, C2, C4, F1, F2, F3, F4 (tracker hook SimulateAfterHoursPoll), G2.
G1 PASS: scratch root only, fake key, no network.  G3 PASS: sibling sweep, all LogSession calls carry rule state, no external Classify callers.

F2b NOT ACHIEVABLE as written: category `paid` is bought entertainment, not income (income is tag-based), so "paid work" cannot be a rule category. After hours records only confident on_plan rules (F2). Needs a decision if income-after-hours is wanted.
UNTESTABLE live: real after-hours clock path (needs real late-evening activity); covered by the 4 unit tests instead.
