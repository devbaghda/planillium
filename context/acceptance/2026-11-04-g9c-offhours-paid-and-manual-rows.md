# G9c acceptance — off-hours paid work + manual diary rows (approved 2026-11-04)

User rulings (2026-11-04): after-hours work a confident rule marks **on-plan** earns points; **neutral** earns nothing. Plus: the user can add off-hours rows by hand.

**H. Rules carry a tag**
- H1 A Bulk Mark of a paid tag (e.g. Studioshoo) on ticked rows offers a rule "app + words -> Studioshoo" (category unchanged); same dialog, evidence, confirm, Undo as G9b.
- H2 Settings rules list shows the tag; Delete/search work on it.
- H3 Same confidence bar as G9b (>=10 examples, Wilson lower bound >=85%); below it, the Diary suggests the tag with Accept (backup + Undo).
- H4 After hours, a confident rule with a paid tag AND category on-plan records the row with that tag: it counts in "Of which paid work", in Income, and earns on-plan points.
- H5 After hours, a confident rule with a paid tag but category neutral records nothing (user ruling: neutral earns nothing).
- H6 Suggestion-only rules still record nothing after hours.
- H7 Totals: a paid row at 22:00 and one at 14:00 are priced the same (tag rate / 168 h); the Reports figures equal a direct count on the data.

**M. Add a row by hand**
- M1 Diary has "Add entry": date, start, end, category, optional tag, optional note. Works for any time of day, including after hours.
- M2 Validation: end after start, not in the future, duration computed; bad input blocks Save with a message.
- M3 Saved row goes through the data-change door: preview, backup, Undo in the message bar and Change history.
- M4 The row counts like a tracked one: score, income (if paid tag), Reports and Diary filters; rule state "Marked by you".
- M5 Any overlap with an existing row is refused (user, 2026-11-04): nothing is saved and the message names the clashing row (time and window). Touching ends (09:00-10:00 then 10:00-11:00) is not an overlap.
- M6 Added rows are marked by hand, so they teach no rule unless ticked and marked in Bulk Mark as usual.

**G. Safety**
- G1 Scratch run only; no real data. G2 Strict build 0 warnings, suite green, new unit tests for H4-H7 and M2/M5. G3 Sweep siblings: every place that totals tagged minutes or reads rule category.

## Verdict (2026-11-04, solo flow)
Strict build 0 warnings; suite 284/284 (run with plain `dotnet build -warnaserror` in the tests project, then `dotnet test --no-build`).
Scratch run (worktree, scratch MENTOR_ROOT, UIA): M1 PASS (dialog opens, add works); M2 PASS (end before start, future, bad date refused with a message); M3 PASS (backup, change-history entry "added entry", Undo from the message bar removes the row); M4 PASS (row stored on_plan, tag studioshoo, rule_state manual); M5 PASS (overlap message names "10:00-10:30 Quotation_Beta0 - Drafter", nothing saved; 10:30-11:00 touching both neighbours accepted); H1 PASS (tag Bulk Mark of 5 ticked rows offered "Browser -> Studioshoo" and "Drafter -> Studioshoo"; saved with tag); H2 PASS (Settings > Activity keywords lists "Browser + meme -> off-plan + studioshoo").
H3 (Diary Accept for a suggested tag rule) and H4-H7, M6: covered by unit tests only (after-hours poll needs the clock; not driven in the UI).
Known limits: the Edit entry dialog does not check overlaps; tag-rule evidence covers only rows pruned since G9c (older pruned rows lost their tags).
