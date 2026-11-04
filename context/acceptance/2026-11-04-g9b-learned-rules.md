# Acceptance — G9b learned combination rules (2026-11-04)

## User's words and decisions (top, verbatim intent)
- Combination = app + title words, general for every app (Excel was only an example); save *alike* combinations, not exact words.
- Learned from marks (Bulk Mark); proposed first, user confirms. MOST SPECIFIC WINS (more conditions beats fewer).
- Act alone only with >=10 examples AND Wilson 95% lower bound >=85% (DECISIONS 19c); otherwise a suggestion in the Diary, one-click accept, nothing asks live.
- After hours: nothing tracked, except activity a confident rule marks as work (counts for score and income; amends rule 1).
- Settings: list of rules with examples + range, delete only (no hand editing). Diary needs a "Rule status" filter.
- NOT in this feature: AI helper, merging synonyms across languages, hand-written rules.

## Cases (PASS only if seen; scratch MENTOR_ROOT, no real data, no AI)
**A. Learning proposal**
- A1 After a Bulk Mark of rows from >1 app, one proposal line per app group: "App + word(s) -> category (N examples, range)".
- A2 Words are the most shared across the marked rows of that app and rare in other rows of that app; app name, numbers, dates, file extensions are never proposed.
- A3 "Quotation_Acme.xlsx", "Quotations 2026", "quotation beta" all yield the word "quotation" (case, plural, digits ignored).
- A4 Confirm saves the rule; Skip saves nothing; editing the words before confirming saves the edited words.
- A5 Nothing ticked, or only idle rows marked: no proposal (DECISIONS 19i, j).
- A6 No shared word exists: proposal offers app-only rule or none; never an empty-word rule.

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
- E3 Rule creation/deletion is covered by Undo and by the pre-change backup (19b).

**F. After hours**
- F1 Outside working hours with no confident work rule: nothing is recorded (unchanged).
- F2 Outside working hours with a confident rule saying on-plan: the activity is recorded, counts for score and income.
- F3 Outside working hours with a suggestion-only rule: not recorded.
- F4 Off-plan or neutral rule after hours: not recorded.

**G. Safety / regression**
- G1 Scratch run never touches real data or Credential Manager; no network call.
- G2 Strict build 0 warnings; existing suite green; new unit tests for normalising, whole-word match, specificity, Wilson bar.
- G3 Sweep siblings: every place that classifies titles (tracker, bulk mark, reports, idle text) uses the same classifier.
