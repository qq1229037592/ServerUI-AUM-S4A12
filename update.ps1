# ============================== ServerS4A12 一键更新脚本 ==============================
# 支持增量更新（默认，只更新最近 3 天变更的文件）和全量同步（-FullSync 开关）
# 全流程：[1/5] 备份数据库 → [2/5] 下载最新源码 → [3/5] 更新文件 → [4/5] 编译 → [5/5] 提交日志
param([switch]$FullSync, [switch]$NonInteractive, [switch]$SkipCommitLog)    # -FullSync：全量同步开关；-NonInteractive：GUI非交互模式；-SkipCommitLog：跳过提交日志拉取

$ErrorActionPreference = "Continue"   # 遇到非致命错误时不中断脚本，继续执行后续步骤
$ScriptRoot = $PSScriptRoot; $SrcRoot = Join-Path $ScriptRoot "ServerS4A12-AUM"    # ScriptRoot=脚本所在目录，SrcRoot=服务器主目录
$RepoApi = "https://gitgud.io/api/v4/projects/rewio%2F86JP"    # gitgud.io (GitLab) 仓库 API 地址
$utf8 = [System.Text.Encoding]::UTF8
$ApiToken = "ggio_Evb_FDif1lUTVAQkw0zKWG86MQp1OjJjZ3gK.01.101gu1kjc"
$ApiHeaders = @{ "PRIVATE-TOKEN" = $ApiToken }

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
$CommitCacheFile = Join-Path $ScriptRoot ".update-cache\commits.json"
$ChinaTZ   = [System.TimeZoneInfo]::FindSystemTimeZoneById("China Standard Time")    # 中国标准时间（UTC+8）时区对象

function ToChinaDate($d) {    # 将 UTC 时间字符串转换为北京时间（UTC+8）的日期格式 yyyy-MM-dd
    $dateStr = "$d"           # 强制转字符串，防止数组干扰
    $dt = [DateTimeOffset]::Parse($dateStr, [System.Globalization.CultureInfo]::InvariantCulture)
    return ([System.TimeZoneInfo]::ConvertTime($dt, $ChinaTZ)).ToString("yyyy-MM-dd")
}

function Download-File($uri, $target) {
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Write-Host "Download attempt $attempt/5: $uri"
            Invoke-WebRequest -Uri $uri -OutFile $target -UseBasicParsing -TimeoutSec 60 -Headers $ApiHeaders
            if ((Test-Path $target) -and (Get-Item $target).Length -gt 1024) { return $true }
            throw "Downloaded file is missing or too small."
        } catch {
            Remove-Item $target -Force -ErrorAction SilentlyContinue
            Write-Host "WARNING: download attempt $attempt failed: $_"
            if ($attempt -lt 5) { Start-Sleep -Seconds ([math]::Pow(2, $attempt - 1)) }
        }
    }
    return $false
}

function Invoke-RepositoryRequest($uri, [int]$MaxAttempts = 2, [int]$TimeoutSec = 8, [switch]$Quiet) {
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            if (-not $Quiet) { Write-Host "[API] Request $attempt/$MaxAttempts" }
            return Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec $TimeoutSec -Headers $ApiHeaders
        } catch {
            Write-Host "WARNING: API attempt $attempt/$MaxAttempts failed: $_"
            if ($attempt -lt $MaxAttempts) {
                $delay = [math]::Pow(2, $attempt - 1)
                Write-Host "[API] Retrying in $delay second(s)..."
                Start-Sleep -Seconds $delay
            }
        }
    }
    Write-Host "WARNING: API unavailable after $MaxAttempts attempts. Falling back to archive-only update."
    return $null
}

function Read-CommitCache {
    if (-not (Test-Path $CommitCacheFile)) { return @() }
    try {
        $data = Get-Content $CommitCacheFile -Raw -Encoding UTF8 | ConvertFrom-Json
        return @($data)
    } catch {
        Write-Host "WARNING: commit cache is invalid and will be rebuilt."
        return @()
    }
}

function Write-CommitCache($commits) {
    $directory = Split-Path $CommitCacheFile -Parent
    if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $commits | ConvertTo-Json -Depth 4 | Set-Content $CommitCacheFile -Encoding UTF8
}

