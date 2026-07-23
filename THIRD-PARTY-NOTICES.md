# Third-Party Notices

Planillium (`winui/Planillium.App`) is built on the open-source and Microsoft-provided
packages below. Versions match `winui/Planillium.App/packages.lock.json` — that lock
file is the authoritative source if this list ever drifts from it.

This is an engineering-level inventory, not a legal opinion — verify current license text
on each package's NuGet/GitHub page before relying on this for a commercial or compliance
purpose.

## Direct dependencies

| Package | Version | License | Notes |
|---|---|---|---|
| [Microsoft.WindowsAppSDK](https://www.nuget.org/packages/Microsoft.WindowsAppSDK) | 1.8.260529003 | MIT | WinUI 3 framework this app is built on |
| [Microsoft.Windows.SDK.BuildTools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools) | 10.0.26100.4654 | MS-EULA (build-time only) | Compile-time tooling; not redistributed inside the app itself |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) | 8.0.6 | MIT | SQLite ADO.NET provider (part of dotnet/efcore) |
| [SQLitePCLRaw.bundle_e_sqlite3](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3) | 3.0.3 | Apache-2.0 | Pinned directly over Microsoft.Data.Sqlite's older transitive version to carry a patched SQLite engine (CVE-2025-6965) |
| [H.NotifyIcon.WinUI](https://www.nuget.org/packages/H.NotifyIcon.WinUI) | 2.1.3 | MIT | System tray icon |

## Transitive dependencies

Regenerated directly against `packages.lock.json` (2026-07-23 audit finding #19 — this
table had drifted from its own declared source of truth: a few entries were missing
outright and one native-engine package was misnamed).

| Package | License |
|---|---|
| H.NotifyIcon, H.GeneratedIcons.System.Drawing | MIT |
| Microsoft.Data.Sqlite.Core | MIT |
| SQLitePCLRaw.core / .config.e_sqlite3 / .provider.e_sqlite3 | Apache-2.0 |
| SourceGear.sqlite3 (native SQLite engine, bundled via SQLitePCLRaw) | Public domain |
| System.Drawing.Common, System.Numerics.Tensors, Microsoft.Win32.SystemEvents | MIT (.NET runtime libraries) |
| Microsoft.Windows.SDK.BuildTools.MSIX | MS-EULA (build-time only, pulled in transitively via Microsoft.WindowsAppSDK.Base) |
| Microsoft.WindowsAppSDK.AI / .Base / .DWrite / .Foundation / .InteractiveExperiences / .ML / .Runtime / .Widgets / .WinUI | MIT (sub-packages of Microsoft.WindowsAppSDK, listed as a direct dependency above) |
| Microsoft.Web.WebView2 (WebView2Loader.dll) | Microsoft Edge WebView2 SDK license (not MIT) — pulled in transitively via Microsoft.WindowsAppSDK.WinUI; confirmed physically shipped in the Release build output (round-5 audit finding #14, previously undocumented here) |

Everything above is permissive (MIT/Apache-2.0/public domain) with one exception —
Microsoft.Web.WebView2 ships under Microsoft's own WebView2/Edge redistribution terms, not
MIT/Apache-2.0. Nothing here carries copyleft (GPL/AGPL) obligations.
