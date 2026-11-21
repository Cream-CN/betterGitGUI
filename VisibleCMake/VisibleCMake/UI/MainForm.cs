using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VisibleCMake.CodeGen;
using VisibleCMake.Model;
using VisibleCMake.Runtime;

namespace VisibleCMake.UI
{
	/// <summary>
	/// 主窗体：左侧目标树，右侧“目标 / 工程 / CMakeLists.txt / Makefile / 构建”五个页签。
	/// 所有编辑都在模型上完成，预览页实时从模型生成文本，构建按钮直接调用本机 cmake / make。
	/// </summary>
	public sealed class MainForm : Form
	{
		private readonly AppOptions _options;
		private ToolchainInfo _toolchain;

		private ProjectModel _model;
		private string _projectPath;
		private bool _dirty;
		private bool _loadingProject;
		private readonly string _startupArgument;

		private TreeView _targetTree;
		private TabControl _tabs;
		private TabPage _targetTab;
		private TabPage _projectTab;
		private TabPage _cmakeTab;
		private TabPage _makeTab;
		private TabPage _buildTab;

		private DetailView _detail;
		private CodePreview _cmakePreview;
		private CodePreview _makePreview;
		private BuildLogView _buildLog;

		private ToolStripStatusLabel _statusMessage;
		private ToolStripStatusLabel _statusToolchain;
		private ToolStripStatusLabel _statusTargets;

		private CancellationTokenSource _cancelSource;
		private bool _running;

		// 工程级设置
		private TextBox _projectNameBox;
		private TextBox _projectVersionBox;
		private TextBox _cmakeMinimumBox;
		private TextBox _buildDirectoryBox;
		private TextBox _runtimeDirectoryBox;
		private TextBox _archiveDirectoryBox;
		private TextBox _makefileNameBox;
		private ComboBox _cppStandardBox;
		private ComboBox _cStandardBox;
		private ComboBox _buildTypeBox;
		private CheckBox _warningsBox;
		private CheckBox _installBox;
		private CheckBox _testingBox;
		private CheckBox _compileCommandsBox;
		private ListEditor _globalIncludes;
		private ListEditor _globalDefinitions;
		private ListEditor _globalOptions;
		private ListEditor _globalLinkDirectories;
		private ListEditor _globalLinkLibraries;
		private TextBox _extraCmakeBox;
		private TextBox _extraMakeBox;

		public MainForm(string[] args)
		{
			_options = AppOptions.Load();
			_toolchain = ToolchainInfo.Detect(_options);
			_startupArgument = args != null && args.Length > 0 ? args[0] : null;

			CreateProjectControls();
			BuildWindow();

			Load += OnFormLoad;
			FormClosing += OnFormClosing;
		}

		// =================================================================
		//  界面搭建
		// =================================================================

		private void BuildWindow()
		{
			Text = "VisibleCMake";
			Font = UiKit.UiFont;
			BackColor = UiKit.Background;
			ClientSize = new Size(1180, 760);
			MinimumSize = new Size(940, 620);
			StartPosition = FormStartPosition.CenterScreen;

			MenuStrip menu = BuildMenu();
			ToolStrip toolbar = BuildToolbar();
			StatusStrip status = BuildStatusStrip();

			SplitContainer split = new SplitContainer();
			split.Dock = DockStyle.Fill;
			split.FixedPanel = FixedPanel.Panel1;
			split.SplitterWidth = 6;
			split.BackColor = UiKit.Border;
			split.Panel1.BackColor = UiKit.Surface;
			split.Panel2.BackColor = UiKit.Background;
			split.Panel1.Controls.Add(BuildSidebar());
			split.Panel2.Controls.Add(BuildTabs());

			TableLayoutPanel top = new TableLayoutPanel();
			top.Dock = DockStyle.Top;
			top.Height = 56;
			top.ColumnCount = 1;
			top.RowCount = 2;
			top.BackColor = UiKit.Surface;
			top.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
			top.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
			menu.Dock = DockStyle.Fill;
			menu.AutoSize = false;
			menu.Height = 26;
			toolbar.Dock = DockStyle.Fill;
			top.Controls.Add(menu, 0, 0);
			top.Controls.Add(toolbar, 0, 1);

			Controls.Add(split);
			Controls.Add(top);
			Controls.Add(status);

			MainMenuStrip = menu;

			// 窗口显示后再设置分隔位置，避免 SplitterDistance 小于 Panel1MinSize
			Shown += delegate
			{
				try { split.SplitterDistance = 258; }
				catch { }
			};
		}

		private MenuStrip BuildMenu()
		{
			MenuStrip menu = new MenuStrip();
			menu.Font = UiKit.UiFont;
			menu.BackColor = UiKit.Surface;

			ToolStripMenuItem file = new ToolStripMenuItem("文件(&F)");
			file.DropDownItems.Add(Item("新建工程…", OnNewProject, Keys.Control | Keys.N));
			file.DropDownItems.Add(Item("打开工程…", OnOpenProject, Keys.Control | Keys.O));
			file.DropDownItems.Add(Item("打开文件夹作为工程…", OnOpenFolder, Keys.Control | Keys.Shift | Keys.O));
			file.DropDownItems.Add(new ToolStripSeparator());
			file.DropDownItems.Add(Item("导入已有 CMakeLists.txt…", OnImportCmake, Keys.None));
			file.DropDownItems.Add(new ToolStripSeparator());
			file.DropDownItems.Add(Item("保存工程", delegate { SaveProject(false); }, Keys.Control | Keys.S));
			file.DropDownItems.Add(Item("另存为…", delegate { SaveProject(true); }, Keys.Control | Keys.Shift | Keys.S));
			file.DropDownItems.Add(new ToolStripSeparator());
			file.DropDownItems.Add(Item("退出", delegate { Close(); }, Keys.Alt | Keys.F4));

			ToolStripMenuItem generate = new ToolStripMenuItem("生成(&G)");
			generate.DropDownItems.Add(Item("生成 CMakeLists.txt + Makefile", OnGenerateBoth, Keys.Control | Keys.G));
			generate.DropDownItems.Add(new ToolStripSeparator());
			generate.DropDownItems.Add(Item("只生成 CMakeLists.txt", OnGenerateCmake, Keys.None));
			generate.DropDownItems.Add(Item("只生成 Makefile", OnGenerateMakefile, Keys.None));
			generate.DropDownItems.Add(new ToolStripSeparator());
			generate.DropDownItems.Add(Item("刷新预览", delegate { RefreshPreviews(); }, Keys.F5));

			ToolStripMenuItem build = new ToolStripMenuItem("构建(&B)");
			build.DropDownItems.Add(Item("CMake 配置（cmake -S -B）", delegate { CmakeConfigure(); }, Keys.None));
			build.DropDownItems.Add(Item("CMake 构建（cmake --build）", delegate { CmakeBuild(); }, Keys.Control | Keys.Shift | Keys.B));
			build.DropDownItems.Add(Item("GNU make 构建", delegate { MakeBuild(); }, Keys.Control | Keys.B));
			build.DropDownItems.Add(new ToolStripSeparator());
			build.DropDownItems.Add(Item("清理构建产物", delegate { CleanBuild(); }, Keys.None));
			build.DropDownItems.Add(Item("运行生成的可执行文件", delegate { RunProgram(); }, Keys.Control | Keys.F5));
			build.DropDownItems.Add(Item("打开构建输出目录", delegate { OpenOutputFolder(); }, Keys.None));
			build.DropDownItems.Add(new ToolStripSeparator());
			build.DropDownItems.Add(Item("停止当前任务", delegate { CancelCurrent(); }, Keys.None));

			ToolStripMenuItem tools = new ToolStripMenuItem("工具(&T)");
			tools.DropDownItems.Add(Item("设置…", delegate { OpenSettings(); }, Keys.Control | Keys.Oemcomma));
			tools.DropDownItems.Add(new ToolStripSeparator());
			tools.DropDownItems.Add(Item("打开工程目录", delegate { OpenProjectFolder(); }, Keys.None));
			tools.DropDownItems.Add(Item("在 Editior 中打开 CMakeLists.txt", delegate { OpenInEditiorGenerated(true); }, Keys.None));
			tools.DropDownItems.Add(Item("在 Editior 中打开 Makefile", delegate { OpenInEditiorGenerated(false); }, Keys.None));
			tools.DropDownItems.Add(Item("在 BetterGitGUI 中打开工程目录", delegate { OpenInBetterGitGUI(); }, Keys.None));

			ToolStripMenuItem help = new ToolStripMenuItem("帮助(&H)");
			help.DropDownItems.Add(Item("运行生成器自检", delegate { RunSelfTest(); }, Keys.None));
			help.DropDownItems.Add(Item("关于", delegate
			{
				using (AboutDialog dialog = new AboutDialog()) dialog.ShowDialog(this);
			}, Keys.None));

			menu.Items.Add(file);
			menu.Items.Add(generate);
			menu.Items.Add(build);
			menu.Items.Add(tools);
			menu.Items.Add(help);
			return menu;
		}

