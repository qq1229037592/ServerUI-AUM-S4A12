chcp 65001
@echo off
cls
echo ========================================
echo   ServerS4A12 - 全量更新
echo ========================================
echo.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update.ps1" -FullSync
pause