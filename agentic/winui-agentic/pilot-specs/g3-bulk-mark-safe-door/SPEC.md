## Screen design

Designed by the agentic designer, 2026-10-29, with `ui-ux-pro-max` (WinUI / desktop guidance: confirm before destructive bulk action, safe default button, no layout shift under the cursor, state always paired with text, status near the control that caused it) and `ui-design-rules.md` (every state drawn: empty, error, loading; no new hue; every nav entry opens something). Reuses what is already in `Planillium.App`: the Diary toolbar in `Pages/ReportsPage.Diary.cs` (`BuildDiaryMarkToolbar`, `UpdateMarkToolbar`), `Dialogs/DialogControls.Build` + `DialogGate.ShowAsync` (confirm style: title, wrapped body, primary + "Cancel", default button Cancel), the page `InfoBar` look (`SaveErrorBar`), and the Settings section menu + `SaveStatus` strip in `Pages/SettingsPage.xaml`. No new colours: only system InfoBar severities, `TextFillColor{Secondary,Tertiary}Brush`, `SubtitleTextBlockStyle`, `DangerButtonStyle` (not used here), card Border look (`CardBackgroundFillColorDefaultBrush` / `CardStrokeColorDefaultBrush`, CornerRadius 8).

Naming used below: **M** = real number of matches of the current day/All-time + search + Category/App/Page/Tag filters, counted BEFORE the 300-row search cap and before paging (the same `filteredList.Count` that feeds the "N entries · time total" subtotal). **T** = number of ticked rows (ticks persist across pages and may include rows not currently shown). Numbers are formatted `N0` with `CultureInfo.InvariantCulture` (8921 -> "8,921"), never the OS culture. "row/rows": singular only for exactly 1.

### 1. Diary: Mark buttons (category row)

Placement: unchanged. Toolbar row 1 in the Diary card, left to right: `Select all` checkbox, tick label, three category buttons. Tag toolbar (row 2) stays below it. Nothing new on the page except the result bar (section 3), which sits directly BELOW the tag toolbar and above the diary list scroller, so the Mark buttons never move when it appears or disappears (no mis-click on a shifted button).

Behaviour change (the point of G3): the three category buttons are enabled whenever M > 0, with or without ticks. Without ticks they act on ALL M matches; with ticks (T > 0) they act on the ticked rows only. The button text always says which and how many, so a click on "all" is never a surprise; the confirm dialog (section 2) is the second guard.

| Control | Type | AutomationId | Visible text |
|---|---|---|---|
| Select all | CheckBox (three-state, unchanged) | `diary-select-all` | `Select all` (unchanged; it still ticks the rows currently shown; tooltip: `Ticks the rows shown. Mark without ticks already covers every match.`) |
| Tick label | TextBlock, 12 px, secondary (the existing `selectedLabel`) | `diary-tick-label` | T = 0: `None ticked`. T >= 1: `T ticked` (e.g. `5 ticked`). Hidden (empty) only while M = 0 |
| Mark on-plan | Button | `diary-mark-on` | see rules below |
| Mark off-plan | Button | `diary-mark-off` | same rules, word `off-plan` |
| Mark neutral | Button | `diary-mark-neutral` | same rules, word `neutral` |

Button text rules (X = `on-plan` / `off-plan` / `neutral`):

| State | Button text | Enabled | Tooltip |
|---|---|---|---|
| M = 0 (empty day / no match) | `Mark X` | no | `No entries to mark.` |
| T = 0, M = 1 | `Mark 1 X` | yes | `Marks the 1 entry matching the current view.` |
| T = 0, M >= 2 | `Mark all M X` (e.g. `Mark all 8,921 on-plan`) | yes | `Marks all M entries matching the current filters and search, not only the rows shown.` |
| T >= 1 | `Mark T ticked X` (e.g. `Mark 5 ticked on-plan`; T = 1: `Mark 1 ticked on-plan`) | yes | `Marks only the T ticked rows.` |
| Preview or write running | text unchanged | no (all three category buttons AND all tag buttons) | `Working...` |

Set `AutomationProperties.Name` to the visible text (it changes with the count), and find the buttons by the stable `AutomationId` in tests. Button text is recomputed on every render, tick change, filter/search change and after Mark/Undo (same place `UpdateMarkToolbar` runs). The 300-row cap text and the G2 "Showing N of M" row are unchanged; they describe what is listed, the buttons describe what Mark does.

Tag buttons (row 2, `Mark <tag>` / `Clear tag`): not part of the brief. Designed default: unchanged (enabled only with T >= 1, act on ticked rows, no dialog). Open question 1 asks whether they go through the same door.

Width: row 1 is about 100 + 80 + 3 x 180 = 720 px worst case, less than the existing tag row (7 buttons), so no wrap/clipping risk.

### 2. Confirm dialog (before EVERY bulk category mark)

Opened when a category Mark button is clicked, after the preview has been computed (preview = read-only count against the database, no write). While the preview runs the Mark buttons are disabled (section 1, running state); if it takes under about 300 ms nothing else shows, otherwise the bar from section 3 shows `Checking N rows...` (Informational, not closable). If the preview fails: error bar `Couldn't check those rows - nothing was changed.` + friendly reason; no dialog.

Built with `DialogControls.Build(XamlRoot, title, content, primaryButtonText, closeButtonText: "Cancel", defaultButton: ContentDialogButton.Close)` and shown through `DialogGate.ShowAsync`. Default button is Cancel, so Enter or Esc never writes; the user must click or Tab to the primary button.

Dialog AutomationId: `g3-confirm-dialog`.

Title: `Mark {total} rows as {X}?` (e.g. `Mark 7,200 rows as on-plan?`; total = 1: `Mark 1 row as on-plan?`). Total 0: `Nothing to change`.

Content: StackPanel, Spacing 10, MinWidth 420 (ContentDialog max is about 548 px; no scrollbar needed, content is about 230 px tall).

1. Scope line (TextBlock, 12 px, secondary, wrap) `g3-confirm-scope`. Texts:
   - no ticks: `All {M} entries matching your current view: {period}{filters}.`
   - ticks: `The {T} ticked entries.`
   - `{period}` = the day as `Thu 29 Oct 2026` or `All time`; `{filters}` = each active filter as ` - Category: Neutral`, ` - App: Chrome`, ` - Page: ...`, ` - Tag: Work`, ` - Search "chrome" in Window` (omit those not active). Long values trim with ellipsis, full text in tooltip.
