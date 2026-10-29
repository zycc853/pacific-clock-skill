# Recovery and implementation notes

## Clock is running but not visible

The first check is **which desktop launched it**, not the process count. Run `scripts/start.ps1`: it returns desktop names and `requires-interactive-launch` when the shell is isolated from the user's desktop. Use the native app-launch capability on `appPath` for normal startup. Floating mode has no taskbar button and may not expose a targetable window to the launch tool. Use `finderPath` only for actual recovery, because it changes the display mode. A cloud host cannot launch a GUI on the user's local machine.

The finder returns immediately after sending a request to the clock. A launcher may therefore say that the finder has no targetable window. Look for the child/existing **PacificClock.exe** window, titled `太平洋时钟 · Pacific Clock`.

`FindPacificClock.exe` restores a minimized/hidden window, disables click-through, centers it, and enables a taskbar button. Select **恢复悬浮模式** afterward to hide that button again. Fresh installs default to compact floating mode. The tray icon may be inside Windows' hidden-icons area; its placement is controlled by Windows.

## Settings and updates

`PacificClock.settings.xml` lives beside the installed EXE and is user-specific. Do not distribute another user's copy. Exit the current instance before replacing locked binaries. In some sandboxes, `Get-Process.Path` is unavailable for the real-desktop process; do not assume a path-filtered stop succeeded. Use the app's Exit menu or an appropriately authorized exact-process operation.

For a fresh copy, `prepare.ps1 -DestinationPath <new-directory>` copies only the two executable files. A failed copy leaves the user's settings intact. Running `prepare.ps1` again with identical binaries is a no-op for those files.

## Build and verification

Run `scripts/build.ps1 -OutputDirectory <build-directory>` then `scripts/verify.ps1 -ExecutablePath <build-directory>\PacificClock.exe`. Verification has a 30-second timeout. The app's 27 checks cover PST/PDT offsets, spring and autumn boundaries, midnight rollover, placement, settings serialization, mouse passthrough, ordinary/floating window styles, resizing, live button painting and fresh-install defaults. Offscreen preview generation is available as `PacificClock.exe --preview <absolute-png-path>`.

Always keep the UTC instant when determining daylight status. Converting to a local 01:xx first makes the autumn repeated hour ambiguous. The clock uses `TimeZoneInfo.IsDaylightSavingTime(DateTimeOffset)` for its PDT/PST label.

## Platform

This implementation is Windows-only and uses built-in .NET Framework and system fonts. Do not tell macOS/Linux users that this executable works natively. On those hosts, explain the limit or treat a native port as a separate requested task.
