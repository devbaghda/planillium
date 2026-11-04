# Feature request: G3 Diary bulk marking + one safe door for data changes (user, 2026-10-23 items 5+6; design approved 2026-10-28)
Today, in the Diary, the Mark buttons act only on the rows on screen (e.g. the 300 most recent of 8 921 matches), and marks the user sets can disappear. Wanted:
1. Mark acts on ALL matches of the current filter/search, not only the rows shown. The button text shows the real count.
2. Marks stay until the user changes them. Investigate why they disappear today, fix the cause.
3. Before anything is written, a confirm dialog shows: rows with no category now (will be filled), rows with another category (will be overwritten), rows already that category (unchanged), and total changed = filled + overwritten. Overwriting is allowed. Buttons: "Mark N rows" / Cancel.
4. ONE door for every bulk change to past data (build it so later features, like learned rules, can reuse it): preview -> automatic backup of the diary database (dated copy, keep the last 5) -> write, storing each row's old value -> Undo.
5. After the write: a message "Marked N rows as X" with an Undo button. Also Settings -> "Change history": the last 20 changes (kept 30 days), each with Undo. Undo restores exactly the old values of that one change for rows not edited since; if some rows were edited since, it restores the rest and says how many it skipped.
Not to change: how single-row marking works, scores/income logic, other pages. Not in this group: learned rules, after-hours, Telegram, AI cost cap (next group, G9).
Rules: DECISIONS.md rule 19. Real data is off limits while building.
