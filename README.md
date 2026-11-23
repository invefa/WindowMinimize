# WindowMinimize

[English](README.md) | [简体中文](README.zh-Hans.md)

A small Windows tool that minimizes the current foreground window with a custom global hotkey. It
lives in the tray, its default minimize hotkey is `Win+Q`, it is a single-file exe, and it depends
on neither the .NET runtime nor any third-party component.

Download the full build (`WindowMinimize-<version>-win-x64.exe`) from the
[releases page](https://github.com/invefa/WindowMinimize/releases/latest) and run it, no
installation needed. The `WindowMinimize-<version>-win-x64-lite.exe` on the same page is about
240 KB, but it needs the .NET 8 Desktop Runtime on the machine. Both can be built from source too
(section 8).

## 1. Usage

Double-click `start.bat` or run `dist\WindowMinimize.exe` directly. After startup the program stays
in the taskbar notification area. Press `Win+Q` to minimize the foreground window at that moment.

Three global hotkeys are registered:

| Action | Default |
| --- | --- |
| Minimize the foreground window | `Win+Q` |
| Move the foreground window to the other configured screen, then minimize it | `Win+S` |
| Restore the most recently minimized window | `Win+W` |

All three can be changed in the settings dialog. The screen move uses the two screens selected as
Screen A and Screen B: a window on A moves to B, a window on B moves back to A, and a window on a
third screen moves to B.

For windows that run as administrator, start the program with `start-admin.bat` or use "Restart as
administrator" in the tray menu; Windows does not deliver the hotkey to a normal-privilege program
while such a window is in front. Turn on "Start with Windows", then tick "Start with Windows as
administrator" to start it elevated at every logon without a UAC prompt.

The first run creates `hotkey.ini` next to the exe. Save your changes and they take effect
immediately, with no restart. Left-clicking the tray icon opens the settings dialog, where the
hotkeys, the mode, the language and notification categories, the screen pair, the auto start and
the debug log can be changed without editing the file.

When the program is already running, starting it again asks that instance to open its settings
instead of showing an "already running" message.

To exit: right-click the tray icon and choose "Exit".

## 2. Hotkey Syntax

Join modifiers with `+` and put the main key last; the order does not change the result. Use `+` or
`-` as the separator.

| Part | Values |
| --- | --- |
| Modifier | `Ctrl` (also `Control`), `Alt`, `Shift`, `Win` (also `Windows`) |
| Main key | `A` to `Z`, `0` to `9`, `F1` to `F24`, `Space`, `Esc`, `Enter`, `Tab`, `Backspace`, `Delete`, `Insert`, `Home`, `End`, `PageUp`, `PageDown`, the four arrow keys, `Num0` to `Num9`, `Minus`, `Plus`, and so on |

Examples: `Win+Q`, `Win+S`, `Win+W`, `Ctrl+Shift+Q`, `Ctrl+Alt+F9`, `Alt+Space`, `F11`, `Ctrl+-`.

Without a modifier, only function keys (`F1` to `F24`) are allowed, so normal typing is not stolen.
`F12` is a system-reserved key and cannot be registered; the program rejects it outright.
System-reserved combinations such as `Alt+Tab`, `Ctrl+Alt+Del`, and `Win+L` are rejected as well.

Combinations that Windows itself occupies (for example `Win+S` to open search, or `Win+D` to show
the desktop) are not rejected. `RegisterHotKey` always fails for these, so the program takes them
over with a keyboard hook; once taken over, the original system function is no longer available.

## 3. Configuration File hotkey.ini

```ini
[general]
Hotkey=Win+Q
MoveHotkey=Win+S
RestoreHotkey=Win+W
MonitorA=
MonitorB=
RestoreAnimation=native
TrackAllMinimized=false
CheckUpdates=true
AutoUpdate=false
Mode=minimize
Language=auto
NotifyStartup=true
NotifyErrors=true
NotifyPermissions=true
Debug=false
```

| Setting | Default | Description |
| --- | --- | --- |
| Hotkey | Win+Q | Global hotkey for minimizing the foreground window |
| MoveHotkey | Win+S | Moves the foreground window to the other configured screen, then minimizes it |
| RestoreHotkey | Win+W | Restores the most recently minimized window |
| MonitorA | empty | Device name of the first screen in the swap pair; empty chooses the primary screen |
| MonitorB | empty | Device name of the second screen in the swap pair; empty chooses the first other screen |
| RestoreAnimation | native | How a moved window is restored: `native` (move the minimized position and keep the native animation), `fade` (fade in at the target), `disable` (restore without a transition), or `default` (leave the native transition and its source position alone) |
| TrackAllMinimized | false | When true, record minimized windows from every program and let `Win+W` restore them too |
| CheckUpdates | true | Check the latest GitHub release when the program starts |
| AutoUpdate | false | Download and install a newer release automatically instead of only reporting it |
| Mode | minimize | `minimize` minimizes to the taskbar; `hide` hides the window directly |
| Language | auto | Interface language: `auto` follows Windows, `en` or `zh` forces one; changing it in the settings dialog updates the tray immediately |
| NotifyStartup | true | Show the balloon that confirms the hotkey at startup |
| NotifyErrors | true | Show failure balloons, such as a hotkey that did not register |
| NotifyPermissions | true | Show elevation-related balloons, such as an elevated target or a task change |
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
| Version | Not clickable heading at the top, shows the version and whether this copy runs as administrator or as a standard user |
| Hidden windows | A submenu listing every window hidden with `hide`; selecting one restores it, and "Restore all hidden windows" restores the whole list. The entries live only in memory and are lost when the program exits |
| Hotkey: xxx | Shows only the hotkey currently in effect |
| Suspend hotkey / Resume hotkey | Temporarily disables it, handy while gaming or recording; hold Shift and click to suspend for 5 minutes and then resume automatically |
| Settings... | Opens the dialog for the three hotkeys, the screen pair, the restore animation, all-window minimize tracking, update checking and auto update, the mode, the language and notification categories, the normal or elevated auto start and the debug log |
| Open config file... | Opens `hotkey.ini` in Notepad |
| Reload config | Reads it again manually; not needed normally |
| Start with Windows | The master auto start switch. When ticked, the program starts with Windows using the normal Run entry; clear it to turn auto start off |
| Start with Windows as administrator | Enabled while auto start is on. Switches it to the elevated logon task: asks for administrator permission once, registers a Task Scheduler logon task with the highest run level and removes the normal Run entry |
| Restart as administrator | Closes the program and starts it again through the UAC prompt, so it can also handle windows that run elevated; not clickable when already elevated |
| Exit | Ends the program |

Clicking the tray icon with the left button opens the settings dialog.

## 5. Command-Line Arguments

```
WindowMinimize.exe [options]

--hotkey <combo>     Set the minimize hotkey for this run, overriding the config file
--mode <mode>        minimize or hide
--config <path>      Use the given config file
--lang <language>    Interface language, en or zh, defaults to the system language
--debug              Write the debug log
--no-tray            Do not show the tray icon (also --quiet)
--help               Help
```

## 6. Language

The interface follows the system language: a Chinese Windows shows the Chinese text, everything
else shows English. `Language=auto` in `hotkey.ini` follows Windows, while `Language=en` or
`Language=zh` forces one; the settings dialog writes this value and rebuilds the tray menu
immediately. `--lang en` or `--lang zh` overrides the config file for one run, to get a Chinese
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

Each of the three hotkeys has its own `HotkeySource`, so a combination that Windows or another
program already owns can fall back to the keyboard hook independently. Changing one hotkey does not
unregister the other two, and a failed registration restores that hotkey's previous value.

The screen move resolves the configured device names through `Screen.AllScreens`. The window is
minimized first, so the minimize animation stays on the source screen. Only after the minimize has
settled does the program write the target restore position, keep the window's relative position
within the target work area, move `ptMinPosition` with `WPF_SETMINPOSITION`, and recreate the
taskbar button on the target screen. The restore animation therefore starts on the target screen
while the minimize animation never crosses screens. The settings dialog also offers a target fade,
a transition-free restore, or the untouched system behavior; normal windows keep the native
animation.

The restore hotkey uses a stack of minimized windows. A newly minimized window is pushed on top,
old entries are pruned when their window or process is gone, and `Win+W` pops the newest live entry.
The stack lives in memory and is not persisted across restarts.

With `TrackAllMinimized` enabled, a shell minimize event hook records windows minimized by any
program, including taskbar clicks and Show Desktop. Already minimized windows are seeded when the
option is enabled, and the same `Win+W` key pops the newest entry from that broader history.

## Updates

At startup the program asks the GitHub release API for the latest stable release and compares it
with the running version. The tray menu also has `Check for updates`. A newer release shows the
version and asks before downloading unless `AutoUpdate` is enabled, in which case the matching full
or lite executable is downloaded automatically.

The downloaded executable is checked against the release `SHA256SUMS.txt` when that asset exists,
then a detached PowerShell helper waits for the current process to exit, replaces the executable
and restarts it. Update files live under `%LOCALAPPDATA%\WindowMinimize\updates`.

Hotkey actions are serialized. A screen move holds the queue until its delayed placement is done,
and a restore holds it until the restore animation has finished, so fast repeated presses cannot
interleave two operations. Restoring also cancels the background minimize retry for that window, so
a quick `Win+W` cannot be undone by the delayed retry.

A second launch does not show an error box. It creates or opens a named event that the first
instance waits on, and the first instance opens its settings dialog. A window-message fallback is
used when the event cannot be opened.

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

Pushing a tag such as `v1.2.2`, matching `<Version>` in `src\WindowMinimize.csproj`, makes
`.github\workflows\release.yml` run the unit tests, build both exe files, extract the matching
section of `CHANGELOG.md` and attach the exe files to the release.

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
   elevated start at every logon, turn on "Start with Windows", then tick "Start with Windows as
   administrator"; it registers a Task Scheduler task with the highest run level and removes the
   normal Run entry.

2. The `hide` mode hides the window completely, so no button remains on the taskbar; use the
   "Hidden windows" submenu in the tray menu to get it back. The list lives only in memory and is
   lost after a restart, leaving only Task Manager. Prefer the default `minimize` for everyday use.

3. The restore hotkey and the hidden-window list are kept in memory. A closed window or a process
   that exits is pruned, but a program restart clears the records.

4. A low level keyboard hook is a global hook, and a few games with strict anti-cheat may be
   sensitive to global hooks. If the hotkey does not need to grab keys (normal registration
   succeeds), the program installs no hook.

5. After a keyboard hook takes over a combination Windows occupies (for example `Win+Q`), the
   original system function no longer responds, for example the search panel no longer pops up.
