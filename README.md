# WindowMinimize

A small Windows tool that minimizes the current foreground window with a custom global hotkey. It
lives in the tray, its default hotkey is `Win+S`, it is a single-file exe, and it depends on neither
the .NET runtime nor any third-party component.

Download `WindowMinimize.exe` from the
[releases page](https://github.com/invefa/WindowMinimize/releases/latest) and run it, no
installation needed. The `WindowMinimize-lite.exe` on the same page is about 200 KB, but it needs
the .NET 8 Desktop Runtime on the machine. Both can be built from source too (section 8).

## 1. Usage

Double-click `start.bat` or run `dist\WindowMinimize.exe` directly. After startup the program stays
in the taskbar notification area; press the hotkey once to minimize the foreground window at that
moment.

For windows that run as administrator, start the program with `start-admin.bat` or use "Restart as
administrator" in the tray menu; Windows does not deliver the hotkey to a normal-privilege program
while such a window is in front. Tick "Start with Windows as administrator" to start it elevated at
every logon without a UAC prompt.

The first run creates `hotkey.ini` next to the exe. Save your changes and they take effect
immediately, with no restart. The tray menu entry "Settings" changes the hotkey, the mode and the
normal auto start without editing the file.

To exit: right-click the tray icon and choose "Exit".

## 2. Hotkey Syntax

Join modifiers with `+` and put the main key last; the order does not change the result. Use `+` or
`-` as the separator.

| Part | Values |
| --- | --- |
| Modifier | `Ctrl` (also `Control`), `Alt`, `Shift`, `Win` (also `Windows`) |
| Main key | `A` to `Z`, `0` to `9`, `F1` to `F24`, `Space`, `Esc`, `Enter`, `Tab`, `Backspace`, `Delete`, `Insert`, `Home`, `End`, `PageUp`, `PageDown`, the four arrow keys, `Num0` to `Num9`, `Minus`, `Plus`, and so on |

Examples: `Win+Alt+M`, `Ctrl+Shift+Q`, `Ctrl+Alt+F9`, `Alt+Space`, `F11`, `Ctrl+-`.

Without a modifier, only function keys (`F1` to `F24`) are allowed, so normal typing is not stolen.
`F12` is a system-reserved key and cannot be registered; the program rejects it outright.
System-reserved combinations such as `Alt+Tab`, `Ctrl+Alt+Del`, and `Win+L` are rejected as well.

Combinations that Windows itself occupies (for example `Win+S` to open search, or `Win+D` to show
the desktop) are not rejected. `RegisterHotKey` always fails for these, so the program takes them
over with a keyboard hook; once taken over, the original system function is no longer available.

## 3. Configuration File hotkey.ini

```ini
[general]
Hotkey=Win+S
Mode=minimize
Debug=false
```

| Setting | Default | Description |
| --- | --- | --- |
| Hotkey | Win+S | Global hotkey; see the previous section for the syntax |
| Mode | minimize | `minimize` minimizes to the taskbar; `hide` hides the window directly |
| AutoStart | not written | When set to `true` or `false`, the startup entry is synced from it at launch; delete the whole line to leave the registry alone and respect the manual setting |
| Debug | false | Whether to write the `hotkey.log` debug log |

The default configuration the program generates does not contain `AutoStart`. To start with Windows,
add the line yourself, or tick it in the tray menu.

`MinimizeToTray` in older configurations still works as an alias for `AutoStart`.

A bad configuration does not break existing behavior: if parsing fails, the last working hotkey is
kept, and the reason is reported in a tray balloon and in the log.

The configuration file lives next to the exe by default. If that directory is not writable (for
example when unpacked to `C:\Program Files`), it falls back to
`%APPDATA%\WindowMinimize\hotkey.ini`.

## 4. Tray Menu

| Menu item | Action |
| --- | --- |
| Version | Not clickable heading at the top, shows the version of the exe that is running |
| Privilege | Not clickable heading below the version, shows whether this copy runs as administrator or as a standard user |
| Minimize current window | Same as pressing the hotkey once |
| Restore last hidden window | Brings the last window hidden with `hide` back to the foreground; not clickable when no window has been hidden |
| Hotkey: xxx | Shows only the hotkey currently in effect |
| Suspend hotkey / Resume hotkey | Temporarily disables it, handy while gaming or recording; hold Shift and click to suspend for 5 minutes and then resume automatically |
| Settings | Opens the dialog for the hotkey, the mode and the normal auto start; the elevated autostart check box is shown but controlled by its own entry |
| Config > Open config file | Opens `hotkey.ini` in Notepad |
| Config > Reload config | Reads it again manually; not needed normally |
| Startup > Start with Windows | When ticked, writes the registry `HKCU\...\Run` entry; only touches the registry, not the config file |
| Startup > Start with Windows as administrator | Registers a Task Scheduler logon task with the highest run level, so the program starts elevated without a UAC prompt. Asks for administrator permission once and removes the normal Run entry; the normal entry above is disabled while the task exists |
| Startup > Restart as administrator | Closes the program and starts it again through the UAC prompt, so it can also handle windows that run elevated; not clickable when already elevated |
| Exit | Ends the program |

Double-clicking the tray icon opens the settings dialog.

## 5. Command-Line Arguments

```
WindowMinimize.exe [options]

--hotkey <combo>     Set a hotkey for this run, overriding the config file
--mode <mode>        minimize or hide
--config <path>      Use the given config file
--lang <language>    Interface language, en or zh, defaults to the system language
--debug              Write the debug log
--no-tray            Do not show the tray icon (also --quiet)
--help               Help
```

## 6. Language

The interface follows the system language: a Chinese Windows shows the Chinese text, everything
else shows English. `--lang en` or `--lang zh` overrides that for one run, to get a Chinese
interface on an English system for example.

Only what the user sees is translated: the tray menu, the balloons, the settings dialog, the usage
text and the messages about a hotkey that did not work. The source code and `hotkey.ini` stay
English.

Both languages live in `src\Resources`, `Strings.resx` for English and `Strings.zh-Hans.resx` for
Chinese. The Chinese one is published as a `zh-Hans` satellite assembly that is carried inside the
same single file, so the interface still follows the system language when the exe is copied to
another machine. The system language is read from the .NET globalization data, which Windows 10
1903 and later provide.

## 7. Implementation Notes

Hotkey registration prefers the system `RegisterHotKey`. If another program already occupies the
combination (Win32 error 1409), it automatically falls back to the low level keyboard hook
`WH_KEYBOARD_LL` to grab the key, so the hotkey always works; the tray balloon then explains how it
was taken over.

A `RegisterHotKey` registration is unique per window + id, so changing the hotkey means
unregistering the old one before registering the new one. When the new combination does not work,
the program restores the last working hotkey, so a bad config file never leaves you without a
usable hotkey.

Combinations that Windows itself occupies (for example `Win+S` to open search, or `Win+D` to show
the desktop) use the same fallback. Registration always fails for these; the balloon states that
Windows occupies it and that the original system function is no longer available after the takeover.

After the keyboard hook swallows a key, the system sees no other key while Win (or Alt) is held, so
releasing Win is treated as "Win pressed on its own" and the Start panel pops up. The program
therefore sends a mask key while swallowing the key (a tap of Ctrl, or Shift when Ctrl is already
part of the combination); the panel does not pop up whether Win or S is released first.

Minimizing takes one of three paths depending on the window type: a regular window gets
`SW_MINIMIZE`; a UWP suspended window occluded by DWM gets `WM_SYSCOMMAND`/`SC_MINIMIZE` instead;
and for the occasional self-drawn window where the first minimize does nothing, the program
rechecks after 350 milliseconds and re-sends it or brings the window to the foreground and forces a
minimize as needed.

When `ShowWindowAsync(SW_MINIMIZE)` is refused, the program sends `WM_SYSCOMMAND`/`SC_MINIMIZE`
immediately instead of waiting for the retry; some windows refuse only the first call. If neither
path works, the log records the refusal and one balloon explains that the window probably runs as
administrator, which the "Restart as administrator" entry can handle.

An elevated window in front is a separate limit: Windows does not pass its keys to the keyboard hook
of a normal-privilege program. That is why the hotkey does nothing while such a window is focused;
restarting the program elevated removes the limit.

The elevated autostart entry registers a Task Scheduler task named `WindowMinimize` at logon with the
highest run level, then removes the normal Run value, so only one copy starts. Removing the entry
unregisters the task. Both directions run through a hidden, elevated PowerShell helper and ask for
administrator permission once.

When it gets the foreground window, it skips system UI: the taskbar, the desktop, the Start menu,
search, and the program's own windows (when the tray menu is clicked, the foreground is usually the
taskbar or the menu itself). These windows are never treated as minimize targets, so system UI is
not tucked away.

