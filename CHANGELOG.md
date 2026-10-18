# Changelog

All notable changes to WindowMinimize. Versions follow SemVer.

## Unreleased

### Added

- A language selector in the settings dialog: follow Windows, English, or Chinese. Changing it
  rebuilds the tray menu immediately and is stored as `Language` in `hotkey.ini`.

### Changed

- Removed the "Minimize current window" tray entry. Clicking the tray moves the foreground to the
  shell, so the entry could not know which window to minimize; the hotkey remains the trigger.

## 1.1.0 - 2026-10-18

A feature release centered on the settings dialog, a clearer tray menu and elevated startup.

### Added

- Left-clicking the tray icon opens a settings dialog for the hotkey, the mode, auto start and the
  debug log.
- The auto start setting is a master switch. Once it is on, "Start with Windows as administrator"
  becomes available and switches the logon start to a silent elevated Task Scheduler task.
- "Restart as administrator" in the tray menu and in the settings dialog. The version heading shows
  whether the running copy is elevated.
- Alt accelerators for the settings controls: K hotkey, M/H mode, A/E auto start, D debug, S save,
  C cancel and R restart.
- `start-admin.bat` for a one-off elevated start.

### Changed

- The tray menu groups its commands into actions, hotkey, settings, config, startup and exit.
  Config and startup items stay on the first level, separated from the other groups.
- A single left click opens the settings dialog; double-click no longer minimizes a window.
- Settings changed in the dialog are written back to `hotkey.ini`, including the auto start choice
  and the debug setting.
- The settings dialog uses an auto-sizing layout with top-aligned labels and explicit hint line
  breaks, so longer translations are not clipped.
- Usage examples use `Win+S` instead of `Ctrl+Alt+M`.

### Fixed

- Double-clicking the tray icon could minimize the Windows 11 tray overflow window, after which the
  "^" button no longer showed the panel.
- The settings dialog could be clipped with longer labels, and the debug option had no left label.
- Help and argument errors no longer create `hotkey.ini`, and the usage text names the actual exe,
  including the lite build.
- `Win` plus a letter, such as `Win+S`, parses and canonicalizes correctly; a lone modifier is
  rejected.
- Cancelling the UAC prompt for the elevated auto start no longer shows a spurious error.
- A window that refuses `ShowWindowAsync` is retried with `WM_SYSCOMMAND`/`SC_MINIMIZE`, so elevated
  targets can still be minimized once this program runs elevated.

### Build

- `WindowMinimize.exe`: self-contained single file for Windows 10/11 x64.
- `WindowMinimize-lite.exe`: framework-dependent single file, needs the .NET 8 Desktop Runtime.
