using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VisibleCMake.UI
{
	/// <summary>统一的颜色、字体和小控件工厂，避免每个窗体各写一套样式。</summary>
	public static class UiKit
	{
		public static readonly Color Background = Color.FromArgb(244, 246, 249);
		public static readonly Color Surface = Color.White;
		public static readonly Color Border = Color.FromArgb(215, 220, 227);
		public static readonly Color TextColor = Color.FromArgb(28, 32, 38);
		public static readonly Color Muted = Color.FromArgb(107, 115, 128);
		public static readonly Color Accent = Color.FromArgb(37, 99, 235);
		public static readonly Color AccentDark = Color.FromArgb(29, 78, 216);
		public static readonly Color Danger = Color.FromArgb(192, 48, 48);
		public static readonly Color Success = Color.FromArgb(22, 140, 90);
		public static readonly Color LogBack = Color.FromArgb(22, 25, 31);
		public static readonly Color LogFore = Color.FromArgb(215, 221, 229);

		public static readonly Font UiFont = new Font("Microsoft YaHei UI", 9f);
		public static readonly Font HeadingFont = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold);
		public static readonly Font SmallFont = new Font("Microsoft YaHei UI", 8.25f);
		public static readonly Font MonoFont = new Font("Consolas", 9.5f);

		// ---------------- 标签 ----------------

		public static Label Heading(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.Font = HeadingFont;
			label.ForeColor = TextColor;
			label.AutoSize = true;
			label.BackColor = Color.Transparent;
			return label;
		}

		public static Label Caption(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.Font = SmallFont;
			label.ForeColor = Muted;
			label.AutoSize = true;
			label.BackColor = Color.Transparent;
			return label;
		}

		public static Label FieldLabel(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.Font = UiFont;
			label.ForeColor = TextColor;
			label.AutoSize = false;
			label.TextAlign = ContentAlignment.MiddleLeft;
			label.BackColor = Color.Transparent;
			return label;
		}

		// ---------------- 按钮 ----------------

		public static Button Primary(string text, EventHandler onClick)
		{
			Button button = BaseButton(text, onClick);
			button.BackColor = Accent;
			button.ForeColor = Color.White;
			button.FlatAppearance.BorderSize = 0;
			button.FlatAppearance.MouseOverBackColor = AccentDark;
			button.FlatAppearance.MouseDownBackColor = AccentDark;
			return button;
		}

		public static Button Secondary(string text, EventHandler onClick)
		{
			Button button = BaseButton(text, onClick);
			button.BackColor = Surface;
			button.ForeColor = TextColor;
			button.FlatAppearance.BorderSize = 1;
			button.FlatAppearance.BorderColor = Border;
			button.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 243, 248);
			return button;
		}

		public static Button DangerButton(string text, EventHandler onClick)
		{
			Button button = BaseButton(text, onClick);
			button.BackColor = Surface;
			button.ForeColor = Danger;
			button.FlatAppearance.BorderSize = 1;
			button.FlatAppearance.BorderColor = Color.FromArgb(226, 196, 196);
			button.FlatAppearance.MouseOverBackColor = Color.FromArgb(252, 240, 240);
			return button;
		}

		private static Button BaseButton(string text, EventHandler onClick)
		{
			Button button = new Button();
			button.Text = text;
			button.Font = UiFont;
			button.FlatStyle = FlatStyle.Flat;
			button.Cursor = Cursors.Hand;
			button.UseVisualStyleBackColor = false;
			button.AutoSize = false;
			button.TabStop = false;

			Size size = TextRenderer.MeasureText(text, UiFont);
			button.Width = Math.Max(64, size.Width + 26);
			button.Height = Math.Max(26, size.Height + 12);

			if (onClick != null) button.Click += onClick;
			return button;
		}

		/// <summary>把一组按钮按从左到右排好，供工具条 / 底部操作区使用。</summary>
		public static FlowLayoutPanel ButtonBar(params Control[] buttons)
		{
			FlowLayoutPanel bar = new FlowLayoutPanel();
			bar.FlowDirection = FlowDirection.LeftToRight;
			bar.WrapContents = false;
			bar.AutoSize = true;
			bar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			bar.BackColor = Color.Transparent;
			bar.Margin = new Padding(0);
			if (buttons != null)
			{
				foreach (Control button in buttons)
				{
					button.Margin = new Padding(0, 0, 8, 0);
					bar.Controls.Add(button);
				}
			}
			return bar;
		}

		// ---------------- 输入控件 ----------------

		public static TextBox Input(string text)
		{
			TextBox box = new TextBox();
			box.Text = text ?? "";
			box.Font = UiFont;
			box.BorderStyle = BorderStyle.FixedSingle;
			box.BackColor = Surface;
			box.ForeColor = TextColor;
			return box;
		}

		public static TextBox MultilineBox(string text, int height)
		{
			TextBox box = Input(text);
			box.Multiline = true;
			box.ScrollBars = ScrollBars.Vertical;
			box.AcceptsReturn = true;
			box.WordWrap = false;
			box.Font = MonoFont;
			box.Height = height;
			return box;
		}

		public static ComboBox Combo(string[] items)
		{
			ComboBox combo = new ComboBox();
			combo.DropDownStyle = ComboBoxStyle.DropDownList;
			combo.Font = UiFont;
			combo.BackColor = Surface;
			combo.ForeColor = TextColor;
			if (items != null)
			{
				foreach (string item in items) combo.Items.Add(item);
			}
			if (combo.Items.Count > 0) combo.SelectedIndex = 0;
			return combo;
		}

		public static CheckBox Check(string text, bool value)
		{
			CheckBox check = new CheckBox();
			check.Text = text;
			check.Checked = value;
			check.Font = UiFont;
			check.ForeColor = TextColor;
			check.AutoSize = true;
			check.BackColor = Color.Transparent;
			return check;
		}

		/// <summary>一行：左侧说明文字 + 右侧填满的编辑控件。</summary>
		public static Control Row(string label, Control editor, int height)
		{
			TableLayoutPanel row = new TableLayoutPanel();
			row.ColumnCount = 2;
			row.RowCount = 1;
			row.Dock = DockStyle.Top;
			row.BackColor = Surface;
			row.Height = height;
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

			Label caption = FieldLabel(label);
			caption.Dock = DockStyle.Fill;
			caption.Margin = new Padding(0, 0, 8, 0);
			editor.Dock = DockStyle.Fill;
			editor.Margin = new Padding(0, 2, 0, 2);

			row.Controls.Add(caption, 0, 0);
			row.Controls.Add(editor, 1, 0);
			return row;
		}

		/// <summary>一行两列：说明 + 控件 + 说明 + 控件。</summary>
		public static Control Row2(string label1, Control editor1, string label2, Control editor2, int height)
		{
			TableLayoutPanel row = new TableLayoutPanel();
			row.ColumnCount = 4;
			row.RowCount = 1;
			row.Dock = DockStyle.Top;
			row.BackColor = Surface;
			row.Height = height;
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
			row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
			row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

			Label caption1 = FieldLabel(label1);
			caption1.Dock = DockStyle.Fill;
			caption1.Margin = new Padding(0, 0, 8, 0);
			editor1.Dock = DockStyle.Fill;
			editor1.Margin = new Padding(0, 2, 8, 2);

			Label caption2 = FieldLabel(label2);
			caption2.Dock = DockStyle.Fill;
			caption2.Margin = new Padding(0, 0, 8, 0);
			editor2.Dock = DockStyle.Fill;
			editor2.Margin = new Padding(0, 2, 0, 2);

			row.Controls.Add(caption1, 0, 0);
			row.Controls.Add(editor1, 1, 0);
			row.Controls.Add(caption2, 2, 0);
			row.Controls.Add(editor2, 3, 0);
			return row;
		}

		public static Control Spacer(int height)
		{
			Panel spacer = new Panel();
			spacer.Height = height;
			spacer.BackColor = Color.Transparent;
			spacer.Margin = new Padding(0);
			return spacer;
		}

		public static Control Divider()
		{
			Panel divider = new Panel();
			divider.Height = 1;
			divider.BackColor = Border;
			divider.Margin = new Padding(0);
			return divider;
		}

		/// <summary>小节标题：加粗标题 + 一条分隔线。</summary>
		public static Control Section(string title)
		{
			Panel panel = new Panel();
			panel.Height = 30;
			panel.BackColor = Surface;

			Label label = new Label();
			label.Text = title;
			label.AutoSize = true;
			label.Font = new Font(UiFont.FontFamily, 10f, FontStyle.Bold);
			label.ForeColor = TextColor;
			label.BackColor = Color.Transparent;
			label.Location = new Point(0, 6);

			Panel line = new Panel();
			line.BackColor = Border;
			line.Height = 1;
			line.SetBounds(0, 28, 800, 1);
			line.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

			panel.Controls.Add(label);
			panel.Controls.Add(line);
			return panel;
		}

		// ---------------- 对话框 ----------------

		/// <summary>单行文本输入框，返回 null 表示用户取消。</summary>
		public static string AskText(IWin32Window owner, string title, string message, string initialValue)
		{
			using (Form form = new Form())
			{
				form.Text = title;
				form.Font = UiFont;
				form.BackColor = Surface;
				form.FormBorderStyle = FormBorderStyle.FixedDialog;
				form.StartPosition = FormStartPosition.CenterParent;
				form.MinimizeBox = false;
				form.MaximizeBox = false;
				form.ShowInTaskbar = false;
				form.ClientSize = new Size(520, 132);

				Label label = FieldLabel(message);
				label.SetBounds(16, 16, 480, 22);

				TextBox box = Input(initialValue);
				box.SetBounds(16, 44, 488, 24);

				Button ok = Primary("确定", null);
				ok.SetBounds(344, 84, 76, 28);
				ok.DialogResult = DialogResult.OK;

				Button cancel = Secondary("取消", null);
				cancel.SetBounds(428, 84, 76, 28);
				cancel.DialogResult = DialogResult.Cancel;

				form.Controls.Add(label);
				form.Controls.Add(box);
				form.Controls.Add(ok);
				form.Controls.Add(cancel);
				form.AcceptButton = ok;
				form.CancelButton = cancel;

				return form.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
			}
		}

		public static bool Confirm(IWin32Window owner, string message, string title)
		{
			return MessageBox.Show(owner, message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
				== DialogResult.OK;
		}
	}

	/// <summary>
	/// 竖向排列的容器：子控件自动占满宽度、按顺序往下排，内容超出时自动出现滚动条。
	/// 比 Dock=Top 的链式布局稳定，也不会因为滚动条出现而错位。
	/// </summary>
	public sealed class StackPanel : Panel
	{
		private readonly List<Control> _items = new List<Control>();
		private readonly VScrollBar _bar;
		private int _gap = 8;
		private int _offset;
		private int _contentHeight;
		private bool _relayouting;

		public StackPanel()
		{
			BackColor = UiKit.Surface;
			Padding = new Padding(16);

			_bar = new VScrollBar();
			_bar.Dock = DockStyle.Right;
			_bar.Visible = false;
			_bar.SmallChange = 40;
			_bar.Scroll += delegate { SetOffset(_bar.Value); };
			Controls.Add(_bar);
		}

		public int Gap
		{
			get { return _gap; }
			set { _gap = Math.Max(0, value); Relayout(); }
		}

		public void AddItem(Control control)
		{
			// 子控件自己可能带着 Dock=Top，会绕开这里的定位，所以统一清掉
			control.Dock = DockStyle.None;
			control.Margin = new Padding(0);
			control.SizeChanged += ChildResized;
			_items.Add(control);
			Controls.Add(control);

			Relayout();
			// 有些控件（表格行、多行文本框）要等一句消息循环之后才有真实高度
			if (IsHandleCreated) BeginInvoke(new Action(Relayout));
		}

		public void ClearItems()
		{
			foreach (Control control in _items) Controls.Remove(control);
			_items.Clear();
			_contentHeight = 0;
			SetOffset(0);
			Relayout();
		}

		public void ScrollToTop()
		{
			SetOffset(0);
		}

		public void Relayout()
		{
			if (_relayouting || _items.Count == 0) return;

			_relayouting = true;
			SuspendLayout();
			try
			{
				int available = ClientSize.Width - Padding.Horizontal;
				if (available < 40) available = 40;

				// 先按满宽量高度，判断要不要竖向滚动条；要的话把滚动条宽度让出来
				int fullHeight = PendingHeight(available);
				bool needBar = fullHeight > ClientSize.Height;

				int width = available - (needBar ? _bar.Width : 0);
				if (width < 40) width = 40;

				int y = Padding.Top;
				foreach (Control control in _items)
				{
					int height = MeasureHeight(control, width);
					control.SetBounds(Padding.Left, y - _offset, width, height);
					y += height + _gap;
				}

				_contentHeight = y + Padding.Bottom;
				UpdateScrollBar(needBar);
			}
			finally
			{
				_relayouting = false;
				ResumeLayout(true);
			}
		}

		private void UpdateScrollBar(bool needBar)
		{
			int max = Math.Max(0, _contentHeight - ClientSize.Height);
			if (_offset > max) _offset = max;

			if (_bar.Visible != needBar) _bar.Visible = needBar;

			// WinForms 的滚动条按 (Maximum - LargeChange) 计算可滚动范围
			_bar.LargeChange = Math.Max(1, ClientSize.Height);
			_bar.Maximum = max + _bar.LargeChange - 1;
			_bar.Value = Math.Min(_offset, Math.Max(0, _bar.Maximum - _bar.LargeChange + 1));
		}

		private void SetOffset(int value)
		{
			if (_offset == value) return;

			_offset = Math.Max(0, value);
			_relayouting = true;
			SuspendLayout();
			try
			{
				int y = Padding.Top;
				foreach (Control control in _items)
				{
					control.Top = y - _offset;
					y += control.Height + _gap;
				}
			}
			finally
			{
				_relayouting = false;
				ResumeLayout(true);
			}
		}

		private int PendingHeight(int width)
		{
			int total = Padding.Vertical;
			foreach (Control control in _items) total += MeasureHeight(control, width) + _gap;
			return total;
		}

		private static int MeasureHeight(Control control, int width)
		{
			int height = control.Height;

			if (control.AutoSize || height <= 0)
			{
				Size preferred = control.GetPreferredSize(new Size(width, 0));
				if (preferred.Height > 0) height = preferred.Height;
			}
			if (height <= 0) height = 24;
			return height;
		}

		private void ChildResized(object sender, EventArgs e)
		{
			if (_relayouting) return;
			Relayout();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			Relayout();
		}

		protected override void OnControlAdded(ControlEventArgs e)
		{
			base.OnControlAdded(e);
			if (!_relayouting && e.Control != _bar) Relayout();
		}

		protected override void OnControlRemoved(ControlEventArgs e)
		{
			base.OnControlRemoved(e);
			if (!_relayouting && e.Control != _bar) Relayout();
		}

		protected override void OnVisibleChanged(EventArgs e)
		{
			base.OnVisibleChanged(e);
			if (Visible && !_relayouting) Relayout();
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			if (!_bar.Visible)
			{
				base.OnMouseWheel(e);
				return;
			}

			int max = Math.Max(0, _contentHeight - ClientSize.Height);
			int next = Math.Max(0, Math.Min(max, _offset - e.Delta / 120 * 60));
			SetOffset(next);
			_bar.Value = next;

			base.OnMouseWheel(e);
		}
	}
}
