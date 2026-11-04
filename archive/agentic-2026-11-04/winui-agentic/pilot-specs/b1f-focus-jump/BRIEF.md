# Brief -- B1f (user decision 2026-10-26)

Fix list in REQUEST.md. The user found this in the agentic app only: in the regular app, 4 blank clicks never moved
the cursor to the salary box. Evidence from the user's real mouse (click log, agentic app, Debug build 58597a2+):
blank-space clicks (pane id SectionScroll) moved focus to the salary box at two of five tries, when the click
followed a click in the tag box by about one second; scripted clicks (pywinauto) never reproduced it, so a real-input
or timing difference is likely (pointer press vs. a delayed focus handler, fast second click). Find the root cause;
do not just add another guard. Read your LESSONS.md and LESSONS-INBOX.md first. Same constraints as B1d BRIEF.

## Addendum 2026-10-26 (PM) -- resume after stop
State: fix 1 d2d30b0 FAILED QA (4 px right-edge strip of the Settings pane still moved the cursor to the salary box, 8/18 and 17/27 jumps with button held >= 50 ms; interior of the pane clean in 600+ trials). Fix pass 1 e82135f is committed and NOT yet QA'd. Remaining work: (1) coder: write the rule into this side's DECISIONS.md standing lessons -- "a focus/pointer fix must cover the whole container incl. edges and padding, and be tested at the borders, not only the interior"; (2) re-run QA on e82135f only: the edge-strip lines (1-5) plus the lines the fix could touch.
New process (user rulings 2026-10-26): trials per case are QA's call per case, with a stated reason; combine checks in one trial where possible; QA appends a line to agentic/winui-agentic/QA-PROGRESS.md after every case; members read the side's CONTEXT.md/DECISIONS.md first (Memory first).
QA budget: 20 min. Defense: 6 cases -- (a) edge strip from the tag box, holds 0/50/250 ms, with the typed-"9" check in the same trial; (b) edge strip from tag-amount / salary-neighbour / Studioshoo boxes (one combined run); (c) first click after the window was inactive; (d) wheel scroll then edge click; (e) interior smoke (12 points, one pass); (f) B1-B1d regression using the existing b1f_reg scripts -- about 2.5 min each = 15 min, plus about 5 min for Debug build and launch. Interior is already proven clean, so it gets one pass only.
