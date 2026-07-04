# The Complete 10-Level Claude Code Mastery Guide

> Step-by-step projects, timelines, and test exercises to take you from Terminal to Routines

---

## 📖 How to Use This Guide

Each level builds directly on the previous one. Work through them in order using the test project for each level — don't skip ahead. Each section contains:

- **Concept** — what the feature does and why it matters
- **Step-by-step instructions** — exactly what to do
- **Test project** — a real mini-project to build
- **Success checklist** — how you know you've nailed it
- **Common mistakes** — pitfalls to avoid

---

## 📅 Master Timeline

Total estimated time: **4 weeks** (part-time, ~1–2 hrs/day)

| Timeline | Levels | Focus | Goal |
|---|---|---|---|
| Days 1–3 | Lvl 1–2: Terminal + CLAUDE.md | Installation & memory setup | Claude reads your project |
| Days 4–5 | Lvl 3: Commands | Session management | Control your context window |
| Days 6–8 | Lvl 4: Custom Commands | Build your own slash commands | One token = full workflow |
| Days 9–11 | Lvl 5: Skills | Package multi-step workflows | Claude self-triggers routines |
| Days 12–14 | Lvl 6: MCP | Connect external services | Claude talks to your tools |
| Days 15–17 | Lvl 7: Subagents | Parallel task execution | Fan out 3+ tasks at once |
| Days 18–19 | Lvl 8: Hooks | Event-driven automation | Scripts run on lifecycle events |
| Days 20–21 | Lvl 9: Headless | CI/CD & scripting integration | Claude runs with no terminal |
| Days 22–28 | Lvl 10: Routines | Scheduled autonomous tasks | Claude works while you sleep |

---

# WEEK 1 · Days 1–8
## Foundation: Getting Claude into your workflow

---

## 🗂️ Level 1 — Terminal

> You open Claude right in your terminal and ask it to write or fix code where your project already lives.

### 📚 Concept

Claude Code is a command-line tool. Instead of copying code into a chat window, you run it inside your actual project directory. Claude can see your files, run commands, and edit code directly — like pairing with a senior engineer who lives in your terminal.

### 🛠️ Step-by-Step Setup

1. Install Node.js v18+ if you don't have it: [nodejs.org](https://nodejs.org)
2. Install Claude Code globally:
```bash
npm install -g @anthropic-ai/claude-code
```
3. Log in with your Anthropic account:
```bash
claude login
```
4. Navigate to any project folder and start a session:
```bash
cd my-project
claude
```
5. Type a task in plain English: *"List all the files in this project and explain what each one does"*
6. Watch Claude read your files and respond in context

### 🧪 Test Project: The Bug Hunt

**Project:** Fix 3 bugs in a starter app
**Time:** ~1.5 hours | **Difficulty:** Beginner

1. Create a new folder and add a small JavaScript file with 3 intentional bugs (wrong variable name, missing return, off-by-one error)
2. Open Claude in that folder: `cd bug-project && claude`
3. Tell Claude: *"Read app.js and find any bugs. Fix them one at a time and explain each fix."*
4. Watch Claude read the file, explain each bug, and apply fixes
5. Ask a follow-up: *"Now add a unit test for the fixed function"*
6. Ask Claude to run the test: *"Run the test with node and tell me if it passes"*

### ✅ Success Checklist

- [ ] Claude installed and runs without errors
- [ ] Claude can read files in your project without you pasting them
- [ ] You've had a multi-turn conversation within one session
- [ ] Claude fixed at least one real bug and explained why
- [ ] You understand how Claude sees your working directory

### ⚠️ Common Mistakes

- Running `claude` from your home directory — always `cd` into the project first
- Treating it like a chat window — Claude can actually RUN code, let it
- Not being specific — *"fix my code"* is weak; *"fix the null check in getUser() in user.js"* is strong

---

## 🗂️ Level 2 — CLAUDE.md

> Claude reads this memory file at the start of every session, so it remembers your stack and rules without you repeating them.

### 📚 Concept

Every time you start a new Claude session, it starts fresh with no memory of past conversations. CLAUDE.md fixes this. It's a markdown file in your project root that Claude automatically reads at the start of every session. Put your stack, conventions, and rules in it — Claude will follow them every time.

