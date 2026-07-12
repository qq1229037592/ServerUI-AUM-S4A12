# ============================== ServerS4A12 一键更新脚本 ==============================
# 支持增量更新（默认，只更新最近 3 天变更的文件）和全量同步（-FullSync 开关）
# 全流程：[1/5] 备份数据库 → [2/5] 下载最新源码 → [3/5] 更新文件 → [4/5] 编译 → [5/5] 提交日志
param([switch]$FullSync, [switch]$NonInteractive)    # -FullSync：全量同步开关，不加此参数默认执行增量更新；-NonInteractive：非交互模式（从GUI调用时使用，跳过人工确认）

$ErrorActionPreference = "Continue"   # 遇到非致命错误时不中断脚本，继续执行后续步骤
$ScriptRoot = $PSScriptRoot; $SrcRoot = Join-Path $ScriptRoot "ServerS4A12-AUM"    # ScriptRoot=脚本所在目录，SrcRoot=服务器主目录
$RepoApi = "https://codeberg.org/api/v1/repos/rewio/ServerS4A12"    # Codeberg 仓库 API 地址
$utf8 = [System.Text.Encoding]::UTF8

# base64 编码的中文字符串字典，避免汉字在控制台/日志中出现乱码
$b64 = @{
    fn_log  = "5pu05paw5pel5b+XLnR4dA=="
    s_ver   = "54mI5pysOiA="
    s_up    = "5pu05paw5pe26Ze0OiA="
    s_total = "57Sv6K6h5o+Q5LqkOiA="
    s_hist  = "5pu05paw5Y6G5Y+yICjku47mnIDliJ3liLDnjrDlnKjvvIzljJfkuqzml7bpl7QgVVRDKzgpOg=="
    s_more  = "5pu05aSa5Y6G5Y+y5pu05paw5pel5b+X77yM6K+35Zyo55uu5b2V5p+l55yLOiA="
    s_repo  = "5LuT5bqT5o+Q5Lqk6K6w5b2VOiA="
    s_inc   = "5aKe6YeP5pu05paw"
    s_full  = "5YWo6YeP5ZCM5q2l"
    s_fullsync = "5YWo6YeP5ZCM5q2lICjmiYDmnInmlofku7YpLi4u"
    s_fallback = "5pyA6L+R5peg5Y+Y5pu0IC0g5Zue6YCA5Yiw5YWo6YeP5ZCM5q2l44CC"
    s_server = "U2VydmVyUzRBMTIgLSA="
    s_updating = "Pj4+IFszLzVdIOato+WcqOabtOaWsOaWh+S7tiAo"
    s_done  = "Pj4+IOWujOaIkCEg"
    s_warn1 = "5byA5aeL5pu05paw5YmN77yM6K+356Gu6K6k572R57uc54iL6YCa77yM5Lim5qOA5p+l5piv5ZCm5bey5byA5ZCv56eR5a2m5LiK572R44CC"
    s_warn2 = "5pys5Zyw5paH5Lu26Lev5b6E5Y+C6ICD77ya"
    s_warn3 = "ICAtIOaVsOaNruW6k+WtmOahozogXFNlcnZlclM0QTEyLUFVTVxkaXN0XHdpbi14NjRcRGF0YVxpbnZlbnRvcnkuZGI="
    s_warn4 = "ICAtIFBWRuaWh+S7tjogICBcU2VydmVyUzRBMTItQVVNXGRpc3Rcd2luLXg2NFxEYXRhXFB2ZlxTY3JpcHQucHZm"
    s_warn5 = "6K+356Gu6K6k5LiK6L+w5paH5Lu25L2N572u5peg6K+v44CC"
    s_skip   = "ICBb5L+d5oqkXSA="
    s_prot   = "ICAo5bey5L+d5oqk77yM5LiN5Lya6KKr6KaG55GWKQ=="
}
function T($key) { return $utf8.GetString([Convert]::FromBase64String($b64[$key])) }    # 将 base64 字符串解码为中文文本

$LogFile   = Join-Path $ScriptRoot (T "fn_log")    # 更新日志文件路径（脚本同目录下）
$DbFile = Join-Path $SrcRoot "Server\DfoServer\Data\inventory.db"    # 玩家数据数据库文件（最重要！）
$DbBackup = Join-Path $SrcRoot "Server\DfoServer\Data\inventory.db.bak"    # 数据库临时备份文件
$TempDir   = Join-Path $env:TEMP "ServerS4A12-update"    # 下载和解压源码用的临时目录
$LocalSdk  = Join-Path $ScriptRoot "dotnet-sdk"    # 本地 .NET SDK 安装目录（脚本同目录下）
$ChinaTZ   = [System.TimeZoneInfo]::FindSystemTimeZoneById("China Standard Time")    # 中国标准时间（UTC+8）时区对象

