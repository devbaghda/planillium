# SPEC -- G7 scratch-run safety (agentic side, 2026-10-27)

Planner spec. Code-only research (no secret store, data/, config.json, plans/ or live exe read).
Per the 2026-10-27 BRIEF addendum: build + existing suite as precheck, no QA, no new tests.

## 1. Requirement (in my words)

When the app is started with the `MENTOR_ROOT` environment variable set (a test copy on scratch data), it must
not read, write or delete any real secret in Windows Credential Manager (the real AI key, the TickTick client
secret, access token, refresh token), and must not contact api.anthropic.com, ticktick.com or api.ticktick.com
(including opening the TickTick sign-in page in the browser). The local AI stub (`PLANILLIUM_AI_BASE_URL`, with
its separate test key slot `anthropic_api_key_test`) keeps working. Without `MENTOR_ROOT` nothing changes.

## 2. Where secrets are read and where services are called (code evidence)

Single secret store: `Services/CredentialStore.cs` -- `Read` (CredRead), `Write` (CredWriteW), `Delete` (CredDeleteW).
Nothing else in `Planillium.App/` calls `CredRead`/`CredWriteW`/`CredDeleteW`, `PasswordVault` or DPAPI (grep).

| # | Place | Secret / service | Reaches it through |
|---|---|---|---|
| S1 | `AiKey.Read` / `Has` (`AiKey.cs:14,16`) | AI key, name from `AiSettings.CredentialKey` (`anthropic_api_key`, or `anthropic_api_key_test` when the stub override is active) | `CredentialStore.Read` |
| S2 | `AiKey.Save` / `Remove` (`AiKey.cs:20-27`) | AI key write/delete | `CredentialStore.Write/Delete` |
| S3 | `TickTickAuth.ClientSecret` / `IsConfigured` (`TickTickAuth.cs:60-62`) | `ticktick_client_secret` | `CredentialStore.Read` |
| S4 | `TickTickAuth.SaveClientSecret` (`:64`) | client secret write | `CredentialStore.Write` |
| S5 | `TickTickAuth.Disconnect` (`:72`), called by Settings "Disconnect" and "Clear all my data" (`SettingsPage.xaml.cs:1124,1544`) | DELETES all three real TickTick credentials | `CredentialStore.Delete` |
| S6 | `TickTickAuth.RefreshAsync` (`:266`) | `ticktick_refresh_token` | `CredentialStore.Read` |
| S7 | `TickTickAuth.TokenRequestAsync` (`:287,314,316`) | client secret read; access/refresh token write | `CredentialStore.Read/Write` |
| S8 | `TickTickService.AccessToken` / `IsAuthorized` (`TickTickService.cs:21-22`), read on every Today render (`TickTickSection.cs:37`) and Settings (`SettingsPage.xaml.cs:1041-1051`) | `ticktick_access_token` | `CredentialStore.Read` |
| N1 | `AiClient.AskAsync` (`AiClient.cs:128`) -> `AiSettings.BaseUrl()` (`api.anthropic.com` unless stub) | Anthropic | sends only if the key provider (`AiKey.Read`, S1) returns a key; `NoKey` otherwise (`AiClient.cs:130-131`) |
| N2 | `TickTickService.SendAsync` (`:32`) -> `api.ticktick.com` (TasksDueToday, CompleteTask) | TickTick API | needs `AccessToken` (S8), else throws "No TickTick token." before any send; callers reached only when `IsAuthorized` |
| N3 | `TickTickAuth.TokenRequestAsync` -> `ticktick.com/oauth/token` | TickTick OAuth | reached only from `AuthorizeAsync`/`RefreshAsync`, both gated on `IsConfigured` (S3) / refresh token (S6) |
| N4 | `TickTickAuth.AuthorizeAsync` -> `Process.Start(ticktick.com/oauth/authorize)` (browser) | TickTick sign-in page | gated on `IsConfigured` (`:89`), i.e. on S3 |

