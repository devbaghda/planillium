# G9b build plan (2026-11-04) — read with `context/acceptance/2026-11-04-g9b-learned-rules.md`

Build in stages; each stage ends with strict build + suite green + its acceptance cases tested in an isolated worktree (scratch MENTOR_ROOT). Sonnet builds; no subagents unless the user asks.

## Findings from reading the code (2026-11-04)
- Classification today = `Services/ActivityClassifier.cs`: flat lowercase substring lists from config `activity_rules` (on_plan, off_plan, neutral) — on-plan wins over off-plan; `ActivityTracker.PollOnce` calls it (line ~563) via `EffectiveClass(Classify(title))`.
- Keyword teaching = `ConfigService.LearnActivityRule` / `ForgetActivityRule`; bulk mark = `Pages/ReportsPage.BulkMark.cs`; every data change = `Services/DataChangeDoor.cs` (preview, backup, write, Undo).
- `time_diary` (id, date, start_time, end_time, duration_min, category, window, description, tag). Raw rows are pruned after `Database.DiaryRetentionDays` = 90 into `diary_daily_rollup` (totals only) -> older history has no titles and cannot be learned from; hence the permanent counts table (A8).
- Outside working hours `PollOnce` only closes an open session (`HandleOutsideDiaryHours`); nothing is opened. Rule 1 in DECISIONS says nothing is tracked there.
- No Wilson code exists yet. Categories: on_plan, off_plan, neutral, idle, paid, dayoff. "Work" = paid (income); "on-plan" = points.

## Stages
1. **Engine (pure C#, unit-tested):** `TitleWords` normaliser (lowercase, strip accents/digits/dates/extensions/app name, singular=plural, whole-word), `Wilson` lower bound, `LearnedRule` (app, words, category), `RuleMatcher` (most conditions wins, tie = older rule), proposal finder (app group -> best 1-2 words by share in marked rows vs rarity in other rows of that app). Link new files into the test project (no ProjectReference).
2. **Storage:** tables `learned_rules` (id, app, words, category, created) and `rule_word_counts` (app, word-set key, category, n) in `Database.EnsureSchema`; `time_diary` gets nullable `rule_state` ('auto' | 'suggested' | 'manual') — same ADD COLUMN pattern as `tag`. Pruning tallies counts before delete (no double counting with live rows). Settings Clear actions also clear both tables (DECISIONS 19k); decide and state Export/backup handling (A9). Rule create/delete goes through `DataChangeDoor` (Undo + backup).
3. **Learning UI:** after Bulk Mark show per-app proposal(s) with N and range; Confirm / edit words / Skip; ranges from live rows + counts table.
4. **Classifier + tracker:** `ActivityClassifier.Classify` consults confident rules (>=10 examples AND Wilson lower bound >=85%) first, then old keyword lists; suggestion-only rules never change the class; tracker stamps `rule_state`.
5. **Diary:** "suggested" label on matching rows with one-click accept (via the door); new "Rule status" filter (no rule / acted automatically / suggested / marked by hand) combining with existing filters and paging.
6. **Settings:** rules list (app, words, category, N, range, status) + live search box + delete with confirm (Undo covered).
7. **After hours:** in `PollOnce`, outside working hours open/log a session only when a confident rule says on_plan or paid; scoring/income as in-hours. Amend DECISIONS rule 1 and MANUAL/CHANGELOG in the same pass.
8. **Close-out:** tests, acceptance run (UI PASS only if seen), siblings sweep (every classify call site: tracker, bulk mark, reports, idle text), LESSONS line per defect, docs (CONTEXT, todos, CHANGELOG, MANUAL), no push without the user's word.

## Open before/while building
- Export-all-my-data and backups: include the counts table? (state the choice in A9).
- Rule-word choice when no word is shared (A6): app-only rule or no proposal.
