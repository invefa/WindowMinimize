# Changelog

All notable changes to WindowMinimize. Versions follow SemVer.

## 1.2.2 - 2026-10-19

A fix release for hidden-window tracking.

### Fixed

- The `hide` mode now tracks every hidden window and lists them in a "Hidden windows" tray
  submenu. Each entry restores one window, and "Restore all hidden windows" restores the list.

## 1.2.1 - 2026-10-19

A small UI fix release for the 1.2.0 language and notification settings.

### Fixed

- English builds clipped the third notification toggle. The labels are now Startup, Errors and
  Permissions, and all three fit on one row.
- The language list always shows English and 简体中文 in their own names, regardless of the
  current interface language.

## 1.2.0 - 2026-10-19

A focused update for localization, notification control and tray menu cleanup.

### Added

- A language selector in the settings dialog: follow Windows, English, or Chinese. Changing it
  rebuilds the tray menu immediately and is stored as `Language` in `hotkey.ini`.
- Three independent notification switches in the settings dialog: startup, errors and permissions,
  stored as `NotifyStartup`, `NotifyErrors` and `NotifyPermissions` in `hotkey.ini`; turning one off
  suppresses only that kind of tray balloon while keeping the log messages.
- A Chinese README, `README.zh-Hans.md`, linked from the English one.

### Changed

- Removed the "Minimize current window" tray entry. Clicking the tray moves the foreground to the
  shell, so the entry could not know which window to minimize; the hotkey remains the trigger.
- Notification balloons now use a short first line and a detail line. The parenthetical takeover
  notes are gone, and the three notification toggles share one compact row.

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
