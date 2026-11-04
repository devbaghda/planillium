# Feature request: Insights — euro equivalents and a corrected off-plan ratio

User request (2026-10-01): "Add not only hours but their EUR equivalent. Off-plan ratio should be
against on-plan + neutral."

In the Reports "INSIGHTS" box, every sentence that quotes hours of off-plan time (total off-plan
time, the biggest distraction) must also quote the euro equivalent. The euro value of an hour is
the single hour value from the sibling request `hour-value-correction` (monthly net income ÷ 168 h)
— use that same value, don't invent another. The "off-plan time is over X% of your productive
time" insight must compute the ratio as off-plan ÷ (on-plan + neutral) — today's denominator is
different; check the current code and also check anywhere else the app (Reports text, exported
report) states this same ratio so they all agree.
