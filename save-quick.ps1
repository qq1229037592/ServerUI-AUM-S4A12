# 快速换挡脚本：将拖拽进来的 .db 存档文件替换为游戏当前使用的 inventory.db，替换前自动备份旧档，支持输入 2 撤销换挡
$ErrorActionPreference = "Continue"
# 脚本自身所在的目录路径（与 .bat 文件同目录）
$ScriptRoot = $PSScriptRoot
# 用户拖拽进来的 .db 源文件路径（通过 BAT 传入的第一个参数）
$SrcFile = $args[0]
# 游戏实际使用的存档文件路径（要替换的目标文件）
$DbTarget = Join-Path $ScriptRoot "ServerS4A12-AUM\dist\win-x64\Data\inventory.db"
# 旧存档的备份目录（替换前会把当前存档备份到这里）
$BackupDir = Join-Path $ScriptRoot "存档管理\备份存档"

# 如果没有传入任何参数（用户直接双击运行而非拖拽），显示使用说明后退出
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
    Write-Host "替换前会自动备份旧存档，支持输入 2 撤销。"
    Write-Host "========================================"
    pause
    exit
}

# 检查用户拖拽的源文件是否真实存在，不存在则报错退出
if (-not (Test-Path $SrcFile)) {
    Write-Host "错误: 文件未找到 - $SrcFile"
    pause
    exit 1
}

# 如果备份目录还不存在，则自动创建它
if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
}

Write-Host "========================================"
Write-Host "  快速换挡"
Write-Host "========================================"
Write-Host ""
# 显示用户拖拽的源文件名和目标路径
Write-Host "源文件: $(Split-Path $SrcFile -Leaf)"
Write-Host "目标位置: $DbTarget"
Write-Host ""

# 用当前时间生成唯一的备份文件名（格式：backup_年月日_时分秒.db）
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$bakFile = Join-Path $BackupDir "backup_${ts}.db"

# 如果当前已有游戏存档，先把它备份到备份目录中，以防误操作造成数据丢失
if (Test-Path $DbTarget) {
    Write-Host "正在备份当前存档..."
    Copy-Item $DbTarget $bakFile -Force
    Write-Host "备份完成: $bakFile"
}

# 将用户拖拽的存档文件强制覆盖到游戏存档位置，完成换挡
Write-Host "正在替换存档..."
Copy-Item $SrcFile $DbTarget -Force
Write-Host ""
Write-Host "换挡完成!"
Write-Host ""
Write-Host "========================================"
Write-Host "  输入 2 并回车 = 撤销本次换挡 (恢复旧存档)"
Write-Host "  直接关闭窗口   = 确认替换"
Write-Host "========================================"
# 等待用户选择：输入 2 撤销换挡，输入其他任意内容或直接回车则确认替换
$choice = Read-Host "请输入"

# 如果用户输入 2，则将之前备份的旧存档写回游戏存档位置，完成撤销
if ($choice -eq "2") {
    if (Test-Path $bakFile) {
        Copy-Item $bakFile $DbTarget -Force
        Write-Host "已恢复为换挡前的存档。"
    } else {
        Write-Host "未找到备份文件，无法撤销。"
    }
} else {
    # 用户确认替换，提示旧存档备份的位置
    Write-Host "已确认。旧存档备份在: $bakFile"
}
pause