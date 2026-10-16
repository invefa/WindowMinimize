@echo off
rem Launch the hotkey minimize tool. If already running it notifies and does not start again.
setlocal

set "EXE=%~dp0dist\WindowMinimize.exe"

if not exist "%EXE%" (
    echo [error] %EXE% not found
    echo Run build\build.ps1 first to produce the executable.
    pause
    exit /b 1
)

tasklist /fi "imagename eq WindowMinimize.exe" 2>nul | find /i "WindowMinimize.exe" >nul
if not errorlevel 1 (
    echo WindowMinimize is already running; check the taskbar notification area.
    timeout /t 3 >nul
    exit /b 0
)

start "" "%EXE%"
exit /b 0
