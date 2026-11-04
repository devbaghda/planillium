# Feature request: Replace remaining tasks, v2 (better prompt, verification, parity)

User (2026-10-13), after trying "Replace remaining tasks" on the AI Microsolutions plan to remove Reddit.

**Settled decisions (user, 2026-10-13):**
- Overdue tasks are still replaced along with every other unfinished task (today's behaviour kept after the
  alternative "keep overdue" was offered and declined). Past score history is never touched.
- The prompt must give Claude the full content of every remaining task, not only titles: the detail text,
  mentor note, category, duration and tools. Claude can then find the thing to remove wherever it appears
  (e.g. inside a task's detail text).
- The dialog gets an optional "Words that must be gone" field (comma-separated, e.g. `Reddit`). When filled,
  the preview lists every new task whose title, detail, mentor note or tools still contain any of those words
  (case-insensitive) and warns clearly. The user may still confirm or cancel. Left empty: no check.
- Both apps have the "What should change?" field and both prompts list the remaining tasks in full.
  (The agentic app currently has neither; the two apps keep their own designs otherwise.)
- Nothing changes until the user confirms the preview (unchanged).
