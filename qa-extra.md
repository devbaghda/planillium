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
- 2026-10-29 (group QA, regular): Ask Claude, Replace remaining and Add plan, with a valid one-line compact-JSON stub reply: the preview shows the tasks and the log has no error (regular failed: "JsonSerializerOptions must specify a TypeInfoResolver").
- 2026-10-29 (group QA, regular): Reports Week/Month with 4+ equal rows carrying fractions of a cent: the rows add up to the Total row in both income columns, to the cent.
- 2026-10-29 (group QA, regular): Diary tag filter where a tag is also a paid tag: the tag is listed once.
- 2026-10-29 (group QA, regular): AI cap box: type -5 and Save -> message says the cap must be above 0; after leaving the box the message matches the current text.
- 2026-10-29 (group QA, agentic): Diary App filter with Telegram rows that have no chat name ("- Telegram"): they are grouped under "Telegram", not "Other" (agentic failed; check regular).
- 2026-10-29 (group QA, agentic): Reports table at the default 1180x780 window: every column header is fully readable (agentic cut the last to "Of which pa"; check regular).
- 2026-10-30 (user, regular): Settings text/number box (AI cap, Income boxes): Esc leaves the box keeping the typed value; a click on blank areas of the page (not only the sidebar) also releases focus (regular: neither worked).
- 2026-10-30 (user, agentic): same Esc rule on the Settings boxes (agentic: click-outside works and saves, Esc does nothing).
- 2026-10-30 (QA regular): Esc and a click on blank page space release the AI cap, Model and API key boxes in Settings.
- 2026-10-30 (QA regular): Salary box: type 999999999999, Tab -> "Amount is too large", nothing saved, box shows the saved amount (regular saved 999 999 999 EUR/month silently).
- 2026-10-30 (QA regular): Paid work of one week is identical to the cent in Week, Month and Year (regular showed 99,20 in Week Total, 99,21 in Month and Year rows).
- 2026-10-30 (QA regular): Every App filter entry, including "-", matches at least one diary row, or the "-" entry is explained.
- 2026-10-30 (QA regular round 2): Settings > AI: click the cap box, type 6, click blank space, Shift+Tab -> focus goes to the Model box (same as after Esc); never to the nav list.
- 2026-10-30 (QA regular round 2): Settings > Income: click the tag box, click blank space, Tab -> the tag Remove button; Shift+Tab -> the Employed toggle.
- 2026-10-30 (QA regular round 2): Settings > Income: New payable tag name/rate box, type text, Esc -> text cleared and focus parked (not on the section list).
- G13 (key never saved by accident): type a key, Tab to Model, type a model, Enter -> summary stays "No key"; blank click empties the key box and Save then stores nothing. Type a key + Save (no blank click) -> stored. Enter in the key box: state per side. Credential Manager entry names identical before/after.
- G13 (both sides): key typed, Tab to Model, blank click, press Save -> by rule stores the old key (key still visible as dots; user ruling pending whether Save-for-model should ignore it). Key typed, click Model box, Esc there -> key stays in key box unsaved, summary "No key". PasswordBox is unreadable via UIA: judge empty by pixel diff vs empty baseline + screenshot.
