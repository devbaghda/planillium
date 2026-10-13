# SPEC — ai-layer (agentic side)

**This feature has screens** (Settings -> new "AI" section; an "Ask Claude" action with progress/cancel inside two
existing dialogs; a one-time notice dialog). The Designer adds a **Design** section below.

## Requirement

Today "Replace remaining tasks" and "Add plan" make the user copy a generated prompt into claude.ai and paste the
reply back. Add an optional AI layer: the user stores their own Anthropic API key once (Settings -> AI), and in those
two dialogs an "Ask Claude" action sends the exact prompt shown, shows progress with a Cancel, and on success puts the
reply into the existing reply box, where it passes through the same validation and the same preview/confirm as a
pasted reply. With no key, or on any failure, the manual copy/paste screen stays exactly as it is, plus a plain reason.
Nothing is changed by the AI call itself. Only the prompt text is ever sent.

## Chosen approach

1. **New plain-C# service `AiClient`** (no WinUI types, so the test project can source-link it). One method:
   `Task<AiResult> AskAsync(string prompt, CancellationToken ct)`; `AiResult` = success text, or a failure with a
   category enum (`NoKey, Rejected401, ModelNotFound404, RateLimited429, ServerError5xx, BadRequest400, Timeout,
   Network, Cancelled, Truncated, EmptyReply, Unreadable`) and a plain-language message built from the category only.
   Constructor takes the `HttpClient`/handler, base URL, model, key provider (`Func<string?>`) and timeout, so tests
   inject a stub. No WinUI, no `CredentialStore` inside the client class (that is P/Invoke on the real vault).
2. **Wire format**: `POST {base}/v1/messages`, headers `x-api-key`, `anthropic-version: 2023-06-01`,
   `content-type: application/json`; body exactly `{model, max_tokens, messages:[{role:"user", content:<prompt>}]}` —
   no system prompt, no metadata, nothing else. Non-streaming. `max_tokens` 8000 (one constant). Reply text = the
   concatenation of the `text` blocks of `content`. `stop_reason == "max_tokens"` -> `Truncated` (never hand a cut-off
   JSON to the parser as if complete). Overall timeout 120 s via a linked `CancellationTokenSource`; user cancel
   distinguished from timeout.
3. **Key storage**: Windows Credential Manager via the existing `CredentialStore` (`Write`/`Read`/`Delete`), key name
   `anthropic_api_key` (user decision; same vault the TickTick token uses). Never config.json, state, DB, plans, logs.
4. **Model + notice flag**: model name in config.json under `ai.model` (not secret; default constant = current
   Sonnet, `claude-sonnet-5-5`, defined once; blank field falls back to it). The "notice accepted" flag is
   `ai_notice_accepted` (bool, default false) on `AppState` in state.json, next to `name_asked`.
5. **Test hook**: environment variable `PLANILLIUM_AI_BASE_URL` overrides the base URL **only in
   `#if DEBUG || PLANILLIUM_TESTS` builds**, exactly like `MENTOR_ROOT` in `AppPaths`; Release ignores it and always uses
   `https://api.anthropic.com`. (Otherwise anyone able to set an env var could redirect the key.) When the override is
   active, the key is read/written/deleted under a **different credential name** (`anthropic_api_key_test`), so QA's
   fake keys never overwrite, read or delete the user's real key in Credential Manager.
6. **Dialog flow, both dialogs**: "Ask Claude" runs: no key -> not offered, hint line "No API key — add one in
   Settings -> AI, or copy the prompt to claude.ai as before."; key present and notice not accepted -> notice dialog
   (Accept stores the flag and continues; Decline sends nothing and returns); then call with progress + Cancel.
   Success -> reply text goes into the existing reply `TextBox`.
   - *Replace remaining*: then run the **same** `PlanReplacement.ParseReplacement` path the Preview button runs, and if
     valid continue to Step 2 (the preview); if invalid show the same error with the reply left in the box (manual
     screen). The prompt sent is `BuildPrompt(situation, change.Text)` — byte-for-byte what the prompt box shows.
   - *Add plan*: the prompt sent is the generated prompt (same validation as "Generate prompt": required fields, own
     plan text in Reformat mode). On success fill the reply box and stop; the user presses the existing **Import plan**
     (that is the confirm; `TryImport` unchanged). **No auto-import.**
   - Failure/cancel/timeout: plain reason shown (separate from the red reply-validation error line so editing the reply
     does not hide it), manual screen fully usable, plan files untouched. Closing the dialog during a call cancels it.
7. **Logging**: log only category + HTTP status number (e.g. `AiClient: 401`). Never the key, request body, response
   body, headers or `ex.Message` of HTTP exceptions' bodies. The key is only ever passed to the request header.
