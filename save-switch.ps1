# 存档管理脚本：列出切换库中所有 .db 存档、显示当前游戏存档，支持切换/保存/撤销操作，切换前自动备份旧档
$ErrorActionPreference = "Stop"
# 脚本自身所在的目录路径
$ScriptRoot = $PSScriptRoot
# 切换库目录：存放用户保存的多个存档文件，每个都是一个独立的 .db 档
$SwitchDir = Join-Path $ScriptRoot "存档管理\切换库"
# 游戏实际使用的存档文件路径（切换时被替换的目标文件）
$DbTarget = Join-Path $ScriptRoot "ServerS4A12-AUM\dist\win-x64\Data\inventory.db"
# 备份目录：切换前自动把当前游戏存档备份到这里，方便撤销
$BackupDir = Join-Path $ScriptRoot "存档管理\备份存档"

# 如果切换库目录不存在则自动创建，用于存放用户保存的多个存档
if (-not (Test-Path $SwitchDir)) {
    New-Item -ItemType Directory -Path $SwitchDir -Force | Out-Null
}
# 如果备份目录不存在则自动创建，用于存放切换前的旧存档备份
if (-not (Test-Path $BackupDir)) {
    New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
}

Write-Host "========================================"
Write-Host "  存档管理"
Write-Host "========================================"
Write-Host ""
# 显示两个关键路径，让用户知道存档库在哪、游戏存档在哪
Write-Host "切换库路径: $SwitchDir"
Write-Host "目标位置:   $DbTarget"
Write-Host ""

# 获取切换库目录下所有 .db 存档文件，存入数组（忽略找不到文件的错误）
$files = @(Get-ChildItem $SwitchDir -Filter "*.db" -ErrorAction SilentlyContinue)

# 列出切换库中所有可用的存档，每条显示编号、文件名和文件大小（KB）
Write-Host "--- 现有存档 ---"
if ($files.Count -eq 0) {
    Write-Host "  (暂无存档文件)"
} else {
    for ($i = 0; $i -lt $files.Count; $i++) {
        $sizeKB = [math]::Round($files[$i].Length / 1KB, 1)
        Write-Host ("  " + ($i+1) + ". " + $files[$i].Name + "  (" + $sizeKB + " KB)")
    }
}

Write-Host ""
# 显示当前正在被游戏使用的存档文件及其大小
Write-Host "--- 当前游戏存档 ---"
if (Test-Path $DbTarget) {
    $curSize = [math]::Round((Get-Item $DbTarget).Length / 1KB, 1)
    Write-Host "  inventory.db ($curSize KB)"
} else {
    Write-Host "  (未找到)"
}

Write-Host ""
Write-Host "========================================"
# 操作菜单：让用户选择是切换存档、保存当前存档，还是取消退出
Write-Host "  操作选择"
Write-Host "========================================"
Write-Host ""
Write-Host "  输入编号 = 切换到对应存档"
Write-Host "  输入 S   = 保存当前存档到切换库"
Write-Host "  输入 0   = 取消退出"
Write-Host ""

# 读取用户输入并去掉首尾空格
$sel = (Read-Host "请选择").Trim()

# 输入 0 或直接回车 = 不做任何操作，退出脚本
if ($sel -eq "0" -or $sel -eq "") {
    Write-Host "已取消。"
    pause
    exit
}

# 用户选择 S/s = 将当前游戏存档复制一份到切换库中保存
if ($sel -eq "S" -or $sel -eq "s") {
    # 先检查当前是否存在游戏存档，没有则无法保存
    if (-not (Test-Path $DbTarget)) {
        Write-Host "错误: 当前没有 inventory.db 可保存。"
        pause
        exit
    }
    # 让用户给这个存档起个名字（不需要加 .db 后缀）
    $saveName = (Read-Host "请输入存档名称 (不含 .db 后缀)").Trim()
    if ($saveName -eq "") {
        Write-Host "已取消。"
        pause
        exit
    }
    $saveFile = Join-Path $SwitchDir ($saveName + ".db")
    # 如果同名存档已存在，询问是否覆盖
    if (Test-Path $saveFile) {
        $overwrite = (Read-Host "该名称已存在，是否覆盖? (Y/N)").Trim()
        if ($overwrite -ne "Y" -and $overwrite -ne "y") {
            Write-Host "已取消。"
            pause
            exit
        }
    }
    # 执行保存：将当前游戏存档复制到切换库中
    Copy-Item $DbTarget $saveFile -Force
    if (Test-Path $saveFile) {
        Write-Host "当前存档已保存为: $($saveName).db ($([math]::Round((Get-Item $saveFile).Length/1KB,1)) KB)"
        Write-Host "保存位置: $SwitchDir"
    } else {
        Write-Host "错误: 保存失败！"
    }
    pause
    exit
}

# 尝试将用户输入的编号转换为整数索引（显示编号 1 对应数组索引 0）
try {
    $idx = [int]$sel - 1
} catch {
    Write-Host "无效输入: $sel"
    pause
    exit
}

# 校验索引是否在有效范围内（不能小于 0 或超出存档列表长度）
if ($idx -lt 0 -or $idx -ge $files.Count) {
    Write-Host "无效编号: $sel (有效范围: 1-$($files.Count))"
    pause
    exit
}

# 根据用户选择的编号，取出对应存档文件的完整路径
$srcFile = $files[$idx].FullName
Write-Host ""
Write-Host "已选择: $($files[$idx].Name) ($([math]::Round($files[$idx].Length/1KB,1)) KB)"
Write-Host ""

# 用当前时间生成唯一的备份文件名（格式：backup_年月日_时分秒.db），防止备份文件互相覆盖
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$bakFile = Join-Path $BackupDir "backup_${ts}.db"

# 切换前先备份当前游戏存档（如果存在），万一切换错了还可以用备份恢复
if (Test-Path $DbTarget) {
    Write-Host "正在备份当前存档..."
    Copy-Item $DbTarget $bakFile -Force -ErrorAction Stop
    if (Test-Path $bakFile) {
        Write-Host "备份完成: $bakFile"
    } else {
        Write-Host "警告: 备份可能失败"
    }
}

# 将用户选择的存档文件复制到游戏存档位置，完成切换
Write-Host "正在切换..."
Copy-Item $srcFile $DbTarget -Force -ErrorAction Stop

# 显示切换后的游戏存档文件大小，确认切换成功
$newSize = [math]::Round((Get-Item $DbTarget).Length / 1KB, 1)
Write-Host "切换完成! inventory.db ($newSize KB)"
pause
