VERDICT: PASS - workflow=regular feature=b1h-esc-saved-boxes defectsQa=0

# QA report: B1h Esc in saved money boxes (regular side), 2026-10-27

Build tested: commit 0ea247e, in its own QA worktree (`.claude/worktrees/qa-b1h`, detached). Strict Debug build
(`-warnaserror --no-incremental`): 0 errors. With warnings treated as errors, 0 errors also means 0 warnings. `dotnet test`: 229/229 passed.
The app ran against a scratch MENTOR_ROOT holding the fixture config (salary 2925, Studioshoo 1600) and a `data/` folder with only
a `winui_state.json` that marks today's kickoff and review as done. No progress.db was copied. The run used
`MENTOR_INSTANCE_SUFFIX=verify` and `PLANILLIUM_AI_BASE_URL` set to a dead local port. QA started only its own exe and killed only that process.
All input was real: real keystrokes (`send_keys`) and real mouse clicks (`click_input`/`mouse.click`). After each check QA read the focused element and typed "9"
to confirm that nothing changed. Time used: about 7 of the 15 minutes.

## Per-line results

| # | Result | Evidence |
|---|---|---|
| 1 | PASS | Salary box: QA typed 2500 and pressed Esc. The box showed "2 925", config stayed 2925 and no "Saved." appeared. Focus went to `IncomeFocusPark`, and typing "9" changed nothing. Studioshoo box: same test, the box showed "1 600" and config stayed 1600 (`qa/b1h_lines1-4.out`, screenshot `qa/b1h_esc_tag.png`). |
| 2 | PASS | QA emptied each box and pressed Esc. Each box showed its saved amount again, with config unchanged, focus on the park target, and "9" changing nothing. |
| 3 | PASS | "abc" and three spaces, in both boxes: typing leaves the box empty. Esc put back the saved amount, config was unchanged, focus went to the park target, and "9" changed nothing. |
| 4 | PASS | "-5" in both boxes: the box showed "-5" with "Amounts can't be negative." on screen. Esc put back the saved amount and the message disappeared. Config was unchanged, focus went to the park target, and "9" changed nothing. |
| 5 | PASS | Salary 3000 + Enter saved 3000 and showed "Saved.", focus went to the park target, and "9" changed nothing. Studioshoo 1700 + Enter saved 1700 and parked the same way. After that save, typing 2500 + Esc showed "3 000", the new saved amount. New-tag line: "zz"/400 + Esc cleared both fields and added nothing. Focus left the line (to the Income section list item) and "9" changed nothing. Tab out of an edited salary box (2800) saved it and moved focus to the Employment toggle. A click on blank space after editing Studioshoo (1650) saved it and parked focus (`qa/b1h_reg.out`). |
| 6 | PASS | Build clean and 229/229 tests pass. B1d paths the diff could touch were seen again: an empty salary box + Tab went back to the saved amount, and "-5" + Tab stayed in the box with its message while config was unchanged. B1c (Enter-save) is covered by line 5. Scope came from the diff (MoneyBox.OnKeyDown Esc branch plus the two `Cancelled` hookups in SettingsPage), which is why the other B1-B1d lines were not re-run. |

## Defects
None.

## Security
- No real data was touched: scratch root only, the live Release exe was not running, and the fixture holds no secrets (only `ticktick.client_id`, which is empty).
- **O1 (outside the checklist, not caused by B1h):** the QA instance shows "AI: Key saved" and "TickTick: Connected".
  The AI key comes from Windows Credential Manager (`AiSettings.HasKey` -> `CredentialStore.Read`), which neither
  MENTOR_ROOT nor the instance suffix separates. Why it matters: a QA or test run that triggers an AI call would send
  the user's **real key** to whatever `PLANILLIUM_AI_BASE_URL` points at (the local stub, which would then receive the key), or to the real
  API if the variable is missing. Fix: the `verify` instance (or a scratch root) should use its own credential name,
  or none at all. Then a test instance cannot reach the real key. Not exercised here because no AI call was made.

## Untestable lines
None.

## Extra observations
- The new-tag line's Esc moves focus to the Income section list item, not to the park target. This did not change in B1h, and "9" there is harmless.
- The first script run crashed while printing a thin space to the cp1252 console. The regression part was re-run with UTF-8 output, and the Esc results come from the first run's log.

## New cases
- Saved money box (salary or tag): save a new amount with Enter, then type a different number and press Esc. Expected: the box shows the newly saved amount, not the one loaded at startup.
- QA/verify instance with scratch MENTOR_ROOT: open Settings. Expected: the AI row does not show "Key saved" from the user's real Credential Manager entry (O1; this needs a product decision before it becomes an acceptance line).

## Lessons
- Run QA scripts with `PYTHONIOENCODING=utf-8`: the app's numbers contain a thin space (U+2009), and printing one to the cp1252 console kills the script partway through.
- A scratch MENTOR_ROOT does not separate everything. Secrets in Windows Credential Manager (AI key, possibly TickTick) are shared with the live app. Check what the Settings summary shows before assuming the test instance is clean.
- Reusing `b1d_h.py` by setting its module globals (`H.ROOT`, `H.EXE`, ...) worked and saved writing a new harness.