function ToChinaDate($d) {    # 将 UTC 时间字符串转换为北京时间（UTC+8）的日期格式 yyyy-MM-dd
    $dt = [DateTimeOffset]::Parse($d, [System.Globalization.CultureInfo]::InvariantCulture)
    return ([System.TimeZoneInfo]::ConvertTime($dt, $ChinaTZ)).ToString("yyyy-MM-dd")
}

function Get-DotNetExe {    # 检测 .NET SDK 是否存在，按优先级：系统全局 > Program Files > 本地 dotnet-sdk 目录
    $sys = "dotnet"
    try { $v = & dotnet --version 2>&1; if ($LASTEXITCODE -eq 0 -and $v -match "^(\d+)\.(\d+)" -and [int]$matches[1] -ge 10) { return $sys } } catch { }
    try { $pf = "$env:ProgramFiles\dotnet\dotnet.exe"; if (Test-Path $pf) { $v = & $pf --version 2>&1; if ($LASTEXITCODE -eq 0 -and $v -match "^(\d+)\.(\d+)" -and [int]$matches[1] -ge 10) { return $pf } } } catch { }
    $local = Join-Path $LocalSdk "dotnet.exe"
    if (Test-Path $local) { return $local }
    return $null
}

function Ensure-DotNet10 {    # 确保 .NET 10 SDK 可用：优先检测系统现有 SDK，没有则自动下载安装到本地 dotnet-sdk 目录
    $dn = Get-DotNetExe
    if ($dn) {
        $ver = & $dn --version 2>&1
        $verStr = $ver.Trim()
        if ($dn -eq "dotnet" -or $dn -like "*Program Files*") {
            Write-Host "  Using system .NET SDK ($verStr)"
        } else {
            Write-Host "  Using bundled .NET SDK ($verStr)"
        }
        return $dn
    }
    Write-Host "  .NET 10 SDK not found."
    if ($NonInteractive) {
        Write-Host "  Non-interactive mode (GUI): skipping auto-install."
        Write-Host "  Please run 开始更新.bat in a console window to install .NET SDK."
        return $null
    }
    $choice = Read-Host "  Download and install .NET 10 SDK to local folder? (~280MB) (Y/N)"
    if ($choice -ne "Y" -and $choice -ne "y") {
        Write-Host "  Skipped. Cannot compile without .NET SDK."
        return $null
    }
    Write-Host "  Downloading..."
    $installer = Join-Path $ScriptRoot "dotnet-install.ps1"
    if (-not (Test-Path $installer)) {
        Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer -UseBasicParsing
    }
    & $installer -Channel 10.0 -InstallDir $LocalSdk -NoPath 2>&1 | Select-Object -Last 2
    $localExe = Join-Path $LocalSdk "dotnet.exe"
    if (Test-Path $localExe) {
        Write-Host "  SDK installed to dotnet-sdk/"
        return $localExe
    }
    Write-Host "  Installation failed."
    return $null
}

