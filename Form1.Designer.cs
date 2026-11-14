namespace betterGitGUI
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem repoMenu;
        private System.Windows.Forms.ToolStripMenuItem openRepoMenuItem;
        private System.Windows.Forms.ToolStripMenuItem initRepoMenuItem;
        private System.Windows.Forms.ToolStripMenuItem menuSep1;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.ToolStripMenuItem branchMenu;
        private System.Windows.Forms.ToolStripMenuItem checkoutBranchMenuItem;
        private System.Windows.Forms.ToolStripMenuItem createBranchMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteBranchMenuItem;
        private System.Windows.Forms.ToolStripMenuItem remoteMenu;
        private System.Windows.Forms.ToolStripMenuItem fetchMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pullMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pushMenuItem;
        private System.Windows.Forms.ToolStripMenuItem syncMenuItem;
        private System.Windows.Forms.ToolStripMenuItem remoteUrlMenuItem;
        private System.Windows.Forms.ToolStripMenuItem githubMenu;
        private System.Windows.Forms.ToolStripMenuItem githubPanelMenuItem;
        private System.Windows.Forms.ToolStripMenuItem githubOpenRepoMenuItem;
        private System.Windows.Forms.ToolStripMenuItem mergeAbortMenuItem;
        private System.Windows.Forms.ToolStripMenuItem rebaseAbortMenuItem;
        private System.Windows.Forms.ToolStripMenuItem greenMenu;
        private System.Windows.Forms.ToolStripMenuItem splitCommitMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stashMenu;
        private System.Windows.Forms.ToolStripMenuItem stashSaveMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stashListMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpMenu;
        private System.Windows.Forms.ToolStripMenuItem aboutMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recentMenu;

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton openToolBtn;
        private System.Windows.Forms.ToolStripButton initToolBtn;
        private System.Windows.Forms.ToolStripButton refreshToolBtn;
        private System.Windows.Forms.ToolStripSeparator toolSep1;
        private System.Windows.Forms.ToolStripButton stageAllToolBtn;
        private System.Windows.Forms.ToolStripButton unstageAllToolBtn;
        private System.Windows.Forms.ToolStripButton commitToolBtn;
        private System.Windows.Forms.ToolStripButton amendToolBtn;
        private System.Windows.Forms.ToolStripSeparator toolSep2;
        private System.Windows.Forms.ToolStripButton fetchToolBtn;
        private System.Windows.Forms.ToolStripButton pullToolBtn;
        private System.Windows.Forms.ToolStripButton pushToolBtn;
        private System.Windows.Forms.ToolStripButton githubToolBtn;
        private System.Windows.Forms.ToolStripSeparator toolSep3;
        private System.Windows.Forms.ToolStripButton logToolBtn;
        private System.Windows.Forms.ToolStripButton discardToolBtn;
        private System.Windows.Forms.ToolStripButton resetHardToolBtn;
        private System.Windows.Forms.ToolStripSeparator toolSep4;
        private System.Windows.Forms.ToolStripSeparator toolSep5;
        private System.Windows.Forms.ToolStripButton stashToolBtn;
        private System.Windows.Forms.ToolStripButton undoCommitToolBtn;
        private System.Windows.Forms.ToolStripButton lastMsgToolBtn;
        private System.Windows.Forms.ToolStripButton syncToolBtn;
        private System.Windows.Forms.ToolStripLabel filterLabel;
        private System.Windows.Forms.ToolStripTextBox filterToolBox;

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel repoStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel branchStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel aheadBehindLabel;

        private System.Windows.Forms.SplitContainer splitDiff;
        private System.Windows.Forms.Label lblDiffTitle;
        private System.Windows.Forms.RichTextBox richDiff;

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.Label labelUnstaged;
        private System.Windows.Forms.ListView listUnstaged;
        private System.Windows.Forms.ColumnHeader colUnstagedStatus;
        private System.Windows.Forms.ColumnHeader colUnstagedPath;
        private System.Windows.Forms.Label labelStaged;
        private System.Windows.Forms.ListView listStaged;
        private System.Windows.Forms.ColumnHeader colStagedStatus;
        private System.Windows.Forms.ColumnHeader colStagedPath;

        private System.Windows.Forms.Panel commitPanel;
        private System.Windows.Forms.Label labelCommit;
        private System.Windows.Forms.TextBox textBoxCommit;
        private System.Windows.Forms.Button btnCommit;
        private System.Windows.Forms.Button btnAmend;
        private System.Windows.Forms.Button btnStageSelected;
        private System.Windows.Forms.Button btnUnstageSelected;

        private System.Windows.Forms.Panel outputPanel;
        private System.Windows.Forms.Label labelOutput;
        private System.Windows.Forms.TextBox textBoxOutput;

        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.repoMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.openRepoMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.initRepoMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSep1 = new System.Windows.Forms.ToolStripMenuItem();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.branchMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.checkoutBranchMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createBranchMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteBranchMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.remoteMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.fetchMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pullMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pushMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.syncMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.remoteUrlMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.githubMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.githubPanelMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.githubOpenRepoMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mergeAbortMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rebaseAbortMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.greenMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.splitCommitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stashMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.stashSaveMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stashListMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpMenu = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recentMenu = new System.Windows.Forms.ToolStripMenuItem();

            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.openToolBtn = new System.Windows.Forms.ToolStripButton();
            this.initToolBtn = new System.Windows.Forms.ToolStripButton();
            this.refreshToolBtn = new System.Windows.Forms.ToolStripButton();
            this.toolSep1 = new System.Windows.Forms.ToolStripSeparator();
            this.stageAllToolBtn = new System.Windows.Forms.ToolStripButton();
            this.unstageAllToolBtn = new System.Windows.Forms.ToolStripButton();
            this.commitToolBtn = new System.Windows.Forms.ToolStripButton();
            this.amendToolBtn = new System.Windows.Forms.ToolStripButton();
            this.toolSep2 = new System.Windows.Forms.ToolStripSeparator();
            this.fetchToolBtn = new System.Windows.Forms.ToolStripButton();
            this.pullToolBtn = new System.Windows.Forms.ToolStripButton();
            this.pushToolBtn = new System.Windows.Forms.ToolStripButton();
            this.githubToolBtn = new System.Windows.Forms.ToolStripButton();
            this.toolSep3 = new System.Windows.Forms.ToolStripSeparator();
            this.logToolBtn = new System.Windows.Forms.ToolStripButton();
            this.discardToolBtn = new System.Windows.Forms.ToolStripButton();
            this.resetHardToolBtn = new System.Windows.Forms.ToolStripButton();
            this.toolSep4 = new System.Windows.Forms.ToolStripSeparator();
            this.toolSep5 = new System.Windows.Forms.ToolStripSeparator();
            this.stashToolBtn = new System.Windows.Forms.ToolStripButton();
            this.undoCommitToolBtn = new System.Windows.Forms.ToolStripButton();
            this.lastMsgToolBtn = new System.Windows.Forms.ToolStripButton();
            this.syncToolBtn = new System.Windows.Forms.ToolStripButton();
            this.filterLabel = new System.Windows.Forms.ToolStripLabel();
            this.filterToolBox = new System.Windows.Forms.ToolStripTextBox();

            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.repoStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.branchStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.aheadBehindLabel = new System.Windows.Forms.ToolStripStatusLabel();

            this.splitDiff = new System.Windows.Forms.SplitContainer();
            this.lblDiffTitle = new System.Windows.Forms.Label();
            this.richDiff = new System.Windows.Forms.RichTextBox();

            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.labelUnstaged = new System.Windows.Forms.Label();
            this.listUnstaged = new System.Windows.Forms.ListView();
            this.colUnstagedStatus = new System.Windows.Forms.ColumnHeader();
            this.colUnstagedPath = new System.Windows.Forms.ColumnHeader();
            this.labelStaged = new System.Windows.Forms.Label();
            this.listStaged = new System.Windows.Forms.ListView();
            this.colStagedStatus = new System.Windows.Forms.ColumnHeader();
            this.colStagedPath = new System.Windows.Forms.ColumnHeader();

            this.commitPanel = new System.Windows.Forms.Panel();
            this.labelCommit = new System.Windows.Forms.Label();
            this.textBoxCommit = new System.Windows.Forms.TextBox();
            this.btnCommit = new System.Windows.Forms.Button();
            this.btnAmend = new System.Windows.Forms.Button();
            this.btnStageSelected = new System.Windows.Forms.Button();
            this.btnUnstageSelected = new System.Windows.Forms.Button();

            this.outputPanel = new System.Windows.Forms.Panel();
            this.labelOutput = new System.Windows.Forms.Label();
            this.textBoxOutput = new System.Windows.Forms.TextBox();

            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitDiff)).BeginInit();
            this.splitDiff.Panel1.SuspendLayout();
            this.splitDiff.Panel2.SuspendLayout();
            this.splitDiff.SuspendLayout();
            this.commitPanel.SuspendLayout();
            this.outputPanel.SuspendLayout();
            this.SuspendLayout();

            //
            // menuStrip1
            //
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.repoMenu,
                this.branchMenu,
                this.remoteMenu,
                this.greenMenu,
                this.stashMenu,
                this.githubMenu,
                this.helpMenu});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1000, 25);
            this.menuStrip1.TabIndex = 0;

            this.repoMenu.Text = "仓库(&R)";
            this.repoMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.openRepoMenuItem,
                this.initRepoMenuItem,
                this.menuSep1,
                this.recentMenu,
                this.exitMenuItem});
            this.openRepoMenuItem.Text = "打开仓库...";
            this.openRepoMenuItem.Click += new System.EventHandler(this.OpenRepositoryToolStripMenuItem_Click);
            this.initRepoMenuItem.Text = "初始化仓库...";
            this.initRepoMenuItem.Click += new System.EventHandler(this.InitRepositoryToolStripMenuItem_Click);
            this.menuSep1.Text = "-";
            this.recentMenu.Text = "最近打开";
            this.exitMenuItem.Text = "退出";
            this.exitMenuItem.Click += new System.EventHandler(this.ExitToolStripMenuItem_Click);

            this.branchMenu.Text = "分支(&B)";
            this.branchMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.checkoutBranchMenuItem,
                this.createBranchMenuItem,
                this.deleteBranchMenuItem,
                this.mergeAbortMenuItem,
                this.rebaseAbortMenuItem});
            this.checkoutBranchMenuItem.Text = "检出分支...";
            this.checkoutBranchMenuItem.Click += new System.EventHandler(this.CheckoutBranchToolStripMenuItem_Click);
            this.createBranchMenuItem.Text = "新建分支...";
            this.createBranchMenuItem.Click += new System.EventHandler(this.CreateBranchToolStripMenuItem_Click);
            this.deleteBranchMenuItem.Text = "删除分支...";
            this.deleteBranchMenuItem.Click += new System.EventHandler(this.DeleteBranchToolStripMenuItem_Click);
            this.mergeAbortMenuItem.Text = "中止合并 (git merge --abort)";
            this.mergeAbortMenuItem.Click += new System.EventHandler(this.MergeAbortToolStripMenuItem_Click);
            this.rebaseAbortMenuItem.Text = "中止变基 (git rebase --abort)";
            this.rebaseAbortMenuItem.Click += new System.EventHandler(this.RebaseAbortToolStripMenuItem_Click);

            this.remoteMenu.Text = "远程(&O)";
            this.remoteMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.fetchMenuItem,
                this.pullMenuItem,
                this.pushMenuItem,
                this.syncMenuItem,
                this.remoteUrlMenuItem});
            this.fetchMenuItem.Text = "获取 Fetch";
            this.fetchMenuItem.Click += new System.EventHandler(this.FetchToolStripMenuItem_Click);
            this.pullMenuItem.Text = "拉取 Pull";
            this.pullMenuItem.Click += new System.EventHandler(this.PullToolStripMenuItem_Click);
            this.pushMenuItem.Text = "推送 Push";
            this.pushMenuItem.Click += new System.EventHandler(this.PushToolStripMenuItem_Click);
            this.syncMenuItem.Text = "一键同步（fetch + pull --rebase + push）";
            this.syncMenuItem.Click += new System.EventHandler(this.SyncToolStripMenuItem_Click);
            this.remoteUrlMenuItem.Text = "修改远程地址（SSH / HTTPS）...";
            this.remoteUrlMenuItem.Click += new System.EventHandler(this.RemoteUrlToolStripMenuItem_Click);

            this.githubMenu.Text = "GitHub(&U)";
            this.githubMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.githubPanelMenuItem,
                this.githubOpenRepoMenuItem});
            this.githubPanelMenuItem.Text = "GitHub 面板（PR / Issue）...";
            this.githubPanelMenuItem.Click += new System.EventHandler(this.GitHubPanelToolStripMenuItem_Click);
            this.githubOpenRepoMenuItem.Text = "在浏览器打开仓库";
            this.githubOpenRepoMenuItem.Click += new System.EventHandler(this.GitHubOpenRepoMenuItem_Click);

            this.greenMenu.Text = "刷绿(&G)";
            this.greenMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.splitCommitMenuItem});
            this.splitCommitMenuItem.Text = "刷绿（贡献图 / 拆分提交）...";
            this.splitCommitMenuItem.Click += new System.EventHandler(this.SplitCommitToolStripMenuItem_Click);

            this.stashMenu.Text = "储藏(&S)";
            this.stashMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.stashSaveMenuItem,
                this.stashListMenuItem});
            this.stashSaveMenuItem.Text = "把当前改动存进储藏...";
            this.stashSaveMenuItem.Click += new System.EventHandler(this.StashSaveToolStripMenuItem_Click);
            this.stashListMenuItem.Text = "储藏列表 / 弹出 / 删除...";
            this.stashListMenuItem.Click += new System.EventHandler(this.StashListToolStripMenuItem_Click);

            this.helpMenu.Text = "帮助(&H)";
            this.helpMenu.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.aboutMenuItem});
            this.aboutMenuItem.Text = "关于";
            this.aboutMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItem_Click);

            //
            // toolStrip1
            //
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.openToolBtn,
                this.initToolBtn,
                this.refreshToolBtn,
                this.toolSep1,
                this.stageAllToolBtn,
                this.unstageAllToolBtn,
                this.commitToolBtn,
                this.amendToolBtn,
                this.toolSep2,
                this.fetchToolBtn,
                this.pullToolBtn,
                this.pushToolBtn,
                this.githubToolBtn,
                this.toolSep3,
                this.logToolBtn,
                this.discardToolBtn,
                this.resetHardToolBtn,
                this.toolSep4,
                this.stashToolBtn,
                this.undoCommitToolBtn,
                this.lastMsgToolBtn,
                this.syncToolBtn});
            this.toolStrip1.Location = new System.Drawing.Point(0, 25);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1000, 25);
            this.toolStrip1.TabIndex = 1;

            this.openToolBtn.Text = "打开";
            this.openToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.openToolBtn.Click += new System.EventHandler(this.OpenRepositoryToolStripMenuItem_Click);

            this.initToolBtn.Text = "初始化";
            this.initToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.initToolBtn.Click += new System.EventHandler(this.InitRepositoryToolStripMenuItem_Click);

            this.refreshToolBtn.Text = "刷新";
            this.refreshToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.refreshToolBtn.Click += new System.EventHandler(this.RefreshToolStripMenuItem_Click);

            this.stageAllToolBtn.Text = "全部暂存";
            this.stageAllToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.stageAllToolBtn.Click += new System.EventHandler(this.StageAllToolStripMenuItem_Click);

            this.unstageAllToolBtn.Text = "全部取消暂存";
            this.unstageAllToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.unstageAllToolBtn.Click += new System.EventHandler(this.UnstageAllToolStripMenuItem_Click);

            this.commitToolBtn.Text = "提交";
            this.commitToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.commitToolBtn.Click += new System.EventHandler(this.CommitToolStripMenuItem_Click);

            this.amendToolBtn.Text = "修改上次提交";
            this.amendToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.amendToolBtn.Click += new System.EventHandler(this.AmendToolStripMenuItem_Click);

            this.fetchToolBtn.Text = "Fetch";
            this.fetchToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.fetchToolBtn.Click += new System.EventHandler(this.FetchToolStripMenuItem_Click);

            this.pullToolBtn.Text = "Pull";
            this.pullToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.pullToolBtn.Click += new System.EventHandler(this.PullToolStripMenuItem_Click);

            this.pushToolBtn.Text = "Push";
            this.pushToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.pushToolBtn.Click += new System.EventHandler(this.PushToolStripMenuItem_Click);

            this.githubToolBtn.Text = "GitHub";
            this.githubToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.githubToolBtn.ToolTipText = "GitHub CLI 面板：PR、Issue、登录状态";
            this.githubToolBtn.Click += new System.EventHandler(this.GitHubPanelToolStripMenuItem_Click);

            this.logToolBtn.Text = "提交日志";
            this.logToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.logToolBtn.Click += new System.EventHandler(this.LogToolStripMenuItem_Click);

            this.discardToolBtn.Text = "丢弃改动";
            this.discardToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.discardToolBtn.Click += new System.EventHandler(this.DiscardToolStripMenuItem_Click);

            this.resetHardToolBtn.Text = "硬重置";
            this.resetHardToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.resetHardToolBtn.Click += new System.EventHandler(this.ResetHardToolStripMenuItem_Click);

            this.stashToolBtn.Text = "储藏";
            this.stashToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.stashToolBtn.ToolTipText = "保存当前改动到储藏，或管理已有储藏";
            this.stashToolBtn.Click += new System.EventHandler(this.StashListToolStripMenuItem_Click);

            this.undoCommitToolBtn.Text = "撤销上次提交";
            this.undoCommitToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.undoCommitToolBtn.ToolTipText = "把上一次提交退回到暂存区（git reset --soft HEAD~1），改动不会丢";
            this.undoCommitToolBtn.Click += new System.EventHandler(this.UndoCommitToolStripMenuItem_Click);

            this.lastMsgToolBtn.Text = "上次信息";
            this.lastMsgToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.lastMsgToolBtn.ToolTipText = "把上一次提交的说明填进提交框";
            this.lastMsgToolBtn.Click += new System.EventHandler(this.LastMessageToolStripMenuItem_Click);

            this.syncToolBtn.Text = "同步";
            this.syncToolBtn.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.syncToolBtn.ToolTipText = "fetch + pull --rebase + push 一次做完";
            this.syncToolBtn.Click += new System.EventHandler(this.SyncToolStripMenuItem_Click);

            this.filterLabel.Text = "过滤";
            this.filterToolBox.AutoSize = false;
            this.filterToolBox.Size = new System.Drawing.Size(120, 25);
            this.filterToolBox.ToolTipText = "按路径筛选左右两边的文件列表";
            this.filterToolBox.TextChanged += new System.EventHandler(this.FilterToolBox_TextChanged);

            //
            // statusStrip1
            //
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.repoStatusLabel,
                this.branchStatusLabel,
                this.aheadBehindLabel,
                this.filterLabel,
                this.filterToolBox});
            this.repoStatusLabel.Spring = true;
            this.filterLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.filterToolBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.statusStrip1.Location = new System.Drawing.Point(0, 568);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1000, 22);
            this.repoStatusLabel.Text = "未打开仓库";
            this.branchStatusLabel.Text = "";
            this.branchStatusLabel.ForeColor = System.Drawing.Color.DarkBlue;
            this.aheadBehindLabel.Text = "";
            this.aheadBehindLabel.ForeColor = System.Drawing.Color.FromArgb(87, 96, 106);

            //
            // splitDiff：左边文件列表，右边差异面板
            //
            this.splitDiff.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitDiff.Location = new System.Drawing.Point(0, 50);
            this.splitDiff.Name = "splitDiff";
            this.splitDiff.Size = new System.Drawing.Size(1000, 278);
            this.splitDiff.SplitterDistance = 620;
            this.splitDiff.TabIndex = 2;

            this.splitDiff.Panel1.Controls.Add(this.splitContainer1);

            this.splitDiff.Panel2.Controls.Add(this.lblDiffTitle);
            this.splitDiff.Panel2.Controls.Add(this.richDiff);

            this.lblDiffTitle.Text = "差异（选中左侧文件即可查看）";
            this.lblDiffTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblDiffTitle.Height = 22;
            this.lblDiffTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblDiffTitle.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);

            this.richDiff.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richDiff.ReadOnly = true;
            this.richDiff.WordWrap = false;
            this.richDiff.DetectUrls = false;
            this.richDiff.HideSelection = false;
            this.richDiff.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.richDiff.Font = new System.Drawing.Font("Consolas", 9F);
            this.richDiff.BackColor = System.Drawing.Color.White;

            //
            // splitContainer1
            //
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Size = new System.Drawing.Size(620, 278);
            this.splitContainer1.SplitterDistance = 310;
            this.splitContainer1.TabIndex = 2;

            this.splitContainer1.Panel1.Controls.Add(this.labelUnstaged);
            this.splitContainer1.Panel1.Controls.Add(this.listUnstaged);
            this.labelUnstaged.Text = "未暂存的改动（双击暂存）";
            this.labelUnstaged.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelUnstaged.Height = 20;
            this.listUnstaged.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listUnstaged.View = System.Windows.Forms.View.Details;
            this.listUnstaged.FullRowSelect = true;
            this.listUnstaged.GridLines = true;
            this.listUnstaged.MultiSelect = true;
            this.listUnstaged.HideSelection = false;
            this.listUnstaged.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colUnstagedStatus,
                this.colUnstagedPath});
            this.colUnstagedStatus.Text = "状态";
            this.colUnstagedStatus.Width = 60;
            this.colUnstagedPath.Text = "文件";
            this.colUnstagedPath.Width = 400;
            this.listUnstaged.DoubleClick += new System.EventHandler(this.ListUnstaged_DoubleClick);

            this.splitContainer1.Panel2.Controls.Add(this.labelStaged);
            this.splitContainer1.Panel2.Controls.Add(this.listStaged);
            this.labelStaged.Text = "已暂存的改动（双击取消暂存）";
            this.labelStaged.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelStaged.Height = 20;
            this.listStaged.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listStaged.View = System.Windows.Forms.View.Details;
            this.listStaged.FullRowSelect = true;
            this.listStaged.GridLines = true;
            this.listStaged.MultiSelect = true;
            this.listStaged.HideSelection = false;
            this.listStaged.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
                this.colStagedStatus,
                this.colStagedPath});
            this.colStagedStatus.Text = "状态";
            this.colStagedStatus.Width = 60;
            this.colStagedPath.Text = "文件";
            this.colStagedPath.Width = 400;
            this.listStaged.DoubleClick += new System.EventHandler(this.ListStaged_DoubleClick);

            //
            // commitPanel
            //
            this.commitPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.commitPanel.Height = 120;
            // 必须显式给宽度：否则子控件的 Anchor(Left|Right) 会按默认 200 的宽度算基准，
            // 窗口一变宽，提交框就被拉到面板外面，按钮也会跑到屏幕右侧看不见。
            this.commitPanel.Size = new System.Drawing.Size(1000, 120);
            this.commitPanel.Controls.Add(this.labelCommit);
            this.commitPanel.Controls.Add(this.textBoxCommit);
            this.commitPanel.Controls.Add(this.btnCommit);
            this.commitPanel.Controls.Add(this.btnAmend);
            this.commitPanel.Controls.Add(this.btnStageSelected);
            this.commitPanel.Controls.Add(this.btnUnstageSelected);

            this.labelCommit.Text = "提交信息:";
            this.labelCommit.Location = new System.Drawing.Point(10, 8);
            this.labelCommit.AutoSize = true;

            this.textBoxCommit.Location = new System.Drawing.Point(10, 28);
            this.textBoxCommit.Size = new System.Drawing.Size(760, 80);
            this.textBoxCommit.Multiline = true;
            this.textBoxCommit.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxCommit.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            this.btnCommit.Text = "提交";
            this.btnCommit.Location = new System.Drawing.Point(780, 28);
            this.btnCommit.Size = new System.Drawing.Size(90, 28);
            this.btnCommit.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnCommit.Click += new System.EventHandler(this.CommitToolStripMenuItem_Click);

            this.btnAmend.Text = "修改上次提交";
            this.btnAmend.Location = new System.Drawing.Point(780, 60);
            this.btnAmend.Size = new System.Drawing.Size(90, 28);
            this.btnAmend.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnAmend.Click += new System.EventHandler(this.AmendToolStripMenuItem_Click);

            this.btnStageSelected.Text = "暂存所选";
            this.btnStageSelected.Location = new System.Drawing.Point(880, 28);
            this.btnStageSelected.Size = new System.Drawing.Size(90, 28);
            this.btnStageSelected.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnStageSelected.Click += new System.EventHandler(this.StageSelectedToolStripMenuItem_Click);

            this.btnUnstageSelected.Text = "取消暂存所选";
            this.btnUnstageSelected.Location = new System.Drawing.Point(880, 60);
            this.btnUnstageSelected.Size = new System.Drawing.Size(90, 28);
            this.btnUnstageSelected.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this.btnUnstageSelected.Click += new System.EventHandler(this.UnstageSelectedToolStripMenuItem_Click);

            //
            // outputPanel
            //
            this.outputPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.outputPanel.Height = 110;
            this.outputPanel.Size = new System.Drawing.Size(1000, 110);
            this.outputPanel.Controls.Add(this.labelOutput);
            this.outputPanel.Controls.Add(this.textBoxOutput);

            this.labelOutput.Text = "输出:";
            this.labelOutput.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelOutput.Height = 20;

            this.textBoxOutput.Multiline = true;
            this.textBoxOutput.ReadOnly = true;
            this.textBoxOutput.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBoxOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxOutput.Font = new System.Drawing.Font("Consolas", 9F);

            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 590);
            this.Controls.Add(this.splitDiff);
            this.Controls.Add(this.commitPanel);
            this.Controls.Add(this.outputPanel);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Text = "betterGitGUI";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitDiff.Panel1.ResumeLayout(false);
            this.splitDiff.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitDiff)).EndInit();
            this.splitDiff.ResumeLayout(false);
            this.commitPanel.ResumeLayout(false);
            this.commitPanel.PerformLayout();
            this.outputPanel.ResumeLayout(false);
            this.outputPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}
