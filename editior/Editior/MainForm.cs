using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>
	/// 主窗体：菜单、工具栏、资源管理器、多标签编辑区、查找替换和状态栏。
	/// 全部使用 WinForms 自带控件，编辑器为 RichTextBox。
	/// </summary>
	public class MainForm : Form
	{
		private const int EM_REDO = 0x0454;

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		private readonly EditorSettings _settings;

		private MenuStrip _menu;
		private ToolStrip _toolbar;
		private StatusStrip _status;
		private ToolStripStatusLabel _pathLabel;
		private ToolStripStatusLabel _positionLabel;
		private ToolStripStatusLabel _encodingLabel;
		private ToolStripStatusLabel _eolLabel;
		private ToolStripStatusLabel _languageLabel;
		private ToolStripStatusLabel _zoomLabel;
		private ToolStripStatusLabel _memoryLabel;
		private Timer _statsTimer;
		private ToolStripStatusLabel _infoLabel;

		private SplitContainer _split;
		private TreeView _tree;
		private TabControl _tabs;
		private Panel _editorHost;
		private MinimapControl _minimap;
		private ContextMenuStrip _tabMenu;

		private Panel _findPanel;
		private TextBox _findBox;
		private TextBox _replaceBox;
		private CheckBox _caseBox;
		private CheckBox _wordBox;
		private Button _closeFindButton;

		private ToolStripMenuItem _recentMenu;
		private ToolStripMenuItem _explorerMenu;
		private ToolStripMenuItem _wrapMenu;
		private ToolStripMenuItem _statusBarMenu;

		private Timer _highlightTimer;
		private Document _highlightDoc;
		private bool _suppressDirty;
		private bool _suppressTabEvent;
		private bool _highlighting;
		private bool _loaded;
		private int _untitledCount;
		private string _folderPath;
		private Dictionary<string, string> _gitStatus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public MainForm(string[] args)
		{
			_settings = EditorSettings.Load();

			SuspendLayout();
			Text = "Editior";
			Font = new Font("Microsoft YaHei UI", 9f);
			ClientSize = new Size(1100, 720);
			MinimumSize = new Size(800, 500);
			StartPosition = FormStartPosition.CenterScreen;

			BuildEditorArea();
			BuildFindPanel();
			BuildMenu();
			BuildToolbar();
			BuildStatusBar();

			Controls.Add(_split);
			Controls.Add(_toolbar);
			Controls.Add(_status);
			Controls.Add(_menu);

			_highlightTimer = new Timer();
			_highlightTimer.Interval = 220;
			_highlightTimer.Tick += OnHighlightTimer;

			_statsTimer = new Timer();
			_statsTimer.Interval = 2000;
			_statsTimer.Tick += delegate { UpdateMemoryStatus(); };
			_statsTimer.Start();

			AllowDrop = true;
			DragEnter += OnDragEnter;
			DragDrop += OnDragDrop;
			FormClosing += OnFormClosing;
			Load += OnFormLoad;
			Activated += OnFormActivated;

			ResumeLayout();
		}

		// ---------------- 启动与关闭 ----------------

		private void OnFormLoad(object sender, EventArgs e)
		{
			try
			{
				_split.SplitterDistance = 240;
			}
			catch { }

			RebuildRecentMenu();
			_wrapMenu.Checked = _settings.WordWrap;
			ApplySettings();

			if (_settings.LastFolder.Length > 0 && Directory.Exists(_settings.LastFolder))
			{
				LoadFolder(_settings.LastFolder);
			}

			string[] args = Environment.GetCommandLineArgs();
			bool opened = false;
			for (int i = 1; i < args.Length; i++)
			{
				string p = args[i];
				if (SolutionParser.IsSolutionFile(p) && File.Exists(p))
				{
					OpenSolution(p);
					opened = true;
				}
				else if (File.Exists(p))
				{
					if (OpenFile(p) != null) opened = true;
				}
				else if (Directory.Exists(p))
				{
					LoadFolder(p);
					opened = true;
				}
			}

			if (!opened && _settings.RestoreSession)
			{
				foreach (string path in _settings.SessionFiles)
				{
					if (path.Length > 0 && File.Exists(path)) OpenFile(path);
				}
			}

			if (_tabs.TabPages.Count == 0) NewDocument();
			UpdateTitle();
			UpdateStatusBar();
			_loaded = true;
		}

		private void OnFormActivated(object sender, EventArgs e)
		{
			if (!_loaded) return;
			CheckExternalChanges();
		}

		private void CheckExternalChanges()
		{
			foreach (TabPage page in _tabs.TabPages)
			{
				Document doc = page.Tag as Document;
				if (doc == null || doc.FilePath == null) continue;
				if (!File.Exists(doc.FilePath)) continue;

				DateTime disk;
				try { disk = File.GetLastWriteTimeUtc(doc.FilePath); }
				catch { continue; }
				if (disk == doc.LastWriteTime) continue;

				if (doc.Dirty)
				{
					DialogResult r = MessageBox.Show(this,
						"“" + doc.DisplayName + "” 在磁盘上已被修改，是否重新加载？\n当前未保存的修改会丢失。",
						"Editior", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
					if (r != DialogResult.Yes)
					{
						doc.LastWriteTime = disk;
						continue;
					}
				}
				ReloadDocument(doc);
				_infoLabel.Text = "已重新加载 " + doc.DisplayName;
			}
		}

		private void ReloadDocument(Document doc)
		{
			if (doc == null || doc.FilePath == null) return;
			string text;
			Encoding encoding;
			bool bom;
			string lineEnding;
			try { TextFile.Load(doc.FilePath, out text, out encoding, out bom, out lineEnding); }
			catch { return; }

			int caret = doc.Box.SelectionStart;
			_suppressDirty = true;
			doc.Box.Text = text;
			_suppressDirty = false;

			doc.Encoding = encoding;
			doc.EmitBom = bom;
			doc.LineEnding = lineEnding;
			doc.Dirty = false;
			try { doc.LastWriteTime = File.GetLastWriteTimeUtc(doc.FilePath); }
			catch { }
			doc.RefreshTitle();
			UpdateTitle();
			UpdateStatusBar();
			doc.Box.Select(Math.Min(caret, doc.Box.TextLength), 0);
			Highlight(doc);
			_minimap.Attach(doc.Box);
		}

		private void OnFormClosing(object sender, FormClosingEventArgs e)
		{
			foreach (TabPage page in _tabs.TabPages)
			{
				Document doc = page.Tag as Document;
				if (doc == null || !doc.Dirty) continue;

				DialogResult r = MessageBox.Show(this,
					"“" + doc.DisplayName + "” 尚未保存，是否保存？",
					"Editior", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
				if (r == DialogResult.Cancel)
				{
					e.Cancel = true;
					return;
				}
				if (r == DialogResult.Yes && !SaveDocument(doc, false))
				{
					e.Cancel = true;
					return;
				}
			}

			_settings.LastFolder = _folderPath ?? _settings.LastFolder;
			_settings.SessionFiles.Clear();
			foreach (TabPage page in _tabs.TabPages)
			{
				Document doc = page.Tag as Document;
				if (doc != null && doc.FilePath != null) _settings.SessionFiles.Add(doc.FilePath);
			}
			_settings.Save();
		}

		// ---------------- 界面构建 ----------------

		private void BuildEditorArea()
		{
			_split = new SplitContainer();
			_split.Dock = DockStyle.Fill;
			_split.SplitterWidth = 4;
			_split.FixedPanel = FixedPanel.Panel1;

			_tree = new TreeView();
			_tree.Dock = DockStyle.Fill;
			_tree.HideSelection = false;
			_tree.ShowNodeToolTips = true;
			_tree.BeforeExpand += OnTreeBeforeExpand;
			_tree.NodeMouseDoubleClick += OnTreeDoubleClick;
			_tree.KeyDown += OnTreeKeyDown;
			_tree.ContextMenuStrip = BuildTreeMenu();

			_editorHost = new Panel();
			_editorHost.Dock = DockStyle.Fill;

			_minimap = new MinimapControl();
			_minimap.Dock = DockStyle.Right;
			_minimap.Width = _settings.MinimapWidth;
			_minimap.Visible = _settings.ShowMinimap;

			_tabs = new TabControl();
			_tabs.Dock = DockStyle.Fill;
			_tabs.SelectedIndexChanged += OnTabChanged;
			_tabs.MouseClick += OnTabsMouseClick;
			_tabMenu = BuildTabMenu();
			_tabs.ContextMenuStrip = _tabMenu;

			_editorHost.Controls.Add(_tabs);
			_editorHost.Controls.Add(_minimap);
			_tabs.SendToBack();

			_split.Panel1.Controls.Add(_tree);
			_split.Panel2.Controls.Add(_editorHost);
		}

		private ContextMenuStrip BuildTabMenu()
		{
			ContextMenuStrip menu = new ContextMenuStrip();
			menu.Items.Add(Item("关闭", delegate { CloseActive(); }, Keys.None));
			menu.Items.Add(Item("关闭其他", delegate { CloseOthers(); }, Keys.None));
			menu.Items.Add(Item("全部关闭", delegate { CloseAll(); }, Keys.None));
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add(Item("在资源管理器中显示", delegate
			{
				Document doc = ActiveDoc;
				if (doc != null && doc.FilePath != null)
				{
					try { Process.Start("explorer.exe", "/select,\"" + doc.FilePath + "\""); }
					catch { }
				}
			}, Keys.None));
			menu.Items.Add(Item("复制文件路径", delegate
			{
				Document doc = ActiveDoc;
				if (doc != null && doc.FilePath != null) Clipboard.SetText(doc.FilePath);
			}, Keys.None));
			return menu;
		}

		private void BuildFindPanel()
		{
			_findPanel = new Panel();
			_findPanel.Dock = DockStyle.Bottom;
			_findPanel.Height = 66;
			_findPanel.Visible = false;
			_findPanel.Resize += delegate { ArrangeFindPanel(); };

			Label findLabel = new Label();
			findLabel.Text = "查找：";
			findLabel.SetBounds(8, 9, 48, 20);
			_findPanel.Controls.Add(findLabel);

			_findBox = new TextBox();
			_findBox.SetBounds(58, 6, 300, 24);
			_findBox.KeyDown += OnFindBoxKeyDown;
			_findPanel.Controls.Add(_findBox);

			Button prevButton = new Button();
			prevButton.Text = "上一个";
			prevButton.SetBounds(370, 5, 76, 26);
			prevButton.Click += delegate { FindNext(false); };
			_findPanel.Controls.Add(prevButton);

			Button nextButton = new Button();
			nextButton.Text = "下一个";
			nextButton.SetBounds(452, 5, 76, 26);
			nextButton.Click += delegate { FindNext(true); };
			_findPanel.Controls.Add(nextButton);

			_closeFindButton = new Button();
			_closeFindButton.Text = "×";
			_closeFindButton.SetBounds(536, 5, 30, 26);
			_closeFindButton.Click += delegate { HideFindPanel(); };
			_findPanel.Controls.Add(_closeFindButton);

			Label replaceLabel = new Label();
			replaceLabel.Text = "替换：";
			replaceLabel.SetBounds(8, 39, 48, 20);
			_findPanel.Controls.Add(replaceLabel);

			_replaceBox = new TextBox();
			_replaceBox.SetBounds(58, 36, 300, 24);
			_replaceBox.KeyDown += OnFindBoxKeyDown;
			_findPanel.Controls.Add(_replaceBox);

			Button replaceButton = new Button();
			replaceButton.Text = "替换";
			replaceButton.SetBounds(370, 35, 76, 26);
			replaceButton.Click += delegate { ReplaceCurrent(); };
			_findPanel.Controls.Add(replaceButton);

			Button replaceAllButton = new Button();
			replaceAllButton.Text = "全部替换";
			replaceAllButton.SetBounds(452, 35, 90, 26);
			replaceAllButton.Click += delegate { ReplaceAll(); };
			_findPanel.Controls.Add(replaceAllButton);

			_caseBox = new CheckBox();
			_caseBox.Text = "区分大小写";
			_caseBox.AutoSize = true;
			_caseBox.SetBounds(560, 38, 100, 22);
			_findPanel.Controls.Add(_caseBox);

			_wordBox = new CheckBox();
			_wordBox.Text = "全词匹配";
			_wordBox.AutoSize = true;
			_wordBox.SetBounds(560, 10, 100, 22);
			_findPanel.Controls.Add(_wordBox);

			_split.Panel2.Controls.Add(_findPanel);
			_editorHost.SendToBack();
		}

		private void ArrangeFindPanel()
		{
			if (_findPanel == null || _findBox == null || _findPanel.Width <= 0) return;
			int right = _findPanel.ClientSize.Width - 8;

			_closeFindButton.SetBounds(right - 30, 5, 30, 26);
			Button next = FindButtonByText("下一个");
			Button prev = FindButtonByText("上一个");
			if (next != null) next.SetBounds(right - 34 - 76, 5, 76, 26);
			if (prev != null) prev.SetBounds(right - 34 - 76 - 84, 5, 76, 26);

			int boxRight = right - 34 - 76 - 84 - 76 - 8;
			_findBox.SetBounds(58, 6, Math.Max(80, boxRight - 58), 24);
			_replaceBox.SetBounds(58, 36, Math.Max(80, boxRight - 58), 24);

			Button replace = FindButtonByText("替换");
			Button replaceAll = FindButtonByText("全部替换");
			if (replaceAll != null) replaceAll.SetBounds(right - 102, 35, 90, 26);
			if (replace != null) replace.SetBounds(right - 102 - 84, 35, 76, 26);

			_caseBox.SetBounds(right - 102 - 84 - 76 - 104, 38, 100, 22);
			_wordBox.SetBounds(right - 102 - 84 - 76 - 104, 10, 100, 22);
		}

		private Button FindButtonByText(string text)
		{
			foreach (Control c in _findPanel.Controls)
			{
				if (c is Button && c.Text == text) return (Button)c;
			}
			return null;
		}

		private void BuildMenu()
		{
			_menu = new MenuStrip();

			ToolStripMenuItem file = new ToolStripMenuItem("文件(&F)");
			file.DropDownItems.Add(Item("新建", delegate { NewDocument(); }, Keys.Control | Keys.N));
			file.DropDownItems.Add(Item("打开文件…", delegate { PromptOpenFiles(); }, Keys.Control | Keys.O));
			file.DropDownItems.Add(Item("打开文件夹…", delegate { PromptOpenFolder(); }, Keys.Control | Keys.Shift | Keys.O));
			file.DropDownItems.Add(Item("打开解决方案…", delegate { PromptOpenSolution(); }, Keys.None));
			file.DropDownItems.Add(new ToolStripSeparator());
			file.DropDownItems.Add(Item("保存", delegate { SaveActive(false); }, Keys.Control | Keys.S));
			file.DropDownItems.Add(Item("另存为…", delegate { SaveActive(true); }, Keys.Control | Keys.Shift | Keys.S));
			file.DropDownItems.Add(Item("全部保存", delegate { SaveAll(); }, Keys.None));
			file.DropDownItems.Add(Item("关闭标签", delegate { CloseActive(); }, Keys.Control | Keys.W));
			file.DropDownItems.Add(new ToolStripSeparator());
			file.DropDownItems.Add(BuildEncodingMenu());
			file.DropDownItems.Add(BuildLineEndingMenu());
			file.DropDownItems.Add(Item("文件格式（编码 / 换行符）…", delegate { ShowFileFormat(ActiveDoc); }, Keys.Control | Keys.Shift | Keys.E));
			file.DropDownItems.Add(new ToolStripSeparator());
			_recentMenu = new ToolStripMenuItem("最近打开");
			file.DropDownItems.Add(_recentMenu);
			file.DropDownItems.Add(new ToolStripSeparator());
			file.DropDownItems.Add(Item("退出", delegate { Close(); }, Keys.Alt | Keys.F4));

			ToolStripMenuItem edit = new ToolStripMenuItem("编辑(&E)");
			edit.DropDownItems.Add(Item("撤销", delegate { WithEditor(delegate(RichTextBox b) { b.Undo(); }); }, Keys.Control | Keys.Z));
			edit.DropDownItems.Add(Item("重做", delegate { Redo(); }, Keys.Control | Keys.Y));
			edit.DropDownItems.Add(new ToolStripSeparator());
			edit.DropDownItems.Add(Item("剪切", delegate { WithEditor(delegate(RichTextBox b) { b.Cut(); }); }, Keys.Control | Keys.X));
			edit.DropDownItems.Add(Item("复制", delegate { WithEditor(delegate(RichTextBox b) { b.Copy(); }); }, Keys.Control | Keys.C));
			edit.DropDownItems.Add(Item("粘贴", delegate { WithEditor(delegate(RichTextBox b) { b.Paste(); }); }, Keys.Control | Keys.V));
			edit.DropDownItems.Add(Item("全选", delegate { WithEditor(delegate(RichTextBox b) { b.SelectAll(); }); }, Keys.Control | Keys.A));
			edit.DropDownItems.Add(new ToolStripSeparator());
			edit.DropDownItems.Add(Item("查找", delegate { ShowFindPanel(false); }, Keys.Control | Keys.F));
			edit.DropDownItems.Add(Item("替换", delegate { ShowFindPanel(true); }, Keys.Control | Keys.H));
			edit.DropDownItems.Add(Item("转到行…", delegate { PromptGoToLine(); }, Keys.Control | Keys.G));
			edit.DropDownItems.Add(new ToolStripSeparator());
			edit.DropDownItems.Add(Item("放大字号", delegate { ChangeFontSize(1f); }, Keys.Control | Keys.Add));
			edit.DropDownItems.Add(Item("缩小字号", delegate { ChangeFontSize(-1f); }, Keys.Control | Keys.Subtract));

			ToolStripMenuItem view = new ToolStripMenuItem("视图(&V)");
			_explorerMenu = new ToolStripMenuItem("资源管理器");
			_explorerMenu.Checked = true;
			_explorerMenu.CheckOnClick = true;
			_explorerMenu.ShortcutKeys = Keys.Control | Keys.B;
			_explorerMenu.Click += delegate { ToggleExplorer(); };
			view.DropDownItems.Add(_explorerMenu);

			_wrapMenu = new ToolStripMenuItem("自动换行");
			_wrapMenu.CheckOnClick = true;
			_wrapMenu.Click += delegate { ToggleWordWrap(); };
			view.DropDownItems.Add(_wrapMenu);

			_statusBarMenu = new ToolStripMenuItem("状态栏");
			_statusBarMenu.Checked = true;
			_statusBarMenu.CheckOnClick = true;
			_statusBarMenu.Click += delegate { _status.Visible = _statusBarMenu.Checked; };
			view.DropDownItems.Add(_statusBarMenu);

			ToolStripMenuItem tools = new ToolStripMenuItem("工具(&T)");
			tools.DropDownItems.Add(Item("GUID 生成器…", delegate { ShowGuidGenerator(); }, Keys.Control | Keys.Shift | Keys.G));
			tools.DropDownItems.Add(Item("数据转代码（位图 / 汇编 / 字节）…", delegate { ShowDataCode(); }, Keys.Control | Keys.Shift | Keys.D));
			tools.DropDownItems.Add(Item("在 BetterGitGUI 中打开当前仓库", delegate { OpenInBetterGitGUI(); }, Keys.None));
			tools.DropDownItems.Add(new ToolStripSeparator());
			tools.DropDownItems.Add(Item("可视化 CMake / Make（VisibleCMake）…", delegate { ShowVisibleCMake(); }, Keys.Control | Keys.Shift | Keys.M));
			tools.DropDownItems.Add(new ToolStripSeparator());
			tools.DropDownItems.Add(Item("设置…", delegate { PromptSettings(); }, Keys.Control | Keys.Oemcomma));

			ToolStripMenuItem git = new ToolStripMenuItem("Git(&G)");
			git.DropDownItems.Add(Item("刷新文件状态", delegate { RefreshGitStatus(true); }, Keys.F5));
			git.DropDownItems.Add(Item("在 BetterGitGUI 中打开当前仓库", delegate { OpenInBetterGitGUI(); }, Keys.None));
			git.DropDownItems.Add(new ToolStripSeparator());
			git.DropDownItems.Add(Item("打开当前仓库文件夹…", delegate { PromptOpenFolder(); }, Keys.None));
			git.DropDownOpening += delegate
			{
				bool hasRepo = CurrentRepositoryRoot() != null;
				git.DropDownItems[1].Enabled = hasRepo;
			};

			ToolStripMenuItem help = new ToolStripMenuItem("帮助(&H)");
			help.DropDownItems.Add(Item("快捷键说明", delegate { ShowShortcuts(); }, Keys.None));
			help.DropDownItems.Add(Item("第三方开源软件", delegate { ShowThirdParty(); }, Keys.None));
			help.DropDownItems.Add(Item("关于", delegate
			{
				MessageBox.Show(this,
					"Editior\n\n基于 WinForms 的轻量代码编辑器。\n配置文件：%APPDATA%\\.ccnedit\\settings.ini",
					"关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}, Keys.None));

			_menu.Items.Add(file);
			_menu.Items.Add(edit);
			_menu.Items.Add(view);
			_menu.Items.Add(tools);
			_menu.Items.Add(git);
			_menu.Items.Add(help);
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

		private void BuildToolbar()
		{
			_toolbar = new ToolStrip();
			_toolbar.GripStyle = ToolStripGripStyle.Hidden;
			_toolbar.Items.Add(Button("新建", delegate { NewDocument(); }));
			_toolbar.Items.Add(Button("打开文件", delegate { PromptOpenFiles(); }));
			_toolbar.Items.Add(Button("打开文件夹", delegate { PromptOpenFolder(); }));
			_toolbar.Items.Add(Button("保存", delegate { SaveActive(false); }));
			_toolbar.Items.Add(Button("关闭标签", delegate { CloseActive(); }));
			_toolbar.Items.Add(new ToolStripSeparator());
			_toolbar.Items.Add(Button("查找", delegate { ShowFindPanel(false); }));
			_toolbar.Items.Add(Button("替换", delegate { ShowFindPanel(true); }));
			_toolbar.Items.Add(new ToolStripSeparator());
			_toolbar.Items.Add(Button("资源管理器", delegate { ToggleExplorer(); }));
			_toolbar.Items.Add(new ToolStripSeparator());
			_toolbar.Items.Add(Button("CMake / Make", delegate { ShowVisibleCMake(); }));
		}

		private ToolStripButton Button(string text, EventHandler handler)
		{
			ToolStripButton b = new ToolStripButton(text, null, handler);
			b.DisplayStyle = ToolStripItemDisplayStyle.Text;
			return b;
		}

		private void BuildStatusBar()
		{
			_status = new StatusStrip();

			_pathLabel = new ToolStripStatusLabel("");
			_pathLabel.Spring = true;
			_pathLabel.TextAlign = ContentAlignment.MiddleLeft;

			_positionLabel = new ToolStripStatusLabel("行 1，列 1");
			_encodingLabel = new ToolStripStatusLabel("");
			_encodingLabel.IsLink = true;
			_encodingLabel.ToolTipText = "点击选择读取/保存编码";
			_encodingLabel.Click += delegate { ShowFileFormat(ActiveDoc); };
			_eolLabel = new ToolStripStatusLabel("");
			_eolLabel.IsLink = true;
			_eolLabel.ToolTipText = "点击转换 CRLF / LF";
			_eolLabel.Click += delegate { ShowFileFormat(ActiveDoc); };
			_languageLabel = new ToolStripStatusLabel("");
			_zoomLabel = new ToolStripStatusLabel("");
			_infoLabel = new ToolStripStatusLabel("");
			_memoryLabel = new ToolStripStatusLabel("");
			_memoryLabel.Margin = new Padding(6, 0, 12, 0);
			_memoryLabel.ToolTipText = "点击查看内存明细";
			_memoryLabel.Click += delegate { ShowMemoryDetail(); };

			_status.Items.Add(_pathLabel);
			_status.Items.Add(_infoLabel);
			_status.Items.Add(_positionLabel);
			_status.Items.Add(_encodingLabel);
			_status.Items.Add(_eolLabel);
			_status.Items.Add(_languageLabel);
			_status.Items.Add(_zoomLabel);
			_status.Items.Add(_memoryLabel);
			UpdateMemoryStatus();
		}

		// ---------------- 文档管理 ----------------

		private Document ActiveDoc
		{
			get
			{
				if (_tabs == null || _tabs.SelectedTab == null) return null;
				return _tabs.SelectedTab.Tag as Document;
			}
		}

		private void NewDocument()
		{
			Document doc = CreateDocument("", null, new UTF8Encoding(false), false, "CRLF");
			if (doc != null)
			{
				Highlight(doc);
				doc.Box.Focus();
			}
		}

		private Document CreateDocument(string text, string path, Encoding encoding, bool bom, string lineEnding)
		{
			Document doc = new Document();
			doc.FilePath = path;
			doc.Encoding = encoding;
			doc.EmitBom = bom;
			doc.LineEnding = lineEnding;
			doc.Syntax = SyntaxFor(path);
			if (path == null)
			{
				_untitledCount++;
				doc.UntitledName = "未命名" + _untitledCount;
			}

			RichTextBox box = new RichTextBox();
			box.Dock = DockStyle.Fill;
			box.BorderStyle = BorderStyle.None;
			box.Font = new Font(_settings.FontName, _settings.FontSize);
			box.WordWrap = _settings.WordWrap;
			box.ScrollBars = RichTextBoxScrollBars.Both;
			box.HideSelection = false;
			box.DetectUrls = false;
			box.AcceptsTab = true;
			box.BackColor = Color.White;
			box.ForeColor = Color.Black;
			doc.Box = box;

			TabPage page = new TabPage(doc.DisplayName);
			page.Tag = doc;
			doc.Page = page;
			page.Controls.Add(box);

			_suppressDirty = true;
			box.Text = text ?? "";

			_suppressTabEvent = true;
			_tabs.TabPages.Add(page);
			_tabs.SelectedTab = page;
			_suppressTabEvent = false;
			_suppressDirty = false;
			_minimap.Attach(box);

			doc.Dirty = false;
			doc.RefreshTitle();

			if (path != null)
			{
				try
				{
					doc.LastWriteTime = File.GetLastWriteTimeUtc(path);
					doc.ReadOnly = new FileInfo(path).IsReadOnly;
					if (doc.ReadOnly) box.ReadOnly = true;
				}
				catch { }
			}

			ApplyTabStops(box);

			box.TextChanged += OnEditorTextChanged;
			box.SelectionChanged += OnEditorSelectionChanged;
			box.KeyDown += OnEditorKeyDown;
			box.MouseWheel += OnEditorMouseWheel;
			doc.RefreshTitle();
			UpdateTitle();
			UpdateStatusBar();
			return doc;
		}

		private Document OpenFile(string path)
		{
			if (path == null) return null;
			try
			{
				path = Path.GetFullPath(path);
			}
			catch { return null; }

			foreach (TabPage page in _tabs.TabPages)
			{
				Document open = page.Tag as Document;
				if (open != null && open.FilePath != null &&
					string.Equals(open.FilePath, path, StringComparison.OrdinalIgnoreCase))
				{
					_tabs.SelectedTab = page;
					return open;
				}
			}

			if (!File.Exists(path))
			{
				MessageBox.Show(this, "文件不存在：\n" + path, "Editior", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return null;
			}

			string text;
			Encoding encoding;
			bool bom;
			string lineEnding;
			try
			{
				TextFile.Load(path, out text, out encoding, out bom, out lineEnding);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "无法打开文件：\n" + ex.Message, "Editior", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return null;
			}

			Document doc = CreateDocument(text, path, encoding, bom, lineEnding);
			AddRecent(path);
			Highlight(doc);
			return doc;
		}

		private bool SaveDocument(Document doc, bool saveAs)
		{
			if (doc == null) return false;

			string path = doc.FilePath;
			if (saveAs || path == null)
			{
				using (SaveFileDialog dlg = new SaveFileDialog())
				{
					dlg.FileName = doc.DisplayName;
					dlg.Filter = "所有文件|*.*";
					if (doc.FilePath != null) dlg.InitialDirectory = Path.GetDirectoryName(doc.FilePath);
					else if (_folderPath != null) dlg.InitialDirectory = _folderPath;
					if (dlg.ShowDialog(this) != DialogResult.OK) return false;
					path = dlg.FileName;
				}
			}

			string text = doc.Box.Text.Replace("\r\n", "\n").Replace('\r', '\n');
			try
			{
				TextFile.Save(path, text, doc.Encoding, doc.EmitBom, doc.LineEnding);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "保存失败：\n" + ex.Message, "Editior", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			doc.FilePath = path;
			doc.Dirty = false;
			doc.Syntax = SyntaxFor(path);
			doc.RefreshTitle();
			AddRecent(path);
			UpdateTitle();
			UpdateStatusBar();
			Highlight(doc);
			return true;
		}

		private bool SaveActive(bool saveAs)
		{
			Document doc = ActiveDoc;
			if (doc == null) return false;
			return SaveDocument(doc, saveAs);
		}

		private void SaveAll()
		{
			foreach (TabPage page in _tabs.TabPages)
			{
				Document doc = page.Tag as Document;
				if (doc != null && doc.Dirty) SaveDocument(doc, false);
			}
		}

		private bool CloseDocument(Document doc)
		{
			if (doc == null) return true;

			if (doc.Dirty)
			{
				DialogResult r = MessageBox.Show(this,
					"“" + doc.DisplayName + "” 尚未保存，是否保存？",
					"Editior", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
				if (r == DialogResult.Cancel) return false;
				if (r == DialogResult.Yes && !SaveDocument(doc, false)) return false;
			}

			_suppressTabEvent = true;
			_tabs.TabPages.Remove(doc.Page);
			doc.Page.Dispose();
			_suppressTabEvent = false;

			Document active = ActiveDoc;
			_minimap.Attach(active == null ? null : active.Box);

			// 关掉最后一个标签后补一个空白文档，避免编辑区变成一块死白
			if (_tabs.TabPages.Count == 0) NewDocument();
			UpdateTitle();
			UpdateStatusBar();
			return true;
		}

		private void CloseActive()
		{
			Document doc = ActiveDoc;
			if (doc != null) CloseDocument(doc);
		}

		private void CloseOthers()
		{
			Document active = ActiveDoc;
			if (active == null) return;
			List<TabPage> pages = new List<TabPage>(ToPages());
			foreach (TabPage page in pages)
			{
				Document doc = page.Tag as Document;
				if (doc == null || doc == active) continue;
				if (!CloseDocument(doc)) return;
			}
		}

		private void CloseAll()
		{
			List<TabPage> pages = new List<TabPage>(ToPages());
			foreach (TabPage page in pages)
			{
				Document doc = page.Tag as Document;
				if (doc == null) continue;
				if (!CloseDocument(doc)) return;
			}
		}

		private void OnTabChanged(object sender, EventArgs e)
		{
			if (_suppressTabEvent) return;
			UpdateTitle();
			UpdateStatusBar();

			Document doc = ActiveDoc;
			_minimap.Attach(doc == null ? null : doc.Box);
			if (doc != null)
			{
				doc.Box.Focus();
				QueueHighlight(doc);
			}
		}

		private void OnTabsMouseClick(object sender, MouseEventArgs e)
		{
			int index = TabIndexAt(e.Location);
			if (e.Button == MouseButtons.Right && index >= 0)
			{
				_tabs.SelectedIndex = index;
				return;
			}
			if (e.Button != MouseButtons.Middle || index < 0) return;
			Document doc = _tabs.TabPages[index].Tag as Document;
			if (doc != null) CloseDocument(doc);
		}

		private int TabIndexAt(Point location)
		{
			for (int i = 0; i < _tabs.TabPages.Count; i++)
			{
				if (_tabs.GetTabRect(i).Contains(location)) return i;
			}
			return -1;
		}

		private void UpdateTitle()
		{
			Document doc = ActiveDoc;
			if (doc == null) Text = "Editior";
			else Text = doc.DisplayName + (doc.Dirty ? " *" : "") + " — Editior";
		}

		private void UpdateStatusBar()
		{
			Document doc = ActiveDoc;
			if (doc == null)
			{
				_pathLabel.Text = "";
				_encodingLabel.Text = "";
				_eolLabel.Text = "";
				_languageLabel.Text = "";
				_zoomLabel.Text = "";
				_positionLabel.Text = "";
				return;
			}

			_pathLabel.Text = doc.FilePath ?? "(未保存)";
			_encodingLabel.Text = doc.EncodingDisplay;
			_eolLabel.Text = doc.LineEndingDisplay;
			_languageLabel.Text = doc.Syntax.Name;
			_zoomLabel.Text = "缩放 " + (int)Math.Round(doc.Box.ZoomFactor * 100) + "%";
			UpdateCaretStatus();
		}

		private void UpdateCaretStatus()
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			RichTextBox box = doc.Box;
			int pos = box.SelectionStart;
			int line = box.GetLineFromCharIndex(pos);
			int lineStart = box.GetFirstCharIndexFromLine(line);
			int col = pos - (lineStart < 0 ? 0 : lineStart) + 1;
			string text = "行 " + (line + 1) + "，列 " + col;
			if (box.SelectionLength > 0) text += "（已选 " + box.SelectionLength + "）";
			_positionLabel.Text = text;
		}

		private void OnEditorSelectionChanged(object sender, EventArgs e)
		{
			Document doc = ActiveDoc;
			if (doc == null || doc.Box != sender) return;
			UpdateCaretStatus();
		}

		private void OnEditorTextChanged(object sender, EventArgs e)
		{
			Document doc = ActiveDoc;
			if (doc == null || doc.Box != sender) return;
			if (_suppressDirty || _highlighting) return;

			bool wasDirty = doc.Dirty;
			doc.Dirty = true;
			if (!wasDirty)
			{
				doc.RefreshTitle();
				UpdateTitle();
			}
			QueueHighlight(doc);
		}

		// ---------------- 语法高亮 ----------------

		private void QueueHighlight(Document doc)
		{
			_highlightDoc = doc;
			_highlightTimer.Stop();
			_highlightTimer.Start();
		}

		private void OnHighlightTimer(object sender, EventArgs e)
		{
			_highlightTimer.Stop();
			Highlight(_highlightDoc);
			_highlightDoc = null;
		}

		private void Highlight(Document doc)
		{
			if (doc == null || doc.Box == null || doc.Syntax == null) return;
			if (doc.Syntax == SyntaxRegistry.Plain) return;
			if (!SyntaxHighlighter.CanHighlight(doc.Box)) return;

			// 设置字符颜色会触发 RichTextBox 的 TextChanged，这里临时摘掉订阅，
			// 避免把高亮当成用户编辑。
			_highlighting = true;
			doc.Box.TextChanged -= OnEditorTextChanged;
			try
			{
				SyntaxHighlighter.Colorize(doc.Box, doc.Syntax);
			}
			finally
			{
				doc.Box.TextChanged += OnEditorTextChanged;
				_highlighting = false;
			}
		}

		private SyntaxDefinition SyntaxFor(string path)
		{
			if (path == null) return SyntaxRegistry.Plain;
			try { return SyntaxRegistry.ForName(path); }
			catch { return SyntaxRegistry.Plain; }
		}

		// ---------------- 编辑辅助 ----------------

		private void WithEditor(Action<RichTextBox> action)
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			action(doc.Box);
			doc.Box.Focus();
		}

		private void Redo()
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			SendMessage(doc.Box.Handle, EM_REDO, IntPtr.Zero, IntPtr.Zero);
		}

		private void ChangeFontSize(float delta)
		{
			float size = Math.Max(7f, Math.Min(36f, _settings.FontSize + delta));
			if (size == _settings.FontSize) return;
			_settings.FontSize = size;
			foreach (TabPage page in _tabs.TabPages)
			{
				Document doc = page.Tag as Document;
				if (doc == null) continue;
				doc.Box.Font = new Font(_settings.FontName, size);
				ApplyTabStops(doc.Box);
			}
		}

		private void ApplyTabStops(RichTextBox box)
		{
			try
			{
				int unit = TextRenderer.MeasureText(new string(' ', Math.Max(1, _settings.TabSize)), box.Font,
					new Size(int.MaxValue, int.MaxValue),
					TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
				if (unit <= 0) return;
				int[] tabs = new int[24];
				for (int i = 0; i < tabs.Length; i++) tabs[i] = unit * (i + 1);
				int selStart = box.SelectionStart;
				int selLength = box.SelectionLength;
				box.SelectAll();
				box.SelectionTabs = tabs;
				box.Select(Math.Min(selStart, box.TextLength), Math.Min(selLength, Math.Max(0, box.TextLength - selStart)));
			}
			catch { }
		}

		private void ApplySettings()
		{
			foreach (TabPage page in _tabs.TabPages)
			{
				Document doc = page.Tag as Document;
				if (doc == null) continue;
				doc.Box.Font = new Font(_settings.FontName, _settings.FontSize);
				doc.Box.WordWrap = _settings.WordWrap;
				ApplyTabStops(doc.Box);
				Highlight(doc);
			}
			_minimap.Visible = _settings.ShowMinimap;
			_minimap.Width = _settings.MinimapWidth;
			_wrapMenu.Checked = _settings.WordWrap;
			UpdateStatusBar();
		}

		private void PromptSettings()
		{
			using (SettingsForm dlg = new SettingsForm(_settings))
			{
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				dlg.SaveTo(_settings);
				_settings.Save();
				ApplySettings();
				if (_settings.ShowGitStatus) RefreshGitStatus();
				else ClearGitStatus();
			}
		}

		private ToolStripMenuItem BuildEncodingMenu()
		{
			ToolStripMenuItem root = new ToolStripMenuItem("编码");
			ToolStripMenuItem utf8 = new ToolStripMenuItem("UTF-8（无 BOM）");
			ToolStripMenuItem utf8Bom = new ToolStripMenuItem("UTF-8 with BOM");
			ToolStripMenuItem gbk = new ToolStripMenuItem("GBK");

			utf8.Click += delegate { SetEncoding(new UTF8Encoding(false), false, "UTF-8"); };
			utf8Bom.Click += delegate { SetEncoding(new UTF8Encoding(false), true, "UTF-8 BOM"); };
			gbk.Click += delegate
			{
				try { SetEncoding(Encoding.GetEncoding(936), false, "GBK"); }
				catch (Exception ex)
				{
					MessageBox.Show(this, "当前系统不支持 GBK 编码：" + ex.Message, "Editior",
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			};

			root.DropDownItems.Add(utf8);
			root.DropDownItems.Add(utf8Bom);
			root.DropDownItems.Add(gbk);
			root.DropDownOpening += delegate
			{
				Document doc = ActiveDoc;
				bool isUtf8 = doc != null && doc.Encoding is UTF8Encoding;
				utf8.Checked = isUtf8 && !doc.EmitBom;
				utf8Bom.Checked = isUtf8 && doc.EmitBom;
				gbk.Checked = doc != null && doc.Encoding.CodePage == 936;
				bool enabled = doc != null;
				utf8.Enabled = enabled;
				utf8Bom.Enabled = enabled;
				gbk.Enabled = enabled;
			};
			return root;
		}

		private ToolStripMenuItem BuildLineEndingMenu()
		{
			ToolStripMenuItem root = new ToolStripMenuItem("换行符");
			ToolStripMenuItem crlf = new ToolStripMenuItem("CRLF（Windows）");
			ToolStripMenuItem lf = new ToolStripMenuItem("LF（Unix）");

			crlf.Click += delegate { SetLineEnding("CRLF"); };
			lf.Click += delegate { SetLineEnding("LF"); };
			root.DropDownItems.Add(crlf);
			root.DropDownItems.Add(lf);
			root.DropDownOpening += delegate
			{
				Document doc = ActiveDoc;
				crlf.Checked = doc != null && doc.LineEnding == "CRLF";
				lf.Checked = doc != null && doc.LineEnding == "LF";
				crlf.Enabled = doc != null;
				lf.Enabled = doc != null;
			};
			return root;
		}

		private void SetEncoding(Encoding encoding, bool bom, string label)
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			doc.Encoding = encoding;
			doc.EmitBom = bom;
			MarkSettingsChanged(doc);
			_infoLabel.Text = "保存时使用 " + label;
		}

		/// <summary>编码 / 换行符对话框：重新读取、只记设置、立刻写回三选一。</summary>
		private void ShowFileFormat(Document doc)
		{
			if (doc == null) return;

			Encoding encoding = doc.Encoding;
			bool bom = doc.EmitBom;
			string lineEnding = doc.LineEnding;
			bool reload = false;
			bool saveNow = false;

			using (FileFormatDialog dlg = new FileFormatDialog(doc))
			{
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				encoding = dlg.SelectedEncoding;
				bom = dlg.SelectedBom;
				lineEnding = dlg.SelectedLineEnding;
				reload = dlg.ReloadNow;
				saveNow = dlg.SaveNow;
			}

			if (reload)
			{
				if (doc.FilePath == null)
				{
					MessageBox.Show(this, "当前文档尚未保存到磁盘，无法重新读取。", "Editior",
						MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				if (doc.Dirty && MessageBox.Show(this, "“" + doc.DisplayName + "”有未保存的修改，重新读取会丢弃它们。继续？",
					"Editior", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
				return;

				string text;
				bool actualBom;
				string actualEol;
				try { TextFile.LoadWithEncoding(doc.FilePath, encoding, out text, out actualBom, out actualEol); }
				catch (Exception ex)
				{
					MessageBox.Show(this, "按该编码读取失败：\n" + ex.Message, "Editior",
						MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				_suppressDirty = true;
				doc.Box.Text = text;
				_suppressDirty = false;
				doc.Encoding = encoding;
				doc.EmitBom = actualBom;
				doc.LineEnding = actualEol;
				doc.Dirty = false;
				try { doc.LastWriteTime = File.GetLastWriteTimeUtc(doc.FilePath); }
				catch { }
				doc.RefreshTitle();
				UpdateTitle();
				UpdateStatusBar();
				Highlight(doc);
				_infoLabel.Text = "已按 " + doc.EncodingDisplay + " / " + doc.LineEndingDisplay + " 重新读取";
				return;
			}

			doc.Encoding = encoding;
			doc.EmitBom = bom;
			doc.LineEnding = lineEnding;
			MarkSettingsChanged(doc);

			if (saveNow)
			{
				if (SaveDocument(doc, doc.FilePath == null))
					_infoLabel.Text = "已按 " + doc.EncodingDisplay + " / " + doc.LineEndingDisplay + " 写回文件";
			}
			else
			{
				_infoLabel.Text = "保存时使用 " + doc.EncodingDisplay + " / " + doc.LineEndingDisplay;
			}
		}

		/// <summary>状态栏内存文字：工作集（约等于任务管理器里的“内存”）。</summary>
		private void UpdateMemoryStatus()
		{
			try
			{
				System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
				proc.Refresh();
				long workingSet = proc.WorkingSet64;
				long privateBytes = proc.PrivateMemorySize64;
				_memoryLabel.Text = "内存 " + FormatBytes(workingSet);
				_memoryLabel.ToolTipText = "工作集 " + FormatBytes(workingSet) + "，私有 " + FormatBytes(privateBytes) +
					"，GC 堆 " + FormatBytes(GC.GetTotalMemory(false)) + "（点击查看明细）";
			}
			catch
			{
				_memoryLabel.Text = "内存 —";
			}
		}

		private static string FormatBytes(long bytes)
		{
			if (bytes >= 1024L * 1024L * 1024L) return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") + " GB";
			if (bytes >= 1024L * 1024L) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
			return (bytes / 1024.0).ToString("0") + " KB";
		}

		private void ShowMemoryDetail()
		{
			try
			{
				System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
				proc.Refresh();
				MessageBox.Show(this,
					"工作集（Working Set）：" + FormatBytes(proc.WorkingSet64) + "\r\n" +
					"峰值工作集：" + FormatBytes(proc.PeakWorkingSet64) + "\r\n" +
					"私有字节（Private Bytes）：" + FormatBytes(proc.PrivateMemorySize64) + "\r\n" +
					"虚拟内存：" + FormatBytes(proc.VirtualMemorySize64) + "\r\n" +
					"GC 托管堆：" + FormatBytes(GC.GetTotalMemory(false)) + "\r\n" +
					"进程：" + proc.ProcessName + "（PID " + proc.Id + "）",
					"内存占用", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "读取内存信息失败：" + ex.Message, "Editior",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}

		private void SetLineEnding(string ending)
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			doc.LineEnding = ending;
			MarkSettingsChanged(doc);
			_infoLabel.Text = "保存时使用 " + ending;
		}

		private void MarkSettingsChanged(Document doc)
		{
			if (!doc.Dirty)
			{
				doc.Dirty = true;
				doc.RefreshTitle();
				UpdateTitle();
			}
			UpdateStatusBar();
		}

		private void OnEditorKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode != Keys.Enter || e.Control || e.Alt || e.Shift) return;

			RichTextBox box = (RichTextBox)sender;
			int caret = box.SelectionStart;
			int line = box.GetLineFromCharIndex(caret);
			if (line < 0) return;

			string lineText = "";
			if (line < box.Lines.Length) lineText = box.Lines[line];

			int indentEnd = 0;
			while (indentEnd < lineText.Length && (lineText[indentEnd] == ' ' || lineText[indentEnd] == '\t')) indentEnd++;

			string indent = lineText.Substring(0, indentEnd);
			string insert = "\n" + indent;

			if (caret > 0 && box.TextLength < 400000)
			{
				char before = box.Text[caret - 1];
				if (before == '{') insert += "\t";
			}

			box.SelectedText = insert;
			e.SuppressKeyPress = true;
		}

		private void OnEditorMouseWheel(object sender, MouseEventArgs e)
		{
			if ((ModifierKeys & Keys.Control) != Keys.Control) return;
			RichTextBox box = (RichTextBox)sender;
			float zoom = box.ZoomFactor + (e.Delta > 0 ? 0.1f : -0.1f);
			box.ZoomFactor = Math.Max(0.5f, Math.Min(4f, zoom));
			UpdateStatusBar();
			if (e is HandledMouseEventArgs) ((HandledMouseEventArgs)e).Handled = true;
		}

		// ---------------- 打开 / 保存对话框 ----------------

		private void PromptOpenFiles()
		{
			using (OpenFileDialog dlg = new OpenFileDialog())
			{
				dlg.Multiselect = true;
				dlg.Filter = "所有文件|*.*";
				if (_folderPath != null) dlg.InitialDirectory = _folderPath;
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				foreach (string file in dlg.FileNames) OpenFile(file);
			}
		}

		private void PromptOpenFolder()
		{
			using (FolderBrowserDialog dlg = new FolderBrowserDialog())
			{
				dlg.Description = "选择要打开的文件夹";
				if (_folderPath != null) dlg.SelectedPath = _folderPath;
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				LoadFolder(dlg.SelectedPath);
			}
		}

		private void PromptGoToLine()
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			int current = doc.Box.GetLineFromCharIndex(doc.Box.SelectionStart) + 1;
			using (InputDialog dlg = new InputDialog("转到行", "行号（1 - " + doc.Box.Lines.Length + "）：", current.ToString()))
			{
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				int line;
				try { line = int.Parse(dlg.Value); }
				catch { return; }
				line = Math.Max(1, Math.Min(doc.Box.Lines.Length, line));
				int index = doc.Box.GetFirstCharIndexFromLine(line - 1);
				if (index < 0) return;
				doc.Box.Select(index, 0);
				doc.Box.ScrollToCaret();
				doc.Box.Focus();
			}
		}

		private void ShowShortcuts()		{
			MessageBox.Show(this,
				"Ctrl+N  新建\n" +
				"Ctrl+O  打开文件\n" +
				"Ctrl+Shift+O  打开文件夹\n" +
				"Ctrl+S  保存\n" +
				"Ctrl+Shift+S  另存为\n" +
				"Ctrl+W  关闭标签\n" +
				"Ctrl+F  查找\n" +
				"Ctrl+H  替换\n" +
				"Ctrl+G  转到行\n" +
				"Ctrl+B  显示/隐藏资源管理器\n" +
				"Ctrl+滚轮  缩放\n" +
				"F2  重命名资源管理器中的节点",
				"快捷键说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void ShowGuidGenerator()
		{
			using (GuidGeneratorForm form = new GuidGeneratorForm())
			{
				form.ShowDialog(this);
			}
		}

		/// <summary>
		/// 数据转代码：位图 / 汇编机器码 / 十六进制字节 → 各语言常量数组。
		/// 生成的内容可以一键插入当前文档（没有打开文档时先新建一个）。
		/// </summary>
		private void ShowDataCode()
		{
			using (DataCodeForm form = new DataCodeForm(delegate(string text) { InsertGeneratedCode(text); }))
			{
				form.ShowDialog(this);
			}
		}

		private void InsertGeneratedCode(string text)
		{
			if (string.IsNullOrEmpty(text)) return;
			Document doc = ActiveDoc;
			if (doc == null)
			{
				NewDocument();
				doc = ActiveDoc;
			}
			if (doc == null || doc.ReadOnly) return;

			doc.Box.SelectedText = text;
			doc.Dirty = true;
			doc.RefreshTitle();
			doc.Box.Focus();
		}

		private void ShowThirdParty()
		{			MessageBox.Show(this,
				"本程序在解析 .slnx / .csproj 时使用了以下微软官方 NuGet 包：\n\n" +
				"• Microsoft.Build（MIT License）\n" +
				"  用于读取 MSBuild 项目文件（.csproj/.props/.targets）与 .sln 解决方案。\n\n" +
				"• Microsoft.VisualStudio.SolutionPersistence（MIT License）\n" +
				"  用于读取 .slnx 解决方案文件。\n\n" +
				"其余功能使用的都是 .NET Framework 自带的 System.Windows.Forms / System.Drawing。",
				"第三方开源软件", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		// ---------------- 查找与替换 ----------------

		private void ShowFindPanel(bool replace)
		{
			_findPanel.Visible = true;
			ArrangeFindPanel();
			_findBox.Focus();

			Document doc = ActiveDoc;
			if (doc != null && _findBox.Text.Length == 0)
			{
				string selected = doc.Box.SelectedText;
				if (selected.Length > 0 && selected.IndexOf('\n') < 0) _findBox.Text = selected;
			}

			if (replace) _replaceBox.Focus();
		}

		private void HideFindPanel()
		{
			_findPanel.Visible = false;
			Document doc = ActiveDoc;
			if (doc != null) doc.Box.Focus();
		}

		private void OnFindBoxKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Escape)
			{
				HideFindPanel();
				e.SuppressKeyPress = true;
			}
			else if (e.KeyCode == Keys.Enter)
			{
				FindNext(true);
				e.SuppressKeyPress = true;
			}
		}

		private RichTextBoxFinds FindOptions()
		{
			RichTextBoxFinds options = RichTextBoxFinds.None;
			if (_caseBox.Checked) options |= RichTextBoxFinds.MatchCase;
			if (_wordBox.Checked) options |= RichTextBoxFinds.WholeWord;
			return options;
		}

		private void FindNext(bool forward)
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			string query = _findBox.Text;
			if (query.Length == 0) return;

			RichTextBox box = doc.Box;
			RichTextBoxFinds options = FindOptions();
			int index = -1;

			if (forward)
			{
				int from = box.SelectionStart + box.SelectionLength;
				index = box.Find(query, from, options);
				if (index < 0 && from > 0) index = box.Find(query, 0, options);
			}
			else
			{
				int limit = box.SelectionStart;
				if (limit > 0) index = box.Find(query, 0, limit, options | RichTextBoxFinds.Reverse);
				if (index < 0) index = box.Find(query, 0, box.TextLength, options | RichTextBoxFinds.Reverse);
			}

			if (index < 0)
			{
				_infoLabel.Text = "未找到“" + query + "”";
				return;
			}

			box.Select(index, query.Length);
			box.ScrollToCaret();
			UpdateCaretStatus();
			_infoLabel.Text = "第 " + (box.GetLineFromCharIndex(index) + 1) + " 行";
		}

		private void ReplaceCurrent()
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			string query = _findBox.Text;
			if (query.Length == 0) return;

			RichTextBox box = doc.Box;
			bool match = box.SelectionLength == query.Length && string.Equals(box.SelectedText, query,
				_caseBox.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
			if (match)
			{
				box.SelectedText = _replaceBox.Text;
			}
			FindNext(true);
		}

		private void ReplaceAll()
		{
			Document doc = ActiveDoc;
			if (doc == null) return;
			string query = _findBox.Text;
			if (query.Length == 0) return;

			RichTextBox box = doc.Box;
			RichTextBoxFinds options = FindOptions();
			string replacement = _replaceBox.Text;
			int count = 0;
			int start = 0;

			while (count < 100000)
			{
				int index = box.Find(query, start, options);
				if (index < 0) break;
				box.Select(index, query.Length);
				box.SelectedText = replacement;
				start = index + replacement.Length;
				if (replacement.Length == 0) start = index;
				count++;
			}

			_infoLabel.Text = "已替换 " + count + " 处";
			UpdateCaretStatus();
		}

		// ---------------- 资源管理器 ----------------

	private ContextMenuStrip BuildTreeMenu()
		{
			ContextMenuStrip menu = new ContextMenuStrip();
			menu.Items.Add(Item("打开", delegate { OpenSelectedNode(); }, Keys.None));
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add(Item("新建文件…", delegate { NewFileInFolder(); }, Keys.None));
			menu.Items.Add(Item("新建文件夹…", delegate { NewFolderInFolder(); }, Keys.None));
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add(Item("重命名 (F2)", delegate { RenameNode(); }, Keys.None));
			menu.Items.Add(Item("删除", delegate { DeleteNode(); }, Keys.None));
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add(Item("在资源管理器中显示", delegate { RevealNode(); }, Keys.None));
			menu.Items.Add(Item("复制路径", delegate { CopyNodePath(); }, Keys.None));
			menu.Items.Add(Item("刷新", delegate { RefreshNode(); }, Keys.None));
			return menu;
		}

		private void LoadFolder(string path)
		{
			if (path == null || !Directory.Exists(path)) return;
			_folderPath = path;
			_settings.LastFolder = path;

			_tree.BeginUpdate();
			_tree.Nodes.Clear();

			TreeNode root = new TreeNode(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)));
			if (root.Text.Length == 0) root.Text = path;
			root.Tag = path;
			root.Nodes.Add(new TreeNode("…"));
			_tree.Nodes.Add(root);
			_tree.EndUpdate();

			PopulateNode(root);
			root.Expand();
			root.EnsureVisible();
			RefreshGitStatus();
			UpdateStatusBar();
		}

		private void PopulateNode(TreeNode node)
		{
			string dir = node.Tag as string;
			if (dir == null || !Directory.Exists(dir)) return;

			node.Nodes.Clear();
			string[] directories = new string[0];
			string[] files = new string[0];
			try { directories = Directory.GetDirectories(dir); } catch { }
			try { files = Directory.GetFiles(dir); } catch { }

			Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
			Array.Sort(files, StringComparer.OrdinalIgnoreCase);

			foreach (string sub in directories)
			{
				string name = Path.GetFileName(sub);
				if (name == ".git" || name == ".vs" || name == "node_modules" || name == "bin" || name == "obj" || name == "packages") continue;
				TreeNode child = new TreeNode(name);
				child.Tag = sub;
				child.Nodes.Add(new TreeNode("…"));
				node.Nodes.Add(child);
			}

			foreach (string file in files)
			{
				TreeNode child = new TreeNode(Path.GetFileName(file));
				child.Tag = file;
				node.Nodes.Add(child);
			}
		}

		private void OnTreeBeforeExpand(object sender, TreeViewCancelEventArgs e)
		{
			TreeNode node = e.Node;
			if (node == null) return;
			if (node.Tag is string && node.Nodes.Count == 1 && node.Nodes[0].Tag == null)
			{
				PopulateNode(node);
				ApplyGitStatus(node.Nodes);
			}
		}

		private void OnTreeDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
		{
			if (e.Node == null) return;
			if (e.Node.Tag is string)
			{
				string path = (string)e.Node.Tag;
				if (File.Exists(path)) OpenFile(path);
				else if (Directory.Exists(path)) e.Node.Toggle();
			}
		}

		private void OnTreeKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.F2) RenameNode();
		}

		private string SelectedNodePath()
		{
			TreeNode node = _tree.SelectedNode;
			if (node == null) return null;
			return node.Tag as string;
		}

		private string SelectedFolderPath()
		{
			string path = SelectedNodePath();
			if (path == null) return _folderPath;
			if (File.Exists(path)) return Path.GetDirectoryName(path);
			return path;
		}

		private void OpenSelectedNode()
		{
			string path = SelectedNodePath();
			if (path != null && File.Exists(path)) OpenFile(path);
		}

		private void NewFileInFolder()
		{
			string folder = SelectedFolderPath();
			if (folder == null) return;

			using (InputDialog dlg = new InputDialog("新建文件", "文件名：", ""))
			{
				if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Value.Length == 0) return;
				string path = Path.Combine(folder, dlg.Value);
				if (File.Exists(path) || Directory.Exists(path))
				{
					MessageBox.Show(this, "已存在同名文件或文件夹。", "Editior", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(path));
					File.WriteAllText(path, "", new UTF8Encoding(false));
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "创建失败：" + ex.Message, "Editior", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				RefreshFolderView();
				OpenFile(path);
			}
		}

		private void NewFolderInFolder()
		{
			string folder = SelectedFolderPath();
			if (folder == null) return;

			using (InputDialog dlg = new InputDialog("新建文件夹", "文件夹名：", ""))
			{
				if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Value.Length == 0) return;
				string path = Path.Combine(folder, dlg.Value);
				try { Directory.CreateDirectory(path); }
				catch (Exception ex)
				{
					MessageBox.Show(this, "创建失败：" + ex.Message, "Editior", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				RefreshFolderView();
			}
		}

		private void RenameNode()
		{
			string path = SelectedNodePath();
			if (path == null || path == _folderPath) return;

			using (InputDialog dlg = new InputDialog("重命名", "新名称：", Path.GetFileName(path)))
			{
				if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Value.Length == 0) return;
				string parent = Path.GetDirectoryName(path);
				string target = Path.Combine(parent, dlg.Value);
				if (File.Exists(target) || Directory.Exists(target))
				{
					MessageBox.Show(this, "已存在同名文件或文件夹。", "Editior", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
				try
				{
					if (Directory.Exists(path)) Directory.Move(path, target);
					else File.Move(path, target);
				}
				catch (Exception ex)
				{
					MessageBox.Show(this, "重命名失败：" + ex.Message, "Editior", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				RefreshFolderView();
			}
		}

		private void DeleteNode()
		{
			string path = SelectedNodePath();
			if (path == null || path == _folderPath) return;

			DialogResult r = MessageBox.Show(this,
				"确定要删除 “" + Path.GetFileName(path) + "” 吗？此操作不可撤销。",
				"Editior", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			if (r != DialogResult.Yes) return;

			foreach (TabPage page in new List<TabPage>(ToPages()))
			{
				Document doc = page.Tag as Document;
				if (doc != null && doc.FilePath != null && IsUnder(doc.FilePath, path))
				{
					doc.Dirty = false;
					_tabs.TabPages.Remove(doc.Page);
					doc.Page.Dispose();
				}
			}

			try
			{
				if (Directory.Exists(path)) Directory.Delete(path, true);
				else File.Delete(path);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "删除失败：" + ex.Message, "Editior", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			RefreshFolderView();
			UpdateTitle();
			UpdateStatusBar();
		}

		private IEnumerable<TabPage> ToPages()
		{
			foreach (TabPage page in _tabs.TabPages) yield return page;
		}

		private bool IsUnder(string file, string path)
		{
			string full = Path.GetFullPath(file);
			string parent = Path.GetFullPath(path);
			return full.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(full, parent, StringComparison.OrdinalIgnoreCase);
		}

		private void RevealNode()
		{
			string path = SelectedNodePath();
			if (path == null) return;
			try
			{
				if (File.Exists(path)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
				else Process.Start("explorer.exe", "\"" + path + "\"");
			}
			catch { }
		}

		private void CopyNodePath()
		{
			string path = SelectedNodePath();
			if (path != null) Clipboard.SetText(path);
		}

		private void RefreshNode()
		{
			TreeNode node = _tree.SelectedNode;
			if (node == null || !(node.Tag is string)) return;
			string path = (string)node.Tag;
			if (Directory.Exists(path))
			{
				PopulateNode(node);
				node.Expand();
			}
			else
			{
				RefreshFolderView();
			}
		}

		private void RefreshFolderView()
		{
			if (_folderPath == null) return;
			LoadFolder(_folderPath);
		}

		private void ToggleExplorer()
		{
			_split.Panel1Collapsed = !_explorerMenu.Checked;
		}

		private void ToggleWordWrap()
		{
			_settings.WordWrap = _wrapMenu.Checked;
			foreach (TabPage page in _tabs.TabPages)
			{
				Document doc = page.Tag as Document;
				if (doc == null) continue;
				doc.Box.WordWrap = _settings.WordWrap;
				doc.Box.ScrollBars = RichTextBoxScrollBars.Both;
			}
		}

		// ---------------- 最近文件 ----------------

		private void AddRecent(string path)
		{
			_settings.RecentFiles.Remove(path);
			_settings.RecentFiles.Insert(0, path);
			while (_settings.RecentFiles.Count > 10) _settings.RecentFiles.RemoveAt(_settings.RecentFiles.Count - 1);
			RebuildRecentMenu();
		}

		private void RebuildRecentMenu()
		{
			if (_recentMenu == null) return;
			_recentMenu.DropDownItems.Clear();
			if (_settings.RecentFiles.Count == 0)
			{
				ToolStripMenuItem empty = new ToolStripMenuItem("（无）");
				empty.Enabled = false;
				_recentMenu.DropDownItems.Add(empty);
				return;
			}
			foreach (string path in _settings.RecentFiles)
			{
				ToolStripMenuItem item = new ToolStripMenuItem(path);
				item.Click += delegate { OpenFile(path); };
				_recentMenu.DropDownItems.Add(item);
			}
			_recentMenu.DropDownItems.Add(new ToolStripSeparator());
			ToolStripMenuItem clear = new ToolStripMenuItem("清除列表");
			clear.Click += delegate
			{
				_settings.RecentFiles.Clear();
				RebuildRecentMenu();
			};
			_recentMenu.DropDownItems.Add(clear);
		}

		// ---------------- 解决方案（.sln / .slnx / 项目文件） ----------------

		private void PromptOpenSolution()
		{
			using (OpenFileDialog dlg = new OpenFileDialog())
			{
				dlg.Title = "打开解决方案";
				dlg.Filter = "解决方案 (*.sln;*.slnx)|*.sln;*.slnx|所有文件 (*.*)|*.*";
				if (dlg.ShowDialog(this) != DialogResult.OK) return;
				OpenSolution(dlg.FileName);
			}
		}

		private void OpenSolution(string path)
		{
			SolutionNode model;
			try
			{
				model = SolutionParser.LoadSolution(path);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "解析解决方案失败：\n" + ex.Message, "Editior",
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			_tree.BeginUpdate();
			_tree.Nodes.Clear();
			TreeNode root = BuildSolutionNode(model);
			if (root.Nodes.Count > 0) root.Expand();
			_tree.Nodes.Add(root);
			_tree.EndUpdate();

			_folderPath = Path.GetDirectoryName(path);
			_settings.LastFolder = _folderPath;
			_settings.AddRecent(path);
			RebuildRecentMenu();
			RefreshGitStatus();
			UpdateStatusBar();
			_infoLabel.Text = "已加载解决方案 " + Path.GetFileName(path);
		}

		private TreeNode BuildSolutionNode(SolutionNode model)
		{
			TreeNode node = new TreeNode(model.Name);
			node.Tag = model.Path;
			node.ToolTipText = model.Path ?? "";

			if (model.Path != null && IsProjectFile(model.Path) && model.Children.Count == 0)
			{
				string projectDir = Path.GetDirectoryName(model.Path);
				foreach (string file in SolutionParser.GetProjectFiles(model.Path))
				{
					string rel = file;
					if (projectDir != null && file.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase))
						rel = file.Substring(projectDir.Length).TrimStart('\\', '/');
					TreeNode child = new TreeNode(rel);
					child.Tag = file;
					node.Nodes.Add(child);
				}
			}
			else
			{
				foreach (SolutionNode child in model.Children)
					node.Nodes.Add(BuildSolutionNode(child));
			}
			return node;
		}

		private static bool IsProjectFile(string path)
		{
			string ext = Path.GetExtension(path).ToLowerInvariant();
			return ext == ".csproj" || ext == ".vbproj" || ext == ".fsproj" || ext == ".vcxproj"
				|| ext == ".proj" || ext == ".sqlproj" || ext == ".wixproj" || ext == ".shproj";
		}

		// ---------------- Git 集成 ----------------

		private void RefreshGitStatus()
		{
			RefreshGitStatus(false);
		}

		private void RefreshGitStatus(bool notify)
		{
			string root = CurrentRepositoryRoot();

			if (root == null || !_settings.ShowGitStatus)
			{
				ClearGitStatus();
				if (notify) _infoLabel.Text = "当前文件夹不是 Git 仓库";
				return;
			}

			_gitStatus = GitSupport.GetStatus(root);
			ApplyGitStatus(_tree.Nodes);
			if (notify)
			{
				_infoLabel.Text = _gitStatus.Count == 0
					? "Git 工作区干净"
					: "Git 状态：" + _gitStatus.Count + " 个文件有变化";
			}
		}

		private void ApplyGitStatus(TreeNodeCollection nodes)
		{
			foreach (TreeNode node in nodes)
			{
				string path = node.Tag as string;
				if (path != null)
				{
					string code = _gitStatus.ContainsKey(path) ? _gitStatus[path] : null;
					Color color = GitSupport.StatusColor(code);
					node.ForeColor = color;
					node.ToolTipText = code == null ? "" : GitSupport.StatusText(code);
				}
				if (node.Nodes.Count > 0) ApplyGitStatus(node.Nodes);
			}
		}

		private string CurrentRepositoryRoot()
		{
			string root = GitSupport.FindRepositoryRoot(_folderPath);
			if (root == null && ActiveDoc != null && ActiveDoc.FilePath != null)
				root = GitSupport.FindRepositoryRoot(ActiveDoc.FilePath);
			return root;
		}

		private void ClearGitStatus()
		{
			_gitStatus.Clear();
			ApplyGitStatus(_tree.Nodes);
		}

		/// <summary>
		/// 把当前文件所在目录交给 VisibleCMake：在图形界面里维护 CMakeLists.txt / Makefile。
		/// 打开的如果是 CMakeLists.txt 本身，效果一样——按目录定位工程。
		/// </summary>
		private void ShowVisibleCMake()
		{
			string documentPath = ActiveDoc == null ? null : ActiveDoc.FilePath;
			string target = VisibleCMakeSupport.GuessTarget(documentPath, _folderPath);

			if (string.IsNullOrWhiteSpace(target))
			{
				MessageBox.Show(this, "还没有打开文件或文件夹，无法确定工程目录。", "可视化构建",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			VisibleCMakeSupport.OpenDesigner(this, target);
		}

		private void OpenInBetterGitGUI()
		{
			string root = CurrentRepositoryRoot();
			if (root == null)
			{
				MessageBox.Show(this, "当前文件夹不在 Git 仓库中。", "Editior",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			string exe = GitSupport.FindBetterGitGUI(_settings.BetterGitGUIPath);
			if (exe == null)
			{
				MessageBox.Show(this,
					"未找到 betterGitGUI.exe。\n请在“设置”里指定 BetterGitGUI 的路径。",
					"Editior", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			try
			{
				ProcessStartInfo info = new ProcessStartInfo(exe);
				info.Arguments = "\"" + root + "\"";
				info.UseShellExecute = true;
				Process.Start(info);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "启动 BetterGitGUI 失败：" + ex.Message, "Editior",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		// ---------------- 拖拽 ----------------

		private void OnDragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
		}

		private void OnDragDrop(object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
			string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
			if (paths == null) return;
			foreach (string path in paths)
			{
				if (Directory.Exists(path)) LoadFolder(path);
				else if (File.Exists(path)) OpenFile(path);
			}
		}
	}
}