2. Count table (Grid, 2 columns: label left `*`, number right-aligned `Auto`, row spacing 4, labels 14 px, numbers SemiBold). All four rows are always present, even at 0, so the layout and the UIA tree are stable:

| Row | AutomationId | Label | Number |
|---|---|---|---|
| 1 | `g3-confirm-filled` | `No category yet - will be filled` | filled |
| 2 | `g3-confirm-overwritten` | `Another category - will be overwritten` | overwritten |
| 3 | `g3-confirm-unchanged` | `Already {X} - stays as it is` | unchanged |
| divider | | 1 px line (`DividerStrokeColorDefaultBrush`) | |
| 4 | `g3-confirm-total` | `Rows that will change` (bold label) | total = filled + overwritten (bold) |

   Row 2 label and number turn SemiBold only when overwritten > 0 (weight, not a new colour). filled + overwritten + unchanged = scope size (M or T); the dialog never shows numbers that do not add up. Idle rows count as "another category" like any other (a category mark changes only the category; the window name stays, addendum 4).
3. Safety note (TextBlock, 12 px, secondary, wrap) `g3-confirm-note`: `Your diary is backed up first. You can Undo this right after, or later in Settings > Change history. Marking also teaches Planillium the activity keywords of these rows; Undo forgets the ones this mark added.`

Buttons:

| Button | Text | Enabled |
|---|---|---|
| Primary | `Mark {total} rows` (total = 1: `Mark 1 row`) | yes when total > 0; disabled when total = 0 |
| Close (default) | `Cancel` | always |

Total = 0 state: title `Nothing to change`; the table still shows (all zeros except unchanged), plus an extra line above the note: `Every one of these rows is already {X}.` Primary disabled. Cancel writes nothing (acceptance 2). Primary click closes the dialog and starts the write (section 3, running state).

### 3. Result bar (success, undo result, running, errors)

One `InfoBar` (`diary-result-bar`) per Diary page, placed below the tag toolbar, full width of the toolbar, `IsOpen` false until needed. It is a page-level state so a re-render (filter change, search typing, live refresh) re-shows the same bar with the same content until the user closes it, a new Mark/Undo replaces it, or the user leaves the Diary page (Settings > Change history is the lasting route). Only one message at a time.

| State | Severity | Title | Message | Action button | Closable |
|---|---|---|---|---|---|
| Running (write) | Informational | `Marking {total} rows as {X}...` | `Backing up your diary first.` | none | no |
| Success | Success | `Marked {N} rows as {X}` (N = 1: `Marked 1 row as {X}`) | `A backup was made. Undo puts the old categories back and forgets the keywords this mark added.` | `Undo` (`diary-result-undo`) | yes |
| Undoing | Informational | `Undoing...` | none | the Undo button stays, disabled, text `Undoing...` | no |
| Undone, nothing skipped | Success | `Undone: {R} rows restored` (R = 1: `Undone: 1 row restored`) | none | none | yes |
| Undone, some skipped | Success | `Undone: {R} rows restored` | `{S} rows were skipped because you edited them since.` (S = 1: `1 row was skipped because you edited it since.`) | none | yes |
| Undone, all skipped (R = 0) | Warning | `Nothing restored` | `All {S} rows were edited since, so they were left as they are.` | none | yes |
| Write failed (nothing written, all-or-nothing) | Error | `Couldn't mark those rows` | `Nothing was changed. {friendly reason}` | none | yes |
| Backup failed | Error | `Not marked - the backup failed` | `Your diary was left untouched. {friendly reason}` | none | yes |
| Undo failed | Error | `Couldn't undo` | `Nothing was changed. {friendly reason}` | none | yes |

Rules for the texts: X is `on-plan`, `off-plan` or `neutral`. Success and "Undone" texts never say that Undo leaves learned keywords (addendum 2, acceptance 8). The Undone message has R and S as separate numbers so R + S = N (acceptance 5). After a successful Undo or when the message is closed, the Undo button is gone and cannot be pressed twice. The existing `SaveErrorBar` stays for single-row saves; bulk errors use `diary-result-bar` so the reason is next to the buttons that caused it.

Ticks (addendum 3): the bar never touches ticks. After Mark and after Undo the same rows stay ticked: tick label and button counts keep showing `T ticked`; a tick disappears only when its row no longer exists or no longer matches the filters (then T drops, and the buttons and tick label recompute). The old code cleared ticks after every Mark; that line goes.

Focus: after the dialog closes focus returns to the Mark button that was clicked. The bar is not auto-focused (it is not modal); `Undo` is reached with Tab. Screen readers get the bar title through the InfoBar's own automation peer (assertive for Error/Warning).

### 4. Settings: "Change history"

Placement: a new section of the Settings section menu, directly under `Data` (last entry), because it is the lasting door to Undo and sits with the data actions. The menu is a `ListView` (`SectionList`), so the entry is a `ListViewItem` with `AutomationProperties.Name="Change history"` (set on the item, as the page requires, plus `AutomationId` `settings-section-change-history`), title `Change history` (SemiBold 13), summary line (11 px, tertiary) `Undo recent bulk changes`. The panel is a `StackPanel x:Name="ChangeHistoryPanel"` (`settings-change-history-panel`), Spacing 12, `Visibility` toggled like the other panels. Results of an Undo also go to the existing fixed-height `SaveStatus` strip (same texts as section 3: `Undone: 7,196 rows restored. 4 skipped (edited since).`).

Panel content, top to bottom:

1. Heading `Change history` (`SubtitleTextBlockStyle`).
2. Intro (TextBlock, wrap, secondary): `The last 20 bulk changes to your diary, kept for 30 days. Undo puts back the old values of one change; rows you edited since are left as they are.`
3. List `settings-change-history-list`: a vertical StackPanel (Spacing 8, at most 20 rows, newest first, no virtualization needed). Each row is a card Border (card fill, 1 px card stroke, CornerRadius 8, Padding 12) with a 2-column Grid (`*`, `Auto`), AutomationId `settings-change-row-{id}`:
   - Left StackPanel: line 1 (SemiBold 14, `settings-change-title-{id}`): same wording as the success bar, `Marked 7,200 rows as on-plan`. Line 2 (12 px, secondary): `29 Oct 2026, 14:32 - All time - Search "chrome" in Window - kept until 28 Nov` (date format as the rest of the app; the scope is the same string the confirm dialog showed; `kept until` = date + 30 days). Line 3 (12 px, tertiary, `settings-change-status-{id}`, only when undone): `Undone 29 Oct 2026, 14:40 - 7,196 rows restored, 4 skipped (edited since)`.
   - Right: Button `settings-change-undo-{id}`.
