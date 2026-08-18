# SQLite vendor layout

This directory contains vendored SQLite dependencies used by `ee4v` without requiring end users to install additional packages.

## Current versions

- `sqlite-net-base` `1.9.172`
- `SQLitePCLRaw.core` `2.1.11`
- `SQLitePCLRaw.provider.e_sqlite3` `2.1.11`
- `SourceGear.sqlite3` `3.50.4.5`

## Files committed to this package

- `Managed/SQLite-net.dll`
- `Managed/SQLitePCLRaw.core.dll`
- `Managed/SQLitePCLRaw.provider.e_sqlite3.dll`
- `Native/Windows/x86_64/e_sqlite3.dll`
- `licenses/sqlite-net/*`
- `licenses/sqlitepclraw/*`
- `licenses/sourcegear-sqlite/*`

## Update procedure

1. Download these NuGet packages:
   - `sqlite-net-base`
   - `SQLitePCLRaw.core`
   - `SQLitePCLRaw.provider.e_sqlite3`
   - `SourceGear.sqlite3`
2. Extract the following files:
   - `sqlite-net-base/lib/netstandard2.0/SQLite-net.dll`
   - `SQLitePCLRaw.core/lib/netstandard2.0/SQLitePCLRaw.core.dll`
   - `SQLitePCLRaw.provider.e_sqlite3/lib/netstandard2.0/SQLitePCLRaw.provider.e_sqlite3.dll`
   - `SourceGear.sqlite3/runtimes/win-x64/native/e_sqlite3.dll`
3. Refresh the license subtree:
   - `sqlite-net-base/LICENSE.txt`
   - `sqlite-net-base.nuspec`
   - `SQLitePCLRaw.core.nuspec`
   - `SQLitePCLRaw.provider.e_sqlite3.nuspec`
   - Apache 2.0 license text
   - `SourceGear.sqlite3/LICENSE.txt`
   - `SourceGear.sqlite3/README.md`
   - `SourceGear.sqlite3.nuspec`
4. Keep the Unity plugin metadata aligned:
   - managed DLLs stay `Editor` only with auto reference enabled
   - native DLL stays `Editor + Windows + x86_64` only
5. Update `Third Party Notices.md` when versions change.
