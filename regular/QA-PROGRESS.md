# QA progress -- B1h esc-saved-boxes (regular), build 0ea247e, limit 15 min, start 11:14

## Plan
- Setup: worktree qa-b1h + strict Debug build (~2 min measured on B1d) + tests --no-build (~0.5 min). 3 min.
- C1 (lines 1-4, salary + Studioshoo): one script, one launch; per box 4 inputs (2500, empty, "abc"/spaces, -5) -> Esc, read box/cfg/focus/red, type 9. 1 trial each (Esc is a key handler, not timing-dependent). ~2 min run.
- C2 (line 5 regression, from diff: Cancelled wiring + OnKeyDown): same launch; Enter-save+park on both boxes, new-tag line Esc clears+leaves, Tab out of salary box, blank click out of tag box (edited). 1 trial each. ~1.5 min.
- C3 (line 6): build/tests result + B1d lines touched by OnKeyDown (Enter path covered in C2; minus message stays after Tab covered in C2). 0.5 min.
- Report: 3 min. Total ~10-11 min, fits 15.

## Progress
- 11:16 Setup: worktree qa-b1h at 0ea247e, strict Debug build 0 err (1m17s), tests 229/229. 2.5 min of 15.
- 11:17 C1 lines 1-4: 10 Esc checks (salary + Studioshoo x 2500/empty/abc/spaces/-5), 1 trial each (key handler, no timing), all revert to saved amount, cfg unchanged, focus IncomeFocusPark, type-9 unchanged, minus message gone. PASS. 3.5 min.
- 11:18 C2 line 5: 1 trial each (Enter-save+park x2, Esc after a save, new-tag Esc, Tab out, blank click out, B1d empty-Tab revert and -5 Tab). First run died on a cp1252 print, reran regression part only with UTF-8 output. PASS. 4.5 min.
- 11:20 C3 line 6: build/tests clean; B1d revert/minus paths re-seen in C2. PASS. 6 min. Side note: verify instance reads the real AI key from Credential Manager (observation O1).
