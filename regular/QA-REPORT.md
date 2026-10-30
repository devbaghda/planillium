VERDICT: PASS WITH CAVEATS - workflow=regular feature=G13 key-only-on-save defectsQa=0

Build under test: b669eb8 + the uncommitted `regular/winui/Planillium.App/Pages/SettingsPage.xaml.cs` (G13 line in Page_PointerPressed), copied into isolated worktree `D:/CLAUDE/Planillium/.claude/worktrees/qa-g13-reg`, Debug. Scratch: new empty MENTOR_ROOT `scratchpad/mroot-g13`, MENTOR_INSTANCE_SUFFIX=verify-g13, no scratch key (in-memory store active: summary started "No key" although the real Credential Manager holds a key). Run 12:59-13:06 (about 7 of 25 min). One trial per action (nothing timing-dependent).

Caveat (needs a PM ruling, not a defect): ACCEPTANCE line 3's first variant ("click blank space, then click Save: the key IS stored") contradicts REQUEST rule 2 ("a blank click ... discards the unsaved key"). Regular follows rule 2: after a blank click, Save stores nothing. The deliberate path (type key, click Save) stores the key.

## Per-line results

| Line | Result | Evidence |
|---|---|---|
| 1 Key, blank, Model + Enter | PASS | Summary "No key - manual copy/paste" before and after; config `model` = claude-g13-model; screenshot shows key box empty (placeholder); Save probe afterwards kept "No key" (Save stores any typed key, see line 3), so the box was truly empty. |
| 2 Key, blank, cap + Enter | PASS | Summary stays "No key"; config `cap_eur` 5 -> 7; Save probe kept "No key". |
| 3 Deliberate save works | PASS (caveat above) | Type key + Save -> "A key is saved on this PC." / "Key saved · claude-g13-m2". Key + blank + Save -> "No key" (blank click emptied the box, per REQUEST rule 2). Enter in the key box: regular has no Enter-to-save (see siblings). |
| 4 Key, Esc | PASS | Summary "No key"; focus parked (IncomeFocusPark); typing 9 afterwards went nowhere; Save probe kept "No key". |
| 5 Leave key box by Tab / click other box | PASS | Regular behaviour: the typed key STAYS in the box unsaved (dots visible in screenshot), summary stays "No key"; it is stored only by Save. Tab -> Model, Model Enter -> model saved, still "No key". Click cap, cap Enter -> cap 8 saved, still "No key". A following blank click emptied the box (Save probe: "No key"). Never stored silently. |
| 6 Credential Manager count | PASS | `cmdkey /list` 57 Target lines before and after; sorted target names identical. |
| 7 Strict build, suite | PASS | `-warnaserror --no-incremental` Debug: 0 warnings, 0 errors (38 s); suite 229/229. |

## Sibling Enter paths (one trial each)
| Path | Observed |
|---|---|
| Enter in Model | Saves only the model ("Saved."), focus parks, key untouched (line 1, 5). |
| Enter in cap | Saves only the cap, focus parks, key untouched (line 2, 5). |
| Enter in key box | Nothing happens: focus stays in the key box, summary "No key", nothing stored. A blank click then empties it (Save probe "No key"). |

## qa-extra (Settings AI boxes)
| Case | Result | Evidence |
|---|---|---|
| 2026-10-29 cap -5 Save, message matches text after leaving | PASS | "The cap must be more than 0 ...", config unchanged (8); then 6 + Tab -> message gone, cap 6 saved. |
| 2026-10-30 Esc / blank click release cap, Model, key | PASS | Cap Esc: reverts to 6, parks, typed 1 goes nowhere. Cap blank: saves 4, parks. Model Esc: reverts, parks. Model blank: saves, parks. Key blank: parks. |
| 2026-10-30 r2 cap 6, blank, Shift+Tab -> Model | PASS | Focus -> AiModelBox. |
| 2026-10-27 B1h scratch must not show real "Key saved" | PASS (observed) | Scratch instance started "No key saved - Ask Claude is off." while the real store holds `anthropic_api_key@Planillium`. |

## Defects
None.

## Security
- Key never written to config.json (config `ai` held only model and cap_eur throughout).
- Real Credential Manager untouched: 57 = 57 entries, same target names; scratch store in memory only; fake keys only (`sk-ant-FAKE-g13-*`).
- mentor-winui.log has 0 ERROR lines; none of the typed fake keys (`sk-ant`) appears in the log or config.json.
- Abandon paths (blank click, Esc) discard the typed key; Tab / click-other-box keep it in the box but unsaved (visible as dots, summary says "No key").

## Untestable lines
None.

## Extra observations
- After Save with an empty key box the result line says "Saved." (it saved model/cap); fine, but it reads the same whether or not a key was stored. The summary line is the reliable signal.
- Tab / click-other-box leaves a typed key sitting in the box (regular's behaviour for line 5). Only a later blank click, Esc or Save resolves it; navigating away was not tested.
- The Good morning dialog arrived a few seconds after first launch, after the first dismissal pass; the script's focus assert caught it before any typing.

## New cases
- Settings > AI: type a key, Tab to Model, type a model, Enter -> summary stays "No key"; then a blank click -> key box empty (Save afterwards stores nothing).
- Settings > AI: type a key, press Enter in the key box -> nothing stored (regular: focus stays; state the side's behaviour).
- PM ruling needed: ACCEPTANCE G13 line 3 "blank click, then Save stores the key" vs REQUEST rule 2 "blank click discards the key" - pick one wording for both sides.

## Lessons
- In Git Bash, `cmdkey /list` is mangled into a bad switch; use `cmdkey //list`.
- cmdkey output order (and a "Local machine persistence" line) changes between calls: compare the sorted Target names, not a file hash.
- The Good morning dialog can appear seconds after the first dismissal pass: re-run the dialog sweep right before the first real click, and assert focus landed in the box before typing.
- A password box cannot be read through UIA; prove it is empty behaviourally (click Save: if the summary stays "No key", the box was empty), after first proving Save does store a typed key.
- A blank-click point taken as an offset from a box is fine when checked with ElementFromPoint first (here: SectionScroll pane).
