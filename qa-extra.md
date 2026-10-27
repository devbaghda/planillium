# qa-extra.md (PM-owned, shared by both sides; builders never read it)

Cases found by QA beyond the checklist. Every QA runs them as extra lines; folded into ACCEPTANCE at the next feature.

- 2026-10-27 (B1g): saved money box, type 999999999999: "Amount is too large" visible while typed; after Enter / Esc /
  Tab / click blank / click other box the box shows the saved amount, nothing saved. Run on REGULAR (not yet checked).
- 2026-10-27 (B1g): saved money box, type -5, Enter (stays, message), then Esc: box shows saved amount, cursor leaves,
  negative message gone. Run on REGULAR only if the user extends the Esc rule to it (regular Esc does nothing today).
