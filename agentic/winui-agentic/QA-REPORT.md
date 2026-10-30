VERDICT: PASS - workflow=agentic feature=G13 key-only-on-save defectsQa=0

Commit d1fd45c, QA worktree `.claude/worktrees/qa-ag` (detached at d1fd45c), Debug build, scratch MENTOR_ROOT (new, empty, no progress.db) with MENTOR_INSTANCE_SUFFIX=verify. In a scratch run the key store is in memory (AiKey.InMemory), so no test step could reach Credential Manager. Real mouse clicks and keys (SendInput), with a foreground check before every input. Only my own PIDs were killed. About 15 of the 25 minutes used.

How "box empty" was judged: the PasswordBox does not expose its text through UI Automation. I compared a crop of the box with an empty-box baseline (diff pixels: 0 means empty) and checked it by eye in the screenshots. A typed key shows as dots and gives about 470-520 diff pixels. After a save the box shows the "A key is saved - paste a new one to replace it" placeholder, which also differs from the baseline, so those cases were checked by eye. Evidence files: `qa/g13_run.out`, `qa/g13_l3a.out`, `qa/g13_shots/*.png`.

## Acceptance lines
| # | Line | Result | Evidence |
|---|------|--------|----------|
| 1 | Key typed, blank click, Model value + Enter: "No key", Model saved, key box empty | PASS | After the blank click: key box diff 0 (emptied), focus parked (Nav). Model `qa-model-l1` + Enter: status "Model saved.", config `ai.model=qa-model-l1`, summary "No key - copy and paste", Test/Remove buttons disabled, box shows the "sk-ant-..." placeholder (l1_after.png) |
| 2 | Same with cap + Enter | PASS | After the blank click: diff 0. Cap 7 + Enter: "Monthly limit saved.", config `monthly_cap_eur=7`, summary "No key", Test/Remove disabled |
| 3 | Save with no blank click stores the key; blank click then Save stores nothing; Enter in key box stores | PASS | Fresh launch: "No key", then key + Save gives "Key saved for this test run only (kept in memory, not in Windows Credential Manager).", summary "Key saved · claude-sonnet-5-5", Test/Remove enabled. Key, blank, Save: "Not saved: paste your API key first.", still "No key". **This side has Enter-in-key-box:** a key + Enter in the box stores it (same status, summary "Key saved · qa-model-l5"), and the box then shows the saved-key placeholder (l3c_after.png) |
| 4 | Key typed, Esc: box empty, nothing stored | PASS | Diff 516 while typed, 0 after Esc. Focus parked (Nav), "No key", config unchanged |
| 5 | Key typed, Tab or click on another box: nothing stored silently (state behaviour) | PASS | **Behaviour on this side:** Tab or a click on another box keeps the typed key in the box (dots visible) but does not store it. Key, Tab: focus on Model (diff 470). Model + Enter: "Model saved.", "No key". Then a click on the cap box, 9 + Enter: "Monthly limit saved.", "No key". Then a blank click: key still in the box, still "No key". A key followed by a click on the Model box and Esc there: key kept unsaved (diff 366), "No key" |
| 6 | Credential Manager entry count the same before and after the run | PASS | `cmdkey /list` before (taken before the first launch) and after (after both launches): 28 lines each, byte-identical (`diff` shows no difference) |
| 7 | Strict build 0 warnings; suite green | PASS | `dotnet build -p:Platform=x64 -c Debug -warnaserror --no-incremental`: 0 warnings, 0 errors. `dotnet test`: 362/362 passed |

## Sibling check (other AI-section Enter and leave paths, one trial each)
| Path | Result | Evidence |
|------|--------|----------|
| Model Enter with a key left in the box after Tab | PASS | Saves only the model; key not stored (L5) |
| Cap Enter with a key left in the box | PASS | Saves only the cap; key not stored (L5b) |
| Enter in the key box | PASS | Stores the key (L3c) |
| Key box, then a click on another box (Model), then Esc there | PASS | Key kept in the box, unsaved; "No key" |
| Blank click from the cap box while the key box (left earlier) still has text | PASS (by design) | The key stays in the box, unsaved. Only leaving the key box itself for a blank spot empties it (matches DECISIONS G13) |

## qa-extra.md lines
| Case | Result | Evidence |
|------|--------|----------|
| G13: key, Tab to Model, model, Enter -> "No key"; blank click empties and Save stores nothing; key + Save stores; Enter in key box per side; Credential Manager names the same | PASS | L5, L1/L3b, L3a, L3c and L6 above. This side: Enter in the key box stores the key |
| Other qa-extra lines (B1g/B1h money, Reports, Diary, AI-plan replies, regular-only items) | NOT RUN | Outside this brief's scope ("lines 1-7 plus sibling check"); G13 does not touch those areas (the diff changes only SettingsPage AI key/Enter handling) |

## Defects
None.

## Security
- During the run, every key went to the in-memory store, and the status line said so. The `cmdkey /list` output was identical before and after.
- Diff scan of d1fd45c: the new Enter error path logs only the exception (`Log.Error("SettingsPage.AiEnterSave", ex)`), never the key text. The key is not written to config (scratch config after the run had only `ai.model` / `ai.monthly_cap_eur`).
- I searched the scratch root for the typed keys (`sk-ant`): none were written to disk.
- No real data, config, DB or live app was touched. The user's own Planillium.App.exe (PID 7148) was running the whole time and was not touched.

## Untestable lines
None.

## Extra observations
- A key typed and then left by Tab or a click on another box stays in the box as dots indefinitely, even after later blank clicks from other boxes. A later Save stores it. This is the stated rule (deliberate Save stores), not a defect. Worth knowing: someone who walks away and later presses Save for a Model change also stores that old key, because Save is the shared button. If the PM wants Save-for-model to be safe too, that is a product decision.
- After Save (button) the status text is identical to the earlier one, so a second save in a row cannot be told apart from the first by the status alone. Judged here from a fresh launch.

## New cases
- Key typed, Tab to Model, blank click, then press Save: stores the old key (deliberate Save by rule). The PM should confirm that this is intended, because the Save button is shared with Model/limit edits.
- Key typed, click the Model box, Esc in the Model box: the key stays in the key box unsaved and the summary stays "No key".

## Lessons
- A PasswordBox's text is not readable through UIA: judge "empty" by a pixel diff of the box crop against an empty baseline, and confirm by eye. A saved-key placeholder also differs from the baseline, so read the screenshot for those.
- Run "stores the key" lines on a fresh launch before any other save; otherwise the status line already says "Key saved" and the line proves nothing.
- One script and one launch covered all seven lines plus the siblings in about 3 minutes: order the cases so each starts from "No key" and the storing cases run last.
- A Planillium.App.exe that is not yours may be running (the user's live app): kill only the PID you started, and never by image name.
