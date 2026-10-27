# SPEC-2 G4 agentic -- addendum 2 (A3 Plans restyle, 4-minute timeout, Stop/answer verification)

Paths under `D:/CLAUDE/Planillium/agentic/winui-agentic/Planillium.App/` unless marked. Builds on commit 065fed7. No QA this pass (precheck only: strict build + tests, local commit, no push).

## Memory rules applied
- LESSONS "Raising a non-streaming token cap means re-checking the request timeout; show durations from one constant" -> (b): only `DefaultTimeout` changes, `TimeoutMinutesText` feeds every visible mention (already true: AiClient.cs:42, AiAskControls.cs:64). Consequence flagged below.
- LESSONS "check siblings / grep callers" -> (a) automation names and visible texts of moved buttons grepped across Tests/qa: no code or test references them (only old pilot-spec prose). Siblings of the timeout number: CHANGELOG, DECISIONS rule 16, CONTEXT.md line 27, SPEC.md (history, leave), plus the doc comment at AiAskControls.cs:103.
- LESSONS Layout "force layout, test at 1, N, N+2 items" -> card height no longer depends on a variable number of wrapped buttons (4 fixed buttons); check 1, 3 plans and the no-plan state.
- LESSONS "UIA finds elements by automation id/name" -> keep every existing AutomationProperties name on moved buttons.
- Page-level rules in DECISIONS (progress-based "Day X of Y", drift/due line colours, archive rule, max active plans) are logic: untouched.

## (a) Plans page restyle (A3)

Current page, from own code (`Pages/PlansPage.xaml`, `PlansPage.xaml.cs`): scroller `Padding 28,8,28,28`, column `MaxContentWidth 880` centred by PageLayout; header Grid `Margin 0,8,0,16` with `TitleTextBlockStyle` "Plans" and accent "+ Add Plan"; two InfoBars (margin 12 each); `ActiveList` Spacing 10 of card Borders (CardBackgroundFillColorDefaultBrush, CardStroke, radius 8, Padding 18,14,18,14); card = Subtitle-style name (max 2 lines), grey 13 px meta line, 12 px due line (red if drift>0, green otherwise), `ProgressBar` (margin top 6), then a `WrapFlowPanel` (spacing 8, margin top 12) holding up to six buttons: `📋 Briefing` (if plan has one), `+ Add task`, `Replace remaining tasks…`, `Excluded days…`, `Archive`/`Archive ✓`, `Teach on-plan apps…` (if tools). Below: "QUEUED IDEAS" (11 px SemiBold tertiary caps, margin 2,26,0,8) + rows (name left, `Start now`, red `Delete`), "ARCHIVED" header + rows (name, `Restore`) or "No archived plans.".

Target (screenshot 4.png): same skeleton. Differences and changes:

| # | Target | Current | Change |
|---|---|---|---|
| 1 | Button row exactly: `+ Add task`, `Replace remaining…`, `Archive`, `More ▾` | up to six buttons incl. Briefing, Excluded days…, Teach | Move Briefing, Excluded days…, Teach on-plan apps… into a `More` drop-down (below) |
| 2 | Label `Replace remaining…` | `Replace remaining tasks…` | `Content = "Replace remaining…"`. Keep tooltip and `AutomationProperties.Name = "Replace remaining tasks: {plan.Name}"` unchanged (screen reader and QA name stay) |
| 3 | `More ▾` with flyout items `Briefing`, `Excluded days…`, `Teach on-plan apps…` | none | PlansPage.xaml.cs, PlanCard: `var more = new DropDownButton { Content = "More", VerticalAlignment = Center }` (WinUI 3 control draws the small chevron seen in the screenshot) with `Flyout = new MenuFlyout()`. Name `More: {plan.Name}`. Items are `MenuFlyoutItem`, text `Briefing` (no emoji; shown only if `plan.Briefing != null`), `Excluded days…` (always), `Teach on-plan apps…` (only if `PlanStore.DistinctTools(plan).Count > 0`). Item order as in target. Each item keeps its existing click handler body verbatim (BriefingDialog.ShowAsync, ExcludedWeekdaysDialog + Render + RefreshScore, TeachPlanTools.RunAsync) and its tooltip; `AutomationProperties.SetName` on each item = the old button names (`Briefing: {plan}`, `Excluded days: {plan}`, `Teach on-plan apps: {plan}`) |
| 4 | Gap between cards ~12 px (card bottom 250, next top 262) | `ActiveList Spacing=10` | XAML: `Spacing="12"` |
| 5 | Archive greyed when not allowed | same (`IsEnabled = complete`, label `Archive ✓` when complete) | none, keep |
| 6 | Title bold/larger, grey meta, red due line, thin accent progress line full width | already so (SubtitleTextBlockStyle, 13 px secondary, 12 px due, ProgressBar) | none. Optional: give the ProgressBar no extra height (default 3 px track) -- do not set Height |
| 7 | "QUEUED IDEAS" small-caps grey header, rows with `Start now` and red `Delete` right | identical (11 px SemiBold tertiary, DangerButtonStyle Delete) | none |
| 8 | "ARCHIVED" header with "No archived plans." | identical; archived rows show `Restore` | none |
| 9 | Title row `Plans` + accent `+ Add Plan` top right | identical | none |