function Sync-CommitHistory {
    $cached = Read-CommitCache
    $known = @{}
    $newestDate = [DateTimeOffset]::MinValue
    foreach ($commit in $cached) {
        $s = "$($commit.Sha)"
        if ($s) {
            $known[$s] = $commit
            try {
                $d = [DateTimeOffset]::Parse("$($commit.Date)")
                if ($d -gt $newestDate) { $newestDate = $d }
            } catch { }
        }
    }

    if ($cached.Count -gt 0 -and $newestDate -ne [DateTimeOffset]::MinValue) {
        $sinceStr = "&since=" + $newestDate.AddSeconds(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
    } else {
        $sinceStr = "&since=" + (Get-Date).AddYears(-3).ToString("yyyy-MM-ddTHH:mm:ssZ")
    }

    $newCount = 0
    $timeBudget = 120; $startTime = Get-Date
    $cpuCores = [Environment]::ProcessorCount
    $maxThreads = [Math]::Max(3, [Math]::Min(16, [Math]::Ceiling($cpuCores * 0.75)))
    $uriBase = "$RepoApi/repository/commits?ref_name=main&per_page=50$sinceStr&page="

    # 阶段1: 先拉第1页 (3次重试)，确认有无数据/是否需要多页
    try {
        $r1 = $null
        for ($a = 1; $a -le 10; $a++) {
            try {
                $r1 = Invoke-WebRequest -Uri ($uriBase + "1") -UseBasicParsing -TimeoutSec 10 -Headers $ApiHeaders
                break
            } catch { if ($a -lt 10) { Start-Sleep 3 } }
        }
        if (-not $r1) { throw "第1页拉取失败 (10次重试后)" }
        $items = $utf8.GetString($r1.RawContentStream.ToArray()) | ConvertFrom-Json
        if ($items.Count -eq 0) { return @{ Commits=@(); Complete=$false; Refreshed=$false } }

        foreach ($item in $items) {
            $sha = $item.id
            if ($known.ContainsKey($sha)) { continue }
            $known[$sha] = [pscustomobject]@{Sha="$sha"; Date="$($item.committed_date)"; Message="$($item.title)"}
            $newCount++
        }
        Write-Host "##PROGRESS##60"

        if ($items.Count -lt 50) {
            $merged = @($known.Values | Sort-Object {[DateTimeOffset]"$($_.Date)"} -Descending)
            if ($newCount -gt 0) { Write-CommitCache $merged }
            Write-Host "[提交日志] 缓存: $($merged.Count) 条 ($newCount 新增)。"
            return @{ Commits=$merged; Complete=$true; Refreshed=($newCount -gt 0) }
        }
    } catch {
        Write-Host "[提交日志] 拉取中断: $_"
        $merged = @($known.Values | Sort-Object {[DateTimeOffset]"$($_.Date)"} -Descending)
        return @{ Commits=$merged; Complete=($merged.Count -gt 0); Refreshed=($newCount -gt 0) }
    }

    # 阶段2: 多页并行拉取 (RunspacePool, 哈希索引防 Remove 引用 bug)
    Write-Host "[提交日志] 并行拉取 ($maxThreads 线程 / $cpuCores 核心)..."
    try {
        $pool = [RunspaceFactory]::CreateRunspacePool(1, $maxThreads)
        $pool.Open()
        $active = @{}           # Page → {PS, Handle}
        $morePages = $true
        $nextPage = 2

        while ($morePages -or $active.Count -gt 0) {
            $elapsed = [math]::Round(((Get-Date) - $startTime).TotalSeconds, 0)
            if ($elapsed -ge $timeBudget) { Write-Host "[提交日志] 已达时间预算"; $morePages = $false }

            # 发射 (填满线程池)
            while ($active.Count -lt $maxThreads -and $morePages -and $elapsed -lt $timeBudget) {
                $pg = $nextPage++
                $ps = [PowerShell]::Create(); $ps.RunspacePool = $pool
                [void]$ps.AddScript({
                    param($u, $tok)
                    $enc = [System.Text.Encoding]::UTF8
                    $hdrs = @{ "PRIVATE-TOKEN" = $tok }
                    for ($a = 1; $a -le 10; $a++) {
                        try {
                            $r = Invoke-WebRequest -Uri $u -Headers $hdrs -UseBasicParsing -TimeoutSec 10
                            $d = $enc.GetString($r.RawContentStream.ToArray()) | ConvertFrom-Json
                            return $d
                        } catch { if ($a -lt 10) { Start-Sleep 3 } }
                    }
                    return $null
                })
                [void]$ps.AddArgument($uriBase + $pg)
                [void]$ps.AddArgument($ApiToken)
                $active[$pg] = @{PS=$ps; Handle=$ps.BeginInvoke()}
            }

            if ($active.Count -eq 0) { break }

            # 收集已完成 (用 key 查找, 不用引用比较)
            $doneKeys = @($active.Keys | Where-Object { $active[$_].Handle.IsCompleted })
            if ($doneKeys.Count -eq 0) { Start-Sleep -Milliseconds 200; continue }

            foreach ($pg in $doneKeys) {
                $task = $active[$pg]
                $items = $task.PS.EndInvoke($task.Handle)
                $task.PS.Dispose()
                $active.Remove($pg)

                if (-not $items -or $items.Count -eq 0) { $morePages = $false; break }
                foreach ($item in $items) {
                    $sha = $item.id
                    if ($known.ContainsKey($sha)) { continue }
                    $known[$sha] = [pscustomobject]@{Sha="$sha"; Date="$($item.committed_date)"; Message="$($item.title)"}
                    $newCount++
                }
                $pp = 60 + [math]::Min(33, [math]::Round(($pg / 11) * 33))
                Write-Host "##PROGRESS##$pp"
                if ($items.Count -lt 50) { $morePages = $false; break }
            }
        }
        $pool.Close()
    } catch {
        Write-Host "[提交日志] 并行拉取中断: $_"
    }

    $merged = @($known.Values | Sort-Object {[DateTimeOffset]"$($_.Date)"} -Descending)
    if ($newCount -gt 0) { Write-CommitCache $merged }
    Write-Host "##PROGRESS##93"
    Write-Host "[提交日志] 缓存: $($merged.Count) 条 ($newCount 新增)。"
    return @{ Commits=$merged; Complete=($merged.Count -gt 0); Refreshed=($newCount -gt 0) }
}

function Test-ZipFile($path) {
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
        $valid = $zip.Entries.Count -gt 0
        $zip.Dispose()
        return $valid
    } catch { return $false }
}

