# SPEC -- G8 focus protection on other pages (agentic, planner 2026-10-29)

All paths are relative to `agentic/winui-agentic/Planillium.App/` unless stated. Line numbers are as of commit 1a71023.

## 1. Requirement (planner's words)
The Settings -> Income fix (B1f) stops a click on empty space from pushing the cursor into a box. Every other place
that scrolls and has a text box needs the same protection: after the cursor was in a box, a click on blank space
(padding, right edge strip, gaps between sections, area below the content, the scrollbar strip) or leaving the box
any way must leave the cursor in no box unless the user clicked or tabbed into one. Tab order, click-into-box and
Settings -> Income stay exactly as they are. Plans has no box; its add/edit dialogs are in scope.

## 2. The B1f fix (where and how)
- **Where.** `Pages/SettingsPage.xaml` line 83 (`SectionContent` Grid, `Background="Transparent"`, `Padding="0,0,4,0"`,
  `PointerReleased="SectionContent_PointerReleased"`), line 67 (`SectionScroll`, `SizeChanged`), line 360-363
  (`FocusPark` 1x1 Button). `Pages/SettingsPage.xaml.cs`: constructor 66-71 (PreviewKeyDown, GettingFocus,
  `AddHandler(PointerPressed, ..., handledEventsToo: true)`), `ParkFocus` 565-591, `FocusPark_LostFocus` 686,
  `Page_PointerPressed` 718-740, `SectionContent_PointerReleased` 755, `SectionScroll_SizeChanged` 757,
  `TabJustPressed` 771, `Page_PreviewKeyDown` 773, `Page_GettingFocus` 797-818. Commits d2d30b0 and e82135f.
- **Mechanism of the bug.** A ScrollViewer that is not a tab stop answers an unclaimed left-button PointerReleased
  by focusing its first focusable child, but only when focus is not already inside it. With a real mouse (about
  100 ms hold) the press-time park lands before the release, so the scroller then pulls focus into the first box
  (InputDevice reported as Mouse). Scripted 0 ms clicks never show it.
- **Mechanism of the fix (three layers).**
  1. *Release claim:* `SectionContent` marks every PointerReleased handled, so the scroller never runs its focus
     step. e82135f moved the scroller's 4 px padding inside SectionContent and sets `SectionContent.MinHeight` to the
     viewport height, so the edge strip and the area below the content are inside the claimed Grid. This layer
     already covers **all** Settings sections, because every panel sits in SectionContent.
  2. *Blank-press park:* after one dispatcher turn, if focus is a TextBox/ButtonBase inside IncomePanel and the press
     was not on it, `ParkFocus` (deferred again) focuses the 1x1 `FocusPark` (IsTabStop true only while holding focus),
     falling back to SectionList.
  3. *Focus guard:* `Page_GettingFocus` lets a move into an Income box through only for InputDevice
     Mouse/Touch/Pen/Keyboard, a Tab within 600 ms, or a restore of `_lastFocusedInPage`; otherwise TryCancel + park.
- Rejected in B1f and still rejected: `AllowFocusOnInteraction=False` (inherited, so boxes stop taking focus).
- Note: the e82135f QA re-run is still pending per CONTEXT.md; G8 inherits that risk (see 8, risk R1).

## 3. Inventory (acceptance 1)
"Text box" = TextBox (incl. MoneyBox), PasswordBox, NumberBox, AutoSuggestBox, RichEditBox. No editable ComboBox exists
(`IsEditable` not used anywhere). CalendarDatePicker and ComboBox are not text boxes.