Button row panel: keep `WrapFlowPanel` (spacing 8, margin top 12) so a narrow window still wraps; with four short buttons it will not wrap at default width.

Edge cases
- Plan with no briefing and no tools: flyout holds only `Excluded days…` (never an empty menu, since that item is unconditional).
- Dialog opened from a flyout item: the `MenuFlyout` closes first; the handlers use `XamlRoot` of the page (already used), so the dialog opens normally. After success `Render()` rebuilds the card; the flyout is already closed.
- `Render()` called by the day-change watcher while a flyout is open: the card is removed, flyout closes with it; no crash expected (nothing holds the old card).
- Narrow window: button row wraps; `More` stays last. 1, 2 and 3 active plans and zero plans ("No active plans.") all render.
- Accessibility: keyboard Tab reaches `More`; Enter/Space opens the menu; Esc closes it. Do not intercept keys.
- Dark and light theme: only theme resources already used on the page; nothing hard-coded.
- Delete/Start now/Restore, archive confirm, queued start dialog: untouched.
- Header-to-first-card gap in the screenshot is about 44 px; current header margin 16 plus two (empty) InfoBar margins of 12 normally yields about that. Do not change the header margin; if QA later measures a difference, adjust only the header `Margin` bottom.

Files: `Pages/PlansPage.xaml` (ActiveList Spacing 12), `Pages/PlansPage.xaml.cs` (PlanCard button section, lines ~195-282). No other file.

Rejected alternative: a `Button` with a `Flyout` and the text "More ▾" typed as characters -- the glyph would not match the control chevron, would not follow the theme/scale, and Narrator would read the triangle. Also rejected: keeping all six buttons and only restyling (does not match the target structure).

## (b) Timeout 4 minutes

- `Services/AiClient.cs:111` `DefaultTimeout` `FromSeconds(360)` -> `FromSeconds(240)`. `TimeoutMinutesText` already computes "4 minutes"; the Timeout message (line 42) and "Gives up after 4 minutes." (AiAskControls.cs:64) follow with no code change.
- Fix stale text: `Dialogs/AiAskControls.cs:103` doc comment "6 minutes" -> "4 minutes (AiClient.TimeoutMinutesText)"; the doc comment at AiClient.cs line 113 `("6 minutes")` -> `("4 minutes")`; `Services/AiKey.cs` comment if it names a number (check).
- Docs: `CHANGELOG.md` line 5 ("waits up to 6 minutes", "say 6 minutes") -> 4; `DECISIONS.md` line 212 (timeout 6 minutes) -> 4; `CONTEXT.md` line 27 -> 4 minutes; `context/domain.md` AI layer lines if they say 6; `context/todos.md` G4 entry add one dated line for this pass. `SPEC.md` (round 1) is history, leave.
- Tests: none asserts 360 or "6 minutes" (grep clean); no change.
- Consequence (one line for the PM/user): 16000 tokens at about 50-60 tokens/s is 270-320 s, so a reply that really fills the cap can now time out at 240 s ("Claude took longer than 4 minutes"). A 34-task rewrite (about 8-16k tokens) normally finishes in 2-4 min. User decision; no code answer short of streaming (rejected in SPEC.md).
- Test connection also waits at most 4 min now (uses `DefaultTimeout`); fine.

## (c) Stop-on-answer and the Cancel button (verified in code, `Dialogs/AiAskControls.cs`)

Verified, no change needed
- The waiting state already has a button: `Stop` (line 70-72, UIA name `Stop`), in the progress row beside the bar, visible only while a call runs. Its handler `_cts?.Cancel()` cancels the token passed to `AiClient.AskAsync`, which links it into the `HttpClient.SendAsync`/`ReadAsStringAsync` calls (AiClient.cs:149,164,166): the HTTP request is aborted, not just ignored. Result is `AiFailure.Cancelled`, shown as the neutral (non-red) line "Stopped. Nothing was changed." (`ShowNeutral(Stopped)`, lines 212-214), not as a failure.
- Pre-ask state restored after Stop: `Progress` collapsed, `_busy=false`, `Lock(false)` (reply box writable again, Ask and dialog primary button re-enabled, caller inputs unlocked), reply box untouched, focus back on Ask (lines 207-234).
- Answer ends waiting at once: the loop `while(!call.IsCompleted){ ...; await Task.WhenAny(call, Task.Delay(1000)); }` wakes the moment `call` completes (no 1 s polling wait); then `_cts=null; cts.Dispose()`, Progress collapsed, counter no longer updated (lines 198-209). A Stop click in the same instant gives one outcome, Cancelled (AiClient.cs:175). Closing the dialog also cancels silently (lines 121-125).
- Decision: keep the label "Stop" (brief says Stop exists and is liked; same word is used on Settings Test connection). No rename to Cancel.

