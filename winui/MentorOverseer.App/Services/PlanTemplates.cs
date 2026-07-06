namespace MentorOverseer.App.Services;

/// <summary>
/// The three claude.ai prompt templates, ported verbatim from main.py.
/// Placeholders {subject}/{claude_role}/{area_of_interest}/{user_plan} are
/// filled via string.Replace (the templates contain literal JSON braces).
/// </summary>
public static class PlanTemplates
{
    public const string Skill = """
I need to become functional in {subject} as fast as possible. I'm not a beginner
at life or at learning hard things — I'm a beginner in this specific skill. Act as
a {claude_role} with 10+ years of hands-on experience in {area_of_interest}. Treat
me like a capable professional who has no time to waste, not like a student.
Write the plan itself as if you were mentoring me through it in person: for
each day, don't just list a task — tell me why it matters at this exact
point, what a beginner in {subject} typically gets wrong right there, and
what "doing it right" actually looks like, the way a real mentor corrects you
before you form a bad habit rather than after.

Before any plan, give me a short, blunt briefing:
1. The 3 highest-leverage things that produce ~80% of real-world results in
   {subject}. Be specific, not generic.
2. What to completely ignore — the stuff that feels productive but doesn't move
   the needle for someone who needs to be functional, not credentialed.
3. What most learners waste months on that I can skip entirely, and why.
4. A realistic time estimate to reach "functional" (able to do real work
   unsupervised) — no optimism, no sandbagging either.

Then build a day-by-day plan to get me there in the shortest realistic time.
Rules for the plan:
- Every day mixes theory with immediate hands-on practice — no day is pure
  passive learning.
- Difficulty escalates day over day, each day building on what I actually
  practiced, not a generic curriculum order.
- Include real practical outputs (small builds, drills, exercises), not just
  "read about X".
- Be honest if a day needs less time or more time than others — don't pad for
  symmetry.

After the briefing, output ONLY the plan as a single JSON code block (```json
... ```), with nothing else inside the fence. Repeat the 4-point briefing
inside the JSON too (structured, not prose) so it's saved permanently with
the plan, not just visible here in chat. Match this exact schema:

{
  "id": "kebab-case-slug",
  "name": "Short plan title",
  "color": "#3b82f6",
  "total_days": 14,
  "briefing": {
    "high_leverage": ["The 3 highest-leverage things, one per string"],
    "ignore_completely": "What to completely ignore and why",
    "common_time_wasters": "What most learners waste months on that can be skipped",
    "realistic_timeline": "The honest time estimate to reach functional, with reasoning"
  },
  "phases": [
    {
      "title": "Phase name (e.g. Foundations, Applied practice, Stress-testing)",
      "tasks": [
        {
          "day": 1,
          "task": "Short task title",
          "detail": "Concrete instructions: exactly what to do, for how long, and what 'done' looks like.",
          "mentor_note": "Why this matters right now, the mistake beginners make at this exact step, and what doing it right looks like.",
          "category": "theory | practice | project | review",
          "duration_min": 60
        }
      ]
    }
  ]
}

Multiple tasks can share the same "day" if they belong together in one
session. Use "category": "theory" for concept learning, "practice" for
drills/exercises, "project" for a built artifact, "review" for consolidation/
spaced repetition days. Keep "detail" to the concrete how-to (what to do,
for how long, what "done" looks like); put the mentor commentary — why it
matters right now, the mistake beginners make at this exact step, what
right looks like — in "mentor_note", so both are visible together for every
task, not buried back in the briefing.
""";

