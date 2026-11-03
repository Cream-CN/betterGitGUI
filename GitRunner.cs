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
                @"C:\Program Files (x86)\Git\cmd\git.exe",
                @"D:\Program Files\Git\cmd\git.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Git\cmd\git.exe",
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) { GitExe = c; return; }
            }
        }

        /// <summary>
        /// 执行一条 git 命令（同步）。返回退出码与输出。
        /// </summary>
        public static GitResult Run(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = GitExe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            if (!string.IsNullOrEmpty(WorkingDir) && Directory.Exists(WorkingDir))
                psi.WorkingDirectory = WorkingDir;

            var sw = Stopwatch.StartNew();
            var p = new Process { StartInfo = psi };
            p.Start();

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
    /// git status --porcelain 的一行记录。
    /// </summary>
    public class GitStatusEntry
    {
        public string StagedStatus;   // 第一列
        public string WorkTreeStatus; // 第二列
        public string Path;
        public string OldPath; // 重命名时的原路径

        public bool IsStaged { get { return StagedStatus != " " && StagedStatus != "?"; } }
        public bool IsUntracked { get { return StagedStatus == "?" && WorkTreeStatus == "?"; } }

        public string DisplayPath
        {
            get
            {
                if (!string.IsNullOrEmpty(OldPath)) return OldPath + " -> " + Path;
                return Path;
            }
        }
    }
}