### 🛠️ Step-by-Step Setup

1. In your project root, create a file named `CLAUDE.md`
2. Add your project context using this structure:

```markdown
# CLAUDE.md

## Stack
Next.js 14, TypeScript, PostgreSQL, Tailwind CSS

## Conventions
- kebab-case for all file names
- Named exports only (no default exports)
- Use async/await, never .then()
- All API routes in /app/api/

## Rules
- Never edit files in /generated/
- Always add JSDoc comments to new functions
- Run `npm test` after any logic changes

## Architecture
- Auth handled by Clerk (never roll custom auth)
- DB queries in /lib/db.ts only
```

3. Start a new Claude session and say: *"What do you know about this project?"*
4. Claude should recite back your stack, conventions and rules — without you telling it again

### 🧪 Test Project: The Memory File

**Project:** Build a CLAUDE.md for a real project you work on
**Time:** ~1 hour | **Difficulty:** Beginner

1. Take any project you already have (or create a fresh Next.js app)
2. Create `CLAUDE.md` with at least: Stack, Conventions, Rules, and one Architecture note
3. Start a Claude session and ask it to create a new API route
4. Check: did it use your naming conventions? Did it put it in the right folder?
5. If not, update `CLAUDE.md` to be more specific and test again
6. Final test: close the session, open a new one, ask the same question — result should be identical

### ✅ Success Checklist

- [ ] CLAUDE.md is in your project root
- [ ] Claude recites your stack correctly at session start
- [ ] Claude generates code following your naming conventions without being told
- [ ] Claude respects at least one "Never do X" rule in your file
- [ ] You've iterated the file at least once based on Claude not following a rule

### ⚠️ Common Mistakes

- Being vague — *"use good practices"* means nothing; *"use async/await not .then()"* is specific
- Putting CLAUDE.md in a subfolder — it must be at the project root
- Forgetting to mention things you'd tell a new developer on day one

---

## 🗂️ Level 3 — Commands

> Built-in commands steer the session itself — clear history, compact the context, or see what's filling your window.

### 📚 Concept

Claude's context window is limited. During long sessions, it fills up with old conversation turns, wasting space on things Claude already knows. Built-in commands let you manage the session directly: clear it, compress it, or inspect it. Mastering this prevents Claude from "forgetting" halfway through a task.

### 🛠️ The Core Commands

| Command | What It Does |
|---|---|
| `/help` | Show all available commands |
| `/status` | Show your current setup and model |
| `/clear` | Wipe the conversation — fresh start, same session |
| `/compact` | Summarise and compress history — keeps context, saves space |
| `/context` | Show exactly what is filling your context window right now |

### 🧪 Test Project: The Long Session

**Project:** Build a feature across a long session using command management
**Time:** ~1 hour | **Difficulty:** Beginner

1. Start a Claude session and build a small CRUD feature (e.g. a todo list with add/remove/list)
2. After each file Claude creates, ask it to also write a test for it
3. After 10+ exchanges, type `/context` — look at how much of the window is used
4. Type `/compact` — Claude will summarise everything into a dense note
5. Type `/context` again — compare the window size
6. Continue building: ask Claude to add a search feature. Verify it still knows your rules
7. At the end, type `/clear` — notice how Claude loses the conversation but CLAUDE.md persists

### ✅ Success Checklist

- [ ] You've used `/context` at least twice in one session
- [ ] You've used `/compact` and seen the context size reduce
- [ ] You understand the difference between `/clear` (wipe) and `/compact` (compress)
- [ ] Claude continued working correctly after a `/compact`
- [ ] You know when to use each command

### ⚠️ Common Mistakes

- Using `/clear` when you mean `/compact` — `/clear` loses all context
- Never checking `/context` — you'll hit the limit suddenly mid-task
- Not running `/compact` proactively — do it before long coding tasks, not after you've hit the wall

---

# WEEK 2 · Days 6–14
## Power User: Customising Claude to your workflow

---

## 🗂️ Level 4 — Custom Commands

> Save a prompt you keep retyping as your own command, so a whole routine fires from one short token like /commit.

### 📚 Concept