### Pages and panes
| Page / pane | File:line (scroller) | Text boxes | Current protection | Verdict |
|---|---|---|---|---|
| Settings -> Income | SettingsPage.xaml:67/83 | MonthlyIncomeBox (.xaml.cs:45), NewPaidTagName (xaml:125), NewPaidTagRate (127), tag-rate MoneyBoxes (.xaml.cs:461) | B1f, all 3 layers | **covered** -- unchanged |
| Settings -> General | SettingsPage.xaml:88 (inside SectionContent) | YourName (93) | release claim only | **to cover** (park + guard) |
| Settings -> Hours | xaml:141 | WorkStart/WorkEnd/EodTimeBox (156-158); NumberBoxes GraceMin, RepeatMin, IdleMin, RetentionDays, LateDayReminderHours (172-182) | release claim only | **to cover** |
| Settings -> Scoring | xaml:189 | NumberBoxes built in code (.xaml.cs:966) | release claim only | **to cover** |
| Settings -> Keywords | xaml:202 | RulesOn/Off/Neutral multi-line (211-213) | release claim only | **to cover** |
| Settings -> Idle | xaml:217 | IdleOn/Off/Neutral multi-line (229-231) | release claim only | **to cover** |
| Settings -> AI | xaml:248 | AiKeyBox PasswordBox (253), AiModelBox (258) | release claim only | **to cover** |
| Settings -> TickTick, Data, Change history | xaml:235, 291, 325 | none | release claim | **not applicable** -- no box |
| Today | TodayPage.xaml:11 RootScroller | TaskNoteView editBox (Views/TaskNoteView.cs:119, used at TodayPage.xaml.cs:452) | none | **to cover** |
| Schedule | SchedulePage.xaml:11 Scroller + per-plan inner ScrollViewer (SchedulePage.xaml.cs:210, built in Render) | TaskNoteView editBox in DayCards (SchedulePage.xaml.cs:396) | none | **to cover** (both scrollers) |
| Reports (incl. Diary) | ReportsPage.xaml:16 RootScroller; inner: Diary filter row 976, tag toolbar 1059, results 1103 (ReportsPage.Diary.cs); summary tables ReportsPage.Styling.cs:116 | diary search TextBox (ReportsPage.Diary.cs:873) | none | **to cover** (root + inner scrollers, which are blank space of the page) |
| Reports filter flyouts | ReportsPage.Diary.cs:931 | none (checkboxes in popups) | -- | **not applicable** -- no box, popup |
| Plans | PlansPage.xaml:10 RootScroller | none (buttons only; boxes live in dialogs) | -- | **not applicable** -- no box on the page |
| Main window / nav pane | MainWindow | none | -- | **not applicable** -- no box |

### Dialogs
Every dialog is built by `Dialogs/DialogControls.cs` `Build(...)` (line 18, the single `new ContentDialog` at 29). The
WinUI 1.8 ContentDialog template wraps title + 24 px padding + our content in its own `ContentScrollViewer`, so **every
dialog has a scrolling body**; it is in scope whenever its content has a text box.

| Dialog | File:line | Text boxes | Own inner scroller | Current | Verdict |
|---|---|---|---|---|---|
| Add / edit plan | AddPlanDialog.cs:166-205, Build 350 | name/description/etc. TextBoxes (3 multi-line) | yes, MaxHeight 560 (350) | none | **to cover** |
| Add task | AddTaskDialog.cs:34, 35, 43 (NumberBox), 49; Build 66 | yes (1 multi-line) | template only | none | **to cover** |
| Edit diary entry | EditDiaryEntryDialog.cs:24, 25, 31 (NumberBox), 67 (AutoSuggestBox); Build 119 | yes | template only | none | **to cover** |
| Idle return | IdleReturnDialog.cs:118, row NumberBox + AutoSuggestBox 320; Build 221 | yes | horizontal rowsScroller 203 | none | **to cover** |
| Name setup | NameSetupDialog.cs:36; Build 54 | yes | template only | none | **to cover** |
| Replace remaining | ReplaceRemainingDialog.cs:92, 103, 143, 209; Build 244-245 | yes (3 multi-line) | window-aware MaxHeight scroller | none | **to cover** (keep `Loaded -> change.Focus` at 253) |
| Review (evening) | ReviewDialog.cs:358; Build 159 | reflection box | template only | none | **to cover** |
| Spend | SpendDialog.cs:73 (NumberBox); Build 88 | yes | template only | none | **to cover** |
| Split diary entry | SplitDiaryEntryDialog.cs rows (MinutesBox, AutoSuggestBox 174); Build 72 | yes | template only | none | **to cover** |
| TickTick connect | TickTickConnectDialog.cs:43, PasswordBox 54; Build 73 | yes | template only | none | **to cover** |
| Reschedule task, Replan overdue (scroller 66), Excluded weekdays, Briefing, Pending notifications, Kickoff, Task detail, Start queued plan, Teach plan tools (string content), Replace-remaining confirm (350, list scroller 446) and warn (482), Add-plan max-plans choice (118), Edit-diary delete confirm (160), Plans confirms, Reports previews (Diary 1257/1411), Settings confirms (280, 1328, 1475, 1576, 1724, 1787) | various | none (pickers, buttons, text only) | some | -- | **not applicable** -- no text box (auto-detected; the helper does nothing) |

