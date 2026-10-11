# SPEC: multi-select diary filters

## Requirement

On the Reports page's time-diary section, the four dropdown filters (Category, App, Page, Tag) each
accept several chosen values at once instead of one. Within one filter, chosen values are OR-ed (row
matches if its value equals ANY chosen one). Across filters (category, app, page, tag, plus the free-text
search) they stay AND-ed exactly as today. An empty selection on a filter means "no filter" as today. The
selection survives a re-render/navigation (static page state, as the current `_diary*Filter` fields do).
Everything else (All time, search, Show more, mark-selected toolbar, live refresh) is unchanged.

Current behaviour (read from `Pages/ReportsPage.Diary.cs`): four `ComboBox`es, state in
`static string? _diaryCategoryFilter/_diaryAppFilter/_diaryPageFilter/_diaryTagFilter`; predicates
`MatchesCategory/App/Page/Tag/Search` AND-ed in `RenderDiaryResults`; App/Page option lists are
faceted (built from rows matching every OTHER active filter); a persisted App/Page value that no longer
appears in view is dropped; "Clear filters" nulls all four plus All-time and search; `scopeKey` includes the
filters so "Show more" progress resets on a filter change.

## Chosen approach

1. Replace each filter's `string?` state with a `static HashSet<string>` (empty = no filter), using
   `StringComparer.OrdinalIgnoreCase` for app/page (preserves today's case-insensitive match) and
   `StringComparer.Ordinal` for category/tag values (today's `==`). Category/tag store the stable
   VALUES (`on_plan`, `routine`...), not labels.
2. Replace each `ComboBox` with a `Button` (same MinWidth, same `AutomationProperties.Name`
   "Filter diary by category/app/page/tag") whose `Flyout` holds a scrollable `StackPanel` of `CheckBox`es,
   one per option. Button text: `All categories` / `All apps` / `All pages` / `All tags` when empty; the
   single label when one chosen; `"<first label> +N"` when several (e.g. `Off-plan +1`). Flyout stays open
   while ticking (unlike `MenuFlyout`, which closes per click, forcing reopen for each pick). Flyout has a
   "Clear" link/button at its top that empties that one filter.
3. Pull the pure logic out of the page into a new plain-C# static class `Services/DiaryFilter.cs`
   (no WinUI types) so it is unit-testable via the test project's source-link pattern:
   - `static bool Matches(HashSet<string> selected, string? value, StringComparer cmp)` (empty set -> true).
   - `static string ButtonLabel(string allLabel, IReadOnlyList<string> selectedLabels)` producing the text
     in item 2.
   - `static HashSet<string> PruneToAvailable(HashSet<string> selected, IEnumerable<string> available)`
     returning the selected values still present (case-insensitive for app/page), used for the existing
     "drop a filter whose value no longer appears in view" behaviour, now per-value (a value that
     vanishes is removed, the other chosen values stay).
4. Faceted option lists are kept: each of App and Page options is built from rows matching every OTHER
   active filter (same as now). Only the predicate changes from equality to set membership.
5. Rebuild the App and Page flyout checkbox lists inside `RenderDiaryResults` (as the ComboBox items
   are rebuilt today), under the `syncingFilters` guard so programmatic `IsChecked` sync does not fire
   handlers. Category/tag lists are fixed (built once in `BuildDiaryFilterRow`), but their checked state and
   button text are re-synced each render.
   Because rebuilding an App/Page flyout's contents while it is open would be disorienting (the 30 s live
   refresh calls `RenderDiaryResults`), update existing checkbox items in place when the option set is
   unchanged, and only rebuild when it differs.

Rejected alternatives:
- `ListView SelectionMode=Multiple` inside a `Flyout`: heavier, fiddly selection-sync (SelectedItems
  re-sync fires SelectionChanged repeatedly), no gain over CheckBoxes for lists of <= ~dozens.
- `MenuFlyout` with `ToggleMenuFlyoutItem`: flyout closes on every click; picking 3 values = 3 reopenings.
- Chips/token boxes or a third-party multi-select combo (CommunityToolkit): new dependency and a lot of
  layout (the filter row already needs horizontal scroll at the 900 px floor); not worth it.
- Keeping the ComboBoxes and adding a "+" to add more: two-control model, worse discoverability.
- Making cross-filter semantics OR: contradicts "consistent with today's behaviour" and the 2026-07-29
  faceted-filter rule in code comments.

## Exact files to change

- `Planillium.App/Services/DiaryFilter.cs` (new): the pure helpers in item 3.
- `Planillium.App/Pages/ReportsPage.Diary.cs`: state fields to `HashSet<string>`; `Match*` predicates;
  `RenderDiaryResults` (facet lists, prune, button text sync, `filtersActive` = any set non-empty, `scopeKey`
  must serialize each set deterministically, e.g. sorted values joined with `,`); `BuildDiaryFilterRow`
  (Buttons + flyouts, return tuple types change); the four `SelectionChanged` handlers become per-checkbox
  Checked/Unchecked handlers (guarded by `syncingFilters`); `clearFiltersBtn` clears all four sets.
- `Planillium.App.Tests/Planillium.App.Tests.csproj`: add `<Compile Include="..\Planillium.App\Services\DiaryFilter.cs" Link="App\DiaryFilter.cs" />`.
- `Planillium.App.Tests/DiaryFilterTests.cs` (new): unit tests for the helpers.
- `MANUAL.md` / `CHANGELOG.md` (Unreleased) in the agentic copy if present: a one-line user-visible note.
- No XAML change (the filter row is built in code), no settings schema change, no SQLite migration, no
  `config.json` key (filter state is in-memory static, as today; it resets on app restart, as today).

## Edge cases

- Empty selection on one filter => that filter is ignored; all four empty + no search => unfiltered, same as today.
- Several values in one filter: union within, intersection across filters. e.g. Category {Off-plan, Idle} AND Tag {Routine}.
- Faceting: with App={Chrome} the Page list contains only pages from Chrome rows (OR across multiple apps if
  several apps ticked). After ticking, previously ticked values in OTHER filters that no longer appear
  in view are dropped individually; survivors stay.
- Pruning drops only for App/Page (open-ended, scoped to view). Category/Tag values are never pruned (fixed
  lists), as today: a selected category with zero rows yields the "No entries match the selected filter(s)" message.
- Switching day / toggling All time / midnight rollover (`_diaryFollowsToday`) re-scopes the view: App/Page
  selections not present in the new scope are pruned per value; Category/Tag selections persist.
- All pruned selections end empty => reads as "All apps"/"All pages", not an empty-result trap.
- Null/empty values: rows with no tag have `Tag == null`; they never match a non-empty tag selection (no
  "(none)" option is added; that would be new scope). Rows with no page (`AppNames.Sub == null`)
  are not offered as a page option (as today) and never match a non-empty page selection.
- Case: app/page compared OrdinalIgnoreCase, so "chrome"/"Chrome" collapse to one option and one selection.
- `selectedIds` pruning (`RemoveWhere` not in filteredList) and `lastRows` behave unchanged; changing a
  filter does not clear a still-visible selection.
- 30 s live refresh while a flyout is open must not close it or lose focus/scroll (update in place, item 5).
- Live-refresh/`Render()` rebuilds the whole tree: selection state is static and re-applied on build.
- `scopeKey`: two different multi-selections must produce different keys (resetting "Show more" to 40);
  identical selection in a different insertion order must produce the same key (sort before joining).
- Window floor 900 px: the button row must still fit inside its horizontal ScrollViewer; button width must not grow with the number of selections (text is "label +N", trimmed).
- Large option lists (App/Page over all-time can be hundreds): flyout content scrolls (MaxHeight ~320).
- Accessible names preserved on the four buttons; each checkbox has an AutomationProperties.Name = option label.

## Acceptance criteria

1. `dotnet build -p:Platform=x64 -c Debug` from the agentic `Planillium.App/` succeeds with 0 errors, no new warnings.
2. `dotnet test` from the agentic `Planillium.App.Tests/` passes, including new `DiaryFilterTests` covering:
   a. empty set matches every value including null;
   b. one value matches only that value; two values match either;
   c. null value never matches a non-empty set;
   d. app/page comparer is case-insensitive, category/tag is not;
   e. `PruneToAvailable` keeps survivors and drops absent values, returns empty when none survive;
   f. `ButtonLabel` returns the "All ..." text for 0, the label for 1, `"<first> +N"` for N+1 >= 2, using the first in display order.
3. The four filter controls on the diary are no longer `ComboBox`; each is a `Button` with a `Flyout` of `CheckBox`es, and keeps its
   existing automation name ("Filter diary by category/app/page/tag").
4. Ticking two categories shows rows of either; unticking all restores the full list.
5. Category {A} + Tag {X,Y}: shown rows satisfy category A AND (tag X OR Y).
6. Search text still ANDs with the filters, as before; search + filters still scope to the wide range.
7. App list narrows by the other active filters (faceting preserved): with Category=Off-plan ticked, App options
   contain only apps with an Off-plan entry in scope.
8. Navigating to another page and back (or any `Render()`) restores all four selections (checked state and button text).
9. "Clear filters" empties all four selections plus All-time and search; every button reads its "All ..." text.
10. Per-flyout "Clear" empties only that filter.
11. Subtotal line ("N entries · Xh Ym total"), "Show more" batching, and the 300-row wide cap still reflect the combined filters.
12. A filter change resets "Show more" count to 40; an unchanged-scope live refresh does not.
13. Grep sweep: no remaining reference to `_diaryCategoryFilter is not { }`-style single-value state, to the `All categories/apps/pages/tags` ComboBox placeholder items, or to `categoryBox/appBox/pageBox/tagBox` as `ComboBox`; sibling check that no other page (e.g. Today/Schedule) has a mirrored diary filter that needs the same change (expected: none).
14. Diff touches only the files listed above.

## Safety notes

- QA must never run against the user's real app, `data/progress.db` or `config.json`. Use scratch copies in the
  agentic worktree only. Do not start/stop the live Planillium process; do not use the Release exe the
  user runs.
- Do not click "Mark on-plan/off-plan/neutral", "Mark <tag>", Edit or Split on any real database. If a UI run is wanted, seed a scratch
  `progress.db` with a handful of fabricated diary rows (varied categories/tags/apps/pages, a null tag, a null page) and point the
  app's data path at the scratch folder, per `winui-agentic/PILOT.md`. Otherwise verify by unit tests, build and code
  inspection (read-only UIA at most).
- Filter state is in-memory only; no persisted setting is introduced, so nothing in `config.json` should change.
  If it does, that is a defect.

## Open question

None blocking. One product-choice assumption made without a recorded decision, flagged for the user: cross-filter
combining stays AND (and OR within a filter); no "(none)" option for untagged rows is added. If untagged rows
should be selectable in the Tag filter, that is a separate request.