4. No pager, no filters; list ends after 20 rows (older ones are dropped by the 20 / 30 days rule, never shown greyed).

Row states:

| State | Undo button | Row |
|---|---|---|
| Available | `Undo`, enabled | normal |
| Running (this row) | `Undoing...`, disabled | normal; every other row's Undo is also disabled until it finishes |
| Undone | `Undone`, disabled | line 3 shown; row text unchanged (the row stays in the 20 until aged out, so history is a true log) |
| Undo failed | `Undo`, enabled again | line 3 replaced by `Couldn't undo - nothing was changed.` (error text in the secondary brush, with the friendly reason in a tooltip); `SaveStatus` also shows the reason |
| Undone with all rows skipped | `Undone`, disabled | line 3 `Undone 29 Oct 2026, 14:40 - nothing restored: all 7,200 rows were edited since` |

Panel states:

| State | What shows | AutomationId |
|---|---|---|
| Loading (reading the history file/table, normally instant) | one line `Loading...` (secondary), list hidden | `settings-change-loading` |
| Empty (nothing yet, or everything aged out) | TextBlock, secondary, wrap: `No changes yet. When you mark many diary rows at once, the change shows up here with an Undo button.` No placeholder rows | `settings-change-empty` |
| Error (history unreadable) | InfoBar Error, closable no: title `Couldn't read the change history`, message `{friendly reason}`; action button `Try again` (`settings-change-retry`) | `settings-change-error` |
| Populated | intro + list | |

Undo from this list has no confirm dialog (the result is shown and rows edited since are protected). Open question 2 asks whether to add one.

Clear actions delete backups (addendum 1): the two existing confirm dialogs get one more paragraph (TextBlock, 12 px, secondary, wrap, AutomationId `settings-clear-backups-note`), shown only when at least one backup file or history entry exists:
- "Clear activity history?" and "Clear all my data?": `This also deletes the {B} diary backup(s) kept for Undo and the Change history, so earlier bulk changes can no longer be undone.` (B from a count read when the dialog is built; when B = 0 but history entries exist: `This also clears the Change history.`)
- The explanatory footer of the Data panel gets one sentence appended: `Clearing activity history or all data also deletes the diary backups and the Change history.`
After either clear, the Change history list reads as the Empty state.

### Automation ids (all stable, for UIA)

Diary: `diary-select-all`, `diary-tick-label`, `diary-mark-on`, `diary-mark-off`, `diary-mark-neutral`, `diary-result-bar`, `diary-result-undo`.
Dialog: `g3-confirm-dialog`, `g3-confirm-scope`, `g3-confirm-filled`, `g3-confirm-overwritten`, `g3-confirm-unchanged`, `g3-confirm-total`, `g3-confirm-note`; the buttons are found by their text (`Mark N rows`, `Cancel`), which are the dialog's own PrimaryButton / CloseButton.
Settings: `settings-section-change-history`, `settings-change-history-panel`, `settings-change-history-list`, `settings-change-row-{id}`, `settings-change-title-{id}`, `settings-change-status-{id}`, `settings-change-undo-{id}`, `settings-change-loading`, `settings-change-empty`, `settings-change-error`, `settings-change-retry`, `settings-clear-backups-note`. `{id}` is the change id from the history store. The count table rows expose the number as the element's Name text (`"No category yet - will be filled: 1,200"`) so a test can read label and number from one element.

### State coverage (one place)

| Surface | Empty | Loading / running | Error | Disabled | Done |
|---|---|---|---|---|---|
| Mark buttons | M = 0: disabled, plain `Mark X` | preview / write: disabled with `Working...` tooltip | bulk error bar | M = 0 or running | text recomputed |
| Confirm dialog | total = 0: `Nothing to change`, primary disabled | (preview before it opens) | preview failure: bar, no dialog | primary when total = 0 | closes |
| Result bar | closed | Informational with no action | Error severities | Undo while undoing | Success / Warning, Undo removed |
| Change history | `settings-change-empty` text | `Loading...` | InfoBar + `Try again` | Undo while any Undo runs, and after Undone | rows with status line |

### Open questions for the planner / PM

1. **Tag marks** (`Mark Work`, `Clear tag`) are a bulk write to past data too. Designed default: unchanged (ticked rows only, no dialog, not in history). If they should use the same door, the same dialog works with these changes: title `Tag {n} rows as {tag}?`, rows `No tag yet / Another tag / Already {tag} / Rows that will change`, no keyword sentence, buttons `Tag N rows` / Cancel, success `Tagged N rows as {tag}`, and the tag buttons take the count text rule of section 1. Recommended: yes, but as a separate decision (not asked in the brief).
2. **Undo without confirm** from Settings (designed) vs one confirm dialog (`Undo this change? N rows go back to their old values; rows you edited since are skipped.`). Recommended: no confirm; the skip rule and the result message already protect the user.
3. **"No category yet"**: the brief and LESSONS say category is always set (unmatched = neutral). The dialog row exists as requested; the planner must say which stored value counts as "no category" (empty/NULL) and expect it to show 0 on most real data.
4. **Ticks beyond what is listed**: Select all ticks only the rows shown (unchanged). With T > 0 a user may have ticked rows that scrolled out of the 300/paged view; the count in the buttons is T (all ticks still valid), not the visible ticks. Confirm this is intended.

---

## Plan (planner, 2026-10-29)

Code read on this side only (`agentic/winui-agentic/Planillium.App`, side `CONTEXT.md`, `DECISIONS.md`, `context/`, `LESSONS*.md`). No real data was read or run. Line numbers are from the code at commit 1daa545 (before this build).

### 1. Requirement in my words

