# Brief -- B1g (user rulings 2026-10-27)

Fix list in REQUEST.md. Origin: B1f QA found 7 failing checks in the Previous-salary box (Esc / refused Enter keep
the cursor in the box, 12 digits shown after Esc with no message). The user ruled the expected behaviour (REQUEST
lines 1-4). Esc rule decided by the user: revert to the saved amount and leave the box (not "clear and leave").
Read the side's LESSONS.md, LESSONS-INBOX.md, CONTEXT.md and DECISIONS.md first. Same constraints as B1d/B1f BRIEF.
Standing lesson to apply: a focus/key fix must cover every box of the same kind (Previous salary AND every tag
amount box) and every way out; sweep for siblings before calling it done.
QA: plan the whole run with measured minutes first (part C of the input-box matrix needs about 7 s per check,
17 min for 144 checks -- budget it up front or scope it from the diff); one trial per distinct action; do not re-run
what passed on an unchanged build. Model per job: see feature-workflow.md rule 5 and the lead's agent file.

## Addendum 2026-10-27 (PM) -- QA budget
QA budget: 20 min, scope from the diff: acceptance lines 1-7 in the Previous-salary box and one tag-amount box (every
exit: Esc, Enter, Tab, click elsewhere; inputs valid / empty / letters / minus / 12 digits), plus a short regression of
the Add line and B1d Enter. Do NOT run the full part A/B/C matrix. Plan minutes per case first; no extension without a
new fact (user rule).
