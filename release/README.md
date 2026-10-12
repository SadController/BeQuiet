# BeQuiet 1.2.0

From a repository download, keep all numbered ZIP parts together and double-click `Extract-release.cmd`. It verifies checksums, joins the archive, and extracts it to `BeQuiet-1.2.0-win-x64/`. Run `BeQuiet.exe` from the extracted folder. This portable release includes the .NET runtime.

If you already have an extracted release or a local build, run `BeQuiet.exe` directly.

1. In the main window, check the apps you want to add to **Favorites**. Play audio in a missing app, then click **Refresh**.
2. Left-click or right-click the BeQuiet tray icon.
3. Check the apps that should keep their original volume. Focus starts immediately and turns down other apps.
4. Select several apps if needed. Unchecking the last focused app turns focus off and restores the original volume levels.

The first tray-menu item and the hotkey (default: **Ctrl+Q**) toggle focus for the selected apps. This toggle keeps the focus selection for next time.

Favorites and the selected focus apps are saved between launches. Favorites remain listed even when they have no audio session. Focus is off when BeQuiet starts.

**Duck Level** is a percentage of each background app's original volume. With a 20% duck level, an app originally at 60% is reduced to 12%.

All application controls, menus, tooltips, and status messages are in English.

Tested on Windows 10 Enterprise LTSC 2021, build 19044, x64. Based on BeQuiet 1.1.0 (MIT).