## 4. Chosen approach -- one shared helper, `Controls/FocusShield.cs`
A new static class (name checked unused) holding the three B1f layers in reusable form. Settings -> Income code is not
touched; the helper is attached beside it.

### 4.1 `ClaimReleases(ScrollViewer sv)`
Marks PointerReleased handled at the scroller's **template root** (`VisualTreeHelper.GetChild(sv, 0)` once loaded; if
not yet loaded, hook `Loaded` once, then attach). Idempotent (keep a marker, e.g. a private attached property or a
`ConditionalWeakTable`). Handler: `e.Handled = true` -- same as `SectionContent_PointerReleased`.
- Why the template root: it contains the scroller padding, the scrollbar strip, and the area below short content, so
  no padding move, no `MinHeight` fill and no change to `PageLayout.CenterContent` (which reads `scroller.Padding`).
- Handled releases do not stop clicks: Button.Click, CheckBox, ComboBox, TextBox caret placement all act before the
  event bubbles to the scroller root (same as SectionContent today).

### 4.2 `Attach(FrameworkElement scope, Button park, AttachOptions)` -- blank-press park
`scope.AddHandler(UIElement.PointerPressedEvent, handler, handledEventsToo: true)`. Handler, after **one** dispatcher
turn (`DispatcherQueue.TryEnqueue`), parks only when all hold:
1. `FocusManager.GetFocusedElement(scope.XamlRoot)` is a text-entry element (TextBox, PasswordBox, RichEditBox -- this
   includes the inner TextBox of NumberBox/AutoSuggestBox and MoneyBox) inside `scope` and **not** inside any element in
   `options.Skip` (Settings passes IncomePanel);
2. the press source is not inside the focused box's *owner* (the TextBox itself, or its NumberBox / AutoSuggestBox /
   ComboBox ancestor -- so spin buttons and the AutoSuggestBox query button do not park);
3. the press source's visual parent chain does not pass through a `Popup` before reaching `scope` (suggestion lists,
   NumberBox popups, flyouts are left alone);
4. the press source is not itself a focusable control that will take focus (Control with IsTabStop true and
   IsEnabled) -- clicking a button or another box lets that control take focus normally.
Parking happens in a second deferred turn with condition 1 re-checked (mirrors `ParkFocus`).

### 4.3 Park routine
`FocusShield.CreatePark()` returns a Button with the exact FocusPark attributes (1x1, Opacity 0, MinWidth/MinHeight 0,
Padding 0, BorderThickness 0, IsTabStop false, HorizontalAlignment Left, VerticalAlignment Top,
`AutomationProperties.AccessibilityView="Raw"`). Park = set IsTabStop true, `Focus(FocusState.Programmatic)`; the
park's LostFocus sets IsTabStop false again. Handle the park's `BringIntoViewRequested` (`e.Handled = true`) so parking
never scrolls the page. If `Focus` returns false, try `options.Fallback` (if given); then log once via the existing
logger. Pages may also pass an existing park (Settings passes `FocusPark`).

