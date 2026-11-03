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
        private List<GitStatusEntry> _unstaged = new List<GitStatusEntry>();
        private List<GitStatusEntry> _staged = new List<GitStatusEntry>();
        private bool _busy;

        public Form1()
        {
            InitializeComponent();
            GitRunner.ProbeGit();
            this.Load += Form1_Load;
            LoadEmbeddedIcon();
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

        private void Form1_Load(object sender, EventArgs e)
        {
            Log("betterGitGUI 已启动。git: " + GitRunner.GitExe);
            Log("请点击「打开」选择一个 Git 仓库目录，或点击「初始化」新建仓库。");
            // 默认尝试打开当前程序所在目录（如果本身是仓库）
            var here = AppDomain.CurrentDomain.BaseDirectory;
            var root = FindGitRoot(here);
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
            repoStatusLabel.Text = "仓库: " + path;
            Text = "betterGitGUI - " + Path.GetFileName(path);
            RefreshAll();
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
            _unstaged.Clear();
            _staged.Clear();

            if (result.Success)
            {
                using (var reader = new StringReader(result.StdOut))
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

                        if (entry.IsUntracked)
                        {
                            _unstaged.Add(entry);
                        }
                        else
                        {
                            if (entry.IsStaged) _staged.Add(entry);
                            if (entry.WorkTreeStatus != " " && entry.WorkTreeStatus != "?") _unstaged.Add(entry);
                        }
                    }
                }
            }
            else
            {
                Log(result);
            }

            PopulateList(listUnstaged, _unstaged, true);
            PopulateList(listStaged, _staged, false);
        }

        private static string Unquote(string s)
        {
            if (s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\""))
                return s.Substring(1, s.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return s;
        }

        private void PopulateList(ListView list, List<GitStatusEntry> entries, bool unstaged)
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var e in entries)
            {
                var status = unstaged ? e.WorkTreeStatus : e.StagedStatus;
                var item = new ListViewItem(status);
                item.SubItems.Add(e.DisplayPath);
                item.Tag = e;
                if (e.IsUntracked) item.ForeColor = Color.DarkOrange;
                else if (status == "D") item.ForeColor = Color.Firebrick;
                else if (status == "M") item.ForeColor = Color.DarkBlue;
                list.Items.Add(item);
            }
            list.EndUpdate();
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
            // 用 -F - 从 stdin 读取，避免引号/转义问题
            RunGitBusyCommit("commit -F -", msg);
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
                RunGitBusyCommit("commit --amend -F -", msg);
            }
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

        // ---------- 其他一键功能 ----------

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

        private void LogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var r = GitRunner.RunUi("log --graph --oneline --all --decorate -n 200");
            using (var dlg = new Form())
            {
                dlg.Text = "提交日志 (最近 200 条)";
                dlg.Width = 800;
                dlg.Height = 500;
                dlg.StartPosition = FormStartPosition.CenterParent;
                var tb = new TextBox
                {
                    Multiline = true,
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 9F),
                    Text = r.Success ? r.StdOut : (r.StdOut + r.StdErr),
                };
                dlg.Controls.Add(tb);
                dlg.ShowDialog(this);
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(this,
                "betterGitGUI\n一个简单的 Git GUI 客户端\n\n直接调用 git.exe 完成所有操作。",
                "关于");
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
        /// 执行需要从 stdin 读取提交信息的命令（commit -F -）。
        /// </summary>
        private async void RunGitBusyCommit(string args, string message)
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                var result = await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = GitRunner.GitExe,
                        Arguments = "-c core.quotepath=false -c i18n.logoutputencoding=utf-8 -c i18n.commitencoding=utf-8 " + args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = new UTF8Encoding(false),
                        StandardErrorEncoding = new UTF8Encoding(false),
                    };
                    psi.WorkingDirectory = GitRunner.WorkingDir;
                    var p = Process.Start(psi);
                    var msgBytes = new UTF8Encoding(false).GetBytes(message + "\n");
                    p.StandardInput.BaseStream.Write(msgBytes, 0, msgBytes.Length);
                    p.StandardInput.BaseStream.Flush();
                    p.StandardInput.Close();
                    var soTask = p.StandardOutput.ReadToEndAsync();
                    var seTask = p.StandardError.ReadToEndAsync();
                    p.WaitForExit();
                    return new GitResult
                    {
                        ExitCode = p.ExitCode,
                        StdOut = soTask.Result,
                        StdErr = seTask.Result,
                        Command = "git " + args + " (-F -)",
                    };
                });
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