    public const string Goal = """
I need to {subject} as effectively and quickly as realistically possible. I'm
not naive about how hard real-world goals are — I want a plan, not a pep talk.
Act as a {claude_role} with 10+ years of hands-on experience getting people
through exactly this kind of goal in {area_of_interest}. Talk to me like an
operator who has done this dozens of times for clients, not like a
motivational speaker. Write the plan itself as if you were mentoring me
through it in person: for each step, don't just list an action — tell me why
it matters at this exact point, what people trying to {subject} typically
get wrong right there, and what "doing it right" actually looks like, the
way a real mentor corrects you before a mistake happens rather than after.

Before any plan, give me a short, blunt briefing:
1. The 3 decisions or actions that determine ~80% of whether this succeeds,
   fails, or drags on. Be specific to my situation, not generic advice.
2. What to completely ignore — the research rabbit holes, comparison
   shopping, or "just in case" prep that feels responsible but doesn't
   actually move this forward.
3. The mistakes or wasted time that trip up most people trying to {subject},
   and how to route around them.
4. A realistic timeline to get this done — call out anything outside my
   control (approvals, waiting periods, other people's schedules) that sets a
   floor on how fast this can go, and what's actually in my control to
   compress.

Then build a day-by-day plan to get there in the shortest realistic time.
Rules for the plan:
- Every step is a concrete action I can actually take that day (a call to
  make, a document to gather, a decision to lock in) — not "consider" or
  "think about".
- Sequence dependencies correctly: never schedule something before its
  prerequisite is actually done.
- Call out any days that are just waiting on something external, and what I
  should do in parallel instead of sitting idle.
- Be honest if a step needs a single hour or several days — don't pad for
  symmetry, and don't compress steps that genuinely take external processing
  time.

After the briefing, output ONLY the plan as a single JSON code block (```json
... ```), with nothing else inside the fence. Repeat the 4-point briefing
inside the JSON too (structured, not prose) so it's saved permanently with
the plan, not just visible here in chat. Match this exact schema:

{
  "id": "kebab-case-slug",
  "name": "Short plan title",
  "color": "#3b82f6",
  "total_days": 30,
  "briefing": {
    "high_leverage": ["The 3 decisions/actions that determine 80% of the outcome, one per string"],
    "ignore_completely": "What to completely ignore and why",
    "common_time_wasters": "The mistakes/wasted time that trip up most people, and how to route around them",
    "realistic_timeline": "The honest timeline, including what's outside your control vs. what compresses it"
  },
  "phases": [
    {
      "title": "Phase name (e.g. Research & decisions, Paperwork & logistics, Execution)",
      "tasks": [
        {
          "day": 1,
          "task": "Short task title",
          "detail": "Concrete instructions: exactly what to do and what 'done' looks like.",
          "mentor_note": "Why this matters right now, the mistake most people make at this exact step, and what doing it right looks like.",
          "category": "research | decision | logistics | execution",
          "duration_min": 60
        }
      ]
    }
  ]
}

Multiple tasks can share the same "day" if they belong together. Use
"category": "research" for information-gathering, "decision" for
choices/comparisons to lock in, "logistics" for paperwork/admin/coordination,
"execution" for the actual doing steps. Keep "detail" to the concrete how-to;
put the mentor commentary — why it matters right now, the mistake most
people make at this exact step, what right looks like — in "mentor_note", so
both are visible together for every task.
""";

    public const string Reformat = """
I already have a plan for "{subject}" that I wrote myself. I want you to
reformat it into a specific JSON structure so I can import it straight into
my personal tracking app — don't rewrite my content, don't add new tasks,
don't change my sequencing or timeline. Just restructure what I give you.
Where something is ambiguous (a missing day number, a missing duration),
make the most reasonable inference from context rather than inventing new
content that wasn't there.

Here is my plan, exactly as I wrote it:
---
{user_plan}
---

Output ONLY the result as a single JSON code block (```json ... ```), with
nothing else inside the fence, matching this exact schema:

{
  "id": "kebab-case-slug",
  "name": "{subject}",
  "color": "#3b82f6",
  "total_days": 14,
  "phases": [
    {
      "title": "Use my own phase/section names if I had them, otherwise group logically",
      "tasks": [
        {
          "day": 1,
          "task": "Short task title, using my own wording",
          "detail": "The concrete instructions, from what I wrote — expand only for clarity, don't invent new steps.",
          "category": "pick the closest fit: theory | practice | project | review | research | decision | logistics | execution — or omit the field if nothing fits",
          "duration_min": 60
        }
      ]
    }
  ]
}

Only include a per-task "mentor_note" or a plan-level "briefing" block (same
shape as those used elsewhere in this app: high_leverage, ignore_completely,
common_time_wasters, realistic_timeline) if you genuinely have something
useful to add beyond what I already wrote — don't pad either one out just to
fill the field.
""";
}
