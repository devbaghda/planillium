namespace Planillium.App.Services;

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
Write the plan the way a mentor would: for each task say why it matters at this
point, what a beginner typically gets wrong there, and what doing it right looks like.

Before any plan, work out a short, blunt briefing: (1) the 3 highest-leverage things that
produce ~80% of real-world results in {subject}; (2) what to ignore completely; (3) what
learners waste months on that I can skip; (4) a realistic time estimate to "functional"
(able to do real work unsupervised), no optimism and no sandbagging. Do NOT write the
briefing in the chat: it goes only inside the JSON, in "b" below.

Then build a day-by-day plan to get me there in the shortest realistic time. Every day mixes
theory with immediate hands-on practice, difficulty escalates day over day, and there are
real practical outputs (small builds, drills, exercises). Be honest if a day needs less or
more time than others; several tasks may share a day. Categories: theory, practice,
project, review.

To keep your reply short and fast, answer with ONLY one compact JSON code block
(```json ... ```): no text before or after it, no line breaks or indentation inside it, and
brief text in every field (a sentence or two). Use exactly this compact format:

{"cf":1,"id":"kebab-case-slug","name":"Short plan title","color":"#3b82f6","days":14,"b":[["leverage 1","leverage 2","leverage 3"],"what to ignore and why","time wasters to skip","realistic timeline"],"p":[["Phase name",[[1,"Short task title","detail: what to do, for how long, what done looks like","mentor note: why now, the usual mistake, what right looks like","practice",60,["App or site"]],[2,"Next task","detail","mentor note","theory",45,[]]]],["Next phase",[]]]}

Each task is an array: [day, title, detail, mentor note, category, minutes, tools]. Put the
concrete how-to in detail and the mentor commentary in the mentor note.

In "tools" (the 7th item of a task) list the specific applications, tools or websites I'll actually
have open for it, real names such as "VS Code", "Anki", "Chrome - Coursera", "Excel" - never generic
("a code editor", "the internet"), never a bare "Chrome", "Browser" or "Terminal" that I also use for
unrelated things. The list is imported into my tracking app's on-plan activity list, so be precise
enough not to match unrelated apps. Use [] for a task that is not tied to any app (a purely offline exercise).
""";

    public const string Goal = """
I need to {subject} as effectively and quickly as realistically possible. I'm
not naive about how hard real-world goals are — I want a plan, not a pep talk.
Act as a {claude_role} with 10+ years of hands-on experience getting people
through exactly this kind of goal in {area_of_interest}. Talk to me like an
operator who has done this dozens of times for clients, not like a
motivational speaker. Write the plan the way a mentor would: for each step say why it
matters at this point, what people trying to {subject} typically get wrong there, and what
doing it right looks like.

