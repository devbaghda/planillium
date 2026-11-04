# Acceptance -- B1i (scratch MENTOR_ROOT only; never real data)

1. For each of Day / Week / Month / Year the summary table has columns "Income lost/gained" and "Of which paid work"
   after the existing columns; every row and the Total row has a value in both.
2. Row meaning: Day and Week = per day; Month = per week; Year = per month.
3. The rows of each column add up (to the cent) to that column's Total row.
4. Total-row values equal the figures the removed cards showed for the same period on the pre-change build.
5. Sign and colour as on the removed cards (negative loss colour, zero/paid colour); EUR format unchanged.
6. The Score card and the income card row are gone on all four periods; the table follows the period selector
   directly, no gap or leftover heading.
7. The table still fits or scrolls horizontally at a narrow window (no silent clipping).
8. Sibling check: no other page/view still renders the removed cards; CSV/Export content unchanged unless it
   already exported those cards (then report it).
9. Build clean (/warnaserror); existing tests pass; QA runs standing checks (app-qa-checks).
