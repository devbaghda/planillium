# Brief -- B1i (user decision 2026-10-27)

Task size: medium. User's words: "I asked to see the balance and the of-which-paid in this block [the table] split by
corresponding day, week, month" and "remove the cards above" (Score card and the income card row, red cross on both).
Context: B1 built these as cards, B1e removed two duplicate cards; the user wanted columns in the table all along.
Read REQUEST.md, ACCEPTANCE.md, your LESSONS-INBOX.md and DECISIONS.md (income / lost-earnings rules) first.
Business-rule caution: do not invent how a day's income figure is derived -- reuse the existing income calculation
(lost-earnings counter daily rate, payable tags) per day; if the existing service cannot give a per-day value that
sums to the period figure, stop and report that instead of choosing a rule. Remove code that becomes dead.
Same constraints as B1e BRIEF (scratch MENTOR_ROOT, own side only, strict build + precheck, QA via your head).
QA budget: 20 min; scope from the diff, all four periods, plus build/tests; plan minutes first; no extension without a
new fact. The user is not testing during this run.

## Addendum 2026-10-27 (PM, user ruling) -- tests deferred
User: "leave all the tests to the end". Build only: strict build + the existing suite as precheck, report that, and
STOP. Do NOT start QA; the QA-REPORT.md requirement is waived for this batch (QA runs once at the end of the
group sequence, on all groups together). The QA budget line above is void for now.