function Sync-SourceFiles($from, $to) {
    $changes = 0
    $srcList = [System.Collections.ArrayList]::new()
    $buildCount = 0; $dataCount = 0; $otherCount = 0
    Get-ChildItem $from -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($from.Length).TrimStart('\')
        if ($relative -match '(^|\\)(\.git|dist)(\\|$)') { return }
        if ($relative -match '(^|\\)inventory\.db(\.bak)?$') { return }
        if ($relative -match '(^|\\)start-server\.(bat|sh)$') { return }
        $destination = Join-Path $to $relative
        $existing = Get-Item $destination -ErrorAction SilentlyContinue
        # Level 1: size and timestamp. Matching metadata can skip hashing entirely.
        if ($existing -and $existing.Length -eq $_.Length -and $existing.LastWriteTimeUtc -eq $_.LastWriteTimeUtc) { return }

        # Level 2: metadata differs, so compare SHA-256 and avoid copying identical content.
        $sameContent = $false
        if ($existing) {
            $sourceHash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
            $destinationHash = (Get-FileHash $destination -Algorithm SHA256).Hash
            $sameContent = $sourceHash -eq $destinationHash
        }
        if ($sameContent) { return }

        # Level 3: content differs. Retry write and verify the copied SHA-256 each time.
        $updated = $false
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                $directory = Split-Path $destination -Parent
                if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
                Copy-Item $_.FullName $destination -Force
                $sourceHash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
                $destinationHash = (Get-FileHash $destination -Algorithm SHA256).Hash
                if ($sourceHash -ne $destinationHash) { throw "SHA-256 verification failed." }
                [System.IO.File]::SetLastWriteTimeUtc($destination, $_.LastWriteTimeUtc)
                $updated = $true
                break
            } catch {
                Write-Host "WARNING: file sync $attempt/3 failed for ${relative}: $_"
                if ($attempt -lt 3) { Start-Sleep -Seconds ([math]::Pow(2, $attempt - 1)) }
            }
        }
        if ($updated) {
            $action = if ($existing) { "更新" } else { "下载" }
            $detail = "$action $relative | $($_.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'))"
            $ext = [System.IO.Path]::GetExtension($relative).ToLower()
            $name = [System.IO.Path]::GetFileName($relative).ToLower()

            # 分类: 源码展开 | 构建/数据/其他 汇总
            if ($ext -in @('.cs','.csproj','.sln','.sql','.xml','.json','.etc','.bat','.sh','.ps1','.md','.txt','.yml','.yaml') -or
                $name -in @('app.manifest','.gitignore','.gitattributes')) {
                Write-Host "[FILE:CS] $detail"
            } elseif ($ext -in @('.dll','.exe','.pdb','.so','.dylib','.lib','.a','.pch','.obj','.ilk','.exp')) {
                $buildCount++
            } elseif ($ext -in @('.db','.db-bak','.db-wal','.db-shm','.pvf')) {
                $dataCount++
            } else {
                $otherCount++
            }
            [void]$srcList.Add($detail)
            $changes++
        } else {
            Write-Host "ERROR: skipped $relative after 3 failed write attempts."
        }
    }

    # 汇总行
    if ($buildCount -gt 0) { Write-Host "[FILE:SUM] 构建产物: ${buildCount}个文件重新生成" }
    if ($dataCount -gt 0)  { Write-Host "[FILE:SUM] 数据文件: ${dataCount}个已同步" }
    if ($otherCount -gt 0) { Write-Host "[FILE:SUM] 其他文件: ${otherCount}个变更" }

    # 全量详情落盘
    if ($srcList.Count -gt 0) {
        $logPath = Join-Path $to "..\文件变更日志.txt"
        try {
            $content = "========================================`r`n文件变更日志 - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`r`n========================================`r`n`r$n"
            foreach ($s in $srcList) { $content += "  $s`r`n" }
            $content += "`r`n========================================`r`n"
            [System.IO.File]::WriteAllText($logPath, $content, $utf8)
        } catch { }
    }

    return $changes
}

