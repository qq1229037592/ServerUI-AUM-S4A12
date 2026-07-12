@echo off
chcp 65001 >nul
cd /d "%~dp0"
set "DFO_GM_SERVER_BIN=%~dp0ServerS4A12-AUM\dist\win-x64"
start "" "%~dp0dfogmtool\publish\DfoGmTool.exe" --server-bin "%DFO_GM_SERVER_BIN%"
echo GM工具已启动, 正在打开浏览器...
timeout /t 3 /nobreak >nul
start http://localhost:5050