Every developer has prompts they type repeatedly: *"write a commit message for this diff"*, *"review this PR for security issues"*, *"update the README to match the new API"*. Custom commands let you save these as named files. Type `/commit` and the full commit-writing routine fires instantly. No more retyping.

### 🛠️ Step-by-Step Setup

1. Create the global commands directory:
```bash
mkdir -p ~/.claude/commands
```

2. Create your first command — a commit message generator:
```bash
cat > ~/.claude/commands/commit.md << 'EOF'
Look at the current git diff using Bash(git diff --staged).
Analyse the changes and write a conventional commit message.
Format: type(scope): description
Types: feat, fix, docs, style, refactor, test, chore
Keep the description under 72 characters.
EOF
```

3. In any project with staged changes, type `/commit` — Claude runs the full routine

4. Create a second command for code review:
```bash
cat > ~/.claude/commands/review.md << 'EOF'
Review the current git diff for:
1) Security vulnerabilities
2) Performance issues
3) Missing error handling
4) Code style violations
Output a prioritised list of issues with suggested fixes.
EOF
```

### 🧪 Test Project: The Personal Command Library

**Project:** Build 5 custom commands for your real dev workflow
**Time:** ~2 hours | **Difficulty:** Intermediate

| Command | What it does |
|---|---|
| `/commit` | Reads staged diff, writes a conventional commit message, offers to run git commit |
| `/review` | Audits current diff for security, performance, and style issues |
| `/docs` | Reads current file, generates JSDoc comments for every function |
| `/test` | Reads a function or file, generates a full test suite |
| `/cleanup` | Scans for unused imports, console.logs, and dead code — removes them |

1. Create each command file in `~/.claude/commands/`
2. Test each one on a real file in a project
3. Iterate the prompt in the command file until the output is exactly what you want
4. Time yourself: how long did each command save vs typing manually?

### ✅ Success Checklist

- [ ] You have at least 5 commands in `~/.claude/commands/`
- [ ] `/commit` produces a properly formatted conventional commit message
- [ ] `/review` catches at least one real issue in a diff
- [ ] You've iterated at least one command prompt after seeing weak output
- [ ] You've used a command with an argument (e.g. `/docs utils.ts`)

### ⚠️ Common Mistakes

- Making commands too generic — the more specific the instruction, the better the output
- Not testing the command on multiple files — a command that works on one may fail on another
- Forgetting commands can use `$ARGUMENTS` — `/docs $ARGUMENTS` lets you pass a filename

---

## 🗂️ Level 5 — Skills

> A skill packages a whole workflow Claude triggers on its own when the moment fits — you never have to remember to run it.

### 📚 Concept

Custom commands (Lvl 4) require you to type `/something`. Skills go further: Claude recognises when a situation calls for a skill and triggers it automatically. A skill bundles a complete workflow — reading context, making decisions, executing steps — and Claude invokes it without being asked. Think of it as giving Claude instincts.

### 🛠️ Step-by-Step Setup

1. Skills live in a `skills/` folder inside your project (or `~/.claude/skills/` for global ones)
2. Each skill is a markdown file with a name, description, and step-by-step instructions
3. Create your first skill:
```bash
mkdir -p .claude/skills
```

4. Structure a skill file like this:

```markdown
---
name: security-review
description: Audit a code diff or file for security vulnerabilities.
  Trigger when: reviewing auth code, handling user input, working with
  passwords, tokens, or database queries.
---

# Security Review Skill

1. Read the target file or diff carefully
2. Check for: SQL injection, XSS, hardcoded secrets, missing auth checks,
   insecure direct object references, missing rate limiting
3. Output a severity-ranked list: CRITICAL / HIGH / MEDIUM / LOW
4. For each issue: show the vulnerable line, explain the risk, provide a fix
5. If no issues found, confirm what was checked and give it a clean bill
```

5. Test it by asking Claude to review an auth file — it should trigger automatically

### 🧪 Test Project: The Skill Toolkit

**Project:** Create 3 skills that auto-trigger on relevant tasks
**Time:** ~2.5 hours | **Difficulty:** Intermediate

| Skill | When it triggers |
|---|---|
| `security-review` | Auto-triggers when editing auth, login, password, or token-related code |
| `api-design` | Auto-triggers when creating new API routes — checks REST conventions, error handling, validation |
| `pr-prep` | Auto-triggers before a commit — checks tests pass, docs are updated, no debug code left |