### 4.4 Focus guard (pages only, not dialogs)
`scope.GettingFocus` handler (plus a tunnelling `PreviewKeyDown` on the scope that stamps the last Tab time). A move
whose `NewFocusedElement` is a text-entry element in scope (and not in Skip) is allowed when any holds:
InputDevice is Mouse/Touch/Pen/Keyboard; a Tab within 600 ms; NewFocusedElement equals the last element that held
focus in scope (window switch restore); or it equals `FocusShield.Requested` (see below). Otherwise `TryCancel()` and
park (deferred). Same rule set as `Page_GettingFocus`, generalised.
- `FocusShield.RequestFocus(Control box)`: set static `Requested = box`, call `box.Focus(FocusState.Programmatic)`,
  clear in `finally`. Safe because GettingFocus is synchronous (LESSONS: no one-shot flags consumed by LostFocus).
- Never use GotFocus/LostFocus for these decisions (they fire late).

### 4.5 Per-area wiring
- **Settings (non-Income).** In the constructor after the existing hooks: `FocusShield.Attach(SectionContent, FocusPark,
  new() { Skip = { IncomePanel }, Fallback = SectionList, Guard = true })`. Do not call `ClaimReleases` (SectionContent
  already claims). Income handlers keep working on IncomePanel; the helper ignores IncomePanel, so the two act on
  disjoint boxes. Parking out of a Settings box fires that box's existing LostFocus save (`YourName_Changed`,
  `Rules_Changed`, NumberBox commit) -- the same as clicking a button today.
- **Today, Schedule, Reports.** In XAML, wrap the root ScrollViewer in a Grid (the page root becomes the Grid). Add the
  park as the **first** child (so it sits under the scroller and Tab from the parked state enters the page content).
  Keep `x:Name` of the scroller unchanged; code that references `RootScroller`/`Scroller` keeps working.
  Code-behind: `FocusShield.ClaimReleases(scroller)` and `FocusShield.Attach(this, park, new() { Guard = true })`.
  - Schedule: also `ClaimReleases` on each per-plan inner ScrollViewer where it is created in `Render()`
    (SchedulePage.xaml.cs:210).
  - Reports: also `ClaimReleases` on the inner ScrollViewers at ReportsPage.Diary.cs:976, 1059, 1103 and
    ReportsPage.Styling.cs:116 (`Scrollable()`), at creation. Without this the inner scroller's own focus step moves
    focus onto a checkbox/button inside it, which is not a box but still a jump (open question Q4 on whether that
    matters; claiming is cheap and safe).
  - Not the filter flyout scrollers (931): popups, no boxes.
- **TaskNoteView.EnterEdit** (Views/TaskNoteView.cs:173): replace `editBox.Focus(FocusState.Programmatic)` with
  `FocusShield.RequestFocus(editBox)` so the guard lets the user-requested edit box take focus.
- **Plans.** Nothing (not applicable).
- **Dialogs -- centrally in `DialogControls.Build`.** Before creating the ContentDialog, if `content` is a
  UIElement and a logical walk (Panel.Children, Border.Child, ContentControl.Content incl. ScrollViewer.Content,
  ItemsControl items that are UIElements) finds a text-entry control -- do **not** descend into TextBox/NumberBox/
  AutoSuggestBox/PasswordBox templates -- then:
  1. wrap content in `new Grid { Children = { park, content } }` (park first; content unchanged otherwise);
  2. `ClaimReleases` every ScrollViewer found in the content walk (AddPlan 350, ReplaceRemaining 244, IdleReturn 203);
  3. on `dialog.Loaded`, find the nearest ScrollViewer ancestor of the content (the template `ContentScrollViewer`)
     and `ClaimReleases` it;
  4. `FocusShield.Attach(dialog, park, new() { Guard = false })`.
  String content and box-less dialogs are left byte-for-byte as now. No guard in dialogs: the dialog's opening focus
  and ReplaceRemaining's `Loaded -> change.Focus` (253) stay unchanged (open question Q2).
  - Dialog rows built later (Split/IdleReturn "add row") are found at press time, since condition 1 reads the live
    focused element; only the initial walk decides whether to attach. If a dialog starts with no box but adds one later,
    it is not covered -- none does today (checked).

