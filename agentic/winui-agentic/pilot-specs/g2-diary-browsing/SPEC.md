# SPEC -- G2 Diary browsing (agentic side)

Target: `agentic/winui-agentic/`. Build on the current code (after B1i). Tests deferred by the BRIEF addendum:
strict build + existing suite, then STOP; no QA started by the builder.

## Requirement (in my words)

On Reports -> Time diary:
1. Each filter list (Category, App, Page, Tag) gets a "-" entry meaning "this row has no value in this field".
   It combines with the other entries of the same list (OR) and with the other filters/search (AND), as now.
2. A small chooser beside the search box picks what the search looks in: All fields (default, exactly today's
   matching), App, Page, Description, Category, Tag.
3. The "Show 50 more" link becomes a row of links: Show 50 / 100 / 500 / 1000 more (only those smaller than the
   number still hidden) and "Show all". A "Showing N of M" line tells how many rows are on screen out of how many
   are in the list.
4. Investigate only: why the App list holds window titles. Findings below; no code, no stored data, nothing
   filtered out.
Not changed: marking (Mark on/off/neutral, tag buttons, Select all code), scoring, recording of diary rows.

## Rules that apply (from CONTEXT.md / DECISIONS.md / LESSONS.md) and how they are covered

- Diary state survives navigation and rebuilds (statics; DECISIONS "state that must survive a rebuild has to live
  outside the thing being rebuilt"): the new search-field choice and the "Show all" choice are statics next to
  `_diarySearch` / `_diaryRowsShown`; see "State".
- Live refresh every 30 s must not reset the user's paging (comment on `_diaryRowsShown`): scope key extended,
  "Show all" made sticky; see Paging.
- Faceted filters: each list is built from rows matching every OTHER filter (Diary.cs 387-395) -- kept; "-" uses the
  same single predicate for list and results so they cannot disagree.
- DECISIONS rule 10 (day-off rows still visible in the raw Diary) -- untouched: no row is hidden by this change.
- Real data off limits; repo public -- the item 4 examples are explained from code only; no DB read.
- LESSONS "check every sibling": the same title-as-app grouping feeds Time by App and Top Distractions and the
  activity-rule learning; listed in item 4 (not changed).
- LESSONS "a page-level key handler ... / exits": no new text box; the chooser is a ComboBox, no key handling added.
- Builders don't write tests (project CLAUDE.md); pure logic goes into `Services/DiaryFilter.cs`, already linked
  by the test project, so QA can unit-test it later.

## Chosen approach

Keep the existing structure (statics + `RenderDiaryResults` closure + `DiaryFilter` pure helpers). Add:

### 1. "-" entry (no value)

- `DiaryFilter.NoValue` = a sentinel string that no real value can equal (e.g. `"\u0000none"`), and
  `DiaryFilter.NoValueLabel = "-"`.
- `DiaryFilter.Matches(selected, value, cmp)`: if `value` is null, empty or whitespace, return
  `selected.Count == 0 || selected.Contains(NoValue)`; otherwise unchanged. (Existing test
  `Matches_NullValue_NeverMatches` keeps passing: its set holds no sentinel.)
- Exact "no value" per field (the value the filter already compares):
  | Field | Value compared | "-" matches when |
  |---|---|---|
  | Category | `e.Cat` | null / empty / whitespace |
  | App | `AppNames.Group(e.Window)`, with the placeholder `"—"` mapped to null | window null/empty, or the title normalises to nothing (Group returns "—") |
  | Page | `AppNames.Sub(e.Window)` | null / empty |
  | Tag | `e.Tag` | null / empty |
  Mapping App "—" -> null is required: `Group` never returns null, it returns "—" for an empty title, and that is
  what the row's App column shows.
- Lists: "-" is the first checkbox after "Clear" in all four flyouts, always present (fixed lists and the dynamic
  App/Page lists alike), `Content = "-"`, `Tag = DiaryFilter.NoValue`, AutomationProperties.Name
  "- (no category)", "- (no app)", "- (no page)", "- (no tag)".
- App list: the data-derived "—" option disappears (null after mapping, filtered out like Page already filters
  nulls) -- "-" replaces it, so there are never two "empty" entries.
- `PruneToAvailable` must not drop the sentinel: pass `appsInView`/`pagesInView` plus `NoValue` as "available"
  (or exempt `NoValue` inside `PruneToAvailable`). `SameOptionSet` compares the real options only.
- Button label: selected labels in display order with "-" first -> "-", or "- +1" (existing `ButtonLabel`).
- Sync functions (`Sync*Checkboxes`) work unchanged (they compare `Tag` strings against the sets).
- Scope key already serialises the four sets, so choosing "-" resets paging like any filter change.

### 2. Search field chooser

- New static `_diarySearchField` (enum or string const in `DiaryFilter`): All (default), App, Page, Description,
  Category, Tag. Survives navigation.
- ComboBox placed in one horizontal row with the search box (chooser first, box stretches), items in that order,
  labels "All fields", "App", "Page", "Description", "Category", "Tag"; AutomationProperties.Name
  "Search in". Selected item seeded from the static on every build.
- Predicates (q = trimmed text, OrdinalIgnoreCase contains, empty q = matches all as now):
  - All fields: the CURRENT expression in `MatchesSearch` verbatim (Label(window) OR desc OR cat with '_'->' ' OR
    tag label). Default = current behaviour, unchanged.
  - App: `AppNames.Group(e.Window)` contains q.
  - Page: `AppNames.Sub(e.Window)` contains q (null = no match).
  - Description: `e.Desc` contains q.
  - Category: `e.Cat.Replace('_', ' ')` contains q (same text All fields uses).
  - Tag: `DiaryTag.LabelOf(e.Tag)` contains q.
  Put the field predicate in `DiaryFilter` (pure, testable) taking the already-computed strings, or keep it in the
  closure -- builder's choice, but one definition used by both the option lists and the results (as now).
- Changing the chooser: if q is non-empty, call `RenderDiaryResults()` at once (no debounce); if q is empty nothing
  visible changes. No full `Render()` needed (header depends only on whether text is present).
- "Clear filters" also resets the chooser to All fields (it already clears the search text; the chooser reads as
  part of the same row). See Open question 3.
- Scope key gains the search field, so switching field resets paging.

### 3. Paging

- Increments `{50, 100, 500, 1000}` (one constant array). After the shown rows, one horizontal row holds:
  "Showing N of M" (dim text), then a link "Show k more" for each k with k < hidden, then "Show all (hidden)".
  If hidden <= 50 only "Show all (hidden)" appears. Nothing appears when hidden = 0.
- M = number of rows in the list handed to `DiaryList` (`filteredList.Count` after any cap, see Open question 1);
  N = rows built. The subtotal line above ("X entries · total") is unchanged.
- Each click builds the next rows in place (as now), sets `_diaryRowsShown`, rebuilds the paging row.
- "Show all" also sets a new static `_diaryShowAll = true`; while true, a same-scope rebuild (live refresh adds a
  row at the top) shows all rows instead of leaving the newest bottom row hidden. Reset to false wherever
  `_diaryRowsShown` resets (scope change).
- Rejected alternative: a single "Rows per page" ComboBox (50/100/500/1000/All). Rejected: the request says
  "next to Show 50 more", i.e. links that add rows, and a ComboBox would also change the existing reveal model the
  live-refresh fix depends on.

### Rejected alternative (filters)

Show "-" only when some row in view lacks a value (faceted like App/Page). Rejected: ACCEPTANCE 1 says each list
HAS a "-" entry; a disappearing entry would also be pruned from the selection mid-session.

## State (after returning to the page and together)

All new state is static next to the existing diary statics: `_diarySearchField`, `_diaryShowAll`. Returning to
Reports (`OnNavigatedTo` -> `Render` -> `BuildDiarySection`) rebuilds the controls from the statics: filters incl.
"-", chooser, search text, revealed row count (scope key unchanged -> count kept). Filters AND search field AND
paging all feed the one scope key, so any change to any of them restarts paging at 40 and nothing else.

## Files to change

- `Planillium.App/Services/DiaryFilter.cs` -- `NoValue`, `NoValueLabel`, `Matches` empty-value rule, sentinel kept
  by `PruneToAvailable`, search-field enum/predicate (optional), paging increments + "which links to offer" helper.
- `Planillium.App/Pages/ReportsPage.Diary.cs` -- "-" checkbox in the four list builders, App "—" mapping, chooser
  in `BuildDiarySearchBox` (or a new row builder) + handler, `MatchesSearch` by field, scope key, Clear filters,
  paging row in `DiaryList`, `_diaryShowAll`.
- Docs in the same pass: `context/todos.md` entry, `CONTEXT.md` §2 line. No `CHANGELOG.md`/`MANUAL.md` on this side
  unless the lead says otherwise.
- Not touched: `AppNames.cs`, `WindowTitleResolver.cs`, `ActivityTracker.cs`, `DiaryWriter.cs`, `Database.cs`,
  `ReportData.cs`, marking methods, tests.

## Edge cases

- Category is `NOT NULL` in the schema and the tracker always writes one (unmatched -> `neutral`), so Category "-"
  normally lists nothing ("No entries match ..."). Correct per the rule; see Open question 2.
- Rows with category `dayoff` (not in the list) are not "-" (they have a value).
- Tag values not in `DiaryTag.Options` (legacy/removed paid tag) show "—" in the row but are NOT "-" (value
  present). Open question 2.
- Page "-" includes rows whose Page column shows the description in italics (idle rows answered by the user): the
  filter compares `Sub`, not the displayed text -- same as today's Page filter.
- A title literally "—" groups as no value (same as its display today).
- "-" together with real entries in one list: OR (e.g. App "-" + Chrome).
- "-" chosen, then the day changes so no row lacks a value: entry stays checked (not pruned), list says no match.
- All fields uses `AppNames.Label` cut at 60 chars (current behaviour, kept); App/Page single-field search uses the
  uncut values, so a Page search can find a long page name that All fields misses. Kept as is (default must equal
  current behaviour); noted for the user.
- Hidden exactly 50 / 100 / 500 / 1000: that increment is not offered (k < hidden), "Show all" covers it.
- "Show all" on thousands of rows (All time, no cap) freezes the UI for a while (non-virtualising StackPanel).
  User-chosen; acceptable, noted.
- Live refresh while N < M: count kept (existing behaviour); with "Show all": stays all.
- Selection: Select all keeps selecting every row in the list (shown or not), as now.

## Acceptance (verbatim from ACCEPTANCE.md)

1. Each of the four filter lists has a "-" entry; choosing it lists exactly the rows with no value for that field;
   combining it with other filters works.
2. The search field chooser lists each searchable field plus "all fields"; searching a field matches only that field.
3. Paging offers graded increments up to "Show all"; counts shown stay correct ("showing N of M").
4. Filters, search and paging keep working together and after returning to the page.
5. The cause of the odd App-filter entries is reported with a proposal; no stored data changed.
6. Build clean (/warnaserror); existing tests pass.

Mine (designer's, not the user's):
- D1. App list never shows both "—" and "-".
- D2. With All fields chosen, results for any query are identical to before the change.
- D3. After "Show all", a live refresh keeps every row shown.

## Item 4 finding (INVESTIGATE ONLY -- changes no code, no stored data, filters nothing out)

**Cause.** The diary never stores an app name; it stores the foreground window's title bar text, and Reports
guesses the app from that text.
1. Recording: `NativeInput.Foreground()` (`Services/NativeInput.cs` 39-47) reads `GetWindowText` of the foreground
   window. `WindowTitleResolver.ActiveWindowTitle()` (`Services/WindowTitleResolver.cs` 66-141) looks up the
   process name, but only uses it (a) to append " – App" for the nine messenger exes (`ExeAppNames` =
   `MessengerApps.ByExeName`, lines 98-118) or (b) when the title is empty (125-126). For every other program the
   raw title is stored as is; `DiaryWriter.LogSession` (`Services/DiaryWriter.cs` 40) writes it to
   `time_diary.window` (cut at 240 chars). The process name is thrown away.
2. Reading: `AppNames.Group()` (`Services/AppNames.cs` 92-101) splits the title on " - ", " — ", " – " and takes
   the LAST part as the app. When a title has no spaced dash, the whole title becomes the "app".
3. The App filter list is built from `AppNames.Group(e.Window)` of every row in scope
   (`Pages/ReportsPage.Diary.cs` 414-417).
So any foreground window whose title has no " - App" suffix becomes its own app. The examples fit this (likely
sources, not verified against data): "74% complete" / "99% complete" -- Windows File Explorer copy-progress dialog;
"Add a device" -- Windows Bluetooth/device pairing window; "Add Conditional Column" -- Power Query dialog (Excel /
Power BI); "1-Click Cleaner v1.1" -- a program whose title has a hyphen without spaces. The opposite error also
exists: a title like "Notes - Draft" in a program that does not put its name last yields app "Draft".

**Siblings with the same cause (not changed):** Time by App and Top Distractions group by the same `Group`
(`Services/ReportData.cs` 375 and the TopDistractions label via `AppNames.Label`); bulk marking learns
`Sub ?? Group` as an activity keyword (`Pages/ReportsPage.Diary.cs` 889), so marking such a row can teach a dialog
title as a rule; the activity classifier also matches keywords against the raw title.

**Proposed fix.** Record the program behind each row: add a nullable `process` column to `time_diary` (same
ADD COLUMN migration pattern as `tag`, `Database.cs` ~262), fill it from the process name `WindowTitleResolver`
already has, and let `Group` use a friendly name of that process when present, title parsing only for old rows.
This changes how rows are recorded and the schema, so it needs the user's go-ahead (REQUEST "not to change how
diary rows are recorded").

**Options for what counts as an app (user to decide):**
- A. The program (exe) that owned the window, mapped to a friendly name (explorer.exe -> File Explorer,
  chrome.exe -> Chrome); unknown exes shown by exe name. Accurate for new rows; old rows stay title-based (the
  exe was never stored). Needs the recording change above.
- B. Read-time only: the last title part counts as an app only if it is a known app (the existing `Normalise`,
  `Browsers`, messenger and `FileSubApps` lists plus a user-editable list in Settings); anything else is grouped
  under "Other" with the full title as Page. No recording change; real but unlisted apps land in "Other" until
  added.
- C. Read-time only: a title with no " - " separator is not an app; group it under "-" (no app) or "Other", title
  as Page. Simplest; also lumps single-word real apps ("Spotify", "Calculator").
- Recommended: A for new rows plus B for existing rows.

## Open questions

1. **300-row cap in search / All-time view** (`maxSearchResults`, Diary.cs 176, 505-511). With the cap, 500/1000
   and "Show all" can never reach more than 300 rows there, and a single day rarely exceeds 300, so the larger
   increments would almost never appear. Lifting the cap makes "Show all" mean all matches, but "Select all" (code
   unchanged) would then reach every match (thousands) instead of at most 300 -- a wider bulk-mark reach, which
   touches "marking behaviour not to change". Recommended: lift the cap. **Builder default without a ruling: keep
   the cap**; M = rows in the list (<= 300) and the existing "N matches — showing the 300 most recent" notice stays.
2. Should Category "-" also cover values outside the list (e.g. `dayoff`), or "neutral because nothing matched"
   (the user's "never categorized" may mean that; storage cannot tell an auto-neutral from a chosen neutral)? Same
   for Tag "-" and legacy tag values. Default: strictly null/empty.
3. Does "Clear filters" reset the search-field chooser to All fields? Default: yes.
4. Item 4: which option (A/B/C) and whether the recording change is allowed.
