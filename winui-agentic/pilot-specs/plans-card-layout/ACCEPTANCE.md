# Acceptance - Plans page card layout (check in a scratch MENTOR_ROOT or read-only on live)

1. A plan with a long name ("AI Microsolutions Specialist - 30-Day Brand Build") shows its name on at most two lines at the default window width.
2. The meta line ("Day X of Y - n/m tasks done"), the "Originally due" line and the progress bar are not clipped or truncated.
3. Same at the narrowest supported window width: no horizontal scrolling, no text cut mid-word.
4. Every action is still reachable: Briefing (only if the plan has one), + Add task, Replace remaining..., Excluded days..., Archive, Teach on-plan apps... (only if the plan has tools).
5. Each action still opens the same dialog / does the same thing as before; Archive stays disabled until all tasks are done; Replace remaining behaves exactly as it did before the layout change (enabled/disabled state unchanged).
6. Accessibility names still include the plan name; tooltips preserved.
7. No new colours. Build clean with /warnaserror; tests still pass.

