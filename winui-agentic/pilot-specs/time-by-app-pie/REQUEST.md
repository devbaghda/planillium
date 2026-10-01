# Feature request: Time by app as a drill-down pie chart

User request (2026-10-01): "Time by app should be a pie, not bars like now. It should also keep
its collapsible nature, i.e. when clicking on a pie section it should become a pie of the
underlying level."

Replace the bar rows in the Reports page "TIME BY APP" section with a pie chart: one slice per
app (period totals as today). Clicking an app's slice turns the chart into a pie of that app's
underlying breakdown (its sub-items, as the bars' expand rows show today), with a clear way to
go back up a level. Apps with no sub-items don't drill. Keep the existing legend/colour meaning
(on-plan / off-plan / neutral / paid / idle) in some form, show hours per slice, keep the
many-small-apps case readable (today's "show more" idea), and keep it keyboard/screen-reader
reachable like the current rows. No new third-party packages unless unavoidable — draw it with
what the app already uses, and say so in the spec.