### 4.6 Exit-path table (pages and dialogs after the change)
| Exit path from a box | Before | After |
|---|---|---|
| Click blank space (padding, edge strip, section gaps, below content, scrollbar strip) | scroller may pull focus to first box | release claimed; press-park moves focus to the invisible park; no box focused |
| Click another box | that box | unchanged (InputDevice Mouse allowed) |
| Click a button / checkbox | that control | unchanged; if the click rebuilds the tree (Render), WinUI's fallback into a box is blocked by the guard (pages) and parked |
| Tab / Shift+Tab | next/previous stop | unchanged (Keyboard device + Tab stamp allowed) |
| Esc in a page box | stays in box | unchanged (Q1) |
| Esc in a dialog | closes dialog | unchanged |
| Enter in a single-line page box | stays (or the box's own KeyDown, e.g. AiBox_KeyDown) | unchanged (Q1) |
| Enter in a dialog box | default button / newline in multi-line | unchanged |
| Window switch away and back | restores last focus | unchanged (restore allowed) |
| Navigate to another page | page unloads | unchanged |
| Note edit Save/Cancel (TaskNoteView) | box collapses, WinUI falls back to first focusable | fallback into another box is blocked by the guard and parked |

## 5. Rejected alternatives
- **Copy the e82135f content-host pattern to each scroller** (transparent Grid inside each ScrollViewer, move padding
  inside, MinHeight = viewport). Rejected: needs a padding move and a viewport fill per scroller, changes
  `PageLayout.CenterContent` on four pages, and cannot reach the ContentDialog template's title/padding area, which
  is outside our content. The template-root claim covers the same strips with no layout change.
- **Switch Settings -> Income to the helper.** Rejected: changes proven, QA-passed code for no user gain; the request
  says Income stays as is.
- **A single global `FocusManager.GettingFocus` guard.** Rejected: cannot make focus leave a box on a blank click (no
  focus move happens), and blocks legitimate programmatic focus across the app.
- **`AllowFocusOnInteraction=False`** on scrollers: inherited, boxes stop taking focus on click (B1f finding).

## 6. Files to change
- New: `Planillium.App/Controls/FocusShield.cs`.
- `Pages/SettingsPage.xaml.cs` (one Attach call; no change to Income code or XAML).
- `Pages/TodayPage.xaml`, `Pages/TodayPage.xaml.cs`.
- `Pages/SchedulePage.xaml`, `Pages/SchedulePage.xaml.cs`.
- `Pages/ReportsPage.xaml`, `Pages/ReportsPage.xaml.cs`, `Pages/ReportsPage.Diary.cs`, `Pages/ReportsPage.Styling.cs`.
- `Views/TaskNoteView.cs` (RequestFocus).
- `Dialogs/DialogControls.cs` (central dialog wiring).
- Side docs (coder, same pass): `CHANGELOG.md` (Unreleased), `CONTEXT.md`, `context/todos.md`, `DECISIONS.md`
  (lesson below if confirmed). No change to the test project (WinUI helper cannot be source-linked).

## 7. Edge cases
- **Late GotFocus/LostFocus:** decisions use GettingFocus (sync) and the live focused element after a dispatcher turn;
  no flags consumed by LostFocus.
- **Render() rebuilds** (Today/Schedule/Reports): a focused box removed by a rebuild makes WinUI pick a fallback; the
  guard blocks a fallback into a box and parks. Note drafts survive via `_openDrafts`; this is unchanged. Inner
  scrollers created in Render are claimed at creation each time (idempotent claim, new instances each render).
- **ItemsRepeater recycling** (Schedule DayCards): claim is on the scroller, not items, so recycling does not matter.
- **AutoSuggestBox / NumberBox popups:** press inside a Popup never parks (condition 3).
- **Scrollbar press while in a box** now parks (same as Income today).
- **Right-click / context menu on a box:** source is the box owner -> no park.
- **Multi-line boxes** (Keywords, Idle, Add plan, Replace remaining, notes): Enter adds a newline; unchanged.
- **Park must not scroll** (BringIntoViewRequested handled) and must not be visible to Narrator (Raw view).
- **Park inside dialog first in Grid:** zero size, Opacity 0; does not change dialog layout. Coder checks one dialog
  rect visually by reasoning (no QA).
- **Reports G5 tree release** on leave: helper holds no strong static references except `Requested` (cleared in
  finally) and the last-focused element per scope -- store it as a `WeakReference`.
- **Narrator / UIA SetFocus** into a box is blocked by the guard on pages (as in Income); see Q5.
- **Two handlers on Settings:** Income's `Page_PointerPressed` and the helper's both run on a press; each acts only on
  its own boxes, and both park to the same FocusPark -- no conflict.

## 8. Risks
- R1: The template-root claim is inferred from B1f evidence (the padding strip jumped, so the focus step lives at
  ScrollViewer level). Unverified until QA; QA is deferred. The coder must state this in the report.
- R2: The Settings scrollbar strip lies over SectionContent's scroller, not inside SectionContent; it may still jump.
  Pre-existing; to be included in the deferred Income re-run (acceptance 4).
- R3: Dialog content wrapping could shift layout by a pixel; park is zero-size, so expected none.
- R4: Guard blocks programmatic focus into a box not routed through `RequestFocus`. Coder must grep
  `.Focus(` in Pages/Views for any focus into a box and route it (only TaskNoteView:173 found by the planner; dialog
  focus is unguarded).

## 9. Acceptance (verbatim from ACCEPTANCE.md)
1. List every scrolling page/pane with at least one text box, and for each state covered by the same protection as the
   Settings -> Income fix or not applicable (with reason).
2. On each covered page, clicking blank space (including the right edge strip and the gaps between sections) after the
   cursor was in a box leaves the cursor out of every box. Trial counts follow case complexity.
3. Tab order and clicking into a box still work normally on every covered page.
4. Settings -> Income behaves as before (re-run its focus cases once).
5. Build clean (/warnaserror); existing tests pass.

Planner's notes (not acceptance lines): QA is deferred by the BRIEF addendum, so lines 2-4 are UNTESTED after this
round; line 1 is met by section 3 above; line 5 is the coder's precheck.

## 10. Rules applied
- LESSONS (focus/layout): B1f missed a 4 px edge strip -> claim at the template root covers padding, scrollbar strip,
  below-content area; GotFocus/LostFocus are late -> GettingFocus + dispatcher re-check only; guard blocks UIA SetFocus
  -> recorded for QA (real clicks/keys).
- DECISIONS 407-415 (focus/edge lesson, exit-table lesson) -> exit-path table in 4.6 covers every exit, every sibling
  page and dialog in section 3.
- DECISIONS "Safety around real data" -> no data, config, plans or live exe touched; coder's precheck is build + tests only.
- Check siblings -> inventory covers all pages, panes, inner scrollers and all dialogs via the single Build entry point.

## 11. Open questions (default in brackets; coder uses the default)
- Q1: Should Esc / Enter in a page box leave the box, as in Income? [unchanged -- not asked for]
- Q2: Should dialogs also get the guard, and should a dialog still open with the cursor in its first box? [no guard;
  opening focus unchanged]
- Q3: Plans has no box but its scroller can still move focus to "+ Add plan" on a blank click. Claim it? [not applied --
  out of scope]
- Q4: Income also parks a focused button; should other pages park focused buttons/checkboxes too? [no, boxes only;
  inner scrollers are claimed so they cannot jump]
- Q5: The page guard blocks Narrator/UIA programmatic focus into a box (as Income does). Acceptable? [yes, matches Income]
- Q6: In a dialog, after "Remove row" deletes the focused row, WinUI may move focus into another box. Block it? [unchanged]
