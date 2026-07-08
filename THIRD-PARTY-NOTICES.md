# Third-Party Notices

Planillium (`winui/MentorOverseer.App`) is built on the open-source and Microsoft-provided
packages below. Versions match `winui/MentorOverseer.App/packages.lock.json` — that lock
file is the authoritative source if this list ever drifts from it.

This is an engineering-level inventory, not a legal opinion — verify current license text
on each package's NuGet/GitHub page before relying on this for a commercial or compliance
purpose.

## Direct dependencies

| Package | Version | License | Notes |
|---|---|---|---|
| [Microsoft.WindowsAppSDK](https://www.nuget.org/packages/Microsoft.WindowsAppSDK) | 1.5.240627000 | MIT | WinUI 3 framework this app is built on |
| [Microsoft.Windows.SDK.BuildTools](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools) | 10.0.26100.1742 | MS-EULA (build-time only) | Compile-time tooling; not redistributed inside the app itself |
| [Microsoft.Data.Sqlite](https://www.nuget.org/packages/Microsoft.Data.Sqlite) | 8.0.6 | MIT | SQLite ADO.NET provider (part of dotnet/efcore) |
| [H.NotifyIcon.WinUI](https://www.nuget.org/packages/H.NotifyIcon.WinUI) | 2.1.3 | MIT | System tray icon |

## Transitive dependencies

| Package | License |
|---|---|
| H.NotifyIcon, H.GeneratedIcons.System.Drawing | MIT |
| Microsoft.Data.Sqlite.Core | MIT |
| SQLitePCLRaw.core / .bundle_e_sqlite3 / .lib.e_sqlite3 / .provider.e_sqlite3 | Apache-2.0 |
| SQLite (native engine, bundled via SQLitePCLRaw) | Public domain |
| System.Drawing.Common, System.Memory, Microsoft.Win32.SystemEvents | MIT (.NET runtime libraries) |

All licenses above are permissive (MIT/Apache-2.0/public domain) — no copyleft (GPL/AGPL)
obligations apply to this app.
