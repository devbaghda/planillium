# Brief -- B1b (user decision 2026-10-24, from B1 QA observation O1 and the user's own review)

User's words: money boxes save on focus loss / Enter, not per keystroke; park focus on a harmless element when an
Income heading is clicked so stray typing can't edit the first number box; a typed minus shows a short "amounts
can't be negative" hint instead of being dropped silently.

Context: B1 (committed regular c88dd59/c0db5bd, agentic ddfbe2f) added thin-space money formatting, no spinners, the
EUR strip on Reports. Both sides' own code. Rules: DECISIONS.md rule 14 (payable tags). Real data off limits;
build strict (-warnaserror --no-incremental), QA owns all tests. Update your own side's CONTEXT.md /
context/todos.md / CHANGELOG.md / MANUAL.md as needed. Do not push.
