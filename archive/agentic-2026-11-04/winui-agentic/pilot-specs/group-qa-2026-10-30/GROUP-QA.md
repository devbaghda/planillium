# Re-QA after G11 -- 2026-10-30 (one run; PM brief)

Workflow: agentic. Target: this side's Planillium.App, Debug build of current HEAD. Run QA ONCE. Order of priority (do the earlier first;
anything unfinished at the cap is NOT RUN):
1. G11 (pilot-specs/g11-group-qa-fixes/ACCEPTANCE.md), all 10 lines. Scratch AI key via PLANILLIUM_SCRATCH_AI_KEY, AI to the local stub via
   PLANILLIUM_AI_BASE_URL. Include the Ask Claude path (Replace remaining and Add plan) with a valid one-line compact reply, read the log.
2. The qa-extra.md cases (all of them), and a before/after cmdkey /list to prove no Credential Manager change.
3. Lines that were NOT RUN or PARTIAL in the group QA of 2026-10-29 (read the previous QA-REPORT.md in the side folder, "NOT RUN summary",
   before overwriting it): g1 4, 8; g2 1 (other lists), 4; g3 restart/cause, backup keep-5, Undo from message bar and edited-since skip, 6, 8, 9;
   g4 / g4b with a real compact reply (needs the key); g6 new overdue live; g8 other pages; g9a 2 and 3; g10 3, 4; g5 1, 2, 4 last (slowest).
   For lines needing a pre-change build, mark UNTESTABLE (code-only / needs old build) only if no old exe exists.
4. Reports header check at the default window 1180x780: every header readable or the table visibly scrolls.

Rules (user rulings, standing): cap 35 minutes of QA wall-clock in total; write planned minutes per item FIRST; one trial per distinct action;
never re-test a check that already passed in the previous report (list in the prior QA-REPORT.md); regression scope = what the diffs touch.
QA model opus. Isolated worktree; scratch MENTOR_ROOT (existing empty folder) with MENTOR_INSTANCE_SUFFIX=verify; never a real key; kill only
your own process. UIA driven; screenshots via pywinauto capture_as_image if PrintWindow is black. Read qa-extra.md and app-qa-checks.md first.
A UI line is PASS only if seen happening; otherwise UNTESTABLE with an allowed reason (slow is not one); unrun lines = NOT RUN.
Do not fix anything. Report per feature: verdict, defects (severity), NOT RUN lines, New cases, Lessons. Keep the previous report by copying it
to QA-REPORT-2026-10-29.md first, then write the full new report to QA-REPORT.md in the side folder; the builder passes it up verbatim.
