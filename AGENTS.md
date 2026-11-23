# AGENTS.md

[English](AGENTS.md) | [简体中文](AGENTS.zh-Hans.md)

## Workflow

This project enables agent-workflow. At the start of a task read the
[workflow entry](https://github.com/invefa/agent-workflow/blob/main/README.md), then read the
topic pages that apply to the task:

- UI and controls: [UI standard](https://github.com/invefa/agent-workflow/blob/main/standards/ui.md)
- Code, configuration, and persistent data: [engineering standard](https://github.com/invefa/agent-workflow/blob/main/standards/engineering.md)
- Documents, naming, and visual expression: [writing and organization](https://github.com/invefa/agent-workflow/blob/main/standards/writing.md)
- Validation, commits, and delivery: [validation and delivery](https://github.com/invefa/agent-workflow/blob/main/standards/delivery.md)

Follow the [personal preferences](https://github.com/invefa/agent-workflow/blob/main/preferences.md).

## Project

WindowMinimize is a Windows tray tool built with C#, .NET 8, and WinForms. It supports custom global
hotkeys, window minimizing, screen switching, minimized-window history, settings, and GitHub updates.

Source lives in`src`, unit tests in`test/unit`, desktop end-to-end tests in`test/e2e`, and the build
entry point is`build/build.ps1`.

`src` is grouped by responsibility, and every file keeps the`WindowMinimize` namespace:

| Folder | Owns |
|---|---|
| `app` | Entry point, tray shell, single instance, version, log |
| `config` | Settings model and`hotkey.ini` storage |
| `hotkey` | Shortcut syntax and global hotkey registration |
| `window` | Win32 declarations, window operations, taskbar interop |
| `ui` | Settings dialog, custom controls, tray icon, string accessor |
| `update` | GitHub release check and installation |
| `Resources` | String tables only, no code |

## Build and validation

```powershell
dotnet build src\WindowMinimize.csproj -c Release --nologo
dotnet test test\unit\WindowMinimize.Tests.csproj --nologo -q
pwsh -NoProfile -File build\build.ps1
pwsh -NoProfile -File build\build.ps1 -Lite
```

`test/e2e` needs a real desktop, a foreground window, and global hotkeys; run it according to the
affected scope. Check UI changes at the real window size. When a custom control is added, provide a
preview under`test/preview` and give the run command in the delivery.

## Change rules

- Use English for code, comments, logs, and commit messages. Put user-facing text in
  `src/Resources`; do not place Chinese UI text directly in source.
- Use four spaces for indentation and a 100-column limit. Follow K&R braces and the existing
  WinForms control and project structure.
- Before changing behavior, check the other entry points and shared validation so the fix does not
  cover only one trigger path.
- After each code change, commit with an English Conventional Commit title. Keep unrelated format
  cleanup out of feature commits.
- For small fixes and UI changes, finish the implementation, build, and necessary validation, then
  wait for user acceptance before release. Before acceptance do not bump the version, create a tag,
  create a GitHub Release, or push release assets.
- Before release, rebuild from the final commit and check the version, release assets, and update
  filenames used by the updater. After release, verify the actual GitHub Release state.

## Characters and expression

- Do not use emoji or decorative Unicode characters. Use only characters that can be typed on a
  keyboard; write arrows as`->`.
- Use Chinese punctuation in Chinese prose. Keep code, paths, URLs, and commands in half-width
  characters.
- Do not put spaces between English and Chinese characters.
- State the conclusion first, then the evidence, validation, and boundaries needed to judge it. Do
  not narrate process step by step.
- Separate a list item name from its explanation. Express hierarchy with spacing, line weight, and
  type size; use color only for stable meaning.
- Link files with Markdown links. Use relative links in repository documents.

## UI conventions

- Prefer existing WinForms controls, system colors, and project resources. Do not introduce a fixed
  palette or decorative heavy fills.
- Show custom controls in`test/preview` with fixed examples and interaction states before wiring
  them into the settings dialog. Give the run command in the delivery.
- Use the standard Windows title-bar Help button for the standalone help document. Keep
  `HELP.html` and`HELP.zh-Hans.html` available as the document source and embedded fallback.
- Check alignment, clipping, and hit areas at 100%, 125%, and 150% scaling. Accept visual changes
  against a screenshot from the running application.
