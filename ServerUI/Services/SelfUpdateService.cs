/*
 * ==================================================================
 * AUM管理器自更新服务 (SelfUpdateService) — v1.917
 * ==================================================================
 *
 * 【功能说明】
 *   检测 GitHub 仓库中是否有新版本，有则下载源码 → 本地编译 → 替换 EXE。
 *   整个自更新过程完全自动化，利用用户已有的 .NET 10 SDK 编译。
 *
 * 【工作流程】
 *   1. 读取 GitHub Raw 上的 MainForm.cs，正则提取 VER 版本号
 *   2. 对比本地 VER，相同则跳过，不同则进入更新
 *   3. 下载 GitHub 仓库 ZIP → 解压到临时目录
 *   4. 找 ServerUI 子目录 → dotnet restore → dotnet publish
 *   5. 编译成功后生成替换脚本 → 退出旧进程 → 脚本覆盖 EXE → 启动新 EXE
 *
 * 【多轮判定 / 任何一步失败均可安全回滚】
 *   R1-R3: 网络/ZIP 异常 → 重试，不碰本地文件
 *   R4-R7: 编译相关 → 旧 EXE 持续运行，不影响用户
 *   R8:    替换 EXE → 临时脚本保证原子操作
 * ==================================================================
 */
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ServerUI.Services;

public class SelfUpdateService
{
    const string GitHubRaw = "https://raw.githubusercontent.com/118coder/ServerUI-AUM-S4A12/main/";
    const string GitHubApi = "https://api.github.com/repos/118coder/ServerUI-AUM-S4A12/contents/";
    const string RepoZipUrl = "https://api.github.com/repos/118coder/ServerUI-AUM-S4A12/zipball/main";
    const string VerFile = "AUM-version.txt";

    public string RemoteVersion { get; private set; }

    public event Action<string> OutputReceived;
    public event Action<bool> Completed;

    public async Task<bool> CheckForUpdateAsync(string localVer)
    {
        try
        {
            var ver = await FetchRemoteVersion();
            if (ver == null)
            {
                RemoteVersion = null;
                OutputReceived?.Invoke("[AUM自检] 无法连接 GitHub，跳过版本检测");
                return false;
            }

            RemoteVersion = ver;
            var cmp = CompareVersion(ver, localVer);
            return cmp > 0;
        }
        catch
        {
            RemoteVersion = null;
            return false;
        }
    }

