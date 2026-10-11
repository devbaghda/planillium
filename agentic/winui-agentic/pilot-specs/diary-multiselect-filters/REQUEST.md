# Feature request: multi-select diary filters

User request (2026-10-01): "In the diary the filters should be multiselectable."

The Reports page's diary section has filters (single choice today). Each filter must allow
choosing several values at once (e.g. two categories, or several tags). Selecting nothing means
"no filtering" as it does now. Keep how filters combine across different filter kinds consistent
with today's behaviour (check the current code), and keep the filter state surviving a re-render
the way it does now.
