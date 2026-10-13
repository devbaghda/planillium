# Acceptance — replace remaining tasks v2 (run only against a scratch MENTOR_ROOT, never real data)

Fixture: plan of 10 tasks, days 1-10, tasks 1-4 done; tasks 5-10 unfinished, each with detail, mentor note,
category, duration and tools; task 8's detail text mentions "Reddit" (its title does not); task 6's title does.

1. The dialog has a "What should change?" field; its text appears in the copied prompt.
2. The copied prompt lists all 6 unfinished tasks with day, title, detail, mentor note, category, duration, tools.
3. The prompt still lists the 4 done tasks as fixed, and still shows the reply format.
4. The dialog has an optional "Words that must be gone" field. Empty: preview shows no warning section.
5. Reply JSON where a new task's detail contains "reddit" (lower case) and field = "Reddit": preview warns and
   names that task.
6. Same field matched in a title, a mentor note and a tools entry: each is caught.
7. Field "Reddit, LinkedIn": a task containing only LinkedIn is caught too (comma list works, whitespace trimmed).
8. A warning does not block: Confirm still applies the replacement; Cancel leaves the plan file byte-identical.
9. A reply with no match for the words: preview shows "no mentions found" (or equivalent), no warning.
10. The words field is not saved anywhere (reopening the dialog starts empty).
11. All acceptance lines of replace-remaining-tasks still pass (old lines 1-16).
12. Build clean (0 warnings with /warnaserror); existing tests pass; new tests cover the word check.