1. Create each skill file with a clear trigger description in the frontmatter
2. Test each skill by doing the thing it monitors (e.g. edit a login function)
3. Verify Claude triggers the skill without you asking
4. Iterate the trigger description if it fires too often or not often enough

### ✅ Success Checklist

- [ ] You have at least 3 skills in `.claude/skills/`
- [ ] At least one skill triggers automatically without you typing a command
- [ ] Skills produce structured, multi-step output (not just a single response)
- [ ] You can explain the difference between a skill and a custom command
- [ ] You've adjusted a skill's trigger description based on when it fired

### ⚠️ Common Mistakes

- Unclear trigger descriptions — be specific about WHEN Claude should use the skill
- Overly broad triggers — a skill that fires on every file edit is noise, not signal
- Skipping the frontmatter `---` section — this is how Claude reads the metadata

---

## 🗂️ Level 6 — MCP

> MCP servers connect Claude to your real tools and data — your database, your repos, the services you already run.

### 📚 Concept

Without MCP, Claude can only see your local files. With MCP (Model Context Protocol), Claude can query your PostgreSQL database, create GitHub issues, search your Slack, read your Notion docs, and charge a card through Stripe — all from a single conversation. Each MCP server is a bridge between Claude and a real external service.

### 🛠️ Step-by-Step: Connect GitHub MCP

1. Install the GitHub MCP server:
```bash
npm install -g @modelcontextprotocol/server-github
```

2. Add it to your Claude config (`~/.claude/settings.json`):
```json
{
  "mcpServers": {
    "github": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-github"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "your_token_here"
      }
    }
  }
}
```

3. Restart Claude and type: *"List my last 5 open GitHub issues"*
4. Claude should now fetch live data from your GitHub without you opening a browser

### 🛠️ Step-by-Step: Connect PostgreSQL MCP

```bash
npm install -g @modelcontextprotocol/server-postgres
```

Add to `settings.json`:
```json
{
  "mcpServers": {
    "postgres": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-postgres",
               "postgresql://user:pass@localhost/mydb"]
    }
  }
}
```

### 🧪 Test Project: The Connected Workflow

**Project:** Connect 2 MCP servers and build a workflow that uses both
**Time:** ~3 hours | **Difficulty:** Intermediate-Advanced

1. Connect GitHub MCP and PostgreSQL MCP (or Notion/Slack if you prefer)
2. Build a workflow: *"Look at my 5 most recent GitHub issues. For each one, check if there's a related record in my issues table in Postgres. For any issue not in the DB, create a record."*
3. This forces Claude to use both MCPs together in sequence
4. Extend it: *"Now close any GitHub issues that are marked resolved in the DB"*
5. Bonus: Add Slack MCP and make Claude post a summary to a channel when done

### ✅ Success Checklist

- [ ] At least 2 MCP servers configured in `settings.json`
- [ ] Claude fetches live data from at least one external service
- [ ] Claude writes data back to at least one service (creates issue, inserts row, etc.)
- [ ] You've built a workflow that chains 2 MCP servers together
- [ ] You understand what an MCP server is and could add a new one in 10 minutes

### ⚠️ Common Mistakes

- Storing API tokens directly in `settings.json` committed to Git — use environment variables
- Not restarting Claude after adding a new MCP server — changes require a restart
- Connecting too many servers at once — add one, verify it works, then add the next

---

# WEEK 3 · Days 15–21
## Automation: Claude runs without you watching

---

## 🗂️ Level 7 — Subagents

> You fan out to subagents that run in parallel, so several tasks move at once while you do something else.

### 📚 Concept

Most AI sessions are linear: one task, then the next. Subagents break this. You give Claude a complex goal and it spins up multiple specialised agents running in parallel — one designing the UI, one writing the backend, one drafting the tests — all at the same time. When they finish, Claude synthesises the results. This is multiplied throughput.

### 🛠️ Step-by-Step Setup

Frame your task as a parallel problem. Instead of: *"Build a user dashboard"* — write it as independent workstreams:

```
I need to build a User Dashboard page. Spin up 3 parallel agents:

Agent 1 (product-manager): Define the user stories and
  acceptance criteria for the dashboard

Agent 2 (ux-designer): Design the component layout and
  data flow — output a component tree

Agent 3 (backend-engineer): Define the API endpoints
  needed and their response schemas

Run all three in parallel. When done, synthesise their
output into a single implementation spec.
```

Claude will initialise each agent, show their progress, and merge results. Use the synthesised spec to guide the actual coding session.

### 🧪 Test Project: The Parallel Feature Build

**Project:** Build a complete feature using parallel subagents
**Time:** ~2 hours | **Difficulty:** Advanced

Choose a feature: *"Add a notifications system to my app"*. Structure it for subagents:

1. **Agent 1 (product):** List all notification types, triggers, and user preferences needed
2. **Agent 2 (frontend):** Design the notification bell component, dropdown, and badge
3. **Agent 3 (backend):** Design the notifications table schema, API endpoints, and WebSocket events
4. **Agent 4 (devops):** Define the queue setup (Redis/BullMQ) needed to process notifications async
5. After synthesis: use the output to actually build each component in separate Claude sessions
6. Compare total time: parallel spec (1 session) vs sequential (4 sessions)

### ✅ Success Checklist

- [ ] You've triggered at least 3 parallel subagents in one Claude session
- [ ] Agents ran genuinely in parallel (progress shown simultaneously)
- [ ] Claude produced a synthesised output combining all agent results
- [ ] You used the output to drive a subsequent coding task
- [ ] You understand when to use subagents vs a single session

### ⚠️ Common Mistakes

- Using subagents for tasks that depend on each other — agents run in parallel, not in sequence
- Poorly named agents — *"agent1"* produces weaker results than *"ux-designer"* or *"security-engineer"*
- Not asking for synthesis at the end — always explicitly ask Claude to merge the outputs

---

## 🗂️ Level 8 — Hooks

> Hooks run your own scripts automatically on an event — when a session starts, before a tool runs, or the moment Claude stops.

### 📚 Concept

Hooks are scripts that fire at specific lifecycle events in Claude's session. They run automatically — no prompting required. You define them in `settings.json` and they execute whenever the named event occurs. This is how you build guardrails, logging, formatting, and notification systems around Claude.

### 🛠️ The Hook Events

| Event | When it fires |
|---|---|
| `PreToolUse` | Fires BEFORE Claude runs any tool (bash, file edit, etc.) — use to block or log |
| `PostToolUse` | Fires AFTER a tool runs — use to validate output or trigger follow-up actions |
| `Notification` | Fires when Claude sends a notification (task complete, needs input, etc.) |
| `Stop` | Fires when Claude finishes its response — use for cleanup or summaries |

### 🛠️ Step-by-Step: Your First Hook

1. Open `~/.claude/settings.json` and add a hooks section:
```json
{
  "hooks": {
    "Stop": [
      {
        "matcher": "",
        "hooks": [{
          "type": "command",
          "command": "echo \"Session ended at $(date)\" >> ~/claude-sessions.log"
        }]
      }
    ]
  }
}
```

2. Start a Claude session, do some work, exit — check `~/claude-sessions.log`

3. Add a PreToolUse hook that logs every bash command Claude runs:
```json
"PreToolUse": [{
  "matcher": "Bash",
  "hooks": [{"type": "command",
    "command": "echo \"Claude ran bash: $CLAUDE_TOOL_INPUT\" >> ~/claude-commands.log"
  }]
}]
```

### 🧪 Test Project: The Automation Layer

**Project:** Build 3 hooks that run automatically around Claude's work
**Time:** ~2 hours | **Difficulty:** Advanced

| Hook | What it does |
|---|---|
| Session Logger | `Stop` hook — logs timestamp, project folder, and summary to `~/claude-log.txt` every session |
| Auto Formatter | `PostToolUse` hook on file edits — runs prettier on any `.ts` or `.js` file Claude modifies |
| Safety Guard | `PreToolUse` hook on Bash — logs any `rm`, `drop`, or `delete` commands to a `safety-audit.log` before they run |

1. Implement each hook in `settings.json`
2. Run Claude sessions that trigger each hook
3. Check the log files to verify hooks are firing
4. Extend the Safety Guard: make it ask for confirmation before any `rm -rf` command