Not secrets / not external (justified, no change): `TickTickAuth` loopback `TcpListener` on 127.0.0.1:8765 (local only,
and only after N4's gate); `DataExport.cs:28`, `ReportExport.cs:138,217` `Process.Start` open a local exported file;
`ConfigService.TickTickClientId` is a non-secret id read from the scratch copy of config.json; `ThemeSync`,
`PlanTemplates`, `ReportsPage.Diary`, `AddPlanDialog`, `AiAskControls` matched the grep only on `CancellationToken`
or text. No other `HttpClient`, socket, `WebView`, `Launcher` or URL in `Planillium.App/` (grep of `*.cs, *.xaml,
*.csproj, *.manifest`).

Every N-row depends on an S-row, and every S-row goes through `CredentialStore`. So one guard there covers all of them.

## 3. Chosen approach

**3.1 Scratch condition -- one definition.** In `Services/AppPaths.cs` (already source-linked into the tests) add:

```csharp
/// <summary>True when MENTOR_ROOT is set (a test copy on scratch data). Not #if-gated, on purpose.</summary>
public static bool IsScratchRun => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MENTOR_ROOT"));
```

Keyed only on `MENTOR_ROOT` being non-empty. It is **not** wrapped in `#if DEBUG || PLANILLIUM_TESTS` and does **not**
require the folder to exist: if a Release build, or a mistyped path, makes the app ignore `MENTOR_ROOT` and fall back to
the walk-up folder search, the secrets stay blocked anyway (fail-closed). A run without `MENTOR_ROOT` gives `false`, so
everything below is skipped (acceptance 3). Read on each call, not cached (cheap; no stale value).

**3.2 The guard -- inside `CredentialStore`, before the Win32 call.** In `Services/CredentialStore.cs`:

```csharp
/// <summary>In a scratch run only the stub's test key slot may be touched; real secrets never are.</summary>
private static bool Blocked(string key) => AppPaths.IsScratchRun && key != AiSettings.TestCredentialName;
```

- `Read`: first line `if (Blocked(key)) return null;` -- `CredRead` is never called for a real secret.
- `Write`: first line `if (Blocked(key)) { Log.Info($"CredentialStore.Write({key}) refused: scratch run (MENTOR_ROOT set)"); return false; }`
  (the key NAME only, never a value; same shape as the existing `Log.Warn` there).
- `Delete`: first line `if (Blocked(key)) return;` -- a scratch "Disconnect" or "Clear all my data" can no longer delete
  the user's real TickTick credentials (S5) or a real AI key (S2).

The allow-list is the single name `AiSettings.TestCredentialName` (`anthropic_api_key_test`), which by DECISIONS rule 16
only ever holds QA's fake key. `AiSettings.CredentialKey` returns it only when the stub override is active, so:
scratch + stub -> test slot, stub URL (unchanged behaviour); scratch without stub -> name is `anthropic_api_key` ->
blocked -> no key -> `AiClient.AskAsync` returns `NoKey` before building a request (N1 never sends).

**3.3 Clear messages (acceptance 2).** One text per service, defined once next to its owner:

- `AiSettings` (`AiClient.cs`): `public static bool ScratchBlocked => AppPaths.IsScratchRun && !TestOverrideActive;` and
  `public const string ScratchBlockedMessage = "AI is off in this test copy (MENTOR_ROOT is set). Set PLANILLIUM_AI_BASE_URL to a local stub to use it.";`
- `TickTickAuth`: `internal const string ScratchBlockedMessage = "TickTick is off in this test copy (MENTOR_ROOT is set). Nothing was saved or sent.";`

Shown at:
1. `Dialogs/AiAskControls.cs` `ShowHint()`: `SetReason(AiSettings.ScratchBlocked ? AiSettings.ScratchBlockedMessage : NoKeyHint, failure: false);`
   (covers Replace remaining tasks and Add plan; the Ask button stays hidden because `Has()` is false).
2. `Pages/SettingsPage.xaml.cs` `SaveAi()`: before `AiKey.Save(key)` (key non-empty branch) add
   `if (AiSettings.ScratchBlocked) { SetAiStatus("Not saved: " + AiSettings.ScratchBlockedMessage, failure: true); return; }`
   (instead of the misleading "Windows could not store the key"). Model-only save path unchanged.
3. `Dialogs/TickTickConnectDialog.cs` PrimaryButtonClick: first statement after `args.Cancel = true;`
   `if (AppPaths.IsScratchRun) { status.Text = TickTickAuth.ScratchBlockedMessage; return; }`
   -- before the client id is written to config and before `SaveClientSecret`/`AuthorizeAsync`; no browser opens.
   (Without it the user would see "Both fields are required." or "Couldn't save the secret to Windows Credential Manager.")

Where nothing new is needed: Today's TickTick section and Settings show "Not connected." (true for the test copy);
Disconnect is hidden because `IsAuthorized` is false; `RefreshAsync` returns false; Test connection / Remove are disabled
because `Has()` is false. No crash or hang path: every guarded call returns null/false/no-op synchronously.

**Rejected alternative:** guard at each consumer (AiKey, TickTickAuth, TickTickService, SettingsPage) or after the
read (e.g. block the HTTP send). Rejected: eight read/write/delete sites plus future ones would each need the check,
a missed one leaks (the B1f "fix one spot, sibling still broken" class), and a guard after the read still pulls the real
secret into memory. A second rejected option: gate on `#if DEBUG` -- DECISIONS standing lesson ("A #if DEBUG-gated
test-only safety hook is only as safe as tests always build Debug") says gate on the runtime condition instead.

## 4. Files to change (exactly)

1. `Planillium.App/Services/AppPaths.cs` -- add `IsScratchRun` (3.1).
2. `Planillium.App/Services/CredentialStore.cs` -- add `Blocked`, guard first line of `Read`, `Write`, `Delete` (3.2).
3. `Planillium.App/Services/AiClient.cs` -- `AiSettings.ScratchBlocked`, `AiSettings.ScratchBlockedMessage` (3.3).
4. `Planillium.App/Services/TickTickAuth.cs` -- `ScratchBlockedMessage` const (3.3).
5. `Planillium.App/Dialogs/AiAskControls.cs` -- `ShowHint` text choice (3.3.1).
6. `Planillium.App/Pages/SettingsPage.xaml.cs` -- `SaveAi` scratch refusal (3.3.2).
7. `Planillium.App/Dialogs/TickTickConnectDialog.cs` -- scratch refusal at Save & authorize (3.3.3).

No change to `Planillium.App.Tests/` (csproj already links `AppPaths.cs` and `AiClient.cs`; `CredentialStore.cs`,
`AiKey.cs`, TickTick files and dialogs are not linked). Docs in the same pass: `DECISIONS.md` "Safety around real
data" one bullet (rule: a MENTOR_ROOT run never touches real Credential Manager entries; only the AI test slot);
rule 16 sentence unchanged; `context/todos.md` dated entry; `CONTEXT.md` section 2 line; `CHANGELOG.md` Unreleased.

## 5. Edge cases

- `MENTOR_ROOT` set, stub set: AI uses the test slot and the stub exactly as today; TickTick blocked.
- `MENTOR_ROOT` set, stub not set: AI key never read; Settings Save says the scratch message; dialogs show it as the hint; no request built.
- `MENTOR_ROOT` set to a missing folder (app falls back to the walk-up search) or Release build with `MENTOR_ROOT` set: still blocked (fail-closed).
- `MENTOR_ROOT` set to empty string: counts as not set (matches AppPaths, which also ignores empty).
- Scratch "Clear all my data" / "Disconnect TickTick": real credentials untouched (Delete no-op); DB/log/export clearing acts on the scratch root as before.
- Legacy target names (`...@MentorOverseer`): guarded by the same `Blocked(key)` check, since it runs before the target loop.
- Normal run (no `MENTOR_ROOT`): `Blocked` is false, no new message path is taken, code path identical to today.
- Tests: `TestRootFixture` sets `MENTOR_ROOT`; no linked test code reaches `CredentialStore`, and `AiClientTests` inject their own key provider and base URL, so existing tests are unaffected.

## 6. Rules applied (side memory)

- DECISIONS rule 16 (AI key only in Credential Manager; test slot only with the stub): kept; the allow-list is exactly that test slot.
- DECISIONS standing lesson "#if DEBUG-gated safety hook": guard is runtime, not compile-time (3.1).
- DECISIONS "Safety around real data" / scratch MENTOR_ROOT technique: this feature makes that technique safe for secrets; new bullet added there.
- DECISIONS / LESSONS "Check every sibling" and "fix must cover the whole area": guard at the single choke point covers every read, write and delete (table section 2), incl. the delete paths that were not in the request but would destroy real secrets.
- LESSONS "grep all callers before ...": all callers of `CredentialStore`, `AiKey`, `TickTickAuth`, `TickTickService` listed in section 2.
- CONTEXT section 4 (repo public: no secrets): log line writes the key name only.

## 7. Acceptance (verbatim from ACCEPTANCE.md)

1. Under a scratch MENTOR_ROOT the app reads no real AI key and no real TickTick credential/token (show where each is
   read and the guard; code evidence is enough, a live read of the real secret store is NOT allowed).
2. Under a scratch MENTOR_ROOT, AI calls go only to PLANILLIUM_AI_BASE_URL (stub) or fail cleanly with a clear message
   when it is unset; TickTick calls are refused cleanly. No crash, no hang.
3. A run without MENTOR_ROOT is unchanged (code evidence: guard keyed only on the scratch condition).
4. Sibling check: every other place that reads a secret or calls an external service is listed and covered or
   explicitly justified.
5. Build clean (/warnaserror); existing tests pass.

(Planner's own, labelled as mine) P1. Under a scratch MENTOR_ROOT, "Disconnect TickTick", "Clear all my data" and
"Remove key" never delete a real Credential Manager entry (code evidence: `CredentialStore.Delete` guard).

## 8. Open questions

1. **Start with Windows (out of scope, flagged):** `StartupService` writes the real `HKCU\...\Run` value; toggling it in a
   scratch copy would point the user's real autostart at the Debug exe. Not a secret or an external service, so not
   covered here. Extend the scratch guard to it? (Needs a user decision.)
2. Should the Today page TickTick section / Settings TickTick status say "off in this test copy" instead of "Not connected."?
   Spec keeps "Not connected." (no feature change asked); the clear refusal appears when Connect is tried.
