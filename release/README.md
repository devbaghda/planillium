# Releasing Planillium

One command builds the installer:

```powershell
.\release\release.ps1
```

That publishes a self-contained x64 build, stages `release\dist\` (the exe plus a
first-run `config.json` / empty `plans\` / `data\`), compiles
`release\installer\app.iss` with Inno Setup, and writes
`release\output\Planillium-<version>-setup.exe` + `SHA256SUMS.txt`. Neither
`dist\` nor `output\` are committed (see `.gitignore`) — the installer is a build
artifact, not source.

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install
JRSoftware.InnoSetup`) on the machine building the release.

## Versioning

The `<Version>` in `winui\Planillium.App\Planillium.App.csproj` is the one
source of truth — `release.ps1` reads it, the compiled app reads it back via
`Services\AppVersion.cs` and shows it in Settings → Data and in the first line of
`data\mentor-winui.log`. Bump it, commit, *then* release. Semver: MAJOR for a
data-format break, MINOR for features, PATCH for fixes.

## Before tagging a version "done"

```text
[ ] Release built from a clean tree (release.ps1 aborts on a dirty tree unless -Force)
[ ] Fresh install: a folder that has never held the app — installs without asking for
    admin, launches, shows the empty-plans first-run state, no crash
[ ] Installed-copy smoke: exercise Today / Schedule / Reports / Settings from the
    installed copy (not the dev tree) — different working directory, different perms
[ ] Upgrade test: install the PREVIOUS version, create real data (a plan, some
    completions), install the NEW version over it → data intact, Settings shows the
    new version number
[ ] Uninstall: app gone from Start Menu/Programs, desktop shortcut gone if it was
    created, data\ and plans\ still on disk (by design — see below), no leftover
    HKCU Run entry even if "Start with Windows" was ever toggled on
[ ] git tag -a v<version> -m "..." and push it
[ ] SHA256SUMS.txt present next to the setup exe
```

## Signing — not done yet

The installer and exe are **unsigned**. That means Windows SmartScreen shows an
"unrecognized app" screen on first run — click "More info" → "Run anyway". For
your own machines that's a one-time nuisance, not a blocker. If this app is ever
handed to anyone else, get an Authenticode cert (a hardware-backed token/HSM cert,
or Azure Trusted Signing for the practical indie route) before shipping to them —
see `signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 ...`.

## What the installer does and doesn't carry

- **Ships:** the app itself, and a *default* `config.json` (working hours, activity
  keyword rules, scoring) — a clean baseline, not a snapshot of whatever's currently
  configured on this machine.
- **Does not ship:** `data\progress.db` (task completion history, the score ledger,
  the time diary) or `plans\active\*.json` (your actual plans). Those are
  *this machine's* data, not release artifacts — baking a dated snapshot into a
  versioned installer would go stale immediately.

### Moving to a new machine

Install normally, then copy three things from the old machine's install folder into
the new one, overwriting the installer's defaults:
```
config.json
plans\active\
data\
```
(`data\` holds `progress.db` — the actual history/score continuity — plus the log
files, which are safe to leave behind.) TickTick reconnects fresh either way; the
OAuth token lives in Windows Credential Manager, which doesn't travel with a file
copy.

## Uninstall data policy

Deliberate default: **uninstalling keeps `data\` and `plans\`.** `[UninstallDelete]`
in `app.iss` is intentionally empty, and Inno only removes directories it installed
if they're still empty — so a populated `data\` folder survives an uninstall
untouched. If a clean wipe is ever wanted, that's a manual step (delete the install
folder), not a default.
