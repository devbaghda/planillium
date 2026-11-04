# Acceptance -- G10

1. App filter: rows whose app name is a window title without a dash appear under one "Other" entry; choosing it lists
   exactly those rows; real program names remain individual entries; counts add up to the unfiltered total.
2. App filter: combined with the other diary filters and Show all, the result is still correct; clearing the filter
   restores all rows.
3. Compact Ask: the exchange sent to and expected from Claude is shorter than before (state before/after size on a
   sample plan); the stub reply in the new form produces the same plan on screen as the old form did.
4. Compact Ask: a malformed or old-form reply fails cleanly with a message, no crash, no half-applied plan.
5. Under a scratch MENTOR_ROOT the app does not read, write or delete the real start-with-Windows setting (code
   evidence; a live read of the real registry entry is NOT allowed).
6. A run without MENTOR_ROOT is unchanged for the start-with-Windows setting (guard keyed only on the scratch condition).
7. Sibling check: every other place writing outside the scratch folder (registry, other machine-wide settings) is
   listed and covered or justified.
8. Build clean (/warnaserror); existing tests pass.