8. **Test connection** sends the fixed prompt `Reply with the word OK.` with `max_tokens` 16 through the same client
   (so it also validates the model name). It contains no app data, so it does **not** require the one-time notice.

**Rejected alternatives**
- Key in config.json / DPAPI-encrypted file: user decision is Credential Manager; also keeps `CredentialStore` the one
  secret path.
- Official Anthropic .NET SDK: new dependency, lock-file/CVE churn (this repo pins and audits packages), and a plain
  `HttpClient` call is ~100 lines and fully stub-testable.
- Streaming: more robust for long replies but more code and a harder stub; see Open question 1.
- Auto-import for Add plan on success: would apply a change without the confirm step AC15 requires.
- Honouring `PLANILLIUM_AI_BASE_URL` in Release: key-redirection risk for no benefit.

## Exact files to change

- `Planillium.App/Services/AiClient.cs` — **new**: client, `AiResult`/category enum, plain-language messages, `AiSettings` helpers (default model constant, base-URL resolution with the DEBUG/TESTS gate, credential-name selection).
- `Planillium.App/Services/AiKey.cs` — **new** (thin): `Has()`, `Save(string)`, `Remove()`, `Read()` over `CredentialStore` using the selected credential name; trims whitespace/newlines on save.
- `Planillium.App/Services/ConfigService.cs` — add `AiModel` getter (reads `ai.model`, blank -> default) and a setter via `Mutate`.
- `Planillium.App/Services/StateService.cs` — add `ai_notice_accepted` bool to `AppState`.
- `Planillium.App/Pages/SettingsPage.xaml` — new "AI" `ListViewItem` and `AIPanel` (masked `PasswordBox`, model `TextBox`, Save, Remove key, Test connection, status line).
- `Planillium.App/Pages/SettingsPage.xaml.cs` — add `AIPanel` to `_panels` (the menu/panel-count guard must still hold), handlers for Save / Remove / Test.
- `Planillium.App/Dialogs/AiNoticeDialog.cs` — **new**: the one-time "what is sent" notice (Accept / Decline).
- `Planillium.App/Dialogs/AiAskControls.cs` — **new** (shared helper): builds the Ask Claude button + progress + Stop + reason line and runs notice -> call -> result, so both dialogs share one implementation (no duplicated flow).
- `Planillium.App/Dialogs/ReplaceRemainingDialog.cs` — add Ask Claude to Step 1; on success run the existing parse and proceed.
- `Planillium.App/Dialogs/AddPlanDialog.cs` — add Ask Claude beside Generate prompt; on success fill the reply box.
- `Planillium.App.Tests/Planillium.App.Tests.csproj` — source-link `AiClient.cs` (and `AiKey.cs` only if it compiles without WinUI; it should).
- `Planillium.App.Tests/AiClientTests.cs` — **new**: client against a stub (see AC17 below).
- `context/domain.md`, `context/todos.md`, `CONTEXT.md`, `DECISIONS.md` — agentic-side docs: new config key `ai.model`, state flag, key location, the "no secrets / test credential slot" rule.
- Not changed: DB schema (no migration), plan JSON format, `PlanReplacement`, `TryImport`, scoring.

## Edge cases the build must survive

- Key present but dialog opened with no network / DNS fail -> `Network` message, manual screen.
- Key with pasted trailing newline/spaces -> trimmed before saving; empty/whitespace Save -> refused with a message, no write.
- Model blank or whitespace in Settings -> default model used; bad model name -> 404 -> "model not found, check Settings -> AI".
- Reply is valid JSON wrapped in prose or fences (normal) -> existing parsers cope; reply is prose only / truncated / missing `phases` -> same rejection text as a paste, reply left visible and editable.
- `stop_reason: max_tokens`, empty `content`, non-JSON HTTP body, 200 with error shape -> categories above, never an exception to the user.
- Cancel pressed at the same moment the reply arrives -> exactly one outcome; if cancelled, nothing is filled.
- User edits "What should change?" (or Add-plan fields) while a call is in flight -> the in-flight call used the prompt captured at click time; the result still fills the reply, but the dialog shows the current prompt as-is (do not silently re-send). Simplest acceptable: disable those inputs during the call.
- Dialog closed (Cancel/Esc) mid-call -> token cancelled, no later write, no exception on a dead dialog.
- "Back" from Step 2 (Replace remaining v2) keeps the AI-filled reply in the box as it does a pasted one.
- Second click on Ask Claude while a call runs -> ignored (button disabled).
- Date-sensitive logic: the AI call itself touches no dates; Replace remaining's Step 2 re-assesses at confirm as today (no change).
- Key removed in Settings while a dialog is open -> next Ask Claude re-checks the vault and shows the no-key hint.
- Two stub runs in parallel on one machine: credential slot `anthropic_api_key_test` is shared; QA runs them serially.
- HttpClient reuse: one shared instance (as TickTick does), not one per click.

