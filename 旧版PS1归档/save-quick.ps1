# v1.917: 快速换挡脚本 — 拖拽 .db 文件替换 inventory.db，自动停服/重启
$ErrorActionPreference = "Continue"
$ScriptRoot = $PSScriptRoot
$SrcFile = $args[0]
$DbTarget = Join-Path $ScriptRoot "ServerS4A12-AUM\dist\win-x64\Data\inventory.db"
$BackupDir = Join-Path $ScriptRoot "存档管理\备份存档"

if (-not $SrcFile -or $SrcFile -eq "") {
    Write-Host "========================================"
    Write-Host "  快速换挡 - 使用说明"
    Write-Host "========================================"
    Write-Host ""
    Write-Host "请将 .db 存档文件直接拖拽到【快速换挡.bat】图标上。"
    Write-Host "程序会自动将文件替换为 inventory.db。"
    Write-Host ""
    Write-Host "目标路径: ServerS4A12-AUM\dist\win-x64\Data\inventory.db"
    Write-Host "备份路径: 存档管理\备份存档\"
    Write-Host ""
    Write-Host "替换前会自动备份旧存档并停止服务端，操作后自动重启。"
    Write-Host "输入 2 可撤销换挡。"
    Write-Host "========================================"
    pause
    exit
}

if (-not (Test-Path $SrcFile)) {
    Write-Host "错误: 文件未找到 - $SrcFile"
    pause
    exit 1
}

if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
}

Write-Host "========================================"
Write-Host "  快速换挡"
Write-Host "========================================"
Write-Host ""
Write-Host "源文件: $(Split-Path $SrcFile -Leaf)"
Write-Host "目标位置: $DbTarget"
Write-Host ""

# v1.916: 检测并停止服务端
function StopServer { & "$ScriptRoot\停止服务端.bat" 2>$null }
$wasRunning = (Get-Process -Name "DfoServer" -ErrorAction SilentlyContinue).Count -gt 0
if ($wasRunning) {
    Write-Host "检测到服务端运行中，正在自动停止..."
    StopServer
    Start-Sleep -Seconds 2
}

$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$bakFile = Join-Path $BackupDir "backup_${ts}.db"

if (Test-Path $DbTarget) {
    Write-Host "正在备份当前存档..."
    Copy-Item $DbTarget $bakFile -Force
    Write-Host "备份完成: $bakFile"
}

Write-Host "正在替换存档..."
Copy-Item $SrcFile $DbTarget -Force
Write-Host ""
Write-Host "换挡完成!"
Write-Host ""

# v1.916: 提醒勾选清理冗余DB
Write-Host "========================================"
Write-Host "  已成功切换存档。如果无法登录服务端或"
Write-Host "  网络连接中断，请开启 ServerUI 后勾选"
Write-Host "  [清理冗余DB] 复选框再操作一次。"
Write-Host ""
Write-Host "  存档目录位于:"
Write-Host "  $(Join-Path $ScriptRoot 'ServerS4A12-AUM\dist\win-x64\Data')"
Write-Host "  请手动删除除【inventory.db】以外的冗余文件，"
Write-Host "  但其他文件夹不要动！"
Write-Host "========================================"
Write-Host ""
Write-Host "  输入 2 并回车 = 撤销本次换挡 (恢复旧存档)"
Write-Host "  直接关闭窗口   = 确认替换"
$choice = Read-Host "请输入"

if ($choice -eq "2") {
    if (Test-Path $bakFile) {
        Copy-Item $bakFile $DbTarget -Force
        Write-Host "已恢复为换挡前的存档。"
    } else {
        Write-Host "未找到备份文件，无法撤销。"
    }
} else {
    Write-Host "已确认。旧存档备份在: $bakFile"
}

if ($wasRunning) {
    Write-Host ""
    Write-Host "正在重启服务端..."
    Start-Process -FilePath (Join-Path $ScriptRoot "ServerS4A12-AUM\start-server.bat") -WindowStyle Minimized
}
pause
