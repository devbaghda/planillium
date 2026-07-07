#!/usr/bin/env bash
set -e
# Installs the Windows app launch-team skills for Claude Code:
#   windows-app-auditor  — find defects (5 passes incl. privacy & compliance) + remediation loop
#   windows-code-refiner — behavior-preserving cleanup & documentation
#   windows-app-tester   — test suites, smoke harness, soak/runtime QA
#   windows-app-releaser — installers, signing, versioning, update path, crash reporting
#   launch-readiness     — go/no-go gate + beta program playbook
# Run from your project root. Defaults to project-local .claude/skills.
# Pass --global to install into ~/.claude/skills instead (available in every project).
# Regenerated 2026-07-07 from mentor-overseer/.claude/skills.

TARGET=".claude/skills"
if [ "$1" = "--global" ]; then TARGET="$HOME/.claude/skills"; fi
echo "Installing skills into: $TARGET"
mkdir -p "$TARGET/windows-app-auditor/references"
mkdir -p "$TARGET/windows-code-refiner/references"
mkdir -p "$TARGET/windows-app-tester/references"
mkdir -p "$TARGET/windows-app-releaser/references"
mkdir -p "$TARGET/launch-readiness/references"

cat > "$TARGET/windows-app-auditor/SKILL.md" << 'SKILL_EOF'
---
name: windows-app-auditor
description: >-
  Audit a Windows desktop application for production-readiness across architecture/back-end health,
  security, product & UX/UI quality (including audience fit, design language, and application-logic
  sanity), code quality, and privacy & compliance (data inventory, consent, retention, licenses),
  and produce a prioritized findings report with file:line evidence and concrete fixes. Use this whenever the user wants to review, audit, harden, "check," or "is this ready
  to ship" a Windows app (.NET / WPF / WinForms / MAUI / WinUI / packaged or unpackaged), or asks for a
  code review, security review, design review, pre-release check, or quality pass on a desktop
  application we are building together. Trigger even when the user just says "review the app," "is this
  good," "is it user-friendly," "does the design work," "any problems before we ship," or "look over
  what we built" in the context of a Windows app — don't wait for the word "audit." This skill can also
  drive a remediation loop: after auditing, apply the fixes, re-audit, and repeat until no Critical/High
  findings remain — trigger this whenever the user says "fix the findings," "apply the fixes," "take the
  necessary actions," or "loop until it's clean." Do NOT use for web apps, mobile-only apps, or pure
  backend services with no desktop component.
---

# Windows App Auditor

A structured audit pass for Windows desktop applications. The goal is not to admire the code — it is to
find what will bite the user in production: crashes, leaks, attack surface, and friction. Be specific,
cite evidence, and prioritize ruthlessly.

## Core principle: verify, don't vibe

Every finding falls into one of two buckets, and you must be honest about which:

- **Verified** — you read the actual code, config, project file, or build setting and can cite
  `path:line`. State exactly what you saw.
- **Needs manual/runtime check** — correctness depends on running the app, measuring it, or human
  judgment (most UX quality, real memory behavior over hours, actual startup time). Flag it clearly as
  unverified and tell the user *how* to check it. Never present a guess as a confirmed fact.

A report that says "I couldn't verify X, here's how you test it" is worth more than one that pretends.

## Workflow

1. **Scope the target.** Identify the stack before judging anything — the right checks depend on it.
   Look for: `*.csproj` / `*.sln` (read `<TargetFramework>`, `<OutputType>`, UI framework references),
   `*.vcxproj` (native C++), `app.manifest`, `Package.appxmanifest` (MSIX/packaged), `*.vbproj`.
   Determine: UI framework (WPF / WinForms / WinUI 3 / MAUI / native Win32), .NET version, packaged vs
   unpackaged, and whether it requires elevation. Report what you found in one short paragraph.
   Also note: does this app **share data files, a database, or config with another process** (a sibling
   app, an older version still in use, a companion service)? If yes, the shared formats are a contract —
   see the coexistence section of `architecture.md`.

2. **Establish the design target before judging design.** Findings about look, tone, or friendliness
   are meaningless without knowing who the app is for. If the audience, desired look (platform-native
   vs custom brand), and tone of voice haven't been established in the conversation or project docs,
   ask the user up to three short questions first. "The design is wrong" is only a finding relative to
   a stated target; without one, record design observations as questions, not defects.

3. **Run the five category passes.** Read the matching reference file and work through it against the
   real source. Don't load a reference until you're auditing that category.
   - Architecture & back-end health → `references/architecture.md`
   - Security → `references/security.md`
   - Product, UX/UI & accessibility → `references/ux.md`
   - Code quality & maintainability → `references/code-quality.md`
   - Privacy & compliance → `references/privacy-compliance.md` — mandatory (not optional polish) for
     any app that records user behavior (trackers, monitors, diaries) or is headed for users beyond
     its author; for a pure solo dev-tool it may be run lightly, but say so.

4. **Collect findings with evidence.** For each issue: `path:line`, what's wrong, why it matters
   (consequence, not theory), severity, and a concrete fix. Skip anything you can't ground in the code.

5. **Prioritize and write the report** using the exact template below.

If the user only asks about one dimension (e.g. "just the security side"), run only that pass — but say
which passes you skipped so the absence of findings isn't mistaken for a clean bill of health.

## Severity model

Assign one level per finding. Be calibrated — if everything is "Critical," nothing is.

- **Critical** — exploitable security hole, data loss, or guaranteed crash on a common path. Fix before
  anyone runs this. (e.g. unsigned auto-update executing downloaded code, plaintext credentials on disk,
  unhandled exception on the main happy path.)
- **High** — serious risk that will hit real users: a memory/handle leak that grows unbounded, a DLL
  hijack vector, missing global exception handler, blocking I/O on the UI thread.
- **Medium** — real defect with a workaround or limited blast radius: missing `IDisposable`, weak input
  validation on a low-traffic path, no logging around a failure mode, inconsistent terminology.
- **Low** — polish and hygiene: magic strings, minor inconsistency, missing keyboard shortcut.
- **Info** — not a defect; an observation or hardening opportunity worth noting.

## Report structure

ALWAYS use this exact template:

```
# Windows App Audit — [app name]

## Scope & stack
[1 short paragraph: UI framework, .NET/native, packaged?, elevation?, what was and wasn't reviewed]

## Overall assessment
[2-3 sentences. Ship-ready or not, and the single most important thing to fix.]

## Findings by severity
| # | Severity | Category | Finding | Location |
|---|----------|----------|---------|----------|
[one row per finding, sorted Critical → Info]

## Detailed findings
### [#] [Severity] — [short title]   ·  `path:line`
**What:** [the concrete problem]
**Why it matters:** [user-facing or attacker-facing consequence]
**Fix:** [specific, actionable — code sketch if useful]

[repeat per finding]

## Needs manual / runtime verification
[Bulleted list of things you could NOT confirm statically, each with how to test it]

## Quick wins
[3-5 highest-value, lowest-effort fixes to do first]
```

## Runtime verification safety (Windows) — hard rules

When a check calls for running the app (smoke tests, screenshots, live behavior), these rules are
non-negotiable. They exist because violating them has real, user-visible consequences — a naive
screen capture once grabbed the user's private fullscreen game instead of the app, and a focus-forcing
call yanked them out of that game.

- **Never capture the screen region** (`CopyFromScreen`, `BitBlt` from the desktop DC, PrintScreen
  automation). It photographs whatever is visually on top — which may be the user's game, bank, or
  chat, not your app.
- **Never steal focus** (`SetForegroundWindow`, `Activate`, simulated Alt-Tab) to bring the app
  forward for a capture. The user may be mid-game or mid-call.
- **Do** capture via `PrintWindow` with `PW_RENDERFULLCONTENT` (flag 2) against the app's confirmed
  window handle — it renders the target's own surface, works while covered, steals nothing. Confirm
  the window title/process ID first so you're capturing the right window.
- **Prefer app-provided debug hooks** (e.g. an environment variable the app reads at startup to
  navigate to a given page) over UI automation clicks. If the app has none, adding a tiny read-only
  hook is usually safer than scripted clicking.