Before any plan, work out a short, blunt briefing: (1) the 3 decisions or actions that
determine ~80% of whether this succeeds, fails or drags on, specific to my situation; (2) what
to ignore completely (research rabbit holes, comparison shopping, "just in case" prep);
(3) the mistakes or wasted time that trip up most people, and how to route around them;
(4) a realistic timeline, calling out anything outside my control (approvals, waiting periods,
other people's schedules) that sets a floor, and what I can compress. Do NOT write the
briefing in the chat: it goes only inside the JSON, in "b" below.

Then build a day-by-day plan to get there in the shortest realistic time. Every step is a
concrete action I can take that day (a call to make, a document to gather, a decision to lock
in), never "consider" or "think about"; sequence dependencies correctly; call out days that
only wait on something external and what to do in parallel; be honest if a step needs one hour
or several days. Several tasks may share a day. Categories: research, decision, logistics,
execution.

To keep your reply short and fast, answer with ONLY one compact JSON code block
(```json ... ```): no text before or after it, no line breaks or indentation inside it, and
brief text in every field (a sentence or two). Use exactly this compact format:

{"cf":1,"id":"kebab-case-slug","name":"Short plan title","color":"#3b82f6","days":30,"b":[["decision 1","decision 2","decision 3"],"what to ignore and why","mistakes to avoid","realistic timeline"],"p":[["Phase name",[[1,"Short task title","detail: what to do and what done looks like","mentor note: why now, the usual mistake, what right looks like","research",60,["App or site"]],[2,"Next task","detail","mentor note","decision",30,[]]]],["Next phase",[]]]}

Each task is an array: [day, title, detail, mentor note, category, minutes, tools]. Put the
concrete how-to in detail and the mentor commentary in the mentor note.

In "tools" (the 7th item of a task) list the specific applications, tools or websites I'll actually
have open for it, real names such as "Chrome - IND.nl", "Gmail", "DigiD app", "Excel" - never generic
("a website", "email"), never a bare "Chrome", "Browser" or "Email" that I also use for unrelated
things. The list is imported into my tracking app's on-plan activity list, so be precise enough not to
match unrelated apps. Use [] for a task that is not tied to any app (a phone call, an in-person errand).
""";

    /// <summary>Replace-remaining prompt (spec replace-remaining-tasks): Claude rewrites only the
    /// unfinished part of a plan, and is told which tasks are fixed so it does not repeat them.
    /// Placeholders are filled by <see cref="ReplaceRemainder"/> via string.Replace.</summary>
    public const string ReplaceRemainderTemplate = """
I'm revising my plan "{plan_name}" in my personal tracking app. Some of its tasks are already
ticked complete, and those stay exactly as they are. Rewrite only the remaining work. Do not
include the ticked tasks below, and do not repeat them under a new title.

Ticked tasks (fixed, do not repeat):
{done_list}

Tasks to be replaced (the unfinished ones, in full):
{remaining_list}

What I want changed:
{change_text}

Apply the requested change to every place it appears, including inside detail text, mentor notes
and tools of tasks that were not otherwise touched. Keep the remaining work otherwise as it is,
unless the change requires otherwise.

Number the days of the new tasks starting from day {start_day}, and keep whatever spacing between
days makes sense for the work. Every task title must be unique and must
not match any ticked task above.

To keep your reply short and fast, answer with ONLY one compact JSON code block (```json ... ```):
no text before or after it, no line breaks or indentation inside it, and brief text in every field.
Use exactly this compact format:

{"cf":1,"name":"{plan_name}","p":[["Phase name",[[{start_day},"Short task title","detail: what to do, for how long, what done looks like","mentor note: why now, the usual mistake, what right looks like","practice",60,["App or site"]]]]]}

Each task is an array: [day, title, detail, mentor note, category (one word, for example practice or
review), minutes, tools]. In tools list only real, precise app or website names I'll have open for
that task, and use [] where no particular app is needed.
""";

    /// <summary>The change text used when the "What should change?" field is empty or whitespace.</summary>
    public const string NoChangeRequested = "No specific change requested; improve and re-plan the remaining work as you see fit.";

    /// <summary>
    /// Fills <see cref="ReplaceRemainderTemplate"/> for one plan. With no ticked tasks the done list
    /// reads "(none yet)". Filling is a single pass: the values (plan name, done list, the remaining
    /// tasks, the change text) are inserted and never re-scanned, so braces typed by the user or
    /// present in task text (for example "{start_day}") appear literally in the prompt.
    /// </summary>
    public static string ReplaceRemainder(string planName, IReadOnlyList<PlanReplacement.DoneTask> done,
        IReadOnlyList<PlanReplacement.RemainingTask> remaining, int startDay, string changeText)
    {
        var doneList = done.Count == 0
            ? "(none yet)"
            : string.Join("\n", done.Select(d => $"Day {d.AssignedDay}: {d.Title}"));
        var remainingList = remaining.Count == 0
            ? "(none)"
            : string.Join("\n\n", remaining.Select(RemainingBlock));
        var change = string.IsNullOrWhiteSpace(changeText) ? NoChangeRequested : changeText;

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["plan_name"] = planName,
            ["done_list"] = doneList,
            ["remaining_list"] = remainingList,
            ["change_text"] = change,
            ["start_day"] = startDay.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        return System.Text.RegularExpressions.Regex.Replace(
            ReplaceRemainderTemplate,
            @"\{(plan_name|done_list|remaining_list|change_text|start_day)\}",
            m => values[m.Groups[1].Value]);
    }

    /// <summary>One remaining task as plain text (spec replace-remaining-v2, Chosen approach). Every
    /// label is always present; missing values read "(none)". Continuation lines of a multi-line
    /// field are indented under its label.</summary>
    private static string RemainingBlock(PlanReplacement.RemainingTask task)
    {
        var tools = task.Tools.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        var duration = task.DurationMin.HasValue
            ? task.DurationMin.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + " min"
            : "(none)";
        return string.Join("\n",
            $"Day {task.AssignedDay}: {task.Title}",
            $"  Detail: {Field(task.Detail)}",
            $"  Mentor note: {Field(task.MentorNote)}",
            $"  Category: {Field(task.Category)}",
            $"  Duration: {duration}",
            $"  Tools: {(tools.Count == 0 ? "(none)" : string.Join(", ", tools))}");
    }

    private static string Field(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value.Replace("\n", "\n    ");

    public const string Reformat = """
I already have a plan for "{subject}" that I wrote myself. Reformat it into the JSON structure
below so I can import it into my personal tracking app. Don't rewrite my content, don't add new
tasks, don't change my sequencing or timeline. Where something is ambiguous (a missing day number,
a missing duration), make the most reasonable inference from context rather than inventing new
content that wasn't there.

Here is my plan, exactly as I wrote it:
---
{user_plan}
---

To keep your reply short and fast, answer with ONLY one compact JSON code block (```json ... ```):
no text before or after it and no line breaks or indentation inside it. Use exactly this compact format:

{"cf":1,"id":"kebab-case-slug","name":"{subject}","color":"#3b82f6","days":14,"p":[["Phase name: mine if I had them, otherwise group logically",[[1,"Short task title, my own wording","detail from what I wrote, expanded only for clarity","","practice",60,["App or site"]]]]]}

Each task is an array: [day, title, detail, mentor note, category, minutes, tools]. Leave the mentor
note "" and leave out the plan-level briefing ("b": [[high-leverage things],"ignore","time wasters",
"timeline"]) unless you genuinely have something useful to add beyond what I wrote. Category: the
closest of theory, practice, project, review, research, decision, logistics, execution, or "" if
nothing fits.

Tools: only where my own text names or clearly implies a specific app, tool or website for that task,
and then its real name precise enough not to match unrelated apps ("Chrome - LinkedIn", not bare
"Chrome") - the list is imported into my tracking app's on-plan activity list. Use [] for a task where
nothing in my text points to a specific tool.
""";
}
