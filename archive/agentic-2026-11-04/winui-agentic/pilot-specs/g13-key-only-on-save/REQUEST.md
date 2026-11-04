# G13 - an abandoned API key must never be saved (QA finding, agentic G12, 2026-10-30)

Settings -> AI. Repro seen on the agentic side: type a key in the API key box, click blank page space (key stays in the box, summary "No key"), then type a Model or cap value and press Enter. The shared save also stores the key ("Key saved"). In the real app that writes a key the user abandoned to Windows Credential Manager.

Rule (PM, per QA's recommended fix; user informed): a key is stored only by a deliberate save of the KEY itself (Save button, or Enter inside the key box).
1. Enter (or leaving the box) in the Model box or the cap box saves ONLY that box. It never reads or stores the key box.
2. A blank-page click or Esc while the key box holds typed text discards the unsaved key (box empties, nothing stored), like Esc already does on the agentic side.
3. Check the same on THIS side first: if the defect does not exist here, say so with the evidence and change nothing except what rule 1-2 still needs.
Out of scope: where the cursor lands after Enter in cap/Model; the "too large" message lingering. Real data and the real Credential Manager are never touched (scratch runs only, in-memory store).