## Acceptance criteria (copied verbatim from ACCEPTANCE.md)

```
# Acceptance — AI layer (run only against a scratch MENTOR_ROOT; AI calls go to a local stub server, never the
# real API; the stub address is set by environment variable PLANILLIUM_AI_BASE_URL for testing)

1. Settings has an "AI" section: masked API key field, model field (default a current Sonnet id), Save, Remove key,
   Test connection.
2. After Save, the key is not found in plain text in config.json, progress.db, plans/, logs or the repo tree.
3. Remove key deletes the stored secret; the app behaves as "no key" afterwards.
4. Test connection reports success against a stub that returns 200, and a readable error for 401, 429, 500 and timeout.
5. Replace remaining, with key: an "Ask Claude" action sends the prompt and, on success, fills the reply and moves
   to the same preview as a pasted reply.
6. Replace remaining, no key: the manual copy/paste screen shows, with a hint pointing to Settings -> AI.
7. Add plan, with key and no key: same two behaviours as lines 5 and 6.
8. Stub returns 401 / 500 / garbage / non-JSON text: dialog shows a plain-language reason and the manual screen;
   plan file byte-identical.
9. Cancel during a slow call: call stops, nothing changes, dialog usable.
10. A call exceeding the timeout ends with a clear message; nothing changes.
11. The first send shows the one-time notice stating what is sent; Decline sends nothing; Accept is remembered
    (not asked again).
12. The request body the stub receives contains the prompt and nothing else from the app's data: no diary rows,
    scores, earnings, tags, config values or the API key beyond the auth header.
13. The key is never written to the log, even on errors.
14. Reply from the stub with a bad shape (missing phases) is rejected by the same validation as a manual paste.
15. Confirm at the preview applies the change exactly as a pasted reply would (identical plan file result for
    identical reply text).
16. All acceptance lines of replace-remaining-tasks and replace-remaining-v2 still pass.
17. Build clean (0 warnings with /warnaserror); existing tests pass; new tests cover the AI client against the stub.
```

**Planner additions (labelled mine, not from ACCEPTANCE.md):**

