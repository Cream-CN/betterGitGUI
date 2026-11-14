using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace betterGitGUI
{
    /// <summary>
    /// git 命令执行结果。
    /// </summary>
    public class GitResult
    {
        public int ExitCode;
        public string StdOut = "";
        public string StdErr = "";
        public string Command = "";
        public TimeSpan Elapsed;
        public bool Success { get { return ExitCode == 0; } }
    }

    /// <summary>
    /// 负责定位 git.exe 并执行 git 命令。
    /// </summary>
    public static class GitRunner
    {
        public static string GitExe = "git";
        public static string WorkingDir = "";

        private static bool _probed;

        /// <summary>
        /// 探测系统中可用的 git.exe 路径（PATH 优先，其次常见安装目录）。
        /// </summary>
        public static void ProbeGit()
        {
            if (_probed) return;
            _probed = true;

            // 1) PATH 上的 git
            try
            {
                var psi = new ProcessStartInfo("git", "--version")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                var p = Process.Start(psi);
                p.WaitForExit(3000);
                if (p.ExitCode == 0) { GitExe = "git"; return; }
            }
            catch { /* fall through */ }

            // 2) 常见安装路径
            var candidates = new[]
            {
                @"D:\Git\cmd\git.exe",
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files\Git\bin\git.exe",
                @"C:\Program Files (x86)\Git\cmd\git.exe",
                @"D:\Program Files\Git\cmd\git.exe",
                @"D:\Program Files\Git\bin\git.exe",
                @"D:\Program Files (x86)\Git\cmd\git.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Git\cmd\git.exe",
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) { GitExe = c; return; }
            }

            // 3) 从注册表读 Git for Windows 的安装路径（装在非默认盘时也能找到）
            foreach (var key in new[]
            {
                @"SOFTWARE\GitForWindows",
                @"SOFTWARE\WOW6432Node\GitForWindows",
            })
            {
                try
                {
                    using (var registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key))
                    {
                        if (registryKey == null) continue;
                        var installPath = registryKey.GetValue("InstallPath") as string;
                        if (string.IsNullOrEmpty(installPath)) continue;
                        var exe = Path.Combine(installPath, @"cmd\git.exe");
                        if (File.Exists(exe)) { GitExe = exe; return; }
                        exe = Path.Combine(installPath, @"bin\git.exe");
                        if (File.Exists(exe)) { GitExe = exe; return; }
                    }
                }
                catch { /* 注册表不可读时继续尝试下一种 */ }
            }
        }

        /// <summary>
        /// 执行一条 git 命令（同步）。返回退出码与输出。
        /// </summary>
        public static GitResult Run(string args)
        {
            return Run(args, null);
        }

        /// <summary>
        /// 执行一条 git 命令，并可额外注入环境变量（例如 GIT_AUTHOR_DATE）。
        /// </summary>
        public static GitResult Run(string args, IDictionary<string, string> environment)
        {
            var psi = new ProcessStartInfo
            {
                FileName = GitExe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };
            if (environment != null)
            {
                foreach (var kv in environment)
                    psi.EnvironmentVariables[kv.Key] = kv.Value;
            }
            if (!string.IsNullOrEmpty(WorkingDir) && Directory.Exists(WorkingDir))
                psi.WorkingDirectory = WorkingDir;

            var sw = Stopwatch.StartNew();
            var p = new Process { StartInfo = psi };
            try
            {
                p.Start();
            }
            catch (Exception ex)
            {
                // git 没装或路径失效时不要抛异常把界面打崩，返回一个失败结果由调用方提示
                sw.Stop();
                return new GitResult
                {
                    ExitCode = -1,
                    StdErr = "无法启动 git（" + GitExe + "）：" + ex.Message,
                    Command = "git " + args,
                    Elapsed = sw.Elapsed,
                };
            }

            // 异步读取避免死锁
            var soTask = p.StandardOutput.ReadToEndAsync();
            var seTask = p.StandardError.ReadToEndAsync();
            p.WaitForExit();
            sw.Stop();

            return new GitResult
            {
                ExitCode = p.ExitCode,
                StdOut = soTask.Result ?? "",
                StdErr = seTask.Result ?? "",
                Command = "git " + args,
                Elapsed = sw.Elapsed,
            };
        }

        /// <summary>
        /// 执行一条 git 命令，强制 UTF-8 输出、关闭 quotepath，避免中文文件名被转义。
        /// </summary>
        public static GitResult RunUi(string args)
        {
            return Run("-c core.quotepath=false -c i18n.logoutputencoding=utf-8 " + args);
        }

        /// <summary>
        /// 执行提交命令。提交信息先写成 UTF-8（无 BOM）的临时文件，再用 -F 交给 git。
        /// 不能走 stdin：.NET Framework 的 StandardInput 背后是带 UTF-8 BOM 的 StreamWriter，
        /// 关闭它时会把 BOM（EF BB BF）刷进管道，提交信息开头就会多出一个 BOM，
        /// 被按 ANSI/GBK 解读时表现为乱码。args 形如 "commit" 或 "commit --amend"。
        /// </summary>
        public static GitResult RunCommit(string args, string message)
        {
            return RunCommit(args, message, null);
        }

        /// <summary>
        /// 同上，可额外注入环境变量（例如 GIT_AUTHOR_DATE / GIT_COMMITTER_DATE）。
        /// </summary>
        public static GitResult RunCommit(string args, string message, IDictionary<string, string> environment)
        {
            var msgFile = Path.Combine(Path.GetTempPath(),
                "betterGitGUI-commit-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                // 统一成 LF 换行，写纯 UTF-8 字节，不带 BOM
                File.WriteAllText(msgFile,
                    (message ?? "").Replace("\r\n", "\n") + "\n",
                    new UTF8Encoding(false));

                return Run("-c core.quotepath=false -c i18n.logOutputEncoding=utf-8 "
                         + "-c i18n.commitEncoding=utf-8 "
                         + args + " -F " + Quote(msgFile), environment);
            }
            finally
            {
                // 临时文件只用于本次提交，无论如何都清理掉
                try { if (File.Exists(msgFile)) File.Delete(msgFile); }
                catch { /* 清理失败不影响提交结果 */ }
            }
        }

        /// <summary>
        /// 把一段补丁文本写到临时文件后执行 git apply。
        /// cached=true 表示改暂存区（暂存/取消暂存一块），
        /// reverse=true 表示反向应用（取消暂存 / 丢弃一块改动）。
        /// </summary>
        public static GitResult RunApplyPatch(string patchText, bool cached, bool reverse)
        {
            var patchFile = Path.Combine(Path.GetTempPath(),
                "betterGitGUI-patch-" + Guid.NewGuid().ToString("N") + ".patch");
            try
            {
                // 补丁必须是 LF 结尾的纯 UTF-8（无 BOM），否则 git apply 会解析失败
                File.WriteAllText(patchFile,
                    (patchText ?? "").Replace("\r\n", "\n").TrimEnd('\n') + "\n",
                    new UTF8Encoding(false));

                var args = "-c core.quotepath=false apply --recount --whitespace=nowarn"
                         + (cached ? " --cached" : "")
                         + (reverse ? " -R" : "")
                         + " " + Quote(patchFile);

                return Run(args);
            }
            finally
            {
                try { if (File.Exists(patchFile)) File.Delete(patchFile); }
                catch { /* 清理失败不影响结果 */ }
            }
        }

        /// <summary>
        /// 把单个路径用双引号包起来，供命令行使用。
        /// </summary>
        public static string Quote(string path)
        {
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>
        /// 把多个路径拼成 "-- f1 f2 f3" 形式的参数。
        /// </summary>
        public static string JoinPaths(IEnumerable<string> paths)
        {
            var sb = new StringBuilder();
            foreach (var p in paths)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Quote(p));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 判断给定目录是否为 git 仓库（含 .git 子目录或 .git 文件）。
        /// </summary>
        public static bool IsGitRepository(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            return File.Exists(Path.Combine(dir, ".git")) || Directory.Exists(Path.Combine(dir, ".git"));
        }
    }

    /// <summary>
    /// 条目属于左边（未暂存）还是右边（已暂存）列表。
    /// </summary>
    public enum EntryList
    {
        Unstaged,
        Staged,
    }

    /// <summary>
    /// git status --porcelain 的一行记录。
    /// </summary>
    public class GitStatusEntry
    {
        public string StagedStatus;   // 第一列
        public string WorkTreeStatus; // 第二列
        public string Path;
        public string OldPath; // 重命名时的原路径
        public EntryList List = EntryList.Unstaged;

        public bool IsStaged { get { return StagedStatus != " " && StagedStatus != "?"; } }
        public bool IsUntracked { get { return StagedStatus == "?" && WorkTreeStatus == "?"; } }

        /// <summary>
        /// 是否是冲突（未合并）文件：UU / AA / DD / AU / UA / DU / UD。
        /// </summary>
        public bool IsUnmerged
        {
            get
            {
                var code = StagedStatus + WorkTreeStatus;
                return code == "UU" || code == "AA" || code == "DD"
                    || code == "AU" || code == "UA" || code == "DU" || code == "UD";
            }
        }

        public string DisplayPath
        {
            get
            {
                if (!string.IsNullOrEmpty(OldPath)) return OldPath + " -> " + Path;
                return Path;
            }
        }

        /// <summary>
        /// 一个文件同时出现在左右两栏时（部分暂存），需要两条互不影响的记录。
        /// </summary>
        public GitStatusEntry Clone()
        {
            return new GitStatusEntry
            {
                StagedStatus = StagedStatus,
                WorkTreeStatus = WorkTreeStatus,
                Path = Path,
                OldPath = OldPath,
                List = List,
            };
        }
    }
}