- The Diary's three category Mark buttons act on **every row that matches the current day/All-time + search + filters** (not only the 300 listed). If the user has ticked rows, they act on the ticked rows only. The button says which and how many.
- Every bulk write to past diary rows goes through **one service outside the page** (the "door"), in this order:
  1. preview counts;
  2. dated backup of the database file (keep 5);
  3. one all-or-nothing write that records each row's old value;
  4. a history entry that can be undone.
- Undo puts back the old values, but only for rows not edited since; it skips the rest and counts them. Undo also forgets the activity keywords that this one mark newly taught.
- History holds at most 20 changes, none older than 30 days. It can be undone from the Diary message and from Settings > Change history.
- Ticks survive Mark and Undo. A tick goes only when its row is gone or no longer matches.
- Marks must stay put: find and fix what removes them.
- Stays unchanged:
  - idle rows keep their window name "idle";
  - single-row edit, scores logic, income logic and other pages behave as before.
- Clear actions in Settings also delete all backups and the history.

### 2. Investigation

**(a) How Mark and the 300 cap work today** (`Pages/ReportsPage.Diary.cs`)
- Ticks are `var selectedIds = new HashSet<long>()`, a **local** of `BuildDiarySection` (line 172). The page mirror for MainWindow is `_diarySelectedCount` (line 127).
- `LoadScopeRows` (397-408) loads the whole retention window into memory when searching or in All time. Five predicates filter it (544-545), and the subtotal shows the full `filteredList.Count` (549-552).
- The cap (573-579): `if (wideRange && filteredList.Count > maxSearchResults) filteredList = filteredList.Take(300)`. Right after it (580-581), `lastRows = filteredList; selectedIds.RemoveWhere(id => !filteredList.Any(e => e.Id == id))`. So ticks are pruned against the **capped** list, and the check is O(n x m).
- `Select all` (679-699) ticks only `lastRows`, the capped rows.
- `MarkSelectedDiaryRows` (1004-1052):
  - Looks each ticked id up in `lastRows` (1026) and calls `db.UpdateDiaryEntry(id, row.Start, row.End, row.Dur, category, row.Desc, row.Tag)` (1029). That rewrites every field from the in-memory snapshot, not just the category.
  - Collects a keyword per row with `AppNames.Sub(window) ?? AppNames.Group(window)` (1030-1031).
  - After the commit it calls `ConfigService.LearnActivityRule` once per keyword, which is one full `config.json` write each (1035-1036), then `RestartTracker`, then `ScoreService.TryRecalculateDayScores` (1043).
  - Result: Mark can never reach match 301+.
- `MarkSelectedDiaryRowsTag` (1063-1091) has the same shape, without learning keywords or recalculating scores.
- **Writers of `time_diary.category`:**
  - bulk mark (Diary.cs:1029)
  - bulk tag, which passes the category through (1080)
  - `EditDiaryEntryDialog.cs:148`
  - Split, which deletes and re-inserts (`SplitDiaryEntryDialog.cs:258/267`)
  - tracker inserts (`DiaryWriter.LogSession`)
  - idle-answer delete+insert (`ActivityTracker.cs:262-272, 284-307`)
  - retention prune (`Database.cs:275-320`)
  - Clear actions (`Database.cs:424-437, 465-474`)
- **Writers of learned keywords:** `ConfigService.LearnActivityRule` (`ConfigService.cs:253-283`, called from Diary.cs:1036 and `TeachPlanTools.cs:27`) and Settings `SaveRemainingRules` (`SettingsPage.xaml.cs:998-1027`, which replaces `activity_rules` wholesale). `LearnActivityRule` removes the keyword from all three lists and then adds it to the target list, so a keyword that was in another list gets **moved**.

**(b) Why marks disappear.** Causes found in the code, most important first. Not confirmed against the user's real data, which is off limits.

1. **Marked idle placeholders get deleted (stored-row cause; fix in this build).**
   - `DiaryWriter.ClearIdlePlaceholder` (`DiaryWriter.cs:52-68`) runs `DELETE FROM time_diary WHERE date=$d AND window='idle' AND description IN ('unaccounted time','dismissed') AND <overlaps>`. It has **no category condition**.
   - `HasIdlePlaceholder` (79-93) has the same blind spot. `MainWindow.xaml.cs:283-291` uses it to reopen the idle prompt from the toast or tray recap ("Log it") at any later time.
   - When that prompt is answered or skipped (`IdleReturnDialog.cs:382/399/402-408` -> `ActivityTracker.LogIdleAnswer(s)` :267/:297), the row the user had marked (for example "unaccounted time" -> neutral) is deleted and replaced by a new row with the dialog's category. Skip writes `idle` back.
   - The user does work on these rows: a code comment cites their "unacc" search (2026-08-28). Addendum 4 keeps the window "idle" after marking, so a marked row stays matchable by this DELETE.
2. **Marks beyond the 300 cap were never written (perceived cause; fixed by item 1 of the request).** Select all + Mark on 8,921 matches marked only the 300 listed rows (Diary.cs:573-582, 682, 1026). Searching again shows the rest still unmarked.
3. **New rows do not follow marks (rules-level cause; report only, belongs to G9 "learned rules").**
   - Because `LearnActivityRule` moves keywords between lists, a later mark of another row with the same app or page silently reverses an earlier one's rule.
   - `ActivityClassifier.Classify` (`ActivityClassifier.cs:47-53`) tests on_plan first, then off_plan, else neutral, and never reads the `neutral` list. Any on-plan substring therefore beats an off-plan or neutral learned keyword.
   - Stored rows are not touched by this; only new rows come in "wrong". Not fixed here (out of scope per REQUEST).
4. **Retention prune (expected cause).** `PruneAndRollupDiary` (`Database.cs:275-320`) deletes rows older than the retention days. This is working as designed.

The single-row stale snapshot write (`UpdateDiaryEntry` with values from `lastRows`, Diary.cs:1029) can also clobber an edit made in another dialog since the list loaded. The door replaces it with a category-only update.

**(c) Why ticks vanish after Mark**
- `selectedIds.Clear()` runs after every Mark (Diary.cs:1050) and every tag mark (1089). This is the direct cause the user saw.
- Every full `Render()` creates a new empty set, because the set is local to `BuildDiarySection` (172). That covers Edit (1379-1384), Split (1394-1399), the All-time toggle, Clear filters, coming back to the page, and the day change.
- The prune at 581 runs against the capped list, so ticks on rows past 300 are dropped even though those rows still match.

