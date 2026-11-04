# SPEC G4 agentic -- items A1, A4, A5 (A3 out of scope: skipped by PLAN.md, question is with the PM)

All paths below are under `D:/CLAUDE/Planillium/agentic/winui-agentic/Planillium.App/` unless marked Tests.
Not changed: what the AI is asked to do (prompts, `PlanTemplates`), plan data handling, the "cut off" copy, Stop button, "Asking Claude... N s" counter.

## Memory rules applied
- LESSONS Layout: "force layout / test at 1, N, N+2 items" -> A5 height is independent of task count (prompt and reply boxes are fixed-height TextBoxes), so one default-size check plus one small-window check is enough; the notice/Reason line are the only variable-height rows.
- LESSONS "check hidden caps before specifying" -> A1: checked every cap on the path (below); the only caps are MaxTokens and the 120 s timeout. No length check in ParseReplacement/PlanReplyCheck/dialog code (grep for MaxLength / 4-digit limits found none).
- LESSONS "check siblings" -> A1 applies to both callers (one shared `AiAskControls` -> `AiClient.AskAsync`); A5's twin is Add plan (same `MaxHeight = 560`), A4's twin is both dialogs (one shared `AiNoticeDialog`).
- DECISIONS rule 16 / domain.md "AI layer" lines 152-153 state max_tokens 8000 and timeout 2 minutes: these docs must be updated with the change.

## A1 -- reply cap too low

Requirement: a rewrite of a 34-task plan (more than 8000 output tokens) must succeed in Replace remaining and in Add plan Ask. A cap that is hit still shows the existing "cut off" message, no crash.

Findings
- `Services/AiClient.cs:109` `public const int MaxTokens = 8000;` sent as `max_tokens` (line 148) for every real request; Test connection overrides to 16 (`AiSettings.TestConnectionMaxTokens`, line 68) -- untouched.
- Truncation: `AiClient.ParseReply` (line 217-219) returns `AiFailure.Truncated` when `stop_reason == "max_tokens"`; text from `AiMessages.For` line 45: "Claude's reply was cut off before it finished, so it was not used." `AiAskControls.AskAsync` (lines 216-220) prefixes "Couldn't get a reply -- " and appends the copy-prompt suffix. Keep all of it as is.
- Hidden second cap: `AiClient.DefaultTimeout = 120 s` (line 111), non-streaming. A reply of 10-16k tokens at roughly 50-80 tokens/s takes 150-300 s, so raising only the token cap would trade "cut off" for "took longer than 2 minutes". The ai-layer SPEC (line 157) already predicted this. Timeout must be raised with the cap.
- Model limits: default model `claude-sonnet-5-5` (`AiSettings.DefaultModel`); I could not verify its documented output ceiling from this side. Earlier Sonnet models allowed 64k output, so 16000 is well inside any plausible limit; a too-large `max_tokens` would come back as HTTP 400 -> "Anthropic could not accept this request -- it may be too long." (line 41), which would be misleading. The user can pick another model in Settings -> AI; 16000 is small enough for all current Claude models. Anthropic's SDKs also refuse non-streaming requests above about 21,000 max_tokens (10-minute rule); 16000 stays under that.

Chosen approach
1. `AiClient.MaxTokens` 8000 -> **16000** (two times headroom: a 34-task plan failed 8000, so it is at least 8-16k; a 60-task plan still fits). One constant, unchanged name, so `AiClientTests.cs:109` (`Assert.Equal(AiClient.MaxTokens, ...)`) keeps passing without edit.
2. `AiClient.DefaultTimeout` 120 s -> **360 s (6 minutes)**: 16000 tokens at 50 tokens/s = 320 s, so a full-cap reply still finishes. Stop stays available the whole time.
3. Make the two user-visible "2 minutes" strings follow the constant instead of repeating a number: add `AiClient.TimeoutMinutesText` (e.g. `"6 minutes"`, computed from `DefaultTimeout.TotalMinutes`) and use it in
   - `AiClient.cs:42` `AiFailure.Timeout` message: "Claude took longer than {6 minutes}, so the request was stopped."
   - `Dialogs/AiAskControls.cs:64` "Gives up after {6 minutes}." and the doc comment at line 103.
   - `Services/AiKey.cs:11` comment ("120 s limit") -> adjust wording.
   Note: `AiClient` is in a class declared after `AiMessages`; a static const/readonly in `AiClient` is accessible from `AiMessages` (same namespace) -- fine.
4. Docs in the same pass: `context/domain.md` lines 152-153 (max_tokens 16000, timeout 6 minutes), `DECISIONS.md` rule 16 if it quotes the numbers, `context/todos.md` dated entry, `CONTEXT.md` section 2 line, `CHANGELOG.md` (Unreleased) if present on this side. `pilot-specs/ai-layer/SPEC.md` is history, leave.