- A smoke run means: launch, confirm the window title and responsiveness, exercise the specific path
  under test, check the app's log file for new errors, then close by the app's own close path (kill
  only if closing isn't itself under test).

## Remediation mode (audit → fix → re-audit loop)

When the user asks to *fix* the findings — "apply the fixes," "take the necessary actions," "loop until
clean" — don't just patch and stop. Enter the iterative loop defined in `references/remediation-loop.md`
and follow it exactly. The essentials:

- **Triage first.** Every finding is AUTO-FIX (one correct change — just do it), NEEDS DECISION (changes
  behaviour/defaults/data location — ask once, then apply), or ACCEPTED/MANUAL (runtime-only or
  user-deferred — record, don't loop on). Never silently make a product decision.
- **Re-audit the whole app after each fix batch,** not only the touched files — fixes cause regressions.
- **The user's explicit instruction overrides loop mechanics.** "Apply all of them but don't re-audit"
  means exactly that: apply, verify by build + smoke run, and skip the re-audit — recording in the
  summary and the project's handoff notes that a re-audit is still owed. Don't relitigate the loop.
- **Stop honestly:** done = no Critical/High open, every Medium/Low fixed-or-deferred-by-choice, and the
  last re-audit introduced nothing new. "Zero findings" is rarely the literal target; say what remains.
- **Never game the metric** — weakening a check, deleting a test, or suppressing a warning to clear a
  finding is itself a Critical regression. Cap iterations (~5) and stop if a pass makes no new progress.

Read the reference for the full procedure, the anti-thrash guards, and worked examples before starting.

## Tone

Imperative and specific. "Wrap the `FileStream` in `app/Importer.cs:212` in a `using` — it leaks a
handle per import" beats "consider improving resource management." Explain consequences so the user can
make their own priority calls. Don't pad the report with generic best-practice lecturing that isn't tied
to something you actually found in their code.

## Reference files

- `references/architecture.md` — threading (incl. timer reentrancy), lifetime/disposal, error handling
  (incl. WinUI-specific traps), config, culture of persisted data, process coexistence & shared data
  contracts, deployment, startup performance, testability.
- `references/security.md` — least privilege, attack surface, input handling, DLL hijacking, signing,
  transport, secret handling (incl. git history), the update path, memory protections, dependencies.
- `references/ux.md` — audience & design intent, design language / overall look, application-logic
  walkthrough, locale & language coherence, information architecture, first-run, hierarchy, feedback,
  consistency, keyboard, DPI/scaling, dark mode, undo, error copy, accessibility.
- `references/code-quality.md` — God Objects, swallowed exceptions, function length, magic numbers,
  dead code, duplication, naming consistency (incl. shadowing), import hygiene, constants discipline,
  docs, and tooling.
- `references/privacy-compliance.md` — data inventory (window titles are content, not metadata),
  consent & first-run disclosure for monitoring features, retention/export/delete, telemetry
  redaction, third-party license hygiene, claims honesty. Engineering review, not legal advice.
- `references/remediation-loop.md` — the audit → fix → re-audit cycle: triage buckets, the loop, safe
  editing discipline, the exit condition, and anti-thrash guards. Read this when the user asks to fix
  findings, not just report.
SKILL_EOF

cat > "$TARGET/windows-app-auditor/references/architecture.md" << 'SKILL_EOF'
# Architecture & Back-End Health

What separates an app that survives 18 months of maintenance and days of uptime from one that rots.
Read the actual source; cite `path:line` for every finding.

## 1. Separation of concerns

- Is UI logic mixed into business logic? Look for data access, file I/O, or domain rules living inside
  code-behind (`*.xaml.cs`), form event handlers, or view classes. That coupling means nothing can be
  tested or reused.
- Is there a recognizable layering (MVVM / services / domain) or is everything reaching into everything?
- Are dependencies injected, or are concrete types `new`-ed up inline deep in the call tree? Inline
  `new` of an `HttpClient`, `DbContext`, or file system makes the code untestable and often leaks
  resources.

## 2. Threading — the UI thread is sacred

- **Blocking the UI thread:** synchronous I/O, `.Result`, `.Wait()`, `Task.GetAwaiter().GetResult()`,
  or long loops on the UI thread. These freeze the window. Flag every one.
- **`async void`:** legal only for event handlers. Anywhere else it swallows exceptions and can crash
  the process. Flag `async void` methods that aren't event handlers.
- **`async/await` discipline:** background work should be genuinely async, not `Task.Run` wrapped around
  blocking calls as a band-aid (that just burns a thread-pool thread to stay blocked).
- **`ConfigureAwait(false)`** matters in *library/non-UI* code that shouldn't capture the UI context;
  in UI event handlers you usually *want* the context back. Don't flag its absence blindly — judge by
  whether the method needs to touch UI afterward.
- **Shared mutable state across threads** without a lock / `Interlocked` / concurrent collection is a
  race. Look for fields mutated from both UI and background paths.
- **Timer reentrancy.** A periodic timer (`System.Threading.Timer`, `System.Timers.Timer`) fires again
  while the previous tick is still running — a slow poll (network hiccup, locked DB) piles up
  overlapping callbacks that corrupt shared state. The safe pattern is one-shot + re-arm: schedule with
  `Timeout.InfiniteTimeSpan`, re-`Change()` in a `finally`. Flag any periodic timer whose callback does
  I/O.
- **Stacked UI tick loops.** Self-rescheduling UI loops (Tkinter `root.after`, restarted
  `DispatcherTimer`) silently double up when the code path that starts them runs twice (a view rebuild,
  a settings save). Each loop must store its handle/id and cancel the old one before re-scheduling.

## 3. Resource lifetime & leaks

- **`IDisposable` not disposed:** `FileStream`, `HttpClient` (special case — usually long-lived/shared,
  not per-call), DB connections, `Bitmap`/GDI objects, timers, registry keys. A `using` or explicit
  `Dispose` should exist. GDI/handle leaks are the classic "works fine, then the app dies after a day."
- **Event handler leaks — the #1 silent .NET desktop leak.** A long-lived object subscribing to a
  short-lived object's event (or vice versa) with no unsubscribe pins it forever. Look for `+=` on
  events with no matching `-=` in teardown/`Dispose`/`Unloaded`. Static events are the worst offenders.
- **Timers** (`DispatcherTimer`, `System.Timers.Timer`) that are started but never stopped/disposed.
- For a long-running app, ask: does anything accumulate without bound (caches with no eviction, lists
  that only ever grow, undo stacks with no cap)?

## 4. Error handling & logging

- **Global safety net:** are `AppDomain.CurrentDomain.UnhandledException`,
  `TaskScheduler.UnobservedTaskException`, and the dispatcher/thread exception event (WPF
  `DispatcherUnhandledException`, WinForms `Application.ThreadException`) wired up? Without them, one
  stray exception = silent process death with no diagnostics.
- **Swallowed exceptions:** empty `catch {}` or `catch (Exception) { }` that hides failures. Flag them.
- **WinUI 3 specifics:** `Application.UnhandledException` must set `e.Handled = true` after logging or
  the process still dies; Tkinter needs `root.report_callback_exception` (its callback exceptions bypass
  `sys.excepthook` entirely). On **unpackaged** WinUI apps, `AppNotificationManager.Default.Register()`
  can throw at startup — it must be guarded or the app crashes before the window exists. Only one
  `ContentDialog` may be open per XamlRoot: a second concurrent `ShowAsync` throws. If timers/reminders
  and user actions can each open dialogs, they need a gate (e.g. a `SemaphoreSlim(1,1)` wrapper around
  every `ShowAsync` call site).
- **Single-instance guard:** if two copies of the app running would double-write (two trackers logging
  the same activity, two schedulers firing the same reminder), a named `Mutex` check at startup is
  required, not optional.
- **Structured logging** (Serilog / NLog / `ILogger`) vs `Console.WriteLine`/`Debug.WriteLine`/nothing.
  Do logs carry context (operation, IDs) or just "error occurred"? Are exceptions logged with stack
  traces, or only `.Message`?
- **Log file encoding.** On Windows, a log file opened without an explicit encoding gets the ANSI
  codepage and mangles every non-ASCII character (em-dashes, localized window titles, user text) into
  `?`/`�` — exactly the evidence you need, destroyed. Python: `logging.basicConfig(...,
  encoding="utf-8")`; any direct `open()` for logs likewise. Flag log writers with no explicit UTF-8.
- **Recoverability:** does a failed operation leave the app usable, or wedged?

## 5. Configuration & environment

- **Hardcoded paths** (`C:\Users\...`, dev machine names), connection strings, URLs, or secrets in
  source. Grep for `C:\\`, `localhost`, `http://`, `Server=`, `password=`.
- **Writing user data to the wrong place:** the app must write to `%APPDATA%` /
  `Environment.SpecialFolder.ApplicationData` (or `LocalApplicationData`), never to `Program Files`,
  which is read-only for non-elevated users and will throw in production.
- **Layered config** (defaults → user overrides) vs values baked into code.
- Does the app behave correctly per-user, and not assume it's always elevated?
- **Culture of persisted strings.** Any date/number formatted into a database, JSON file, or file name
  must use `CultureInfo.InvariantCulture` (Python: explicit `strftime` format, no locale-dependent
  names). A current-culture `ToString("yyyy-MM-dd")` looks fine on the dev machine and breaks on a
  machine with a different locale — and if another process parses those strings, the data is silently
  corrupt. Grep every DB parameter and serialized field that carries a formatted date.

## 6. Deployment & update hygiene

- **Install/uninstall cleanliness:** does uninstall leave orphaned files, `%APPDATA%` junk, or registry
  keys? (Installer scripts / WiX / MSIX manifest.)
- **Atomic updates:** an interrupted update must not leave a half-written, unlaunchable app. Look at how
  the updater swaps binaries.
- (Security-critical aspects of the updater — signature verification, transport — live in
  `security.md`. Cross-reference; the update path is both a reliability and a security surface.)

## 7. Startup performance

- Expensive work (DB open, file scans, network calls, heavy deserialization) on the startup path that
  isn't deferred or made async. The window should appear fast and fill in.
- Synchronous splash logic that blocks.
- Note: actual startup time and steady-state memory are **runtime** measurements — flag them for manual
  verification rather than asserting numbers you can't see.

## 8. Data access (if present)

- Queries that load whole tables into memory instead of paging/streaming.
- Missing indexes implied by query patterns; N+1 query loops.
- Large file operations buffered fully into RAM instead of streamed.

## 9. Testability

- Is there a test project at all? Can the core logic be exercised without standing up a UI?
- Is logic structured (injected dependencies, pure functions) so it *could* be tested, even if tests
  don't exist yet? "Untestable by construction" is itself a finding.

## 10. Process coexistence & shared data contracts

When two processes touch the same files — a rewrite running alongside the original app, a companion
service, an older installed version — the shared formats are a **contract**, and breaking it corrupts
data with no crash to warn you.

- Identify everything shared: SQLite schema, JSON/config files, credential-store entries, named
  mutexes/pipes. The contract is **frozen**: changes must be additive (new tables, new files) — never
  altered columns, renamed keys, or repurposed fields the other process still reads.
- **Idempotency guards on ledger-style writes.** If both processes can perform the same daily/periodic
  write (end-of-day scoring, a sync push), each write needs a once-per-key guard (e.g. "one
  `daily_score` row per date") using the *same* guard logic in both, or whichever runs second
  double-books.
- **Double-actor detection.** If both processes can do the same background job (activity tracking,
  reminders), one must detect the other and stand down — check per-cycle, not just at startup, because
  the other app can launch later.
- SQLite across processes: short-lived connections or WAL, and every write wrapped against
  "database is locked" — the other app *will* hold the file sometimes. A swallowed lock error on a
  user-initiated write (a completion toggle) is silent data loss; it must be logged and surfaced.
- Deliberate behavior divergences between the two apps (different scoring rules, different caps) are
  findings to *document*, not necessarily fix — but the user must know which app must not run which
  job long-term.
SKILL_EOF

cat > "$TARGET/windows-app-auditor/references/security.md" << 'SKILL_EOF'
# Security

Audit as if you're going to attack this app, because someone will. Read code and config; cite
`path:line`. Where a check needs the built binary (compiler flags, signature), say so and tell the user
how to run it.

## 1. Least privilege

- Does `app.manifest` request `requireAdministrator`/`highestAvailable` when it doesn't truly need it?
  Default should be `asInvoker`. Running elevated "to be safe" turns every bug into a system-level bug.
- If elevation is genuinely required for one task, is *only that component* elevated (a separate helper
  process), or is the whole app running as admin?
- Does the app run as a service as SYSTEM when a lower-privileged account would do?

## 2. Attack surface

- Enumerate every external entry point: listening sockets/ports, named pipes, COM/OLE objects exposed,
  WCF/gRPC endpoints, custom URI scheme handlers, file-association handlers, IPC channels.
- Are debug endpoints, test backdoors, verbose diagnostic interfaces, or telemetry hooks **stripped
  from release builds** (`#if DEBUG`) or merely hidden? Hidden ≠ removed.
- Unused features still ship attack surface. Note anything exposed that the app doesn't need.

## 3. Input is hostile

Treat everything crossing a trust boundary — files, network responses, clipboard, IPC, registry,
command-line args, env vars, UI fields — as attacker-controlled until validated.

- **Path traversal:** user/file-derived paths concatenated without canonicalizing and checking against
  a base directory (`..\..\`). Look for `Path.Combine` with untrusted input then a write/read.
- **Insecure deserialization:** `BinaryFormatter` (deprecated and dangerous — flag on sight),
  `NetDataContractSerializer`, `SoapFormatter`, or `TypeNameHandling.All`/`Auto` in Json.NET over
  untrusted data. These are remote-code-execution vectors.
- **Injection:** string-concatenated SQL, shell commands built from input (`Process.Start` with
  user-controlled args), XML parsed with external entity resolution (XXE) left on.
- Argument/quantity bounds, format validation, and size limits on parsed input.

## 4. DLL hijacking — underrated, very common on Windows

- Does the app rely on the default DLL search order such that a malicious DLL dropped next to the exe
  (or in the working directory) gets loaded? Mitigations to look for:
  `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32...)`, `SetDllDirectory("")`, absolute paths
  for `LoadLibrary`, and an app manifest declaring dependencies.
- Are third-party native DLLs signature-checked before/at load, or loaded blindly?

## 5. Code signing & self-integrity

- Is the shipped binary Authenticode-signed? (Per current CA/B Forum rules the signing key must be
  hardware-protected — token/HSM — or use a managed service like Azure Trusted Signing. You don't need
  to mandate an *EV* cert; you need a valid, hardware-backed signature and a clean timestamp.)
- More important than having a signature: does the app **verify the integrity of the components it
  loads/executes** — plugins, helper exes, and especially anything the updater downloads? An app that
  signs its main exe but runs unsigned downloaded code has the security of the weakest link.

## 6. Transport security

- HTTPS everywhere external; no `http://` for anything carrying data or code.
- TLS 1.2 minimum (1.3 preferred). Flag forced-down `SslProtocols` or `Tls`/`Ssl3` references.
- **Certificate validation never disabled.** Search for
  `ServerCertificateCustomValidationCallback => true`,
  `ServicePointManager.ServerCertificateValidationCallback = (...) => true`, or
  `DangerousAcceptAnyServerCertificateValidator`. Any of these is Critical.
- Certificate **pinning** is situational: it raises the bar against MITM but is an operational footgun
  (a rotated server cert bricks every client). Note its presence/absence as a tradeoff, not a mandate.
- No home-rolled crypto. Use OS/CNG/BCrypt or a vetted library. Flag custom "encryption," ECB mode,
  hardcoded keys/IVs, and `Random` (not `RandomNumberGenerator`) used for anything security-relevant.

## 7. Secret & credential handling

- **No secrets in source or plaintext on disk.** Grep for `password`, `apikey`, `secret`, `token`,
  `connectionString` with literals, and check config files.
- Secrets should live in **Windows Credential Manager** or be protected with **DPAPI**
  (`ProtectedData.Protect`, scoped to `CurrentUser`).
- **Check git history, not just the working tree.** A secret deleted from HEAD still lives in every
  old commit (`git log -p -S "client_secret"`, or scan with a tool like gitleaks). If found: the
  credential is burned — rotate it at the issuer regardless of what else you do; history rewrite
  (`filter-repo`/`filter-branch` + purge reflog + `gc --prune=now`) only helps if no remote/clone
  already has it. "It was only committed briefly" is not mitigation.
- **Minimize secret lifetime in memory.** Note: you cannot reliably wipe a managed `string` (it's
  immutable, may be interned/copied by the GC), and `SecureString` is now discouraged by Microsoft and
  offers little real protection on modern .NET. The right pattern is: fetch the secret on demand, keep
  it in a `byte[]`/`Span<byte>` you can `Array.Clear` when done, and don't hold it longer than needed —
  *not* a false sense of safety from `SecureString`. Flag long-lived plaintext secrets held in fields.

## 8. The update mechanism (highest-trust component)

The updater runs with trust and pulls external code — treat it as the crown jewel.

- Downloads over TLS with cert validation intact.
- **Cryptographic signature of the update verified before execution**, with the trust chain validated
  end to end. No "download then run."
- Resistant to **downgrade attacks** (can't be tricked into installing an older, vulnerable version).
- Failure leaves the app in a safe, working state (ties back to atomicity in `architecture.md`).

## 9. Memory protections (binary-level)

These are build/link settings, verified on the compiled binary — flag for the user to confirm with a
tool like `dumpbin /headers`, BinSkim, or `Get-PESecurity`:

- ASLR (`/DYNAMICBASE`, high-entropy), DEP/NX (`/NXCOMPAT`), Control Flow Guard (`/guard:cf`), stack
  canaries (`/GS`), and — on supported toolchains/CPUs — CET shadow stack (`/CETCOMPAT`).
- For native C++ especially, these are baseline. For pure managed .NET many are implicit, but
  mixed-mode and native helpers must be checked.

## 10. Dependencies & supply chain

- Every NuGet/vendored package is potential vulnerability surface. Is there a way to know what's
  shipped (lockfile, `packages.lock.json`, an SBOM)?
- Are versions current, or pinned to something with known CVEs? Flag obviously stale, high-risk
  packages (old Newtonsoft with `TypeNameHandling`, abandoned crypto libs, etc.).
- Any unsigned or unofficial-source binaries vendored into the repo.

## 11. Audit logging & tamper evidence

- Are security-relevant events (auth attempts, privilege changes, config edits, sensitive file access)
  logged?
- Are those logs written somewhere the app's own (potentially compromised) process can't trivially
  erase, so an attacker can't cleanly wipe their tracks?
SKILL_EOF

cat > "$TARGET/windows-app-auditor/references/ux.md" << 'SKILL_EOF'
# Product, UX/UI & Accessibility

Most UX quality is judged by *using* the app, not reading its source. So split findings honestly:

- **Statically checkable** (in XAML/markup/resources/code): missing keyboard support, no `AutomationProperties`,
  hardcoded pixel sizes, color-only state, missing focus visuals, no high-DPI manifest, empty-state
  handling, error-message text, locale-sensitive formatting, scattered hardcoded colors.
- **Needs the running app / human judgment:** does the hierarchy actually guide the eye, does onboarding
  feel right, is the terminology coherent across screens, do animations feel smooth. Flag these as
  manual checks with *how to evaluate*, don't assert them from code.

This pass covers the product, not just the pixels. An app can be keyboard-perfect and DPI-crisp and
still fail its user because the workflows don't match how they think, the tone is wrong, or the visual
identity is incoherent. Sections 0–3 exist because a real audit once shipped without them and the user
rightly asked "where is the analysis of who this is for and how it looks?"

## 0. Audience & design intent — establish the target first

You cannot grade design against an unstated target. Before judging anything in sections 1–3:

- **Who is the user?** Solo owner-operator, a team, eventual external users? Their expertise level?
  What emotional relationship should the app create (a coach? a dashboard? a companion)?
- **What's the intended look?** Platform-native (Fluent/WinUI: Mica, NavigationView, Segoe, rounded
  corners) or a deliberate custom brand? Both are valid; drifting between them is not.
- **What's the intended tone of copy?** Strict, neutral, encouraging? (A productivity app that scolds,
  or celebrates nothing-days, is a tone bug — see §2.)

If these aren't stated in the conversation, project docs, or memory: **ask the user 2–3 short
questions** and record the answers. If the user is unavailable, state your assumed target explicitly at
the top of this pass and mark every design verdict as conditional on it.

## 1. Design language & overall look

- **Coherence is the test**: one type scale, one spacing rhythm, one corner radius family, one accent
  color used for one meaning. Mixed generations of styling (half native controls, half hand-drawn
  panels; three shades of "primary" blue) read as unfinished even to users who can't say why.
- **Native or custom — committed either way.** If native: does it actually use the platform's design
  system (theme resources/brushes, standard controls, system fonts) or fake it with hardcoded hexes?
  If custom: is the brand applied everywhere, including dialogs and error states?
- Statically greppable: scattered hardcoded color literals (`#2b2d31`, `Color.FromArgb(...)`) instead
  of a central theme dictionary/resource — count them; per-state colors (hover, disabled, focus) missing
  from the theme so widgets fall back to defaults that match neither theme; ad-hoc font sizes off the
  scale.
- The *feel* — whether it looks dated, cluttered, or handsome — is manual. Say what to look at
  (spacing consistency, alignment, visual noise) rather than asserting taste from code.

## 2. Application logic & workflow sanity

Walk the core user journeys end-to-end *as the user*, not as the developer: first run → daily core
loop → error/recovery → done-for-the-day. For each journey ask:

- Any **dead ends** (states with no visible next action), unreachable features (built but no entry
  point), or loops the user can't exit?
- Does the app's **model match the user's mental model**? (Navigation named by user goals, not by
  internal module names.)
- **Copy honesty**: the UI must never congratulate for nothing ("0 tasks today — great job!" on an
  empty day), scold incorrectly, or show numbers that lie (a "final score" displayed while the day can
  still change). Grep celebratory/judgmental strings and check the conditions that gate them.
- **Guardrails on state transitions**: can the user close/finalize something before it's meaningful
  (end-of-day review before the day ends)? Can they trigger the same flow twice concurrently?
- Do features interact sensibly (a "replan" that charges points must say what it costs *before* the
  click)?

These are findings with real severity — a workflow that silently produces a wrong score is a defect,
not a style note.

## 3. Locale & language coherence (statically checkable)

The app's language and the OS locale are different things, and mixing them is a visible bug: an
English-language UI on a Russian-locale Windows will render Cyrillic day/month names anywhere a date is
formatted with the current culture.

- Grep for culture-sensitive formatting into the UI: `ToString("ddd`, `ToString("dddd`, `ToString("MMM`,
  `.ToShortDateString()`, Python `strftime('%A'/%B')` after `locale.setlocale`. Every user-visible
  date/number needs a *deliberate* culture: the app's language culture, or invariant.
- Persisted strings (DB keys, JSON, file names) must be **invariant** — that's an architecture/data
  finding too (see `architecture.md`), but the display side belongs here.
- If the app claims localization, check it's real (resource files) and complete, not half-translated.

## 4. Information architecture

- Is navigation organized around user goals or around internal modules/technical structure? (Often
  visible in how views/menus are named and grouped.) Manual: would a first-timer find the core flow
  without a manual?

## 5. First-run & empty states

- Does the app open to a blank/overwhelming screen for a brand-new user? Look for unhandled empty
  collections in views — is there an empty-state template, or just a bare empty list?
- A good empty state says *why it's empty* and *what to do next*, not "No items found."

## 6. Visual hierarchy & layout (mostly manual)

- One dominant primary action per screen; secondary actions quieter; destructive actions
  (delete/overwrite/format) visually distinct and requiring deliberate intent.
- Consistent grid/alignment and breathing room vs crammed controls. Statically you can spot fixed tiny
  margins and absolute positioning that won't reflow; the *feel* is manual.

## 7. Interaction feedback

- Every action has a visible reaction. Long operations show progress that reflects reality, not a
  forever-spinner. Look for awaited long operations with no busy/progress indicator bound in the view.
- The UI never lies about success — confirmation reflects what actually happened.
- Dialogs don't pile up: timers, reminders, and user actions that can each open a dialog need a gate so
  two never fight (in WinUI 3 a second concurrent `ContentDialog.ShowAsync` *crashes* — that half of
  the issue lives in `architecture.md`).

## 8. Consistency (partly manual)

- Same action looks the same and lives in the same place everywhere.
- **Terminology drift** is statically greppable: is the same concept called "Project" in one view and
  "Workspace" in another? Search resource/string files for synonyms of the same domain noun.
- One icon language, one confirm/cancel/undo pattern throughout.

## 9. Keyboard & power-user support (statically checkable)

- Meaningful actions have accelerators/shortcuts (`InputBindings`, `KeyBinding`, access keys `_File`).
- Logical tab order (`TabIndex`); no keyboard traps.
- **Visible focus states** — not a faint dotted outline that vanishes on a light background. Check that
  focus visuals aren't disabled (`FocusVisualStyle="{x:Null}"` is a red flag).

## 10. DPI / scaling (statically checkable)

- Is per-monitor high-DPI declared? Look in `app.manifest` for
  `<dpiAware>` / `<dpiAwareness>PerMonitorV2</dpiAwareness>`. Missing = blurry on scaled/4K displays.
- Hardcoded pixel sizes and fixed `Width`/`Height` that won't reflow on resize or across DPI; text
  containers that will clip when the OS font size is larger.

## 11. Dark mode (statically checkable, quality is manual)

- If dark mode is claimed: is it a real theme (separate brushes/resources for elevation, borders, icon
  treatments, image overlays) or a naive color inversion? Look for whether shadows/elevations and icon
  assets are reconsidered, not just background flipped.
- Does it follow the OS theme setting, or force one mode?

## 12. Undo & reversibility

- Are destructive/significant actions reversible? Is there an undo/redo stack, or does the user have to
  fear every click? Fear creates hesitation; hesitation is friction.
- Where true undo isn't feasible, is there at least a clear confirm with a real description of
  consequences (not a generic "Are you sure?").

## 13. Error copy (statically checkable)

- Error messages written for the human at the keyboard, not the developer reading logs. Grep displayed
  strings for raw codes (`0x8007...`, exception `.Message` shown verbatim, "An error occurred").
- Good error = what happened + why it matters + what to do next.
  Example: not "Error code 0x80070005" but "You don't have permission to save here — try your Documents
  folder instead."

## 14. Accessibility (statically checkable, treat as first-class)

- Screen-reader support: are interactive elements given accessible names
  (`AutomationProperties.Name`/`LabeledBy`), or are icon-only buttons nameless to a screen reader?
  A list of 75 task checkboxes with no names announces as 75 bare "checkbox"es.
- **Color is never the only signal** of meaning/state — error/success/active must also carry an icon,
  label, or shape, since a meaningful share of users have color-vision deficiency.
- Sufficient contrast (manual to measure, but flag obviously low-contrast hardcoded color pairs).
- Full keyboard operability (overlaps §9) — anything reachable only by mouse fails accessibility.
SKILL_EOF

cat > "$TARGET/windows-app-auditor/references/code-quality.md" << 'SKILL_EOF'
# Code Quality & Maintainability

The auditor's other three passes cover whether the app works correctly today. This pass covers whether
it can be safely changed tomorrow. A codebase that cannot be reasoned about, navigated, or tested is
a liability regardless of how well it currently runs. Read the actual source; cite path:line.

## 1. God Objects and file size

A single class or file that owns unrelated concerns — UI, business logic, database, network, OS
integration — is the most common structural failure in desktop apps. It means:
- A bug fix in one area can silently break another
- Nothing can be tested without wiring up everything
- No one can hold the whole thing in their head

Check: is there a file or class over ~500 lines that handles more than one distinct concern?
Flag the concerns it mixes and name them explicitly. "main.py is 2850 lines doing UI, OAuth, database,
system tray, and reporting" is a finding. Severity: Medium if one extra concern, High if three or more.

The fix is not "split into smaller files arbitrarily." The fix is to identify the natural seams:
one module per concern (ActivityTracker, ReportGenerator, TickTickClient, SettingsStore, etc.)
and move logic to where it belongs.

## 2. Bare except / swallowed exceptions

Count every `except Exception: pass`, `except: pass`, or `except Exception as e: print(e)` (or
equivalent in other languages). These are silent failures — each one is a place where something went
wrong and the user and developer both get no information about it.

Report: total count, and call out the three highest-risk locations specifically (those guarding
network calls, database writes, or background thread loops — the places where silent failure causes
data loss or invisible corruption).

Severity: one or two bare excepts on truly unimportant paths = Low. Five or more, or any on a
critical path (tracker loop, sync callback, file write) = High.

## 3. Function length and cyclomatic complexity

Functions over ~60 lines almost always do more than one thing, making them hard to read, test, and
modify safely. Flag functions over 80 lines by name and line range. Note what multiple things they
are doing.

Complexity signals to look for: deeply nested conditionals (4+ levels), long if/elif chains that
could be dispatch tables, loops containing nested loops with embedded business logic.

These are Medium findings if widespread (more than 10% of non-trivial functions), Low if isolated.

## 4. Magic numbers and hardcoded values

Hardcoded literals scattered through logic — polling intervals, timeout values, UI dimensions,
threshold scores, retry counts, port numbers — make the code brittle and hard to tune. The risk is
not just aesthetics: when the same constant appears in three places and needs to change, one is always
missed.

Grep for: numeric literals in logic (not in UI layout), hardcoded port numbers, hardcoded file names,
hardcoded URL paths, hardcoded time intervals. Flag specific examples with path:line.

Severity: Low for isolated cases, Medium if the same constant appears in multiple places with no
named source of truth.

## 5. Dead and commented-out code

Commented-out code blocks (more than 3 lines) are a maintenance hazard: they suggest something was
tried and abandoned, they drift out of date, and they create noise that makes the real code harder to
read. Flag their existence and location. The fix is deletion — version control preserves history.

Also check: unreachable code paths (return followed by logic, conditions that can never be true given
surrounding context), unused imports, unused variables that were clearly once part of something larger.

Severity: Info unless the dead code is a security concern (e.g. a disabled auth check).

## 6. Naming consistency

Inconsistent naming across a codebase is a subtle but real bug risk — it forces every reader to
maintain a mental translation table.

Check for:
- Mixed naming conventions (snake_case functions next to camelCase functions in the same language)
- The same concept named differently in different places ("task" vs "item" vs "entry" vs "activity"
  for what appears to be the same domain object)
- Abbreviations used inconsistently (sometimes `mgr`, sometimes `manager`, sometimes `handler`)
- Boolean variable names that don't read as booleans (`active` instead of `is_active`, `done`
  instead of `is_complete`)
- **Shadowing collisions**: a member whose name collides with a type in scope (a private method named
  `Log` next to a `Log` class produces CS0119-style ambiguity; a local variable shadowing a module).
  These bite hardest when a *new* type is introduced into an old codebase — flag existing names that
  are one refactor away from a collision (`Log`, `Timer`, `Path`, `Task`, `Settings`)

Flag specific examples. Severity: Low for isolated cases, Medium if the same domain concept has
three or more names across the codebase (it means nobody can search for all uses of a thing).

## 7. Dependency and import hygiene

- Are all imports at the top of the file, or scattered throughout functions (usually a sign that
  something was added as an afterthought and the dependency isn't properly owned)?
- Are there circular imports between modules?
- Are heavy dependencies imported unconditionally at startup when they're only needed on one code path
  (slowing launch time)?
- In Python specifically: are there `import *` statements? These pollute the namespace and make it
  impossible to know where a name comes from.

Severity: Low for scattered imports, Medium for circular dependencies or unconditional heavy imports
on the startup path.

## 8. Configuration and constants discipline

- Are configuration values (timeouts, thresholds, intervals, feature flags) defined in one place
  (a constants file, a config class, a settings object) or duplicated across files?
- Is it possible to change a tunable value — say, the polling interval — by editing one line, or does
  it require a grep across the whole codebase?
- Are default values documented with a comment explaining why that value was chosen?

Severity: Low if scattered but consistent, Medium if the same tunable appears with different values
in different parts of the code (they've already diverged).

## 9. Code-to-comment ratio and documentation quality

This is not about quantity of comments — over-commenting obvious code is noise. It's about whether
the *non-obvious* parts are explained.

Flag: complex algorithms with no explanation of the approach, business rules encoded as raw
conditionals with no comment explaining the rule, and workarounds for known bugs or API quirks that
aren't annotated (these will be "fixed" by the next developer and break things).

Also flag: docstrings that are wrong (describe a different signature or behaviour than the actual
function). A wrong comment is worse than no comment.

Severity: Info unless a critical algorithm or security-relevant decision has no explanation, in which
case Low.

## 10. Duplication (DRY)

Copy-pasted logic is a latent bug: when the behaviour needs to change, one copy gets fixed and the
others quietly diverge. This is how an app ends up validating input three different ways, or formatting
the same date correctly in one report and wrongly in another.

Check for: near-identical blocks repeated across methods/files, the same parsing or formatting logic
re-implemented in multiple places, and copy-pasted error-handling boilerplate that should be a helper.
A reliable copy-paste tell: **names that don't match what they bind** — a SQL parameter `$cat` bound to
a `done_at` timestamp, a variable `userName` holding an email. Each one means a block was cloned and
half-edited; check the clone's siblings for the un-edited half.
Flag the duplicated locations together (`path:line` for each copy) so the user can see the divergence
risk. The fix is extraction into one shared function — but only when the copies are *truly* the same
concern, not coincidentally similar.

Severity: Low for incidental duplication, Medium when the duplicated logic is non-trivial (validation,
money/time math, security checks) where divergence would cause real bugs.

## 11. Tooling — prefer a real analyzer over eyeballing

For most of the checks above, a linter or analyzer is faster, more complete, and more reproducible than
reading by hand — and you can cite its output as evidence. Run one where the toolchain allows:

- **Python:** `ruff` (lint + unused imports/vars), `vulture` (dead code), `radon cc` / `radon mi`
  (complexity and maintainability index), `bandit` (overlaps the security pass).
- **.NET / C#:** the built-in Roslyn analyzers, `dotnet format --verify-no-changes`, SonarLint /
  SonarAnalyzer, and `dotnet build /warnaserror` to surface what's being ignored.

Static reading still matters for judgment calls (God Objects, naming intent, whether a comment lies),
but don't hand-count what a tool can count for you. Note in the report which tool produced which finding.

## 12. Overall refactoring priority

After running the above checks, close this pass with one paragraph: the single highest-leverage
structural change that would most improve the long-term health of this codebase. This is usually
one of: break up the God Object, add a logging infrastructure, or add a test harness for the core
logic. Be specific — name the file, the class, and the first module to extract.
SKILL_EOF

cat > "$TARGET/windows-app-auditor/references/privacy-compliance.md" << 'SKILL_EOF'
# Privacy & Compliance

This pass exists because an app can be secure, stable, and beautiful — and still be a liability the
day someone other than its author runs it. It matters *most* for monitoring-class apps (activity
trackers, time diaries, screenshot tools): their core feature is recording personal behavior.

Scope honesty: this is an engineering compliance review — data inventory, consent mechanics, license
hygiene. It is **not legal advice**; before a real commercial launch in the EU (GDPR) or similar
jurisdictions, tell the user plainly to have a professional review the final posture.

## 1. Data inventory — know what you collect before judging anything

Build the actual list from code (grep every DB INSERT, file write, and network send):

- What personal data exists? For a tracker: window titles (which contain chat names, document
  titles, medical/financial page titles), app usage timelines, idle-time answers typed by the user,
  task texts, scores. Window titles are the sleeper — they look like metadata and are content.
- Where does each item go — local DB, log file, export, network? A "local-only" app with an HTML
  export or a sync feature is no longer local-only for that data.
- Sensitivity-rank the list. Anything revealing health, finances, beliefs, or relationships is
  special-category-adjacent: minimize, don't just protect.

Findings format: "X is collected at `path:line`, stored in Y, retained forever, user cannot see/
delete it" — each clause that's wrong is a finding.

## 2. Consent & transparency (for anything that monitors)

- **First-run disclosure**: before the first byte of tracking, the app says what it records, where
  it's stored, and how to pause it. A tracker that starts recording silently on install fails this
  pass — even for a solo-use app, because "others eventually" is the stated trajectory.
- **Pause/stop control** reachable in ≤2 clicks, and it visibly works (pill/status change).
- **Multi-user machines**: does the tracker record other Windows accounts' sessions, or the lock
  screen? It must not record when the session is locked or when another user is active.
- If the audience ever includes employees (a team lead installs it for a team), the game changes
  entirely — workplace-monitoring law. Flag that boundary if the roadmap points there.

## 3. Retention & user rights (GDPR-shaped, good practice everywhere)

- **Retention limit**: does anything prune old rows, or does the activity log grow forever? Forever
  is both a privacy finding and (eventually) a performance one. A configurable "keep N days/months,
  then delete" with a sane default is the fix.
- **Export**: user can get their data out in a usable format (CSV/JSON). Check it exists and
  actually includes the sensitive tables, not just tasks.
- **Delete**: user can wipe history (all, or a date range) from inside the app — not by hunting for
  a .db file. Verify the wipe includes logs and exports, and consider `VACUUM` after (deleted
  SQLite rows linger in the file until vacuumed).
- **View**: the user can see what's been recorded about them (the diary/report views usually cover
  this — check the *raw* store doesn't contain fields no view ever shows).

## 4. Telemetry & crash reports

- Opt-in, not opt-out, for a personal-tool audience. Off by default on first run.
- **Redaction at source**: window titles, task texts, idle answers, and file paths containing the
  username must never leave the machine in a crash bundle. Check what the report actually contains,
  not what the intent was — build one and read it.
- The user sees the exact payload before it's sent.

## 5. Third-party licenses & attribution

- Inventory every runtime dependency (`pip freeze` / the lockfile / csproj PackageReferences) and its
  license. Watch for: GPL/AGPL in a distributed closed app (copyleft obligations), "non-commercial
  only" clauses, and abandoned packages with no license at all (undistributable in theory).
  PyInstaller itself is GPL *with a bootloader exception* — fine to ship frozen apps commercially;
  cite the exception, don't just vibe it.
- Ship a `THIRD-PARTY-NOTICES` file (or About-dialog section) with the attributions the licenses
  require (MIT/BSD/Apache all require notice preservation).
- Fonts, icons, sounds: assets have licenses too. An icon pack lifted from a search result is a
  finding.

## 6. Claims honesty

Whatever the README/UI *says* about privacy must match the code: "your data never leaves your
device" is falsified by one telemetry call or cloud sync. Grep the claims, then grep the sockets.
A false privacy claim is worse than no claim — severity High.

## Severity calibration for this pass

- **Critical**: sensitive data leaves the machine without consent; false "local-only" claim with
  actual network egress; monitoring continues while the session is locked/another user is active.
- **High**: no first-run disclosure for a monitoring feature; crash reports containing window
  titles/personal content; copyleft violation in a distributed binary.
- **Medium**: no retention limit; no in-app delete/export; missing attribution file.
- **Low**: attribution incomplete; retention exists but isn't configurable.
- **Info**: recommendations for a future multi-user/commercial posture; "get legal review before
  charging money in the EU."
SKILL_EOF

cat > "$TARGET/windows-app-auditor/references/remediation-loop.md" << 'SKILL_EOF'
# Remediation Loop (audit → fix → re-audit → repeat)

This drives findings down to the level that *should* be zero — safely, without thrashing and without
gaming the metric. It runs after a baseline audit exists. It needs to edit files and run builds/tests,
so it is a Claude Code workflow, not a chat one.

The naive version — "fix everything, re-check, repeat until no findings" — fails three ways: it
silently makes product decisions that aren't yours to make, it loops forever on things that can only be
verified by a human running the app, and it tempts you to weaken a check to make a finding "pass." The
procedure below exists to prevent all three.

## Step 0 — Safety net before touching anything

- Ensure the project is under version control with a clean working tree, or make a full copy. Every
  change in this loop must be reversible. If there's no VCS, say so and create a checkpoint copy first.
- Work on a dedicated branch (e.g. `audit-remediation`) so the user can review the whole diff at once.
- Treat the current full audit report as **iteration 0 / baseline**. Everything is measured against it.

## Step 1 — Triage every finding into one of three buckets

Do this *before* fixing anything. The bucket decides whether you may act autonomously.

- **AUTO-FIX** — one unambiguous correct change, no behaviour decision required.
  (Escape HTML output, add a `using`/`Dispose`, cache a recomputed value, parametrize a SQL predicate,
  add `--clean`, add a global exception handler + logging, move a secret to Credential Manager.)
  → Fix these directly.

- **NEEDS DECISION** — the fix changes behaviour, defaults, data location, or UX, so it's a product
  choice, not a bug with one answer.
  (Make autostart opt-in and default it off; move user data to `%APPDATA%`, which relocates existing
  users' data and needs a migration; let the idle dialog be dismissible; split a God Object, which is a
  large structural change.)
  → Do **not** silently decide. Present the choice and the tradeoff, get a one-line answer, then apply.
  Batch these so you ask once, not finding-by-finding.

- **ACCEPTED / MANUAL** — can't be verified statically (DPI sharpness, memory over a workday, the
  notification icon rendering), or the user has chosen to live with it (an Info/Low they accept).
  → These are **not failures to loop on.** Record them as accepted or needs-runtime and exclude them
  from the exit condition. Looping on something only a human can confirm is how you spin forever.

State the triage explicitly at the start of remediation so the user can correct a bucket before you act.

## Step 2 — The loop

Repeat:

1. **Pick the highest-severity open AUTO-FIX items** (plus any NEEDS-DECISION the user has now
   confirmed). Fix in small, related batches — not all at once. Critical/High before Medium/Low.
2. **Verify each fix the same way the audit does.** Re-read the changed code and confirm the issue is
   actually gone (`path:line`). If tests exist, run them. If the project builds, build it. A fix is not
   "done" because you wrote it — it's done when re-inspection confirms it and nothing broke.
3. **Re-run the FULL audit, all four passes — not just the files you touched.** Fixes cause regressions
   and expose new issues; a security fix can break a UX expectation; splitting a class can drop an event
   unsubscribe. Only a full re-audit catches that.
4. **Emit an iteration delta:**
   - **Resolved** — finding ID + the evidence it's gone.
   - **Still open** — with why (blocked on a decision? deferred?).
   - **Newly introduced** — anything the fixes created. These jump to the front of the next batch.
5. **Commit the batch** with a message tying it to the finding IDs (e.g. `audit: fix #1,#4,#7 — escape
   report HTML, cache icon, parametrize SQL`). One reviewable commit per batch.

### Editing discipline (each of these has caused a real incident)

- **No regex/sed bulk edits on structured source.** A slightly-off pattern once turned a clean method
  into a nested, duplicated object initializer that still *looked* plausible. Use exact-string edits or
  hand-edit; if a mechanical transform is unavoidable, re-read every touched site afterwards and build
  before moving on.
- **Grep for name collisions before introducing a new type.** Adding a `Log` class to a codebase with a
  private method named `Log` produced a shadowing compile error (CS0119) — and in dynamic languages the
  same mistake doesn't even fail loudly. Check `class X` / `def x` / member names for the identifier
  you're about to add.
- **Build after every batch, smoke-run after the last one.** A smoke run: launch the app, confirm the
  window title and responsiveness, exercise the specific behaviors you changed, check the app's log for
  new errors, close via the normal path. If you capture the screen for evidence, follow the runtime
  verification safety rules in SKILL.md — `PrintWindow` with `PW_RENDERFULLCONTENT` against the
  confirmed window handle only; never `CopyFromScreen`, never `SetForegroundWindow` (a naive capture
  once photographed the user's private fullscreen game, and a focus call yanked them out of it).

### User overrides beat loop mechanics

If the user explicitly narrows the loop — "apply all of them, but do not start the re-audit," "skip the
commit, I'll review first" — follow their instruction, don't relitigate it. Record what was skipped and
that it's still owed (in the final summary *and* the project's handoff notes), so the debt is visible
instead of silently dropped.

## Step 3 — Exit condition (define "done" honestly)

Stop when **all** of these hold:

- No **Critical** or **High** finding remains open.
- Every remaining **Medium/Low** is either fixed, or a NEEDS-DECISION the user has explicitly deferred,
  or an ACCEPTED/MANUAL item.
- The most recent re-audit introduced **no new Critical or High**.

"Zero findings" is usually *not* the literal target, and pretending it is leads to bad behaviour. Info
observations and accepted runtime items legitimately remain. End by stating plainly what's left and why
it's acceptable — not by hiding it to claim a clean sweep.

## Step 4 — Anti-thrash guards (non-negotiable)

- **Max iterations.** Cap at ~5. If Critical/High still remain, stop and report what's blocking rather
  than looping. Some problems need a human.
- **No-progress detector.** If an iteration resolves nothing new, stop — something needs input or a
  decision. Don't re-attempt the same fix hoping for a different result.
- **Oscillation guard.** If fixing a finding in one pass re-opens one in another (security vs UX, say),
  surface the tradeoff and let the user choose. Don't flip-flop between two "fixed" states.
- **Never game the metric.** Do not weaken, disable, or delete a check, a test, or an assertion to make
  a finding disappear. Do not suppress a warning to silence it. Do not narrow a finding's wording to
  dodge it. The goal is a safer app, not a cleaner-looking report. Silencing a true finding is itself a
  Critical-severity regression.
- **Scope discipline.** Fix what the findings name. Don't opportunistically rewrite unrelated code mid-
  loop — that inflates the diff and introduces risk the audit didn't ask for.

## Step 5 — Final summary

Close with a single before/after:

- Started: counts by severity (from baseline).
- Ended: counts by severity, what was fixed (with commit refs), what was deferred by decision, what
  remains as accepted/manual-verification.
- The one thing the user should still do by hand (run the app and check the items in
  "Needs manual / runtime verification").

## One worked micro-example

Baseline finding: *#4 Medium — unescaped window titles in HTML report (`report.py:212`).*
→ Triage: AUTO-FIX (one correct change).
→ Fix: wrap each insertion in `html.escape(...)`; re-read `report.py:212` to confirm; build.
→ Re-audit: #4 Resolved (evidence: all five insertions now escaped). No new findings in any pass.
→ Commit: `audit: fix #4 — escape window titles in report output`.

Contrast — *#3 High — autostart written on every launch.*
→ Triage: NEEDS DECISION (changing it to opt-in alters default behaviour).
→ Ask: "Default autostart to **off** with a Settings toggle? (recommended) — yes/no."
→ Only after "yes" do you implement, re-audit, and commit.
SKILL_EOF

cat > "$TARGET/windows-code-refiner/SKILL.md" << 'SKILL_EOF'
---
name: windows-code-refiner
description: >-
  Refactor, optimize, and document a Windows desktop app's source to idiomatic, best-in-class quality —
  cleaner structure, correct patterns, consistent formatting, and professional documentation — while
  preserving behaviour. Capable of large restructuring and full rewrites when justified, always verified
  against tests and delivered as reviewable commits. Use whenever the user wants to "clean up," "refactor,"
  "optimize," "beautify," "polish," "make it idiomatic," "bring it up to best practices," "rewrite it
  properly," or "comment/document the code" for a .NET (WPF / WinForms / WinUI / MAUI) or Python Windows
  desktop app. This is the ELEVATE step: it takes working code and makes it clean. It is NOT the auditor
  (which finds defects) and NOT the remediation loop (which fixes reported bugs) — if the user wants to
  find or fix bugs, use windows-app-auditor instead. Do NOT use for web or mobile-only apps.
---

# Windows Code Refiner

Take code that already works and make it *good*: idiomatic, well-structured, consistently formatted, and
documented to a professional standard — without changing what it does. Improvement that silently alters
behaviour is not improvement; it's a new bug wearing a nice sweater.

## Prime directive: behaviour preservation

Every change in this skill is behaviour-preserving by default. Observable behaviour — outputs, side
effects, error semantics, persisted formats, timing that users depend on — must be identical before and
after. If a genuine improvement *would* change behaviour (fixing a latent bug you notice, changing a
default, tightening validation), **stop and surface it as a separate, labelled decision.** Never fold a
behaviour change into a "cleanup" commit where a reviewer won't see it coming.

## Refactor by default, rewrite only when justified

Pick the lightest strategy that achieves the goal, per unit of code:

- **Leave as-is** — clear, correct, idiomatic already. The best refactor is often none.
- **In-place refactor** — rename, extract method/class, replace a pattern, tidy formatting. Default.
- **Extract & restructure** — break a God Object into modules along its natural seams, introduce DI,
  separate UI from logic. Larger, still incremental.
- **Full rewrite of a unit** — justified only when the code is genuinely unsalvageable (tangled beyond
  safe incremental change) *and* you can pin its behaviour with tests first. A rewrite without
  characterization tests is a gamble, not an upgrade. State the justification before doing it.

Prefer many small, reviewable diffs over one giant "rewrote everything" changeset nobody can review — an
unreviewable diff is a failure mode, not a flex.

## Before touching anything (safety net)

1. Confirm version control with a clean working tree, or make a full copy. **Never commit to `main`/
   `master` (or the current integration branch).** Create and check out a dedicated branch first —
   `code-refinement` for refactors, `rewrite/<unit>` for a rewrite — and commit only there. `main` stays
   untouched until a human has reviewed the diff, tests are green, and they merge it themselves. If the
   repo isn't under version control yet, stop and initialise it (or take a full copy) before any edit;
   without a branch to isolate on, the work doesn't start. Every change must be reversible.
2. Identify the build and test commands. Run them to establish a **green baseline**. You cannot claim
   behaviour is preserved if you never established what it was.
3. If the code you're about to restructure has **no tests**, write characterization tests first — they
   capture current behaviour (even quirks) so any accidental change shows up as a failing test. This is
   mandatory before any extract-and-restructure or rewrite.

## Workflow

1. **Format first.** Apply the formatter as its own isolated commit (`dotnet format`, `ruff format` /
   `black`). Mechanical, safe, and it gets whitespace noise out of the way so later diffs show real
   changes. Add/adopt an `.editorconfig` if none exists.
2. **Scope and strategize.** List the units (files/classes/functions) and assign each a strategy above.
   Justify anything beyond in-place.
3. **Refine in small behaviour-preserving batches**, using `references/refactoring-catalog.md`:
   structure (SRP, seams, DI), language idioms, error handling + logging, resource/lifetime correctness,
   async correctness, performance (measured, not guessed), naming.
4. **Document to standard** as you go, per `references/documentation-standard.md`.
5. **Verify after every batch:** format → build → run tests and linters/analyzers → all green. A batch
   isn't done until it's green. Then commit with a message describing the transformation (e.g.
   `refactor: extract TickTickClient from main — no behaviour change`).
6. **Report** at the end: structure before/after, what was refactored vs rewritten and why, every
   behaviour-affecting change flagged separately, and any deferred suggestions.

## Guardrails (non-negotiable)

- **Behaviour preservation is the contract.** If tests go red, either your change altered behaviour
  (revert or flag it) or the test asserted an implementation detail (fix it deliberately and say so).
  Never delete, skip, or weaken a test to make a refactor "pass."
- **Never commit to `main`/`master`.** All work — refactors and rewrites alike — lands on a dedicated
  branch and stays there. Merging into the integration branch is a human decision made after review, not
  something this skill does. Do not fast-forward, force-push, or rebase onto `main` on the user's behalf.
- **Measure before optimizing.** Don't trade clarity for a micro-optimization without evidence it
  matters. Readable code that's fast enough beats clever code that's marginally faster.
- **Don't over-engineer.** Not every class needs an interface; not every value needs a config entry.
  Introduce abstraction to remove real duplication or a real seam, not speculatively.
- **Respect the project's constraints.** Don't add a framework, dependency, or language-version bump the
  project can't take. Match the existing architecture unless restructuring it is the explicit goal.
- **Don't over-comment.** Follow the documentation standard — comments earn their place by explaining
  *why*, not narrating *what*.
- **No regex/sed bulk edits on structured source.** A slightly-off pattern silently corrupts code into
  something that still looks plausible (it has produced a nested, duplicated initializer in a real
  session). Use exact-string edits; after any mechanical transform, re-read every touched site and
  build before continuing.
- **Grep for name collisions before introducing a new type or renaming.** A new class whose name
  matches an existing member (`Log` class vs a private `Log` method) breaks compilation in C# and
  silently shadows in Python. Check the identifier across the codebase first.
- **Verify on the live app without disturbing the user.** If a refinement warrants a smoke run with
  screenshots: capture via `PrintWindow` with `PW_RENDERFULLCONTENT` (flag 2) against the app's
  confirmed window handle. Never `CopyFromScreen` (it photographs whatever is on top — possibly the
  user's game or private windows) and never `SetForegroundWindow` (it steals their focus).

## Reference files

- `references/refactoring-catalog.md` — concrete best-practice transformations, with C#/.NET and Python
  specifics: structure & SOLID, idioms, async, resource management, error handling, performance, naming.
- `references/documentation-standard.md` — what "perfectly commented" actually means: why-not-what,
  XML doc comments / docstrings, when to comment and when silence is better, and worked examples.
SKILL_EOF

cat > "$TARGET/windows-code-refiner/references/refactoring-catalog.md" << 'SKILL_EOF'
# Refactoring Catalog

Concrete, best-practice transformations. Apply the ones the code needs — this is a menu, not a mandate.
Every transformation is behaviour-preserving; if one wouldn't be, flag it as a decision instead.

## A. Structure & SOLID

- **Single Responsibility / break up God Objects.** A class or file owning UI + business logic + data +
  network + OS integration is the top structural smell. Extract along natural seams — one cohesive unit
  per concern (e.g. `ActivityTracker`, `ReportGenerator`, `TickTickClient`, `SettingsStore`). Move logic
  to where its data lives; don't just split by line count.
- **Separate UI from logic.** Business rules, I/O, and computation do not belong in code-behind
  (`*.xaml.cs`), form event handlers, or view classes. In WPF, push them into ViewModels/services
  (MVVM); in any stack, into plain testable classes the UI merely calls.
- **Depend on abstractions, inject dependencies.** Replace `new HttpClient()` / `new DbConnection()`
  buried in a method with a dependency passed into the constructor. This is what makes logic testable and
  lifetimes manageable — but only abstract where there's a real seam, not reflexively.
- **Replace conditionals with polymorphism / dispatch** when a long `if/elif` or `switch` on a type code
  keeps reappearing. A dictionary dispatch or strategy object often reads better and is easier to extend.
- **Reduce nesting** with guard clauses and early returns instead of deep `if` pyramids.

## B. C# / .NET idioms

- **Nullable reference types** enabled (`<Nullable>enable</Nullable>`) and honoured — annotate, don't
  suppress with `!` unless provably safe.
- **`using` declarations** (C# 8+) for disposables instead of manual try/finally where scope allows.
- **Pattern matching** and switch expressions over verbose type-checks and if-ladders.
- **Records** for immutable data carriers; **`readonly`** for fields that never change; expression-bodied
  members for one-liners.
- **LINQ** where it clarifies intent — but not when it hides an expensive multiple-enumeration or when a
  plain loop is clearer. Materialize (`.ToList()`) once when enumerated repeatedly.
- **`string` handling:** `StringBuilder` for hot concatenation loops; interpolation over `+`;
  `StringComparison` specified explicitly for correctness.
- **Modern DI/logging/config:** `Microsoft.Extensions.DependencyInjection`, `ILogger<T>`,
  `IOptions<T>`/`IConfiguration` rather than hand-rolled singletons and static config.

## C. Python idioms

- **PEP 8 + type hints** throughout; run `ruff`/`black` for formatting and `mypy`/`pyright` for types.
- **Context managers** (`with`) for every file/connection/lock — never a bare `open()` whose close
  depends on GC.
- **`pathlib.Path`** over `os.path` string-mashing; **f-strings** over `%`/`.format()`.
- **`dataclasses`** (or `attrs`/`pydantic`) for structured data instead of ad-hoc dicts/tuples.
- **`logging`** module over `print`; module-level `logger = logging.getLogger(__name__)`.
- **Comprehensions/generators** where they clarify — but a readable loop beats a dense nested
  comprehension. Prefer generators for large streams to avoid buffering everything in memory.
- **`enum.Enum`** for fixed sets of constants instead of bare string/int literals.
- Replace `import *` with explicit imports; hoist function-local imports to the top unless there's a real
  reason (optional dep, circular-import break).

## D. Async & threading correctness

- **Never block the UI thread.** Replace sync-over-async (`.Result`, `.Wait()`,
  `GetAwaiter().GetResult()`) with genuine `async`/`await`. In Python UI loops, move blocking work off the
  UI thread and marshal results back (e.g. Tkinter `root.after`).
- **`async void`** only for event handlers; everywhere else return `Task`.
- **`ConfigureAwait(false)`** in library/non-UI code that doesn't need to resume on the captured context.
- **CancellationToken** threaded through long-running async operations so they can be cancelled cleanly.
- **Give each thread its own DB connection** or serialize access with a lock; don't share a connection
  across threads and hope.
- **Make periodic timers non-reentrant.** Replace fixed-interval timers whose callback does I/O with
  the one-shot + re-arm pattern (`Timeout.InfiniteTimeSpan`, then `Change()` in a `finally`) so a slow
  tick can't overlap the next. For self-rescheduling UI loops (Tkinter `after`, restarted
  `DispatcherTimer`), store the handle/id and cancel the previous loop before starting a new one — a
  view rebuild that re-enters the start path otherwise stacks loops silently.

## E. Resource management & lifetime

- Ensure every `IDisposable`/native/GDI/handle/timer is disposed — `using`, `finally`, or an owning
  `Dispose`. In Python, `with` or explicit `close()` in a `finally`.
- **Unsubscribe events** you subscribe to (`-=` matching every `+=`) in teardown — the #1 silent .NET
  leak. Consider weak event patterns for long-lived publishers.
- Cache expensive-to-create objects that are recomputed on a hot path (e.g. an icon rebuilt on every
  notification) instead of regenerating each time.

## F. Error handling & logging

- Replace empty `catch {}` / `except: pass` with either a real recovery or a logged, contextual failure.
  Catch the **specific** exception you can handle, not blanket `Exception`, unless it's a top-level
  boundary that logs and rethrows.
- Add a **global exception boundary** if missing (`AppDomain.UnhandledException` +
  `DispatcherUnhandledException`; Python `sys.excepthook` + `threading.excepthook`) so nothing dies
  silently.
- Introduce **structured logging** with context (operation, ids) and full stack traces — not
  `print`/`Console.WriteLine`. This is often the single highest-leverage improvement.
- **Open log files as UTF-8 explicitly** (`logging.basicConfig(..., encoding="utf-8")`; explicit
  encoding on any `open()` for logs). On Windows the default is the ANSI codepage, which corrupts
  non-ASCII log content — window titles, user text, even em-dashes in your own messages.

## G. Performance (measured, never guessed)

- Profile or benchmark before optimizing; confirm the hot path is actually hot. Don't sacrifice
  readability for speed the app doesn't need.
- Stream large files/queries instead of buffering fully into memory; page DB queries; avoid N+1 loops.
- Defer expensive startup work (DB open, scans, network) off the launch path so the window appears fast.
- For genuine .NET hot paths: `Span<T>`/`Memory<T>` to cut allocations, avoid boxing, avoid LINQ in
  tight inner loops. Only where measurement justifies it.

## H. Naming

- Reveal intent: `secondsUntilRetry` not `t`; `isComplete` not `flag`. Booleans read as predicates.
- One name per concept across the whole codebase — don't call the same thing `task`, `item`, and
  `activity` in three files. Pick one; rename the rest.
- Follow the language convention consistently (PascalCase methods in C#, snake_case in Python). Don't mix.
- Rename in a dedicated commit so the diff is obviously a no-op rename, not hidden logic change.
- **Avoid names that shadow types in scope** (`Log`, `Timer`, `Path`, `Task`, `Settings` as member
  names) — they compile fine today and break the day someone introduces the matching class.

## I. Culture & locale correctness

- Every date/number formatted into a **database, JSON, or file name** gets `CultureInfo.InvariantCulture`
  (Python: fixed-format `strftime` without locale-dependent names). Current-culture formatting persists
  locale-shaped strings that another machine — or another process sharing the file — can't parse.
- Every **user-visible** date/number gets a deliberate culture matching the app's language, not the OS
  default: an English UI on a Russian-locale OS otherwise renders Cyrillic day names.
- Grep targets: `ToString("` with date/number formats and no `CultureInfo` argument,
  `.ToShortDateString()`, `DateTime.Parse` without a culture, `strftime('%A'/'%B')`.
SKILL_EOF

cat > "$TARGET/windows-code-refiner/references/documentation-standard.md" << 'SKILL_EOF'
# Documentation Standard — what "perfectly commented" means

"Perfectly commented" does **not** mean a comment on every line. Line-by-line narration is noise: it
restates what the code already says, drifts out of sync, and buries the few comments that matter. The
professional standard is different — and it's what this skill applies.

## The one rule: comments explain WHY, code shows WHAT

Good names and clear structure make the *what* self-evident. Comments are reserved for what the code
*can't* say on its own: the reason behind a non-obvious choice, a constraint, a tradeoff, a workaround.

```python
# BAD — restates the code, adds nothing, will rot
i = i + 1  # increment i
retries = 3  # set retries to 3

# GOOD — explains the why the code can't express
# TickTick rate-limits at ~5 req/s; 3 retries with backoff stays under the ceiling.
retries = 3
```

If a comment would only echo the code, delete the comment. If the code needs a comment to be
understood, first ask whether a better *name* would remove the need.

## Document the public surface

Every public class, method, and function that another developer (or future you) will call gets a doc
comment describing its contract — purpose, parameters, return, thrown exceptions, and any side effects or
threading constraints. This is API documentation, not narration, and tooling surfaces it in IntelliSense.

**C# — XML doc comments (`///`):**
```csharp
/// <summary>Persists a completed session and updates the daily score.</summary>
/// <param name="session">The session to record; must have a non-null EndTime.</param>
/// <returns>The recomputed score for the day the session belongs to.</returns>
/// <exception cref="IOException">The database file is locked or unwritable.</exception>
/// <remarks>Thread-safe; may be called from the tracker background thread.</remarks>
public int RecordSession(Session session) { ... }
```

**Python — docstrings (Google or NumPy style, applied consistently):**
```python
def record_session(session: Session) -> int:
    """Persist a completed session and update the daily score.

    Args:
        session: Session to record; ``end_time`` must be set.

    Returns:
        The recomputed score for the session's day.

    Raises:
        sqlite3.OperationalError: If the database is locked.

    Note:
        Thread-safe; may be called from the tracker background thread.
    """
```

Private one-line helpers whose name already says everything don't need a docstring. Use judgment: the
goal is a reader never having to guess a contract, not ceremony for its own sake.

## What is always worth a comment

- **Why, not what:** the reasoning behind a non-obvious decision.
- **Business rules** encoded as bare conditionals (`if score < 40:` → what does 40 mean and why).
- **Workarounds** for a known bug, API quirk, or platform behaviour — annotate with a reference, so the
  next developer doesn't "clean it up" and reintroduce the bug.
- **Units, ranges, and invariants:** `timeoutMs`, "0–100", "caller owns disposal", "must hold `_lock`".
- **Magic constants** that survive (better: name them; if they must stay inline, explain them).
- **Concurrency contracts:** what thread a method runs on, what guards what.

## What NOT to do

- No comment that restates the code (`// loop over users`).
- No commented-out code — delete it; version control is the history.
- No stale or wrong comments — a comment that lies is worse than none. If you change the code, update or
  remove the comment in the same edit.
- No decorative banners or dead TODO graveyards. A TODO must say who/what/why, ideally with a ticket.
- No docstrings that describe a different signature than the actual function.

## Module / file headers

A short header at the top of a non-trivial module stating its single responsibility helps navigation:

```python
"""TickTick OAuth2 client: token acquisition, refresh, and authenticated requests.

Owns nothing UI-related. All network access to TickTick goes through this module.
"""
```

Keep it to purpose and boundaries — not a changelog (that's what VCS is for).

## The standard, in one line

Self-documenting names, a documented public contract, why-comments where intent isn't obvious, and
nothing else. When you finish, a competent developer should be able to read any file top-to-bottom and
never have to reverse-engineer *why* — while never wading through comments that just echo the code.
SKILL_EOF

cat > "$TARGET/windows-app-tester/SKILL.md" << 'SKILL_EOF'
---
name: windows-app-tester
description: >-
  Build and maintain automated test suites and runtime QA for Windows desktop apps (Python/Tkinter and
  .NET/WPF/WinForms/WinUI): unit tests for core logic, DB-layer tests against throwaway databases,
  characterization tests before refactors, a scripted smoke harness, and soak/performance measurement
  (memory over hours, startup time). Use whenever the user wants to "write tests," "add a test suite,"
  "cover this with tests," "characterization tests," "smoke test," "soak test," "measure startup/memory,"
  or asks how to stop regressions — and proactively after any remediation or refactor that currently has
  no persistent tests protecting it. This is the PROTECT step: the auditor finds defects, the refiner
  cleans code, this skill makes sure neither has to be re-done by hand next month. Do NOT use for web
  apps or mobile-only apps.
---

# Windows App Tester

Manual verification dies with the session that ran it. This skill converts "I launched it and it looked
fine" into a suite that anyone — including a future Claude session — can run in one command and trust.
Tests are the launch team's memory.

## What to test, in priority order

1. **Core logic that silently corrupts** — date/culture formatting, score/money math, SQL
   parameter-to-column mapping, idempotency guards. These are the bugs that never crash and are
   discovered months later in the data. One unit test each is cheap insurance.
2. **The data layer** — schema creation, writes land in the right columns, reads round-trip, the
   "database is locked" path does what the app promises (logs + surfaces, not swallows).
3. **Characterization tests** — before any refactor or rewrite of untested code, pin current behavior
   (including its quirks) so accidental change shows up as a red test. Mandatory partner to the
   refiner skill.
4. **A smoke harness** — scripted launch → window title check → responsiveness → log-file check →
   clean close. This is the same smoke run the remediation loop does by hand, made repeatable.
5. **Runtime QA** — soak (memory/handles over hours), startup time. See `references/runtime-qa.md`.

Don't chase coverage percentages. Cover the logic whose failure is silent or expensive; skip trivial
getters and UI layout.

## Making desktop code testable

Most desktop apps start with logic buried in UI classes. The order of operations:

- **Extract before you test, when extraction is cheap.** A `today_key()` or score formula inside a
  window class can usually be pulled to module level (or a plain class) in minutes — coordinate with
  the refiner's rules (behavior-preserving, characterization first if risky).
- **When extraction is not cheap, test through the seam you have:** import the module without starting
  the UI (guard `mainloop()`/`Application.Run` behind `main()` / `if __name__ == "__main__"` — add the
  guard if missing, it's a two-line behavior-preserving change), instantiate logic classes directly,
  point them at a temp data directory.
- **UI-widget testing is last resort on Windows desktop** — Tkinter needs a real display, WinUI needs
  the app packaged/running. Prefer moving logic out of widgets over automating clicks. If click-level
  automation is truly required (.NET), FlaUI/WinAppDriver exist — treat as expensive integration
  tests, few in number.

## Stack specifics

**Python:** `pytest`, tests in `tests/`, run with `python -m pytest tests/ -q`. Use `tmp_path` for
every file/DB the code writes. Freeze time by injecting a clock or monkeypatching `datetime` where
logic depends on "now". Import the app module with its UI entry guarded.

**.NET:** xUnit (or MSTest) in a sibling `*.Tests` project targeting the same TFM; run with
`dotnet test`. Extract logic into testable services (the app's `Services/` classes should be
constructible without a window). SQLite tests against a temp file via the same `Database` class.

## Hard rules

- **Never point a test at the user's real data.** Tests create their own temp DB/config and delete it.
  A test that "just reads" the live database will eventually be joined by one that writes. (The
  Mentor-Overseer live DB is production data — treat it that way.)
- **Deterministic or deleted.** No `sleep`-and-hope, no time-of-day dependence (a test that fails only
  after 20:00 because of an EOD rule is a real historical failure mode — inject the clock), no
  dependence on OS locale (set/pin it in the test, both ways: the invariant path AND a hostile-locale
  path).
- **Never weaken a test to make it pass.** If a test goes red, either the code regressed (fix the
  code) or the test asserted an implementation detail (change the test *deliberately, in its own
  commit, saying so*). Deleting/skipping a failing test to ship is a Critical-severity act.
- **One command, green, fast.** The whole unit suite must run in seconds and exit non-zero on failure
  so scripts and future sessions can gate on it. Slow soak/integration tests live behind a marker
  (`-m soak`), not in the default run.
- **Runtime tests follow the auditor's runtime-safety rules** — no focus stealing, no screen capture;
  `PrintWindow` flag 2 only, if visual evidence is needed at all.

## Workflow

1. Inventory: what logic exists, what's silently-corrupting-class, what's already covered.
2. Make the module importable without UI (add the `main()` guard if absent).
3. Write the priority-1/2 unit tests against temp resources; run; green.
4. Add the smoke harness script (see `references/test-recipes.md`).
5. Wire a one-command entry point (`run-tests.bat` / `dotnet test`) and document it in the README.
6. Report: what's covered, what deliberately isn't and why, and the command to run it all.

## Reference files

- `references/test-recipes.md` — concrete, copy-adaptable patterns: pytest + temp SQLite, frozen
  clock, hostile-locale test, characterization harness, Tkinter-without-display, xUnit + temp DB,
  smoke harness script (PowerShell), test entry-point scripts.
- `references/runtime-qa.md` — soak testing (memory/handle growth over hours), startup-time
  measurement, thresholds and how to read them, safe evidence capture.
SKILL_EOF

cat > "$TARGET/windows-app-tester/references/test-recipes.md" << 'SKILL_EOF'
# Test Recipes — copy, adapt, keep green

Concrete patterns for the situations Windows desktop apps actually present. Every recipe uses
throwaway resources — none touches real user data.

## 1. Pytest + temp SQLite (the workhorse)

```python
# tests/test_store.py
import sqlite3
import pytest
import focus_diary as app  # module must import without starting the UI


@pytest.fixture
def temp_base(tmp_path, monkeypatch):
    """Point every file the app writes at a throwaway directory."""
    monkeypatch.setattr(app, "BASE", tmp_path)
    monkeypatch.setattr(app, "DB", tmp_path / "data" / "diary.db")
    monkeypatch.setattr(app, "LOG", tmp_path / "data" / "test.log")
    return tmp_path


def test_activity_columns_mean_what_they_say(temp_base):
    """Regression guard for the classic bound-to-wrong-column bug."""
    conn = app.db()
    conn.execute("INSERT INTO activity(logged_at, category, note) VALUES (?, ?, ?)",
                 ("2026-01-01 00:00:00", "focus", "auto"))
    conn.commit()
    row = conn.execute("SELECT logged_at, category, note FROM activity").fetchone()
    conn.close()
    assert row == ("2026-01-01 00:00:00", "focus", "auto")
```

The `monkeypatch.setattr` on module-level path constants is the cheapest seam when the app wasn't
built with dependency injection. If paths are computed inside functions, extract them to module level
first (behavior-preserving, tiny diff).

## 2. Frozen clock

Logic that depends on "now" (EOD rules, day keys, streaks) must be tested at controlled times —
including the boundaries (23:59:59, exactly EOD, midnight rollover):

```python
class FakeDateTime:
    fixed = None
    @classmethod
    def now(cls):
        return cls.fixed

def test_day_key_at_midnight_boundary(monkeypatch):
    import datetime as real_dt
    FakeDateTime.fixed = real_dt.datetime(2026, 12, 31, 23, 59, 59)
    monkeypatch.setattr(app, "datetime", FakeDateTime)
    assert app.App.today_key(None) == "2026-12-31"
```

(If `today_key` is an instance method that doesn't use `self`, it can be called with `None` — better:
extract it to a module function while you're here.)

## 3. Hostile-locale test

The invariant-culture bug class only shows up under a non-default locale — so create one on purpose:

```python
def test_persisted_dates_ignore_locale(monkeypatch):
    import locale
    try:
        locale.setlocale(locale.LC_ALL, "ru_RU.UTF-8")   # or "Russian_Russia.1251" on Windows
    except locale.Error:
        pytest.skip("Russian locale not available on this machine")
    try:
        key = app.App.today_key(None)
        assert key == __import__("datetime").datetime.now().strftime("%Y-%m-%d")
        assert all(ch.isascii() for ch in key)
    finally:
        locale.setlocale(locale.LC_ALL, "C")
```

In .NET the equivalent: set `CultureInfo.CurrentCulture = new CultureInfo("ru-RU")` in the test and
assert the persisted string is invariant-shaped.

## 4. Characterization harness (before refactors)

Pin what the code does *today*, quirks included — the point is detecting change, not asserting
correctness:

```python
@pytest.mark.parametrize("done,total,expected", [
    (0, 0, 0), (1, 3, 10), (3, 3, 30),   # captured from current behavior, verified by running it
])
def test_score_formula_characterization(done, total, expected):
    assert app.score_for(done, total) == expected
```

Generate the expected values by *running the current code once* and recording outputs — never by
reasoning about what it "should" do. If a captured value looks wrong, file it as a finding; don't fix
it inside the characterization commit.

## 5. Tkinter without a display

Unit tests must not need a window. Two rules make that true:

- The module's last lines are `if __name__ == "__main__": main()` — importing runs nothing.
- Logic methods don't touch `self.root`/widgets. Where one does both (computes AND renders), split it
  (`compute_score()` + `refresh_labels()`), then test the compute half.

Widget-level Tk tests are possible (`root = tk.Tk(); root.withdraw()`) on a machine with a desktop
session — mark them `@pytest.mark.gui` and exclude from the default run; they're flaky headless.

## 6. xUnit + temp DB (.NET)

```csharp
public class DatabaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public DatabaseTests()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("MENTOR_ROOT", _dir);  // app's own root override hook
    }

    [Fact]
    public void SaveCompletion_RoundTrips()
    {
        using var db = new Database();
        db.SaveCompletion("plan-a", 3, "Read chapter 1", done: true);
        Assert.True(db.LoadCompletions()[("plan-a", 3, "Read chapter 1")]);
    }

    public void Dispose() { Directory.Delete(_dir, recursive: true); }
}
```

An env-var root override (like `MENTOR_ROOT`) is the ideal seam — if the app has one, use it; if not,
adding one is a small, safe change that pays off everywhere (tests, debugging, portable installs).

## 7. Smoke harness (PowerShell, scripted version of the manual smoke run)

```powershell
# smoke.ps1 -- exit 0 = pass. Follows runtime-safety rules: no focus theft, no screen capture.
param([string]$Exe = ".\FocusDiary.exe")
$before = (Get-Content .\data\*.log -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
$p = Start-Process $Exe -PassThru
Start-Sleep -Seconds 6
$proc = Get-Process ($p.Name) -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle }
if (-not $proc -or -not $proc.Responding) { Write-Error "window missing or hung"; exit 1 }
if ($proc.MainWindowTitle -ne "Focus Diary") { Write-Error "wrong title: $($proc.MainWindowTitle)"; exit 1 }
$proc.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 3
if (Get-Process ($p.Name) -ErrorAction SilentlyContinue) { Write-Error "did not close cleanly"; exit 1 }
$after = Get-Content .\data\*.log -ErrorAction SilentlyContinue | Select-Object -Skip $before
if ($after -match "\[ERROR\]") { Write-Error "errors logged during smoke: $after"; exit 1 }
"SMOKE PASS"
```

Adapt titles/paths per app. For apps with page-navigation debug hooks (env var), loop the hook values
to smoke every page.

## 8. One-command entry points

- Python: `run-tests.bat` → `python -m pytest tests/ -q --tb=short` (unit) and
  `powershell -File smoke.ps1` as a second line or separate script.
- .NET: `dotnet test` already is the command; add `--filter Category!=Gui` if GUI-marked tests exist.

Document the command in the project README. A suite nobody knows how to run does not exist.
SKILL_EOF

cat > "$TARGET/windows-app-tester/references/runtime-qa.md" << 'SKILL_EOF'
# Runtime QA — soak, startup, and reading the numbers

The static passes explicitly defer "memory over hours" and "actual startup time" to runtime — this is
where they get owned instead of forgotten. All procedures follow the runtime-safety rules: no focus
stealing, no screen capture (PrintWindow flag 2 only if visual evidence is needed).

## 1. Soak test (the one that matters for always-on apps)

An app meant to run all day (a tracker, a monitor) must be tested *running all day*. The failure
modes it catches — event-handler leaks, stacked timers, unbounded caches, handle leaks — are invisible
in a 30-second smoke run by construction.

```powershell
# soak.ps1 — sample every 5 min for N hours; writes soak.csv
param([string]$ProcName = "MentorOverseer.App", [int]$Hours = 8)
"time,workingset_mb,private_mb,handles,threads" | Out-File soak.csv
$end = (Get-Date).AddHours($Hours)
while ((Get-Date) -lt $end) {
    $p = Get-Process $ProcName -ErrorAction SilentlyContinue
    if (-not $p) { "$(Get-Date -F s),PROCESS_GONE,,," | Add-Content soak.csv; break }
    "$(Get-Date -F s),$([math]::Round($p.WorkingSet64/1MB,1)),$([math]::Round($p.PrivateMemorySize64/1MB,1)),$($p.HandleCount),$($p.Threads.Count)" |
        Add-Content soak.csv
    Start-Sleep -Seconds 300
}
```

Run it in the background during a normal day of real use — synthetic idling misses the interactions
(view rebuilds, dialogs, reconnects) that actually leak.

**Reading the CSV:**
- **Healthy:** working set rises early, then plateaus with sawtooth (GC/caching). Handles and threads
  flat after warmup.
- **Leak:** any counter with a steady positive slope across the whole run. Handles creeping by even
  ~10/hour = a handle leak that kills the app in days. Thread count growing = something spawns
  without joining (look at timers and reconnect paths).
- Exercise the suspicious feature repeatedly (open/close the dialog 50×, rebuild the view 50×) and
  re-sample — a per-use leak becomes obvious in minutes this way.

## 2. Startup time

Measure cold-ish start to interactive window, 5 runs, report median (first run after boot is the
honest "cold" number; note it separately):

```powershell
1..5 | ForEach-Object {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process $exe -PassThru
    while (-not $p.MainWindowHandle -or $p.MainWindowHandle -eq 0) { Start-Sleep -Milliseconds 50; $p.Refresh() }
    $sw.Stop(); $p.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 2
    $sw.ElapsedMilliseconds
}
```

Rules of thumb for a small desktop utility: **< 1.5 s** to window feels instant, **1.5–4 s**
acceptable, **> 4 s** find and defer the expensive startup work (the auditor's architecture pass
lists the usual suspects). PyInstaller onefile adds ~1–2 s self-extraction — that's a packaging
choice (onedir is faster) — and self-contained .NET first-run JIT is real; measure, don't guess.

## 3. When to run which

- **Every release candidate:** startup measurement + at least a 2-hour soak during real use.
- **After fixing anything timer/event/cache related:** targeted repeat-the-action soak (50× loop).
- **Before first public release:** one full-workday soak on the machine class users actually have,
  not just the dev box.

## 4. Recording results

Keep a `qa/` folder in the repo: `soak-YYYY-MM-DD.csv` + a three-line verdict in the release notes
("8h soak 2026-07-07: WS 84→91 MB plateau, handles flat at 412, verdict PASS"). Numbers without a
recorded baseline are unreadable next release.
SKILL_EOF

cat > "$TARGET/windows-app-releaser/SKILL.md" << 'SKILL_EOF'
---
name: windows-app-releaser
description: >-
  Take a Windows desktop app from "builds on my machine" to "installs cleanly on a stranger's machine":
  versioned one-command builds, installers (Inno Setup / MSIX / portable zip), code signing strategy,
  changelogs and git tags, a safe update path, crash reporting that respects privacy, and
  install/upgrade/uninstall verification. Use whenever the user wants to "make an installer," "ship it,"
  "release," "package," "distribute," "sign the app," "version bump," "publish a build," "auto-update,"
  or asks how someone else can install their app. Covers Python (PyInstaller) and .NET
  (dotnet publish) apps. This is the SHIP step — the gate between a working build and the market. Do NOT
  use for web deployment or app-store submissions for mobile.
---

# Windows App Releaser

A release is not an exe — it's a versioned, signed, installable, upgradeable, uninstallable,
diagnosable artifact plus the paper trail (tag, changelog, checksums) to reproduce it. This skill
builds that pipeline and refuses to pretend a loose Debug exe is a release.

## The pipeline (every release walks these stages, scripted)

1. **Version stamp** — one source of truth (`__version__` / `<Version>` in the csproj), bumped
   deliberately (semver: breaking.feature.fix), surfaced in the app's About/Settings and its log
   header (support will ask "what version?" — the app must know).
2. **Clean build, Release configuration** — one committed script (`release.bat` / `release.ps1`)
   from a clean tree. Never ship Debug: it's slower, larger, and leaks assert/diagnostic behavior.
   - Python: `pyinstaller --noconsole --name App --distpath dist ...` (onedir starts faster;
     onefile is tidier but adds 1–2 s self-extraction — pick per app, document why).
   - .NET: `dotnet publish -c Release -p:Platform=x64` (self-contained for no-runtime-install UX,
     framework-dependent for small downloads — pick and document).
3. **Package** — see `references/packaging-recipes.md` for the decision matrix and skeletons:
   - **Inno Setup** — the pragmatic default for indie distribution outside the Store.
   - **MSIX** — for Microsoft Store or managed environments; brings auto-update but sandbox
     constraints (an app that reads a sibling app's files or walks up from the exe may break —
     verify the app's file-access pattern *first*).
   - **Portable zip** — legitimate for tool-savvy audiences; still versioned and checksummed.
4. **Sign** — see signing reality below.
5. **Verify** — the non-negotiable checklist:
   - Fresh install on a machine/profile that has never seen the app (a new Windows user account is
     the cheap approximation of a clean machine).
   - **Upgrade-in-place over the previous version with real user data present — data must survive.**
     This is where frozen-contract/migration promises are actually tested.
   - Uninstall: user data handling is a deliberate, documented choice (default: keep data, say so);
     no orphaned autostart entries, services, or protocol handlers.
   - Smoke run of the *installed* copy (not the build-tree copy) — installed apps run from
     `Program Files` with different permissions and working directory than the dev tree.
6. **Record** — git tag `vX.Y.Z`, changelog entry (user-language, not commit messages), SHA-256
   checksums next to the artifacts. Artifacts do NOT get committed to the repo.

## Code signing reality (decide, don't drift)

- Unsigned executables get SmartScreen's "unrecognized app" wall and a scary blue banner — for a
  general audience that is a launch blocker, full stop. For a private beta of trusted users it's
  survivable, but say so out loud in the release notes.
- Signing requires an Authenticode certificate with a **hardware-backed key** (current CA/B rules):
  a token/HSM cert from a CA, or **Azure Trusted Signing** (subscription service — the practical
  indie option). Always timestamp (`/tr`) so signatures outlive cert expiry.
- Even signed, *new* certs build SmartScreen reputation over downloads — early users may still see
  warnings. That's expected; don't burn days "fixing" it.
- MSIX must be signed with a cert the target machine trusts (Store signing handles this for Store
  distribution; sideloading needs your cert installed or Trusted Signing).

## The update path (highest-trust component — design it boring)

The safest v1 update mechanism is **notify-and-link**: check a version endpoint over HTTPS, tell the
user "1.4 is available", link to the download. No code execution, tiny attack surface. Only build
silent auto-update when there's a real need, and then the auditor's security rules apply in full:
TLS with validation, signature verified before execution, downgrade refusal, atomic swap with
rollback. An unsigned auto-executing update is the single worst security decision a desktop app can
make — the audit will flag it Critical, so don't build it.

## Crash reporting & field diagnostics (opt-in, minimal, honest)

You cannot fix what you cannot see, and post-launch you can't see anything without telemetry — but a
desktop app that phones home by default torches trust (and for monitoring-class apps, may leak the
user's private data into your inbox).

- The app already logs locally (auditor requirement). Add a **"Report a problem"** action that
  bundles: app version, OS version, the last N KB of the log, the stack trace — shows the user the
  exact bundle, and lets *them* send it (mailto / upload button). Opt-in per event beats silent
  streaming for a personal-tool audience.
- **Redact before bundling**: window titles, task texts, and anything personal have no place in a
  crash report (privacy pass rules apply to telemetry too).
- Automatic crash-report streaming (Sentry-class) is for later scale; if added, it's opt-in at
  first run, documented in the privacy notes, and scrubbed at the source.

## Hard rules

- Never ship Debug. Never ship uncommitted code (tag = exact tree). Never ship without the verify
  checklist — "it worked in the dev tree" is not evidence for the installed artifact.
- Unsigned distribution to a general audience is a decision the *user* makes after being told the
  SmartScreen consequence — not a silent default.
- The release script is committed and is the only way releases get built. A release nobody can
  rebuild is a liability.
- Upgrade must preserve user data, proven by actually doing it in verification — not by reading the
  code and nodding.

## Reference files

- `references/packaging-recipes.md` — packaging decision matrix; Inno Setup script skeleton;
  PyInstaller and dotnet-publish command sets; signtool/Trusted Signing usage; versioning and
  changelog conventions; the verification checklist in runnable form.
SKILL_EOF

cat > "$TARGET/windows-app-releaser/references/packaging-recipes.md" << 'SKILL_EOF'
# Packaging Recipes

## Decision matrix

| Route | Pick when | Cost / catch |
|---|---|---|
| **Inno Setup** | Direct download distribution, indie/prosumer audience | Learn one `.iss` file; unsigned installers still hit SmartScreen |
| **MSIX** | Microsoft Store, or managed/enterprise fleets | Sandbox: virtualized filesystem/registry — apps that share files with a sibling app, walk up from the exe, or write next to themselves need rework first; signing mandatory |
| **Portable zip** | Tech-savvy users, USB-stick portability, zero-install policy | No Start-menu/uninstall integration; users lose it in Downloads; still version + checksum it |
| **Bare exe** | Never for the public. Dev/beta-of-one only | No version trail, no upgrade story |

Store-vs-direct is a *distribution* choice, not just packaging: the Store brings discovery, silent
updates, and its own certification pass; direct download brings control and no revenue share.

## Inno Setup skeleton (the 90% case)

```ini
; app.iss — compile with: iscc app.iss
#define AppName "Focus Diary"
#define AppVersion "1.0.0"          ; single source of truth — stamp from the build script
#define AppExe "FocusDiary.exe"

[Setup]
AppId={{8E1B2A34-YOUR-GUID-HERE}}   ; generate ONCE, never change — it's the upgrade identity
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}  ; per-machine; use {userpf} for per-user, no-elevation installs
PrivilegesRequired=lowest            ; per-user default — matches least-privilege audit rule
OutputBaseFilename={#AppName}-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
; SignTool=mysign $f                ; wire to signtool when a cert exists

[Files]
Source: "dist\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "Create a desktop icon"; Flags: unchecked

[UninstallDelete]
; Deliberately EMPTY for user data — uninstall keeps the user's database/config.
; Document this choice. If offering data wipe, make it an explicit checked-by-user page.
```

Key points: the `AppId` GUID is the upgrade identity (same GUID → installs over the old version);
`PrivilegesRequired=lowest` keeps the least-privilege promise; user data is *not* under `{app}` —
apps should write to `%APPDATA%\AppName` (auditor architecture rule) so `Program Files` stays
read-only and uninstall/upgrade can't eat data.

**App data location note:** dev-tree apps often write next to the exe. Under `Program Files` that
*fails* for non-elevated users. Before packaging, verify the app resolves its data dir to
`%APPDATA%` (or supports an override env var) — this is the #1 "works in dev, broken installed" bug.

## Build commands

**Python / PyInstaller:**
```powershell
# onedir (faster startup, folder output) — preferred for installers:
python -m PyInstaller --noconsole --name FocusDiary --distpath dist --workpath build `
    --specpath build focus_diary.py
# onefile (single exe, +1-2s self-extract) — for portable/zip distribution:
python -m PyInstaller --onefile --noconsole --name FocusDiary focus_diary.py
```
Remember the frozen-path rule: `sys.executable`'s dir when `getattr(sys, "frozen", False)`, else
`__file__`'s — and installed apps should prefer `%APPDATA%` for writes (see above).

**.NET:**
```powershell
# self-contained (no runtime prerequisite, ~80MB+):
dotnet publish -c Release -p:Platform=x64 --self-contained true -r win-x64
# framework-dependent (small, needs .NET runtime installed):
dotnet publish -c Release -p:Platform=x64 --self-contained false
```
WinUI 3 unpackaged: keep `WindowsAppSDKSelfContained=true` unless you want users installing the
WASDK runtime themselves.

## Signing commands

```powershell
# Classic signtool with a token/HSM cert:
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /n "Cert Subject" path\to\artifact
# Azure Trusted Signing: same signtool, /dlib + metadata JSON per their docs (practical indie route).
# Verify:
signtool verify /pa /v path\to\artifact
```
Sign the exe AND the installer. Always timestamp — an untimestamped signature dies with the cert.

## Version & changelog conventions

- Semver: MAJOR breaking (incl. data-format changes!), MINOR features, PATCH fixes.
- Stamp once, read everywhere: Python `__version__ = "1.2.0"` used by the About dialog, log header,
  and the build script (injected into the `.iss` via `/D` or a generated include).
- `CHANGELOG.md`, newest first, written for users ("Fixed: score no longer says 'Final' before the
  day ends"), not for git archaeologists.
- Tag after verification passes: `git tag -a v1.2.0 -m "..."`. Checksums:
  `Get-FileHash *.exe,*-setup.exe -Algorithm SHA256 > SHA256SUMS.txt`.

## Verification checklist (runnable form)

```text
[ ] Release build from clean tree via the committed script (no local edits — git status clean)
[ ] Fresh install on a never-seen profile: installs without elevation surprises, launches, smoke passes
[ ] Installed-copy smoke (from Program Files, not the dev tree)
[ ] Upgrade test: install PREVIOUS version, create real data, install NEW over it → data intact,
    version shows new
[ ] Uninstall: app gone, Start-menu entries gone, autostart/protocol entries gone, user data kept
    (and that choice documented)
[ ] Tag pushed, changelog updated, checksums generated
[ ] SmartScreen behavior checked on a machine that never saw the cert (know what users will see)
```
SKILL_EOF

cat > "$TARGET/launch-readiness/SKILL.md" << 'SKILL_EOF'
---
name: launch-readiness
description: >-
  Run a go/no-go launch gate for a Windows desktop app: score every readiness category (audit health,
  test coverage, release pipeline, privacy posture, runtime QA, beta feedback, docs & support) against
  hard evidence and produce a scorecard with a clear GO / NO-GO recommendation and the shortest path to
  GO. Also owns the beta program playbook (recruiting testers, the 2-week protocol, feedback triage).
  Use whenever the user asks "are we ready to launch/ship/release," "what's missing before launch,"
  "run the launch checklist," "go/no-go," "can I give this to other people now," or wants to plan or
  evaluate a beta. This skill GATES; it does not fix — it dispatches to windows-app-auditor,
  windows-app-tester, windows-app-releaser, or windows-code-refiner for the actual work. Do NOT use
  for web or mobile launches.
---

# Launch Readiness

The gate between "works for me" and "in strangers' hands." Every category below gets a verdict —
**READY / AT RISK / BLOCKED** — backed by evidence that exists *outside this conversation* (a file, a
commit, a CSV, a tag). "I remember it being fine" scores as AT RISK by definition.

This skill never rubber-stamps. If the honest answer is NO-GO, say NO-GO and give the shortest path
to GO. And never inflate: "100% sure" does not exist in shipping software — the professional target
is: every *known* risk class has an owner and evidence; the unknown ones have instrumentation (logs,
crash reporting) so they'll be seen when they happen.

## The seven categories

Score each; the report is the scorecard plus the path to GO.

1. **Audit health** — most recent full audit (all five passes, incl. privacy) on the *current* code:
   no Critical/High open; Mediums fixed or explicitly accepted with rationale. Evidence: the audit
   report + remediation record. Audit older than the last significant code change = AT RISK (stale).
2. **Test protection** — suite exists, green, runnable in one documented command; covers the
   silently-corrupting logic (dates/culture, money/score math, DB mapping); smoke harness scripted.
   Evidence: the passing run output + the command in the README.
3. **Release pipeline** — versioned Release build from a committed script; installer verified on a
   clean profile; **upgrade-over-previous-with-data test passed**; uninstall clean; signing decision
   made *consciously* (signed, or unsigned-with-documented-SmartScreen-consequence for a limited
   beta). Evidence: the verification checklist filled in, tag + changelog + checksums.
4. **Privacy posture** — privacy pass run; first-run disclosure exists for monitoring features;
   pause control; export/delete present; retention decided; license notices shipped. For an app that
   records behavior, this category BLOCKS on disclosure/consent — no exceptions.
5. **Runtime QA** — startup time measured and acceptable; soak run on a realistic day (memory/handles
   plateau); results recorded in `qa/`. Evidence: the CSV + the three-line verdict.
6. **Beta signal** — real non-author humans used a release-built install; feedback triaged through
   the severity model; no open Critical/High from the field; the top friction items consciously
   accepted or fixed. See `references/beta-program.md`. For a first public step, 3–5 testers over
   2 weeks is the floor — zero external users = AT RISK at best.
7. **Docs & support** — a user-facing README/quick-start (what it does, install, first run, where
   data lives, how to uninstall); a way to report problems (email/issues link, ideally the in-app
   "Report a problem" bundle); the changelog current.

## Verdict rules

- Any category **BLOCKED** → **NO-GO**. More than two **AT RISK** → **NO-GO** for a public launch
  (may still be GO for a *closed beta*, whose whole purpose is converting AT RISK to evidence — say
  which launch tier the verdict applies to: closed beta / open beta / public).
- The recommendation always ends with the **shortest path to GO**: the minimum ordered list of
  actions, each mapped to the skill that does it.

## Report template

```
# Launch Readiness — [app] [version] — [date]

Target tier: [closed beta / open beta / public]

| # | Category | Verdict | Evidence | Gap (if any) |
|---|----------|---------|----------|---------------|
| 1 | Audit health | READY/AT RISK/BLOCKED | [link/file/commit] | ... |
| ... |

## Verdict: GO / NO-GO for [tier]
[2-4 sentences of honest reasoning.]

## Shortest path to GO
1. [action] → [skill/owner]
2. ...

## Accepted risks going in
[Every AT RISK item consciously carried into launch, one line each, so nobody discovers them as surprises.]
```

## Reference files

- `references/beta-program.md` — recruiting, the 2-week protocol, the feedback form, triage into the
  severity model, exit criteria.
SKILL_EOF

cat > "$TARGET/launch-readiness/references/beta-program.md" << 'SKILL_EOF'
# Beta Program Playbook

Static passes can verify everything except the one thing that decides adoption: what real humans do
with the app on machines you've never seen. A beta converts guesses into evidence. Small and
structured beats big and vague — 3 engaged testers with a protocol outperform 30 silent downloads.

## 1. Recruiting

- **3–10 people matching the intended audience** (the ux.md §0 answer — not just fellow developers,
  unless developers ARE the audience). Friends are fine for wave one *if* they'll be honest; add at
  least one person who owes you nothing.
- Machine diversity on purpose: at least one laptop with display scaling ≥150%, one non-English
  Windows locale, one modest/older machine. These three surface the DPI, culture, and performance
  bug classes respectively — the exact classes static audits flag as "needs runtime check."
- What they get: the versioned installer (release-built, from the pipeline — never a dev-tree exe),
  the quick-start doc, and the known-issues list (honesty up front makes feedback honest).

## 2. The 2-week protocol

- **Day 0** — install unassisted. The tester writes down every moment of confusion from download to
  first successful use. You say *nothing* during this; the silence is the test. First-run confusion
  is the highest-value data a beta produces and it can only be harvested once per tester.
- **Days 1–13** — normal use, no reminders beyond one mid-point nudge. A daily-use app that testers
  quietly stop opening by day 4 has told you the most important finding of the whole program —
  instrument for it (ask for the app's log or a screenshot of their week view at the end, with
  their consent).
- **Day 14** — the structured form (below) plus a 15-minute conversation if they'll give it. Collect
  log files (with consent — logs may contain personal data; the privacy pass rules apply to *your
  own beta collection* too).

## 3. The feedback form (short enough to actually get answered)

```
1. Setup: did anything confuse or stop you between download and first use? What exactly?
2. In one sentence: what does this app do? (Tests whether the product concept landed.)
3. How many days did you actually use it? What made you open it — or stop opening it?
4. The single most annoying thing?
5. The single most valuable thing?
6. Anything that looked wrong/broken/ugly on your machine? (screenshot welcome)
7. Would you keep using it after today? Would you pay [price] for it? (blunt on purpose)
8. Machine: Windows version, display scaling, system language.
```

## 4. Triage — feedback becomes findings, not vibes

Run every report through the auditor's severity model, same as code findings:

- Crash/data loss/"it recorded something it shouldn't" → **Critical/High**, fix before wider release.
- "I stopped using it because…" → treat as **High** product finding — retention failure is launch
  failure for a daily-use app, even though no code is "broken."
- Friction/confusion items → Medium/Low, batch into the normal remediation loop.
- Every item gets a verdict: **fixed / accepted (with reason) / deferred (to when)** — reported back
  to testers ("you said X, we did Y"). Testers who see their feedback land will test your next wave;
  testers who hear silence won't.

## 5. Exit criteria (the beta is done when…)

- No open Critical/High from the field.
- ≥ half the testers completed setup with zero assistance.
- ≥ half still *voluntarily* using it in week 2 (for a daily-use app; adjust for occasional-use).
- The "would you keep using it" answers are mostly yes — or the noes share one fixable reason.
- Every piece of feedback has a triage verdict recorded.

Results feed category 6 of the launch scorecard. A failed beta is a *successful* program — it bought
the truth for ten users instead of a thousand.
SKILL_EOF

echo "Done. Installed files:"
find "$TARGET" -path "*windows-app-*" -o -path "*windows-code-*" -o -path "*launch-readiness*" -type f | sort
