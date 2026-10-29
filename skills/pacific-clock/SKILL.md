---
name: pacific-clock
description: Set up and open a lightweight native Windows desktop clock for US Pacific time, with automatic PST/PDT switching, Paper/Graphite themes, compact mode, and tray recovery. Use when the user wants a glanceable Pacific-time clock or asks to open, recover, or customize this widget. Windows only; not for changing the computer's system timezone.
---

# Pacific Clock

Provide the bundled native desktop clock. It uses Windows `Pacific Standard Time` rules for Los Angeles and automatically handles daylight saving time. No browser, Python, Node.js, or network connection is needed by the clock itself.

## Prepare and open

Resolve paths relative to this skill directory; never use a path from the author's machine.

1. Confirm the execution host is Windows and is the user's intended computer. A remote/cloud execution host cannot display a widget on another computer; offer the portable download in that case.
2. Run `scripts/prepare.ps1`. It copies the bundled executables into `%LOCALAPPDATA%\PacificClock` and returns `appPath` and `finderPath`. Supply `-DestinationPath` only when a different location is requested or needed. Existing settings are preserved.
3. Open the prepared `PacificClock.exe` on the user's **interactive desktop**. Prefer a native app-launch tool supplied by the environment, using the exact returned path. In a regular interactive PowerShell session, `scripts/start.ps1` prepares and launches it.
4. Verify a returned application window or actual visible state. A running process, `WS_VISIBLE`, or exit code alone does not prove the user can see the window.

### Sandboxed or hidden desktops

Some Windows agent shells run on a desktop such as `CodexSandboxDesktop-*` while the user sees `Default`. A GUI started there can run successfully and remain invisible to the user.

`scripts/start.ps1` detects this difference. When its result says `requires-interactive-launch`, use the environment's native desktop launcher with `finderPath`; do not keep relaunching from the same shell. `FindPacificClock.exe` opens or restores the widget, switches it to a discoverable taskbar mode, and centers it on the mouse's monitor. It exits quickly, so the launcher may report no window for the finder itself: verify the resulting **PacificClock.exe** window instead.

If no interactive launch capability is available, give the user the prepared `finderPath` to double-click and state that the files are ready, rather than claiming it is visible.

## Use and customize

- Drag the middle of the clock to move it. Right-click, or use the full card's `···` button, for settings.
- **缩放比例** offers 50–200% presets. Drag any edge or corner to resize continuously while keeping the aspect ratio. Size and position are saved. Disable mouse click-through before dragging.
- **外观主题** switches between warm Paper and dark Graphite. **精简模式** uses a small pill.
- **恢复悬浮模式** hides the taskbar button and avoids taking keyboard focus. **普通窗口模式** makes the widget discoverable in the taskbar again.
- Mouse click-through can be disabled from the tray menu or by running `FindPacificClock.exe`.
- Startup is opt-in. Do not enable it unless the user asks or selects it themselves. No system timezone change is needed.

The settings XML lives beside the deployed executable. Do not replace it when updating binaries. If files are locked, explain that the existing clock must exit before replacement; do not stop unrelated processes or silently reset preferences.

For display/recovery issues, read [references/troubleshooting.md](references/troubleshooting.md). For requested source changes, edit `assets/source/`, build with `scripts/build.ps1`, and verify with `scripts/verify.ps1` before replacing a user's running copy. Preserve UTC-based DST detection; the repeated autumn local hour is ambiguous.

## Supported environment

Windows 10/11 with .NET Framework 4.x. The packaged `.exe` files are ready to run. Rebuilding uses the Windows .NET Framework C# compiler; no .NET SDK is required. The app reads the system clock and Windows timezone database, so it relies on an accurate system clock and current Windows timezone rules.
