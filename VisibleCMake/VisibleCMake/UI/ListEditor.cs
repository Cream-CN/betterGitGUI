using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using VisibleCMake.Model;

namespace VisibleCMake.UI
{
	/// <summary>
	/// 可复用的“字符串列表编辑器”：标题 + 列表 + 添加/编辑/删除/上移/下移。
	/// 列表直接操作模型里的 List&lt;string&gt;，改完就生效，不需要再点“应用”。
	/// </summary>
	public sealed class ListEditor : UserControl
	{
		public enum ValueMode
		{
			/// <summary>手工输入任意文本（例如编译定义 FOO=1）。</summary>
			Text,
			/// <summary>通过文件对话框选择文件，自动转成相对工程路径。</summary>
			Files,
			/// <summary>通过文件夹对话框选择目录。</summary>
			Folders
		}

		private readonly Label _titleLabel;
		private readonly Label _hintLabel;
		private readonly ListBox _list;
		private readonly Button _addButton;
		private readonly Button _editButton;
		private readonly Button _removeButton;
		private readonly Button _upButton;
		private readonly Button _downButton;

		private List<string> _items = new List<string>();
		private string _title = "列表";
		private string _hint = "";
		private ValueMode _mode = ValueMode.Text;
		private string _rootDirectory = "";
		private string _fileFilter = "所有文件|*.*";

		private bool _refreshing;

		public ListEditor()
		{
			Height = 160;
			BackColor = UiKit.Surface;

			Panel header = new Panel();
			header.Dock = DockStyle.Top;
			header.Height = 38;
			header.BackColor = UiKit.Surface;

			_titleLabel = new Label();
			_titleLabel.AutoSize = true;
			_titleLabel.Text = _title;
			_titleLabel.Font = new Font(UiKit.UiFont.FontFamily, 9f, FontStyle.Bold);
			_titleLabel.ForeColor = UiKit.TextColor;
			_titleLabel.BackColor = Color.Transparent;
			_titleLabel.Location = new Point(0, 2);

			_hintLabel = new Label();
			_hintLabel.AutoSize = true;
			_hintLabel.Text = _hint;
			_hintLabel.Font = UiKit.SmallFont;
			_hintLabel.ForeColor = UiKit.Muted;
			_hintLabel.BackColor = Color.Transparent;
			_hintLabel.Location = new Point(0, 21);

			header.Controls.Add(_titleLabel);
			header.Controls.Add(_hintLabel);

			_list = new ListBox();
			_list.Dock = DockStyle.Fill;
			_list.Font = UiKit.UiFont;
			_list.BorderStyle = BorderStyle.FixedSingle;
			_list.IntegralHeight = false;
			_list.SelectionMode = SelectionMode.MultiExtended;
			_list.HorizontalScrollbar = true;
			_list.Margin = new Padding(0);
			_list.DoubleClick += delegate { EditSelected(); };
			_list.KeyDown += OnListKeyDown;

			FlowLayoutPanel buttons = new FlowLayoutPanel();
			buttons.Dock = DockStyle.Right;
			buttons.Width = 92;
			buttons.FlowDirection = FlowDirection.TopDown;
			buttons.WrapContents = false;
			buttons.BackColor = UiKit.Surface;
			buttons.Padding = new Padding(6, 2, 0, 0);

			_addButton = SmallButton("添加…", delegate { AddClicked(); });
			_editButton = SmallButton("编辑…", delegate { EditSelected(); });
			_removeButton = SmallButton("删除", delegate { RemoveSelected(); });
			_upButton = SmallButton("上移", delegate { MoveSelected(-1); });
			_downButton = SmallButton("下移", delegate { MoveSelected(1); });

			buttons.Controls.Add(_addButton);
			buttons.Controls.Add(_editButton);
			buttons.Controls.Add(_removeButton);
			buttons.Controls.Add(_upButton);
			buttons.Controls.Add(_downButton);

			Controls.Add(_list);
			Controls.Add(buttons);
			Controls.Add(header);
		}

		public event EventHandler Changed;

		public string Title
		{
			get { return _title; }
			set { _title = value ?? ""; _titleLabel.Text = _title; }
		}

		public string Hint
		{
			get { return _hint; }
			set { _hint = value ?? ""; _hintLabel.Text = _hint; }
		}

		public ValueMode Mode
		{
			get { return _mode; }
			set { _mode = value; }
		}

		public string RootDirectory
		{
			get { return _rootDirectory; }
			set { _rootDirectory = value ?? ""; }
		}

		public string FileFilter
		{
			get { return _fileFilter; }
			set { _fileFilter = string.IsNullOrEmpty(value) ? "所有文件|*.*" : value; }
		}

