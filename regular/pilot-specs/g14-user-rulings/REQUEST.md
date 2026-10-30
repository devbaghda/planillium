# G14 - user rulings 2026-10-30 (same text on both sides)
1. Data folder missing: if MENTOR_ROOT is set but the folder does not exist, the app refuses to start and shows a plain message naming the folder. It must never fall back to the real data. (When MENTOR_ROOT is not set at all, nothing changes.)
2. Reports tables (Day/Week, Month/Year): column headers wrap onto two lines so every header is fully readable at the default window 1180x780; no clipped header.
3. Settings: pressing Enter in the AI cap box or the Model box with a valid value saves it AND leaves the box (cursor in no box), like Esc and click-away. Invalid value: message stays, box keeps focus (as today).
4. Settings: the "Amount is too large" message (salary and payable-tag amounts) disappears when focus moves to another box.
5. Settings (regular): the "Amount is too large" message appears while typing, as soon as the typed amount exceeds the ceiling (agentic already does; keep it there). Nothing is saved until the box is left.
Unchanged by ruling: Diary "Mark all neutral" overwrite behaviour; paid-tag income ignores capitals; small-task rule.
