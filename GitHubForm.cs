using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace betterGitGUI
{
    /// <summary>
    /// GitHub CLI（gh）面板：登录状态、仓库信息、Pull Request 与 Issue 的常用操作。
    /// 所有命令都通过 gh.exe 执行，不依赖任何在线 API 封装。
    /// </summary>
    public class GitHubForm : Form
    {
        private Label lblStatus;
        private Button btnRefreshStatus;
        private Button btnLogin;
        private Button btnPickGh;
        private Button btnOpenRepo;
        private TabControl tabs;

        private ListView listPrs;
        private TextBox txtPrDetail;
        private Button btnPrRefresh;
        private Button btnPrNew;
        private Button btnPrCheckout;
        private Button btnPrOpen;
        private Button btnPrMerge;
        private Button btnPrClose;

        private ListView listIssues;
        private TextBox txtIssueDetail;
        private Button btnIssueRefresh;
        private Button btnIssueNew;
        private Button btnIssueOpen;
        private Button btnIssueClose;

        private List<Dictionary<string, object>> _prs = new List<Dictionary<string, object>>();
        private List<Dictionary<string, object>> _issues = new List<Dictionary<string, object>>();

        private string _defaultBranch = "";
        private string _repoUrl = "";
        private bool _busy;

        /// <summary>面板里做过会改变本地仓库的操作（检出 PR / 合并），主窗口据此刷新。</summary>
        public bool RepoChanged { get; private set; }

        public GitHubForm()
        {
            BuildUi();
            GhRunner.ProbeGh();
            RefreshStatus();
        }

        // ---------- 界面 ----------

        private void BuildUi()
        {
            Text = "GitHub（gh CLI）";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1060, 680);
            MinimumSize = new Size(860, 560);
            Font = new Font("Microsoft YaHei UI", 9F);

            var top = new Panel { Dock = DockStyle.Top, Height = 78 };

            lblStatus = new Label
            {
                Location = new Point(10, 8),
                Size = new Size(700, 62),
                Text = "正在检测 gh...",
                AutoEllipsis = false,
            };

            btnRefreshStatus = new Button { Text = "刷新状态", Location = new Point(720, 10), Size = new Size(96, 28) };
            btnRefreshStatus.Click += (s, e) => RefreshStatus();

            btnLogin = new Button { Text = "登录 / 授权", Location = new Point(824, 10), Size = new Size(106, 28) };
            btnLogin.Click += (s, e) => StartLogin();

            btnPickGh = new Button { Text = "指定 gh.exe", Location = new Point(720, 44), Size = new Size(120, 28) };
            btnPickGh.Click += (s, e) => PickGh();

            btnOpenRepo = new Button { Text = "打开仓库网页", Location = new Point(850, 44), Size = new Size(130, 28) };
            btnOpenRepo.Click += (s, e) => OpenUrl(_repoUrl);

            top.Controls.AddRange(new Control[] { lblStatus, btnRefreshStatus, btnLogin, btnPickGh, btnOpenRepo });

            tabs = new TabControl { Dock = DockStyle.Fill };

            var prTab = new TabPage("Pull Requests");
            BuildPrTab(prTab);

            var issueTab = new TabPage("Issues");
            BuildIssueTab(issueTab);

            tabs.TabPages.Add(prTab);
            tabs.TabPages.Add(issueTab);

            // 隐藏的 TabPage 里 ListView 还没有窗口句柄，选中项读不到，
            // 切页时补一次详情，避免右边一直空白。
            tabs.SelectedIndexChanged += (s, e) =>
            {
                LayoutSplits();
                if (tabs.SelectedTab == prTab)
                {
                    if (txtPrDetail.TextLength == 0) LoadPrDetail();
                }
                else if (tabs.SelectedTab == issueTab)
                {
                    if (txtIssueDetail.TextLength == 0) LoadIssueDetail();
                }
            };

            Controls.Add(tabs);
            Controls.Add(top);
        }

        private void BuildPrTab(TabPage page)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 470,
                SplitterWidth = 5,
            };

            listPrs = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                HideSelection = false,
            };
            listPrs.Columns.Add("编号", 64);
            listPrs.Columns.Add("标题", 260);
            listPrs.Columns.Add("分支", 110);
            listPrs.Columns.Add("状态", 70);
            listPrs.SelectedIndexChanged += (s, e) => LoadPrDetail();
            listPrs.DoubleClick += (s, e) => OpenSelectedPr();

            split.Panel1.Controls.Add(listPrs);

            txtPrDetail = MakeDetailBox();
            split.Panel2.Controls.Add(txtPrDetail);

            // 操作按钮放左侧列表下面（按钮作用于选中的 PR），右下只留详情，避免窄栏挤爆
            var buttons = new Panel { Dock = DockStyle.Bottom, Height = 72 };
            btnPrRefresh = MakeButton("刷新", 10, 8, buttons, (s, e) => LoadPullRequests());
            btnPrNew = MakeButton("新建 PR...", 118, 8, buttons, (s, e) => CreatePullRequest());
            btnPrCheckout = MakeButton("检出到本地", 240, 8, buttons, (s, e) => CheckoutPr());
            btnPrOpen = MakeButton("浏览器打开", 350, 8, buttons, (s, e) => OpenSelectedPr());
            btnPrMerge = MakeButton("合并 (squash)", 10, 40, buttons, (s, e) => MergePr());
            btnPrClose = MakeButton("关闭 PR", 140, 40, buttons, (s, e) => ClosePr());
            split.Panel1.Controls.Add(buttons);

            _prSplit = split;
            page.Controls.Add(split);
        }

        /// <summary>
        /// 右侧详情固定约 400px 宽：SplitContainer 在构造时还没有真实宽度，
        /// 设 SplitterDistance 会被忽略或引起反复布局，所以等窗口显示后再校正。
        /// </summary>
        private bool _normalizingSplit;
        private SplitContainer _prSplit;
        private SplitContainer _issueSplit;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutSplits();
        }

        private void LayoutSplits()
        {
            if (_normalizingSplit) return;
            _normalizingSplit = true;
            try
            {
                ApplyDetailWidth(_prSplit, 400);
                ApplyDetailWidth(_issueSplit, 400);
            }
            finally
            {
                _normalizingSplit = false;
            }
        }

        private static void ApplyDetailWidth(SplitContainer split, int detailWidth)
        {
            if (split == null) return;
            if (split.Width <= detailWidth + 300) return;

            split.FixedPanel = FixedPanel.Panel2;
            var target = split.Width - detailWidth;
            if (Math.Abs(split.SplitterDistance - target) <= 2) return;
            try
            {
                split.SplitterDistance = target;
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void BuildIssueTab(TabPage page)
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 470,
                SplitterWidth = 5,
            };

            listIssues = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                GridLines = true,
                HideSelection = false,
            };
            listIssues.Columns.Add("编号", 64);
            listIssues.Columns.Add("标题", 320);
            listIssues.Columns.Add("状态", 80);
            listIssues.Columns.Add("更新", 110);
            listIssues.SelectedIndexChanged += (s, e) => LoadIssueDetail();
            listIssues.DoubleClick += (s, e) => OpenSelectedIssue();

            split.Panel1.Controls.Add(listIssues);

            txtIssueDetail = MakeDetailBox();
            split.Panel2.Controls.Add(txtIssueDetail);

            var buttons = new Panel { Dock = DockStyle.Bottom, Height = 72 };
            btnIssueRefresh = MakeButton("刷新", 10, 8, buttons, (s, e) => LoadIssues());
            btnIssueNew = MakeButton("新建 Issue...", 118, 8, buttons, (s, e) => CreateIssue());
            btnIssueOpen = MakeButton("浏览器打开", 240, 8, buttons, (s, e) => OpenSelectedIssue());
            btnIssueClose = MakeButton("关闭 Issue", 350, 8, buttons, (s, e) => CloseIssue());
            split.Panel1.Controls.Add(buttons);

            _issueSplit = split;
            page.Controls.Add(split);
        }

        private static TextBox MakeDetailBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White,
            };
        }

        private static Button MakeButton(string text, int x, int y, Panel parent, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(x >= 240 ? 104 : 96, 28),
            };
            button.Click += onClick;
            parent.Controls.Add(button);
            return button;
        }

        // ---------- gh 状态 ----------

        private async void RefreshStatus()
        {
            if (_busy) return;
            _busy = true;
            UseWaitCursor = true;
            try
            {
                GhRunner.ProbeGh();
                if (!GhRunner.Available)
                {
                    lblStatus.Text = "未找到 gh（GitHub CLI）。\r\n"
                        + "可以安装 GitHub CLI，或点右侧「指定 gh.exe」手动选择已下载的 gh.exe。";
                    SetActionsEnabled(false);
                    return;
                }

                var version = await Task.Run(() => GhRunner.Run("--version"));
                var auth = await Task.Run(() => GhRunner.Run("auth status"));
                var repo = await Task.Run(() => GhRunner.Run("repo view --json nameWithOwner,url,defaultBranchRef"));

                var firstLine = version.Success
                    ? version.StdOut.Split('\n')[0].Trim()
                    : "gh 版本未知";

                var authText = (auth.StdOut + "\n" + auth.StdErr).Trim();
                var loggedIn = FindLine(authText, "Logged in to") ?? FindLine(authText, "已登录");
                if (loggedIn == null && auth.Success) loggedIn = authText.Split('\n')[0];

                var repoDict = repo.Success ? GhRunner.ParseObject(repo.StdOut) : null;
                if (repoDict != null)
                {
                    _repoUrl = GhRunner.Str(repoDict, "url");
                    var branchRef = repoDict.ContainsKey("defaultBranchRef")
                        ? repoDict["defaultBranchRef"] as Dictionary<string, object>
                        : null;
                    _defaultBranch = branchRef == null ? "" : GhRunner.Str(branchRef, "name");
                }

                var lines = new List<string>();
                lines.Add(firstLine);
                lines.Add(loggedIn != null ? loggedIn.Trim() : "未登录（点「登录 / 授权」完成 gh auth login）");
                if (repoDict != null) lines.Add("仓库：" + GhRunner.Str(repoDict, "nameWithOwner"));
                else if (!repo.Success) lines.Add("仓库信息不可用：" + FirstLine(repo.StdErr + repo.StdOut));

                lblStatus.Text = string.Join("\r\n", lines.ToArray());
                SetActionsEnabled(true);
            }
            finally
            {
                _busy = false;
                UseWaitCursor = false;
            }

            LoadPullRequests();
            LoadIssues();
        }

        private static string FindLine(string text, string keyword)
        {
            foreach (var line in text.Split('\n'))
                if (line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return line.Trim();
            return null;
        }

        private static string FirstLine(string text)
        {
            foreach (var line in (text ?? "").Split('\n'))
                if (line.Trim().Length > 0) return line.Trim();
            return "";
        }

        private void SetActionsEnabled(bool enabled)
        {
            foreach (var button in new[]
            {
                btnPrRefresh, btnPrNew, btnPrCheckout, btnPrOpen, btnPrMerge, btnPrClose,
                btnIssueRefresh, btnIssueNew, btnIssueOpen, btnIssueClose,
            })
            {
                if (button != null) button.Enabled = enabled;
            }
            btnOpenRepo.Enabled = enabled && _repoUrl.Length > 0;
        }

        private void StartLogin()
        {
            if (!GhRunner.Available)
            {
                MessageBox.Show(this, "请先安装 gh，或点「指定 gh.exe」。", "没有 gh",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var answer = MessageBox.Show(this,
                "会打开一个控制台窗口执行 gh auth login，按提示在浏览器里输入一次性验证码即可。\r\n\r\n完成后回到这里点「刷新状态」。",
                "登录 GitHub", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (answer != DialogResult.OK) return;

            try
            {
                // 用 ShellExecute 启动，gh 是控制台程序，这样会自动带一个可交互的控制台窗口
                Process.Start(new ProcessStartInfo(
                    GhRunner.GhExe,
                    "auth login --web --hostname github.com --git-protocol https")
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "启动 gh 登录失败：" + ex.Message, "登录",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void PickGh()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "选择 gh.exe";
                dlg.Filter = "gh.exe|gh.exe|可执行文件 (*.exe)|*.exe";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                GhRunner.SetPath(dlg.FileName);
                GhRunner.ReloadProbe();
                GhRunner.ProbeGh();
                RefreshStatus();
            }
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开链接失败：" + ex.Message);
            }
        }

        // ---------- Pull Requests ----------

        private async void LoadPullRequests()
        {
            if (!GhRunner.Available || _busy) return;

            var r = await Task.Run(() => GhRunner.Run(
                "pr list --limit 40 --json number,title,state,isDraft,headRefName,baseRefName,url,author,updatedAt"));

            listPrs.BeginUpdate();
            listPrs.Items.Clear();
            _prs.Clear();

            if (!r.Success)
            {
                listPrs.EndUpdate();
                txtPrDetail.Text = "读取 PR 列表失败：\r\n" + (r.StdErr + "\r\n" + r.StdOut).Trim();
                return;
            }

            _prs = GhRunner.ParseArray(r.StdOut);
            foreach (var pr in _prs)
            {
                var item = new ListViewItem("#" + GhRunner.Str(pr, "number"));
                item.SubItems.Add(GhRunner.Str(pr, "title"));
                item.SubItems.Add(GhRunner.Str(pr, "headRefName") + " → " + GhRunner.Str(pr, "baseRefName"));
                item.SubItems.Add(PrStateText(pr));
                item.Tag = pr;
                if (GhRunner.Str(pr, "state") == "OPEN") item.ForeColor = Color.DarkBlue;
                else if (GhRunner.Str(pr, "state") == "MERGED") item.ForeColor = Color.FromArgb(20, 110, 40);
                else item.ForeColor = Color.Firebrick;
                listPrs.Items.Add(item);
            }
            listPrs.EndUpdate();

            if (listPrs.Items.Count > 0)
            {
                listPrs.Items[0].Selected = true;
                // ListView 的 SelectedIndexChanged 只在控件有焦点时才触发，
                // 首次加载时列表还没焦点，这里显式加载一次详情，避免右侧空白。
                LoadPrDetail();
            }
            else txtPrDetail.Text = "（没有 PR，或者当前仓库不是 GitHub 仓库）";
        }

        private static string PrStateText(Dictionary<string, object> pr)
        {
            var state = GhRunner.Str(pr, "state");
            var draft = GhRunner.Str(pr, "isDraft");
            if (draft.Equals("true", StringComparison.OrdinalIgnoreCase)) return state + " 草稿";
            return state;
        }

        private async void LoadPrDetail()
        {
            var item = SelectedOrFirst(listPrs);
            if (item == null) return;
            var pr = item.Tag as Dictionary<string, object>;
            if (pr == null) return;

            var number = GhRunner.Str(pr, "number");
            var r = await Task.Run(() => GhRunner.Run(
                "pr view " + number
                + " --json title,body,state,isDraft,author,headRefName,baseRefName,url,mergeable,"
                + "additions,deletions,changedFiles,reviewDecision,statusCheckRollup"));

            if (!r.Success)
            {
                txtPrDetail.Text = "读取 PR 详情失败：\r\n" + (r.StdErr + "\r\n" + r.StdOut).Trim();
                return;
            }

            var detail = GhRunner.ParseObject(r.StdOut);
            if (detail == null) { txtPrDetail.Text = "（无法解析 PR 详情）"; return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("#" + number + "  " + GhRunner.Str(detail, "title"));
            sb.AppendLine("状态: " + PrStateText(detail) + "    作者: " + GhRunner.LoginOf(detail, "author"));
            sb.AppendLine("分支: " + GhRunner.Str(detail, "headRefName") + " → " + GhRunner.Str(detail, "baseRefName"));
            sb.AppendLine("变更: +" + GhRunner.Str(detail, "additions") + "  −" + GhRunner.Str(detail, "deletions")
                + "    " + GhRunner.Str(detail, "changedFiles") + " 个文件");
            var mergeable = GhRunner.Str(detail, "mergeable");
            var review = GhRunner.Str(detail, "reviewDecision");
            sb.AppendLine("可合并: " + (mergeable.Length > 0 ? mergeable : "-")
                + "    评审结论: " + (review.Length > 0 ? review : "-"));
            sb.AppendLine("链接: " + GhRunner.Str(detail, "url"));

            var checks = GhRunner.SubList(detail, "statusCheckRollup");
            if (checks.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("检查项:");
                foreach (var check in checks.Take(20))
                {
                    var conclusion = GhRunner.Str(check, "conclusion");
                    if (conclusion.Length == 0) conclusion = GhRunner.Str(check, "status");
                    sb.AppendLine("  " + (conclusion.Length > 0 ? conclusion : "?") + "  " + GhRunner.Str(check, "name"));
                }
            }

            sb.AppendLine();
            sb.AppendLine("描述:");
            sb.AppendLine(GhRunner.Str(detail, "body"));

            txtPrDetail.Text = sb.ToString();
        }

        private void OpenSelectedPr()
        {
            var url = SelectedUrl(listPrs, "url");
            OpenUrl(url);
        }

        private void CheckoutPr()
        {
            if (!GhRunner.Available || listPrs.SelectedItems.Count == 0) return;
            var pr = listPrs.SelectedItems[0].Tag as Dictionary<string, object>;
            var number = GhRunner.Str(pr, "number");

            var dirty = GitRunner.RunUi("status --porcelain").StdOut.Trim();
            if (dirty.Length > 0)
            {
                var answer = MessageBox.Show(this,
                    "工作区有未提交改动，检出 PR 可能失败或覆盖改动。\r\n建议先提交或储藏。仍要继续吗？",
                    "检出 PR", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
            }

            var r = GhRunner.Run("pr checkout " + number);
            if (!r.Success)
            {
                MessageBox.Show(this, (r.StdErr + "\r\n" + r.StdOut).Trim(), "检出 PR 失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RepoChanged = true;
            MessageBox.Show(this, "已检出 PR #" + number + " 对应的分支。", "检出 PR",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshStatus();
        }

        private void MergePr()
        {
            if (!GhRunner.Available || listPrs.SelectedItems.Count == 0) return;
            var pr = listPrs.SelectedItems[0].Tag as Dictionary<string, object>;
            var number = GhRunner.Str(pr, "number");

            var answer = MessageBox.Show(this,
                "用 squash 方式合并 PR #" + number + "？\r\n\r\n会合并到远端仓库，之后本地需要再拉取。",
                "合并 PR", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            var r = GhRunner.Run("pr merge " + number + " --squash");
            if (!r.Success)
            {
                MessageBox.Show(this, (r.StdErr + "\r\n" + r.StdOut).Trim(), "合并失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RepoChanged = true;
            MessageBox.Show(this, "PR #" + number + " 已合并。", "合并 PR",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            LoadPullRequests();
        }

        private void ClosePr()
        {
            if (!GhRunner.Available || listPrs.SelectedItems.Count == 0) return;
            var pr = listPrs.SelectedItems[0].Tag as Dictionary<string, object>;
            var number = GhRunner.Str(pr, "number");

            var answer = MessageBox.Show(this, "关闭 PR #" + number + "（不合并）？", "关闭 PR",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            var r = GhRunner.Run("pr close " + number);
            if (!r.Success)
            {
                MessageBox.Show(this, (r.StdErr + "\r\n" + r.StdOut).Trim(), "关闭失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadPullRequests();
        }

        private void CreatePullRequest()
        {
            if (!GhRunner.Available) return;

            var head = GitRunner.RunUi("rev-parse --abbrev-ref HEAD").StdOut.Trim();
            if (head.Length == 0)
            {
                MessageBox.Show(this, "当前不在任何分支上（可能是 detached HEAD），无法新建 PR。");
                return;
            }

            using (var dlg = new NewGitHubItemForm("新建 Pull Request", true, _defaultBranch, true))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (dlg.TitleText.Trim().Length == 0)
                {
                    MessageBox.Show(this, "PR 标题不能为空。");
                    return;
                }

                var args = "pr create --title " + GitRunner.Quote(dlg.TitleText.Trim())
                         + " --head " + GitRunner.Quote(head);
                if (dlg.BaseText.Trim().Length > 0)
                    args += " --base " + GitRunner.Quote(dlg.BaseText.Trim());
                if (dlg.DraftChecked) args += " --draft";

                var r = GhRunner.RunWithBodyFile(args, dlg.BodyText, "--body");
                if (!r.Success)
                {
                    MessageBox.Show(this,
                        "新建 PR 失败。\r\n\r\n" + (r.StdErr + "\r\n" + r.StdOut).Trim()
                        + "\r\n\r\n常见原因：当前分支还没推送到远端（先用「Push」推一下），或没有权限。",
                        "新建 PR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(this, "PR 已创建：\r\n" + (r.StdOut.Trim()), "新建 PR",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadPullRequests();
            }
        }

        // ---------- Issues ----------

        private async void LoadIssues()
        {
            if (!GhRunner.Available || _busy) return;

            var r = await Task.Run(() => GhRunner.Run(
                "issue list --limit 40 --json number,title,state,author,updatedAt,url"));

            listIssues.BeginUpdate();
            listIssues.Items.Clear();
            _issues.Clear();

            if (!r.Success)
            {
                listIssues.EndUpdate();
                txtIssueDetail.Text = "读取 Issue 列表失败：\r\n" + (r.StdErr + "\r\n" + r.StdOut).Trim();
                return;
            }

            _issues = GhRunner.ParseArray(r.StdOut);
            foreach (var issue in _issues)
            {
                var item = new ListViewItem("#" + GhRunner.Str(issue, "number"));
                item.SubItems.Add(GhRunner.Str(issue, "title"));
                item.SubItems.Add(GhRunner.Str(issue, "state"));
                item.SubItems.Add(GhRunner.Str(issue, "updatedAt"));
                item.Tag = issue;
                if (GhRunner.Str(issue, "state") == "OPEN") item.ForeColor = Color.DarkBlue;
                else item.ForeColor = Color.FromArgb(110, 110, 110);
                listIssues.Items.Add(item);
            }
            listIssues.EndUpdate();

            if (listIssues.Items.Count > 0)
            {
                listIssues.Items[0].Selected = true;
                LoadIssueDetail();
            }
            else txtIssueDetail.Text = "（没有 Issue，或者当前仓库不是 GitHub 仓库）";
        }

        /// <summary>
        /// ListView 在隐藏页或无焦点时 SelectedItems 可能为空（但 Item.Selected 已是 true），
        /// 这里退回到第一个被标记选中的项，保证详情能显示。
        /// </summary>
        private static ListViewItem SelectedOrFirst(ListView list)
        {
            if (list.SelectedItems.Count > 0) return list.SelectedItems[0];
            foreach (ListViewItem item in list.Items)
            {
                if (item.Selected) return item;
            }
            return list.Items.Count > 0 ? list.Items[0] : null;
        }

        private async void LoadIssueDetail()
        {
            var item = SelectedOrFirst(listIssues);
            if (item == null) return;
            var issue = item.Tag as Dictionary<string, object>;
            if (issue == null) return;

            var number = GhRunner.Str(issue, "number");
            var r = await Task.Run(() => GhRunner.Run(
                "issue view " + number + " --json title,body,state,author,url,labels,comments"));

            if (!r.Success)
            {
                txtIssueDetail.Text = "读取 Issue 详情失败：\r\n" + (r.StdErr + "\r\n" + r.StdOut).Trim();
                return;
            }

            var detail = GhRunner.ParseObject(r.StdOut);
            if (detail == null) { txtIssueDetail.Text = "（无法解析 Issue 详情）"; return; }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("#" + number + "  " + GhRunner.Str(detail, "title"));
            sb.AppendLine("状态: " + GhRunner.Str(detail, "state") + "    作者: " + GhRunner.LoginOf(detail, "author"));
            var labels = GhRunner.SubList(detail, "labels");
            if (labels.Count > 0)
                sb.AppendLine("标签: " + string.Join(", ", labels.Select(l => GhRunner.Str(l, "name")).ToArray()));
            sb.AppendLine("链接: " + GhRunner.Str(detail, "url"));

            var comments = GhRunner.SubList(detail, "comments");
            sb.AppendLine("评论: " + comments.Count + " 条");

            sb.AppendLine();
            sb.AppendLine("描述:");
            sb.AppendLine(GhRunner.Str(detail, "body"));

            if (comments.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("最近评论:");
                foreach (var comment in comments.Skip(Math.Max(0, comments.Count - 5)))
                {
                    sb.AppendLine("── " + GhRunner.LoginOf(comment, "author"));
                    sb.AppendLine(GhRunner.Str(comment, "body"));
                    sb.AppendLine();
                }
            }

            txtIssueDetail.Text = sb.ToString();
        }

        private void OpenSelectedIssue()
        {
            OpenUrl(SelectedUrl(listIssues, "url"));
        }

        private void CloseIssue()
        {
            if (!GhRunner.Available || listIssues.SelectedItems.Count == 0) return;
            var issue = listIssues.SelectedItems[0].Tag as Dictionary<string, object>;
            var number = GhRunner.Str(issue, "number");

            var answer = MessageBox.Show(this, "关闭 Issue #" + number + "？", "关闭 Issue",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            var r = GhRunner.Run("issue close " + number);
            if (!r.Success)
            {
                MessageBox.Show(this, (r.StdErr + "\r\n" + r.StdOut).Trim(), "关闭失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            LoadIssues();
        }

        private void CreateIssue()
        {
            if (!GhRunner.Available) return;

            using (var dlg = new NewGitHubItemForm("新建 Issue", false, "", false))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (dlg.TitleText.Trim().Length == 0)
                {
                    MessageBox.Show(this, "Issue 标题不能为空。");
                    return;
                }

                var args = "issue create --title " + GitRunner.Quote(dlg.TitleText.Trim());
                var r = GhRunner.RunWithBodyFile(args, dlg.BodyText, "--body");
                if (!r.Success)
                {
                    MessageBox.Show(this, (r.StdErr + "\r\n" + r.StdOut).Trim(), "新建 Issue 失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(this, "Issue 已创建：\r\n" + r.StdOut.Trim(), "新建 Issue",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadIssues();
            }
        }

        // ---------- 小工具 ----------

        private static string SelectedUrl(ListView list, string key)
        {
            if (list.SelectedItems.Count == 0) return "";
            var dict = list.SelectedItems[0].Tag as Dictionary<string, object>;
            return GhRunner.Str(dict, key);
        }
    }

    /// <summary>
    /// 新建 PR / Issue 的输入窗口：标题 + 正文，PR 还会多一个目标分支和草稿选项。
    /// </summary>
    public class NewGitHubItemForm : Form
    {
        private TextBox txtTitle;
        private TextBox txtBody;
        private TextBox txtBase;
        private CheckBox chkDraft;

        public string TitleText { get { return txtTitle.Text; } }
        public string BodyText { get { return txtBody.Text; } }
        public string BaseText { get { return txtBase == null ? "" : txtBase.Text; } }
        public bool DraftChecked { get { return chkDraft != null && chkDraft.Checked; } }

        public NewGitHubItemForm(string caption, bool showBase, string defaultBase, bool showDraft)
        {
            Text = caption;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(620, 460);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Font = new Font("Microsoft YaHei UI", 9F);

            var lblTitle = new Label { Text = "标题:", Location = new Point(12, 14), AutoSize = true };
            txtTitle = new TextBox { Location = new Point(60, 12), Size = new Size(548, 24) };

            var lblBody = new Label { Text = "正文:", Location = new Point(12, 50), AutoSize = true };
            txtBody = new TextBox
            {
                Location = new Point(60, 48),
                Size = new Size(548, 330),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
            };

            Controls.AddRange(new Control[] { lblTitle, txtTitle, lblBody, txtBody });

            if (showBase)
            {
                var lblBase = new Label { Text = "目标分支:", Location = new Point(12, 392), AutoSize = true };
                txtBase = new TextBox
                {
                    Location = new Point(90, 389),
                    Size = new Size(200, 24),
                    Text = defaultBase ?? "",
                };
                Controls.Add(lblBase);
                Controls.Add(txtBase);
            }

            if (showDraft)
            {
                chkDraft = new CheckBox { Text = "创建为草稿 (draft)", Location = new Point(310, 391), Size = new Size(200, 24) };
                Controls.Add(chkDraft);
            }

            var ok = new Button { Text = "创建", Location = new Point(420, 424), Size = new Size(90, 28), DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "取消", Location = new Point(518, 424), Size = new Size(90, 28), DialogResult = DialogResult.Cancel };

            Controls.AddRange(new Control[] { ok, cancel });

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
