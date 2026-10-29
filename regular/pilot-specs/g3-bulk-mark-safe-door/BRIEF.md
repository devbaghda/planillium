# Brief: G3 bulk marking + safe door (this side only; work inside D:/CLAUDE/Planillium/regular/)
Read REQUEST.md and ACCEPTANCE.md here. Investigate first how Mark and the 300-row cap work today and why marks disappear; then build. Reuse the existing Diary and Settings look; confirm dialog and toast/message follow the existing dialog style. The data-change door must be one reusable service (preview, backup, write with old values, undo), not code inside the page. Never touch real data (data/, config.json, plans/, live Release exe); test only on a scratch copy built in your scratchpad (MENTOR_ROOT, MENTOR_INSTANCE_SUFFIX=verify, PLANILLIUM_AI_BASE_URL=http://127.0.0.1:9/). Precheck only: dotnet build -p:Platform=x64 -c Debug -warnaserror --no-incremental then dotnet test --no-build; no QA, no push; commit locally on winui-rebuild. Update your own side docs (CHANGELOG Unreleased, MANUAL if user-visible, context notes). Report: cause of disappearing marks, files changed, how Undo handles edited rows, questions. Kill only processes you started.

## Addendum 2026-10-29 (user decisions, apply to this build)
1. Keep: the confirm dialog on every bulk mark; Clear actions in Settings also delete all backups; ticks persist across pages; Show all (G2) reaches every row.
2. Undo must also forget the activity keywords that THAT mark newly taught (only the ones it added; never rules that existed before). The success message must no longer say Undo leaves them.
3. Ticks must survive a Mark and an Undo: after marking a partial selection, the same rows stay ticked (a tick is dropped only when its row no longer exists or no longer matches the filters). The user saw ticks vanish after Mark although the rows stayed in the list; find the cause and fix it.
4. Idle rows: no change. A category mark only changes the category; the window name "idle" stays.

## Addendum 2 2026-10-29 (user answers; QA deferred to the end, builders run the precheck only)
1. Tag buttons (Work, Clear tag, other tags) act on ALL filter matches when nothing is ticked, exactly like the category marks, through the same door (confirm, backup, Undo).
2. Undo from Settings -> Change history asks for confirmation before restoring.
3. "Show all" lifts the 300-row display cap: Show all reaches every match.
4. A mark with nothing ticked does NOT teach activity keywords (category marks with ticked rows still do).
5. Backups: no expiry; each new backup replaces the oldest, last 5 kept (as built). Settings text notes backups hold full window titles.

## Addendum 3 2026-10-29 (user)
1. Idle rows are skipped when a mark teaches activity keywords ("idle" is never learned). The category mark itself still applies to idle rows.
2. Vanishing ticks: user saw them in the live Release exe, which is the 26 Oct build (before G3). Not a defect of the current source; closed.