## 8. Build

Requires .NET SDK 8. Run:

```
pwsh -NoProfile -File build\build.ps1
```

The output is `dist\WindowMinimize.exe`, a self-contained release that runs directly when copied to
another Windows 10/11 machine. `global.json` in the repository pins the SDK to the 8.0.403 series.
Add `-Lite` to get `dist\lite\WindowMinimize-lite.exe` instead, the framework dependent single file
that needs the .NET 8 Desktop Runtime on the machine it runs on. It lives in a folder of its own
because a single file publish clears its output folder before writing.

Pushing a tag such as `v1.0.0`, matching `<Version>` in `src\WindowMinimize.csproj`, makes
`.github\workflows\release.yml` run the unit tests, build both exe files and attach them to the
release.

Unit tests:

```
dotnet test test\unit\WindowMinimize.Tests.csproj
```

Directory layout:

```
WindowMinimize
  build        Build scripts and icon generation scripts
  src          Source code, app.ico and the string table under Resources
  test/e2e     End-to-end verification scripts; need real windows and a global hotkey
  test/unit    Unit tests for HotkeySpec parsing and for the string table
  dist         Build output (not committed)
  start.bat    Launch entry point
  start-admin.bat  Launch with administrator rights for elevated windows
```

## 9. Known Limitations

1. Windows blocks a normal-privilege program from capturing the hotkey while an elevated window is
   in front, and from minimizing that window. Run this program as administrator to handle such
   windows: use "Restart as administrator" in the tray menu or `start-admin.bat`. For a silent
   elevated start at every logon, tick "Start with Windows as administrator"; it registers a Task
   Scheduler task with the highest run level and removes the normal Run entry.

2. The `hide` mode hides the window completely, so no button remains on the taskbar; use "Restore
   last hidden window" in the tray menu to get it back. That record lives only in memory and is lost
   after a restart, leaving only Task Manager. Prefer the default `minimize` for everyday use.

3. A low level keyboard hook is a global hook, and a few games with strict anti-cheat may be
   sensitive to global hooks. If the hotkey does not need to grab keys (normal registration
   succeeds), the program installs no hook.

4. After a keyboard hook takes over a combination Windows occupies (for example `Win+S`), the
   original system function no longer responds, for example the search panel no longer pops up.