function Remove-StaleSourceFiles($from, $to) {
    $removed = 0
    # Only remove stale C# source files from repository-managed code directories.
    # Runtime data, inventory.db, launch scripts, and user files are never considered here.
    foreach ($folder in @("Server", "Tool")) {
        $localFolder = Join-Path $to $folder
        if (-not (Test-Path $localFolder)) { continue }
        Get-ChildItem $localFolder -File -Recurse -Filter "*.cs" | ForEach-Object {
            $relative = $_.FullName.Substring($to.Length).TrimStart('\')
            if ($relative -match '(^|\\)(bin|obj)(\\|$)') { return }
            $sourcePath = Join-Path $from $relative
            if (-not (Test-Path $sourcePath)) {
                Remove-Item $_.FullName -Force
                Write-Host "[FILE] 删除过期源码 $relative"
                $removed++
            }
        }
    }
    return $removed
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
    Write-Host "  .NET 10 SDK is required. Click 安装SDK in ServerUI, finish the Microsoft installer, then restart ServerUI."
    return $null
}

$buildOk = $false; $gmBuildOk = $false

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
    Write-Host ">>> [2/5] Downloading source <<<"
    if (Test-Path $TempDir) { Remove-Item -Recurse -Force $TempDir }
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null
    $TempZip = Join-Path $TempDir "main.zip"
    $TempExtract = Join-Path $TempDir "extract"
    $ProgressPreference = "SilentlyContinue"

    # GM 临时目录
    $gmTempDir = Join-Path $env:TEMP "ServerS4A12-gmupdate"
    if (Test-Path $gmTempDir) { Remove-Item -Recurse -Force $gmTempDir }
    New-Item -ItemType Directory -Path $gmTempDir -Force | Out-Null
    $gmTempZip = Join-Path $gmTempDir "main.zip"
    $gmTempExtract = Join-Path $gmTempDir "extract"
    $gmRepo = "https://codeberg.org/rewio/DfoGmTool"

    # 并行下载: 服务端 + GM (5次重试 + 指数退避)
    $pool = [RunspaceFactory]::CreateRunspacePool(1, 2); $pool.Open()

    $svrPS = [PowerShell]::Create(); $svrPS.RunspacePool = $pool
    [void]$svrPS.AddScript({
        param($u, $t, $tok)
        Remove-Item $t -Force -ErrorAction SilentlyContinue
        $h = @{ "PRIVATE-TOKEN" = $tok }
        for ($a = 1; $a -le 5; $a++) {
            try {
                Invoke-WebRequest -Uri $u -OutFile $t -Headers $h -UseBasicParsing -TimeoutSec 60
                if ((Test-Path $t) -and (Get-Item $t).Length -gt 51200) { return $true }
                Remove-Item $t -Force -ErrorAction SilentlyContinue
            } catch { Remove-Item $t -Force -ErrorAction SilentlyContinue }
            if ($a -lt 5) { Start-Sleep -Seconds ([math]::Pow(2, $a - 1)) }
        }
        return $false
    })
    [void]$svrPS.AddArgument("https://gitgud.io/api/v4/projects/rewio%2F86JP/repository/archive.zip?sha=main")
    [void]$svrPS.AddArgument($TempZip)
    [void]$svrPS.AddArgument($ApiToken)
    $svrHandle = $svrPS.BeginInvoke()

    $gmPS = [PowerShell]::Create(); $gmPS.RunspacePool = $pool
    [void]$gmPS.AddScript({
        param($u, $t)
        Remove-Item $t -Force -ErrorAction SilentlyContinue
        for ($a = 1; $a -le 5; $a++) {
            try {
                Invoke-WebRequest -Uri $u -OutFile $t -UseBasicParsing -TimeoutSec 60
                if ((Test-Path $t) -and (Get-Item $t).Length -gt 10240) { return $true }
                Remove-Item $t -Force -ErrorAction SilentlyContinue
            } catch { Remove-Item $t -Force -ErrorAction SilentlyContinue }
            if ($a -lt 5) { Start-Sleep -Seconds ([math]::Pow(2, $a - 1)) }
        }
        return $false
    })
    [void]$gmPS.AddArgument("$gmRepo/archive/main.zip")
    [void]$gmPS.AddArgument($gmTempZip)
    $gmHandle = $gmPS.BeginInvoke()

    $svrOk = $svrPS.EndInvoke($svrHandle); $svrPS.Dispose()
    $svrSize = if ($svrOk -and (Test-Path $TempZip)) { "$([math]::Round((Get-Item $TempZip).Length/1KB)) KB" } else { "N/A" }
    Write-Host "Server download: $(if($svrOk){'OK'}else{'FAILED'}) ($svrSize)"

    $gmOk = $gmPS.EndInvoke($gmHandle); $gmPS.Dispose()
    Write-Host "GM download: $(if($gmOk){'OK'}else{'FAILED'})"
    $pool.Close()

    if (-not $svrOk) {
        Write-Host "ERROR: Server source download failed."
        if ($dbExisted) { Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force }
        exit 1
    }

    # 解压服务端 + GM
    Write-Host "Extracting..."
    try { Expand-Archive -Path $TempZip -DestinationPath $TempExtract -Force }
    catch { Write-Host "ERROR: Server extraction failed: $_"; exit 1 }
    $srcDir = Get-ChildItem -Path $TempExtract -Directory | Select-Object -First 1
    if (-not $srcDir) { Write-Host "ERROR: Server extraction failed."; exit 1 }
    $srcPath = $srcDir.FullName

    if ($gmOk) {
        try {
            Expand-Archive -Path $gmTempZip -DestinationPath $gmTempExtract -Force
            $gmSrcDir = Get-ChildItem -Path $gmTempExtract -Directory | Select-Object -First 1
            if ($gmSrcDir) { $gmSrcPath = $gmSrcDir.FullName }
        } catch { Write-Host "GM extraction failed: $_"; $gmOk = $false }
    }

    Write-Host ""
    Write-Host "$(T 's_updating')$modeText) <<<"
    try { Expand-Archive -Path $TempZip -DestinationPath $TempExtract -Force }
    catch { Write-Host "ERROR: Extraction failed: $_"; if ($dbExisted) { Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force }; exit 1 }
    $srcDir = Get-ChildItem -Path $TempExtract -Directory | Select-Object -First 1
    if (-not $srcDir) {
        Write-Host "ERROR: Extraction failed."
        if ($dbExisted) { Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force }
        exit 1
    }
    $srcPath = $srcDir.FullName

    # The archive is the source of truth. Avoid slow compare/commit API calls here;
    # Sync-SourceFiles performs the three-level file verification below.
    if ($FullSync) { Write-Host (T "s_fullsync") }
    else { Write-Host "Incremental mode: archive sync will update only content that changed." }

    # 同步: Server + GM 并行; 无GM时仅 Server
    if ($gmOk -and $gmSrcPath) {
        $gmDir = Join-Path $ScriptRoot "dfogmtool"
        if (-not (Test-Path $gmDir)) { New-Item -ItemType Directory -Path $gmDir -Force | Out-Null }
        Write-Host "Parallel sync: server + GM source..."

        $pool2 = [RunspaceFactory]::CreateRunspacePool(1, 2); $pool2.Open()

        $syncSvr = [PowerShell]::Create(); $syncSvr.RunspacePool = $pool2
        [void]$syncSvr.AddScript({
            param($from, $to)
            $ch = 0; $st = 0
            Get-ChildItem $from -File -Recurse | ForEach-Object {
                $relative = $_.FullName.Substring($from.Length).TrimStart('\')
                if ($relative -match '(^|\\)(\.git|dist)(\\|$)') { return }
                if ($relative -match '(^|\\)inventory\.db(\.bak)?$') { return }
                if ($relative -match '(^|\\)start-server\.(bat|sh)$') { return }
                $dst = Join-Path $to $relative
                $exist = Get-Item $dst -ErrorAction SilentlyContinue
                if ($exist -and $exist.Length -eq $_.Length -and $exist.LastWriteTimeUtc -eq $_.LastWriteTimeUtc) { return }
                $same = $false
                if ($exist) {
                    $sh = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
                    $dh = (Get-FileHash $dst -Algorithm SHA256).Hash
                    $same = ($sh -eq $dh)
                }
                if ($same) { return }
                $dir = Split-Path $dst -Parent
                if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
                Copy-Item $_.FullName $dst -Force
                [System.IO.File]::SetLastWriteTimeUtc($dst, $_.LastWriteTimeUtc)
                $ch++
            }
            # Remove stale
            foreach ($folder in @("Server","Tool")) {
                $lf = Join-Path $to $folder
                if (-not (Test-Path $lf)) { continue }
                Get-ChildItem $lf -File -Recurse -Filter "*.cs" | ForEach-Object {
                    $rel = $_.FullName.Substring($to.Length).TrimStart('\')
                    if ($rel -match '(^|\\)(bin|obj)(\\|$)') { return }
                    if (-not (Test-Path (Join-Path $from $rel))) { Remove-Item $_.FullName -Force; $st++ }
                }
            }
            return "$ch updated, $st stale removed"
        })
        [void]$syncSvr.AddArgument($srcPath); [void]$syncSvr.AddArgument($SrcRoot)
        $syncSvrHandle = $syncSvr.BeginInvoke()

        $syncGm = [PowerShell]::Create(); $syncGm.RunspacePool = $pool2
        [void]$syncGm.AddScript({
            param($from, $to)
            $ch = 0; $st = 0
            Get-ChildItem $from -File -Recurse | ForEach-Object {
                $relative = $_.FullName.Substring($from.Length).TrimStart('\')
                if ($relative -match '(^|\\)(\.git|dist)(\\|$)') { return }
                $dst = Join-Path $to $relative
                $exist = Get-Item $dst -ErrorAction SilentlyContinue
                if ($exist -and $exist.Length -eq $_.Length -and $exist.LastWriteTimeUtc -eq $_.LastWriteTimeUtc) { return }
                $same = $false
                if ($exist) {
                    if ((Get-FileHash $_.FullName -Algorithm SHA256).Hash -eq (Get-FileHash $dst -Algorithm SHA256).Hash) { return }
                }
                $dir = Split-Path $dst -Parent
                if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
                Copy-Item $_.FullName $dst -Force
                [System.IO.File]::SetLastWriteTimeUtc($dst, $_.LastWriteTimeUtc)
                $ch++
            }
            Get-ChildItem $to -File -Recurse -Filter "*.cs" | ForEach-Object {
                $rel = $_.FullName.Substring($to.Length).TrimStart('\')
                if ($rel -match '(^|\\)(bin|obj)(\\|$)') { return }
                if (-not (Test-Path (Join-Path $from $rel))) { Remove-Item $_.FullName -Force; $st++ }
            }
            return "$ch updated, $st stale removed"
        })
        [void]$syncGm.AddArgument($gmSrcPath); [void]$syncGm.AddArgument($gmDir)
        $syncGmHandle = $syncGm.BeginInvoke()

        $safetyChanges = $syncSvr.EndInvoke($syncSvrHandle); $syncSvr.Dispose()
        Write-Host "Server sync: $safetyChanges"

        $gmChanges = $syncGm.EndInvoke($syncGmHandle); $syncGm.Dispose()
        Write-Host "GM sync: $gmChanges"

        $pool2.Close()
        Remove-Item -Recurse -Force $gmTempDir -ErrorAction SilentlyContinue
    } else {
        # 单 Server 同步
        $safetyChanges = Sync-SourceFiles $srcPath $SrcRoot
        $staleRemoved = Remove-StaleSourceFiles $srcPath $SrcRoot
        Write-Host "Safety check done: $safetyChanges file(s) updated, $staleRemoved stale removed."
    }

    if ($dbExisted) {
        Copy-Item $DbBackup $DbFile -Force; Remove-Item $DbBackup -Force
        Write-Host "inventory.db restored."
    }
    Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host ">>> [4/5] Building <<<"
    $dn = Ensure-DotNet10
    $buildOk = $false
    $gmBuildOk = $false

    $distDb = Join-Path $SrcRoot "dist\win-x64\Data\inventory.db"
    $distDbBak = Join-Path $SrcRoot "dist\win-x64\Data\inventory.db.tmpbak"
    if (Test-Path $distDb) { Copy-Item $distDb $distDbBak -Force }

    if (-not $dn) {
        Write-Host "Could not obtain .NET SDK. Skipping builds."
    } else {
        $serverProject = Join-Path $SrcRoot "Server\DfoServer\DfoServer.csproj"
        $distDir = Join-Path $SrcRoot "dist\win-x64"
        $serverDir = Split-Path $serverProject -Parent
        if (-not (Test-Path $serverProject)) { throw "Server project not found: $serverProject" }

        # --- GM 预处理 (串行: 停进程) ---
        $gmDir = Join-Path $ScriptRoot "dfogmtool"
        $gmProject = Join-Path $gmDir "DfoGmTool.csproj"
        $gmExePath = Join-Path $gmDir "publish\DfoGmTool.exe"
        if (Test-Path $gmExePath) {
            try { Get-Process -Name "DfoGmTool" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Sleep 1 } catch { }
            Write-Host "Stopped existing GM tool process."
        }

        # 全量保存时间戳 (编译后恢复, 防止 C# 文件检测误报编译产物)
        $tsSave = @{}
        @($SrcRoot, $gmDir) | ForEach-Object {
            Get-ChildItem $_ -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
                $tsSave[$_.FullName] = $_.LastWriteTimeUtc
            }
        }

        # --- 并行编译 (RunspacePool ×2, 严格校验) ---
        Write-Host "Compiling server and GM tool in parallel..."

        $pool = [RunspaceFactory]::CreateRunspacePool(1, 2)
        $pool.Open()

        # 服务端编译
        $svrPS = [PowerShell]::Create(); $svrPS.RunspacePool = $pool
        [void]$svrPS.AddScript({
            param($dotnet, $proj, $outDir)
            $lines = [System.Collections.ArrayList]::new()
            function w($m) { [void]$lines.Add($m) }
            function Run-Cmd($exe, $cmdArgs) {
                $tmp = & $exe @cmdArgs 2>&1
                $ec = $LASTEXITCODE
                $tmp | Out-String | ForEach-Object { w $_.TrimEnd() }
                return $ec
            }
            w "Server build: .NET SDK $(& $dotnet --version)"
            if (-not (Test-Path $proj)) { w "ERROR: project not found: $proj"; return [pscustomobject]@{Ok=$false; Log=$lines} }
            $projDir = Split-Path $proj -Parent
            Set-Location $projDir

            # 还原
            $rc = Run-Cmd $dotnet @("restore", $proj, "--ignore-failed-sources")
            if ($rc -ne 0) { w "ERROR: restore failed (exit $rc)"; return [pscustomobject]@{Ok=$false; Log=$lines} }

            # 第1次编译
            $rc = Run-Cmd $dotnet @("publish", $proj, "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-o", $outDir)
            if ($rc -ne 0) {
                w "Retry: rebuilding..."
                $rc = Run-Cmd $dotnet @("publish", $proj, "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-o", $outDir)
            }

            # 校验产物
            if ($rc -ne 0) { w "ERROR: publish failed (exit $rc)"; return [pscustomobject]@{Ok=$false; Log=$lines} }
            $exe = Join-Path $outDir "DfoServer.exe"
            if (-not (Test-Path $exe)) { w "ERROR: DfoServer.exe not found at $exe"; return [pscustomobject]@{Ok=$false; Log=$lines} }
            $size = (Get-Item $exe).Length
            if ($size -le 0) { w "ERROR: DfoServer.exe is empty"; return [pscustomobject]@{Ok=$false; Log=$lines} }
            w "OK - DfoServer.exe ($([math]::Round($size/1MB,2)) MB)"
            return [pscustomobject]@{Ok=$true; Log=$lines}
        })
        [void]$svrPS.AddArgument($dn); [void]$svrPS.AddArgument($serverProject); [void]$svrPS.AddArgument($distDir)
        $svrHandle = $svrPS.BeginInvoke()

        # GM 编译
        $gmPS = [PowerShell]::Create(); $gmPS.RunspacePool = $pool
        [void]$gmPS.AddScript({
            param($dotnet, $proj, $gmDirPath)
            $lines = [System.Collections.ArrayList]::new()
            function w($m) { [void]$lines.Add($m) }
            function Run-Cmd($exe, $cmdArgs) {
                $tmp = & $exe @cmdArgs 2>&1
                $ec = $LASTEXITCODE
                $tmp | Out-String | ForEach-Object { w $_.TrimEnd() }
                return $ec
            }
            w "GM build: .NET SDK $(& $dotnet --version)"
            if (-not (Test-Path $proj)) { w "GM project not found, skipping."; return [pscustomobject]@{Ok=$false; Log=$lines} }
            $projDir = Split-Path $proj -Parent
            Set-Location $projDir

            # 还原
            $rc = Run-Cmd $dotnet @("restore", $proj, "--ignore-failed-sources")
            if ($rc -ne 0) { w "WARNING: GM restore failed (exit $rc)"; return [pscustomobject]@{Ok=$false; Log=$lines} }

            # 编译
            $pubDir = Join-Path $gmDirPath "publish"
            $rc = Run-Cmd $dotnet @("publish", $proj, "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-o", $pubDir)
            if ($rc -ne 0) {
                w "Retry: rebuilding..."
                $rc = Run-Cmd $dotnet @("publish", $proj, "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-o", $pubDir)
            }

            # 校验产物
            if ($rc -ne 0) { w "WARNING: GM publish failed (exit $rc)"; return [pscustomobject]@{Ok=$false; Log=$lines} }
            $exe = Join-Path $pubDir "DfoGmTool.exe"
            if (-not (Test-Path $exe)) { w "WARNING: DfoGmTool.exe not found"; return [pscustomobject]@{Ok=$false; Log=$lines} }
            $size = (Get-Item $exe).Length
            if ($size -le 0) { w "WARNING: DfoGmTool.exe is empty"; return [pscustomobject]@{Ok=$false; Log=$lines} }
            w "OK - DfoGmTool.exe ($([math]::Round($size/1MB,2)) MB)"
            return [pscustomobject]@{Ok=$true; Log=$lines}
        })
        [void]$gmPS.AddArgument($dn); [void]$gmPS.AddArgument($gmProject); [void]$gmPS.AddArgument($gmDir)
        $gmHandle = $gmPS.BeginInvoke()

        # 等待服务端先完成
        $svrResult = $svrPS.EndInvoke($svrHandle); $svrPS.Dispose()
        Write-Host ($svrResult.Log -join "`n")
        $buildOk = $svrResult.Ok

        # 等待 GM
        $gmResult = $gmPS.EndInvoke($gmHandle); $gmPS.Dispose()
        Write-Host ($gmResult.Log -join "`n")
        $gmBuildOk = $gmResult.Ok

        $pool.Close()
    }

    # 恢复时间戳 (防止 C# 文件变更检测误报编译产物)
    foreach ($path in $tsSave.Keys) {
        if (Test-Path $path) {
            try { [System.IO.File]::SetLastWriteTimeUtc($path, $tsSave[$path]) } catch { }
        }
    }

    # 恢复 dist DB + 补充检查 (串行)
    if (Test-Path $distDbBak) {
        Copy-Item $distDbBak $distDb -Force; Remove-Item $distDbBak -Force
        Write-Host "Restored dist inventory.db"
    }
    $checkFiles = @(
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

    if (-not $buildOk) {
        Write-Host "ERROR: Update files were synchronized but the server build did not succeed."
        exit 1
    }

} catch {
    Write-Host "ERROR: $_"
    if (Test-Path $DbBackup) { Copy-Item $DbBackup $DbFile -Force -ErrorAction SilentlyContinue; Remove-Item $DbBackup -Force }
    if (Test-Path $TempDir) { Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue }
    exit 1
} finally {
    Write-Host ""
    if ($SkipCommitLog) {
        Write-Host ">>> [5/5] 【已跳过更新日志拉取 — 由用户设置】 <<<"
    } else {
        Write-Host ">>> [5/5] 【正在获取仓库更新日志中，速度较慢，请等待】 <<<"
    }
    if (-not $currentDate)  { $currentDate  = Get-Date -Format "yyyy-MM-dd" }
    if (-not $currentTime)  { $currentTime  = Get-Date -Format "yyyy-MM-dd HH:mm:ss" }
    if (-not $modeText)     { $modeText     = if ($FullSync) { T "s_full" } else { T "s_inc" } }

    $allGrouped = @{}
    if (-not $SkipCommitLog) {
        $history = Sync-CommitHistory
        foreach ($c in @($history.Commits)) {
            try {
                $d = ToChinaDate $c.Date
                $message = "$($c.Message)".Split("`n")[0].Trim()
                if ($message.Length -gt 120) { $message = $message.Substring(0,117)+"..." }
                if (-not $allGrouped.Contains($d)) { $allGrouped[$d] = @() }
                $allGrouped[$d] += $message
            } catch { }
        }

    # 严谨判定: 优化方案确实失败 (0 条数据) → 启动旧版纯直连方案
    if ($allGrouped.Count -eq 0) {
        Write-Host "[提交日志] 优化方案无数据，切换旧版可靠方案 (直接 API 全量拉取)..."
        try {
            $page = 1; $perPage = 50; $fbSince = (Get-Date).AddYears(-3).ToString("yyyy-MM-ddTHH:mm:ssZ")
            while ($true) {
                $resp = $null
                for ($a = 1; $a -le 10; $a++) {
                    try {
                        $resp = Invoke-WebRequest -Uri "$RepoApi/repository/commits?ref_name=main&per_page=$perPage&page=$page&since=$fbSince" -Headers $ApiHeaders -UseBasicParsing -TimeoutSec 15
                        break
                    } catch { if ($a -lt 10) { Start-Sleep 1 } }
                }
                if (-not $resp) { throw "兜底拉取第${page}页失败 (10次重试后)" }
                $list = $utf8.GetString($resp.RawContentStream.ToArray()) | ConvertFrom-Json
                if ($list.Count -eq 0) { break }
                foreach ($c in $list) {
                    $cd = "$($c.committed_date)"; $msg = "$($c.title)"
                    $d = ToChinaDate $cd
                    $t = $msg.Split("`n")[0].Trim()
                    if ($t.Length -gt 120) { $t = $t.Substring(0,117)+"..." }
                    if (-not $allGrouped.Contains($d)) { $allGrouped[$d] = @() }
                    $allGrouped[$d] += $t
                }
                if ($list.Count -lt $perPage) { break }
                $page++
            }
        } catch {
            Write-Host "[提交日志] 旧版方案也失败: $_"
        }
    }
    }

    $sortedDates = $allGrouped.Keys | Sort-Object -Descending
    $sortedDatesAsc = $allGrouped.Keys | Sort-Object
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
        foreach ($m in $allGrouped[$d]) {
            $tt = if ($m.Length -gt 120) { $m.Substring(0,117)+"..." } else { $m }
            [void]$logLines.Add("  $tt")
        }
        [void]$logLines.Add("")
    }
    [void]$logLines.Add("========================================")
    [void]$logLines.Add("")

    $logText = ($logLines -join "`r`n") + "`r`n"
    [System.IO.File]::WriteAllText($LogFile, $logText, (New-Object System.Text.UTF8Encoding $true))
    Write-Host "[提交日志] 已输出 更新日志.txt ($totalCommits 条提交)"

    $sda = (Get-Date).AddDays(-7).ToString("yyyy-MM-dd")
    foreach ($d in $sortedDatesAsc) {
        if ($d -lt $sda) { continue }
        Write-Host "--- $d ($($allGrouped[$d].Count) commits) ---"
        foreach ($m in $allGrouped[$d]) {
            $tt = if ($m.Length -gt 120) { $m.Substring(0,117)+"..." } else { $m }
            Write-Host "  $tt"
        }
        Write-Host ""
    }
    if (($sortedDatesAsc | Where-Object { $_ -lt $sda })) {
        Write-Host "---"
        Write-Host ((T "s_more") + (T "fn_log"))
        Write-Host ((T "s_repo") + "https://gitgud.io/rewio/86JP/-/commits/main")
    }
}
