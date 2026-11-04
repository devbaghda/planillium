# ACCEPTANCE — G11 group-QA fixes
Mechanical pass/fail, run in the app process on a scratch MENTOR_ROOT with the local AI stub.
1. Stub returns a valid one-line compact JSON reply: Ask Claude (Replace remaining) shows the task preview; no "Something went wrong"; no ERROR in the log.
2. Same for Add plan.
3. Reports Week, Month, Year with seeded rows that carry fractions of a cent (e.g. 4 x -81,66): each income column's rows sum to its Total row to the cent.
4. Diary tag filter with a paid tag also present as a tag: the name appears once; one "Mark all" button for it.
5. Diary App filter: "- Telegram" and "– Telegram" rows are counted under Telegram; "Other" count excludes them; Other + all named apps = total.
6. Reports at 1180x780: all column headers fully readable (UIA text), or the table scrolls sideways.
7. AI cap box: "-5" + Save -> message contains "more than 0"; then type 6, click blank space -> the message is cleared or matches the current text.
8. Spend line shows the app's euro format (€0,00) and changes after a successful stub call without restarting.
9. Scratch run: AI key supplied from the environment; Ask and Test connection reach the stub; Credential Manager has no new or changed entry after the run (check before/after with cmdkey /list).
10. Build and suite: strict build 0 warnings, tests green.
