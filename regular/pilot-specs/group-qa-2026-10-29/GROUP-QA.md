# Group QA -- 2026-10-29 (one run for the whole group; PM brief, user ruled end-of-group QA)

Workflow: regular. Target: the side's Planillium.App (Debug build of current HEAD). Run QA ONCE, covering every feature
below, against each feature's own ACCEPTANCE.md in its pilot-specs folder (the same files on both sides).
Features: g1 (b1i-reports-income-columns), g2-diary-browsing, g3-bulk-mark-safe-door, g4-ask-claude + g4b-ask-wait-cancel, g5-memory, g6-today-dot, g7-scratch-safety, g9a-ai-cap-telegram, g10-appfilter-ask-startup

Rules (user rulings, standing):
- Cap 35 minutes of QA wall-clock in total. Write the minutes you plan per feature FIRST, before any test.
- One trial per distinct action. Never re-test a check that already passed. Regression scope = what the diffs touch.
- QA model opus. Isolated worktree; scratch MENTOR_ROOT (empty DB, no progress.db* copied) with
  MENTOR_INSTANCE_SUFFIX=verify; AI only to the local stub via PLANILLIUM_AI_BASE_URL; never a real key. Kill only the
  process you started. UIA driven; PrintWindow flag 2 for screenshots. Read qa-extra.md and app-qa-checks.md first.
- A UI line is PASS only if seen happening; otherwise UNTESTABLE with one of the allowed reasons (slow is not one).
- If the time cap runs out, mark every unrun line NOT RUN per feature in the report -- never pass unrun lines.
- Do not fix anything. Report per feature: verdict, defects found (severity), lines NOT RUN, New cases, Lessons.
- Seed scratch data as needed (diary rows > 300 incl. dashless window titles; an overdue task; a Telegram title
  with a dash in the chat name) -- scratch only.
Write the full report to QA-REPORT.md in the side folder; the builder passes it up verbatim.
