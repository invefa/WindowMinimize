@echo off
rem Start WindowMinimize with administrator rights, so it can also handle elevated windows.
rem Exit the running copy from the tray menu first.
setlocal

set "EXE=%~dp0dist\WindowMinimize.exe"

if not exist "%EXE%" (
    echo [error] %EXE% not found, build the project first.
    pause
    exit /b 1
)

tasklist /fi "imagename eq WindowMinimize.exe" 2>nul | find /i "WindowMinimize.exe" >nul
if not errorlevel 1 (
    echo WindowMinimize is already running. Exit it from the tray menu first.
    ping -n 6 127.0.0.1 >nul 2>nul
    exit /b 1
)

powershell -NoProfile -Command "Start-Process -FilePath '%EXE%' -Verb RunAs"
exit /b 0
