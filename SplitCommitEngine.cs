using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace betterGitGUI
{
    /// <summary>拆分粒度。</summary>
    public enum SplitGranularity
    {
        ByFile,   // 每个文件一个提交
        ByCount,  // 指定提交总数，按改动行数平均分配
    }

    /// <summary>提交时间策略。</summary>
    public enum SplitDateMode
    {
        KeepToday,    // 全部落在今天
        RecentDays,   // 最近 N 天内随机
        DateRange,    // 指定日期区间内铺开
    }

    public class SplitOptions
    {
        public SplitGranularity Granularity = SplitGranularity.ByFile;
        public int CommitCount = 5;
        public SplitDateMode DateMode = SplitDateMode.KeepToday;
        public int RecentDays = 7;
        public DateTime RangeStart = DateTime.Today.AddDays(-6);
        public DateTime RangeEnd = DateTime.Today;
        public string BaseMessage = "";
    }

    /// <summary>计划中的一个提交。</summary>
    public class SplitSlice
    {
        public int Index;
        public int Quota;      // 本提交最多消费的改动行数
        public int Lines;      // 预估（实际）消费的改动行数
        public bool WholeFile; // 按文件模式：整取一个文件
        public bool Binary;    // 含有二进制文件（界面不展示行数，改显示「二进制」）
        public DateTime When;
        public string Message = "";
        public List<string> Paths = new List<string>();
    }

    public class SplitPlan
    {
        public List<SplitSlice> Slices = new List<SplitSlice>();
        public int TotalLines;
        public int TotalFiles;
        public SplitGranularity Granularity;
        public string DiffFingerprint = "";
    }

    /// <summary>
    /// 把工作区未提交的改动按选定方式拆成多个提交。
    ///
    /// 做法：先 git add -N 让未跟踪文件出现在 diff 里，然后反复「取当前剩余 diff 的前若干个改动行
    /// （或第一个文件）→ 生成零上下文补丁 → git apply --cached → commit」，每一步都重新取 diff，
    /// 因此行号始终基于当前暂存区状态，不需要维护行号偏移。
    /// </summary>
    public static class SplitCommitEngine
    {
        private const string DiffCommand =
            "-c core.quotepath=false diff -U0 --no-renames --binary --no-color --no-ext-diff";

        // ---------- 对外接口 ----------

        /// <summary>生成拆分计划（会执行 git add -N，索引里只多出 intent-to-add 记录）。</summary>
        public static SplitPlan BuildPlan(SplitOptions opt)
        {
            RequireRepository();
            EnsureIndexClean();

            // 只对「未跟踪文件」做 intent-to-add，让它们出现在 diff 里。
            // 不能用 git add -N -A：那是把删除/修改也一并暂存了，会绕过拆分直接混进第一个提交。
            StageIntentForUntracked();

            try
            {
                return BuildPlanFromCurrentDiff(opt);
            }
            catch
            {
                // 计划没建成，撤销 add -N 留下的 intent-to-add 记录
                GitRunner.Run("reset");
                throw;
            }
        }

        private static SplitPlan BuildPlanFromCurrentDiff(SplitOptions opt)
        {            var diffText = GetDiffText();
            var files = ParsePatch(diffText);
            if (files.Count == 0)
                throw new InvalidOperationException("工作区没有未提交的改动，不需要拆分。");

            var plan = new SplitPlan
            {
                Granularity = opt.Granularity,
                DiffFingerprint = Fingerprint(diffText),
                TotalFiles = files.Count,
            };

            int totalLines = 0;
            foreach (var f in files) totalLines += f.ChangedLines();
            plan.TotalLines = totalLines;

            var binaryPaths = new HashSet<string>();
            foreach (var f in files)
            {
                if (f.ShowsAsBinary && !string.IsNullOrEmpty(f.Path)) binaryPaths.Add(f.Path);
            }

            if (opt.Granularity == SplitGranularity.ByFile)
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var slice = new SplitSlice
                    {
                        Index = i,
                        WholeFile = true,
                        Lines = files[i].ChangedLines(),
                        Binary = files[i].ShowsAsBinary,
                    };
                    slice.Paths.Add(files[i].Path);
                    plan.Slices.Add(slice);
                }
            }
            else
            {
                int count = opt.CommitCount;
                if (count < 1) count = 1;
                if (count > 1000) count = 1000;

                var cursor = new DiffCursor(files);
                int remaining = totalLines;
                for (int i = 0; i < count; i++)
                {
                    int remainingCommits = count - i;
                    int quota;
                    if (totalLines > 0) quota = remaining > 0 ? (int)Math.Ceiling((double)remaining / remainingCommits) : 0;
                    // 只有模式变更（0 行改动）时也要发出补丁，否则 mode 行永远进不了暂存区
                    else quota = 1;

                    var slice = new SplitSlice { Index = i, Quota = quota };
                    if (quota > 0)
                    {
                        slice.Lines = cursor.Consume(quota, slice.Paths);
                        remaining -= slice.Lines;
                        if (slice.Lines <= 0 && totalLines > 0) remaining = 0; // 防御：不该发生
                    }
                    plan.Slices.Add(slice);
                }
            }

            foreach (var slice in plan.Slices)
            {
                if (slice.Binary) continue;
                foreach (var p in slice.Paths)
                {
                    if (binaryPaths.Contains(p)) { slice.Binary = true; break; }
                }
            }

            AssignDatesAndMessages(plan, opt);
            return plan;
        }

        /// <summary>按计划执行拆分，返回实际创建的提交数。失败时会回滚暂存区（已创建的提交保留）。</summary>
        public static int Execute(SplitPlan plan, Action<string> log)
        {
            RequireRepository();

            var now = GetDiffText();
            if (Fingerprint(now) != plan.DiffFingerprint)
                throw new InvalidOperationException("工作区改动在生成预览之后又发生变化了，请重新生成预览。");

            int made = 0;
            try
            {
                for (int i = 0; i < plan.Slices.Count; i++)
                {
                    var slice = plan.Slices[i];
                    var files = ParsePatch(GetDiffText());

                    string patch;
                    if (slice.WholeFile)
                        patch = BuildPatchWholeFile(files);
                    else if (slice.Quota > 0)
                        patch = BuildPatchPrefix(files, slice.Quota);
                    else
                        patch = "";

                    if (!string.IsNullOrEmpty(patch))
                        ApplyPatch(patch);

                    bool empty = string.IsNullOrEmpty(patch);
                    var args = empty ? "commit --allow-empty" : "commit";
                    var result = GitRunner.RunCommit(args, slice.Message, MakeDateEnv(slice.When));
                    if (!result.Success)
                    {
                        throw new InvalidOperationException(string.Format(
                            "第 {0} 个提交失败：{1}", i + 1,
                            string.IsNullOrEmpty(result.StdErr) ? ("exit " + result.ExitCode) : result.StdErr.Trim()));
                    }

                    made++;
                    if (log != null)
                        log(string.Format("已创建 {0}/{1}：{2}", made, plan.Slices.Count, FirstLine(slice.Message)));
                }
            }
            catch
            {
                // 已创建的提交不回滚，但把暂存区还原，避免留下半吊子状态
                GitRunner.Run("reset");
                throw;
            }

            var leftover = GetDiffText();
            if (!string.IsNullOrWhiteSpace(leftover))
            {
                GitRunner.Run("reset");
                throw new InvalidOperationException(
                    "还有改动没有进入任何提交，已还原暂存区。请重新刷新后检查工作区状态。");
            }
            return made;
        }

        /// <summary>清掉预览阶段留下的 intent-to-add 记录（暂存区原本为空时才安全）。</summary>
        public static void CleanupIndex()
        {
            GitRunner.Run("reset");
        }

        // ---------- 前置检查 ----------

        private static void RequireRepository()
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir) || !Directory.Exists(GitRunner.WorkingDir))
                throw new InvalidOperationException("请先打开一个仓库。");
            if (!GitRunner.IsGitRepository(GitRunner.WorkingDir))
                throw new InvalidOperationException("当前目录不是 Git 仓库。");
        }

        private static void StageIntentForUntracked()
        {
            var list = GitRunner.Run("-c core.quotepath=false ls-files --others --exclude-standard");
            if (!list.Success) return;

            var paths = new List<string>();
            foreach (var line in (list.StdOut ?? "").Split('\n'))
            {
                var p = line.TrimEnd('\r');
                if (p.Length > 0) paths.Add(p);
            }

            // 分批传参，避免命令行过长
            const int batch = 40;
            for (int start = 0; start < paths.Count; start += batch)
            {
                var sb = new StringBuilder();
                int end = Math.Min(paths.Count, start + batch);
                for (int i = start; i < end; i++)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(GitRunner.Quote(paths[i]));
                }
                GitRunner.Run("-c core.quotepath=false add -N -- " + sb.ToString());
            }
        }

        private static void EnsureIndexClean()
        {
            var staged = GitRunner.Run("-c core.quotepath=false diff --cached --name-only");
            if (!staged.Success)
                throw new InvalidOperationException("读取暂存区失败：" + staged.StdErr.Trim());
            if (!string.IsNullOrWhiteSpace(staged.StdOut))
                throw new InvalidOperationException(
                    "暂存区里已经有内容了。请先提交或全部取消暂存，再来拆分。");

            var unmerged = GitRunner.Run("ls-files -u --name-only");
            if (unmerged.Success && !string.IsNullOrWhiteSpace(unmerged.StdOut))
                throw new InvalidOperationException("存在未解决的合并冲突，请先处理冲突。");
        }

        // ---------- git 调用 ----------

        private static string GetDiffText()
        {
            var r = GitRunner.Run(DiffCommand);
            if (!r.Success)
                throw new InvalidOperationException("读取改动失败：" + r.StdErr.Trim());
            return r.StdOut ?? "";
        }

        private static void ApplyPatch(string patch)
        {
            var path = Path.Combine(Path.GetTempPath(),
                "betterGitGUI-patch-" + Guid.NewGuid().ToString("N") + ".patch");
            try
            {
                // 补丁必须是纯 UTF-8（无 BOM），且以换行结尾
                File.WriteAllText(path, patch, new UTF8Encoding(false));
                var r = GitRunner.Run("apply --cached --unidiff-zero --whitespace=nowarn " + GitRunner.Quote(path));
                if (!r.Success)
                    throw new InvalidOperationException("把改动写入暂存区失败：" + r.StdErr.Trim());
            }
            finally
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch { /* 忽略清理失败 */ }
            }
        }

        private static Dictionary<string, string> MakeDateEnv(DateTime when)
        {
            var text = FormatGitDate(when);
            var env = new Dictionary<string, string>();
            env["GIT_AUTHOR_DATE"] = text;
            env["GIT_COMMITTER_DATE"] = text;
            return env;
        }

        private static string FormatGitDate(DateTime when)
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(when);
            var sign = offset.TotalMinutes >= 0 ? "+" : "-";
            var abs = TimeSpan.FromMinutes(Math.Abs(offset.TotalMinutes));
            return when.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                 + sign + abs.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        private static string FirstLine(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            var idx = message.IndexOf('\n');
            return idx >= 0 ? message.Substring(0, idx) : message;
        }

        private static string Fingerprint(string text)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder();
                foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        // ---------- 日期与提交信息 ----------

        private static void AssignDatesAndMessages(SplitPlan plan, SplitOptions opt)
        {
            int n = plan.Slices.Count;
            var dates = MakeDates(n, opt);
            for (int i = 0; i < n; i++)
            {
                var slice = plan.Slices[i];
                slice.When = dates[i];
                slice.Message = MakeMessage(opt.BaseMessage, i + 1, n);
            }
        }

        private static string MakeMessage(string baseMessage, int index, int total)
        {
            if (string.IsNullOrEmpty(baseMessage))
                return string.Format("chore: 拆分提交（{0}/{1}）", index, total);
            return string.Format("{0}（{1}/{2}）", baseMessage, index, total);
        }

        private static List<DateTime> MakeDates(int count, SplitOptions opt)
        {
            var list = new List<DateTime>();
            if (count <= 0) return list;

            var now = DateTime.Now;
            var rnd = new Random();

            switch (opt.DateMode)
            {
                case SplitDateMode.RecentDays:
                {
                    int days = opt.RecentDays;
                    if (days < 1) days = 1;
                    if (days > 3650) days = 3650;
                    for (int i = 0; i < count; i++)
                    {
                        var day = DateTime.Today.AddDays(-rnd.Next(0, days));
                        list.Add(day.AddMinutes(rnd.Next(8 * 60, 23 * 60)));
                    }
                    list.Sort();
                    break;
                }
                case SplitDateMode.DateRange:
                {
                    var start = opt.RangeStart.Date;
                    var end = opt.RangeEnd.Date;
                    if (start > end) { var t = start; start = end; end = t; }
                    int days = (int)(end - start).TotalDays + 1;
                    for (int i = 0; i < count; i++)
                    {
                        int dayOffset = count <= days
                            ? (int)Math.Round(i * (days - 1) / (double)Math.Max(1, count - 1))
                            : (int)((long)i * days / count);
                        var day = start.AddDays(dayOffset);
                        list.Add(day.AddMinutes(rnd.Next(8 * 60, 23 * 60)));
                    }
                    list.Sort();
                    break;
                }
                default:
                {
                    // 全部落在今天：从当前时间往前推，保证先后有序
                    var from = now.AddMinutes(-Math.Min(count, 720));
                    for (int i = 0; i < count; i++)
                        list.Add(from.AddMinutes(i));
                    break;
                }
            }

            // 不允许出现未来时间（未来的提交不会计入 GitHub 贡献图）
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] > now)
                {
                    var clamped = now.AddSeconds(-(list.Count - i) * 30);
                    list[i] = clamped < now ? clamped : now.AddSeconds(-1);
                }
            }
            return list;
        }

        // ---------- diff 解析 ----------

        private class DiffHunk
        {
            public string RawHeader;
            public int OldStart;
            public int OldCount;
            public int NewStart;
            public int NewCount;
            public List<string> Body = new List<string>();

            public int ChangedLines()
            {
                int n = 0;
                foreach (var line in Body)
                    if (line.Length > 0 && (line[0] == '+' || line[0] == '-')) n++;
                return n;
            }
        }

        private class DiffFile
        {
            public List<string> Header = new List<string>();
            public List<DiffHunk> Hunks = new List<DiffHunk>();
            /// <summary>git 的判定：补丁体是 GIT binary patch / Binary files ... differ。</summary>
            public bool IsBinary;
            /// <summary>我们按 git 的算法（前 8000 字节出现 0x00）读工作区文件得到的判定。</summary>
            public bool IsBinaryBlob;
            public bool IsDeleted;
            public List<string> BinaryBlock = new List<string>();
            public string Path = "";

            /// <summary>给界面看的二进制判定：两种来源任一成立。</summary>
            public bool ShowsAsBinary { get { return IsBinary || IsBinaryBlob; } }

            /// <summary>
            /// 整块处理的单元：二进制内容（含 git 当作文本、但算法判定是二进制的）和整文件删除。
            /// 二进制没有「行」的概念，不按行拆分；删除的补丁头是 deleted file mode，只删几行会自相矛盾。
            /// 文件模式（chmod）变更不在此列，交给 git 默认行为：mode 行随文件补丁一起提交。
            /// </summary>
            public bool Atomic { get { return IsBinary || IsBinaryBlob || IsDeleted; } }

            public int ChangedLines()
            {
                if (IsBinary) return 1;
                int n = 0;
                foreach (var h in Hunks) n += h.ChangedLines();
                return n;
            }
        }

        private static readonly Regex HunkHeaderRegex =
            new Regex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@", RegexOptions.Compiled);

        private static List<DiffFile> ParsePatch(string text)
        {
            var files = new List<DiffFile>();
            if (string.IsNullOrEmpty(text)) return files;

            DiffFile cur = null;
            DiffHunk hunk = null;
            bool inBinary = false;

            using (var reader = new StringReader(text))
            {
                string raw;
                while ((raw = reader.ReadLine()) != null)
                {
                    var line = raw.TrimEnd('\r');

                    if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                    {
                        cur = new DiffFile();
                        cur.Header.Add(line);
                        files.Add(cur);
                        hunk = null;
                        inBinary = false;
                        continue;
                    }
                    if (cur == null) continue;

                    if (inBinary)
                    {
                        cur.BinaryBlock.Add(line);
                        continue;
                    }

                    if (hunk != null && line.StartsWith("@@", StringComparison.Ordinal))
                    {
                        hunk = ParseHunkHeader(line);
                        if (hunk == null) throw new InvalidOperationException("无法解析 diff 的 hunk 头：" + line);
                        cur.Hunks.Add(hunk);
                        continue;
                    }
                    if (hunk != null)
                    {
                        hunk.Body.Add(line);
                        continue;
                    }

                    if (line.StartsWith("GIT binary patch", StringComparison.Ordinal) ||
                        line.StartsWith("Binary files ", StringComparison.Ordinal))
                    {
                        cur.IsBinary = true;
                        inBinary = true;
                        cur.BinaryBlock.Add(line);
                        continue;
                    }

                    if (line.StartsWith("@@", StringComparison.Ordinal))
                    {
                        hunk = ParseHunkHeader(line);
                        if (hunk == null) throw new InvalidOperationException("无法解析 diff 的 hunk 头：" + line);
                        cur.Hunks.Add(hunk);
                        continue;
                    }

                    cur.Header.Add(line);
                }
            }

            foreach (var f in files) f.Path = ExtractPath(f.Header);
            foreach (var f in files)
            {
                foreach (var line in f.Header)
                {
                    if (line.StartsWith("deleted file mode", StringComparison.Ordinal)) f.IsDeleted = true;
                }
                MarkBinaryBlob(f);
            }
            return files;
        }

        /// <summary>
        /// git 的二进制判别算法：看文件头 8000 字节里有没有 0x00。
        /// git 用大白话讲就是「能在前 8000 字节里找到 NUL 就算二进制」（见 buffer_is_binary）。
        /// 这里自己读工作区文件再判一遍，覆盖 git 被 .gitattributes（比如 *.bin text）误导成文本的情况。
        /// </summary>
        private static void MarkBinaryBlob(DiffFile f)
        {
            if (f.IsBinary || string.IsNullOrEmpty(f.Path)) return;
            var abs = ToWorkingPath(f.Path);
            if (abs == null) return;
            // 删除、改名源文件等取不到内容的情形，直接信 git 的判定
            f.IsBinaryBlob = BinaryDetector.IsBinaryFile(abs);
        }

        private static string ToWorkingPath(string repoRelativePath)
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir)) return null;
            try
            {
                return Path.Combine(GitRunner.WorkingDir, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
            }
            catch (ArgumentException)
            {
                return null; // 路径里有非法字符，交给 git
            }
        }

        private static DiffHunk ParseHunkHeader(string line)
        {
            var m = HunkHeaderRegex.Match(line);
            if (!m.Success) return null;
            var h = new DiffHunk { RawHeader = line, OldStart = int.Parse(m.Groups[1].Value) };
            h.OldCount = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
            h.NewStart = int.Parse(m.Groups[3].Value);
            h.NewCount = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 1;
            return h;
        }

        private static string ExtractPath(List<string> header)
        {
            string minus = null, plus = null, fromGitLine = null;
            foreach (var line in header)
            {
                if (line.StartsWith("--- ", StringComparison.Ordinal)) minus = line.Substring(4).Trim();
                else if (line.StartsWith("+++ ", StringComparison.Ordinal)) plus = line.Substring(4).Trim();
                else if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    // 二进制、删除等没有 ---/+++ 行，只能从 "diff --git a/x b/x" 里取
                    var rest = line.Substring("diff --git ".Length);
                    int mid = rest.IndexOf(" b/", StringComparison.Ordinal);
                    if (mid > 0) fromGitLine = UnquotePath(rest.Substring(mid + 3));
                }
            }
            var p = plus;
            if (string.IsNullOrEmpty(p) || p == "/dev/null") p = minus;
            if (string.IsNullOrEmpty(p) || p == "/dev/null") p = fromGitLine;
            if (string.IsNullOrEmpty(p) || p == "/dev/null") return "";
            if (p.StartsWith("a/", StringComparison.Ordinal) || p.StartsWith("b/", StringComparison.Ordinal))
                p = p.Substring(2);
            return UnquotePath(p);
        }

        private static string UnquotePath(string p)
        {
            if (p.Length >= 2 && p[0] == '"' && p[p.Length - 1] == '"')
            {
                var inner = p.Substring(1, p.Length - 2);
                return inner.Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            return p;
        }

        // ---------- 补丁生成 ----------

        private static string BuildFileHeader(DiffFile f)
        {
            var sb = new StringBuilder();
            foreach (var line in f.Header)
            {
                // 文本补丁按前缀拆分时，index 行会让 git 校验 postimage blob 而报 "corrupt patch"，必须去掉；
                // 但二进制补丁必须保留 index 行，否则 git apply 直接报 "No valid patches in input"。
                if (!f.IsBinary && line.StartsWith("index ", StringComparison.Ordinal)) continue;
                sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }

        private static string BuildPatchWholeFile(List<DiffFile> files)
        {
            if (files.Count == 0) return "";
            var f = files[0];
            var sb = new StringBuilder();
            sb.Append(BuildFileHeader(f));
            if (f.IsBinary)
            {
                foreach (var line in f.BinaryBlock) sb.Append(line).Append('\n');
                return sb.ToString();
            }
            foreach (var h in f.Hunks)
            {
                sb.Append(h.RawHeader).Append('\n');
                foreach (var body in h.Body) sb.Append(body).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>取当前剩余 diff 的前 maxLines 个改动行（按文件顺序）。</summary>
        private static string BuildPatchPrefix(List<DiffFile> files, int maxLines)
        {
            var sb = new StringBuilder();
            int consumed = 0;

            foreach (var f in files)
            {
                if (consumed >= maxLines) break;

                sb.Append(BuildFileHeader(f));

                if (f.Atomic)
                {
                    // 二进制改动、整文件删除：整段带走，可能超出本提交的额度
                    foreach (var line in f.BinaryBlock) sb.Append(line).Append('\n');
                    if (!f.IsBinary)
                    {
                        foreach (var h in f.Hunks)
                        {
                            sb.Append(h.RawHeader).Append('\n');
                            foreach (var body in h.Body) sb.Append(body).Append('\n');
                        }
                    }
                    consumed += f.ChangedLines();
                    continue;
                }

                foreach (var h in f.Hunks)
                {
                    if (consumed >= maxLines) break;
                    int need = maxLines - consumed;
                    int partLines;
                    sb.Append(BuildHunkPart(h, need, out partLines));
                    consumed += partLines;
                }
            }
            return sb.ToString();
        }

        private static string BuildHunkPart(DiffHunk h, int maxLines, out int partLines)
        {
            partLines = 0;
            int total = h.ChangedLines();
            if (maxLines >= total)
            {
                var full = new StringBuilder();
                full.Append(h.RawHeader).Append('\n');
                foreach (var line in h.Body) full.Append(line).Append('\n');
                partLines = total;
                return full.ToString();
            }

            var sb = new StringBuilder();
            int oldCount = 0, newCount = 0, taken = 0;
            foreach (var line in h.Body)
            {
                bool isChange = line.Length > 0 && (line[0] == '+' || line[0] == '-');
                if (taken >= maxLines && isChange) break;
                if (line.Length > 0 && line[0] == '\\') break; // "\ No newline at end of file" 只在整段时保留

                sb.Append(line).Append('\n');
                if (isChange)
                {
                    taken++;
                    if (line[0] == '-') oldCount++;
                    else newCount++;
                }
                else if (line.Length > 0 && line[0] == ' ')
                {
                    oldCount++;
                    newCount++;
                }
            }

            partLines = taken;
            var header = string.Format(CultureInfo.InvariantCulture,
                "@@ -{0},{1} +{2},{3} @@", h.OldStart, oldCount, h.NewStart, newCount);
            return header + "\n" + sb.ToString();
        }

        // ---------- 预览用游标（按改动顺序消费） ----------

        private class DiffCursor
        {
            private readonly List<DiffFile> _files;
            private int _fileIndex;
            private int _hunkIndex;
            private int _bodyIndex;

            public DiffCursor(List<DiffFile> files) { _files = files; }

            public int Consume(int maxLines, List<string> paths)
            {
                int consumed = 0;
                while (_fileIndex < _files.Count && consumed < maxLines)
                {
                    var f = _files[_fileIndex];
                    bool touched = false;

                    if (f.Atomic)
                    {
                        // 原子单元：整段带走，可能超出额度
                        touched = true;
                        consumed += f.ChangedLines();
                        if (!string.IsNullOrEmpty(f.Path)) paths.Add(f.Path);
                        _fileIndex++;
                        _hunkIndex = 0;
                        _bodyIndex = 0;
                        continue;
                    }

                    if (f.Hunks.Count == 0)
                    {
                        // 只有模式变更的文件，不占改动行
                        touched = true;
                        if (!string.IsNullOrEmpty(f.Path)) paths.Add(f.Path);
                        _fileIndex++;
                        _hunkIndex = 0;
                        _bodyIndex = 0;
                        continue;
                    }

                    while (_hunkIndex < f.Hunks.Count && consumed < maxLines)
                    {
                        var h = f.Hunks[_hunkIndex];
                        int room = maxLines - consumed;
                        int remaining = RemainingChanged(h, _bodyIndex);
                        touched = true;
                        if (remaining <= room)
                        {
                            consumed += remaining;
                            _hunkIndex++;
                            _bodyIndex = 0;
                        }
                        else
                        {
                            _bodyIndex = AdvanceBody(h, _bodyIndex, room);
                            consumed += room;
                            break;
                        }
                    }

                    if (touched && !string.IsNullOrEmpty(f.Path))
                        paths.Add(f.Path);

                    if (_hunkIndex >= f.Hunks.Count)
                    {
                        _fileIndex++;
                        _hunkIndex = 0;
                        _bodyIndex = 0;
                    }
                    else
                    {
                        break; // 本文件还没取完，额度已用尽
                    }
                }
                return consumed;
            }

            private static int RemainingChanged(DiffHunk h, int from)
            {
                int n = 0;
                for (int i = from; i < h.Body.Count; i++)
                {
                    var line = h.Body[i];
                    if (line.Length > 0 && (line[0] == '+' || line[0] == '-')) n++;
                }
                return n;
            }

            private static int AdvanceBody(DiffHunk h, int from, int take)
            {
                int taken = 0;
                int i = from;
                for (; i < h.Body.Count; i++)
                {
                    var line = h.Body[i];
                    if (line.Length > 0 && (line[0] == '+' || line[0] == '-'))
                    {
                        if (taken >= take) break;
                        taken++;
                    }
                }
                return i;
            }
        }
    }

    /// <summary>
    /// 二进制判别，算法照 git（buffer_is_binary）：只看文件前 8000 字节，出现 0x00 就算二进制。
    /// 纯文本、UTF-8 中文、UTF-16 之外的常见文本都不会有 NUL；图片/压缩包/可执行文件基本必然命中。
    /// </summary>
    internal static class BinaryDetector
    {
        private const int SampleBytes = 8000;

        public static bool IsBinaryFile(string absolutePath)
        {
            try
            {
                if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath)) return false;
                using (var fs = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var buffer = new byte[SampleBytes];
                    int read = fs.Read(buffer, 0, buffer.Length);
                    for (int i = 0; i < read; i++)
                    {
                        if (buffer[i] == 0) return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                // 读不到内容（被占用、没权限）就交给 git 自己的判定
                return false;
            }
        }
    }
}
