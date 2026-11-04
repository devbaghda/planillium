# Request -- G10 App filter grouping, compact Ask, start-with-Windows guard (user, 2026-10-29)

Three small changes.

1. Diary App filter. The list of apps in the Diary's App filter contains odd entries: window titles instead of program
   names. Required: entries whose app name is a window title without a dash are grouped under one entry called
   "Other"; choosing "Other" shows all the rows grouped there. Real program names stay listed individually as today.
   (Recording the real program for every row is a later job -- not now.)
2. Ask Claude speed. Claude currently writes a long, fully formatted plan, which is slow. Required: the plan exchange
   with Claude uses a more compact form so Claude writes less and replies faster. What the user sees (the plan, its
   tasks, preview and apply) stays exactly the same. The wait limit and Cancel stay as they are.
3. Start-with-Windows guard. A test copy of the app (scratch data folder via MENTOR_ROOT) can still change the real
   "start with Windows" setting. Required: under a scratch data folder the app never reads, writes or deletes that real
   setting. A normal run behaves exactly as today.
Not to change: anything else.
