# Lessons -- regular side (rules only; merged by the generalist after each QA verdict)

## Input boxes and focus
- Leaving an input is many paths: for every input list each way out (Tab, Enter, Esc, click another field, blank area, heading, a box's own caption) and make each follow the user's rule.
- A button that is not a tab stop refuses programmatic focus; parking focus needs a harmless focusable element.
- A handler that corrects text must not reset state it just set; set hints after the correction and clear them by value change, not by any TextChanged.
- Set every piece of a control's state before raising its change events (a readout read a stale sign and stuck on "= --/h").
- A default value in an input ("0") can pass validation that only checks "empty"; start optional amounts empty and refuse 0 where 0 is meaningless.
- A box that shows a stored value must never sit blank on leaving: revert or keep the text with a visible message.
- After any click, check where focus ended up, not only what the click was on (containers may hand focus to the first focusable box).

## User rules (Settings -> Income, 2026-10-25)
- Esc in a saved box (salary, payable tag) reverts to the saved amount, saves nothing, cursor ends nowhere (B1h, 2026-10-27).
- Esc / click outside clears the add line; Tab out of its amount box keeps the text.
- Enter in the add line = Add, then the cursor leaves; Enter or leaving a money box saves and focus ends nowhere, never the salary box.
- A typed minus stays visible with its message until valid; nothing invalid is saved; a payable tag is never saved at 0 or empty.

## QA and testing
- QA must run the standing input-box checks (app-qa-checks section 5); a line passing on one path is not a pass on all paths.
- Focus tests need real mouse clicks and keystrokes, window-relative coordinates, and a "type 9, nothing changes" check.
- Pre-seed today's kickoff and review as done in the scratch state before launch, or timed prompts steal focus.
- Read the status line by its own ID, separately from inline red messages.
- Run QA scripts with `PYTHONIOENCODING=utf-8`: amounts contain a thin space (U+2009) that crashes cp1252 console output.
- A scratch MENTOR_ROOT does not isolate Windows Credential Manager secrets (AI key, TickTick); check the Settings AI/TickTick rows before assuming a test instance is clean.
- Reuse the B1d harness (`qa/b1d_h.py`) by setting its module globals instead of writing a new one.
- MoneyBox is a WinUI control; the source-link test project cannot unit-test it (extracting its logic is an open offer, not a decision).
- Seed scratch data in two steps: first launch creates the schema, kill the app, seed `time_diary` with sqlite3, relaunch; a seeded diary gap triggers a "Welcome back" dialog to skip before driving the UI.
- A QA worktree may start on another branch's commit: `git checkout --detach winui-rebuild` before building, or old code runs.
- The shell wrapper refuses compound commands containing git inside a worktree; run git commands on their own.
- The Diary list is virtualized: maximize, ScrollIntoView on "Select all", then mouse-wheel before counting row checkboxes (only on-screen rows exist in UIA).
- A scratch driver script must kill the app it started in a `finally`; an exception otherwise leaves it running.
- Before fixing a reported UI symptom, reproduce it on the current build in scratch; it may already be gone (G3 vanishing ticks were pre-G3 code).

## Data changes (safe door)
- Any side effect of a bulk change outside the database (e.g. learned keywords in config.json) is recorded with the change, so Undo can reverse exactly that and nothing older.
- Settings boxes that save on LostFocus must be reloaded after an Undo rewrites the same config, or the next Save writes the undone values back.
- Record only what a change newly did: check the prior state first; a helper that returns true when nothing changed over-reports.

## Memory (WinUI)
- Measure memory with cached UIA element references (select/toggle/invoke); full tree walks create automation peers and inflate the figure.
- WinUI native memory behind dropped trees is freed late (GC sees only small wrappers): force a GC before calling growth a leak, and queue a low-priority collect after dropping a big tree.
- A C# IElementFactory held by a native ItemsRepeater is a GC root; if its delegate's closure captures the repeater it leaks forever: hold the builder weakly, the page strongly.
- Clearing a cached page's tree on leave loses its scroll position; save the offset and restore it after layout.
- Watch private bytes as well as working set: Windows trims the working set of an idle app.

## Models
- Focus / key-event work: consider opus (B1b sonnet needed 5 QA rounds; B1c and B1d opus passed in 1). QA for real-click focus work: opus.

## Context
- Read `../CONTEXT.md` in full yourself; the project CLAUDE.md no longer auto-loads it.
