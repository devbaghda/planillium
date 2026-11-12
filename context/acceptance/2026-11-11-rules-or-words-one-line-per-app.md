# Learned rules v3 — OR words, one line per app, manual add, junk-word filter (2026-11-11)

User's words (2026-11-11): exact match forces separate rules for "word + договор" and "word + договора" — a mess. One line per app with all its rules inside. In the rule Edit window the words are separated by comma, not space, and are an OR, not an AND. I want to add rules myself. The app proposes absurd rules like "Chrome + google" or "Word + and"; remove those.

Decisions (user, 2026-11-11): old multi-word rules are converted to OR with the same words (backup first). Several rules of one app matching a title: most matching words wins, then a rule with a tag, then the older. Never propose, and refuse when typed: filler words (and, the, for, with, of, и, в, на, для, по, words under 3 letters), app/brand names (the app's own name, google, chrome, microsoft, word, excel, edge …), digits and dates (numbers, years, file extensions like docx/xlsx/pdf).

**W. Words are an OR**
- W1 A rule matches a window when the app matches and ANY one of its words is a whole word of the title.
- W2 Rule "Excel: договор, договора" matches "договор - Excel" and "договора_2 - Excel", not "report - Excel".
- W3 Two rules of one app both match: the one with more of its words in the title wins; tie -> the rule with a tag; tie -> the older.
- W4 Evidence (Wilson range, example counts) counts history rows of the app that contain any of the rule's words.
- W5 Adding by hand (or editing) a rule whose app + category + tag already has a rule MERGES the words into it (one rule, undoable); same words again changes nothing.

**L. One line per app (Settings → Learned rules)**
- L1 One line per app; inside it one sub-row per category/tag combination showing its words separated by commas, evidence and Edit / Delete.
- L2 Apps sorted alphabetically; the list scrolls; search finds apps, words and tags.
- L3 Existing rules need no conversion (same words, now OR); several stored rules of one app + category + tag show as ONE sub-row with all their words; Edit replaces them with one rule, Delete removes them all, each as one undoable change after a backup.

**E. Edit window**
- E1 Words field shows words separated by commas ("договор, договора, акт"); comma (and a new line) separates words, a space inside a word is not a separator.
- E2 Saving normalises words (lower-case, trimmed, duplicates removed) and refuses empty or junk-only input with a message naming the refused words.
- E3 Category and tag stay editable as before.

**A. Add a rule by hand**
- A1 Settings → Learned rules has "Add rule": app (typed or picked from apps seen in the Diary), words (comma separated), category, optional tag.
- A2 Same checks as E2; saved rule acts at once; goes through the data-change door (backup, Undo, Change history).
- A3 Adding to an existing app + category + tag merges (W5).

**J. Junk words**
- J1 Bulk Mark never proposes a rule containing a junk word; if only junk words are left, no proposal (like "no usable word").
- J2 "Chrome + google" and "Word + and" are never proposed.
- J3 Typed junk words in Add/Edit are refused (E2).
- J4 Existing saved rules with junk words are left as they are (not deleted); the user can edit them.

**P. Proposals under OR**
- P1 A proposal lists up to 5 words; a further word is added only when it covers marked rows the earlier words missed and keeps the rule pure (the union does not drop clearly below the first word's purity).
- P2 Marking rows titled "договор", "договора", "договору" in one app proposes one rule with those words, not three.

**S. Safety**
- S1 Scratch run only, no real data. Strict build 0 warnings, suite green, new tests for W1-W5, E2, A3, J1-J3, P1-P2, group Edit/Delete undo (L3).
- S2 Siblings: every user of rule words (matcher, evidence, proposer, store, Settings list, Diary suggestion/accept, DataChangeDoor undo, export/backup, tests).

## Verdict (2026-11-11, solo flow)
Strict build 0 warnings; suite 319/319 (new: OR match/evidence/tie-break, ParseTyped + junk, proposer OR + junk, add/merge/refuse/undo, group edit/delete undo). Scratch-root UI (Debug build, empty DB seeded with 5 rules): W/L1/L3 PASS (Excel's two stored rules show as one sub-row; one card per app, alphabetical); E1 PASS (Edit shows "budget, forecast"); A1/A3/J3 PASS (Add rule with "and, google" refused naming both, text kept; corrected save merged into Word's rule, status "Rule added. Undo is in Change history"). UNTESTABLE live: Reports proposal dialog commas (needs Bulk Mark flow; code-only, covered by proposer tests), Delete/Undo clicks in UI (covered by unit tests). Not in live Release yet.
