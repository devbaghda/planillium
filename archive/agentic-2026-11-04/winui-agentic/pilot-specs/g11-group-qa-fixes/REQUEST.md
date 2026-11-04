# G11 — Group-QA fixes (2026-10-30)

Fix what the 2026-10-29 end-of-group QA found, plus one safety change for QA runs. Same list on both sides; each side checks every
item in its own app and fixes it where it occurs. If an item does not occur in your app, say so in your hand-off (not a failure).
Product terms only; how to fix is your call.

1. Ask Claude: a valid one-line compact reply from the AI must turn into a plan preview (Replace remaining and Add plan), inside the real
   running app, not just in unit tests. Today (regular) every such reply ends in "Something went wrong" with the log error
   "JsonSerializerOptions ... must specify a TypeInfoResolver" at CompactReply.Expand. Check agentic too (never exercised).
2. Reports (Week, Month, Year): in both income columns, the rows add up to the Total row to the cent. The Total is the sum of the displayed
   rows. (Regular showed Total off by 0.01 on Week/Month; agentic already matched.)
3. Diary tag filter: a paid tag that is also a tag is listed once, and has one "Mark all N <tag>" button (agentic and regular both listed
   "Studioshoo" twice).
4. Diary App filter: rows titled "- Telegram" or "– Telegram" (Telegram with no chat name) belong under "Telegram", not "Other".
   (Agentic put them in Other; check regular.)
5. Reports table at the default window 1180x780: every column header is fully readable (agentic shows "Of which pa"), or the table scrolls
   sideways. Check regular too.
6. Settings > AI monthly-limit box: (a) "-5" + Save says the value must be more than 0 (regular said "must be a number", agentic "cannot be
   negative" — use one wording, "must be more than 0"); (b) after a refused attempt, changing the text and leaving the box must not leave a
   message that no longer matches; (c) the spend line uses the same euro format as the rest of the app (€0,00) and refreshes after a
   successful AI call.
7. QA safety: a scratch run (MENTOR_ROOT set) must never read or write the real Windows Credential Manager, and must still let QA exercise
   Ask and Test connection against the local stub. Provide the scratch AI key through an in-memory value that a Debug scratch run can be
   given from the environment (e.g. one environment variable), so QA needs no typing and no credential-manager write. Regular's in-memory store
   exists already; agentic still stores the test credential name in the real Credential Manager — remove that.
