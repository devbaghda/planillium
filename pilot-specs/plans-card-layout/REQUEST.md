# Fix request: Plans page card is overcrowded

User (2026-10-09), with screenshot: "terrible design solution, fix it".

Problem: on the Plans page each active-plan card puts the plan text (name, "Day X of Y" meta line,
"Originally due" line, progress bar) and up to six action buttons (Briefing, + Add task, Replace
remaining..., Excluded days..., Archive, Teach on-plan apps...) in ONE horizontal row. The buttons
take most of the width, so the plan name wraps one word per line and the meta line, due line and
progress bar are clipped ("Day 30 of 30 · 3...", "Originally due 2...").

Required: redesign the card so the plan text is fully readable at normal and narrow window widths and
every existing action is still reachable. No new hues; keep accessibility names that include the plan
name; keep tooltips; no behaviour change to any action.
