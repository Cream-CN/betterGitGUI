using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterGitGUI
{
    public partial class Form1 : Form
    {
        /// <summary>差异面板最多渲染多少行，防止超大 diff 卡死界面。</summary>
        private const int MaxDiffLines = 5000;

        private List<GitStatusEntry> _unstaged = new List<GitStatusEntry>();
        private List<GitStatusEntry> _staged = new List<GitStatusEntry>();
        private bool _busy;

        private string _filter = "";

        /// <summary>上次刷新得到的 status 指纹；没变化就不重填列表，避免自动刷新时闪动。</summary>
        private string _lastStatusSignature = "";

        /// <summary>当前差异面板显示的文件；刷新后用它把选中项还原回去。</summary>
        private string _diffPath;
        private DiffMode _diffMode = DiffMode.None;
        private DiffFile _diffFile;
        private List<DiffLine> _diffLines = new List<DiffLine>();
        private int _diffSeq;
        private bool _restoringSelection;
        private DateTime _lastAutoRefresh = DateTime.MinValue;

        private List<string> _recentRepos = new List<string>();

        private enum DiffMode
        {
            None,
            Unstaged,
            Staged,
            Untracked,
        }

        public Form1()
        {
            InitializeComponent();
            GitRunner.ProbeGit();
            this.Load += Form1_Load;
            LoadEmbeddedIcon();

            BuildContextMenus();
            WireEvents();
        }

        private void LoadEmbeddedIcon()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream("betterGitGUI.icon.ico"))
            {
                if (stream != null)
                {
                    this.Icon = new System.Drawing.Icon(stream);
                }
            }
        }

        private void WireEvents()
        {
            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;
            this.Activated += Form1_Activated;

            listUnstaged.SelectedIndexChanged += (s, e) => OnListSelectionChanged(listUnstaged, DiffMode.Unstaged);
            listStaged.SelectedIndexChanged += (s, e) => OnListSelectionChanged(listStaged, DiffMode.Staged);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Log("betterGitGUI 已启动。git: " + GitRunner.GitExe);
            Log("请点击「打开」选择一个 Git 仓库目录，或点击「初始化」新建仓库。");

            _recentRepos = RepoStore.Load();
            ReloadRecentMenu();

            // 默认尝试打开当前程序所在目录（如果本身是仓库），否则打开上次用的仓库
            var here = AppDomain.CurrentDomain.BaseDirectory;
            var root = FindGitRoot(here);
            if (root == null)
            {
                foreach (var repo in _recentRepos)
                {
                    if (GitRunner.IsGitRepository(repo)) { root = repo; break; }
                }
            }
            if (root != null)
            {
                LoadRepository(root);
            }
        }

        // ---------- 仓库管理 ----------

        private void OpenRepositoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择 Git 仓库目录";
                if (Directory.Exists(GitRunner.WorkingDir)) dlg.SelectedPath = GitRunner.WorkingDir;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var path = dlg.SelectedPath;
                if (!GitRunner.IsGitRepository(path))
                {
                    var r = MessageBox.Show(this,
                        "该目录不是 Git 仓库（未找到 .git）。\n是否在该目录执行 git init？",
                        "不是仓库", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes)
                    {
                        GitRunner.WorkingDir = path;
                        var init = GitRunner.Run("init");
                        Log(init);
                        if (!init.Success) { MessageBox.Show(this, init.StdErr, "git init 失败"); return; }
                    }
                    else return;
                }
                LoadRepository(path);
            }
        }

        private void InitRepositoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择一个空目录或已有目录用于初始化新仓库";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var path = dlg.SelectedPath;
                GitRunner.WorkingDir = path;
                var init = GitRunner.Run("init");
                Log(init);
                if (init.Success)
                {
                    LoadRepository(path);
                }
                else
                {
                    MessageBox.Show(this, init.StdErr, "git init 失败");
                }
            }
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void LoadRepository(string path)
        {
            GitRunner.WorkingDir = path;
            // 状态栏只显示仓库名，完整路径放到悬停提示里，否则长路径会把分支信息挤出屏幕
            repoStatusLabel.Text = "仓库: " + Path.GetFileName(path);
            repoStatusLabel.ToolTipText = path;
            Text = "betterGitGUI - " + Path.GetFileName(path);

            _lastStatusSignature = "";
            _diffPath = null;
            _diffMode = DiffMode.None;

            _recentRepos = RepoStore.Add(path);
            ReloadRecentMenu();

            RefreshAll();
        }

        private void ReloadRecentMenu()
        {
            recentMenu.DropDownItems.Clear();

            if (_recentRepos.Count == 0)
            {
                var none = new ToolStripMenuItem("（暂无记录）");
                none.Enabled = false;
                recentMenu.DropDownItems.Add(none);
                return;
            }

            foreach (var repo in _recentRepos)
            {
                var item = new ToolStripMenuItem(repo);
                item.ToolTipText = repo;
                var target = repo;
                item.Click += (s, e) => OpenRecentRepository(target);
                recentMenu.DropDownItems.Add(item);
            }

            recentMenu.DropDownItems.Add(new ToolStripSeparator());
            var clear = new ToolStripMenuItem("清空最近列表");
            clear.Click += (s, e) =>
            {
                _recentRepos.Clear();
                RepoStore.Save(_recentRepos);
                ReloadRecentMenu();
            };
            recentMenu.DropDownItems.Add(clear);
        }

        private void OpenRecentRepository(string path)
        {
            if (!Directory.Exists(path))
            {
                MessageBox.Show(this, "目录已不存在：" + path, "打开失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _recentRepos.RemoveAll(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase));
                RepoStore.Save(_recentRepos);
                ReloadRecentMenu();
                return;
            }
            if (!GitRunner.IsGitRepository(path))
            {
                MessageBox.Show(this, "该目录已经不是 Git 仓库：" + path, "打开失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _recentRepos.RemoveAll(r => string.Equals(r, path, StringComparison.OrdinalIgnoreCase));
                RepoStore.Save(_recentRepos);
                ReloadRecentMenu();
                return;
            }
            LoadRepository(path);
        }

        private static string FindGitRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, ".git")) ||
                    Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        // ---------- 刷新 / 状态 ----------

        private void RefreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshAll();
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            // 自动刷新：切回窗口时如果距上次刷新超过 1 秒，就重新读一次状态
            if (_busy || string.IsNullOrEmpty(GitRunner.WorkingDir)) return;
            var now = DateTime.Now;
            if ((now - _lastAutoRefresh).TotalSeconds < 1) return;
            _lastAutoRefresh = now;
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir)) return;
            if (!GitRunner.IsGitRepository(GitRunner.WorkingDir)) return;

            RefreshStatus();
            RefreshBranch();
        }

        private void RefreshStatus()
        {
            var result = GitRunner.RunUi("status --porcelain -uall");
            if (!result.Success)
            {
                Log(result);
                return;
            }

            var fresh = ParseStatus(result.StdOut);
            var signature = BuildSignature(fresh);

            // 状态没变：不重填列表，保留当前选中项和差异面板
            if (signature == _lastStatusSignature)
                return;

            _lastStatusSignature = signature;
            _unstaged = fresh.Where(x => x.List == EntryList.Unstaged).ToList();
            _staged = fresh.Where(x => x.List == EntryList.Staged).ToList();

            _restoringSelection = true;
            try
            {
                PopulateList(listUnstaged, _unstaged, true);
                PopulateList(listStaged, _staged, false);
            }
            finally
            {
                _restoringSelection = false;
            }

            RestoreDiffSelection();
        }

        private string BuildSignature(List<GitStatusEntry> entries)
        {
            var sb = new StringBuilder();
            foreach (var e in entries)
                sb.Append(e.List).Append('|').Append(e.StagedStatus).Append(e.WorkTreeStatus)
                  .Append('|').Append(e.Path).Append('\n');
            return sb.ToString();
        }

        private List<GitStatusEntry> ParseStatus(string output)
        {
            var list = new List<GitStatusEntry>();
            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length < 3) continue;
                    var entry = new GitStatusEntry
                    {
                        StagedStatus = line.Substring(0, 1),
                        WorkTreeStatus = line.Substring(1, 1),
                        Path = line.Substring(3),
                    };

                    // 处理重命名: "R  old -> new"
                    var arrow = entry.Path.IndexOf(" -> ");
                    if (arrow > 0)
                    {
                        entry.OldPath = entry.Path.Substring(0, arrow);
                        entry.Path = entry.Path.Substring(arrow + 4);
                    }
                    // 去掉引号（porcelain 会给特殊字符路径加引号）
                    entry.Path = Unquote(entry.Path);
                    if (!string.IsNullOrEmpty(entry.OldPath)) entry.OldPath = Unquote(entry.OldPath);

                    if (entry.IsUnmerged)
                    {
                        // 冲突文件只放一边，避免左右两栏重复出现
                        entry.List = EntryList.Unstaged;
                        list.Add(entry);
                    }
                    else if (entry.IsUntracked)
                    {
                        entry.List = EntryList.Unstaged;
                        list.Add(entry);
                    }
                    else
                    {
                        if (entry.IsStaged)
                        {
                            var stagedEntry = entry.Clone();
                            stagedEntry.List = EntryList.Staged;
                            list.Add(stagedEntry);
                        }
                        if (entry.WorkTreeStatus != " " && entry.WorkTreeStatus != "?")
                        {
                            entry.List = EntryList.Unstaged;
                            list.Add(entry);
                        }
                    }
                }
            }
            return list;
        }

        private static string Unquote(string s)
        {
            if (s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\""))
                return s.Substring(1, s.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return s;
        }

        private bool MatchesFilter(GitStatusEntry entry)
        {
            if (_filter.Length == 0) return true;
            return entry.Path.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void PopulateList(ListView list, List<GitStatusEntry> entries, bool unstaged)
        {
            var selected = new HashSet<string>();
            foreach (ListViewItem item in list.SelectedItems)
            {
                var e = item.Tag as GitStatusEntry;
                if (e != null) selected.Add(e.Path);
            }

            list.BeginUpdate();
            list.Items.Clear();
            foreach (var e in entries)
            {
                if (!MatchesFilter(e)) continue;

                var status = unstaged ? e.WorkTreeStatus : e.StagedStatus;
                if (e.IsUnmerged) status = "冲突";
                var item = new ListViewItem(status);
                item.SubItems.Add(e.DisplayPath);
                item.Tag = e;

                if (e.IsUnmerged)
                {
                    item.ForeColor = Color.Firebrick;
                    item.Font = new Font(list.Font, FontStyle.Bold);
                }
                else if (e.IsUntracked) item.ForeColor = Color.DarkOrange;
                else if (status == "D") item.ForeColor = Color.Firebrick;
                else if (status == "M") item.ForeColor = Color.DarkBlue;

                list.Items.Add(item);

                if (selected.Contains(e.Path)) item.Selected = true;
            }
            list.EndUpdate();
        }

        /// <summary>
        /// 刷新后把差异面板对应的文件重新选中，否则用户每次刷新都要重新点一遍。
        /// </summary>
        private void RestoreDiffSelection()
        {
            if (_diffPath == null) return;

            var order = _diffMode == DiffMode.Staged
                ? new[] { DiffMode.Staged, DiffMode.Unstaged }
                : new[] { DiffMode.Unstaged, DiffMode.Staged };

            foreach (var mode in order)
            {
                var list = mode == DiffMode.Staged ? listStaged : listUnstaged;
                var entry = TrySelect(list, mode == _diffMode);
                if (entry != null)
                {
                    ShowDiff(mode, entry);
                    return;
                }
            }

            // 文件已经不在改动列表里（例如全部提交了）
            ClearDiff();
        }

        private GitStatusEntry TrySelect(ListView list, bool primary)
        {
            foreach (ListViewItem item in list.Items)
            {
                var e = item.Tag as GitStatusEntry;
                if (e != null && string.Equals(e.Path, _diffPath, StringComparison.Ordinal))
                {
                    if (!item.Selected)
                    {
                        _restoringSelection = true;
                        item.Selected = true;
                        item.Focused = primary;
                        _restoringSelection = false;
                    }
                    return e;
                }
            }
            return null;
        }

        private void RefreshBranch()
        {
            var head = GitRunner.RunUi("rev-parse --abbrev-ref HEAD");
            if (head.Success)
            {
                branchStatusLabel.Text = "分支: " + head.StdOut.Trim();
            }
            else
            {
                branchStatusLabel.Text = "分支: (无提交)";
            }

            // 与上游的差距：能提前看到"落后几个提交/领先几个"，避免推拉时才发现冲突
            var upstream = GitRunner.RunUi("rev-parse --abbrev-ref --symbolic-full-name @{u}");
            if (!upstream.Success)
            {
                aheadBehindLabel.Text = "  （当前分支没有上游，首次推送后显示）";
                return;
            }

            var counts = GitRunner.RunUi("rev-list --left-right --count HEAD...@{u}");
            if (!counts.Success)
            {
                aheadBehindLabel.Text = "";
                return;
            }

            var parts = counts.StdOut.Trim().Split('\t');
            if (parts.Length >= 2)
            {
                var ahead = parts[0].Trim();
                var behind = parts[1].Trim();
                aheadBehindLabel.Text = "  ↑" + ahead + " 待推送   ↓" + behind + " 待拉取   (上游 " + upstream.StdOut.Trim() + ")";
                aheadBehindLabel.ForeColor = behind != "0"
                    ? Color.FromArgb(160, 30, 30)
                    : Color.FromArgb(87, 96, 106);
            }
        }

        // ---------- 差异面板 ----------

        private void OnListSelectionChanged(ListView list, DiffMode mode)
        {
            if (_busy || _restoringSelection) return;
            if (list.SelectedItems.Count == 0) return;

            // 两边都有选中项时，以当前有焦点的那个列表为准
            var other = mode == DiffMode.Unstaged ? listStaged : listUnstaged;
            if (other.SelectedItems.Count > 0 && !list.ContainsFocus) return;

            var entry = list.SelectedItems[0].Tag as GitStatusEntry;
            if (entry == null) return;

            ShowDiff(mode, entry);
        }

        private void ClearDiff()
        {
            _diffSeq++;
            _diffPath = null;
            _diffMode = DiffMode.None;
            _diffFile = null;
            _diffLines = new List<DiffLine>();
            lblDiffTitle.Text = "差异（选中左侧文件即可查看）";
            richDiff.Clear();
        }

        private async void ShowDiff(DiffMode mode, GitStatusEntry entry)
        {
            if (entry == null) { ClearDiff(); return; }

            var seq = ++_diffSeq;
            var path = entry.Path;
            _diffPath = path;
            _diffMode = mode;
            _diffFile = null;

            var modeText = mode == DiffMode.Staged ? "已暂存" : mode == DiffMode.Unstaged ? "未暂存" : "未跟踪";
            lblDiffTitle.Text = "差异：" + path + "  [" + modeText + "]" + (entry.IsUnmerged ? "  —— 存在冲突" : "");

            List<DiffLine> lines = null;
            DiffFile diffFile = null;

            if (entry.IsUnmerged)
            {
                lines = new List<DiffLine>
                {
                    new DiffLine { Text = "该文件存在合并冲突。右键左侧列表可以选择「用我的版本 / 用对方版本 / 标记已解决」。", ForeColor = Color.Firebrick },
                    new DiffLine { Text = "" },
                };
                var conflictText = await Task.Run(() =>
                    GitRunner.RunUi("diff --no-color --no-ext-diff --no-textconv -- " + GitRunner.Quote(path)));
                if (seq != _diffSeq) return;
                if (conflictText.Success && !string.IsNullOrWhiteSpace(conflictText.StdOut))
                    lines.AddRange(DiffRenderer.BuildLines(conflictText.StdOut));
                RenderDiff(lines);
                return;
            }

            if (mode == DiffMode.Untracked)
            {
                var content = await Task.Run(() => ReadTextForPreview(Path.Combine(GitRunner.WorkingDir, path)));
                if (seq != _diffSeq) return;

                lines = new List<DiffLine>
                {
                    new DiffLine { Text = "（未跟踪文件，下面是全部内容；整文件暂存后才会进入右侧列表）", ForeColor = Color.DarkOrange, Bold = true },
                };
                if (content.IsBinary)
                {
                    lines.Add(new DiffLine { Text = "（二进制文件，无法显示差异）", ForeColor = Color.Gray });
                }
                else if (content.Error != null)
                {
                    lines.Add(new DiffLine { Text = content.Error, ForeColor = Color.Firebrick });
                }
                else
                {
                    var contentLines = content.Text.Replace("\r\n", "\n").Split('\n');
                    if (contentLines.Length > MaxDiffLines)
                    {
                        lines.Add(new DiffLine { Text = "（文件过长，仅显示前 " + MaxDiffLines + " 行）", ForeColor = Color.Gray });
                        contentLines = contentLines.Take(MaxDiffLines).ToArray();
                    }
                    foreach (var l in contentLines) lines.Add(DiffRenderer.AsAddedLine(l, -1));
                }
                RenderDiff(lines);
                return;
            }

            var result = await Task.Run(() => GitRunner.RunUi(BuildDiffArgs(mode, path)));
            if (seq != _diffSeq) return;

            if (!result.Success)
            {
                lines = new List<DiffLine>
                {
                    new DiffLine { Text = "读取差异失败：", ForeColor = Color.Firebrick, Bold = true },
                    new DiffLine { Text = result.StdErr.Trim(), ForeColor = Color.Firebrick },
                };
                RenderDiff(lines);
                return;
            }

            diffFile = DiffFile.Parse(result.StdOut);
            lines = DiffRenderer.BuildLines(result.StdOut);

            if (lines.Count == 0)
            {
                lines.Add(new DiffLine { Text = "（没有可显示的差异）", ForeColor = Color.Gray });
            }
            else if (lines.Count > MaxDiffLines)
            {
                lines = lines.Take(MaxDiffLines).ToList();
                lines.Add(new DiffLine { Text = "（差异过长，仅显示前 " + MaxDiffLines + " 行）", ForeColor = Color.Gray });
            }

            if (seq != _diffSeq) return;
            _diffFile = diffFile;
            RenderDiff(lines);
        }

        private static string BuildDiffArgs(DiffMode mode, string path)
        {
            var args = "diff --no-color --no-ext-diff --no-textconv -- ";
            if (mode == DiffMode.Staged)
                args = "diff --cached --no-color --no-ext-diff --no-textconv -- ";
            return args + GitRunner.Quote(path);
        }

        private void RenderDiff(List<DiffLine> lines)
        {
            _diffLines = lines;
            DiffRenderer.ApplyTo(richDiff, lines);
        }

        private class PreviewText
        {
            public string Text = "";
            public bool IsBinary;
            public string Error;
        }

        private static PreviewText ReadTextForPreview(string fullPath)
        {
            var result = new PreviewText();
            try
            {
                if (!File.Exists(fullPath))
                {
                    result.Error = "（文件已不存在）";
                    return result;
                }
                if (new FileInfo(fullPath).Length > 2 * 1024 * 1024)
                {
                    result.Error = "（文件超过 2MB，不预览）";
                    return result;
                }

                var bytes = File.ReadAllBytes(fullPath);
                if (bytes.Any(b => b == 0))
                {
                    result.IsBinary = true;
                    return result;
                }

                try
                {
                    result.Text = new UTF8Encoding(false, true).GetString(bytes);
                }
                catch
                {
                    result.Text = Encoding.Default.GetString(bytes);
                }
            }
            catch (Exception ex)
            {
                result.Error = "（读取失败：" + ex.Message + "）";
            }
            return result;
        }

        // ---------- 差异块操作（按块暂存 / 取消暂存 / 丢弃） ----------

        private int GetHunkUnderCursor()
        {
            if (_diffLines.Count == 0) return -1;
            var point = richDiff.PointToClient(Cursor.Position);
            var charIndex = richDiff.GetCharIndexFromPosition(point);
            var line = richDiff.GetLineFromCharIndex(charIndex);
            if (line < 0 || line >= _diffLines.Count) return -1;
            return _diffLines[line].HunkIndex;
        }

        private async void ApplyHunk(int hunkIndex, bool cached, bool reverse, bool confirmDiscard)
        {
            if (_diffFile == null || hunkIndex < 0 || hunkIndex >= _diffFile.Hunks.Count) return;
            if (_busy) return;

            if (confirmDiscard)
            {
                var hunk = _diffFile.Hunks[hunkIndex];
                var answer = MessageBox.Show(this,
                    "丢弃这一块改动？\n\n+ " + hunk.Added + " 行，- " + hunk.Removed + " 行\n\n丢弃后无法恢复。",
                    "丢弃改动块", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            var patch = _diffFile.BuildPatch(new[] { hunkIndex });
            var mode = _diffMode;
            var path = _diffPath;

            SetBusy(true);
            try
            {
                var result = await Task.Run(() => GitRunner.RunApplyPatch(patch, cached, reverse));
                Log(result);
                if (!result.Success)
                {
                    MessageBox.Show(this,
                        "这一块没能应用成功。\n\n" + (result.StdErr + "\n" + result.StdOut).Trim(),
                        "应用改动块失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                SetBusy(false);
            }

            RefreshAll();
            ReselectAfterHunk(mode, path);
        }

        private void ReselectAfterHunk(DiffMode mode, string path)
        {
            var target = mode == DiffMode.Staged ? listStaged : listUnstaged;
            foreach (ListViewItem item in target.Items)
            {
                var e = item.Tag as GitStatusEntry;
                if (e != null && e.Path == path)
                {
                    _restoringSelection = true;
                    item.Selected = true;
                    item.Focused = true;
                    _restoringSelection = false;
                    ShowDiff(mode, e);
                    return;
                }
            }
            // 文件已经不是这个状态了（例如最后一块被暂存），转到另一个列表
            var other = mode == DiffMode.Staged ? listUnstaged : listStaged;
            var otherMode = mode == DiffMode.Staged ? DiffMode.Unstaged : DiffMode.Staged;
            foreach (ListViewItem item in other.Items)
            {
                var e = item.Tag as GitStatusEntry;
                if (e != null && e.Path == path)
                {
                    _restoringSelection = true;
                    item.Selected = true;
                    item.Focused = true;
                    _restoringSelection = false;
                    ShowDiff(otherMode, e);
                    return;
                }
            }
            ClearDiff();
        }

        // ---------- 暂存 / 取消暂存 ----------

        private List<string> GetSelectedPaths(ListView list)
        {
            var paths = new List<string>();
            foreach (ListViewItem item in list.SelectedItems)
            {
                var e = item.Tag as GitStatusEntry;
                if (e != null) paths.Add(e.Path);
            }
            return paths;
        }

        private void StageSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var paths = GetSelectedPaths(listUnstaged);
            if (paths.Count == 0) { MessageBox.Show(this, "请先在左侧未暂存列表中选择文件。"); return; }
            RunGitBusy("add -- " + GitRunner.JoinPaths(paths), true);
        }

        private void UnstageSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var paths = GetSelectedPaths(listStaged);
            if (paths.Count == 0) { MessageBox.Show(this, "请先在右侧已暂存列表中选择文件。"); return; }
            RunGitBusy("reset HEAD -- " + GitRunner.JoinPaths(paths), true);
        }

        private void StageAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunGitBusy("add -A", true);
        }

        private void UnstageAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunGitBusy("reset HEAD", true);
        }

        private void ListUnstaged_DoubleClick(object sender, EventArgs e)
        {
            if (listUnstaged.SelectedItems.Count == 0) return;
            var paths = GetSelectedPaths(listUnstaged);
            RunGitBusy("add -- " + GitRunner.JoinPaths(paths), true);
        }

        private void ListStaged_DoubleClick(object sender, EventArgs e)
        {
            if (listStaged.SelectedItems.Count == 0) return;
            var paths = GetSelectedPaths(listStaged);
            RunGitBusy("reset HEAD -- " + GitRunner.JoinPaths(paths), true);
        }

        // ---------- 提交 ----------

        private void CommitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var msg = textBoxCommit.Text.Trim();
            if (string.IsNullOrEmpty(msg))
            {
                MessageBox.Show(this, "请填写提交信息。");
                return;
            }
            if (_staged.Count == 0 && _unstaged.Count > 0)
            {
                var r = MessageBox.Show(this,
                    "暂存区是空的，现在提交不会包含任何改动。\n是否先全部暂存再提交？",
                    "没有已暂存的改动", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes)
                {
                    StageAllToolStripMenuItem_Click(sender, e);
                    return;
                }
            }
            // 提交信息写临时文件交给 git 读取，避免引号/转义以及 stdin 的 BOM 问题
            RunGitBusyCommit("commit", msg);
        }

        private void AmendToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var msg = textBoxCommit.Text.Trim();
            if (string.IsNullOrEmpty(msg))
            {
                // 不修改信息，仅追加暂存内容
                RunGitBusy("commit --amend --no-edit", true);
            }
            else
            {
                RunGitBusyCommit("commit --amend", msg);
            }
        }

        /// <summary>
        /// 把上一次提交的说明填进提交框：做 amend 或者沿用同一套描述时不用重新打字。
        /// </summary>
        private void LastMessageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var r = GitRunner.RunUi("log -1 --format=%B");
            if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut))
            {
                MessageBox.Show(this, "还没有提交记录。", "上次提交信息",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            textBoxCommit.Text = r.StdOut.Trim();
            textBoxCommit.Focus();
            textBoxCommit.SelectionStart = textBoxCommit.TextLength;
        }

        /// <summary>
        /// 撤销上次提交：git reset --soft HEAD~1，提交内容退回到暂存区，改动不会丢。
        /// </summary>
        private void UndoCommitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var head = GitRunner.RunUi("log -1 --format=\"%h %s\"");
            if (!head.Success || string.IsNullOrWhiteSpace(head.StdOut))
            {
                MessageBox.Show(this, "当前分支还没有提交，无法撤销。");
                return;
            }

            var r = MessageBox.Show(this,
                "把上次提交「" + head.StdOut.Trim() + "」退回到暂存区？\n\n"
                + "使用的是 git reset --soft HEAD~1：提交记录被移除，改动仍然保留并已暂存。",
                "撤销上次提交", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            RunGitBusy("reset --soft HEAD~1", true);
        }

        // ---------- 远程 ----------

        private void FetchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunGitBusy("fetch --all --prune", false);
        }

        private void PullToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunGitBusy("pull", false);
        }

        private void PushToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunGitBusy("push", false);
        }

        /// <summary>
        /// 一键同步：fetch → pull --rebase → push。任何一步失败就停下，不继续往下做。
        /// </summary>
        private async void SyncToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_busy) return;
            if (string.IsNullOrEmpty(GitRunner.WorkingDir)) { MessageBox.Show(this, "请先打开或初始化仓库。"); return; }
            if (_unstaged.Count > 0 || _staged.Count > 0)
            {
                var answer = MessageBox.Show(this,
                    "工作区还有未提交的改动，同步（pull --rebase）可能因此失败。\n建议先提交或储藏。仍要继续吗？",
                    "同步", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
            }

            SetBusy(true);
            try
            {
                var steps = new[]
                {
                    new { Name = "fetch", Args = "fetch --all --prune" },
                    new { Name = "pull --rebase", Args = "pull --rebase" },
                    new { Name = "push", Args = "push" },
                };

                foreach (var step in steps)
                {
                    var result = await Task.Run(() => GitRunner.RunUi(step.Args));
                    Log(result);
                    if (!result.Success)
                    {
                        MessageBox.Show(this,
                            "同步在 " + step.Name + " 这一步停下了。\n\n"
                            + (result.StdErr + "\n" + result.StdOut).Trim(),
                            "同步失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                Log("同步完成。");
            }
            finally
            {
                SetBusy(false);
            }
            RefreshAll();
        }

        // ---------- 远程地址 / GitHub ----------

        /// <summary>
        /// 打开远程地址窗口：识别当前是 SSH 还是 HTTPS，并切换到另一种写法。
        /// </summary>
        private void RemoteUrlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir) || !GitRunner.IsGitRepository(GitRunner.WorkingDir))
            {
                MessageBox.Show(this, "请先打开或初始化仓库。");
                return;
            }
            if (_busy) return;

            using (var dlg = new RemotesForm())
            {
                dlg.ShowDialog(this);
                if (dlg.Changed)
                {
                    Log("远程地址已更新。");
                    RefreshAll();
                }
            }
        }

        private void GitHubPanelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir) || !GitRunner.IsGitRepository(GitRunner.WorkingDir))
            {
                MessageBox.Show(this, "请先打开或初始化仓库。");
                return;
            }
            if (_busy) return;

            using (var dlg = new GitHubForm())
            {
                dlg.ShowDialog(this);
                if (dlg.RepoChanged) RefreshAll();
            }
        }

        /// <summary>
        /// 打开仓库网页：优先交给 gh（它知道工作仓库和上游 fork），
        /// 没有 gh 或调用失败时，把 origin 地址转成 https 网页地址直接打开。
        /// </summary>
        private void GitHubOpenRepoMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir) || !GitRunner.IsGitRepository(GitRunner.WorkingDir))
            {
                MessageBox.Show(this, "请先打开仓库。");
                return;
            }

            GhRunner.ProbeGh();
            if (GhRunner.Available)
            {
                var ghResult = GhRunner.Run("repo view --web");
                if (ghResult.Success) return;
            }

            var remote = GitRunner.RunUi("remote get-url origin");
            if (!remote.Success || string.IsNullOrWhiteSpace(remote.StdOut))
            {
                MessageBox.Show(this, "读不到 origin 的远程地址。", "打开仓库网页");
                return;
            }

            string url;
            string error;
            if (!RemoteUrl.TryToHttps(remote.StdOut.Trim(), out url, out error))
            {
                MessageBox.Show(this, "无法把远程地址转成网页地址：" + error, "打开仓库网页");
                return;
            }
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                url = url.Substring(0, url.Length - 4);

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开链接失败：" + ex.Message);
            }
        }

        // ---------- 分支 ----------

        private void CheckoutBranchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var branches = ListBranches();
            if (branches == null) return;
            using (var dlg = new Form())
            {
                dlg.Text = "检出分支";
                dlg.Width = 360;
                dlg.Height = 300;
                dlg.StartPosition = FormStartPosition.CenterParent;
                var list = new ListBox { Dock = DockStyle.Fill };
                foreach (var b in branches) list.Items.Add(b);
                var ok = new Button { Text = "检出", Dock = DockStyle.Bottom, Height = 30 };
                dlg.Controls.Add(list);
                dlg.Controls.Add(ok);
                ok.Click += (s, ev) => { if (list.SelectedItem != null) dlg.DialogResult = DialogResult.OK; };
                if (dlg.ShowDialog(this) == DialogResult.OK && list.SelectedItem != null)
                {
                    var name = list.SelectedItem.ToString().TrimStart('*', ' ');
                    RunGitBusy("checkout " + GitRunner.Quote(name), true);
                }
            }
        }

        private void CreateBranchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var name = Prompt(this, "新建分支", "分支名称:", "");
            if (string.IsNullOrEmpty(name)) return;
            RunGitBusy("checkout -b " + GitRunner.Quote(name), true);
        }

        private void DeleteBranchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var branches = ListBranches();
            if (branches == null) return;
            using (var dlg = new Form())
            {
                dlg.Text = "删除分支";
                dlg.Width = 360;
                dlg.Height = 300;
                dlg.StartPosition = FormStartPosition.CenterParent;
                var list = new ListBox { Dock = DockStyle.Fill };
                foreach (var b in branches) list.Items.Add(b);
                var ok = new Button { Text = "删除(已合并)", Dock = DockStyle.Bottom, Height = 30 };
                dlg.Controls.Add(list);
                dlg.Controls.Add(ok);
                ok.Click += (s, ev) => { if (list.SelectedItem != null) dlg.DialogResult = DialogResult.OK; };
                if (dlg.ShowDialog(this) == DialogResult.OK && list.SelectedItem != null)
                {
                    var name = list.SelectedItem.ToString().TrimStart('*', ' ');
                    RunGitBusy("branch -d " + GitRunner.Quote(name), true);
                }
            }
        }

        private List<string> ListBranches()
        {
            var r = GitRunner.RunUi("branch --list");
            if (!r.Success) { Log(r); return null; }
            var list = new List<string>();
            using (var reader = new StringReader(r.StdOut))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) list.Add(line);
                }
            }
            return list;
        }

        private void MergeAbortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show(this, "中止当前合并并回到合并前的状态？\n已解决的冲突改动会丢失。",
                "中止合并", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            RunGitBusy("merge --abort", true);
        }

        private void RebaseAbortToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show(this, "中止当前变基并回到变基前的状态？\n变基过程中已做的改动会丢失。",
                "中止变基", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            RunGitBusy("rebase --abort", true);
        }

        // ---------- 冲突处理 ----------

        private async void ResolveConflict(string mode)
        {
            var paths = GetSelectedPaths(listUnstaged);
            if (paths.Count == 0)
            {
                MessageBox.Show(this, "请先在左侧列表中选择冲突文件。");
                return;
            }

            string label;
            if (mode == "ours") label = "用「我的版本」（当前分支的版本）覆盖这些文件？";
            else if (mode == "theirs") label = "用「对方版本」（被合并分支的版本）覆盖这些文件？";
            else label = "把所选文件标记为已解决（暂存当前磁盘上的内容）？";

            var answer = MessageBox.Show(this, label + "\n\n" + string.Join("\n", paths),
                "解决冲突", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            SetBusy(true);
            try
            {
                foreach (var path in paths)
                {
                    if (mode == "ours" || mode == "theirs")
                    {
                        var checkout = await Task.Run(() => GitRunner.Run(
                            "checkout --" + mode + " -- " + GitRunner.Quote(path)));
                        Log(checkout);
                        if (!checkout.Success)
                        {
                            MessageBox.Show(this, (checkout.StdErr + checkout.StdOut).Trim(),
                                "取版本失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    var add = await Task.Run(() => GitRunner.Run("add -- " + GitRunner.Quote(path)));
                    Log(add);
                    if (!add.Success)
                    {
                        MessageBox.Show(this, (add.StdErr + add.StdOut).Trim(),
                            "暂存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
            }
            finally
            {
                SetBusy(false);
            }
            RefreshAll();
        }

        // ---------- 刷绿：贡献图选日 + 拆分提交 ----------

        /// <summary>
        /// 打开刷绿对话框：可以在贡献图样式的方格图上选日期，把改动拆成多个提交，
        /// 也可以只生成空提交（不碰工作区）把贡献图刷绿。
        /// </summary>
        private void SplitCommitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir) || !GitRunner.IsGitRepository(GitRunner.WorkingDir))
            {
                MessageBox.Show(this, "请先打开或初始化仓库。");
                return;
            }
            if (_busy) return;

            int made = 0;
            using (var dlg = new SplitCommitForm(textBoxCommit.Text))
            {
                dlg.ShowDialog(this);
                if (dlg.Executed) made = dlg.CommitMadeCount;
            }

            RefreshAll();
            if (made > 0)
                Log("刷绿完成：新建了 " + made + " 个提交。");
        }

        // ---------- 储藏 ----------

        private void StashSaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir) || !GitRunner.IsGitRepository(GitRunner.WorkingDir))
            {
                MessageBox.Show(this, "请先打开或初始化仓库。");
                return;
            }
            var message = Prompt(this, "保存到储藏", "储藏说明（可留空）:", textBoxCommit.Text.Trim());
            if (message == null) return;

            var args = "stash push -u" + (message.Trim().Length > 0 ? " -m " + GitRunner.Quote(message.Trim()) : "");
            RunGitBusy(args, true);
        }

        private void StashListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir) || !GitRunner.IsGitRepository(GitRunner.WorkingDir))
            {
                MessageBox.Show(this, "请先打开或初始化仓库。");
                return;
            }
            using (var dlg = new StashForm())
            {
                dlg.ShowDialog(this);
            }
            RefreshAll();
        }

        // ---------- 历史 / 其他一键功能 ----------

        private void LogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(GitRunner.WorkingDir) || !GitRunner.IsGitRepository(GitRunner.WorkingDir))
            {
                MessageBox.Show(this, "请先打开或初始化仓库。");
                return;
            }
            using (var dlg = new HistoryForm())
            {
                dlg.ShowDialog(this);
            }
            RefreshAll();
        }

        private void DiscardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var paths = GetSelectedPaths(listUnstaged);
            if (paths.Count == 0)
            {
                var r = MessageBox.Show(this,
                    "未选择文件。是否丢弃所有未暂存改动？",
                    "丢弃改动", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
                RunGitBusy("checkout -- .", true);
            }
            else
            {
                var r = MessageBox.Show(this,
                    "确定丢弃所选 " + paths.Count + " 个文件的改动？此操作不可恢复。",
                    "丢弃改动", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
                RunGitBusy("checkout -- " + GitRunner.JoinPaths(paths), true);
            }
        }

        private void ResetHardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var r = MessageBox.Show(this,
                "硬重置将丢弃所有未提交改动并恢复到 HEAD。\n确定继续？",
                "硬重置", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            RunGitBusy("reset --hard", true);
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this,
                "betterGitGUI\n一个简单的 Git GUI 客户端\n\n直接调用 git.exe 完成所有操作。\n\n"
                + "常用快捷键：F5 刷新；Ctrl+Enter 提交；Ctrl+Shift+A 全部暂存。",
                "关于");
        }

        // ---------- 右键菜单 ----------

        private void BuildContextMenus()
        {
            BuildDiffContextMenu();
            BuildListContextMenu(listUnstaged, true);
            BuildListContextMenu(listStaged, false);
        }

        private void BuildDiffContextMenu()
        {
            var menu = new ContextMenuStrip();
            var miStageHunk = new ToolStripMenuItem("暂存这一块（只暂存这个 hunk）");
            var miUnstageHunk = new ToolStripMenuItem("取消暂存这一块");
            var miDiscardHunk = new ToolStripMenuItem("丢弃这一块改动");
            var miCopyHunk = new ToolStripMenuItem("复制这一块内容");
            var miWholeFile = new ToolStripMenuItem("暂存整个文件");
            var miUnstageWhole = new ToolStripMenuItem("取消暂存整个文件");
            var miCopyDiff = new ToolStripMenuItem("复制全部差异");
            var miCopyPath = new ToolStripMenuItem("复制文件路径");

            miStageHunk.Click += (s, e) => ApplyHunk(GetHunkUnderCursor(), true, false, false);
            miUnstageHunk.Click += (s, e) => ApplyHunk(GetHunkUnderCursor(), true, true, false);
            miDiscardHunk.Click += (s, e) => ApplyHunk(GetHunkUnderCursor(), false, true, true);
            miCopyHunk.Click += (s, e) => CopyHunkUnderCursor();
            miWholeFile.Click += (s, e) =>
            {
                if (_diffPath != null) RunGitBusy("add -- " + GitRunner.Quote(_diffPath), true);
            };
            miUnstageWhole.Click += (s, e) =>
            {
                if (_diffPath != null) RunGitBusy("reset HEAD -- " + GitRunner.Quote(_diffPath), true);
            };
            miCopyDiff.Click += (s, e) => Clipboard.SetText(richDiff.Text);
            miCopyPath.Click += (s, e) =>
            {
                if (_diffPath != null) Clipboard.SetText(Path.Combine(GitRunner.WorkingDir, _diffPath));
            };

            menu.Items.Add(miStageHunk);
            menu.Items.Add(miUnstageHunk);
            menu.Items.Add(miDiscardHunk);
            menu.Items.Add(miCopyHunk);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miWholeFile);
            menu.Items.Add(miUnstageWhole);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miCopyDiff);
            menu.Items.Add(miCopyPath);

            menu.Opening += (s, e) =>
            {
                var hunk = GetHunkUnderCursor();
                var hasHunk = hunk >= 0 && _diffFile != null;
                miStageHunk.Enabled = hasHunk && _diffMode == DiffMode.Unstaged;
                miUnstageHunk.Enabled = hasHunk && _diffMode == DiffMode.Staged;
                miDiscardHunk.Enabled = hasHunk && _diffMode == DiffMode.Unstaged;
                miCopyHunk.Enabled = hasHunk;
                miWholeFile.Enabled = _diffPath != null && _diffMode != DiffMode.Staged;
                miUnstageWhole.Enabled = _diffPath != null && _diffMode == DiffMode.Staged;
                miCopyDiff.Enabled = richDiff.TextLength > 0;
                miCopyPath.Enabled = _diffPath != null;
                if (!hasHunk && _diffLines.Count > 0 && hunk < 0)
                {
                    miStageHunk.Text = "暂存这一块（把鼠标放到改动行上）";
                }
                else
                {
                    miStageHunk.Text = "暂存这一块（只暂存这个 hunk）";
                }
            };

            richDiff.ContextMenuStrip = menu;
        }

        private void CopyHunkUnderCursor()
        {
            var hunk = GetHunkUnderCursor();
            if (_diffFile == null || hunk < 0 || hunk >= _diffFile.Hunks.Count) return;
            var text = _diffFile.BuildPatch(new[] { hunk });
            Clipboard.SetText(text);
        }

        private void BuildListContextMenu(ListView list, bool unstaged)
        {
            var menu = new ContextMenuStrip();

            var miStage = new ToolStripMenuItem("暂存所选");
            var miUnstage = new ToolStripMenuItem("取消暂存所选");
            var miOurs = new ToolStripMenuItem("用我的版本解决冲突");
            var miTheirs = new ToolStripMenuItem("用对方版本解决冲突");
            var miRresolved = new ToolStripMenuItem("标记为已解决（暂存当前内容）");
            var miExplorer = new ToolStripMenuItem("在资源管理器中显示");
            var miEditor = new ToolStripMenuItem("用默认程序打开");
            var miCopyPath = new ToolStripMenuItem("复制路径");
            var miDiscard = new ToolStripMenuItem("丢弃所选改动");

            miStage.Click += (s, e) => StageSelectedToolStripMenuItem_Click(s, e);
            miUnstage.Click += (s, e) => UnstageSelectedToolStripMenuItem_Click(s, e);
            miOurs.Click += (s, e) => ResolveConflict("ours");
            miTheirs.Click += (s, e) => ResolveConflict("theirs");
            miRresolved.Click += (s, e) => ResolveConflict("resolved");
            miExplorer.Click += (s, e) => OpenInExplorer(GetSelectedPaths(list));
            miEditor.Click += (s, e) => OpenWithDefaultProgram(GetSelectedPaths(list));
            miCopyPath.Click += (s, e) =>
            {
                var paths = GetSelectedPaths(list);
                if (paths.Count > 0) Clipboard.SetText(string.Join(Environment.NewLine, paths));
            };
            miDiscard.Click += (s, e) => DiscardToolStripMenuItem_Click(s, e);

            menu.Items.Add(miStage);
            menu.Items.Add(miUnstage);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miOurs);
            menu.Items.Add(miTheirs);
            menu.Items.Add(miRresolved);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExplorer);
            menu.Items.Add(miEditor);
            menu.Items.Add(miCopyPath);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miDiscard);

            menu.Opening += (s, e) =>
            {
                var has = list.SelectedItems.Count > 0;
                var conflicted = false;
                foreach (ListViewItem item in list.SelectedItems)
                {
                    var entry = item.Tag as GitStatusEntry;
                    if (entry != null && entry.IsUnmerged) conflicted = true;
                }
                miStage.Enabled = has && unstaged && !conflicted;
                miUnstage.Enabled = has && !unstaged;
                miOurs.Enabled = has && conflicted;
                miTheirs.Enabled = has && conflicted;
                miRresolved.Enabled = has && conflicted;
                miExplorer.Enabled = has;
                miEditor.Enabled = has;
                miCopyPath.Enabled = has;
                miDiscard.Enabled = has && unstaged && !conflicted;
            };

            list.ContextMenuStrip = menu;
        }

        private void OpenInExplorer(List<string> paths)
        {
            if (paths.Count == 0) return;
            var full = Path.Combine(GitRunner.WorkingDir, paths[0]);
            try
            {
                if (File.Exists(full))
                {
                    Process.Start("explorer.exe", "/select," + GitRunner.Quote(full));
                }
                else
                {
                    var dir = Path.GetDirectoryName(full);
                    while (!Directory.Exists(dir) && dir != null && dir.Length > 0)
                        dir = Path.GetDirectoryName(dir);
                    if (dir != null) Process.Start("explorer.exe", GitRunner.Quote(dir));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开资源管理器失败：" + ex.Message);
            }
        }

        private void OpenWithDefaultProgram(List<string> paths)
        {
            if (paths.Count == 0) return;
            var full = Path.Combine(GitRunner.WorkingDir, paths[0]);
            if (!File.Exists(full))
            {
                MessageBox.Show(this, "文件已不存在：" + full);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开文件失败：" + ex.Message);
            }
        }

        // ---------- 过滤 ----------

        private void FilterToolBox_TextChanged(object sender, EventArgs e)
        {
            var text = filterToolBox.Text.Trim();
            if (text == _filter) return;
            _filter = text;

            _restoringSelection = true;
            try
            {
                PopulateList(listUnstaged, _unstaged, true);
                PopulateList(listStaged, _staged, false);
            }
            finally
            {
                _restoringSelection = false;
            }
        }

        // ---------- 快捷键 ----------

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                RefreshAll();
                e.Handled = true;
                return;
            }
            if (e.Control && e.Shift && e.KeyCode == Keys.A)
            {
                StageAllToolStripMenuItem_Click(sender, e);
                e.Handled = true;
                return;
            }
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                CommitToolStripMenuItem_Click(sender, e);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        // ---------- 执行辅助 ----------

        /// <summary>
        /// 在后台线程执行 git 命令，完成后刷新状态。
        /// </summary>
        private async void RunGitBusy(string args, bool refreshAfter)
        {
            if (_busy) return;
            if (string.IsNullOrEmpty(GitRunner.WorkingDir)) { MessageBox.Show(this, "请先打开或初始化仓库。"); return; }

            SetBusy(true);
            try
            {
                var result = await Task.Run(() => GitRunner.RunUi(args));
                Log(result);
                if (refreshAfter) RefreshAll();
                if (!result.Success && !string.IsNullOrEmpty(result.StdErr))
                {
                    MessageBox.Show(this, result.StdErr.Trim(), "git 命令出错", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        /// <summary>
        /// 执行需要提交信息的命令（commit / commit --amend）。
        /// 提交信息由 GitRunner.RunCommit 以 UTF-8 临时文件形式传入，不再经过 stdin。
        /// </summary>
        private async void RunGitBusyCommit(string args, string message)
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                var result = await Task.Run(() => GitRunner.RunCommit(args, message));
                Log(result);
                RefreshAll();
                if (result.Success)
                {
                    textBoxCommit.Clear();
                }
                else
                {
                    MessageBox.Show(this, result.StdErr.Trim(), "提交失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            this.UseWaitCursor = busy;
            menuStrip1.Enabled = !busy;
            toolStrip1.Enabled = !busy;
        }

        private void Log(GitResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("$ " + r.Command + "  (" + r.Elapsed.TotalMilliseconds.ToString("0") + " ms, exit=" + r.ExitCode + ")");
            if (!string.IsNullOrEmpty(r.StdOut)) sb.AppendLine(r.StdOut.TrimEnd());
            if (!string.IsNullOrEmpty(r.StdErr)) sb.AppendLine(r.StdErr.TrimEnd());
            sb.AppendLine("");
            AppendLog(sb.ToString());
        }

        private void Log(string msg)
        {
            AppendLog(msg + Environment.NewLine);
        }

        private void AppendLog(string text)
        {
            if (textBoxOutput.InvokeRequired)
            {
                textBoxOutput.BeginInvoke(new Action<string>(AppendLog), text);
                return;
            }
            textBoxOutput.AppendText(text);
        }

        // ---------- 简易输入框 ----------

        public static string Prompt(IWin32Window owner, string title, string label, string defaultValue)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.Width = 420;
                f.Height = 150;
                f.StartPosition = FormStartPosition.CenterParent;
                var lbl = new Label { Left = 12, Top = 12, Width = 380, Text = label };
                var txt = new TextBox { Left = 12, Top = 36, Width = 380, Text = defaultValue };
                var ok = new Button { Text = "确定", Left = 240, Width = 70, Top = 70, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "取消", Left = 320, Width = 70, Top = 70, DialogResult = DialogResult.Cancel };
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
                return f.ShowDialog(owner) == DialogResult.OK ? txt.Text : null;
            }
        }
    }
}
