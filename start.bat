@echo off
rem 启动快捷键最小化工具。已经在运行时会提示，不会重复启动。
setlocal

set "EXE=%~dp0dist\WindowMinimize.exe"

if not exist "%EXE%" (
    echo [错误] 找不到 %EXE%
    echo 请先运行 build\build.ps1 生成程序。
    pause
    exit /b 1
)

tasklist /fi "imagename eq WindowMinimize.exe" 2>nul | find /i "WindowMinimize.exe" >nul
if not errorlevel 1 (
    echo WindowMinimize 已经在运行，请查看任务栏通知区域。
    timeout /t 3 >nul
    exit /b 0
)

start "" "%EXE%"
exit /b 0