		private ToolStripMenuItem Item(string text, EventHandler handler, Keys shortcut)
		{
			ToolStripMenuItem item = new ToolStripMenuItem(text, null, handler);
			if (shortcut != Keys.None)
			{
				item.ShortcutKeys = shortcut;
				item.ShowShortcutKeys = true;
			}
			return item;
		}

		private ToolStrip BuildToolbar()
		{
			ToolStrip toolbar = new ToolStrip();
			toolbar.GripStyle = ToolStripGripStyle.Hidden;
			toolbar.BackColor = UiKit.Surface;
			toolbar.Padding = new Padding(8, 2, 8, 2);
			toolbar.Items.Add(ToolButton("新建工程", delegate { OnNewProject(null, EventArgs.Empty); }));
			toolbar.Items.Add(ToolButton("打开工程", delegate { OnOpenProject(null, EventArgs.Empty); }));
			toolbar.Items.Add(ToolButton("保存", delegate { SaveProject(false); }));
			toolbar.Items.Add(new ToolStripSeparator());
			toolbar.Items.Add(ToolButton("生成构建文件", delegate { OnGenerateBoth(null, EventArgs.Empty); }));
			toolbar.Items.Add(ToolButton("CMake 构建", delegate { CmakeBuild(); }));
			toolbar.Items.Add(ToolButton("make 构建", delegate { MakeBuild(); }));
			toolbar.Items.Add(new ToolStripSeparator());
			toolbar.Items.Add(ToolButton("设置", delegate { OpenSettings(); }));
			return toolbar;
		}

		private ToolStripButton ToolButton(string text, EventHandler handler)
		{
			ToolStripButton button = new ToolStripButton(text, null, handler);
			button.DisplayStyle = ToolStripItemDisplayStyle.Text;
			button.Font = UiKit.UiFont;
			button.Margin = new Padding(2, 0, 2, 0);
			return button;
		}

		private StatusStrip BuildStatusStrip()
		{
			StatusStrip status = new StatusStrip();
			status.BackColor = UiKit.Surface;
			status.SizingGrip = false;

			_statusMessage = new ToolStripStatusLabel("就绪");
			_statusMessage.Spring = true;
			_statusMessage.TextAlign = ContentAlignment.MiddleLeft;
			_statusMessage.Font = UiKit.UiFont;

			_statusTargets = new ToolStripStatusLabel("");
			_statusTargets.ForeColor = UiKit.Muted;
			_statusTargets.Font = UiKit.SmallFont;

			_statusToolchain = new ToolStripStatusLabel("");
			_statusToolchain.ForeColor = UiKit.Muted;
			_statusToolchain.Font = UiKit.SmallFont;

			status.Items.Add(_statusMessage);
			status.Items.Add(_statusTargets);
			status.Items.Add(new ToolStripStatusLabel("　"));
			status.Items.Add(_statusToolchain);
			return status;
		}

		private Control BuildSidebar()
		{
			Panel sidebar = new Panel();
			sidebar.Dock = DockStyle.Fill;
			sidebar.BackColor = UiKit.Surface;
			sidebar.Padding = new Padding(12, 10, 12, 10);

			TableLayoutPanel layout = new TableLayoutPanel();
			layout.Dock = DockStyle.Fill;
			layout.ColumnCount = 1;
			layout.RowCount = 3;
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));

			Panel head = new Panel();
			head.Height = 46;
			head.BackColor = UiKit.Surface;
			Label title = UiKit.Heading("构建目标");
			title.Location = new Point(0, 0);
			Label hint = UiKit.Caption("点选目标后在右侧编辑");
			hint.Location = new Point(0, 24);
			head.Controls.Add(title);
			head.Controls.Add(hint);

			_targetTree = new TreeView();
			_targetTree.Dock = DockStyle.Fill;
			_targetTree.BorderStyle = BorderStyle.None;
			_targetTree.Font = UiKit.UiFont;
			_targetTree.HideSelection = false;
			_targetTree.FullRowSelect = true;
			_targetTree.ShowLines = true;
			_targetTree.ShowRootLines = true;
			_targetTree.ItemHeight = 24;
			_targetTree.AfterSelect += OnTreeSelect;

			FlowLayoutPanel buttons = new FlowLayoutPanel();
			buttons.Dock = DockStyle.Fill;
			buttons.FlowDirection = FlowDirection.LeftToRight;
			buttons.WrapContents = false;
			buttons.BackColor = UiKit.Surface;

			Button addButton = UiKit.Secondary("新建目标", delegate { AddTarget(); });
			addButton.Width = 74;
			addButton.Height = 28;
			addButton.Margin = new Padding(0, 0, 6, 0);

			Button duplicateButton = UiKit.Secondary("复制", delegate { DuplicateTarget(); });
			duplicateButton.Width = 58;
			duplicateButton.Height = 28;
			duplicateButton.Margin = new Padding(0, 0, 6, 0);

			Button removeButton = UiKit.DangerButton("删除", delegate { RemoveTarget(); });
			removeButton.Width = 58;
			removeButton.Height = 28;
			removeButton.Margin = new Padding(0);

			buttons.Controls.Add(addButton);
			buttons.Controls.Add(duplicateButton);
			buttons.Controls.Add(removeButton);

			layout.Controls.Add(head, 0, 0);
			layout.Controls.Add(_targetTree, 0, 1);
			layout.Controls.Add(buttons, 0, 2);

