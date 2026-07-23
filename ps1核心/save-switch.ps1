# v1.917: 存档管理脚本 — 列出切换库中所有 .db 存档、切换/保存操作，操作时自动停服/重启
$ErrorActionPreference = "Continue"
$ScriptRoot = $PSScriptRoot
if ((Get-Item $ScriptRoot).Name -eq 'ps1核心' -or (Get-Item $ScriptRoot).Name -eq '旧版ps1') { $ScriptRoot = (Get-Item $ScriptRoot).Parent.FullName }
if ((Get-Item $ScriptRoot).Name -eq '旧版ps1' -or (Get-Item $ScriptRoot).Name -eq 'ps1核心') { $ScriptRoot = (Get-Item $ScriptRoot).Parent.FullName }
$SwitchDir = Join-Path $ScriptRoot "存档管理\切换库"
$DbTarget = Join-Path $ScriptRoot "ServerS4A12-AUM\dist\win-x64\Data\inventory.db"
$BackupDir = Join-Path $ScriptRoot "存档管理\备份存档"

if (-not (Test-Path $SwitchDir)) { New-Item -ItemType Directory -Path $SwitchDir -Force | Out-Null }
if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null }

Write-Host "========================================"
Write-Host "  存档管理"
Write-Host "========================================"
Write-Host ""
Write-Host "切换库路径: $SwitchDir"
Write-Host "目标位置:   $DbTarget"
Write-Host ""

$files = @(Get-ChildItem $SwitchDir -Filter "*.db" -ErrorAction SilentlyContinue)
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
Write-Host "--- 当前游戏存档 ---"
if (Test-Path $DbTarget) {
    $curSize = [math]::Round((Get-Item $DbTarget).Length / 1KB, 1)
    Write-Host "  inventory.db ($curSize KB)"
} else {
    Write-Host "  (未找到)"
}

Write-Host ""
Write-Host "========================================"
Write-Host "  操作选择"
Write-Host "========================================"
Write-Host ""
Write-Host "  输入编号 = 切换到对应存档"
Write-Host "  输入 S   = 保存当前存档到切换库"
Write-Host "  输入 0   = 取消退出"
Write-Host ""

$sel = (Read-Host "请选择").Trim()

if ($sel -eq "0" -or $sel -eq "") {
    Write-Host "已取消。"
    pause
    exit
}

# v1.916: 检测并停止服务端
function StopServer { & "$ScriptRoot\停止服务端.bat" 2>$null }
$wasRunning = (Get-Process -Name "DfoServer" -ErrorAction SilentlyContinue).Count -gt 0
if ($wasRunning) {
    Write-Host "检测到服务端运行中，正在自动停止..."
    StopServer
    Start-Sleep -Seconds 2
}

if ($sel -eq "S" -or $sel -eq "s") {
    if (-not (Test-Path $DbTarget)) {
        Write-Host "错误: 当前没有 inventory.db 可保存。"
        pause
        exit
    }
    $saveName = (Read-Host "请输入存档名称 (不含 .db 后缀)").Trim()
    if ($saveName -eq "") {
        Write-Host "已取消。"
        pause
        exit
    }
    $saveFile = Join-Path $SwitchDir ($saveName + ".db")
    if (Test-Path $saveFile) {
        $overwrite = (Read-Host "该名称已存在，是否覆盖? (Y/N)").Trim()
        if ($overwrite -ne "Y" -and $overwrite -ne "y") {
            Write-Host "已取消。"
            pause
            exit
        }
    }
    Copy-Item $DbTarget $saveFile -Force
    if (Test-Path $saveFile) {
        Write-Host "当前存档已保存为: $($saveName).db ($([math]::Round((Get-Item $saveFile).Length/1KB,1)) KB)"
        Write-Host "保存位置: $SwitchDir"
    } else {
        Write-Host "错误: 保存失败！"
    }

    if ($wasRunning) {
        Write-Host "正在重启服务端..."
        Start-Process -FilePath (Join-Path $ScriptRoot "ServerS4A12-AUM\start-server.bat") -WindowStyle Minimized
    }
    pause
    exit
}

try { $idx = [int]$sel - 1 } catch {
    Write-Host "无效输入: $sel"
    pause
    exit
}

if ($idx -lt 0 -or $idx -ge $files.Count) {
    Write-Host "无效编号: $sel (有效范围: 1-$($files.Count))"
    pause
    exit
}

$srcFile = $files[$idx].FullName
Write-Host ""
Write-Host "已选择: $($files[$idx].Name) ($([math]::Round($files[$idx].Length/1KB,1)) KB)"
Write-Host ""

$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$bakFile = Join-Path $BackupDir "backup_${ts}.db"

if (Test-Path $DbTarget) {
    Write-Host "正在备份当前存档..."
    Copy-Item $DbTarget $bakFile -Force -ErrorAction Stop
    if (Test-Path $bakFile) {
        Write-Host "备份完成: $bakFile"
    } else {
        Write-Host "警告: 备份可能失败"
    }
}

Write-Host "正在切换..."
Copy-Item $srcFile $DbTarget -Force -ErrorAction Stop
$newSize = [math]::Round((Get-Item $DbTarget).Length / 1KB, 1)
Write-Host "切换完成! inventory.db ($newSize KB)"

# v1.916: 提醒勾选清理冗余DB
Write-Host ""
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

if ($wasRunning) {
    Write-Host ""
    Write-Host "正在重启服务端..."
    Start-Process -FilePath (Join-Path $ScriptRoot "ServerS4A12-AUM\start-server.bat") -WindowStyle Minimized
}
pause