### ✅ Success Checklist

- [ ] You have at least 3 hooks in `settings.json`
- [ ] At least one hook fires on `Stop`
- [ ] At least one hook fires on `PreToolUse` or `PostToolUse`
- [ ] You can read your session log and see exactly what Claude did
- [ ] Auto-formatter runs without you asking after every file edit

### ⚠️ Common Mistakes

- Shell syntax errors in hook commands — always test the command in terminal first
- Hooks that take too long — hooks should complete in <2 seconds or they block Claude
- Forgetting exact event name casing — it's `PreToolUse` not `pretooluse`

---

## 🗂️ Level 9 — Headless

> You run Claude headless and script it, so it works inside your CI and automation with no terminal open.

### 📚 Concept

Everything up to now has required you to sit at a terminal. Headless mode removes that constraint. You call Claude non-interactively from a shell script, pipe its output to other tools, embed it in GitHub Actions, or chain it into any automated pipeline. Claude becomes a programmable step in your infrastructure.

### 🛠️ Step-by-Step: Headless Basics

1. Run Claude with a single task and capture the output:
```bash
claude -p "List all .ts files in src/ and count how many there are" \
  --output-format json | jq '.result'
```

2. Pipe Claude into a processing chain:
```bash
# Get Claude to summarise all TODO comments in the codebase
grep -r "TODO" src/ | claude -p "Summarise these TODO comments \
  and group them by urgency (critical/normal/low)" \
  --output-format json | jq -r '.result'
```

3. Use Claude in a shell script:
```bash
#!/bin/bash
# daily-audit.sh

cd ~/my-project

RESULT=$(claude -p "Read package.json and identify any dependencies \
  with known security issues. Output JSON: {issues: [{pkg, severity, reason}]}" \
  --output-format json | jq '.result')

echo "Audit complete: $RESULT"
```

### 🧪 Test Project: The CI Pipeline Integration

**Project:** Add Claude as an automated step in a GitHub Actions workflow
**Time:** ~3 hours | **Difficulty:** Advanced

1. Create `.github/workflows/claude-review.yml`
2. On every pull request: check out the code, install Claude Code, run a headless security review on the diff
3. Post the review output as a PR comment automatically
4. Add a second step: if Claude finds CRITICAL issues, fail the CI check
5. Test it by opening a PR with a deliberate security issue (e.g. a hardcoded password)

```yaml
# Skeleton workflow
name: Claude Code Review
on: [pull_request]
jobs:
  review:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: npm install -g @anthropic-ai/claude-code
      - run: |
          git diff origin/main...HEAD > diff.txt
          claude -p "Review this diff for security issues: $(cat diff.txt)" \
            --output-format json > review.json
        env:
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
      - name: Post review as PR comment
        uses: actions/github-script@v7
        with:
          script: |
            const review = require('./review.json');
            github.rest.issues.createComment({...context.repo,
              issue_number: context.issue.number,
              body: review.result});
```

### ✅ Success Checklist

- [ ] `claude -p` runs and returns output non-interactively
- [ ] You can pipe Claude's output to `jq` and parse specific fields
- [ ] You have a working shell script that uses Claude
- [ ] Claude runs in at least one GitHub Actions workflow
- [ ] CI posts Claude's review as a PR comment automatically

### ⚠️ Common Mistakes

- Forgetting `--output-format json` — without it, output is markdown that's hard to parse
- Not setting `ANTHROPIC_API_KEY` in CI environment secrets — headless needs this to authenticate
- Tasks that require back-and-forth — headless is for single-shot tasks, not multi-turn conversations

---

# WEEK 4 · Days 22–28
## Full Autonomy: Claude works while you sleep

---

## 🗂️ Level 10 — Routines

> You put Claude on a schedule so it runs itself on a loop and does the job while you are asleep.

### 📚 Concept

Routines are the final level: fully autonomous, scheduled Claude tasks. You combine everything you've learned — headless mode, MCP connections, hooks, and skills — and put the whole system on a cron schedule. While you sleep, Claude audits your dependencies, checks for failing tests, monitors your API for errors, and files issues. You wake up to a finished report.

### 🛠️ Step-by-Step: Your First Routine