try {    # ===== 主更新流程开始：共 5 个步骤 =====
    Set-Location $SrcRoot
    $currentDate  = Get-Date -Format "yyyy-MM-dd"
    $currentTime  = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $threeDaysAgo = (Get-Date).AddHours(-72)

    $modeText = if ($FullSync) { T "s_full" } else { T "s_inc" }
    Write-Host ""
    Write-Host "========================================"
    Write-Host "  $(T 's_server')$modeText"
    Write-Host "  Date: $currentDate (UTC+8)"
    Write-Host "========================================"
    Write-Host ""
    Write-Host "[ $(T 's_warn1') ]"
    Write-Host "$(T 's_warn2')"
    Write-Host "$(T 's_warn3')"
    Write-Host "$(T 's_warn4')"
    Write-Host "$(T 's_warn5')"
    Write-Host ""
    Write-Host "========================================"
    Write-Host ""

    if (Test-Path $LogFile) {
        $raw = [System.IO.File]::ReadAllText($LogFile, $utf8)
        $rx = [regex]::Matches($raw, "\d{4}-\d{2}-\d{2}")
        if ($rx.Count -gt 0) {
            $lv = $rx[$rx.Count - 1].Value
            if ($lv -eq $currentDate) { Write-Host "Last version: $lv (up-to-date)" }
            else { Write-Host "Last version: $lv ($(((Get-Date $currentDate)-(Get-Date $lv)).Days)d ago)" }
        }
    } else { Write-Host "First run." }

    Write-Host ""
    Write-Host ">>> [1/5] Backing up inventory.db <<<"    # [1/5] 先备份玩家数据库，防止更新过程中数据丢失
    $dbExisted = Test-Path $DbFile
    if ($dbExisted) { Copy-Item $DbFile $DbBackup -Force; Write-Host "OK ($((Get-Item $DbFile).Length) bytes)" }
    else { Write-Host "No inventory.db, skip." }

    Write-Host ""
    Write-Host ">>> [2/5] Downloading source <<<"    # [2/5] 从 Codeberg 仓库下载最新主分支 ZIP 源码包
    if (Test-Path $TempDir) { Remove-Item -Recurse -Force $TempDir }
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    $TempZip = Join-Path $TempDir "main.zip"
    $TempExtract = Join-Path $TempDir "extract"
    $ProgressPreference = "SilentlyContinue"
    Invoke-WebRequest -Uri "https://codeberg.org/rewio/ServerS4A12/archive/main.zip" -OutFile $TempZip -UseBasicParsing
    if (-not (Test-Path $TempZip)) {
        Write-Host "ERROR: Download failed."
        if ($dbExisted) { Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force }
        exit 1
    }
    Write-Host "OK ($([math]::Round((Get-Item $TempZip).Length/1KB)) KB)"

    Write-Host ""
    Write-Host "$(T 's_updating')$modeText) <<<"    # [3/5] 更新文件：增量模式只更新最近变更的文件，全量模式同步所有历史文件；同时保护 inventory.db 和 start-server.bat 不被覆盖
    Expand-Archive -Path $TempZip -DestinationPath $TempExtract -Force
    $srcDir = Get-ChildItem -Path $TempExtract -Directory | Select-Object -First 1
    if (-not $srcDir) {
        Write-Host "ERROR: Extraction failed."
        if ($dbExisted) { Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force }
        exit 1
    }
    $srcPath = $srcDir.FullName

    if ($FullSync) {
        Write-Host (T "s_fullsync")
        $allC = @(); $pg = 1
        try {
            while ($true) {
                $resp = Invoke-WebRequest -Uri "$RepoApi/commits?sha=main&limit=50&page=$pg" -UseBasicParsing -TimeoutSec 15
                $j = $utf8.GetString($resp.RawContentStream.ToArray()); $l = $j | ConvertFrom-Json
                if (-not $l -or $l.Count -eq 0) { break }
                $allC += $l; if ($l.Count -lt 50) { break }; $pg++
            }
        } catch { Write-Host "WARNING: $_" }
        if ($allC.Count -gt 0) {
            $old = $allC[-1]; $bs = if ($old.parents -and $old.parents.Count -gt 0) { $old.parents[0].sha } else { $old.sha }
            try {
                $dr = Invoke-WebRequest -Uri "$RepoApi/compare/$bs...main" -UseBasicParsing -TimeoutSec 15
                $dj = $utf8.GetString($dr.RawContentStream.ToArray()); $dd = $dj | ConvertFrom-Json
                if ($dd.files) {
                    $cc = 0
                    foreach ($f in $dd.files) {
                        $rp = $f.filename; $fn = Split-Path $rp -Leaf
                        if ($rp -eq "Server/DfoServer/Data/inventory.db") { continue }
                        if ($fn -eq "start-server.bat" -or $fn -eq "start-server.sh") { Write-Host "$(T 's_skip')$rp $(T 's_prot')"; continue }
                        $sf = Join-Path $srcPath $rp; $df = Join-Path $SrcRoot $rp
                        if ($f.status -eq "removed") { if (Test-Path $df) { Remove-Item $df -Force; $cc++ } }
                        elseif (Test-Path $sf) { $ddir = Split-Path $df -Parent; if (-not (Test-Path $ddir)) { New-Item -ItemType Directory -Path $ddir -Force | Out-Null }; Copy-Item $sf $df -Force; $cc++ }
                    }
                    Write-Host "Synced $cc files from full repo history."
                }
            } catch { Write-Host "WARNING: Compare API failed, falling back to robocopy."; robocopy "$srcPath" "$SrcRoot" /E /COPY:DAT /DCOPY:T /R:3 /W:2 /XD ".git" /XF "inventory.db" "inventory.db.bak" "start-server.bat" "start-server.sh" /NP /NDL 2>&1 | Out-Null }
        }
    } else {
        $sinceParam = [System.Uri]::EscapeDataString($threeDaysAgo.ToString("yyyy-MM-ddTHH:mm:ssZ"))
        $allCommits = @(); $page = 1
        try {
            while ($true) {
                $resp = Invoke-WebRequest -Uri "$RepoApi/commits?sha=main&since=$sinceParam&limit=50&page=$page" -UseBasicParsing -TimeoutSec 15
                $json = $utf8.GetString($resp.RawContentStream.ToArray())
                $list = $json | ConvertFrom-Json
                if (-not $list -or $list.Count -eq 0) { break }
                $allCommits += $list
                if ($list.Count -lt 50) { break }
                $page++
            }
        } catch { Write-Host "WARNING: API: $_" }
        $changedCount = 0
        if ($allCommits.Count -gt 0) {
            $oldest = $allCommits[-1]
            $baseSha = if ($oldest.parents -and $oldest.parents.Count -gt 0) { $oldest.parents[0].sha } else { $oldest.sha }
            try {
                $diffResp = Invoke-WebRequest -Uri "$RepoApi/compare/$baseSha...main" -UseBasicParsing -TimeoutSec 15
                $diffJson = $utf8.GetString($diffResp.RawContentStream.ToArray())
                $diff = $diffJson | ConvertFrom-Json
                if ($diff.files) {
                    foreach ($f in $diff.files) {
                        $rp = $f.filename
                        $fileName = Split-Path $rp -Leaf
                        if ($rp -eq "Server/DfoServer/Data/inventory.db") { continue }
                        if ($fileName -eq "start-server.bat" -or $fileName -eq "start-server.sh") {
                            Write-Host "$(T 's_skip')$rp $(T 's_prot')"
                            continue
                        }
                        $sf = Join-Path $srcPath $rp; $df = Join-Path $SrcRoot $rp
                        if ($f.status -eq "removed") { if (Test-Path $df) { Remove-Item $df -Force } }
                        elseif (Test-Path $sf) {
                            $dd = Split-Path $df -Parent
                            if (-not (Test-Path $dd)) { New-Item -ItemType Directory -Path $dd -Force | Out-Null }
                            Copy-Item $sf $df -Force; $changedCount++
                        }
                    }
                }
            } catch { Write-Host "WARNING: Compare API: $_" }
        }
        if ($changedCount -eq 0) {
            Write-Host (T "s_fallback")
            robocopy "$srcPath" "$SrcRoot" /E /COPY:DAT /DCOPY:T /R:3 /W:2 /XD ".git" /XF "inventory.db" "inventory.db.bak" "start-server.bat" "start-server.sh" /NP /NDL 2>&1 | Out-Null
        } else {
            Write-Host "Updated $changedCount file(s) from $($allCommits.Count) recent commits."
        }
    }

    # 安全网：robocopy 兜底确保所有新增/变更文件都被同步（防止 Compare API 遗漏部分文件导致编译失败）
    Write-Host "Safety sync: ensuring all source files are complete..."
    robocopy "$srcPath" "$SrcRoot" /E /COPY:DAT /DCOPY:T /R:1 /W:1 /XD ".git" /XF "inventory.db" "inventory.db.bak" "start-server.bat" "start-server.sh" /NP /NDL 2>&1 | Out-Null
    Write-Host "Safety check done."

    if ($dbExisted) {
        Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force
        Write-Host "inventory.db restored."
    }
    Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host ">>> [4/5] Building <<<"    # [4/5] 编译：使用 dotnet publish 将 C# 源码编译为单个可执行文件 DfoServer.exe，发布到 dist 目录
    $dn = Ensure-DotNet10
    $buildOk = $false

    $distDb = Join-Path $SrcRoot "dist\win-x64\Data\inventory.db"
    $distDbBak = Join-Path $SrcRoot "dist\win-x64\Data\inventory.db.tmpbak"

    if (Test-Path $distDb) {
        Copy-Item $distDb $distDbBak -Force
    }

    if ($dn) {
        Write-Host "Compiling (this may take a while)..."
        & $dn publish "$SrcRoot\Server\DfoServer\DfoServer.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "dist\win-x64" 2>&1 | Select-Object -Last 5
        if ($LASTEXITCODE -eq 0) {
            $exe = Get-Item "dist\win-x64\DfoServer.exe" -ErrorAction SilentlyContinue
            if ($exe) {
                Write-Host "OK - DfoServer.exe ($([math]::Round($exe.Length/1MB,2)) MB)"
                $buildOk = $true
            } else {
                Write-Host "WARNING: DfoServer.exe not found after build."
            }
        } else { Write-Host "Build failed. Check errors above." }
    } else { Write-Host "Could not obtain .NET SDK. Skipping build." }

    if (Test-Path $distDbBak) {
        Copy-Item $distDbBak $distDb -Force
        Remove-Item $distDbBak -Force
        Write-Host "Restored dist inventory.db"
    }

    $checkFiles = @(    # 编译后补充检查：确保 SQL 模式文件和配置文件也被复制到 dist 发布目录
        @{src="Server\DfoServer\Sqlite\item_schema.sql"; dst="dist\win-x64\Sqlite\item_schema.sql"},
        @{src="Server\DfoServer\channel_info.etc"; dst="dist\win-x64\channel_info.etc"}
    )
    foreach ($cf in $checkFiles) {
        $dp = Join-Path $SrcRoot $cf.dst
        if (-not (Test-Path $dp)) {
            $sp = Join-Path $SrcRoot $cf.src
            if (Test-Path $sp) {
                $dd = Split-Path $dp -Parent
                if (-not (Test-Path $dd)) { New-Item -ItemType Directory -Path $dd -Force | Out-Null }
                Copy-Item $sp $dp -Force
                Write-Host "Fixed: copied $($cf.dst) from source"
            }
        }
    }

    Write-Host ""
    Write-Host ">>> GM Tool Sync & Build <<<"
    Write-Host "在执行更新的时候，会同步更新GM工具"
    $gmRepo = "https://codeberg.org/rewio/DfoGmTool"
    $gmDir = Join-Path $ScriptRoot "dfogmtool"
    $gmExtract = Join-Path $env:TEMP "ServerS4A12-gmtool"
    $gmBuildOk = $false

    if ($dn) {
        try {
            if (Test-Path $gmExtract) { Remove-Item -Recurse -Force $gmExtract }
            New-Item -ItemType Directory -Path $gmExtract -Force | Out-Null
            $gmZip = Join-Path $gmExtract "main.zip"
            $gmSrcDir = Join-Path $gmExtract "extract"

            Write-Host "Downloading GM tool source..."
            Invoke-WebRequest -Uri "$gmRepo/archive/main.zip" -OutFile $gmZip -UseBasicParsing

            if (-not (Test-Path $gmZip)) {
                Write-Host "WARNING: GM tool source download failed."
            } else {
                Expand-Archive -Path $gmZip -DestinationPath $gmSrcDir -Force
                $gmSrc = Get-ChildItem -Path $gmSrcDir -Directory | Select-Object -First 1

                if (-not $gmSrc) {
                    Write-Host "WARNING: GM tool source extraction failed."
                } else {
                    Write-Host "Syncing GM tool files..."
                    robocopy "$($gmSrc.FullName)" $gmDir /E /COPY:DAT /DCOPY:T /R:1 /W:1 /XD ".git" /NP /NDL 2>&1 | Out-Null

                    Write-Host "Compiling GM tool (this may take a while)..."
                    & $dn publish "$gmDir\DfoGmTool.csproj" -c Release -r win-x64 --self-contained true -o "$gmDir\publish" 2>&1 | Select-Object -Last 3

                    if ($LASTEXITCODE -eq 0) {
                        $gmExe = Get-Item "$gmDir\publish\DfoGmTool.exe" -ErrorAction SilentlyContinue
                        if ($gmExe) {
                            Write-Host "OK - DfoGmTool.exe ($([math]::Round($gmExe.Length/1MB,2)) MB)"
                            $gmBuildOk = $true
                        } else {
                            Write-Host "WARNING: DfoGmTool.exe not found after build."
                        }
                    } else { Write-Host "GM tool build failed. Check errors above." }
                }
            }
        } catch {
            Write-Host "WARNING: GM tool update failed: $_"
        }

        if (Test-Path $gmExtract) { Remove-Item -Recurse -Force $gmExtract -ErrorAction SilentlyContinue }
    } else { Write-Host "No .NET SDK, skipping GM tool build." }

    Write-Host ""
    Write-Host ">>> [5/5] Commit log <<<"    # [5/5] 提交日志：从 Codeberg API 拉取所有 commit 历史，按日期分组，写入日志文件
    $allGrouped = @{}
    try {
        $page = 1; $perPage = 50
        while ($true) {
            $resp = Invoke-WebRequest -Uri "$RepoApi/commits?sha=main&limit=$perPage&page=$page" -UseBasicParsing -TimeoutSec 15
            $json = $utf8.GetString($resp.RawContentStream.ToArray())
            $list = $json | ConvertFrom-Json
            if (-not $list -or $list.Count -eq 0) { break }
            foreach ($c in $list) {
                $d = ToChinaDate $c.commit.committer.date
                $t = $c.commit.message.Split("`n")[0].Trim()
                if ($t.Length -gt 120) { $t = $t.Substring(0,117)+"..." }
                if (-not $allGrouped.Contains($d)) { $allGrouped[$d] = @() }
                $allGrouped[$d] += $t
            }
            if ($list.Count -lt $perPage) { break }
            $page++
        }
    } catch { Write-Host "WARNING: $_" }

    $sortedDates = $allGrouped.Keys | Sort-Object -Descending    # 按日期降序排列（新日期在上，旧日期在下）——用于写入日志文件
    $sortedDatesAsc = $allGrouped.Keys | Sort-Object    # 按日期升序排列（旧日期在上，新日期在下）——用于控制台输出
    $totalCommits = 0; foreach ($d in $sortedDates) { $totalCommits += $allGrouped[$d].Count }

    Write-Host ""
    Write-Host "========================================"
    Write-Host "$(T 's_done')$modeText"
    Write-Host "  Version: $currentDate | Commits: $totalCommits"
    if ($buildOk) { Write-Host "  Server Build: OK" } else { Write-Host "  Server Build: Skipped" }
    if ($gmBuildOk) { Write-Host "  GM Tool Build: OK" } else { Write-Host "  GM Tool Build: Skipped" }
    Write-Host "========================================"
    Write-Host ""

    $ver = T "s_ver"; $up = T "s_up"; $total = T "s_total"; $hist = T "s_hist"
    $logLines = [System.Collections.ArrayList]::new()
    [void]$logLines.Add("========================================")
    [void]$logLines.Add($ver + $currentDate)
    [void]$logLines.Add($up + $currentTime)
    [void]$logLines.Add($total + $totalCommits)
    [void]$logLines.Add("方式: " + $modeText)
    $bs = if ($buildOk) { "OK" } else { "Skipped" }
    [void]$logLines.Add("Server Build: " + $bs)
    $gbs = if ($gmBuildOk) { "OK" } else { "Skipped" }
    [void]$logLines.Add("GM Tool Build: " + $gbs)
    [void]$logLines.Add("========================================")
    [void]$logLines.Add(""); [void]$logLines.Add($hist); [void]$logLines.Add("")
    foreach ($d in $sortedDates) {
        [void]$logLines.Add("--- $d ($($allGrouped[$d].Count) commits) ---")
        foreach ($m in $allGrouped[$d]) { [void]$logLines.Add("  $m") }
        [void]$logLines.Add("")
    }
    [void]$logLines.Add("========================================")
    [void]$logLines.Add("")

    $logText = ($logLines -join "`r`n") + "`r`n"
    [System.IO.File]::WriteAllText($LogFile, $logText, (New-Object System.Text.UTF8Encoding $true))

    $sda = (Get-Date).AddDays(-7).ToString("yyyy-MM-dd")
    foreach ($d in $sortedDatesAsc) {    # 控制台输出按日期升序显示（旧日期在上，新日期在下），仅显示最近 7 天的 commit
        if ($d -lt $sda) { continue }
        Write-Host "--- $d ($($allGrouped[$d].Count) commits) ---"
        foreach ($m in $allGrouped[$d]) { Write-Host "  $m" }
        Write-Host ""
    }
    if (($sortedDatesAsc | Where-Object { $_ -lt $sda })) {
        Write-Host "---"
        Write-Host ((T "s_more") + (T "fn_log"))
        Write-Host ((T "s_repo") + "https://codeberg.org/rewio/ServerS4A12/commits/branch/main")
    }

} catch {    # 出错时恢复：尝试还原数据库备份，清理临时目录，然后退出
    Write-Host "ERROR: $_"
    if (Test-Path $DbBackup) { Copy-Item $DbBackup $DbFile -Force -ErrorAction SilentlyContinue; Remove-Item $DbBackup -Force }
    if (Test-Path $TempDir) { Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue }
    exit 1
}