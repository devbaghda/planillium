# Acceptance -- G4 agentic (scratch MENTOR_ROOT + local stub only; never a real key)

1. A stub reply for a 34-task plan (over 8000 output tokens) is accepted in Replace remaining and in Add plan.
2. Truncation by the new cap still shows the clear "cut off" message, no crash.
3. The Plans page layout matches the regular build's Plans page look (you may read the regular build's running look
   via the user's reference screenshots only if provided in the brief; otherwise describe the layout from this
   side's own code and request the user's reference).
4. The Ask dialog's acceptance text is visibly smaller; still readable and unclipped.
5. Replace remaining dialog: no vertical scrollbar at its default size with a typical task list.
6. Build clean (/warnaserror); existing tests pass.
