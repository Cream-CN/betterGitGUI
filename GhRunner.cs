using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace betterGitGUI
{
    /// <summary>
    /// 负责定位并调用 GitHub CLI（gh.exe）。
    /// gh 与 git 一样是外部进程：所有命令都在当前仓库目录下执行、捕捉输出。
    /// </summary>
    public static class GhRunner
    {
        public static string GhExe = "gh";
        public static bool Available = false;

        private static bool _probed;

        private static string SettingDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "betterGitGUI");
            }
        }

        private static string PathFile
        {
            get { return Path.Combine(SettingDir, "gh-path.txt"); }
        }

        /// <summary>
        /// 探测 gh：先用手动指定的路径，再试 PATH，最后试常见安装位置。
        /// </summary>
        public static void ProbeGh()
        {
            if (_probed) return;
            _probed = true;

            // 1) 手动指定过且文件还在
            try
            {
                if (File.Exists(PathFile))
                {
                    var saved = File.ReadAllText(PathFile, Encoding.UTF8).Trim();
                    if (saved.Length > 0 && File.Exists(saved))
                    {
                        GhExe = saved;
                        Available = true;
                        return;
                    }
                }
            }
            catch { /* 读不到就继续探测 */ }

            // 2) PATH 上的 gh
            try
            {
                var psi = new ProcessStartInfo("gh", "--version")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                var p = Process.Start(psi);
                p.WaitForExit(3000);
                if (p.ExitCode == 0) { GhExe = "gh"; Available = true; return; }
            }
            catch { /* fall through */ }

            // 3) 常见安装路径
            var candidates = new[]
            {
                @"C:\Program Files\GitHub CLI\gh.exe",
                @"D:\Program Files\GitHub CLI\gh.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\GitHub CLI\gh.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\WinGet\Links\gh.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\scoop\shims\gh.exe",
                @"C:\ProgramData\chocolatey\bin\gh.exe",
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                {
                    GhExe = c;
                    Available = true;
                    return;
                }
            }

            Available = false;
        }

        public static void ReloadProbe()
        {
            _probed = false;
        }

        /// <summary>
        /// 手动指定 gh.exe 路径并记住，下次启动直接用。
        /// </summary>
        public static void SetPath(string path)
        {
            GhExe = path;
            Available = File.Exists(path);
            _probed = true;
            try
            {
                Directory.CreateDirectory(SettingDir);
                if (Available)
                    File.WriteAllText(PathFile, path, new UTF8Encoding(false));
                else if (File.Exists(PathFile))
                    File.Delete(PathFile);
            }
            catch { /* 记不住路径不影响本次使用 */ }
        }

        /// <summary>
        /// 执行一条 gh 命令。环境变量关掉了交互提示、颜色和更新检查，
        /// 否则在 GUI 里调用时可能卡在提示上。
        /// </summary>
        public static GitResult Run(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = GhExe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };
            psi.EnvironmentVariables["GH_PROMPT_DISABLED"] = "1";
            psi.EnvironmentVariables["GH_NO_UPDATE_NOTIFIER"] = "1";
            psi.EnvironmentVariables["NO_COLOR"] = "1";

            if (!string.IsNullOrEmpty(GitRunner.WorkingDir) && Directory.Exists(GitRunner.WorkingDir))
                psi.WorkingDirectory = GitRunner.WorkingDir;

            var sw = Stopwatch.StartNew();
            var p = new Process { StartInfo = psi };
            try
            {
                p.Start();
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new GitResult
                {
                    ExitCode = -1,
                    StdErr = "无法启动 gh（" + GhExe + "）：" + ex.Message,
                    Command = "gh " + args,
                    Elapsed = sw.Elapsed,
                };
            }

            var soTask = p.StandardOutput.ReadToEndAsync();
            var seTask = p.StandardError.ReadToEndAsync();
            p.WaitForExit();
            sw.Stop();

            return new GitResult
            {
                ExitCode = p.ExitCode,
                StdOut = soTask.Result ?? "",
                StdErr = seTask.Result ?? "",
                Command = "gh " + args,
                Elapsed = sw.Elapsed,
            };
        }

        /// <summary>
        /// 需要正文字段的命令（PR/Issue 描述）：正文写进 UTF-8 临时文件，用 --body-file 传入，
        /// 避免多行文本、引号、中文在命令行里被转义坏。
        /// bodyArgName 传 "--body"，这里会拼成 --body-file。
        /// </summary>
        public static GitResult RunWithBodyFile(string args, string body, string bodyArgName)
        {
            var bodyFile = Path.Combine(Path.GetTempPath(),
                "betterGitGUI-gh-body-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllText(bodyFile,
                    (body ?? "").Replace("\r\n", "\n"),
                    new UTF8Encoding(false));
                return Run(args + " " + bodyArgName + "-file " + GitRunner.Quote(bodyFile));
            }
            finally
            {
                try { if (File.Exists(bodyFile)) File.Delete(bodyFile); }
                catch { /* 清理失败不影响结果 */ }
            }
        }

        // ---------- JSON 解析 ----------

        public static List<Dictionary<string, object>> ParseArray(string json)
        {
            var result = new List<Dictionary<string, object>>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                var serializer = new JavaScriptSerializer();
                var data = serializer.DeserializeObject(json);
                if (!(data is object[])) return result;
                foreach (var item in (object[])data)
                {
                    if (item is Dictionary<string, object>)
                        result.Add((Dictionary<string, object>)item);
                }
            }
            catch { /* JSON 结构异常时返回空列表，界面显示"暂无数据" */ }
            return result;
        }

        public static Dictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var serializer = new JavaScriptSerializer();
                var data = serializer.DeserializeObject(json);
                if (data is Dictionary<string, object>) return (Dictionary<string, object>)data;
            }
            catch { }
            return null;
        }

        public static string Str(Dictionary<string, object> dict, string key)
        {
            if (dict == null) return "";
            var value = dict.ContainsKey(key) ? dict[key] : null;
            if (value == null) return "";
            return value.ToString();
        }

        /// <summary>取 author / user 这类嵌套对象的 login 字段。</summary>
        public static string LoginOf(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key)) return "";
            var value = dict[key];
            if (value is Dictionary<string, object>)
                return Str((Dictionary<string, object>)value, "login");
            return value == null ? "" : value.ToString();
        }

        public static List<Dictionary<string, object>> SubList(Dictionary<string, object> dict, string key)
        {
            var result = new List<Dictionary<string, object>>();
            if (dict == null || !dict.ContainsKey(key)) return result;
            var value = dict[key];
            if (value is object[])
            {
                foreach (var item in (object[])value)
                    if (item is Dictionary<string, object>) result.Add((Dictionary<string, object>)item);
            }
            return result;
        }
    }
}