### 3. Approach

**New service `Services/DiaryChangeService.cs`.** Plain C# with no WinUI types, so the test project can source-link it like `Database.cs`. Every bulk write to past diary rows uses it: category marks and tag marks now, G9 learned-rule rewrites later.

```
enum ChangeColumn { Category, Tag }
record ChangeRequest(ChangeColumn Column, string? NewValue, IReadOnlyCollection<long> RowIds, string ScopeText, bool TeachKeywords);
record ChangePreview(int Filled, int Overwritten, int Unchanged, int Missing) { int Total => Filled + Overwritten; }
record ChangeResult(long ChangeId, int Changed, IReadOnlyList<DateOnly> AffectedDates, bool KeywordsChanged);
record UndoResult(int Restored, int Skipped, IReadOnlyList<DateOnly> AffectedDates, bool KeywordsChanged);
record ChangeEntry(long Id, DateTime At, ChangeColumn Column, string? NewValue, string ScopeText, int RowCount, DateTime? UndoneAt, int? Restored, int? Skipped);

static class DiaryChangeService
  ChangePreview Preview(Database db, ChangeRequest req)
  ChangeResult  Apply(Database db, ChangeRequest req)      // throws BackupFailedException / write exception; nothing written on throw
  UndoResult    Undo(Database db, long changeId)
  IReadOnlyList<ChangeEntry> History(Database db)          // prunes first, newest first
  (int Backups, int Entries) Counts(Database db)           // for the Clear dialogs
  void DeleteAllBackupsAndHistory(Database db)             // Clear actions
  Func<DateTime> Now = () => DateTime.Now;                 // test seam for the 30-day rule
```

