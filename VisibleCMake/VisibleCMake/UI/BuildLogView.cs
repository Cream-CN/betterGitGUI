using System;
using System.Drawing;
using System.Windows.Forms;

namespace VisibleCMake.UI
{
	/// <summary>构建输出面板：深色等宽字体，按行着色，附带清空 / 复制 / 停止。</summary>
	public sealed class BuildLogView : UserControl
	{
		private readonly RichTextBox _log;
		private readonly Label _caption;
		private readonly Button _cancelButton;
		private readonly Button _clearButton;

		public event EventHandler CancelRequested;

		public BuildLogView()
		{
			BackColor = UiKit.Surface;

			Panel header = new Panel();
			header.Dock = DockStyle.Top;
			header.Height = 38;
			header.BackColor = UiKit.Surface;

			_caption = new Label();
			_caption.AutoSize = true;
			_caption.Location = new Point(2, 11);
			_caption.Font = UiKit.SmallFont;
			_caption.ForeColor = UiKit.Muted;
			_caption.BackColor = Color.Transparent;
			_caption.Text = "构建输出（cmake / make 的原始输出都会显示在这里）";

			Button copy = SmallButton("复制全部", delegate { CopyAll(); });
			_clearButton = SmallButton("清空", delegate { Clear(); });
			_cancelButton = SmallButton("停止", delegate
			{
				if (CancelRequested != null) CancelRequested(this, EventArgs.Empty);
			});
			_cancelButton.Enabled = false;

			FlowLayoutPanel bar = new FlowLayoutPanel();
			bar.Dock = DockStyle.Right;
			bar.AutoSize = true;
			bar.WrapContents = false;
			bar.FlowDirection = FlowDirection.LeftToRight;
			bar.BackColor = UiKit.Surface;
			bar.Padding = new Padding(0, 5, 4, 0);
			bar.Controls.Add(copy);
			bar.Controls.Add(_clearButton);
			bar.Controls.Add(_cancelButton);

			header.Controls.Add(_caption);
			header.Controls.Add(bar);

			_log = new RichTextBox();
			_log.Dock = DockStyle.Fill;
			_log.ReadOnly = true;
			_log.Font = UiKit.MonoFont;
			_log.WordWrap = false;
			_log.DetectUrls = false;
			_log.BorderStyle = BorderStyle.None;
			_log.BackColor = UiKit.LogBack;
			_log.ForeColor = UiKit.LogFore;
			_log.ScrollBars = RichTextBoxScrollBars.Both;
			_log.HideSelection = false;

			Controls.Add(_log);
			Controls.Add(header);
		}

		public void Clear()
		{
			if (IsDisposed) return;
			_log.Clear();
		}

		public void SetRunning(bool running)
		{
			if (IsDisposed) return;
			_cancelButton.Enabled = running;
		}

		/// <summary>追加一行日志；按内容自动着色。</summary>
		public void Append(string line)
		{
			if (IsDisposed) return;
			WriteLine(line ?? "", ColorFor(line));
		}

		/// <summary>阶段标题，例如“==> CMake 配置”。</summary>
		public void Banner(string text)
		{
			if (IsDisposed) return;
			if (_log.TextLength > 0) WriteLine("", UiKit.LogFore);
			WriteLine(text, Color.FromArgb(130, 180, 255));
		}

		private Color ColorFor(string line)
		{
			if (string.IsNullOrEmpty(line)) return UiKit.LogFore;

			string lower = line.ToLowerInvariant();
			if (lower.IndexOf("error", StringComparison.Ordinal) >= 0
				|| line.IndexOf("错误", StringComparison.Ordinal) >= 0
				|| lower.IndexOf("failed", StringComparison.Ordinal) >= 0
				|| line.IndexOf("失败", StringComparison.Ordinal) >= 0)
			{
				return Color.FromArgb(255, 125, 125);
			}
			if (lower.IndexOf("warning", StringComparison.Ordinal) >= 0
				|| line.IndexOf("警告", StringComparison.Ordinal) >= 0)
			{
				return Color.FromArgb(242, 200, 120);
			}
			if (line.StartsWith("==>", StringComparison.Ordinal)) return Color.FromArgb(130, 180, 255);
			if (line.StartsWith("   ", StringComparison.Ordinal) && line.IndexOf('/') < 0) return Color.FromArgb(180, 200, 190);
			return UiKit.LogFore;
		}

		private void WriteLine(string text, Color color)
		{
			int start = _log.TextLength;
			_log.AppendText(text + "\n");
			_log.Select(start, text.Length);
			_log.SelectionColor = color;
			_log.Select(_log.TextLength, 0);
			_log.ScrollToCaret();
		}

		private void CopyAll()
		{
			try
			{
				if (_log.TextLength > 0) Clipboard.SetText(_log.Text);
			}
			catch { }
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
			button.UseVisualStyleBackColor = false;
			button.AutoSize = false;
			Size size = TextRenderer.MeasureText(text, UiKit.SmallFont);
			button.Size = new Size(Math.Max(52, size.Width + 18), 26);
			button.Margin = new Padding(0, 0, 6, 0);
			if (handler != null) button.Click += handler;
			return button;
		}
	}
}
