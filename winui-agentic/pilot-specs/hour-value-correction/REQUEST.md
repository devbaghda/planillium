# Bug report + decision: euro cost per off-plan hour is inflated

User report (2026-10-01): in Reports, "Top distractions" shows e.g. 37.3 h = -€5,353 (≈ €143/h),
"my hour of work never cost that much". The "Unearned income" card shows the same flaw in its
"That's €X per off-plan hour" line.

Cause as understood: the euro-per-hour rate is the period's whole lost income (every calendar day,
weekends and nights included) divided by only the off-plan hours.

**Settled decision (user, 2026-10-01):** one hour of the user's time is worth
**configured monthly net income ÷ 168 hours** (21 working days × 8 h; ≈ €16/h at €2,700). Use that
single hour value for the per-distraction euro figures AND the "per off-plan hour" line, so they
always agree. Put the 168 in one named place (not scattered literals). The "Unearned income"
card's own total (the calendar-day ledger figure) is NOT being changed by this item.