Rejected alternative: ask Claude for a shorter reply (fewer fields / terser JSON) -- forbidden by the brief ("do not change what the AI is asked to do"). Also rejected: streaming with an idle timeout (the ai-layer SPEC's own follow-up) -- larger change (new parse path, test stub changes) than the request needs; revisit only if 6 minutes proves too short. Also rejected: max_tokens 32000 -- breaks the SDK 10-minute rule, makes a runaway reply cost more and wait up to ~10 minutes.

Edge cases
- Reply exactly at the cap with `stop_reason: end_turn` -> accepted (unchanged).
- `stop_reason: max_tokens` -> Truncated message, reply box untouched, no crash (existing test `Reply_cut_off_at_max_tokens_is_refused_even_with_text`). Stub for QA acceptance 2: return `stop_reason: "max_tokens"` regardless of size.
- Stub reply > 8000 tokens (acceptance 1): the client never counts tokens, so a large `end_turn` body (34 tasks) passes through `ParseReply` and then `PlanReplacement.ParseReplacement` / `PlanReplyCheck.Problem`; no size limit exists there. The stub test with `DelayMs` must stay below 360 s.
- Timeout test (`Slow_reply_past_the_timeout...`) passes its own 300 ms timeout; unaffected.
- Settings test-connection is unaffected (16 tokens; same client constructor via `AiKey.CreateClient` uses `DefaultTimeout` -- a dead connection now waits 6 min instead of 2 for Test connection too; acceptable, Stop exists. Optional: the coder may pass a shorter timeout there; not required).
- The Stop-while-waiting behaviour and the live counter run for up to 360 s: counter text is `{int} s`, no width issue up to 3 digits.

## A4 -- Ask dialog acceptance text oversized

Meaning: the "acceptance text" is the one-time notice a user must Accept ("Before Planillium asks Claude ... Accept and continue / Decline"), `Dialogs/AiNoticeDialog.cs`, shown inline in both Replace remaining and Add plan (shared builder, so one change covers both). My reading of "acceptance text" is the notice; the brief does not define it further -- see Open question 1.

Current sizes: all `Body(...)` TextBlocks FontSize 13 (title and "What is sent" heading SemiBold 13; the "once" line 12).

Chosen approach (file `Dialogs/AiNoticeDialog.cs`)
- Body text (intro, bullet, keyLine): 13 -> **11.5**; headings ("Before Planillium asks Claude", "What is sent"): SemiBold 13 -> **12 SemiBold**; "once" line: 12 -> **11** (secondary colour). Change the `Body` default `size` from 13 to 11.5 and pass 12 for the two headings, 11 for `once`. Panel `Spacing` 8 -> 6.
- Keep text wording, `TextWrapping.Wrap`, automation names, button styles. Buttons unchanged.
- Readable: 11 px is the floor (WinUI Caption is 12; 11.5 body next to 12 headings stays legible on normal DPI). Unclipped: `TextWrapping.Wrap` with the panel stretched to the dialog width; no fixed heights, no MaxHeight -> nothing can clip. In Replace remaining the notice sits in the page `ScrollViewer`, so it cannot be cut at the bottom.
- Cheaper alternative if the user wants "visibly" more: 11 body / 11.5 headings. Coder uses the values above; QA judges against the old 13.

Rejected alternative: scale the whole notice with a `Viewbox` -- text would shrink with window width, no control over readability, blurry at fractional scale.

Edge cases: notice shown at the narrowest dialog width (4-5 wrapped lines of keyLine) still fits without horizontal scroll; Add plan bullet is the longest (ends with the "Format my own plan" sentence) -- must wrap, not clip; dark and light theme use theme brushes already.
Siblings checked: other 13 px strips in `AiAskControls` (progress 13, Reason 13, "Gives up" 12) are not the "acceptance text" and are not changed.

## A5 -- Replace remaining: no vertical scrollbar at default size

Findings (`Dialogs/ReplaceRemainingDialog.cs`)
- Step 1 content is `new ScrollViewer { Content = panel, MaxHeight = 560 }` (line 245). Panel height is independent of the task list (prompt box fixed 110, reply box 110, change box 72), roughly: summary 2 lines + caption + change box with header (~92) + caption + prompt with header (~130) + button row + caption + reply with header (~130) + words box with header (~56) + helper 2 lines + 10 px spacings = about 640-660 px. That exceeds 560, so the inner scrollbar always appears (matches the user's report). Also the ContentDialog template itself caps height at `ContentDialogMaxHeight` (default 756) minus title and buttons (~150), i.e. about 600, so raising only the 560 would move the scrollbar to the outer one.
- Width is `DialogWidth = 640` (line 19); the summary, the placeholder text and the words helper wrap to 2 lines at that width.
- Step 2 (preview, `ShowStep2Async`) has no ScrollViewer and is short; unchanged.

Chosen approach
1. Width: `DialogWidth` 640 -> **760** (`DialogContentWidth = DialogWidth - 64` follows; applied to `ContentDialogMaxWidth` at lines 248 and 352, so Step 2 widens too -- acceptable and consistent between steps). Fewer wrapped lines saves about 30 px.
2. Height: replace the fixed `MaxHeight = 560` with a window-aware value: `Math.Min(720, Math.Max(320, xamlRoot.Size.Height - 230))` (230 = title + button row + dialog margins). And set `dialog.Resources["ContentDialogMaxHeight"] = 900;` next to the existing MaxWidth override (line 248) so the template does not impose its own 756 cap. Result at default window size (window height normally >= 900): the full ~620 px panel fits, no scrollbar; `VerticalScrollBarVisibility` stays Auto so a small screen (window height under about 850) gets the scrollbar instead of the dialog running off screen.
3. Do not shrink the fixed boxes (prompt/reply 110, change 72) -- users read them.
4. Optional sibling (not requested, coder may skip unless trivial): Add plan has the same `MaxHeight = 560` (AddPlanDialog.cs:351); leave as is, mention in report.

Rejected alternative: remove the ScrollViewer altogether -- on a small screen the buttons would go off screen with no way to reach them. Also rejected: shrinking the three text boxes to fit under 560 -- cuts the visible text the user needs.

Edge cases
- Small screen (e.g. 1366x768, window about 700 DIP high): height clamp gives about 470; scrollbar appears, buttons stay visible. This is intended and is the only case where a scrollbar is allowed.
- Notice (first Ask ever), progress row and Reason line are extra rows (up to ~250 px) shown transiently; a scrollbar while the notice is showing is acceptable (after A4 the notice is shorter).
- Error text under the reply box adds a line; same rule.
- `xamlRoot.Size` must be read when the dialog is built (it is the XamlRoot already passed in); do not read it before layout in a Loaded handler.
- Typical task list does not change the panel height (verified from the code: lists live only inside text boxes).

## Files to change
1. `Services/AiClient.cs` -- `MaxTokens` 16000, `DefaultTimeout` 360 s, `TimeoutMinutesText`, Timeout message.
2. `Services/AiKey.cs` -- comment line 11 only.
3. `Dialogs/AiAskControls.cs` -- "Gives up after ..." text (line 64) and doc comment (line 103).
4. `Dialogs/AiNoticeDialog.cs` -- font sizes and spacing.
5. `Dialogs/ReplaceRemainingDialog.cs` -- `DialogWidth` 760, ScrollViewer MaxHeight formula, `ContentDialogMaxHeight` resource.
6. Docs: `context/domain.md` (152-153), `DECISIONS.md` rule 16 if numbers quoted, `context/todos.md`, `CONTEXT.md` section 2, `CHANGELOG.md` / `MANUAL.md` if they exist here and mention the numbers.
7. Tests (Tests project): none must change (`AiClientTests` uses `AiClient.MaxTokens`; no test asserts 8000 or "2 minutes"). Builders do not write tests; QA (deferred per brief addendum) adds the 34-task stub case.

## Open questions
1. A4 wording: I read "acceptance text" as the one-time Anthropic notice with the Accept and continue button. If the user meant another text (e.g. the "Asking Claude" progress block or the Reason line), tell the PM; the notice change is cheap to keep either way.
2. A1 timeout: visible wording changes from "2 minutes" to "6 minutes" (needed for a full-size reply to finish). The user's original ai-layer request said "about 2 minutes"; confirm 6 minutes is acceptable, otherwise a long plan will time out instead of being cut off.
3. Model output ceiling of `claude-sonnet-5-5` not verifiable from this side; 16000 is chosen to be safe for all current models.

## Acceptance (copied verbatim from ACCEPTANCE.md; item 3 is A3, out of scope for this pass)
1. A stub reply for a 34-task plan (over 8000 output tokens) is accepted in Replace remaining and in Add plan.
2. Truncation by the new cap still shows the clear "cut off" message, no crash.
3. The Plans page layout matches the regular build's Plans page look (you may read the regular build's running look
   via the user's reference screenshots only if provided in the brief; otherwise describe the layout from this
   side's own code and request the user's reference).
4. The Ask dialog's acceptance text is visibly smaller; still readable and unclipped.
5. Replace remaining dialog: no vertical scrollbar at its default size with a typical task list.
6. Build clean (/warnaserror); existing tests pass.

My additional check lines (labelled mine): (M1) a reply taking longer than 120 s but under 360 s no longer times out; (M2) the progress line and the Timeout message both say 6 minutes; (M3) in a window under about 850 px high the Replace remaining dialog keeps its buttons visible and scrolls instead of running off screen.