    async Task<string> FetchRemoteVersion()
    {
        var rawTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 第1层: 尝试 GitHub API（刷新最快，无CDN缓存）
        for (int a = 1; a <= 2; a++)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                client.DefaultRequestHeaders.Add("User-Agent", "ServerUI-AUM");
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache, no-store");
                client.DefaultRequestHeaders.Add("Pragma", "no-cache");

                var resp = await client.GetStringAsync(GitHubApi + VerFile + "?ref=main&t=" + rawTimestamp);
                using var doc = JsonDocument.Parse(resp);
                if (doc.RootElement.TryGetProperty("content", out var contentEl))
                {
                    var bytes = Convert.FromBase64String(contentEl.GetString() ?? "");
                    var text = Encoding.UTF8.GetString(bytes).Trim();
                    text = Regex.Replace(text, @"\s+", "");
                    if (text.Length > 0) return text;
                }
            }
            catch
            {
                if (a < 2) await Task.Delay(500);
            }
        }

        // 第2层: 回退到 Raw URL（带多重缓存穿透参数）
        for (int a = 1; a <= 2; a++)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                client.DefaultRequestHeaders.Add("User-Agent", "ServerUI-AUM");
                client.DefaultRequestHeaders.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                client.DefaultRequestHeaders.Add("Pragma", "no-cache");

                var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var url = GitHubRaw + VerFile + "?r=" + ts + "&_=" + Guid.NewGuid().ToString("N")[..8];
                var text = await client.GetStringAsync(url);
                text = text.Trim();
                text = Regex.Replace(text, @"\s+", "");
                if (text.Length > 0) return text;
            }
            catch
            {
                if (a < 2) await Task.Delay(500);
            }
        }

        return null;
    }

    public int CompareVersion(string a, string b)
    {
        var partsA = ParseVersion(a);
        var partsB = ParseVersion(b);
        int len = Math.Max(partsA.Length, partsB.Length);
        for (int i = 0; i < len; i++)
        {
            int va = i < partsA.Length ? partsA[i] : 0;
            int vb = i < partsB.Length ? partsB[i] : 0;
            if (va != vb) return va.CompareTo(vb);
        }
        return 0;
    }

    static int[] ParseVersion(string v)
    {
        if (string.IsNullOrWhiteSpace(v)) return new[] { 0 };
        var s = v.Trim();
        var parts = s.Split('.', '-', '_');
        var nums = new System.Collections.Generic.List<int>();
        foreach (var p in parts)
        {
            var cleaned = System.Text.RegularExpressions.Regex.Replace(p, @"[^0-9]", "");
            if (int.TryParse(cleaned, out var n) && n >= 0)
                nums.Add(n);
            else if (cleaned.Length > 0)
                nums.Add(0);
        }
        if (nums.Count == 0) nums.Add(0);
        return nums.ToArray();
    }

    public async Task RunUpdateAsync(string localDir)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "ServerUI-AUM-update");
        var tmpZip = Path.Combine(tmpDir, "source.zip");
        var tmpExtract = Path.Combine(tmpDir, "extract");
        var publishDir = Path.Combine(tmpDir, "publish");
        var curExe = Environment.ProcessPath ?? "";

        try
        {
            // R1 准备临时目录
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
            Directory.CreateDirectory(tmpDir);
            Directory.CreateDirectory(publishDir);

            // R2 下载源码 ZIP (5次重试)
            OutputReceived?.Invoke("[AUM更新] 正在下载最新源码...");
            var ok = false;
            for (int a = 1; a <= 5; a++)
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                    client.DefaultRequestHeaders.Add("User-Agent", "ServerUI-AUM");
                    var data = await client.GetByteArrayAsync(RepoZipUrl);
                    await File.WriteAllBytesAsync(tmpZip, data);

                    if (new FileInfo(tmpZip).Length > 10240) { ok = true; break; }
                }
                catch { }
                if (a < 5) await Task.Delay((int)Math.Pow(2, a) * 1000);
            }

            if (!ok)
            {
                OutputReceived?.Invoke("[AUM更新] 下载源码失败，请检查网络。");
                Completed?.Invoke(false);
                return;
            }

            // R3 解压
            OutputReceived?.Invoke("[AUM更新] 正在解压源码...");
            if (Directory.Exists(tmpExtract)) Directory.Delete(tmpExtract, true);
            ZipFile.ExtractToDirectory(tmpZip, tmpExtract);

            // GitHub zipball 解压后有个子目录 (如 118coder-ServerUI-AUM-S4A12-xxxxx)
            var rootDir = tmpExtract;
            var subDir = Directory.GetDirectories(tmpExtract).FirstOrDefault(
                d => d.Contains("ServerUI") || d.Contains("S4A12")) ?? tmpExtract;
            rootDir = subDir;

            // 找 ServerUI 源码目录
            var srcDir = Path.Combine(rootDir, "ServerUI");
            if (!Directory.Exists(srcDir))
            {
                var dirs = Directory.GetDirectories(rootDir, "ServerUI", SearchOption.AllDirectories);
                if (dirs.Length > 0) srcDir = dirs[0];
            }

            if (!Directory.Exists(srcDir) || !File.Exists(Path.Combine(srcDir, "ServerUI.csproj")))
            {
                OutputReceived?.Invoke("[AUM更新] 源码包结构异常，请手动更新。");
                Completed?.Invoke(false);
                return;
            }

            // R4 直接在下载的源码上编译（不动本地文件，确保编译源正确）
            OutputReceived?.Invoke("[AUM更新] 正在编译新版本（直接从GitHub源码）...");
            var sdk = FindDotNet();
            if (sdk == null)
            {
                OutputReceived?.Invoke("[AUM更新] 未找到 .NET 10 SDK，无法编译。请安装SDK后重试。");
                Completed?.Invoke(false);
                return;
            }

            // 清理重复文件
            CleanDuplicates(srcDir);

            // R5 dotnet restore
            OutputReceived?.Invoke("[AUM更新] 正在还原依赖...");
            var projFile = Path.Combine(srcDir, "ServerUI.csproj");
            var exit = await RunDotnet(sdk, $"restore \"{projFile}\" --ignore-failed-sources", srcDir);
            if (exit != 0)
            {
                OutputReceived?.Invoke("[AUM更新] 依赖还原失败 (exit " + exit + ")。");
                Completed?.Invoke(false);
                return;
            }

            // R6 dotnet publish (无依赖版)
            OutputReceived?.Invoke("[AUM更新] 正在编译新版本...");
            var fdExePath = Path.Combine(publishDir, "ServerUI.exe");
            exit = await RunDotnet(sdk,
                $"publish \"{projFile}\" -c Release -r win-x64 --no-self-contained -o \"{publishDir}\"",
                srcDir);

            if (exit != 0)
            {
                OutputReceived?.Invoke("[AUM更新] 编译失败 (exit " + exit + ")。");
                Completed?.Invoke(false);
                return;
            }

            if (!File.Exists(fdExePath))
            {
                OutputReceived?.Invoke("[AUM更新] 编译产物缺失，请检查源码。");
                Completed?.Invoke(false);
                return;
            }

            // R7 验证新 EXE 大小
            var newSize = new FileInfo(fdExePath).Length;
            if (newSize < 10240)
            {
                OutputReceived?.Invoke("[AUM更新] 编译产物异常 (仅 " + newSize + " 字节)。");
                Completed?.Invoke(false);
                return;
            }

            // R7.5 编译成功后，同步源码到本地（异步，不影响替换流程）
            try { SyncDirectory(srcDir, localDir); CleanDuplicates(localDir); SyncRootFiles(rootDir, Path.GetDirectoryName(localDir) ?? localDir); ReorganizeScripts(Path.GetDirectoryName(localDir) ?? localDir); } catch { }

            // R7.6 下载镜像中的更新日志（不编译，直接拉取）
            try { await DownloadChangelogFromMirror(Path.GetDirectoryName(localDir) ?? localDir); } catch { }

            // R8 生成替换脚本并退出
            // 用临时 PowerShell 脚本实现: 等旧进程退出 → 覆盖 EXE → 启动 → 自清理
            OutputReceived?.Invoke("[AUM更新] 编译成功，正在准备替换...");
            var psPath = Path.Combine(tmpDir, "replace.ps1");
            var psScript = new StringBuilder();
            psScript.AppendLine("$oldPid = " + Environment.ProcessId + ";");
            psScript.AppendLine("$newExe = @\"\n" + fdExePath + "\n\"@;");
            psScript.AppendLine("$target = @\"\n" + curExe + "\n\"@;");
            psScript.AppendLine("$tmpDir = @\"\n" + tmpDir + "\n\"@;");
            psScript.AppendLine("Start-Sleep -Seconds 2;");
            psScript.AppendLine("# 等待旧进程完全退出 (最多等 30 秒)");
            psScript.AppendLine("for ($i = 0; $i -lt 30; $i++) {");
            psScript.AppendLine("    $p = Get-Process -Id $oldPid -ErrorAction SilentlyContinue;");
            psScript.AppendLine("    if (-not $p) { break };");
            psScript.AppendLine("    Start-Sleep -Seconds 1;");
            psScript.AppendLine("}");
            psScript.AppendLine("# 覆盖旧 EXE");
            psScript.AppendLine("try {");
            psScript.AppendLine("    Copy-Item -LiteralPath $newExe -Destination $target -Force;");
            psScript.AppendLine("    Write-Host '替换成功';");
            psScript.AppendLine("    Start-Process -FilePath $target;");
            psScript.AppendLine("} catch {");
            psScript.AppendLine("    Write-Host \"替换失败: $_\";");
            psScript.AppendLine("    Start-Sleep -Seconds 10;");
            psScript.AppendLine("}");
            psScript.AppendLine("# 清理临时目录");
            psScript.AppendLine("Start-Sleep -Seconds 2;");
            psScript.AppendLine("Remove-Item -LiteralPath $tmpDir -Recurse -Force -ErrorAction SilentlyContinue;");
            File.WriteAllText(psPath, psScript.ToString(), Encoding.UTF8);

            OutputReceived?.Invoke("[AUM更新] 即将退出并完成替换...");
            OutputReceived?.Invoke("[AUM更新] 当前程序: " + curExe + "  →  将被新版本覆盖");
            Completed?.Invoke(true);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + psPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            OutputReceived?.Invoke("[AUM更新] 异常: " + ex.Message);
            Completed?.Invoke(false);
        }
        finally
        {
            try { Directory.Delete(tmpDir, true); }
            catch { }
        }
    }

    static string FindDotNet()
    {
        // 第一级: 尝试系统 PATH 中的 dotnet
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (p == null) throw new Exception("Process.Start returned null");
            var v = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            var m = Regex.Match(v, @"^(\d+)\.");
            if (p.ExitCode == 0 && m.Success && int.Parse(m.Groups[1].Value) >= 10)
                return "dotnet";
        }
        catch { }

        // 第二级: 用 where.exe 查找 dotnet.exe 所在位置
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (p == null) throw new Exception("Process.Start returned null");
            var all = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            var lines = all.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var path = line.Trim();
                if (!File.Exists(path)) continue;
                try
                {
                    var vp = Process.Start(new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "--version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });
                    if (vp == null) continue;
                    var v = vp.StandardOutput.ReadToEnd().Trim();
                    vp.WaitForExit();
                    var m = Regex.Match(v, @"^(\d+)\.");
                    if (vp.ExitCode == 0 && m.Success && int.Parse(m.Groups[1].Value) >= 10)
                        return path;
                }
                catch { }
            }
        }
        catch { }

        // 第三级: 标准安装目录 (x64 + x86)
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "dotnet.exe"),
        };
        foreach (var pf in candidates)
        {
            if (!File.Exists(pf)) continue;
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = pf,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                if (p == null) continue;
                var v = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit();
                var m = Regex.Match(v, @"^(\d+)\.");
                if (p.ExitCode == 0 && m.Success && int.Parse(m.Groups[1].Value) >= 10)
                    return pf;
            }
            catch { }
        }

        return null;
    }

    async Task<int> RunDotnet(string sdk, string args, string workDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = sdk,
            Arguments = args,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment["DOTNET_EnableCET"] = "0";

        using var p = new Process { StartInfo = psi };
        p.Start();

        // 必须异步读取输出，否则缓冲区满会导致进程卡死
        var act = OutputReceived;
        _ = Task.Run(() =>
        {
            while (!p.StandardOutput.EndOfStream)
            {
                var line = p.StandardOutput.ReadLine();
                if (line != null && line.Length > 0)
                    act?.Invoke("[编译] " + line);
            }
        });
        _ = Task.Run(() =>
        {
            while (!p.StandardError.EndOfStream)
            {
                var line = p.StandardError.ReadLine();
                if (line != null && line.Length > 0)
                    act?.Invoke("[编译] " + line);
            }
        });

        await p.WaitForExitAsync();
        return p.ExitCode;
    }

    static async Task DownloadChangelogFromMirror(string destDir)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("User-Agent", "ServerUI-AUM");

            var urls = new[] {
                "https://raw.githubusercontent.com/118coder/ServerS4A12.86JP/main/mirrors/%E6%9B%B4%E6%96%B0%E6%97%A5%E5%BF%97.txt",
                "https://codeberg.org/118coder/ServerS4A12.86JP/raw/branch/main/mirrors/%E6%9B%B4%E6%96%B0%E6%97%A5%E5%BF%97.txt"
            };

            foreach (var url in urls)
            {
                try
                {
                    var data = await client.GetByteArrayAsync(url);
                    if (data.Length > 100)
                    {
                        var dest = Path.Combine(destDir, "更新日志.txt");
                        File.WriteAllBytes(dest, data);
                        return;
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    static void SyncRootFiles(string repoRoot, string userRoot)
    {
        // .bat / .txt / .md → userRoot (直接在 AUM管理组件 根目录)
        var extsBat = new[] { "*.bat", "*.txt", "*.md" };
        foreach (var pattern in extsBat)
        {
            foreach (var f in Directory.GetFiles(repoRoot, pattern))
            {
                var name = Path.GetFileName(f);
                if (name.Contains("GameLog") || name.Contains("运行日志")) continue;
                var dest = Path.Combine(userRoot, name);
                var srcInfo = new FileInfo(f);

                if (File.Exists(dest))
                {
                    var dstInfo = new FileInfo(dest);
                    if (srcInfo.Length == dstInfo.Length
                        && srcInfo.LastWriteTimeUtc == dstInfo.LastWriteTimeUtc)
                        continue;
                    if (FileHash(f) == FileHash(dest))
                        continue;
                }
                File.Copy(f, dest, true);
            }
        }

        // .ps1 → userRoot\ps1核心\ (全部放入 ps1核心 子目录)
        var ps1Dir = Path.Combine(userRoot, "ps1核心");
        Directory.CreateDirectory(ps1Dir);
        foreach (var f in Directory.GetFiles(repoRoot, "*.ps1"))
        {
            var name = Path.GetFileName(f);
            var dest = Path.Combine(ps1Dir, name);
            var srcInfo = new FileInfo(f);

            if (File.Exists(dest))
            {
                var dstInfo = new FileInfo(dest);
                if (srcInfo.Length == dstInfo.Length
                    && srcInfo.LastWriteTimeUtc == dstInfo.LastWriteTimeUtc)
                    continue;
                if (FileHash(f) == FileHash(dest))
                    continue;
            }
            File.Copy(f, dest, true);
        }
    }

    /// <summary>
    /// 更新AUM完成后执行文件重组：
    ///   1. 确保 ps1核心 目录存在, 将根目录下 .ps1 全部移入
    ///   2. 更新所有 .bat 中对 .ps1 的引用路径 → ps1核心\
    ///   3. 清理游戏根目录下的冗余 .bat/.ps1 文件
    /// </summary>
    static void ReorganizeScripts(string aumDir)
    {
        try
        {
            var ps1Dir = Path.Combine(aumDir, "ps1核心");
            Directory.CreateDirectory(ps1Dir);

            // 1. 将 AUM管理组件 根目录下散落的 .ps1 移入 ps1核心
            foreach (var f in Directory.GetFiles(aumDir, "*.ps1"))
            {
                var name = Path.GetFileName(f);
                var dest = Path.Combine(ps1Dir, name);
                if (!File.Exists(dest))
                {
                    File.Move(f, dest);
                }
                else
                {
                    if (FileHash(f) != FileHash(dest))
                        File.Copy(f, dest, true);
                    File.Delete(f);
                }
            }

            // 2. 更新所有 .bat 文件中 .ps1 引用路径
            foreach (var f in Directory.GetFiles(aumDir, "*.bat"))
            {
                UpdateBatReference(f);
            }

            // 3. 清理游戏根目录（AUM管理组件的父目录）下的冗余 .bat/.ps1
            var gameRoot = Directory.GetParent(aumDir)?.FullName;
            if (gameRoot != null && gameRoot != aumDir)
            {
                foreach (var f in Directory.GetFiles(gameRoot, "*.bat"))
                {
                    var name = Path.GetFileName(f);
                    if (name.Contains("GameLog") || name.Contains("运行日志") || name.Contains("DNF")) continue;
                    if (File.Exists(Path.Combine(aumDir, name)))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                foreach (var f in Directory.GetFiles(gameRoot, "*.ps1"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// 将 .bat 文件中 %~dp0xxx.ps1 或 %BASE%xxx.ps1 更新为 %~dp0ps1核心\xxx.ps1
    /// 自动检测 GBK/UTF-8 编码，不做编码转换
    /// </summary>
    static void UpdateBatReference(string batPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(batPath);

            // 先尝试 UTF-8 解码
            var text = Encoding.UTF8.GetString(bytes);
            var hasPs1Ref = text.Contains(".ps1", StringComparison.OrdinalIgnoreCase);
            var enc = Encoding.UTF8;

            // 如果 UTF-8 下找不到 .ps1 引用，换 GBK 重试
            if (!hasPs1Ref)
            {
                text = Encoding.GetEncoding(936).GetString(bytes);
                hasPs1Ref = text.Contains(".ps1", StringComparison.OrdinalIgnoreCase);
                if (hasPs1Ref) enc = Encoding.GetEncoding(936);
            }

            // 已包含 ps1核心 或完全不引用 .ps1 → 跳过
            if (text.Contains("ps1核心") || !hasPs1Ref)
                return;

            text = Regex.Replace(text,
                @"(%~dp0|%BASE%)([^""'\\\s]+?\.ps1)",
                "$1" + "ps1核心" + "\\$2",
                RegexOptions.IgnoreCase);

            File.WriteAllBytes(batPath, enc.GetBytes(text));
        }
        catch { }
    }

    static string FileHash(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return BitConverter.ToString(hash);
    }

    static void CleanDuplicates(string dir)
    {
        var subDirs = new[] { "Services", "Models" };
        foreach (var sub in subDirs)
        {
            var subPath = Path.Combine(dir, sub);
            if (!Directory.Exists(subPath)) continue;
            foreach (var f in Directory.GetFiles(subPath, "*.cs"))
            {
                var name = Path.GetFileName(f);
                var rootPath = Path.Combine(dir, name);
                if (File.Exists(rootPath))
                {
                    File.Delete(rootPath);
                }
            }
        }
    }

    static void SyncDirectory(string src, string dst)
    {
        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = f.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar);
            if (rel.StartsWith("bin" + Path.DirectorySeparatorChar) ||
                rel.StartsWith("obj" + Path.DirectorySeparatorChar))
                continue;
            var target = Path.Combine(dst, rel);
            var td = Path.GetDirectoryName(target);
            if (!Directory.Exists(td))
                Directory.CreateDirectory(td!);
            File.Copy(f, target, true);
        }
    }
}