- P1. The key never appears in `state.json` or in the exported data (Settings -> Data export), and not in the crash/exception text shown to the user.
- P2. With the test override active the app uses credential `anthropic_api_key_test`; QA confirms the user's real `anthropic_api_key@Planillium` entry (if any) is untouched before/after the run. In a build without DEBUG/TESTS, `PLANILLIUM_AI_BASE_URL` is ignored.
- P3. Request: exactly one POST to `/v1/messages`, body keys limited to `model`, `max_tokens`, `messages`; headers `x-api-key` and `anthropic-version` present; the single message content equals the prompt box text exactly (Replace remaining: including the change text; Add plan: including Reformat's own plan text).
- P4. `stop_reason: max_tokens` stub reply -> readable "reply was cut off" message, manual screen, nothing changed.
- P5. Add plan, AI success: nothing is imported until the user presses Import plan (plans/active unchanged before that).
- P6. Decline at the notice leaves the flag false; the next Ask Claude asks again. Accept persists across app restart. Test connection works without ever showing the notice.
- P7. Save with empty key refused; after a successful Save the PasswordBox is cleared and the section only states "A key is saved", never the key or part of it.
- P8. Closing a dialog during a slow call cancels it (stub sees the connection drop or no further effect); no plan file or DB change afterward.
- P9. Unit tests cover: request shape (P3), each status-code category, timeout vs user-cancel, truncated, empty content, non-JSON body, text-block concatenation, key not present in any logged line (capture the log file in the test root).

## Safety notes (for QA)

- Scratch `MENTOR_ROOT` only; never the real `data/`, `config.json`, `plans/`, or the live running instance. Build Debug (the override is compiled out of Release).
- AI calls go **only** to the local stub (`PLANILLIUM_AI_BASE_URL`, loopback). Never the real Anthropic API, never a real key. Use obviously fake keys (e.g. `sk-ant-FAKE-test-0001`) and search all scratch files for that exact string for AC2/AC13.
- Credential Manager is **not** scratch: it is the real user's vault. Only the test slot (`anthropic_api_key_test@Planillium`) may be created/deleted, and QA removes it at the end. Do not touch `anthropic_api_key@Planillium` or any TickTick entry.
- Do not edit the real `config.json`; the `ai.model` key is written to the scratch root's config.json only.
- Run one app instance at a time; the regular app and the real DB are out of bounds.

## Open question

1. **Reply length vs 2-minute timeout.** Non-streaming, a full plan (Add plan) can take well over a minute at 8000 tokens; a 120 s hard cap could time out on legitimate long plans. Spec keeps the user's "about 2 minutes" non-streaming; if real use shows timeouts, the follow-up is streaming with a no-data (idle) timeout instead of a total timeout. Needs a user call only if they want streaming now.
2. **Default model id.** Spec uses `claude-sonnet-5-5` as the "current Sonnet" default; the user can change it in Settings. Confirm it is the id they want shipped as default.
3. **Does Remove key reset the notice acceptance?** Spec says no (acceptance is about what is sent, not the key). Confirm if the user would prefer re-asking after a key is removed and re-added.
4. **Add plan / Reformat mode** sends the user's own pasted plan text (inside the prompt). The notice must say so explicitly; no product decision needed, flagged for the Designer's copy.

---

# Design

Written by the Designer. Tool used: `ui-ux-pro-max` (product UI, WinUI 3 desktop). Its relevant rules applied: visible
labels not placeholder-only, errors next to the thing that caused them, loading feedback on any wait over ~1 s,
destructive actions confirmed, 44-DIP-class hit targets via the platform's default button sizes, state never by
colour alone, reduced motion respected by using only the platform's own indeterminate bar. The existing app's look is
followed, not reinvented: stock WinUI controls, theme brushes only (`TextFillColorSecondaryBrush`,
`TextFillColorTertiaryBrush`, `SystemFillColorCriticalBrush`, `AccentButtonStyle`), the existing 640 / 576 dialog
widths, 12 px helper text, 13 px body text, `Spacing=10` stacks in dialogs and `Spacing=12/16` in Settings panels.
**Colour budget: the system accent (primary buttons only) + the existing critical red (failure lines only) + the
existing neutral greys. No new hue, no green for success** (success is a words-plus-check-mark line in the neutral
primary text colour, like the existing "Copied ✓").

All copy below is exact. Use ASCII `->` exactly as written in "Settings -> AI" everywhere (QA greps for it); use the
em dash and the check mark `✓` exactly as written.

## D1. Settings -> AI section

### D1.1 Menu entry
- New `ListViewItem` placed **between "TickTick" and "Data"** (Data stays last). Same template as its siblings:
  `HorizontalContentAlignment="Stretch"`, `AutomationProperties.Name="AI"`, title `AI` (SemiBold 13), summary line
  (11 px, tertiary, max 2 lines, ellipsis) in a named `SumAI` TextBlock.
- Summary text: no key saved -> `No key — copy and paste`; key saved -> `Key saved · ` + model name actually in
  use (the default if the field is blank). The summary refreshes after Save, Remove and on page open.
- The menu now has 9 items; the "N of 9" panel-count guard must hold. The entry opens `AIPanel` (nothing greyed).

### D1.2 Panel layout (`AIPanel`, `StackPanel Spacing="12" VerticalAlignment="Top"`, hidden by default like its siblings)
Top to bottom, left-aligned, all in this order (do not reorder, do not add elements):

1. `AI` — `SubtitleTextBlockStyle`.
2. Explainer, 12 px, secondary brush, wrapping:
   `Optional. With your own Anthropic API key, "Ask Claude" in Replace remaining tasks and Add plan sends the prompt shown there to Anthropic and fills in Claude's reply for you. Without a key you copy the prompt to claude.ai and paste the reply back, as before. The key is stored in Windows Credential Manager for your Windows user — never in your plan files, database, settings file or logs.`
3. `PasswordBox` — Header `Anthropic API key`, `Width="360"`, `HorizontalAlignment="Left"`,
   `PasswordRevealMode="Hidden"` (no peek/reveal button: the key is never displayed back). `AutomationProperties.Name="Anthropic API key"`.
   Placeholder: no key saved -> `sk-ant-…`; key saved -> `A key is saved — paste a new one to replace it`.
4. `TextBox` — Header `Model`, `Width="260"`, `HorizontalAlignment="Left"`, placeholder = the default model id
   (`claude-sonnet-5-5`, taken from the one constant, not retyped). Shows the stored value, or empty (placeholder visible)
   when the user never set one. `AutomationProperties.Name="Model"`.
5. Helper under the model box, 12 px, secondary: `Leave empty to use the default (claude-sonnet-5-5). Change it here if Anthropic retires this model — no app update needed.` (default id from the constant).
6. Button row: a `Grid`, `ColumnSpacing="10"`, three `*` columns each `MaxWidth="170"`, buttons
   `HorizontalAlignment="Stretch"` (same technique and reason as the Data panel: a horizontal StackPanel would drop
   the last button off a narrow window). Left to right:
   - `Save` — `AccentButtonStyle` (the only accent control on the panel). `AutomationProperties.Name="Save"`.
   - `Test connection` — default style.
   - `Remove key…` — **default style, not DangerButtonStyle** (it deletes only a key the user can re-enter; red is
     kept for data loss). Trailing ellipsis because it opens a confirm.
7. Progress row (collapsed unless a test runs): indeterminate `ProgressBar` (stretch, max width 360) + `Stop` button
   to its right. See D1.4.
8. Status line `AIStatus` — one `TextBlock`, wrapping, 13 px, **always present and its height reserved for one line**
   (`MinHeight` of one 13 px line, empty text when idle) so the panel does not jump when a message appears.
   `AutomationProperties.LiveSetting="Polite"` so screen readers announce results. Neutral states use the primary
   text brush; failures use `SystemFillColorCriticalBrush` **and** start with the words `Not saved:` / `Not connected:`.
9. Footer note, 12 px, tertiary: `Test connection sends only the words "Reply with the word OK." — no plan or diary data.`

Panel never shows a "Clear" or "Show key" control. The Save button is the only commit point (no commit-on-blur here:
saving a secret and a model together should be one deliberate act; this is the one place that departs from
"edit in place", and the departure is deliberate — say nothing about it on screen).

### D1.3 States and behaviour of the controls

| State | Key box | Buttons | Status line |
|---|---|---|---|
| Opened, no key saved | empty, placeholder `sk-ant-…` | Save enabled, Test connection **disabled**, Remove key… **disabled** | empty |
| Opened, key saved | empty, placeholder `A key is saved — paste a new one to replace it` | all three enabled | `A key is saved on this computer.` |
| Save, key box empty, no key saved | unchanged | — | `Not saved: paste your API key first.` (critical), nothing written |
| Save, key box empty, key already saved | unchanged | — | model written; `Model saved.` (if the model field changed) else `Nothing to save.` |
| Save with key text | box cleared immediately | Test connection and Remove key… become enabled | `✓ Key saved. It is stored in Windows Credential Manager and is not shown again.` Trim whitespace/newlines before writing. |
| Save fails (vault write error) | box keeps its text | — | `Not saved: Windows could not store the key. Try again.` (critical) |
| Unsaved edits present (key box non-empty, or model text differs from stored) and the user presses Test connection | — | — | `Press Save first — Test connection uses the saved key and model.` (neutral) and **no request is made** |
| Test connection running | box + model read-only | Save, Test connection, Remove key… disabled; progress row visible | `Asking Claude… 12 s` (counter updates each second; counter text is not a live region) |
| Test OK | — | re-enabled | `✓ Connected. Claude answered using <model in use>.` |
| Test fails | — | re-enabled | `Not connected: ` + the category message from D4 (without the dialog-only fallback sentence) |
| Test stopped by user | — | re-enabled | `Stopped. Nothing was changed.` (neutral) |

- Status line text from a previous action is replaced by the next action's text; it clears when leaving the section.
- Keyboard: Enter inside the key box or model box = Save. Tab order: key, model, Save, Test connection, Remove key…, (Stop when visible).
- Focus after Save success: stays on Save. After a failure: stays on the button used.

### D1.4 Progress (Test connection)
Row appears directly below the button row: `ProgressBar IsIndeterminate="True"` (platform animation; no custom motion)
and a `Stop` button (default style). The status line carries the text `Asking Claude… N s` (N = whole elapsed seconds).
Stop cancels the call, status becomes `Stopped. Nothing was changed.`. The 120 s timeout ends with the Timeout message.
Row and its reserved space collapse when idle; because the status line already reserves one line, the only layout
change is the row itself appearing under the buttons (acceptable: it is a transient state with its own label).

### D1.5 Remove key confirm
`Remove key…` opens `ContentDialog` (built with `DialogControls.Build`):
title `Remove the saved API key?`; body `"Ask Claude" will no longer be offered until you save a key again. Your plans and other settings are not changed.`;
primary `Remove key`, close `Cancel`, **default button = Cancel** (same rule as Replace remaining's Step 2).
After removal: key box placeholder returns to `sk-ant-…`, Test connection and Remove key… disable, summary line
`No key — copy and paste`, status `Key removed.`. The one-time notice acceptance is **not** reset (Open question 3).

## D2. "Ask Claude" strip (both dialogs; one shared builder in `AiAskControls`)

The strip is one block with three parts, built once and inserted at a fixed slot in each dialog. It never changes the
dialog's width (still 640 outer / 576 content) and the dialog's existing `ScrollViewer MaxHeight=560` is kept.

### D2.1 Parts
1. **Action row** — holds the Ask Claude button (see per-dialog placement).
   - Label `Ask Claude`; `AccentButtonStyle` **only if** the dialog's primary button is not the nearest accent action
     — to keep a single accent per screen the dialog's own primary button (Preview / Import plan) stays accent and
     **Ask Claude is a default-style button**. (Do not make two accent buttons.) `AutomationProperties.Name="Ask Claude"`.
   - Label becomes `Ask again` once the reply box is non-empty.
2. **Progress row** (collapsed unless a call runs): indeterminate `ProgressBar` (stretch within the 576 content width,
   minus the Stop button) + text `Asking Claude… N s` (13 px, primary) + under it, 12 px secondary, `Gives up after 2 minutes.` + `Stop` button on the right (default style, `AutomationProperties.Name="Stop"`).
3. **Reason line** `AiReason` — one wrapping TextBlock, 13 px, **separate from the red reply-validation `error`
   TextBlock** (never shares it, so editing the reply cannot hide it). Collapsed when empty. Failures use
   `SystemFillColorCriticalBrush` and always start with `Couldn't get a reply — ` so the meaning never rests on colour.
   Neutral messages use the secondary brush. It is cleared when the next call starts, and when the dialog's inputs
   that form the prompt change (so a stale reason never sits beside a new prompt).

### D2.2 Placement (exact)
- **Replace remaining tasks, Step 1** order: summary; caption 1; "What should change?"; caption 2; "Prompt for
  Claude"; **row: `Copy` and `Ask Claude` side by side, 8 px apart, both left-aligned**; **progress row**; **reason
  line / no-key hint**; caption 3; "Claude's reply"; "Words that must be gone"; its helper; the existing red `error`.
- **Add plan** order: unchanged down to the field boxes; `genRow` becomes `Generate prompt`, `Copy`, `Ask Claude`
  (same 8 px spacing, left-aligned); then "Generated prompt" box; **progress row**; **reason line / no-key hint**;
  "Claude's reply"; the existing red `error`. (Strip sits between the prompt and the reply it fills.)
- Queue-only mode of Add plan ("Save Plan Idea"): Ask Claude is offered exactly as in normal mode (the reply is the same
  JSON); its primary button remains `Save idea`.

### D2.3 Captions (Replace remaining only — it has numbered steps)
- **No key:** captions unchanged: `2. Copy this prompt into claude.ai.` / `3. Paste Claude's reply below.` — the
  manual screen is byte-identical to today apart from the hint line.
- **Key saved:** caption 2 becomes `2. Ask Claude here, or copy this prompt into claude.ai.`; caption 3 stays.

### D2.4 No-key hint (replaces the Ask Claude button's slot; button is **not drawn**, not greyed)
Secondary brush, 12 px, in the reason-line slot, exactly:
`No API key — add one in Settings -> AI, or copy the prompt to claude.ai as before.`
Checked each time Ask Claude would be shown and again on click (if the key was removed while the dialog was open, the
button disappears at that click and the hint appears; no request is made). Wording is not a link (dialogs are modal; do
not add navigation).

### D2.5 States (both dialogs)

| State | Ask Claude | Prompt/inputs | Reply box | Dialog primary button | Strip shows |
|---|---|---|---|---|---|
| Idle, key saved | enabled | editable | editable | enabled | nothing (reason line empty) |
| Idle, no key | not drawn | editable | editable | enabled | no-key hint |
| Notice pending | disabled | — | — | — | notice dialog on top (D3) |
| Notice declined | enabled again | unchanged | unchanged | enabled | neutral: `Nothing was sent.` |
| Running | disabled (label stays) | **change/words-affecting inputs read-only** (Replace: "What should change?"; Add plan: mode, field 1–3, own plan text, Load from file, Generate prompt) — `Copy` stays enabled | `IsReadOnly=true` | **disabled** (so a half-arrived reply cannot be submitted) | progress row |
| Success, Replace remaining | — | — | filled | — | none; the dialog then runs the existing Preview check at once |
| Success, Replace, reply not valid | enabled, label `Ask again` | editable | filled, editable | enabled | neutral: `Claude replied, but the reply isn't usable yet — see the message below. You can edit it, or ask again.` and the existing red `error` shows `ParseReplacement`'s own text under the reply box |
| Success, Add plan | enabled, label `Ask again` | editable | filled, editable | enabled (`Import plan`) | neutral: `✓ Claude's reply is in the box below. Nothing is added until you press Import plan.` |
| Failure | enabled, label unchanged | editable | **left exactly as it was** | enabled | critical reason, D4 |
| Stopped by user | enabled | editable | unchanged | enabled | neutral: `Stopped. Nothing was changed.` |

Focus: opening the dialog is unchanged (Replace focuses "What should change?"). After any end of a call (success,
failure, stop) focus returns to the Ask Claude button. When Success moves Replace remaining to Step 2, the existing
Step 2 rules (default = Cancel) apply untouched.

`Ask again` replaces the reply box content with the new reply without asking (the previous reply is regenerable; see
Open question 5). A pasted reply the user typed in is overwritten the same way.

Edit-while-running is prevented rather than handled: those inputs are read-only for the duration (spec edge case,
"simplest acceptable"), and the prompt box shows exactly what was sent.

Closing the dialog (Cancel / Esc / X) while running cancels the call silently: no message, no write.

## D3. One-time notice (`AiNoticeDialog`)

A `ContentDialog` via `DialogControls.Build`, shown on top of the Ask Claude dialog the first time a key is present
and the user presses Ask Claude (never from Test connection). Width 640 like the other dialogs; content stack
`Spacing="10"`, body 13 px primary, bullet headings SemiBold.

- Title: `Before Planillium asks Claude`
- Paragraph 1: `Planillium will send the prompt you can see in this window to Anthropic, the company behind Claude, using your own API key. Nothing else leaves your computer — no diary, scores, earnings, tags or settings.`
- Heading `What is sent`
- Bullet (Replace remaining tasks): `Replace remaining tasks: the plan's name, the titles and details of its tasks, and what you typed in "What should change?".`
- Bullet (Add plan): `Add plan: the fields you filled in. If you use "Format my own plan", that includes your own plan text in full — everything you pasted or loaded from a file.`
- Paragraph 2: `Your key is only used to sign the request. What you send is handled under Anthropic's own terms and privacy policy. Claude's reply changes nothing in Planillium until you confirm it yourself.`
- Paragraph 3 (12 px, secondary): `You will only be asked this once. You can always use the copy-and-paste way instead.`
- Buttons: primary `Accept and continue`, close `Decline`; **default button = Decline** (consent is never given by pressing Enter).
- Accept: stores `ai_notice_accepted = true`, closes, and the call starts immediately (no second click).
- Decline / Esc: nothing is sent, flag stays false, strip shows `Nothing was sent.`; the next Ask Claude shows the notice again.
- The notice is plain text only (no links, no images); the bullets are two `TextBlock`s prefixed with `•  ` (not a ListView).

## D4. Error and reason copy (exact; one string per category)

`AiClient`'s message is built from the category only (spec). The text below is that message. Dialog display =
`Couldn't get a reply — ` + message + (for every failure except `NoKey`) ` You can still copy the prompt to claude.ai.`
Settings Test connection display = `Not connected: ` + message (no copy-prompt sentence). **Never include the key, the
model text, an HTTP body, a URL or an exception message in any of them.**

| Category | Message |
|---|---|
| NoKey | `No API key is saved. Add one in Settings -> AI.` |
| Rejected401 | `Anthropic did not accept the API key. Check it in Settings -> AI and save it again.` |
| ModelNotFound404 | `Anthropic does not know the model name. Check the Model field in Settings -> AI.` |
| RateLimited429 | `Anthropic is limiting requests right now (too many, or no credit left on the account). Wait a minute and try again.` |
| ServerError5xx | `Anthropic had a problem on its side. Try again in a few minutes.` |
| BadRequest400 | `Anthropic could not accept this request — it may be too long.` |
| Timeout | `Claude took longer than 2 minutes, so the request was stopped.` |
| Network | `Planillium could not reach Anthropic. Check your internet connection.` |
| Cancelled | `Stopped. Nothing was changed.` (neutral, no prefix, no suffix; used for both Stop and Esc-less cases) |
| Truncated | `Claude's reply was cut off before it finished, so it was not used.` |
| EmptyReply | `Claude sent back an empty reply.` |
| Unreadable | `Anthropic's answer was not in a form this app understands.` |

Reply-validation errors (reply arrived but fails the plan check) are **not** AI errors: they keep the existing red
`error` line with the existing text from `ParseReplacement` / `TryImport`, unchanged, under the reply box.

## D5. Things the Coder must not improvise

- No second accent button, no green/amber/blue status colours, no icons beyond the `✓` already in the copy, no emoji.
- No "Show key" / reveal / copy-key control; no key echo anywhere (status, tooltip, automation name, log, summary).
- No new nav item, no tab, no toast, no InfoBar: all feedback is the status/reason lines above.
- No auto-import and no auto-preview beyond what D2.5 states (Replace remaining runs the existing check immediately;
  Add plan stops at "Import plan").
- Ask Claude is **absent** (not disabled) with no key; Test connection and Remove key… are **disabled** (not hidden)
  with no key because they sit in a fixed row where hiding would shift the layout.
- Do not change the manual screen's layout, field order, button names or copy beyond what D2 lists. The prompt box
  content and Copy behaviour are untouched.
- Do not retry automatically, do not stream, do not show partial text.
- Do not pre-fill the model box with the default text (leave it empty; the default is the placeholder) so an
  untouched field stays "use whatever the app's default is".
- All new controls get `AutomationProperties.Name` exactly as quoted; status/reason TextBlocks get their text as
  their automation name (as `headline` and the mentions block do today).

## D6. Design checks QA can run mechanically (extra, labelled Designer — not from ACCEPTANCE.md)

- DC1. Settings menu lists exactly 9 items in order General, Income, Hours and reminders, Scoring, Activity keywords,
  Idle-answer library, TickTick, AI, Data; selecting "AI" shows `AIPanel` and hides every other panel (UIA: only one
  panel's controls visible).
- DC2. UIA tree of `AIPanel` contains, with these Names: `Anthropic API key` (password-type element, `IsPassword=true`),
  `Model`, `Save`, `Test connection`, `Remove key…`. The password element's value pattern never returns the key.
- DC3. Fresh scratch root (no key): Test connection and Remove key… are disabled; menu summary reads `No key — copy and paste`; status line empty.
- DC4. After Save with a fake key: key box empty; status begins `✓ Key saved.`; summary begins `Key saved · `; none of the visible text or UIA names on the page contains any substring of the fake key.
- DC5. Save with empty box and no key: status is exactly `Not saved: paste your API key first.`; the test credential slot still absent.
- DC6. Test connection against stub 200: status `✓ Connected. Claude answered using claude-sonnet-5-5.`; against 401, 429, 500, timeout: status begins `Not connected: ` and contains the matching D4 message verbatim; contains no digits of the HTTP status.
- DC7. Remove key… opens a dialog titled `Remove the saved API key?` whose default button is `Cancel`; Cancel leaves the credential in place.
- DC8. Replace remaining / Add plan, no key: no element named `Ask Claude` exists in the UIA tree; the exact no-key hint (D2.4) is present; Replace remaining captions are the original two strings.
- DC9. With key: element `Ask Claude` present (default style, not the dialog's primary button); sits in the same row as `Copy`, to its right, left edges within the 576 DIP content width; no horizontal scrolling in the dialog.
- DC10. During a slow stub call: a progress bar (`IsIndeterminate`), the text `Asking Claude… N s` with N increasing, and `Stop` are present; `Ask Claude`, the dialog primary button and the prompt-forming inputs are disabled/read-only; `Copy` is enabled. After Stop: the text `Stopped. Nothing was changed.` and all of them re-enabled.
- DC11. For each non-cancel failure, the dialog's reason line starts `Couldn't get a reply — `, contains the D4 message verbatim, ends with `You can still copy the prompt to claude.ai.` (except NoKey), and is a different element from the red reply-error line; typing in the reply box does not remove it.
- DC12. Notice: first Ask Claude shows a dialog titled `Before Planillium asks Claude` whose text contains the strings `Format my own plan`, `your own plan text in full`, and `no diary, scores, earnings, tags or settings`; its default button is `Decline`; Test connection never shows it (P6).
- DC13. Add plan success: reason line reads `✓ Claude's reply is in the box below. Nothing is added until you press Import plan.` and the button label is `Ask again`; `plans/active` unchanged.
- DC14. Hue budget: in a screenshot or by brush inspection of the AI panel and both strips, the only non-neutral colours are the system accent (one button per screen) and `SystemFillColorCriticalBrush` (failure lines, each of which also starts with a text label).
- DC15. Dialog outer width remains 640 and no AI element extends past 576 DIP content width at the app's minimum window width (UIA `BoundingRectangle`).

## Conflict with plan

None. The plan's "Test connection requires no notice", "no auto-import" and "reason line separate from the red
error" are all honoured. One note for the Planner (not a conflict): Save writes the model as well as the key, and
`AiKey.Save` must therefore not be the only write path — `ConfigService` writes `ai.model` in the same handler.

## Open question (Designer additions, no product decision made)

5. **"Ask again" overwrites the reply box.** If the user has typed or edited a reply by hand and presses Ask again, their
   edits are replaced without a prompt. Chosen: replace silently (replies are regenerable, extra confirm adds a click to
   the common case). Say if they would rather get a confirm when the box was edited by hand.
6. **No way to withdraw the notice acceptance** other than editing state.json. Not in the request, so not designed;
   say if Settings -> AI should get a "Show the data notice again" link.