1. Create a shell script that Claude will execute on a schedule:
```bash
#!/bin/bash
# ~/routines/nightly-audit.sh

cd ~/my-project

# 1. Dependency audit
claude -p "Run npm audit and summarise any HIGH or CRITICAL vulnerabilities. \
  Output JSON: {critical: [], high: []}" \
  --output-format json > /tmp/audit.json

# 2. Dead code check
claude -p "Scan the src/ directory for unused exports, dead code, \
  and console.log statements. List file:line for each." \
  --output-format json > /tmp/deadcode.json

# 3. Combine and send report
node ~/routines/send-report.js
```

2. Make the script executable:
```bash
chmod +x ~/routines/nightly-audit.sh
```

3. Schedule it with cron (runs every night at 2am):
```bash
# Edit crontab
crontab -e

# Add this line:
0 2 * * * /bin/bash ~/routines/nightly-audit.sh >> ~/routines/audit.log 2>&1
```

4. Verify it works by running manually first:
```bash
bash ~/routines/nightly-audit.sh
```

### 🧪 Test Project: The Autonomous Dev Assistant

**Project:** Build a full autonomous routine system with 3 scheduled tasks
**Time:** ~4 hours | **Difficulty:** Expert

| Routine | Schedule | What it does |
|---|---|---|
| Nightly Audit | 2:00 AM daily | Run npm audit + check for console.logs + verify all tests pass. Email a pass/fail report. |
| Dependency Update | Monday 9:00 AM | Use GitHub MCP to check for outdated packages. Open a PR with the updates. Assign it to you. |
| Error Monitor | Every 15 minutes | Query your database (Postgres MCP) for error_logs entries in the last 15 min. If found, post a Slack alert. |

1. Build each script using the patterns from Lvl 9 (headless) + Lvl 6 (MCP)
2. Schedule all three using cron
3. Run the Error Monitor manually first to confirm it works before scheduling
4. Leave your computer for 24 hours — verify all 3 ran in the logs the next day
5. Make at least one deliberate error in your project and verify the monitor catches it

### ✅ Success Checklist

- [ ] You have at least 3 routines running on a cron schedule
- [ ] At least one routine uses MCP to interact with an external service
- [ ] At least one routine creates something (PR, issue, report) without you touching it
- [ ] You receive an alert/notification from a routine without manually triggering it
- [ ] All routines log their output and you can audit what Claude did overnight

### ⚠️ Common Mistakes

- Not using absolute paths in cron scripts — cron has no PATH; use `/usr/bin/node` not `node`
- No error handling — if a script fails silently, you'll never know; always redirect stderr to the log
- Running too frequently without rate limiting — Claude API calls cost money; schedule wisely
- Granting routines write access before testing them thoroughly in read-only mode first

---

# 🎓 You've Reached Level 10

Here's what you can now do that most developers can't:

| Level | What you unlocked |
|---|---|
| Lvl 1 · Terminal | Claude lives inside your project — not a chat window |
| Lvl 2 · CLAUDE.md | Claude knows your stack and rules from session one |
| Lvl 3 · Commands | You control the context window — no more forgotten context |
| Lvl 4 · Custom | One word triggers your entire workflow |
| Lvl 5 · Skills | Claude self-triggers the right workflow at the right moment |
| Lvl 6 · MCP | Claude talks to your real tools — GitHub, DB, Slack, Stripe |
| Lvl 7 · Subagents | Multiple agents work in parallel — 4x throughput |
| Lvl 8 · Hooks | Automated scripts run around every Claude action |
| Lvl 9 · Headless | Claude runs in CI, scripts, and pipelines — no human needed |
| Lvl 10 · Routines | Claude works autonomously on a schedule — 24/7 |

---

## 📚 Resources to Go Deeper

- Official docs: [docs.claude.ai/en/docs/claude-code](https://docs.claude.ai/en/docs/claude-code)
- MCP servers directory: [modelcontextprotocol.io/servers](https://modelcontextprotocol.io/servers)
- Claude Code GitHub: [github.com/anthropics/claude-code](https://github.com/anthropics/claude-code)
- Community skills: [github.com/anthropics/claude-code/tree/main/skills](https://github.com/anthropics/claude-code/tree/main/skills)
