# Acceptance — Mint / Muted blue themes + status-text colouring (Mac), 2026-11-25

**User's words:** "ok to mint and muted blue for both light and dark themes. The only thing is that I want the text
like 98d late from the plan to be colored differently. 'from the plan' should have a color of the regular text, while
the days count and the status 'late' or 'ahead' should change the color based on the status. The same rule should apply
to profits and losses and other timings that have status."

- T1 Settings has "Colour theme" (Mint / Muted blue), independent of Follow/Light/Dark; choice survives restart.
- T2 Mint light, Mint dark, Muted blue light, Muted blue dark each seen on Today, Plans, Reports, Settings: no black/white
  leftovers, text readable.
- T3 Sidebar drift card: "Nd late" red / "Nd ahead" green; " from plan" / " of plan" in regular text colour. "On track" green.
- T4 Plans page due line: only "Nd later / earlier" coloured; "Originally due … — now" regular.
- T5 Today overdue caption: only "N day(s) late" red; "— from day N" regular.
- T6 Sidebar balance chips: figure red if negative, green if positive, amber at zero; "pts" and labels regular.
- T7 Reports / Review ledger numbers already coloured by sign with regular labels (checked, unchanged).
- Standing: real DB md5 0a62966e... unchanged; `git status regular/winui` clean; scratch data only.

## Results (2026-11-26, scratch copy, window screenshots)
- T1 PASS (radio seen, switches theme; saved to file; restart persistence not separately restarted-checked beyond each run reading it).
- T2 PASS: Mint light/dark, Muted blue light/dark seen on Today, Plans, Reports, Settings. Found + fixed: system-blue checkboxes/radios/slider/Add Plan, unreadable selected card in dark.
- T3-T6 PASS seen. T7: found Reports Total score uncoloured, fixed (red).
- Real DB md5 0a62966e... unchanged; `git status regular/winui` clean; build 0 errors.
