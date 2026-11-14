using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace betterGitGUI
{
    /// <summary>
    /// 应用设置，配置文件位于 %USERPROFILE%\.betterGitGUI\settings.ini。
    /// 首次启动自动生成带注释的默认文件；也可用「设置 → 首选项...」在界面上修改。
    /// 文件缺失或某一项写错时，该项退回默认值，不影响启动。
    /// 除字体之外，改完配置需要重启程序生效。
    /// </summary>
    public static class AppSettings
    {
        public const int DefaultMaxDiffLines = 5000;
        public const int DefaultMaxDiffSizeKB = 2048;
        public const int DefaultMaxFilePreviewKB = 2048;
        public const int DefaultMaxRecentRepos = 10;
        public const string DefaultDiffFontName = "Consolas";
        public const int DefaultDiffFontSize = 9;
        public const bool DefaultConfirmDangerous = true;

        private static bool _loaded;
        private static int _maxDiffLines;
        private static int _maxDiffSizeKB;
        private static int _maxFilePreviewKB;
        private static int _maxRecentRepos;
        private static string _diffFontName;
        private static int _diffFontSize;
        private static bool _confirmDangerous;

        static AppSettings()
        {
            ResetFields();
        }

        /// <summary>配置文件所在目录：%USERPROFILE%\.betterGitGUI</summary>
        public static string SettingDir
        {
            get { return Path.Combine(UserProfile, ".betterGitGUI"); }
        }

        /// <summary>配置文件路径：%USERPROFILE%\.betterGitGUI\settings.ini</summary>
        public static string FilePath
        {
            get { return Path.Combine(SettingDir, "settings.ini"); }
        }

        /// <summary>旧版本用的配置目录，只在迁移时读取一次。</summary>
        private static string LegacySettingDir
        {
            get { return Path.Combine(UserProfile, ".beetergitgui"); }
        }

        // ---------- 差异面板 ----------

        /// <summary>差异面板最多渲染的行数，超出部分只显示前 N 行（最小 1）。</summary>
        public static int MaxDiffLines
        {
            get { Load(); return _maxDiffLines; }
            set { Load(); _maxDiffLines = value < 1 ? 1 : value; }
        }

        /// <summary>单次 diff 文本超过多少 KB 就不再显示（0 = 不限制）。</summary>
        public static int MaxDiffSizeKB
        {
            get { Load(); return _maxDiffSizeKB; }
            set { Load(); _maxDiffSizeKB = value < 0 ? 0 : value; }
        }

        /// <summary>差异面板字体名。</summary>
        public static string DiffFontName
        {
            get { Load(); return _diffFontName; }
            set { Load(); _diffFontName = string.IsNullOrWhiteSpace(value) ? DefaultDiffFontName : value.Trim(); }
        }

        /// <summary>差异面板字号（6 - 72 磅）。</summary>
        public static int DiffFontSize
        {
            get { Load(); return _diffFontSize; }
            set { Load(); _diffFontSize = value < 6 ? 6 : value > 72 ? 72 : value; }
        }

        // ---------- 未跟踪文件预览 ----------

        /// <summary>未跟踪 / 预览文件超过多少 KB 就不显示内容（0 = 不限制）。</summary>
        public static int MaxFilePreviewKB
        {
            get { Load(); return _maxFilePreviewKB; }
            set { Load(); _maxFilePreviewKB = value < 0 ? 0 : value; }
        }

        // ---------- 其他 ----------

        /// <summary>「最近打开」保留（并自动打开）的仓库数量（1 - 100）。</summary>
        public static int MaxRecentRepos
        {
            get { Load(); return _maxRecentRepos; }
            set { Load(); _maxRecentRepos = value < 1 ? 1 : value > 100 ? 100 : value; }
        }

        /// <summary>丢弃改动、硬重置、中止合并/变基等不可恢复操作前是否弹确认框。</summary>
        public static bool ConfirmDangerous
        {
            get { Load(); return _confirmDangerous; }
            set { Load(); _confirmDangerous = value; }
        }

        /// <summary>
        /// 程序启动时调用一次：读入配置，并在配置文件不存在时生成默认文件
        /// （若存在旧路径 .beetergitgui\settings.ini，会先把它迁移过来）。
        /// </summary>
        public static void Initialize()
        {
            Load();
        }

        /// <summary>把内存中的当前值写回 settings.ini（首选项里点「确定」时调用）。</summary>
        public static bool Save()
        {
            try
            {
                Directory.CreateDirectory(SettingDir);
                var text = BuildContent();
                // 先写临时文件再替换，避免写到一半崩溃留下残缺配置
                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, text, new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
                _loaded = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>恢复全部默认值（不自动保存，调用方决定是否 Save）。</summary>
        public static void ResetToDefaults()
        {
            Load();
            ResetFields();
        }

        /// <summary>重新读一遍配置文件（用于外部改过 ini 后刷新）。</summary>
        public static void Reload()
        {
            _loaded = false;
            ResetFields();
            Load();
        }

        private static void ResetFields()
        {
            _maxDiffLines = DefaultMaxDiffLines;
            _maxDiffSizeKB = DefaultMaxDiffSizeKB;
            _maxFilePreviewKB = DefaultMaxFilePreviewKB;
            _maxRecentRepos = DefaultMaxRecentRepos;
            _diffFontName = DefaultDiffFontName;
            _diffFontSize = DefaultDiffFontSize;
            _confirmDangerous = DefaultConfirmDangerous;
        }

        private static string UserProfile
        {
            get
            {
                var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(profile)) return profile;
                var home = Environment.GetEnvironmentVariable("USERPROFILE");
                return string.IsNullOrEmpty(home) ? Environment.CurrentDirectory : home;
            }
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true; // 只尝试读取一次，读失败就一路用默认值
            ResetFields();

            try
            {
                EnsureFile();
                MergeMissingKeys();
                if (!File.Exists(FilePath)) return;

                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == ';' || line[0] == '#' || line[0] == '[') continue;

                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    var key = line.Substring(0, eq).Trim();
                    var value = line.Substring(eq + 1).Trim();
                    if (key.Length == 0) continue;

                    switch (key.ToLowerInvariant())
                    {
                        case "maxdifflines":
                            _maxDiffLines = ParseInt(value, _maxDiffLines, 1, int.MaxValue);
                            break;
                        case "maxdiffsizekb":
                            _maxDiffSizeKB = ParseInt(value, _maxDiffSizeKB, 0, int.MaxValue);
                            break;
                        case "difffontname":
                            if (value.Length > 0) _diffFontName = value;
                            break;
                        case "difffontsize":
                            _diffFontSize = ParseInt(value, _diffFontSize, 6, 72);
                            break;
                        case "maxfilepreviewkb":
                            _maxFilePreviewKB = ParseInt(value, _maxFilePreviewKB, 0, int.MaxValue);
                            break;
                        case "maxrecentrepos":
                            _maxRecentRepos = ParseInt(value, _maxRecentRepos, 1, 100);
                            break;
                        case "confirmdangerous":
                            _confirmDangerous = ParseBool(value, _confirmDangerous);
                            break;
                    }
                }
            }
            catch
            {
                // 读取失败不影响启动，沿用默认值
            }
        }

        /// <summary>解析整数并夹到 [min,max]；写错、为空或小于下限时保留原值。</summary>
        private static int ParseInt(string text, int fallback, int min, int max)
        {
            long n;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                return fallback;
            if (n < min) return fallback;
            if (n > max) return max;
            return (int)n;
        }

        private static bool ParseBool(string text, bool fallback)
        {
            var v = text.Trim().ToLowerInvariant();
            if (v == "1" || v == "true" || v == "yes" || v == "on" || v == "是") return true;
            if (v == "0" || v == "false" || v == "no" || v == "off" || v == "否") return false;
            return fallback;
        }

        private static void EnsureFile()
        {
            try
            {
                if (File.Exists(FilePath)) return;
                Directory.CreateDirectory(SettingDir);

                // 旧路径（.beetergitgui）里有配置就搬过来，别让用户白改一遍
                var legacyFile = Path.Combine(LegacySettingDir, "settings.ini");
                if (File.Exists(legacyFile))
                {
                    try
                    {
                        File.Move(legacyFile, FilePath);
                        if (Directory.GetFileSystemEntries(LegacySettingDir).Length == 0)
                            Directory.Delete(LegacySettingDir);
                        return;
                    }
                    catch
                    {
                        // 迁移失败就继续走生成默认文件的流程
                    }
                }

                File.WriteAllText(FilePath, BuildContent(), new UTF8Encoding(false));
            }
            catch
            {
                // 目录不可写时就不建文件，配置全部走默认值
            }
        }

        /// <summary>配置文件里的一项：所属小节、键名、注释、当前值的文本形式。</summary>
        private class IniEntry
        {
            public string Section;
            public string Key;
            public string Comment;
            public string Value;

            public IniEntry(string section, string key, string comment, string value)
            {
                Section = section;
                Key = key;
                Comment = comment;
                Value = value;
            }
        }

        /// <summary>按当前值列出全部配置项，顺序即文件里的书写顺序。</summary>
        private static List<IniEntry> CurrentEntries()
        {
            return new List<IniEntry>
            {
                new IniEntry("diff", "MaxDiffLines",
                    "差异面板最多渲染的行数，超出部分只显示前 N 行（最小 1）",
                    _maxDiffLines.ToString(CultureInfo.InvariantCulture)),
                new IniEntry("diff", "MaxDiffSizeKB",
                    "单次 diff 文本超过多少 KB 就直接提示「差异过大，不显示」，0 表示不限制",
                    _maxDiffSizeKB.ToString(CultureInfo.InvariantCulture)),
                new IniEntry("diff", "DiffFontName",
                    "差异面板字体名（填本机已安装的字体，如 Consolas / 更纱黑体 Mono）",
                    _diffFontName),
                new IniEntry("diff", "DiffFontSize",
                    "差异面板字号（6 - 72）",
                    _diffFontSize.ToString(CultureInfo.InvariantCulture)),
                new IniEntry("preview", "MaxFilePreviewKB",
                    "未跟踪文件 / 预览内容超过多少 KB 就不显示，0 表示不限制",
                    _maxFilePreviewKB.ToString(CultureInfo.InvariantCulture)),
                new IniEntry("general", "MaxRecentRepos",
                    "「最近打开」菜单保留并自动打开的仓库数量（1 - 100）",
                    _maxRecentRepos.ToString(CultureInfo.InvariantCulture)),
                new IniEntry("general", "ConfirmDangerous",
                    "丢弃改动、硬重置、中止合并/变基等不可恢复操作前是否弹确认框（true/false）",
                    _confirmDangerous ? "true" : "false"),
            };
        }

        /// <summary>按当前值拼出完整的配置文件内容（含注释）。首选项保存时会整文件重写。</summary>
        private static string BuildContent()
        {
            var sb = new StringBuilder();
            sb.Append("; betterGitGUI 配置文件\r\n");
            sb.Append("; 路径：%USERPROFILE%\\.betterGitGUI\\settings.ini\r\n");
            sb.Append("; 可以直接改本文件，也可以用「设置 → 首选项...」在界面上改。\r\n");
            sb.Append("; 注意：在首选项里点「确定」会重写整个文件，手写的注释不会保留。\r\n");

            var currentSection = "";
            foreach (var entry in CurrentEntries())
            {
                if (entry.Section != currentSection)
                {
                    sb.Append("\r\n[").Append(entry.Section).Append("]\r\n");
                    currentSection = entry.Section;
                }
                sb.Append("; ").Append(entry.Comment).Append("\r\n");
                sb.Append(entry.Key).Append('=').Append(entry.Value).Append("\r\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 升级后新增的配置项：旧配置里没有的键按默认值补在文件末尾，
        /// 已有内容和用户手写的注释都保持不变。
        /// </summary>
        private static void MergeMissingKeys()
        {
            try
            {
                if (!File.Exists(FilePath)) return;

                var text = File.ReadAllText(FilePath, Encoding.UTF8);
                var present = new List<string>();
                foreach (var raw in text.Split('\n'))
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == ';' || line[0] == '#' || line[0] == '[') continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    present.Add(line.Substring(0, eq).Trim().ToLowerInvariant());
                }

                var append = new StringBuilder();
                foreach (var entry in CurrentEntries())
                {
                    if (present.Contains(entry.Key.ToLowerInvariant())) continue;
                    if (append.Length == 0)
                        append.Append("\r\n; ---- 以下配置项由程序按默认值补充 ----\r\n");
                    append.Append("; ").Append(entry.Comment).Append("\r\n");
                    append.Append(entry.Key).Append('=').Append(entry.Value).Append("\r\n");
                }
                if (append.Length == 0) return;

                File.AppendAllText(FilePath, append.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                // 补写失败不影响使用，缺的项按默认值走
            }
        }
    }
}
