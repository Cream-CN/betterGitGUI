using System;
using System.Drawing;
using System.Windows.Forms;
using VisibleCMake.Model;

namespace VisibleCMake.UI
{
	/// <summary>单个构建目标的编辑面板：基本信息 + 源文件 + 编译/链接选项 + 附加代码。</summary>
	public sealed class DetailView : UserControl
	{
		private readonly StackPanel _stack;
		private readonly Label _emptyLabel;
		private readonly Label _titleLabel;
		private readonly TextBox _nameBox;
		private readonly ComboBox _kindBox;
		private readonly TextBox _outputBox;
		private readonly CheckBox _enabledBox;
		private readonly CheckBox _windowsBox;
		private readonly ListEditor _sources;
		private readonly ListEditor _headers;
		private readonly ListEditor _includes;
		private readonly ListEditor _definitions;
		private readonly ListEditor _compileOptions;
		private readonly ListEditor _linkDirectories;
		private readonly ListEditor _linkLibraries;
		private readonly TextBox _postBuildBox;
		private readonly TextBox _extraBox;

		private TargetModel _target;
		private bool _loading;

		public DetailView()
		{
			BackColor = UiKit.Surface;

			_stack = new StackPanel();
			_stack.Dock = DockStyle.Fill;

			Panel head = new Panel();
			head.Height = 52;
			head.BackColor = UiKit.Surface;

			_titleLabel = UiKit.Heading("目标");
			_titleLabel.Location = new Point(0, 0);
			Label caption = UiKit.Caption("在这里改的都是图形化配置；生成或切到预览页时，CMakeLists.txt / Makefile 会同步更新。");
			caption.Location = new Point(0, 28);

			head.Controls.Add(_titleLabel);
			head.Controls.Add(caption);

			_nameBox = UiKit.Input("");
			_kindBox = UiKit.Combo(new string[] { "可执行文件", "静态库", "共享库", "接口库" });
			_outputBox = UiKit.Input("");
			_enabledBox = UiKit.Check("参与构建（取消勾选则从生成的脚本里去掉）", true);
			_windowsBox = UiKit.Check("Windows 窗口程序（WinMain / -mwindows）", false);

			_sources = CreateList("源文件", "参与编译的 .cpp / .c 文件，路径相对工程目录", ListEditor.ValueMode.Files);
			_sources.FileFilter = "C/C++ 源文件|*.c;*.cc;*.cpp;*.cxx;*.c++|所有文件|*.*";

			_headers = CreateList("头文件", "仅用于编辑器/IDE 显示，不参与编译", ListEditor.ValueMode.Files);
			_headers.FileFilter = "C/C++ 头文件|*.h;*.hh;*.hpp;*.hxx;*.inl;*.ipp|所有文件|*.*";

			_includes = CreateList("包含目录", "等价于 -I；相对工程目录", ListEditor.ValueMode.Folders);
			_definitions = CreateList("编译定义", "例如 FOO=1、NDEBUG", ListEditor.ValueMode.Text);
			_compileOptions = CreateList("编译选项", "例如 -fno-rtti、/bigobj", ListEditor.ValueMode.Text);
			_linkDirectories = CreateList("链接目录", "等价于 -L", ListEditor.ValueMode.Folders);
			_linkLibraries = CreateList("链接库", "其它目标名、库名（如 ws2_32）或 .a/.lib 路径", ListEditor.ValueMode.Text);

			_postBuildBox = UiKit.MultilineBox("", 76);
			_extraBox = UiKit.MultilineBox("", 90);

			_stack.AddItem(head);
			_stack.AddItem(UiKit.Spacer(4));
			_stack.AddItem(UiKit.Row2("目标名称", _nameBox, "目标类型", _kindBox, 30));
			_stack.AddItem(UiKit.Row2("输出名称（可空）", _outputBox, "链接方式", _windowsBox, 28));
			_stack.AddItem(UiKit.Row("", _enabledBox, 24));
			_stack.AddItem(UiKit.Divider());
			_stack.AddItem(Section("源文件与头文件"));
			_stack.AddItem(_sources);
			_stack.AddItem(_headers);
			_stack.AddItem(Section("编译配置"));
			_stack.AddItem(_includes);
			_stack.AddItem(_definitions);
			_stack.AddItem(_compileOptions);
			_stack.AddItem(Section("链接"));
			_stack.AddItem(_linkDirectories);
			_stack.AddItem(_linkLibraries);
			_stack.AddItem(Section("扩展"));
			_stack.AddItem(UiKit.Row("构建后命令", _postBuildBox, 84));
			_stack.AddItem(UiKit.Row("附加 CMake 代码", _extraBox, 98));

			_emptyLabel = new Label();
			_emptyLabel.Dock = DockStyle.Fill;
			_emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
			_emptyLabel.Font = UiKit.HeadingFont;
			_emptyLabel.ForeColor = UiKit.Muted;
			_emptyLabel.BackColor = UiKit.Surface;
			_emptyLabel.Text = "左侧选一个目标，或点“新建目标”开始。";
			_emptyLabel.Visible = false;

			Controls.Add(_stack);
			Controls.Add(_emptyLabel);

			WireEvents();
			LoadTarget(null, null);
		}

		public event EventHandler Changed;

		public TargetModel CurrentTarget
		{
			get { return _target; }
		}

