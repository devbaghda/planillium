# Feature request: lost/gained-earnings counter

Product-level request, as given by the user (2026-09-22, after an earlier clarifying round —
decisions below are settled, not open for re-litigation). Written for the Planner to turn into a
SPEC.md; do not read this as an implementation plan.

## What the user asked for

"I want to have in the sidebar a number in EUR of an unearned income that should be updating daily
automatically even if the PC was not turned off during the night. The calculation should consider
that I could have earned €2,700 net monthly and as far as I don't have that income I am losing at
least that much per month. I want to have the number in the reports page as well and it should be
calculated correspondingly for day, week, month, year. Pay attention to the text to correspond the
timerange it talks about. In the settings there should be a place where I can change the potential
net monthly income and there should be a boolean tick for employment status. As soon as the
employment status is employed, the calculation changes from deducting to increasing."

## Settled decisions (from the clarifying round — do not re-ask the user these)

1. **Daily rate** = configured monthly net income ÷ the actual number of calendar days in that
   specific month (28-31). Not a flat /30, not annualized. A full calendar month must always sum
   to exactly the configured monthly figure.
2. **Start date: 2025-12-04.** This is a real, backdated start — the user's income actually
   stopped that day. The running total must reflect the full accumulated amount from 2025-12-04
   through today, not start from €0 on whatever day this feature is built/deployed. (Confirmed
   twice live after two conflicting corrections — this is the final, correct date.)
3. **Employment status is a boolean toggle, default OFF (unemployed) right now.** While OFF, each
   day's entry **deducts** the daily rate from the running total. While ON (employed), each day's
   entry **adds** the daily rate instead — the sign flips from the day the toggle changes forward.
   Days already posted before a status change keep whatever sign applied on the day they were
   posted; flipping the toggle must not retroactively change the sign of historical entries.
4. **Sidebar**: shows the current running total in EUR, must update automatically every day the
   app is used, including catching up correctly after the PC/app was off overnight or longer (the
   user was explicit this must not depend on the app or PC staying on continuously).
5. **Reports page**: the same figure, broken down for day / week / month / year — matching
   whatever period-tab pattern Reports already uses elsewhere in the app (see
   `context/domain.md`, "App architecture", and `DECISIONS.md` for existing period-handling
   conventions before inventing a new one). **The accompanying text must name the exact timerange
   it's describing** (e.g. a "today" figure must say "today," not reuse "this month"'s wording) and
   must use language consistent with the current sign (e.g. "lost"/"unearned" while deducting,
   "gained"/"earned extra" while adding) — don't hardcode one wording for both states.
6. **Settings**: add an editable "potential net monthly income" field (EUR), and the employment
   status boolean toggle described above. Existing settings-save conventions apply — see
   `DECISIONS.md`, "A settings page split across independent sections must save each section on
   its own success" (CONTEXT.md §6) before touching the Settings page.

## Edge cases the implementation must survive

- App not opened for several days (or weeks) — daily entries for the gap must still get correctly
  backfilled/caught up on next launch, each with the sign that applied on *that* calendar day, not
  today's current toggle state.
- The monthly income figure or the employment toggle changing mid-month, or mid-day.
- The very first calculation after this feature is built: must correctly backfill every day from
  2025-12-04 to today in one pass, not just start counting from build day.
- Reporting-period boundaries (day/week/month/year) — get the boundaries right per existing
  Planillium conventions (working-hours window, week start, etc. — check `DECISIONS.md` rather
  than assuming Sunday/Monday or midnight-UTC conventions that might not match how the rest of the
  app already handles periods).
- A day where the toggle flips *during* that day — decide and document one deterministic rule
  (e.g. the status as of end-of-day, or as of when the toggle was flipped) rather than leaving it
  ambiguous; state the chosen rule in the SPEC.

## Out of scope

- No requirement to let the user edit/override any single day's entry by hand (unlike the existing
  score ledger's diary-edit recalculation) unless it's trivial to add consistently with existing
  patterns — if it's not trivial, leave it out and note it as a follow-up, don't add scope the
  user didn't ask for.
- No requirement to reconcile this figure with the existing points/score ledger — they are
  separate concepts (EUR opportunity cost vs. the accountability point system) and must not be
  merged or share a table unless the Planner has a strong architectural reason, stated explicitly
  in the SPEC.
