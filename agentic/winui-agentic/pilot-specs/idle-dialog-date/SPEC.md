# SPEC: date in the "Welcome back" absence dialog

## Requirement
The "Welcome back" dialog (`IdleReturnDialog.ShowAsync`) currently says
"You were away 6 min (10:30–10:36). What was it, roughly?". It must also show the calendar date of
the absence, using the app's existing display-date convention. If the absence crosses midnight, both
dates must be shown so the span is unambiguous.

## Chosen approach
Add one pure, culture-independent formatting helper to `DateExtensions` and call it from the dialog's
sentence. Dates use the existing `ToDisplayDate()` shape ("Thu 01.10": English weekday + dd.MM,
InvariantCulture) — the same shape `EditDiaryEntryDialog` already uses next to a start–end time
range ("from {start}–{end} on {date.ToDisplayDate()}"). Times stay `ToIsoTimeOfDay()` ("HH:mm").

Helper (name suggestion, Coder may rename): `DateExtensions.ToDisplayAbsenceSpan(DateTime start, DateTime end)`
returning the text that goes inside the parentheses:
- Same calendar date (`start.Date == end.Date`): `Thu 01.10, 10:30–10:36`
  -> "You were away 6 min (Thu 01.10, 10:30–10:36). What was it, roughly?"
- Different dates: `Wed 30.09 23:50 – Thu 01.10 00:05`
  -> "You were away 15 min (Wed 30.09 23:50 – Thu 01.10 00:05). What was it, roughly?"
The sentence prefix/suffix ("You were away N min (", "). What was it, roughly?") is unchanged.

Rejected:
- Date only on the start ("Wed 30.09 23:50–00:05"): ambiguous on midnight crossing, which the
  request explicitly forbids.
- ISO `yyyy-MM-dd`: that is the persistence format, not the app's display convention.
- Year-bearing formats (`ToDisplayDateNumeric`): an idle prompt is always the current/previous
  day or two; the app's dialog-level convention omits the year, and the weekday helps recognition.
- Inline `$"{x:ddd dd.MM}"` in the dialog: the codebase deliberately centralises every date shape
  in `DateExtensions` (see its doc comments); a hand-typed copy is the drift class DECISIONS.md
  already warns about, and an inline version could not be unit-tested without WinUI.

## Exact files to change
- `winui-agentic/Planillium.App/Services/DateExtensions.cs` — add the span helper (with a doc comment).
- `winui-agentic/Planillium.App/Dialogs/IdleReturnDialog.cs` — line ~112: replace the
  `({idleStart.ToIsoTimeOfDay()}–{idleEnd.ToIsoTimeOfDay()})` interpolation with the helper
  (`idleEnd` already computed at line 109).
- `winui-agentic/Planillium.App.Tests/` — new test file (e.g. `AbsenceSpanFormatTests.cs`) covering
  the helper. If the test project does not already link `DateExtensions.cs`, add a
  `<Compile Include="..\Planillium.App\Services\DateExtensions.cs" Link="App\DateExtensions.cs" />`
  to its csproj (source-file link, NOT a ProjectReference — see project CLAUDE.md). Check first;
  do not duplicate an existing link.
- No settings/schema/migration change. No change to what is logged (`LogIdleAnswer(s)` still gets
  `idleStart`/`idleMinutes` unchanged). No other callers need edits: all three entry points
  (`Trigger` toast path, `MainWindow.xaml.cs:~291`, `ReviewDialog.cs:~217` gap sweep) go through
  `ShowAsync`, so one edit covers them.

## Edge cases
- Absence entirely within one day: single date, comma form.
- Absence crossing midnight (e.g. 23:50 + 15 min): both dates, each with its own time.
- Absence ending exactly at 00:00 of the next day (start 23:54, 6 min): end is next date at 00:00,
  so `end.Date != start.Date` -> two-date form (not collapsed). Correct and unambiguous.
- Absence starting exactly at 00:00: same-day form.
- Absence from a prior day surfaced later (ReviewDialog gap sweep passes `gap.Start`, possibly
  yesterday or earlier; app not run for days): date shown is the absence's own date, not today.
  Do not substitute `DateTime.Today`.
- Month/year boundary (31.12 -> 01.01): dates compared via `.Date`, formatted dd.MM; second date
  correctly rolls. (Year omitted per convention; accepted.)
- Locale: output must be identical under any OS culture (English weekday, `.` separators) —
  InvariantCulture only.
- `leadIn` (optional bold line above) is untouched and still shown.
- Zero/long idleMinutes: no special handling; helper is purely start/end based.
- Dialog width/wrapping: the sentence is a `TextWrapping.Wrap` TextBlock in a 716px content
  width; the longer two-date text must wrap, not clip.

## Acceptance criteria (mechanical)
1. `dotnet build -p:Platform=x64 -c Debug` from `winui-agentic/Planillium.App/` succeeds with no
   new warnings.
2. `dotnet test` from `winui-agentic/Planillium.App.Tests/` passes, including new tests asserting
   exactly (using 2026-10-01 as Thu):
   - start 2026-10-01 10:30, end 10:36 -> `Thu 01.10, 10:30–10:36`
   - start 2026-09-30 23:50, end 2026-10-01 00:05 -> `Wed 30.09 23:50 – Thu 01.10 00:05`
   - start 2026-09-30 23:54, end 2026-10-01 00:00 -> `Wed 30.09 23:54 – Thu 01.10 00:00`
   - start 2026-12-31 23:55, end 2027-01-01 00:10 -> `Thu 31.12 23:55 – Fri 01.01 00:10`
   - same result when `CultureInfo.CurrentCulture`/`CurrentUICulture` is set to e.g. `de-DE` and `ru-RU`.
   (The dash in the same-day form is the en dash U+2013 as today; spaced en dash in the two-date form.)
3. Grep: `IdleReturnDialog.cs` no longer builds the away-sentence from two bare `ToIsoTimeOfDay()`
   calls; it calls the new helper; the helper lives in `DateExtensions.cs`, uses
   `CultureInfo.InvariantCulture`.
4. Grep: no other file hand-types `"ddd dd.MM"` for this purpose (no new inline date format strings
   outside `DateExtensions.cs`).
5. Sentence prefix `You were away {idleMinutes} min (` and suffix `). What was it, roughly?` are unchanged;
   `LogIdleAnswer`/`LogIdleAnswers`/`BuildSegments` code is unchanged (diff shows no edits there).
6. Optional visual check (scratch instance only): dialog shows the date for a same-day absence and
   wraps cleanly for a midnight-crossing one.

## Safety notes
- QA must NOT run against the real `data/` or real `config.json`, or touch the user's live running
  Planillium instance. Use scratch copies in the QA worktree only.
- Do not click "Log it"/"Skip" in any instance bound to real data (these write diary rows).
  Preferred verification is the unit tests plus code inspection plus a clean build; any visual
  check must run on a scratch data directory.
- Do not edit `winui/Planillium.App/` (the original) in any way.

## Open question
None. (Year omission and weekday inclusion follow existing convention; if the user wants the year,
swap to `ToDisplayDateWithYear`-style — a one-line change in the helper.)
