# qa-extra.md (PM-owned, shared by both sides; builders never read it)

Cases found by QA beyond the checklist. Every QA runs them as extra lines; folded into ACCEPTANCE at the next feature.

- 2026-10-27 (B1g): saved money box, type 999999999999: "Amount is too large" visible while typed; after Enter / Esc /
  Tab / click blank / click other box the box shows the saved amount, nothing saved. Run on REGULAR (not yet checked).
- 2026-10-27 (B1g): saved money box, type -5, Enter (stays, message), then Esc: box shows saved amount, cursor leaves,
  negative message gone. Run on REGULAR only if the user extends the Esc rule to it (regular Esc does nothing today).
- 2026-10-27 (B1h, regular QA): saved money box (salary or tag): save a new amount with Enter, then type a different
  number and press Esc: box shows the newly saved amount, not the one loaded at startup.
- 2026-10-27 (B1h, regular QA, needs a product decision first): scratch/verify instance Settings must not show "AI: Key
  saved" / "TickTick: Connected" from the real Credential Manager (O1; same item as "scratch app reaches real credentials").
- 2026-10-27 (B1e, agentic QA): Reports with seeded diary data and paid-tag time, all four periods: the kept strip values
  ("Income lost or gained", "Of which paid work") equal their pre-change values (B1e checked wording on empty data only).