What to add or change (small)
1. Nothing functional. Optional hygiene, coder may skip: the `Task.Delay(1000)` timers are not cancelled when the call finishes first (they expire harmlessly within 1 s). Do not touch.
2. Make the Stop button's accessibility text explicit about its effect: `AutomationProperties.SetName(stop, "Stop")` stays (QA finds it by this name); add `ToolTipService.SetToolTip(stop, "Stop waiting and cancel the request. Nothing is changed.")`.
3. Edge: no Stop exists while the first-use notice is showing (`Progress` hidden, no request yet); there `Decline` is the way out ("Nothing was sent." via `NothingSent`). Left as is.

## Acceptance
Copied verbatim from `ACCEPTANCE.md`:
1. A stub reply for a 34-task plan (over 8000 output tokens) is accepted in Replace remaining and in Add plan.
2. Truncation by the new cap still shows the clear "cut off" message, no crash.
3. The Plans page layout matches the regular build's Plans page look (you may read the regular build's running look
   via the user's reference screenshots only if provided in the brief; otherwise describe the layout from this
   side's own code and request the user's reference).
4. The Ask dialog's acceptance text is visibly smaller; still readable and unclipped.
5. Replace remaining dialog: no vertical scrollbar at its default size with a typical task list.
6. Build clean (/warnaserror); existing tests pass.

Added by me (SPEC-2; all deferred QA, precheck only now):
- S1. Plans page matches the screenshot structure: per active plan a rounded card with title, grey meta line, red/green due line, full-width thin progress line, then exactly four controls `+ Add task`, `Replace remaining…`, `Archive` (disabled unless every task is done), `More ▾`; `More` opens Briefing (only if the plan has one) / Excluded days… / Teach on-plan apps… (only if the plan has tools); below the cards "QUEUED IDEAS" rows with `Start now` and red `Delete`, then "ARCHIVED" with "No archived plans." or rows with `Restore`.
- S2. Every moved action still works (opens its dialog, same results); UIA names unchanged (`Briefing: {plan}`, `Excluded days: {plan}`, `Teach on-plan apps: {plan}`, `Replace remaining tasks: {plan}`), cards 12 px apart.
- S3. The timeout is 4 minutes: the "Gives up after 4 minutes." line and the Timeout message both say "4 minutes" and nothing in the UI says 6 or 2 minutes.
- S4. Cancel works: pressing `Stop` during the wait aborts the request (the stub sees the connection drop), shows the neutral "Stopped. Nothing was changed.", hides the progress row, leaves the reply box empty and writable, Ask enabled, no red message.
- S5. An answer arriving ends the waiting: progress row and counter vanish immediately (under 1 s after the stub replies), reply box filled, no further counter ticks, Stop gone.

## Files to change (summary)
1. `Pages/PlansPage.xaml` (Spacing 12)
2. `Pages/PlansPage.xaml.cs` (PlanCard: Replace label, More DropDownButton + MenuFlyout; remove the three buttons from the row)
3. `Services/AiClient.cs` (DefaultTimeout 240 s, doc comment)
4. `Dialogs/AiAskControls.cs` (doc comment line 103, Stop tooltip)
5. `Services/AiKey.cs` (comment only, if it states a number)
6. Docs: `CHANGELOG.md`, `DECISIONS.md` rule 16, `CONTEXT.md` line 27 and the A3 note ("out of scope" -> built), `context/domain.md`, `context/todos.md`.

## Open questions
1. "Replace remaining…" visible text: I follow the screenshot (shortened from "Replace remaining tasks…"); UIA name and tooltip keep the long form. Tell the PM if the full label should stay.
2. Timeout 4 min versus a full 16000-token reply (see (b)): decided by the user; stated once.

## Lessons
- A "restyle to a screenshot" job is mostly already done by an earlier layout spec: diff the screenshot against the card builder first, and the real changes shrink to the button row and one spacing value.
- Moving buttons into a menu: keep each control's UIA name on the menu item and keep the long name when the visible label is shortened, so QA scripts and screen readers still find them.
- When a constant already feeds every visible text (TimeoutMinutesText), a number change is one line; grep the docs and doc comments, not the UI strings, for the stale number.
- Verify "Stop" by following the token from the button to HttpClient.SendAsync, not by the label; check the Task.WhenAny loop wakes on completion before claiming "no polling delay".
