using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// diff 里的一个块（hunk）：从 @@ 行开始到下一个 @@ 之前的所有行。
    /// </summary>
    public class DiffHunk
    {
        /// <summary>块头，形如 "@@ -1,7 +1,9 @@ 方法名"。</summary>
        public string HeaderLine = "";

        /// <summary>块体，保留每行行首的 ' ' / '+' / '-' / '\' 标记。</summary>
        public List<string> Lines = new List<string>();

        /// <summary>块体里新增的行数（显示在右侧角标上）。</summary>
        public int Added
        {
            get { return Lines.Count(l => l.StartsWith("+")); }
        }

        /// <summary>块体里删除的行数。</summary>
        public int Removed
        {
            get { return Lines.Count(l => l.StartsWith("-")); }
        }
    }

    /// <summary>
    /// 单个文件的 unified diff：文件头（diff --git / index / --- / +++ 等）+ 若干块。
    /// 只做文本解析与补丁拼装，不调用 git，便于单独验证。
    /// </summary>
    public class DiffFile
    {
        public List<string> Header = new List<string>();
        public List<DiffHunk> Hunks = new List<DiffHunk>();
        public bool IsBinary;

        public bool HasHunks { get { return Hunks.Count > 0; } }

        /// <summary>
        /// 解析 git diff 的输出。多文件时应逐个文件调用；这里只保留第一个文件的块。
        /// </summary>
        public static DiffFile Parse(string diffText)
        {
            var file = new DiffFile();
            if (string.IsNullOrEmpty(diffText)) return file;

            var lines = diffText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int i = 0;

            // 文件头：从 diff --git 一直到第一个 @@ 之前
            for (; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("@@")) break;
                file.Header.Add(line);
                if (line.StartsWith("Binary files") || line.StartsWith("GIT binary patch"))
                    file.IsBinary = true;
            }

            // 块体
            DiffHunk current = null;
            for (; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("@@"))
                {
                    current = new DiffHunk { HeaderLine = line };
                    file.Hunks.Add(current);
                    continue;
                }

                // 意外遇到下一个文件时停止（防止把别的文件的改动混进来）
                if (line.StartsWith("diff --git"))
                {
                    current = null;
                    continue;
                }

                if (current != null) current.Lines.Add(line);
            }

            // 末尾若因为文本以换行结尾而多出一个空行，去掉它
            foreach (var h in file.Hunks)
            {
                while (h.Lines.Count > 0 && h.Lines[h.Lines.Count - 1].Length == 0)
                    h.Lines.RemoveAt(h.Lines.Count - 1);
            }

            // git 有时会把空行写成只有一个空格的上下文行，保持原样即可
            return file;
        }

        /// <summary>
        /// 把指定的若干块拼成一个可以直接喂给 git apply 的补丁。
        /// 文件头原样保留，块的顺序按原文件顺序输出。
        /// </summary>
        public string BuildPatch(IEnumerable<int> hunkIndexes)
        {
            var sb = new StringBuilder();
            foreach (var line in Header) sb.Append(line).Append('\n');

            foreach (var idx in hunkIndexes.Distinct().OrderBy(x => x))
            {
                if (idx < 0 || idx >= Hunks.Count) continue;
                var hunk = Hunks[idx];
                sb.Append(hunk.HeaderLine).Append('\n');
                foreach (var line in hunk.Lines) sb.Append(line).Append('\n');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// diff 的一行，带上显示用的颜色与所属块号。
    /// </summary>
    public class DiffLine
    {
        public string Text = "";
        public Color ForeColor = Color.Black;
        public Color BackColor = Color.Empty;
        public bool Bold;
        public int HunkIndex = -1;
    }

    /// <summary>
    /// 把 diff 文本变成带颜色的行；也负责把行写进 RichTextBox。
    /// 差异面板和提交历史窗口共用这里的规则。
    /// </summary>
    public static class DiffRenderer
    {
        private static readonly Color AddedBack = Color.FromArgb(230, 255, 236);
        private static readonly Color RemovedBack = Color.FromArgb(255, 235, 235);

        public static List<DiffLine> BuildLines(string diffText)
        {
            var result = new List<DiffLine>();
            if (string.IsNullOrEmpty(diffText)) return result;

            var lines = diffText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool inHunk = false;
            int hunkIndex = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                var text = lines[i];
                var item = new DiffLine { Text = text };

                if (text.StartsWith("@@"))
                {
                    hunkIndex++;
                    inHunk = true;
                    item.ForeColor = Color.FromArgb(0, 110, 130);
                    item.Bold = true;
                    item.HunkIndex = hunkIndex;
                }
                else if (!inHunk)
                {
                    if (text.StartsWith("diff --git") || text.StartsWith("index ") ||
                        text.StartsWith("old mode") || text.StartsWith("new mode") ||
                        text.StartsWith("new file") || text.StartsWith("deleted file") ||
                        text.StartsWith("similarity") || text.StartsWith("rename from") ||
                        text.StartsWith("rename to") || text.StartsWith("Binary files") ||
                        text.StartsWith("GIT binary patch"))
                    {
                        item.ForeColor = Color.FromArgb(110, 110, 110);
                        item.Bold = true;
                    }
                    else if (text.StartsWith("---")) item.ForeColor = Color.FromArgb(150, 30, 30);
                    else if (text.StartsWith("+++")) item.ForeColor = Color.FromArgb(20, 110, 40);
                    else item.ForeColor = Color.FromArgb(110, 110, 110);
                }
                else
                {
                    item.HunkIndex = hunkIndex;
                    if (text.StartsWith("+"))
                    {
                        item.ForeColor = Color.FromArgb(20, 110, 40);
                        item.BackColor = AddedBack;
                    }
                    else if (text.StartsWith("-"))
                    {
                        item.ForeColor = Color.FromArgb(160, 30, 30);
                        item.BackColor = RemovedBack;
                    }
                    else if (text.StartsWith("\\"))
                    {
                        item.ForeColor = Color.FromArgb(130, 130, 130);
                    }
                    else
                    {
                        item.ForeColor = Color.Black;
                    }
                }

                result.Add(item);
            }

            // 文本以换行结尾时 split 会多出一个空元素，去掉避免尾部多一行
            if (result.Count > 0 && result[result.Count - 1].Text.Length == 0)
                result.RemoveAt(result.Count - 1);

            return result;
        }

        /// <summary>
        /// 把带颜色的行写进只读的 RichTextBox。
        /// 位置一律用 RichTextBox 自己的行索引换算：控件内部按 CRLF 存文本，
        /// 用 '\\n' 自己算偏移会一行错一位，整段着色都会歪。
        /// </summary>
        public static void ApplyTo(RichTextBox box, IList<DiffLine> lines)
        {
            box.SuspendLayout();

            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i].Text);
                if (i < lines.Count - 1) sb.Append("\r\n");
            }

            box.Text = sb.ToString();
            var physicalLines = box.Lines;

            using (var boldFont = new Font(box.Font, FontStyle.Bold))
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    if (line.Text.Length == 0) continue;
                    if (line.ForeColor == Color.Black && line.BackColor == Color.Empty && !line.Bold)
                        continue;

                    var start = box.GetFirstCharIndexFromLine(i);
                    if (start < 0 || start >= box.TextLength) continue;
                    var length = i < physicalLines.Length ? physicalLines[i].Length : line.Text.Length;

                    box.SelectionStart = start;
                    box.SelectionLength = length;
                    box.SelectionColor = line.ForeColor;
                    box.SelectionBackColor = line.BackColor != Color.Empty ? line.BackColor : box.BackColor;
                    if (line.Bold) box.SelectionFont = boldFont;
                }
            }

            box.SelectionStart = 0;
            box.SelectionLength = 0;

            box.ResumeLayout();
        }

        /// <summary>
        /// 给一行文本套上 diff 的前缀颜色（用于把未跟踪文件按"全新增"展示）。
        /// </summary>
        public static DiffLine AsAddedLine(string text, int hunkIndex)
        {
            return new DiffLine
            {
                Text = "+" + text,
                ForeColor = Color.FromArgb(20, 110, 40),
                BackColor = AddedBack,
                HunkIndex = hunkIndex,
            };
        }
    }
}