		/// <summary>直接引用模型里的 List，界面上的增删改会立刻反映到工程数据上。</summary>
		public List<string> Items
		{
			get { return _items; }
			set
			{
				_items = value ?? new List<string>();
				RefreshList();
			}
		}

		/// <summary>列表里显示的条目（只读副本），供宿主做额外处理。</summary>
		public IList<string> DisplayItems
		{
			get { return _items.ToArray(); }
		}

		private Button SmallButton(string text, EventHandler handler)
		{
			Button button = new Button();
			button.Text = text;
			button.Font = UiKit.SmallFont;
			button.FlatStyle = FlatStyle.Flat;
			button.BackColor = UiKit.Surface;
			button.ForeColor = UiKit.TextColor;
			button.FlatAppearance.BorderSize = 1;
			button.FlatAppearance.BorderColor = UiKit.Border;
			button.Cursor = Cursors.Hand;
			button.Size = new Size(78, 21);
			button.Margin = new Padding(0, 0, 0, 3);
			button.UseVisualStyleBackColor = false;
			if (handler != null) button.Click += handler;
			return button;
		}

		private void RefreshList()
		{
			_refreshing = true;
			try
			{
				int selected = _list.SelectedIndex;
				_list.BeginUpdate();
				_list.Items.Clear();
				foreach (string item in _items) _list.Items.Add(item);
				_list.EndUpdate();
				if (selected >= 0 && selected < _list.Items.Count) _list.SelectedIndex = selected;
			}
			finally
			{
				_refreshing = false;
			}
		}

		private void AddClicked()
		{
			if (_mode == ValueMode.Files)
			{
				using (OpenFileDialog dialog = new OpenFileDialog())
				{
					dialog.Multiselect = true;
					dialog.Filter = _fileFilter;
					dialog.Title = _title;
					if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

					bool changed = false;
					foreach (string file in dialog.FileNames)
					{
						string item = ToProjectPath(file);
						if (!_items.Contains(item)) { _items.Add(item); changed = true; }
					}
					if (changed) { RefreshList(); RaiseChanged(); }
				}
				return;
			}

			if (_mode == ValueMode.Folders)
			{
				using (FolderBrowserDialog dialog = new FolderBrowserDialog())
				{
					dialog.Description = string.IsNullOrEmpty(_hint) ? _title : _hint;
					dialog.ShowNewFolderButton = true;
					if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

					string item = ToProjectPath(dialog.SelectedPath);
					if (!_items.Contains(item)) { _items.Add(item); RefreshList(); RaiseChanged(); }
				}
				return;
			}

			string value = UiKit.AskText(FindForm(), "添加条目", _title + "：", "");
			if (value == null) return;
			value = value.Trim();
			if (value.Length == 0) return;

			_items.Add(value);
			RefreshList();
			_list.SelectedIndex = _list.Items.Count - 1;
			RaiseChanged();
		}

		private void EditSelected()
		{
			if (_refreshing) return;
			int index = _list.SelectedIndex;
			if (index < 0 || index >= _items.Count) return;

			string value = UiKit.AskText(FindForm(), "编辑条目", _title + "：", _items[index]);
			if (value == null) return;
			value = value.Trim();
			if (value.Length == 0) return;

			_items[index] = value;
			RefreshList();
			_list.SelectedIndex = index;
			RaiseChanged();
		}

		private void RemoveSelected()
		{
			if (_list.SelectedIndices.Count == 0) return;

			List<int> indices = new List<int>();
			foreach (int index in _list.SelectedIndices) indices.Add(index);
			indices.Sort();

			for (int i = indices.Count - 1; i >= 0; i--)
			{
				int index = indices[i];
				if (index >= 0 && index < _items.Count) _items.RemoveAt(index);
			}

			RefreshList();
			RaiseChanged();
		}

		private void MoveSelected(int direction)
		{
			int index = _list.SelectedIndex;
			if (index < 0) return;

			int target = index + direction;
			if (target < 0 || target >= _items.Count) return;

			string value = _items[index];
			_items[index] = _items[target];
			_items[target] = value;

			RefreshList();
			_list.SelectedIndex = target;
			RaiseChanged();
		}

		private void OnListKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Delete) { RemoveSelected(); e.Handled = true; }
			else if (e.KeyCode == Keys.Insert) { AddClicked(); e.Handled = true; }
		}

		private string ToProjectPath(string absolutePath)
		{
			if (string.IsNullOrWhiteSpace(absolutePath)) return "";
			if (string.IsNullOrWhiteSpace(_rootDirectory)) return PathUtil.ToSlashes(absolutePath);
			return PathUtil.Relative(_rootDirectory, absolutePath);
		}

		private void RaiseChanged()
		{
			if (Changed != null) Changed(this, EventArgs.Empty);
		}
	}
}
