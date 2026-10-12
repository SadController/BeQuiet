# BeQuiet 1.2.0

A portable Windows application: save apps as favorites, then use the tray menu to choose which apps keep their volume. All other apps are turned down. The interface and all files in this package are in English.

## Contents

- `source/` - modified C# code, .NET project, and interface resources.
- `release/` - portable release ZIP parts, extraction scripts, instructions, licenses, and SHA-256 checksums.
- `tests/` - integration checks using silent audio sessions.
- `build.ps1` - builds the portable release with the .NET SDK.
- `CHANGELOG.md` - changes in version 1.2.0.

You can copy this entire folder into a Git repository. Building does not require files from the surrounding workspace. `.gitignore` excludes temporary build files; `release/` is included in the commit.

## Run

Download the repository ZIP and extract it, then double-click `release/Extract-release.cmd`. This joins the release archive parts and extracts them to `release/BeQuiet-1.2.0-win-x64/`. Run `BeQuiet.exe` from that folder. Choose favorites in the main window, click the BeQuiet tray icon, and select apps to focus on. Clearing the last selection restores the previous volume levels. The default hotkey is **Ctrl+Q**.

The runtime is already compressed, so the release archive is split into numbered parts to fit the GitHub API connection's upload limit. The extraction script verifies checksums; the executable is identical to the tested release.

Tested on Windows 10 Enterprise LTSC 2021 (19044), x64. The release includes .NET 9.0.4; no separate runtime installation is required.

## Build

Install the .NET 9 SDK or a newer SDK for Windows. The first build requires access to NuGet to download NAudio 2.3.0.

```powershell
.\build.ps1
```

The executable, portable ZIP, numbered archive parts, and updated checksums are written to `release/`. The repository tracks the archive parts; the loose executable, joined ZIP, and extracted files are excluded from Git.

```powershell
.\tests\run.ps1
```

Tests use separate settings in `build/test-settings` and stop the processes they create. Existing applications' original volume levels are preserved during testing.

## Code provenance

Based on BeQuiet 1.1.0 by Doctordefector (MIT). The public archive contained only a README and license, so the C# code was recovered from the official executable using ILSpy and then modified. Original WPF templates are preserved in `source/Resources/BeQuiet.g.resources`; the original XAML files are unavailable. The application can be built from the C# files and resources included here.

The supplied release was compiled with Roslyn and packaged with the original application's runtime. A conventional `.csproj` build was not run in this environment because the .NET SDK is not installed. See `VALIDATION.md` for the checks performed.

NAudio uses the MIT license: [project](https://github.com/naudio/NAudio), [license](https://github.com/naudio/NAudio/blob/master/license.txt). The BeQuiet license is in `LICENSE`.

Suggested commit message: `Release 1.2.0: favorites and tray focus selection`.
