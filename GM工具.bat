@echo off
chcp 65001 >nul
title DfoGmTool - GM工具
cd /d "%~dp0"

echo ========================================
echo   DfoGmTool - GM工具 (独立启动)
echo ========================================
echo.

set "SERVER_BIN=%~dp0ServerS4A12-AUM\dist\win-x64"
set "GM_EXE=%~dp0dfogmtool\publish\DfoGmTool.exe"

if not exist "%GM_EXE%" (
    echo [错误] GM工具未编译, 请先在ServerUI中执行一次增量/全量更新。
    echo        或手动运行: 开始更新.bat
    pause
    exit /b 1
)

if not exist "%SERVER_BIN%\Data\inventory.db" (
    echo [错误] 服务端数据目录不完整:
    echo        %SERVER_BIN%
    echo        缺少 Data\inventory.db, 请先执行一次更新。
    pause
    exit /b 1
)

if not exist "%SERVER_BIN%\Data\Pvf\Script.pvf" (
    echo [错误] 服务端数据目录缺少 Data\Pvf\Script.pvf, 请先执行一次更新。
    pause
    exit /b 1
)

echo [1/2] 正在停止旧的GM工具进程...
taskkill /f /im DfoGmTool.exe >nul 2>&1
timeout /t 1 /nobreak >nul

echo [2/2] 正在启动GM工具...
set "DFO_GM_SERVER_BIN=%SERVER_BIN%"
start "" "%GM_EXE%" --server-bin "%SERVER_BIN%"

echo.
echo GM工具已启动, 3秒后自动打开浏览器...
timeout /t 3 /nobreak >nul
start http://localhost:5050

echo.
echo GM工具运行中, 浏览器访问: http://localhost:5050
echo 关闭此窗口不会停止GM工具 (在ServerUI中退出才会自动清理)
echo ========================================
exit /b 0