- **Preview.** Reads the current value of each id **fresh from the database**, in chunks of 500 ids per `IN (...)`. It never uses the page snapshot.
  - filled = stored value is null, empty or whitespace (the G2 "-" definition: `DiaryFilter` no-value).
  - unchanged = equal to the new value (category: ordinal; tag: same as the tag filter's comparison).
  - overwritten = anything else.
  - Missing = ids no longer in the table. They are not in the dialog; they are dropped from the request.
- **Apply.** Steps in order:
  1. **Backup.**
     - `SqliteConnection.BackupDatabase` from the live connection to `<dir of AppPaths.DbPath>/backups/progress-yyyyMMdd-HHmmss.db`. If two backups land in the same second, append `-2`, `-3`.
     - SQLite online backup is consistent under WAL; `File.Copy` of a WAL database is not.
     - Then delete all but the newest 5 `progress-*.db` files, sorted by name (which is by date).
     - Any exception means `BackupFailedException` and **nothing is written**.
  2. **One transaction** (`db.RunInTransaction`):
     - Re-read each row.
     - Skip rows already equal to the new value.
     - `UPDATE time_diary SET <column>=$v WHERE id=$id` with one prepared command reused. This is a new `Database.UpdateDiaryColumn` that touches only that column. `window` is never written, which covers addendum 4.
     - Insert one `change_history` row and one `change_history_rows` row per changed row (old value, new value, fingerprint of the row **after** the write).
     - If `TeachKeywords` is set, insert `change_history_keywords` rows (computed in step 3a).
  3. **Keywords** (category marks only).
     - a. Inside the transaction, before the commit: collect keywords exactly as today (`AppNames.Sub(w) ?? AppNames.Group(w)`, excluding `"—"`, empty, and `AppNames.Browsers`, as `LearnActivityRule` does), from the changed rows only. Read the current `activity_rules` from `ConfigService.Root` (read only). For each keyword record `prior_list` (on_plan / off_plan / neutral / null if absent, case-insensitive). Rows where the keyword is already in the target list are **not** recorded and not taught (nothing to undo).
     - b. After the commit: write all recorded keywords with ONE new `ConfigService.LearnActivityRules(IEnumerable<string>, category)`. It is a single `Mutate`, the same move semantics as `LearnActivityRule`, and replaces the per-keyword file writes that would mean thousands of writes for "Mark all".
     - If this config write fails, the DB change stands. The bar shows success plus a log entry, the same as today's after-commit learning. Undo is still safe because it only removes keywords that are present (see Undo).
  4. **History pruning** (see 3.4), then return the result.
  5. The page, not the service (WinUI), calls `RestartTracker()` when `KeywordsChanged`. The service calls `ScoreService.TryRecalculateDayScores(db, dates, "DiaryChange.Apply")` for category changes (DECISIONS rule 10). Tag changes do not recalculate; tags never feed the score, and income is computed from tags when read, with nothing stored.
- **Undo(changeId).** Refused if the change is already undone.
  1. Backup first (same as Apply; Undo is a bulk write, REQUEST item 4 "every bulk change").
  2. One transaction:
     - For each `change_history_rows` row: if the diary row is missing, or its current fingerprint differs from the stored one, it is **skipped**.
     - Otherwise set the column back to `old_value` (category-only / tag-only update).
     - Store `undone_at`, `restored` and `skipped` on the history row.
  3. After the commit, keywords, in one `Mutate` (`ConfigService.UndoLearnedRules(records, taughtCategory)`). For each recorded keyword that is **still** in the taught list:
     - prior null: remove it (a newly taught keyword is forgotten);
     - prior another list: move it back there (the pre-existing rule is left as it was before the mark).
     - A keyword the user has since moved or deleted (no longer in the taught list) is left alone.
     - Pre-existing keywords in the same list were never recorded, so they are never touched (acceptance 8).
  4. Recalculate scores for the restored rows' dates (category). RestartTracker happens on the page.
- **"Edited since" = row fingerprint.**
  - A deterministic string `date|start|end|dur|category|window|desc|tag` (null as `\0`) of the row as it was right after this change wrote it, stored as SHA-256 hex.
  - It detects any single-row edit (time, description, category, tag) and any later bulk change.
  - Split deletes and re-inserts the row, so it counts as missing, which is skipped.
  - An edit that ends exactly where the mark left the row is not detected. That is harmless, because the row is in the state this change produced, so restoring is what the user asked for.
  - Skipped count = missing + fingerprint differs. The designer's text says "edited since"; a deleted row is reported inside that number (see Open question 6).
- **3.4 History storage and retention.** History lives in the same `progress.db`, so it commits atomically with the write. Add to `EnsureSchema` (`CREATE TABLE IF NOT EXISTS`):
  - `change_history(id INTEGER PRIMARY KEY AUTOINCREMENT, ts TEXT NOT NULL, kind TEXT NOT NULL /*category|tag*/, new_value TEXT, scope_text TEXT NOT NULL, row_count INTEGER NOT NULL, undone_at TEXT, restored INTEGER, skipped INTEGER)`
  - `change_history_rows(change_id INTEGER NOT NULL, diary_id INTEGER NOT NULL, old_value TEXT, new_value TEXT, fingerprint TEXT NOT NULL)` + index on `change_id`
  - `change_history_keywords(change_id INTEGER NOT NULL, keyword TEXT NOT NULL, taught_list TEXT NOT NULL, prior_list TEXT)`
  - Pruning, on every Apply and every History load:
    1. delete entries with `ts < Now() - 30 days`;
    2. then delete all but the newest 20 by id;
    3. delete child rows of the removed ids.
  - These tables are **not** added to `ExportedTables` (export output unchanged). They are emptied by `ClearActivityHistory` and `ClearAllData`.
- **Backups and Clear** (`Pages/SettingsPage.xaml.cs`).
  - `ClearHistory_Click` (1296-1372) and `ClearAllData_Click` (1508-1553) also call `DiaryChangeService.DeleteAllBackupsAndHistory`, inside the same `RunClearActionAsync` work.
  - The early return `"No activity history to clear."` (when diary rows = 0 and rollup days = 0) must also require backups = 0 and history entries = 0. Otherwise backups could never be cleared on an empty diary.
  - The designer's `settings-clear-backups-note` uses `Counts()`.
- **Page wiring** (`Pages/ReportsPage.Diary.cs`).
  - **Ticks** become a page field `private readonly HashSet<long> _diaryTicks`. It replaces the local at 172 and survives `Render()` and navigation within the run, since the page is cached.
    - On every results render, prune against the **uncapped** filtered list: build a `HashSet<long>` of its ids before the cap and `RemoveWhere(id => !ids.Contains(id))`.
    - Prune only after a successful load (an error or empty load keeps today's 568-570 behaviour: empty result means no matches, so clear).
    - Delete `selectedIds.Clear()` at 1050 and 1089.
    - `_diarySelectedCount` keeps mirroring `_diaryTicks.Count`.
    - Select all: unchanged semantics. Checked ticks the rows shown; Unchecked clears all ticks.
  - **Targets.** With T = 0, the ids of the full uncapped filtered list (M). With T > 0, `_diaryTicks`; these are all valid matches after the prune, including ticks on rows past the cap or on other G2 pages (lead ruling 4).
  - Keep `allFiltered` (uncapped) next to `lastRows` (capped) so the buttons and targets use M.
  - Button texts, tooltips and AutomationIds as in Screen design section 1, recomputed in `UpdateMarkToolbar`.
  - Click flow:
    1. Disable the category and tag buttons.
    2. Run `Preview` on a background thread (`Task.Run`; the DB is opened per call).
    3. Show the `g3-confirm-dialog`.
    4. Cancel means stop, nothing written.
    5. Primary runs `Apply` on a background thread.
    6. Open `diary-result-bar` (page-level state) and re-render the results.
    7. On `KeywordsChanged`, `RestartTracker()` on the UI thread.
  - Undo in the bar calls `Undo` the same way. The bar's error states map to `BackupFailedException` / other exceptions. `SaveErrorBar` stays for single-row saves only.
  - Scope text for history and the dialog: built by the page from the period and active filters (designer section 2), passed in as `ScopeText`.
- **Tag buttons (lead ruling 1: through the door).**
  - Same service with `Column = Tag`, `TeachKeywords = false`.
  - Enabling stays as today: ticked rows only, T >= 1. Extending them to "all matches" is not asked; see Open question 1.
  - Dialog: the designer's variant (open question 1 of Screen design): title `Tag {n} rows as {tag}?`, rows `No tag yet - will be filled / Another tag - will be overwritten / Already {tag} - stays as it is / Rows that will change`, no keyword sentence, primary `Tag N rows`, success `Tagged N rows as {tag}`.
  - **Mine:** `Clear tag` uses title `Clear the tag of {n} rows?`, rows `No tag yet` (always 0, shown for a stable layout), `Has a tag - will be cleared`, `No tag - stays as it is`, `Rows that will change`, primary `Clear {n} tags`, success `Cleared the tag of {N} rows`.
  - History title in Settings uses the same success wording.
  - Size: small to moderate. The door already takes a column, the fingerprint covers tags, and there are no keywords or scores. Risk is low because it removes the stale whole-row write at 1080.
- **Idle placeholder fix (cause b1)** (`Services/DiaryWriter.cs`, `Services/ActivityTracker.cs`).
  - Add `AND category = 'idle'` to the WHERE of both `ClearIdlePlaceholder` and `HasIdlePlaceholder`. A placeholder the user has marked then counts as answered: it is never deleted and never re-prompted.
  - Edge: an idle answer whose gap overlaps only a *marked* placeholder (a prompt already open while the user bulk-marks) would now add a second row for the same minutes.
  - Default: in `LogIdleAnswer` / `LogIdleAnswers`, if no `idle`-category placeholder overlaps but a marked one does, skip the insert and `Log.Info` it (Open question 4).

**Rejected alternatives**
- *Write path inside the page, as today, plus a history list:* rejected. The brief requires one reusable service, and it could not be source-linked into tests.
- *"Edited since" via a new `time_diary.edited_seq` / `updated_at` column bumped by every writer:* rejected. It is a schema change on the user's main table, and every writer (Edit, Split, tracker, idle answer, door) must remember to bump it. A missed writer silently makes Undo overwrite edits. The fingerprint needs no change to existing writers.
- *Comparing only the category:* rejected. It misses time, description and tag edits and back-and-forth bulk marks.
- *History in a JSON file next to the DB:* rejected. It is not atomic with the diary write, so a crash between the two leaves rows changed with no record to undo them.
- *File.Copy backup:* rejected. Unsafe under WAL (the -wal file holds uncommitted pages).
- *Lifting the 300 cap now:* not done; see Open question 2.

### 4. Files to change

| File | Change |
|---|---|
| `Planillium.App/Services/DiaryChangeService.cs` (new) | the door: Preview / Apply / Undo / History / Counts / DeleteAllBackupsAndHistory, backups, fingerprints, retention |
| `Planillium.App/Services/Database.cs` | three history tables in `EnsureSchema`; `UpdateDiaryColumn(id, column, value)` (category or tag only, column from a fixed whitelist, never interpolated from input); history CRUD helpers; `ClearActivityHistory` + `ClearAllData` also empty the history tables; a count for the Clear dialogs. `UpdateDiaryEntry` unchanged (single-row edit) |
| `Planillium.App/Services/ConfigService.cs` | `LearnActivityRules(keywords, category)` (one Mutate) and `UndoLearnedRules(records)` (one Mutate). `LearnActivityRule` stays for TeachPlanTools |
| `Planillium.App/Services/DiaryWriter.cs` | `category = 'idle'` guard in `ClearIdlePlaceholder` and `HasIdlePlaceholder` |
| `Planillium.App/Services/ActivityTracker.cs` | `LogIdleAnswer(s)`: skip the insert when only a marked placeholder overlaps (Open question 4) |
| `Planillium.App/Pages/ReportsPage.Diary.cs` | tick field + uncapped prune; remove both `Clear()`; M/T button texts; door wiring for category and tag buttons; confirm dialog; result bar logic; delete `MarkSelectedDiaryRows`/`MarkSelectedDiaryRowsTag` bodies in favour of the door |
| `Planillium.App/Pages/ReportsPage.xaml` (or built in code like the toolbar) | `diary-result-bar` InfoBar under the tag toolbar |
| `Planillium.App/Pages/SettingsPage.xaml` + `.xaml.cs` | "Change history" SectionList item + `ChangeHistoryPanel` (add it to `_panels` at line 39 in the same order as the items, or the page throws); Undo handler; after an Undo, reload the Keywords panel text (`LoadRules`) so a later Save there does not write back the forgotten keywords; Clear dialogs note + backup/history deletion + the early-return fix; Data footer sentence |
| `Planillium.App.Tests/*.csproj` | `<Compile Include="..\Planillium.App\Services\DiaryChangeService.cs" Link=...>` only (QA writes the tests) |
| Docs (coder) | `CHANGELOG.md` Unreleased, `MANUAL.md` (Mark, Undo, Change history, backups), `context/todos.md`, `CONTEXT.md`, `DECISIONS.md` (this side's rule for the door + lessons) |

Not touched: `EditDiaryEntryDialog`, `SplitDiaryEntryDialog`, `ScoreService`, `IncomeService`, `ActivityClassifier`, other pages.

### 5. Edge cases

- **M = 0:** buttons disabled. **Total = 0** in preview: `Nothing to change`, primary disabled, no backup, no history entry.
- **Rows deleted between preview and Apply** (prune, Split, Delete): Apply re-reads and skips them. The bar shows the real `Changed` count, which may be below the dialog total.
- **A row edited between preview and Apply:** Apply writes only the column, so the other edits survive. Its old value is the one read inside the transaction.
- **Tracker inserting while Apply runs:** new rows are not in the id list. `busy_timeout` 2000 covers lock waits. The backup runs before the transaction, so it can miss a row inserted in between; that row is not part of the change either.
- **Very large changes** (8,921 rows): one transaction with a prepared command; chunked `IN` for preview; one config write. The UI stays responsive because the work runs off the UI thread.
- **Backup folder missing:** create it. **Disk full / locked:** backup failed, nothing written.
- **Same-second backups:** suffix. **Foreign files in `backups/`:** only `progress-*.db` is counted or deleted.
- **Undo twice / Undo of an undone change:** refused, button disabled. **Undo of an aged-out change:** not listed, cannot be called.
- **Two changes on the same rows, undo the older first:** its rows were changed by the newer one, so they are skipped (fingerprint). Undo the newer first, then the older: both restore.
- **Undo after a retention prune or Clear:** rows missing, so skipped. After Clear, history is empty anyway.
- **Undo while the Diary is open:** the bar or Settings triggers a reload. Ticks survive per addendum 3; a tick is dropped only if the restored value no longer matches the filter (acceptance 9).
- **Ticks:** Select all on page 2 adds that page's rows. Ticks on other days drop when the period changes (they no longer match). App restart forgets ticks (memory only).
- **Keyword edge:** two rows in one mark giving the same keyword are recorded once. A keyword moved from off_plan to on_plan by the mark returns to off_plan on Undo. A keyword the user changed in Settings after the mark is left alone. Marking idle rows teaches `AppNames.Group("idle")` exactly as today (no new rule; Open question 5).
- **Idle rows:** a category mark changes only `category`, never `window` (column-only UPDATE). A marked placeholder is no longer deleted or re-prompted.
- **Filled = 0 in practice:** `time_diary.category` is `TEXT NOT NULL` and every writer sets one of the five values. The "No category yet" row will show 0 unless older data holds `''` (Open question 3).
- **Number formatting:** `N0` invariant (designer).

### 6. Rules applied and how they are covered

| Source | Rule | Covered by |
|---|---|---|
| side CONTEXT / brief | real data off limits | spec only; QA uses a scratch `MENTOR_ROOT` |
| DECISIONS rule 10 | a bulk mark recalculates the score of every affected day | Apply and Undo call `TryRecalculateDayScores` for category changes |
| DECISIONS G2 "-" rule | no value = null/empty/whitespace | used for "filled" (category and tag) |
| DECISIONS G2 300-cap lead ruling | cap stays, Select all reaches the listed rows | cap kept; Mark now reaches M by not depending on the list. Conflict with addendum "Show all reaches every row": Open question 2 |
| DECISIONS G5 lessons | virtualized rows, no full rebuild per change | result bar and button text update without a full `Render()`; a tick change does not rebuild rows |
| LESSONS: hidden caps | find every cap between data and action | the 300 cap at 573-579 and the tick prune at 581 are both handled |
| LESSONS: trace capture -> storage -> read | every writer of the column | writer list in 2(a); this found cause b1 |
| LESSONS: no-value placeholder check | "-" / empty | filled definition |
| LESSONS: strict build | `-warnaserror --no-incremental` | precheck by the coder |
| Playbook `change-data-rule.md` | date-validity data rules | not applicable (no date-validity rule changes) |
| REQUEST "DECISIONS.md rule 19" | not in this side's DECISIONS.md | Open question 7 |

### 7. Acceptance (copied verbatim from ACCEPTANCE.md)

1. With a filter matching more rows than shown (scratch data, e.g. 2 400 rows, 300 shown), Mark changes every match; the button text shows the real count.
2. The confirm dialog numbers are correct: filled + overwritten = total changed; unchanged excluded; Cancel writes nothing.
3. After Mark, the marks are still there after leaving and re-opening the Diary and after restarting the app (scratch). The report states the cause of the old disappearing marks.
4. A dated backup of the diary database is made before each bulk write; only the last 5 are kept.
5. Undo (message and Settings -> Change history) restores the exact old values; rows edited since are skipped and counted in the message.
6. History keeps the last 20 changes for 30 days; older are dropped.
7. Single-row marking, scores and income are unchanged. Strict build and existing suite green.
8. Undo of a category mark removes the keywords that mark newly taught and leaves every pre-existing keyword rule; the message no longer says Undo leaves them.
9. Tick 5 of 12 rows, Mark: afterwards the same 5 rows are still ticked (also after Undo); with a filter that makes a marked row stop matching, only that row loses its tick.

Planner's own checks (mine, not from ACCEPTANCE.md):
- P1. A marked "unaccounted time" row survives answering or skipping the idle prompt for its gap from the toast / tray recap.
- P2. Tag marks show the dialog, appear in Change history and undo.
- P3. Clear activity history with an empty diary but existing backups still deletes the backups.
- P4. After an Undo from Settings, the Keywords panel shows the forgotten keywords gone.

### 8. Open questions

1. **Tag buttons on all matches?** They now go through the door (ruling 1), but stay "ticked rows only". Should they also act on all M without ticks, like the category buttons? Not asked, so not built.
2. **Show all vs the 300 cap.** Addendum 1 says "Show all (G2) reaches every row". The G2 lead ruling (DECISIONS) keeps the 300 cap, and ACCEPTANCE 1 itself assumes "300 shown". Default: the cap stays; Mark reaches every match anyway. Should Show all lift the cap? G5's virtualized list makes that feasible.
3. **"No category yet" will show 0.** `category` is NOT NULL and always set, so "filled" is defined as null/empty/whitespace and expected to be 0 on real data. Confirm the row should still show.
4. **Idle answer over a marked placeholder:** default is to skip the insert (no double minutes). Alternative: replace the marked row as before. Confirm.
5. **Bulk keyword volume.** "Mark all 8,921 neutral" can teach thousands of keywords and move existing on/off-plan keywords into neutral. Undo reverts that, but until then new rows follow the moved rules. Should bulk marks without ticks teach keywords at all, or ask in the dialog? Kept as today (teach) since not asked. Related: marking idle rows teaches the keyword "idle" today; kept.
6. **"Skipped" includes deleted rows** (Split, prune, Delete) with "edited since" rows, under the designer's wording "edited since". Separate count or wording `edited or deleted since`?
7. **"DECISIONS.md rule 19"** cited by REQUEST does not exist in this side's DECISIONS.md (the root file is off limits). The rule text is missing; the coder writes this side's door rule from REQUEST/BRIEF.
8. **Backups vs retention / privacy.** Backups hold full window titles and can outlive the retention prune by up to 5 bulk changes. Should backups older than the retention days (or 30 days, like history) be deleted too?
9. **Keyword "move back".** For a keyword that existed in another list before the mark, Undo moves it back (my reading of "leaves every pre-existing keyword rule"). Confirm.
10. **Classifier precedence / keyword moves (cause b3)** are left for G9 learned rules. Confirm that is where they belong.

#### User answers (2026-10-29, round 2; BRIEF "Addendum 2" is authoritative)

- **Answer 1 -> Q1 (tag buttons).** Yes: the tag buttons (each tag, Clear tag) act on ALL filter matches when nothing is ticked, ticked rows otherwise, through the same door (preview, confirm dialog, backup, history, Undo). Button text follows the category rule: `Mark all 8,921 Routine`, `Mark 5 ticked Routine`, `Mark 1 Routine`; `Clear tag of all 8,921`, `Clear tag of 5 ticked`, `Clear tag of 1`; M = 0 keeps `Mark Routine` / `Clear tag`, disabled. The tag row sits in a sideways-scrolling strip because the longer texts can outgrow the page.
- **Answer 2 (new question).** Undo from Settings > Change history asks first: dialog `Undo this change?` (title wording of the change, rows go back, rows edited or removed since are skipped, backup first, keywords this mark taught are forgotten), buttons `Undo` / `Cancel`, Cancel default. The Diary result bar's Undo stays one click (not asked). Replaces the designer's "no confirm" default.
- **Answer 3 -> Q2 (Show all vs the 300 cap).** "Show all" lifts the cap and lists every match. Until it is pressed the cap and its notice stay (the notice now says Show all lists every match). `DiaryList` takes the full list plus the cap: "Show N more" stops at the cap, "Show all" (sticky) goes to M; rows stay virtualized (G5 ItemsRepeater). Select all ticks the rows the list can show (`lastRows`): capped list before Show all, every match after (a callback refreshes it and the checkbox state). Show all resets with the scope as before.
- **Answer 4 -> Q5 (keywords).** A mark with nothing ticked does NOT teach activity keywords (`TeachKeywords` false: no `change_history_keywords` rows, so Undo has nothing to forget and the bar/dialog texts say so). A category mark of ticked rows still teaches; its Undo forgets only the keywords it added (unchanged). Tag marks never teach.
- **Answer 5 -> Q8 (backups).** No expiry; each new backup replaces the oldest, last 5 kept (as built, no code change). Settings > Change history shows an always-visible note (`settings-change-backup-note`): the last 5 backups are kept, a new one replaces the oldest, they do not expire, backups hold full window titles.
- **Defaults accepted by the user:** Q3 ("No category yet" row shows, expected 0), Q4 (idle answer over a marked gap skips the insert), Q6 (skipped count includes removed rows; wording "edited or removed since"), Q7 (this side's rule is DECISIONS rule 17), Q9 (Undo moves a keyword back to its prior list), Q10 (classifier precedence / keyword moves stay with G9).