			sidebar.Controls.Add(layout);
			return sidebar;
		}

		private Control BuildTabs()
		{
			_tabs = new TabControl();
			_tabs.Dock = DockStyle.Fill;
			_tabs.Font = UiKit.UiFont;
			_tabs.Padding = new Point(16, 5);

			_targetTab = NewTabPage("目标");
			_projectTab = NewTabPage("工程");
			_cmakeTab = NewTabPage("CMakeLists.txt");
			_makeTab = NewTabPage("Makefile");
			_buildTab = NewTabPage("构建");

			_detail = new DetailView();
			_detail.Dock = DockStyle.Fill;
			_detail.Changed += delegate
			{
				if (_loadingProject) return;
				MarkDirty();
				UpdateTargetNode(_detail.CurrentTarget);
				UpdateTargetSummary();
			};
			_targetTab.Controls.Add(_detail);

			_projectTab.Controls.Add(BuildProjectSettings());

			_cmakePreview = new CodePreview();
			_cmakePreview.Dock = DockStyle.Fill;
			_cmakePreview.RefreshRequested += delegate { RefreshPreviews(); };
			_cmakeTab.Controls.Add(_cmakePreview);

			_makePreview = new CodePreview();
			_makePreview.Dock = DockStyle.Fill;
			_makePreview.RefreshRequested += delegate { RefreshPreviews(); };
			_makeTab.Controls.Add(_makePreview);

			_buildTab.Controls.Add(BuildBuildTab());

			_tabs.TabPages.Add(_targetTab);
			_tabs.TabPages.Add(_projectTab);
			_tabs.TabPages.Add(_cmakeTab);
			_tabs.TabPages.Add(_makeTab);
			_tabs.TabPages.Add(_buildTab);

			_tabs.SelectedIndexChanged += delegate
			{
				if (_tabs.SelectedTab == _cmakeTab || _tabs.SelectedTab == _makeTab) RefreshPreviews();
			};

			return _tabs;
		}

		private TabPage NewTabPage(string text)
		{
			TabPage page = new TabPage(text);
			page.BackColor = UiKit.Background;
			page.Padding = new Padding(3);
			return page;
		}

		private Control BuildProjectSettings()
		{
			StackPanel stack = new StackPanel();
			stack.Dock = DockStyle.Fill;

			Panel head = new Panel();
			head.Height = 52;
			head.BackColor = UiKit.Surface;
			Label title = UiKit.Heading("工程设置");
			title.Location = new Point(0, 0);
			Label caption = UiKit.Caption("这些设置对所有目标生效；路径都相对工程目录。");
			caption.Location = new Point(0, 28);
			head.Controls.Add(title);
			head.Controls.Add(caption);

			FlowLayoutPanel checks = new FlowLayoutPanel();
			checks.Height = 28;
			checks.AutoSize = true;
			checks.WrapContents = false;
			checks.BackColor = UiKit.Surface;

			_warningsBox.Margin = new Padding(0, 2, 20, 0);
			_installBox.Margin = new Padding(0, 2, 20, 0);
			_testingBox.Margin = new Padding(0, 2, 20, 0);
			_compileCommandsBox.Margin = new Padding(0, 2, 0, 0);

			checks.Controls.Add(_warningsBox);
			checks.Controls.Add(_installBox);
			checks.Controls.Add(_testingBox);
			checks.Controls.Add(_compileCommandsBox);

			stack.AddItem(head);
			stack.AddItem(UiKit.Row2("工程名称", _projectNameBox, "版本号", _projectVersionBox, 30));
			stack.AddItem(UiKit.Row2("CMake 最低版本", _cmakeMinimumBox, "默认构建类型", _buildTypeBox, 30));
			stack.AddItem(UiKit.Row2("C++ 标准", _cppStandardBox, "C 标准", _cStandardBox, 30));
			stack.AddItem(UiKit.Row2("构建目录", _buildDirectoryBox, "可执行输出目录", _runtimeDirectoryBox, 30));
			stack.AddItem(UiKit.Row2("库输出目录", _archiveDirectoryBox, "Makefile 文件名", _makefileNameBox, 30));
			stack.AddItem(checks);
			stack.AddItem(UiKit.Section("全局编译与链接"));
			stack.AddItem(_globalIncludes);
			stack.AddItem(_globalDefinitions);
			stack.AddItem(_globalOptions);
			stack.AddItem(_globalLinkDirectories);
			stack.AddItem(_globalLinkLibraries);
			stack.AddItem(UiKit.Section("附加代码"));
			stack.AddItem(UiKit.Row("附加 CMake 代码", _extraCmakeBox, 92));
			stack.AddItem(UiKit.Row("附加 make 规则", _extraMakeBox, 92));

			return stack;
		}

		private Control BuildBuildTab()
		{
			TableLayoutPanel layout = new TableLayoutPanel();
			layout.Dock = DockStyle.Fill;
			layout.ColumnCount = 1;
			layout.RowCount = 2;
			layout.BackColor = UiKit.Background;
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));

			_buildLog = new BuildLogView();
			_buildLog.Dock = DockStyle.Fill;
			_buildLog.CancelRequested += delegate { CancelCurrent(); };

			Panel actions = new Panel();
			actions.Dock = DockStyle.Fill;
			actions.BackColor = UiKit.Surface;
			actions.Padding = new Padding(10, 8, 10, 8);

			FlowLayoutPanel bar = new FlowLayoutPanel();
			bar.Dock = DockStyle.Fill;
			bar.FlowDirection = FlowDirection.LeftToRight;
			bar.WrapContents = false;
			bar.BackColor = UiKit.Surface;

			Button generate = UiKit.Primary("生成构建文件", delegate { OnGenerateBoth(null, EventArgs.Empty); });
			generate.Height = 28;
			generate.Margin = new Padding(0, 0, 8, 0);

			Button configure = UiKit.Secondary("CMake 配置", delegate { CmakeConfigure(); });
			configure.Height = 28;
			configure.Margin = new Padding(0, 0, 8, 0);

			Button cmakeBuild = UiKit.Secondary("CMake 构建", delegate { CmakeBuild(); });
			cmakeBuild.Height = 28;
			cmakeBuild.Margin = new Padding(0, 0, 8, 0);

			Button makeBuild = UiKit.Secondary("make 构建", delegate { MakeBuild(); });
			makeBuild.Height = 28;
			makeBuild.Margin = new Padding(0, 0, 8, 0);

			Button clean = UiKit.Secondary("清理", delegate { CleanBuild(); });
			clean.Height = 28;
			clean.Margin = new Padding(0, 0, 8, 0);

			Button run = UiKit.Secondary("运行", delegate { RunProgram(); });
			run.Height = 28;
			run.Margin = new Padding(0, 0, 8, 0);

			Button folder = UiKit.Secondary("打开输出目录", delegate { OpenOutputFolder(); });
			folder.Height = 28;
			folder.Margin = new Padding(0);

			bar.Controls.Add(generate);
			bar.Controls.Add(configure);
			bar.Controls.Add(cmakeBuild);
			bar.Controls.Add(makeBuild);
			bar.Controls.Add(clean);
			bar.Controls.Add(run);
			bar.Controls.Add(folder);

			actions.Controls.Add(bar);

			layout.Controls.Add(_buildLog, 0, 0);
			layout.Controls.Add(actions, 0, 1);
			return layout;
		}

		private void CreateProjectControls()
		{
			_projectNameBox = UiKit.Input("");
			_projectVersionBox = UiKit.Input("");
			_cmakeMinimumBox = UiKit.Input("3.20");
			_buildDirectoryBox = UiKit.Input("build");
			_runtimeDirectoryBox = UiKit.Input("bin");
			_archiveDirectoryBox = UiKit.Input("lib");
			_makefileNameBox = UiKit.Input("Makefile");

			_cppStandardBox = UiKit.Combo(ProjectModel.CppStandardOptions);
			_cStandardBox = UiKit.Combo(ProjectModel.CStandardOptions);
			_buildTypeBox = UiKit.Combo(ProjectModel.BuildTypeOptions);

			_warningsBox = UiKit.Check("启用常用编译警告", true);
			_installBox = UiKit.Check("生成 install 规则", false);
			_testingBox = UiKit.Check("enable_testing()", false);
			_compileCommandsBox = UiKit.Check("生成 compile_commands.json", true);

			_globalIncludes = CreateProjectList("全局包含目录", "对所有目标生效的 -I 目录", ListEditor.ValueMode.Folders);
			_globalDefinitions = CreateProjectList("全局编译定义", "例如 WIN32_LEAN_AND_MEAN", ListEditor.ValueMode.Text);
			_globalOptions = CreateProjectList("全局编译选项", "例如 /utf-8、-fPIC", ListEditor.ValueMode.Text);
			_globalLinkDirectories = CreateProjectList("全局链接目录", "对所有目标生效的 -L 目录", ListEditor.ValueMode.Folders);
			_globalLinkLibraries = CreateProjectList("全局链接库", "例如 ws2_32、user32", ListEditor.ValueMode.Text);

			_extraCmakeBox = UiKit.MultilineBox("", 92);
			_extraMakeBox = UiKit.MultilineBox("", 92);

			WireProjectEvents();
		}

		private ListEditor CreateProjectList(string title, string hint, ListEditor.ValueMode mode)
		{
			ListEditor editor = new ListEditor();
			editor.Title = title;
			editor.Hint = hint;
			editor.Mode = mode;
			return editor;
		}

		private void WireProjectEvents()
		{
			EventHandler textChanged = OnProjectTextChanged;

			_projectNameBox.TextChanged += textChanged;
			_projectVersionBox.TextChanged += textChanged;
			_cmakeMinimumBox.TextChanged += textChanged;
			_buildDirectoryBox.TextChanged += textChanged;
			_runtimeDirectoryBox.TextChanged += textChanged;
			_archiveDirectoryBox.TextChanged += textChanged;
			_makefileNameBox.TextChanged += textChanged;
			_extraCmakeBox.TextChanged += textChanged;
			_extraMakeBox.TextChanged += textChanged;

			_cppStandardBox.SelectedIndexChanged += delegate
			{
				if (_loadingProject || _model == null) return;
				_model.CppStandard = SelectedStandard(_cppStandardBox, ProjectModel.CppStandardOptions);
				MarkDirty();
			};

			_cStandardBox.SelectedIndexChanged += delegate
			{
				if (_loadingProject || _model == null) return;
				_model.CStandard = SelectedStandard(_cStandardBox, ProjectModel.CStandardOptions);
				MarkDirty();
			};

			_buildTypeBox.SelectedIndexChanged += delegate
			{
				if (_loadingProject || _model == null) return;
				int index = _buildTypeBox.SelectedIndex;
				if (index >= 0 && index < ProjectModel.BuildTypeOptions.Length)
					_model.DefaultBuildType = ProjectModel.BuildTypeOptions[index];
				MarkDirty();
			};

			_warningsBox.CheckedChanged += delegate
			{
				if (_loadingProject || _model == null) return;
				_model.EnableWarnings = _warningsBox.Checked;
				MarkDirty();
			};

			_installBox.CheckedChanged += delegate
			{
				if (_loadingProject || _model == null) return;
				_model.InstallRules = _installBox.Checked;
				MarkDirty();
			};

			_testingBox.CheckedChanged += delegate
			{
				if (_loadingProject || _model == null) return;
				_model.EnableTesting = _testingBox.Checked;
				MarkDirty();
			};

			_compileCommandsBox.CheckedChanged += delegate
			{
				if (_loadingProject || _model == null) return;
				_model.GenerateCompileCommands = _compileCommandsBox.Checked;
				MarkDirty();
			};

			EventHandler listChanged = delegate
			{
				if (_loadingProject) return;
				MarkDirty();
			};

			_globalIncludes.Changed += listChanged;
			_globalDefinitions.Changed += listChanged;
			_globalOptions.Changed += listChanged;
			_globalLinkDirectories.Changed += listChanged;
			_globalLinkLibraries.Changed += listChanged;
		}

		private void OnProjectTextChanged(object sender, EventArgs e)
		{
			if (_loadingProject || _model == null) return;

			_model.Name = _projectNameBox.Text;
			_model.Version = _projectVersionBox.Text;
			_model.CmakeMinimumVersion = _cmakeMinimumBox.Text;
			_model.BuildDirectory = _buildDirectoryBox.Text;
			_model.RuntimeOutputDirectory = _runtimeDirectoryBox.Text;
			_model.ArchiveOutputDirectory = _archiveDirectoryBox.Text;
			_model.MakefileFileName = _makefileNameBox.Text;
			_model.ExtraCmakeCode = _extraCmakeBox.Text;
			_model.ExtraMakeCode = _extraMakeBox.Text;

			if (_targetTree.Nodes.Count > 0) _targetTree.Nodes[0].Text = "工程：" + (_model.Name ?? "");
			MarkDirty();
		}

		private static string SelectedStandard(ComboBox combo, string[] options)
		{
			int index = combo.SelectedIndex;
			if (index <= 0 || index >= options.Length) return "";
			return options[index];
		}

		// =================================================================
		//  生命周期
		// =================================================================

		private void OnFormLoad(object sender, EventArgs e)
		{
			ProbeToolchainAsync();

			if (!string.IsNullOrWhiteSpace(_startupArgument))
			{
				if (OpenArgument(_startupArgument)) return;
			}

			if (!string.IsNullOrWhiteSpace(_options.LastProjectPath) && File.Exists(_options.LastProjectPath))
			{
				try
				{
					ProjectModel model = ModelStore.Load(_options.LastProjectPath);
					ApplyModel(model, _options.LastProjectPath, false);
					Status("已打开上次的工程：" + Path.GetFileName(_options.LastProjectPath));
					return;
				}
				catch (Exception ex)
				{
					Status("上次的工程打不开：" + ex.Message);
				}
			}

			CreateUntitledModel();
		}

		private bool OpenArgument(string argument)
		{
			try
			{
				if (File.Exists(argument))
				{
					if (argument.EndsWith(ModelStore.Extension, StringComparison.OrdinalIgnoreCase))
					{
						OpenProject(argument);
						return true;
					}
					OpenDirectoryAsProject(Path.GetDirectoryName(Path.GetFullPath(argument)));
					return true;
				}
				if (Directory.Exists(argument))
				{
					OpenDirectoryAsProject(Path.GetFullPath(argument));
					return true;
				}
			}
			catch (Exception ex)
			{
				Status("命令行参数无法打开：" + ex.Message);
			}
			return false;
		}

		private void OnFormClosing(object sender, FormClosingEventArgs e)
		{
			if (_running)
			{
				if (!UiKit.Confirm(this, "还有任务在运行，停止它并退出吗？", "可见构建"))
				{
					e.Cancel = true;
					return;
				}
				CancelCurrent();
			}

			if (!_dirty) return;

			DialogResult answer = MessageBox.Show(this, "当前工程有未保存的修改，要保存吗？", "可见构建",
				MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
			if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
			if (answer == DialogResult.Yes && !SaveProject(false)) e.Cancel = true;
		}

		// =================================================================
		//  工程读写
		// =================================================================

		private void CreateUntitledModel()
		{
			ProjectModel model = new ProjectModel();
			model.Name = "MyProject";
			model.RootDirectory = DefaultRootDirectory();
			model.ExtraCmakeCode = ProjectModel.DefaultExtraCode;
			model.ExtraMakeCode = ProjectModel.DefaultExtraMakeCode;

			TargetModel target = new TargetModel();
			target.Name = "app";
			model.Targets.Add(target);

			ApplyModel(model, null, true);
			Status("新建了一个未保存的工程，选好源文件后按 Ctrl+G 生成构建文件。");
		}

		private static string DefaultRootDirectory()
		{
			string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop)) return desktop;
			return Environment.CurrentDirectory;
		}

		private void ApplyModel(ProjectModel model, string path, bool dirty)
		{
			_model = model ?? new ProjectModel();
			_model.EnsureDefaults();
			_projectPath = path;
			_dirty = false;

			BindProject();
			RefreshTree();

			TargetModel first = _model.Targets.Count > 0 ? _model.Targets[0] : null;
			_detail.LoadTarget(first, _model);
			if (first != null) SelectTargetNode(first);

			RefreshPreviews();
			_dirty = dirty;
			UpdateTitle();
			UpdateTargetSummary();
			_tabs.SelectedTab = _targetTab;

			if (!string.IsNullOrEmpty(_model.RootDirectory)) _options.LastOpenDirectory = _model.RootDirectory;
		}

		private void BindProject()
		{
			if (_model == null) return;

			_loadingProject = true;
			try
			{
				_projectNameBox.Text = _model.Name ?? "";
				_projectVersionBox.Text = _model.Version ?? "";
				_cmakeMinimumBox.Text = _model.CmakeMinimumVersion ?? "";
				_buildDirectoryBox.Text = _model.BuildDirectory ?? "";
				_runtimeDirectoryBox.Text = _model.RuntimeOutputDirectory ?? "";
				_archiveDirectoryBox.Text = _model.ArchiveOutputDirectory ?? "";
				_makefileNameBox.Text = _model.MakefileFileName ?? "";

				_cppStandardBox.SelectedIndex = OptionIndex(ProjectModel.CppStandardOptions, _model.CppStandard);
				_cStandardBox.SelectedIndex = OptionIndex(ProjectModel.CStandardOptions, _model.CStandard);
				_buildTypeBox.SelectedIndex = BuildTypeIndex(_model.DefaultBuildType);

				_warningsBox.Checked = _model.EnableWarnings;
				_installBox.Checked = _model.InstallRules;
				_testingBox.Checked = _model.EnableTesting;
				_compileCommandsBox.Checked = _model.GenerateCompileCommands;

				string root = _model.RootDirectory ?? "";
				_globalIncludes.RootDirectory = root;
				_globalDefinitions.RootDirectory = root;
				_globalOptions.RootDirectory = root;
				_globalLinkDirectories.RootDirectory = root;
				_globalLinkLibraries.RootDirectory = root;

				_globalIncludes.Items = _model.GlobalIncludeDirectories;
				_globalDefinitions.Items = _model.GlobalDefinitions;
				_globalOptions.Items = _model.GlobalCompileOptions;
				_globalLinkDirectories.Items = _model.GlobalLinkDirectories;
				_globalLinkLibraries.Items = _model.GlobalLinkLibraries;

				_extraCmakeBox.Text = _model.ExtraCmakeCode ?? "";
				_extraMakeBox.Text = _model.ExtraMakeCode ?? "";
			}
			finally
			{
				_loadingProject = false;
			}
		}

		private static int OptionIndex(string[] options, string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return 0;
			for (int i = 0; i < options.Length; i++)
			{
				if (string.Equals(options[i], value.Trim(), StringComparison.OrdinalIgnoreCase)) return i;
			}
			return 0;
		}

		private static int BuildTypeIndex(string value)
		{
			int index = OptionIndex(ProjectModel.BuildTypeOptions, value);
			return index < 0 ? 0 : index;
		}

		private void OnNewProject(object sender, EventArgs e)
		{
			if (!ConfirmSaveIfDirty()) return;

			string defaultDirectory = _model != null && !string.IsNullOrWhiteSpace(_model.RootDirectory)
				? _model.RootDirectory
				: _options.LastOpenDirectory;

			using (NewProjectDialog dialog = new NewProjectDialog(defaultDirectory))
			{
				if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Model == null) return;
				ApplyModel(dialog.Model, null, true);
				_options.LastOpenDirectory = dialog.Model.RootDirectory;
				_options.Save();
				Status("已创建工程：" + dialog.Model.Name);
			}
		}

		private void OnOpenProject(object sender, EventArgs e)
		{
			if (!ConfirmSaveIfDirty()) return;

			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Title = "打开 VisibleCMake 工程";
				dialog.Filter = "VisibleCMake 工程 (*.vcmake)|*.vcmake|所有文件|*.*";
				if (!string.IsNullOrWhiteSpace(_options.LastOpenDirectory) && Directory.Exists(_options.LastOpenDirectory))
					dialog.InitialDirectory = _options.LastOpenDirectory;
				if (dialog.ShowDialog(this) != DialogResult.OK) return;

				OpenProject(dialog.FileName);
			}
		}

		private void OpenProject(string path)
		{
			try
			{
				ProjectModel model = ModelStore.Load(path);
				ApplyModel(model, path, false);

				_options.LastProjectPath = path;
				_options.LastOpenDirectory = model.RootDirectory;
				_options.Save();

				Status("已打开工程：" + Path.GetFileName(path));
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "打开工程失败：" + ex.Message, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void OnOpenFolder(object sender, EventArgs e)
		{
			if (!ConfirmSaveIfDirty()) return;

			using (FolderBrowserDialog dialog = new FolderBrowserDialog())
			{
				dialog.Description = "选择一个源码目录，作为工程的根目录";
				if (!string.IsNullOrWhiteSpace(_options.LastOpenDirectory) && Directory.Exists(_options.LastOpenDirectory))
					dialog.SelectedPath = _options.LastOpenDirectory;
				if (dialog.ShowDialog(this) != DialogResult.OK) return;

				OpenDirectoryAsProject(dialog.SelectedPath);
			}
		}

		private void OpenDirectoryAsProject(string directory)
		{
			if (string.IsNullOrWhiteSpace(directory)) return;
			directory = Path.GetFullPath(directory);

			string projectFile = ModelStore.FindProjectFile(directory);
			if (!string.IsNullOrEmpty(projectFile))
			{
				OpenProject(projectFile);
				return;
			}

			ProjectModel model = null;
			string cmakePath = Path.Combine(directory, "CMakeLists.txt");
			if (File.Exists(cmakePath))
			{
				try
				{
					model = CmakeImporter.ImportFile(cmakePath);
					Status("已从现有 CMakeLists.txt 导入配置：" + cmakePath);
				}
				catch (Exception ex)
				{
					Status("导入 CMakeLists.txt 失败：" + ex.Message);
				}
			}

			if (model == null)
			{
				model = new ProjectModel();
				model.RootDirectory = directory;
				model.Name = SanitizeName(Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
				model.ExtraCmakeCode = ProjectModel.DefaultExtraCode;
				model.ExtraMakeCode = ProjectModel.DefaultExtraMakeCode;

				TargetModel target = new TargetModel();
				target.Name = "app";
				model.Targets.Add(target);
			}

			ApplyModel(model, null, true);
			_options.LastOpenDirectory = directory;
			_options.Save();
			Status("已把目录作为工程打开：" + directory + "（还没有 .vcmake，记得保存）");
		}

		private void OnImportCmake(object sender, EventArgs e)
		{
			if (!ConfirmSaveIfDirty()) return;

			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Title = "导入 CMakeLists.txt";
				dialog.Filter = "CMakeLists.txt|CMakeLists.txt|CMake 脚本 (*.cmake)|*.cmake|所有文件|*.*";
				if (!string.IsNullOrWhiteSpace(_options.LastOpenDirectory) && Directory.Exists(_options.LastOpenDirectory))
					dialog.InitialDirectory = _options.LastOpenDirectory;
				if (dialog.ShowDialog(this) != DialogResult.OK) return;

				try
				{
					ProjectModel model = CmakeImporter.ImportFile(dialog.FileName);
					ApplyModel(model, null, true);
					Status("已导入 " + Path.GetFileName(dialog.FileName) + "，认不出的语句保留在“附加 CMake 代码”里。");
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "导入失败：" + ex.Message, "可见构建",
						MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
		}

		private bool ConfirmSaveIfDirty()
		{
			if (!_dirty) return true;

			DialogResult answer = MessageBox.Show(this, "当前工程有未保存的修改，先保存吗？", "可见构建",
				MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
			if (answer == DialogResult.Cancel) return false;
			if (answer == DialogResult.Yes) return SaveProject(false);
			return true;
		}

		private bool SaveProject(bool saveAs)
		{
			if (_model == null) return false;
			_model.EnsureDefaults();

			string path = _projectPath;
			if (saveAs || string.IsNullOrWhiteSpace(path))
			{
				using (SaveFileDialog dialog = new SaveFileDialog())
				{
					dialog.Title = "保存工程";
					dialog.Filter = "VisibleCMake 工程 (*.vcmake)|*.vcmake|所有文件|*.*";
					dialog.FileName = ModelStore.SuggestFileName(_model);
					if (!string.IsNullOrEmpty(_model.RootDirectory) && Directory.Exists(_model.RootDirectory))
						dialog.InitialDirectory = _model.RootDirectory;
					if (dialog.ShowDialog(this) != DialogResult.OK) return false;
					path = dialog.FileName;
				}
			}

			try
			{
				ModelStore.Save(_model, path);
				_projectPath = path;
				_dirty = false;
				UpdateTitle();

				_options.LastProjectPath = path;
				_options.LastOpenDirectory = _model.RootDirectory;
				_options.Save();

				if (_options.AutoGenerateOnSave) RefreshPreviews();
				Status("已保存工程：" + Path.GetFileName(path));
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "保存失败：" + ex.Message, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}

		// =================================================================
		//  目标树
		// =================================================================

		private void RefreshTree()
		{
			if (_model == null || _targetTree == null) return;

			TargetModel selected = _detail == null ? null : _detail.CurrentTarget;

			_targetTree.BeginUpdate();
			try
			{
				_targetTree.Nodes.Clear();
				TreeNode root = new TreeNode("工程：" + (_model.Name ?? ""));
				root.Tag = null;
				_targetTree.Nodes.Add(root);

				foreach (TargetModel target in _model.Targets)
				{
					TreeNode node = new TreeNode(TargetLabel(target));
					node.Tag = target;
					root.Nodes.Add(node);
				}

				root.Expand();
				if (selected != null) SelectTargetNode(selected);
			}
			finally
			{
				_targetTree.EndUpdate();
			}

			UpdateTargetSummary();
		}

		private static string TargetLabel(TargetModel target)
		{
			string name = string.IsNullOrWhiteSpace(target.Name) ? "（未命名）" : target.Name;
			string text = name + "　·　" + target.KindText;
			if (!target.Enabled) text += "　（已停用）";
			return text;
		}

		private void SelectTargetNode(TargetModel target)
		{
			if (target == null || _targetTree.Nodes.Count == 0) return;
			foreach (TreeNode node in _targetTree.Nodes[0].Nodes)
			{
				TargetModel tag = node.Tag as TargetModel;
				if (tag != null && ReferenceEquals(tag, target))
				{
					_targetTree.SelectedNode = node;
					return;
				}
			}
		}

		private void OnTreeSelect(object sender, TreeViewEventArgs e)
		{
			TargetModel target = e.Node == null ? null : e.Node.Tag as TargetModel;
			_detail.LoadTarget(target, _model);
		}

		private void UpdateTargetNode(TargetModel target)
		{
			if (target == null || _targetTree == null || _targetTree.Nodes.Count == 0) return;
			foreach (TreeNode node in _targetTree.Nodes[0].Nodes)
			{
				TargetModel tag = node.Tag as TargetModel;
				if (tag != null && ReferenceEquals(tag, target))
				{
					string text = TargetLabel(target);
					if (node.Text != text) node.Text = text;
					return;
				}
			}
		}

		private void AddTarget()
		{
			if (_model == null) return;

			TargetModel target = new TargetModel();
			target.Name = UniqueTargetName("target");
			_model.Targets.Add(target);

			_dirty = true;
			RefreshTree();
			SelectTargetNode(target);
			_detail.LoadTarget(target, _model);
			UpdateTitle();
			Status("已新建目标：" + target.Name);
		}

		private void DuplicateTarget()
		{
			TargetModel source = _detail == null ? null : _detail.CurrentTarget;
			if (source == null)
			{
				Status("先在左侧选中一个目标。");
				return;
			}

			TargetModel copy = source.Clone();
			copy.Name = UniqueTargetName(source.Name + "_copy");
			_model.Targets.Add(copy);

			_dirty = true;
			RefreshTree();
			SelectTargetNode(copy);
			_detail.LoadTarget(copy, _model);
			UpdateTitle();
			Status("已复制目标：" + copy.Name);
		}

		private void RemoveTarget()
		{
			TargetModel target = _detail == null ? null : _detail.CurrentTarget;
			if (target == null)
			{
				Status("先在左侧选中一个目标。");
				return;
			}

			if (!UiKit.Confirm(this, "删除目标“" + target.Name + "”？", "可见构建")) return;

			int index = _model.Targets.IndexOf(target);
			_model.Targets.Remove(target);

			_dirty = true;
			RefreshTree();

			TargetModel next = _model.Targets.Count == 0
				? null
				: _model.Targets[Math.Min(Math.Max(0, index), _model.Targets.Count - 1)];
			_detail.LoadTarget(next, _model);
			if (next != null) SelectTargetNode(next);
			UpdateTitle();
			Status("已删除目标：" + target.Name);
		}

		private string UniqueTargetName(string preferred)
		{
			string baseName = string.IsNullOrWhiteSpace(preferred) ? "target" : preferred.Trim();
			string name = baseName;
			int index = 2;
			while (NameExists(name))
			{
				name = baseName + index;
				index++;
			}
			return name;
		}

		private bool NameExists(string name)
		{
			foreach (TargetModel target in _model.Targets)
			{
				if (string.Equals(target.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
			}
			return false;
		}

		private void UpdateTargetSummary()
		{
			if (_statusTargets == null) return;
			_statusTargets.Text = _model == null ? "" : _model.Targets.Count + " 个目标";
		}

		private void MarkDirty()
		{
			if (_model == null) return;
			_dirty = true;
			UpdateTitle();
		}

		private void UpdateTitle()
		{
			string fileName = string.IsNullOrWhiteSpace(_projectPath)
				? (_model == null ? "未命名" : (_model.Name ?? "未命名") + "（未保存）")
				: Path.GetFileName(_projectPath);
			Text = "VisibleCMake — " + fileName + (_dirty ? " *" : "");
		}

		// =================================================================
		//  预览与生成
		// =================================================================

		private void RefreshPreviews()
		{
			if (_model == null) return;

			try
			{
				_cmakePreview.SetText(CmakeGenerator.Generate(_model), _model.CmakeListsPath);
				_makePreview.SetText(MakefileGenerator.Generate(_model), _model.MakefilePath);
			}
			catch (Exception ex)
			{
				_cmakePreview.SetText("生成预览失败：" + ex.Message, "");
				Status("生成预览失败：" + ex.Message);
			}
		}

		private void OnGenerateBoth(object sender, EventArgs e)
		{
			GenerateBuildFiles(true, true);
		}

		private void OnGenerateCmake(object sender, EventArgs e)
		{
			GenerateBuildFiles(true, false);
		}

		private void OnGenerateMakefile(object sender, EventArgs e)
		{
			GenerateBuildFiles(false, true);
		}

		private bool GenerateBuildFiles(bool writeCmake, bool writeMakefile)
		{
			if (!EnsureModel()) return false;

			ValidationResult validation = ModelValidator.Validate(_model);
			foreach (string item in validation.All)
			{
				_buildLog.Append("配置检查：" + item);
			}

			if (validation.HasErrors)
			{
				ShowTab(_buildTab);
				MessageBox.Show(this, validation.Summary + "：\r\n\r\n" + string.Join("\r\n", validation.Errors.ToArray()),
					"可见构建", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			}

			if (!EnsureRootDirectory()) return false;

			bool written = true;
			if (writeCmake)
			{
				written &= WriteGenerated(_model.CmakeListsPath, CmakeGenerator.Generate(_model));
			}
			if (writeMakefile)
			{
				written &= WriteGenerated(_model.MakefilePath, MakefileGenerator.Generate(_model));
			}

			RefreshPreviews();
			ShowTab(_buildTab);

			if (written)
			{
				Status("已生成构建文件：" + _model.RootDirectory);
				_buildLog.Append("==> 生成完成，可以点上面的按钮直接构建。");
			}
			else
			{
				Status("生成被取消。");
			}
			return written;
		}

		private bool WriteGenerated(string path, string content)
		{
			try
			{
				if (File.Exists(path))
				{
					string existing = File.ReadAllText(path, Encoding.UTF8);
					if (!string.IsNullOrWhiteSpace(existing) && existing.IndexOf("由 VisibleCMake 生成", StringComparison.Ordinal) < 0)
					{
						DialogResult answer = MessageBox.Show(this,
							"文件不是 VisibleCMake 生成的：\r\n" + path + "\r\n\r\n覆盖前会先备份为 .bak，继续吗？",
							"可见构建", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
						if (answer != DialogResult.Yes) return false;

						File.Copy(path, path + ".bak", true);
						_buildLog.Append("已备份原文件：" + path + ".bak");
					}
				}

				string directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

				File.WriteAllText(path, content, new UTF8Encoding(false));
				_buildLog.Append("已写入：" + path + "（" + content.Length + " 字节）");
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "写入失败：" + ex.Message + "\r\n" + path, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}

		private bool EnsureModel()
		{
			if (_model != null) return true;
			MessageBox.Show(this, "还没有打开工程。", "可见构建", MessageBoxButtons.OK, MessageBoxIcon.Information);
			return false;
		}

		private bool EnsureRootDirectory()
		{
			if (string.IsNullOrWhiteSpace(_model.RootDirectory))
			{
				MessageBox.Show(this, "还没有指定工程目录。请在“工程”页签里填写源码目录。", "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				ShowTab(_projectTab);
				return false;
			}
			try
			{
				if (!Directory.Exists(_model.RootDirectory)) Directory.CreateDirectory(_model.RootDirectory);
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "工程目录不可用：" + ex.Message, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}

		// =================================================================
		//  构建
		// =================================================================

		private void CmakeConfigure()
		{
			if (!EnsureModel() || !EnsureRootDirectory() || !RequireCmake()) return;

			string arguments = "-S \"" + _model.RootDirectory + "\" -B \"" + _model.BuildPath + "\"";
			if (!string.IsNullOrWhiteSpace(_options.CmakeGenerator)) arguments += " -G \"" + _options.CmakeGenerator + "\"";
			if (!string.IsNullOrWhiteSpace(_model.DefaultBuildType)) arguments += " -DCMAKE_BUILD_TYPE=" + _model.DefaultBuildType;

			StartProcess("CMake 配置", _toolchain.CmakePath, arguments, _model.RootDirectory);
		}

		private void CmakeBuild()
		{
			if (!EnsureModel() || !RequireCmake()) return;

			string arguments = "--build \"" + _model.BuildPath + "\""
				+ " --config " + (string.IsNullOrWhiteSpace(_model.DefaultBuildType) ? "Debug" : _model.DefaultBuildType)
				+ " --parallel " + Math.Max(1, _options.Jobs);

			StartProcess("CMake 构建", _toolchain.CmakePath, arguments, _model.RootDirectory);
		}

		private void MakeBuild()
		{
			if (!EnsureModel() || !EnsureRootDirectory() || !RequireMake()) return;

			string arguments = "-C \"" + _model.RootDirectory + "\" -f \"" + MakefileName() + "\" -j" + Math.Max(1, _options.Jobs);
			StartProcess("GNU make 构建", _toolchain.MakePath, arguments, _model.RootDirectory);
		}

		private void CleanBuild()
		{
			if (!EnsureModel()) return;

			string buildDirectory = _model.BuildPath;
			if (!Directory.Exists(buildDirectory))
			{
				MessageBox.Show(this, "构建目录还不存在：\r\n" + buildDirectory, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			if (_toolchain.HasCmake)
			{
				string arguments = "--build \"" + buildDirectory + "\" --target clean --config "
					+ (string.IsNullOrWhiteSpace(_model.DefaultBuildType) ? "Debug" : _model.DefaultBuildType);
				StartProcess("CMake 清理", _toolchain.CmakePath, arguments, _model.RootDirectory);
				return;
			}

			if (_toolchain.HasMake)
			{
				string arguments = "-C \"" + _model.RootDirectory + "\" -f \"" + MakefileName() + "\" clean";
				StartProcess("make 清理", _toolchain.MakePath, arguments, _model.RootDirectory);
				return;
			}

			MessageBox.Show(this, "没有找到 cmake 或 make，无法清理。可以在设置里指定它们的路径。",
				"可见构建", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void RunProgram()
		{
			if (!EnsureModel()) return;

			TargetModel target = _model.PrimaryExecutable;
			if (target == null)
			{
				MessageBox.Show(this, "工程里没有启用的可执行文件目标。", "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			string executable = null;
			foreach (string candidate in ExecutableCandidates(target))
			{
				if (File.Exists(candidate)) { executable = candidate; break; }
			}

			if (executable == null)
			{
				MessageBox.Show(this, "还没有找到构建产物，先构建一次试试：\r\n" + _model.BuildPath,
					"可见构建", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			try
			{
				ProcessStartInfo info = new ProcessStartInfo(executable);
				info.UseShellExecute = true;
				info.WorkingDirectory = Path.GetDirectoryName(executable);
				Process.Start(info);
				Status("已启动：" + executable);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "启动失败：" + ex.Message, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private IEnumerable<string> ExecutableCandidates(TargetModel target)
		{
			string name = target.EffectiveOutputName;
			string build = _model.BuildPath;
			string runtime = string.IsNullOrWhiteSpace(_model.RuntimeOutputDirectory) ? "bin" : _model.RuntimeOutputDirectory.Trim();

			List<string> candidates = new List<string>();
			candidates.Add(Path.Combine(build, runtime, name + ".exe"));
			candidates.Add(Path.Combine(build, runtime, name));
			candidates.Add(Path.Combine(build, name + ".exe"));
			candidates.Add(Path.Combine(build, name));
			candidates.Add(Path.Combine(build, runtime, "Debug", name + ".exe"));
			candidates.Add(Path.Combine(build, runtime, "Release", name + ".exe"));
			candidates.Add(Path.Combine(build, "Debug", name + ".exe"));
			candidates.Add(Path.Combine(build, "Release", name + ".exe"));
			candidates.Add(Path.Combine(_model.RootDirectory ?? "", runtime, name + ".exe"));
			return candidates;
		}

		private void OpenOutputFolder()
		{
			if (!EnsureModel()) return;
			EditiorBridge.RevealInExplorer(this, _model.BuildPath);
		}

		private bool RequireCmake()
		{
			if (_toolchain.HasCmake) return true;
			MessageBox.Show(this, "没有找到 cmake.exe。\r\n可以在“工具 → 设置”里指定路径，或者改用 GNU make 构建。",
				"可见构建", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return false;
		}

		private bool RequireMake()
		{
			if (_toolchain.HasMake) return true;
			MessageBox.Show(this, "没有找到 GNU make（make.exe / mingw32-make.exe）。\r\n可以在“工具 → 设置”里指定路径，或者用 CMake 构建。",
				"可见构建", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return false;
		}

		private string MakefileName()
		{
			return string.IsNullOrWhiteSpace(_model.MakefileFileName) ? "Makefile" : _model.MakefileFileName.Trim();
		}

		private void StartProcess(string title, string executable, string arguments, string workingDirectory)
		{
			if (_running)
			{
				Status("已有任务在运行，等它结束或先点“停止”。");
				return;
			}
			if (string.IsNullOrWhiteSpace(executable))
			{
				Status("没有可用的可执行文件。");
				return;
			}

			_running = true;
			_cancelSource = new CancellationTokenSource();
			_buildLog.SetRunning(true);
			ShowTab(_buildTab);
			_buildLog.Banner("==> " + title);
			_buildLog.Append("$ " + executable + " " + arguments);
			Status(title + " 运行中…");

			CancellationToken token = _cancelSource.Token;

			Thread worker = new Thread(new ThreadStart(delegate
			{
				ProcessResult runResult = null;
				try
				{
					runResult = ProcessRunner.Run(executable, arguments, workingDirectory,
						delegate(string line) { Post(delegate { _buildLog.Append(line); }); }, token);
				}
				catch (Exception ex)
				{
					Post(delegate { _buildLog.Append("启动失败：" + ex.Message); });
				}

				ProcessResult result = runResult;
				Post(delegate
				{
					if (result == null)
					{
						_buildLog.Append("==> 没有结果");
					}
					else if (result.Canceled)
					{
						_buildLog.Append("==> 已停止");
					}
					else if (result.TimedOut)
					{
						_buildLog.Append("==> 超时");
					}
					else
					{
						_buildLog.Append("==> 完成，退出码 " + result.ExitCode + (result.Success ? "（成功）" : "（失败）"));
					}

					if (result != null && !result.Success && !string.IsNullOrEmpty(result.Error))
						_buildLog.Append(result.Error);

					_running = false;
					_buildLog.SetRunning(false);
					Status(result != null && result.Success ? "完成：" + title : "结束：" + title);
				});
			}));

			worker.IsBackground = true;
			worker.Start();
		}

		private void CancelCurrent()
		{
			if (_cancelSource != null)
			{
				try { _cancelSource.Cancel(); }
				catch { }
			}
			Status("正在停止…");
		}

		private void Post(Action action)
		{
			if (action == null) return;
			try
			{
				if (IsDisposed) return;
				if (InvokeRequired) BeginInvoke(action);
				else action();
			}
			catch { }
		}

		// =================================================================
		//  工具与帮助
		// =================================================================

		private void OpenSettings()
		{
			using (SettingsDialog dialog = new SettingsDialog(_options))
			{
				if (dialog.ShowDialog(this) != DialogResult.OK) return;

				_toolchain = ToolchainInfo.Detect(_options);
				ProbeToolchainAsync();
				Status("设置已保存。");
			}
		}

		private void ProbeToolchainAsync()
		{
			if (_statusToolchain != null) _statusToolchain.Text = _toolchain.Summary();

			Thread worker = new Thread(new ThreadStart(delegate
			{
				_toolchain.ProbeVersions();
				Post(delegate
				{
					if (_statusToolchain != null) _statusToolchain.Text = _toolchain.Summary();
					_buildLog.Append("工具链：" + _toolchain.Details().Replace("\r\n", "　|　"));
				});
			}));
			worker.IsBackground = true;
			worker.Start();
		}

		private void OpenProjectFolder()
		{
			if (!EnsureModel()) return;
			EditiorBridge.RevealInExplorer(this, _model.RootDirectory);
		}

		private void OpenInEditiorGenerated(bool cmake)
		{
			if (!EnsureModel()) return;

			string path = cmake ? _model.CmakeListsPath : _model.MakefilePath;
			if (!File.Exists(path))
			{
				if (!GenerateBuildFiles(cmake, !cmake)) return;
			}
			EditiorBridge.OpenInEditior(this, path);
		}

		private void OpenInBetterGitGUI()
		{
			if (!EnsureModel()) return;
			EditiorBridge.OpenInBetterGitGUI(this, _model.RootDirectory);
		}

		private void RunSelfTest()
		{
			if (_running)
			{
				Status("已有任务在运行。");
				return;
			}

			_running = true;
			_buildLog.SetRunning(false);
			ShowTab(_buildTab);
			_buildLog.Banner("==> 生成器自检");
			Status("自检运行中…");

			Thread worker = new Thread(new ThreadStart(delegate
			{
				string logPath = Path.Combine(Path.GetTempPath(), "visiblecmake-selftest.log");
				bool passed = false;
				string content = "";
				try
				{
					passed = SelfTest.Run(logPath);
					if (File.Exists(logPath)) content = File.ReadAllText(logPath, Encoding.UTF8);
				}
				catch (Exception ex)
				{
					content = "自检异常：" + ex;
				}

				string finalContent = content;
				bool finalPassed = passed;

				Post(delegate
				{
					if (!string.IsNullOrEmpty(finalContent))
					{
						string[] lines = finalContent.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
						foreach (string line in lines) _buildLog.Append(line);
					}
					_buildLog.Append(finalPassed ? "==> 自检通过" : "==> 自检未通过");
					Status(finalPassed ? "自检通过" : "自检未通过");
					_running = false;
				});
			}));
			worker.IsBackground = true;
			worker.Start();
		}

		private void ShowTab(TabPage page)
		{
			if (page != null && _tabs != null) _tabs.SelectedTab = page;
		}

		private void Status(string message)
		{
			if (_statusMessage != null) _statusMessage.Text = message ?? "";
		}

		private static string SanitizeName(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return "MyProject";
			StringBuilder sb = new StringBuilder();
			foreach (char c in name.Trim())
			{
				if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') sb.Append(c);
			}
			return sb.Length == 0 ? "MyProject" : sb.ToString();
		}
	}
}
