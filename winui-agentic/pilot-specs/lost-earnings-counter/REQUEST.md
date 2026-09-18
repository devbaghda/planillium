# Feature request — lost earnings counter

Captured verbatim from the user (mastermind session, 2026-09-18) for `planillium-planner` to read
as-is. Do not paraphrase or reinterpret this section — it is the input, not a summary of it.

> i want to add a counter of lost earnings . the counter should start on 04 december 2025. it
> should consider that my after tax income was 2700 eur per month and now i do not have that
> income , so potentially i am losing every day . i want that 2700 be adjustable from the settings
> so when I start earning i can deduct the monthly earnings from that amount . the count of
> losses/profits should appear in the sidebar and in the reports as well . in the reports should be
> correspondingly changing for day/week/month/year. to develop this use the 2 workflows scenarios
> that i have just discussed with you in mastermind session

## Context for the Planner

- "the 2 workflows scenarios" = this pilot itself: build this feature once via the regular
  single-agent-with-skills workflow in `winui/Planillium.App/`, and once via this agentic
  Planner → Coder → QA pipeline in `winui-agentic/Planillium.App/`. Nothing further to resolve on
  that point — it's an instruction to run the comparison, not a spec detail.
- No other detail beyond the quote above was given in the mastermind session. Anything not stated
  (e.g. exact sidebar placement, report chart type, rounding rules) is the Planner's own design
  decision — write it into SPEC.md explicitly so Coder and QA both see the same resolved answer,
  and flag it as a judgement call rather than presenting it as something the user specified.