		public void LoadTarget(TargetModel target, ProjectModel project)
		{
			_loading = true;
			try
			{
				_target = target;

				string root = project == null ? "" : project.RootDirectory;
				_sources.RootDirectory = root;
				_headers.RootDirectory = root;
				_includes.RootDirectory = root;
				_linkDirectories.RootDirectory = root;
				_definitions.RootDirectory = root;
				_compileOptions.RootDirectory = root;
				_linkLibraries.RootDirectory = root;

				if (target == null)
				{
					_stack.Visible = false;
					_emptyLabel.Visible = true;
					return;
				}

				_stack.Visible = true;
				_emptyLabel.Visible = false;

				_nameBox.Text = target.Name ?? "";
				_kindBox.SelectedIndex = KindIndex(target.Kind);
				_outputBox.Text = target.OutputName ?? "";
				_enabledBox.Checked = target.Enabled;
				_windowsBox.Checked = target.WindowsSubsystem;
				_windowsBox.Enabled = target.Kind == TargetKind.Executable;

				_sources.Items = target.Sources;
				_headers.Items = target.Headers;
				_includes.Items = target.IncludeDirectories;
				_definitions.Items = target.Definitions;
				_compileOptions.Items = target.CompileOptions;
				_linkDirectories.Items = target.LinkDirectories;
				_linkLibraries.Items = target.LinkLibraries;

				_postBuildBox.Text = target.PostBuildCommands ?? "";
				_extraBox.Text = target.ExtraCode ?? "";

				UpdateTitle();
				_stack.ScrollToTop();
			}
			finally
			{
				_loading = false;
			}
		}

		/// <summary>外部改了名字（例如导入 / 树里重命名）后刷新显示。</summary>
		public void RefreshTitle()
		{
			UpdateTitle();
		}

		private static int KindIndex(TargetKind kind)
		{
			switch (kind)
			{
				case TargetKind.StaticLibrary: return 1;
				case TargetKind.SharedLibrary: return 2;
				case TargetKind.InterfaceLibrary: return 3;
				default: return 0;
			}
		}

		private void WireEvents()
		{
			_nameBox.TextChanged += delegate
			{
				if (_loading || _target == null) return;
				_target.Name = _nameBox.Text;
				UpdateTitle();
				RaiseChanged();
			};

			_outputBox.TextChanged += delegate
			{
				if (_loading || _target == null) return;
				_target.OutputName = _outputBox.Text;
				RaiseChanged();
			};

			_kindBox.SelectedIndexChanged += delegate
			{
				if (_loading || _target == null || _kindBox.SelectedIndex < 0) return;
				_target.Kind = (TargetKind)_kindBox.SelectedIndex;
				bool isExecutable = _target.Kind == TargetKind.Executable;
				_windowsBox.Enabled = isExecutable;
				if (!isExecutable && _windowsBox.Checked)
				{
					_windowsBox.Checked = false;
					_target.WindowsSubsystem = false;
				}
				UpdateTitle();
				RaiseChanged();
			};

			_enabledBox.CheckedChanged += delegate
			{
				if (_loading || _target == null) return;
				_target.Enabled = _enabledBox.Checked;
				RaiseChanged();
			};

			_windowsBox.CheckedChanged += delegate
			{
				if (_loading || _target == null) return;
				_target.WindowsSubsystem = _windowsBox.Checked;
				RaiseChanged();
			};

			_postBuildBox.TextChanged += delegate
			{
				if (_loading || _target == null) return;
				_target.PostBuildCommands = _postBuildBox.Text;
				RaiseChanged();
			};

			_extraBox.TextChanged += delegate
			{
				if (_loading || _target == null) return;
				_target.ExtraCode = _extraBox.Text;
				RaiseChanged();
			};

			EventHandler listChanged = delegate
			{
				if (_loading || _target == null) return;
				RaiseChanged();
			};
			_sources.Changed += listChanged;
			_headers.Changed += listChanged;
			_includes.Changed += listChanged;
			_definitions.Changed += listChanged;
			_compileOptions.Changed += listChanged;
			_linkDirectories.Changed += listChanged;
			_linkLibraries.Changed += listChanged;
		}

		private void UpdateTitle()
		{
			if (_target == null)
			{
				_titleLabel.Text = "目标";
				return;
			}
			string name = string.IsNullOrWhiteSpace(_target.Name) ? "（未命名）" : _target.Name;
			_titleLabel.Text = "目标：" + name + "　·　" + _target.KindText;
		}

		private void RaiseChanged()
		{
			if (Changed != null) Changed(this, EventArgs.Empty);
		}

		private ListEditor CreateList(string title, string hint, ListEditor.ValueMode mode)
		{
			ListEditor editor = new ListEditor();
			editor.Title = title;
			editor.Hint = hint;
			editor.Mode = mode;
			return editor;
		}

		private Control Section(string title)
		{
			Panel panel = new Panel();
			panel.Height = 30;
			panel.BackColor = UiKit.Surface;

			Label label = new Label();
			label.Text = title;
			label.AutoSize = true;
			label.Font = new Font(UiKit.UiFont.FontFamily, 10f, FontStyle.Bold);
			label.ForeColor = UiKit.TextColor;
			label.BackColor = Color.Transparent;
			label.Location = new Point(0, 6);
			panel.Controls.Add(label);

			Panel line = new Panel();
			line.BackColor = UiKit.Border;
			line.Height = 1;
			line.SetBounds(0, 28, 900, 1);
			line.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
			panel.Controls.Add(line);

			return panel;
		}
	}
}
