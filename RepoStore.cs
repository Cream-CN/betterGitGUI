using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace betterGitGUI
{
    /// <summary>
    /// 最近打开的仓库列表，保存在 %APPDATA%\betterGitGUI\recent-repos.txt。
    /// 每次启动把上次用的仓库自动打开，省得每次重新选目录。
    /// </summary>
    public static class RepoStore
    {
        /// <summary>最多保留几条，数量可在 settings.ini 的 MaxRecentRepos 里改。</summary>
        private static int MaxCount
        {
            get { return AppSettings.MaxRecentRepos; }
        }

        private static string SettingDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "betterGitGUI");
            }
        }

        private static string FilePath
        {
            get { return Path.Combine(SettingDir, "recent-repos.txt"); }
        }

        public static List<string> Load()
        {
            var result = new List<string>();
            try
            {
                if (!File.Exists(FilePath)) return result;
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var path = line.Trim();
                    if (path.Length == 0) continue;
                    if (result.Contains(path)) continue;
                    if (result.Count >= MaxCount) break;
                    result.Add(path);
                }
            }
            catch { /* 读取失败按空处理，不影响启动 */ }
            return result;
        }

        public static void Save(IEnumerable<string> repos)
        {
            try
            {
                Directory.CreateDirectory(SettingDir);
                var lines = new List<string>();
                foreach (var repo in repos)
                {
                    if (string.IsNullOrEmpty(repo)) continue;
                    if (lines.Count >= MaxCount) break;
                    lines.Add(repo);
                }
                File.WriteAllText(FilePath, string.Join(Environment.NewLine, lines) + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { /* 保存失败不影响主流程 */ }
        }

        /// <summary>
        /// 把仓库插到最前面（去重后最多保留 10 条）。
        /// </summary>
        public static List<string> Add(string repo)
        {
            var repos = Load();
            repos.RemoveAll(r => string.Equals(r, repo, StringComparison.OrdinalIgnoreCase));
            repos.Insert(0, repo);
            if (repos.Count > MaxCount) repos.RemoveRange(MaxCount, repos.Count - MaxCount);
            Save(repos);
            return repos;
        }
    }
}
