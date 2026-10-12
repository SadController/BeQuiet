# Release validation

Date: 2026-10-11.

Environment: Windows 10 Enterprise LTSC 2021, 21H2, build 19044, x64.

## Build

The release was rebuilt from the C# files in `source/` with the resources in `source/Resources/`. Roslyn reported no errors or warnings. The self-contained executable retains the original .NET 9.0.4 runtime. Managed file/product metadata and the executable's Windows version resource identify the release as **1.2.0**.

## Tests

**36 / 36 integration checks passed.** Tests use isolated settings and silent audio sessions. They cover settings migration, favorites independent of focus, real WPF checkbox bindings and events, tray-menu item events, multiple focused apps, focus changes while ducking, new sessions, no cumulative ducking, exact volume restoration, persistence, and English-only labels, tooltips, buttons, and status messages. Temporary audio processes were stopped.

The suite creates a dedicated protected audio session so it works when no user audio applications are open.

## Packaging

- The C# project references resources within `source/Resources/` and NAudio 2.3.0 NuGet packages. It has no dependency on files outside this package.
- The resource file, icon, scripts, tests, licenses, changelog, and numbered portable release ZIP parts are included. The archive contains the validated executable. An extraction script joins the parts and verifies checksums.
- Build output and temporary helpers are ignored by Git. `release/` is tracked.
- The .NET bundle's embedded application matches the compiled 1.2.0 assembly.
- `release/SHA256SUMS.txt` records checksums for the executable, joined ZIP, and numbered parts. The extracted executable's checksum matches the validated release.

## Limits

The conventional SDK build and SDK-based test script were not run here because the .NET SDK is not installed. C# compilation and the integration checks used Roslyn and the original embedded runtime.

Automated physical interaction with the tray icon was not checked. The menu and its selection events were exercised by the integration suite. Screenshot capture was unavailable in the earlier UI inspection.
