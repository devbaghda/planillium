# Request -- G4 Ask Claude (regular side; user, 2026-10-23)

1. R1: the Ask Claude dialog shows a "time waiting" counter while a reply is pending (the user likes it in the other
   build): elapsed time, updating live, gone/frozen when the reply ends or is cancelled.
2. R2: the reply arrives too slowly (Replace remaining -> Ask Claude, a 31-task rewrite). First MEASURE where the time
   goes (model call time, size of reply, any wasted wait) with the local stub or timing in code -- never a real key.
   Then make it faster or visibly progressing (e.g. show the reply as it streams, or ask for a smaller reply). Report
   the measurement and what you chose.
Not to change: what the AI is asked to do, acceptance wording, plan data handling.
