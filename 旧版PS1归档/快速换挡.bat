chcp 65001
@echo off
if "%~1"=="" (
    cd /d "%~dp0"
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0save-quick.ps1"
    pause
    exit /b
)
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0save-quick.ps1" "%~1"
pause