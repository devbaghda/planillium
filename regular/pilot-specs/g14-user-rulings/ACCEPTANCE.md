# G14 acceptance (mechanical)
1. Scratch run with MENTOR_ROOT pointing to a folder that does not exist: the app does not open the real data; it shows a message naming the folder (or exits with a clear message) and no database is created anywhere else. With an existing empty folder it starts normally.
2. Reports at 1180x780, each period (Day, Week, Month, Year): every table header is fully readable, wrapped onto at most two lines, none clipped.
3. AI cap box: type a valid new value, press Enter: saved (reopen Settings: still there) and no box holds the cursor. Same for the Model box. Invalid cap (-5), Enter: message shown, box keeps focus.
4. Salary box: type 999999999999, Tab: "too large" message. Click another box (e.g. AI cap): the message is gone.
5. Regular: type 999999999999 in the salary box without leaving: message appears while typing; nothing saved until Tab; after Tab the box shows the saved amount. Agentic: same still holds.
6. Regression, one trial each: Esc reverts a Settings box; blank click parks the cursor; typed API key + blank click stores nothing.
7. Strict build 0 warnings, tests green.
