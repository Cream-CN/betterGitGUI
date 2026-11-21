using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using VisibleCMake.Runtime;

namespace VisibleCMake.UI
{
	/// <summary>只读的代码预览：显示生成的 CMakeLists.txt / Makefile，带简单着色和常用动作。</summary>
	public sealed class CodePreview : UserControl
	{
		private readonly RichTextBox _text;
		private readonly Label _caption;
		private readonly Button _openButton;

		private string _filePath = "";

		public event EventHandler RefreshRequested;

		public CodePreview()
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
			_caption.Text = "尚未生成";

			Button refresh = SmallButton("刷新", delegate
			{
				if (RefreshRequested != null) RefreshRequested(this, EventArgs.Empty);
			});
			Button copy = SmallButton("复制", delegate { CopyAll(); });
			Button save = SmallButton("另存为…", delegate { SaveAs(); });
			_openButton = SmallButton("在编辑器中打开", delegate
			{
				EditiorBridge.OpenInEditior(FindForm(), _filePath);
			});

			FlowLayoutPanel bar = new FlowLayoutPanel();
			bar.Dock = DockStyle.Right;
			bar.AutoSize = true;
			bar.WrapContents = false;
			bar.FlowDirection = FlowDirection.LeftToRight;
			bar.BackColor = UiKit.Surface;
			bar.Padding = new Padding(0, 5, 4, 0);
			bar.Controls.Add(refresh);
			bar.Controls.Add(copy);
			bar.Controls.Add(save);
			bar.Controls.Add(_openButton);

			header.Controls.Add(_caption);
			header.Controls.Add(bar);

			_text = new RichTextBox();
			_text.Dock = DockStyle.Fill;
			_text.ReadOnly = true;
			_text.Font = UiKit.MonoFont;
			_text.WordWrap = false;
			_text.DetectUrls = false;
			_text.HideSelection = false;
			_text.BorderStyle = BorderStyle.None;
			_text.BackColor = UiKit.Surface;
			_text.ForeColor = UiKit.TextColor;
			_text.ScrollBars = RichTextBoxScrollBars.Both;

			Controls.Add(_text);
			Controls.Add(header);
		}

		public string FilePath
		{
			get { return _filePath; }
		}

		public string TextValue
		{
			get { return _text.Text; }
		}

		public void SetText(string content, string filePath)
		{
			_filePath = filePath ?? "";

			_text.SuspendLayout();
			try
			{
				_text.Text = content ?? "";
				Colorize();
				_text.Select(0, 0);
			}
			finally
			{
				_text.ResumeLayout();
			}

			string name = string.IsNullOrEmpty(_filePath) ? "（未命名）" : Path.GetFileName(_filePath);
			_caption.Text = name + "　·　" + _text.Lines.Length + " 行　·　预览，保存工程后写入磁盘";
			_openButton.Enabled = !string.IsNullOrEmpty(_filePath);
		}

		private void Colorize()
		{
			int lineCount = _text.Lines.Length;
			if (lineCount == 0) return;

			_text.SuspendLayout();
			try
			{
				_text.SelectAll();
				_text.SelectionColor = UiKit.TextColor;

				for (int i = 0; i < lineCount; i++)
				{
					string line = _text.Lines[i];
					if (line.Length == 0) continue;
					int start = _text.GetFirstCharIndexFromLine(i);
					if (start < 0) continue;

					string trimmed = line.TrimStart();
					int indent = line.Length - trimmed.Length;

					if (trimmed.StartsWith("#", StringComparison.Ordinal))
					{
						_text.Select(start, line.Length);
						_text.SelectionColor = Color.FromArgb(106, 153, 85);
						continue;
					}

					// 命令 / 目标名的首词着色
					int tokenLength = 0;
					while (tokenLength < trimmed.Length)
					{
						char c = trimmed[tokenLength];
						if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') tokenLength++;
						else break;
					}

					if (tokenLength > 0)
					{
						_text.Select(start + indent, tokenLength);
						_text.SelectionColor = Color.FromArgb(37, 99, 235);
					}
				}

				_text.Select(0, 0);
				_text.SelectionColor = UiKit.TextColor;
			}
			finally
			{
				_text.ResumeLayout();
			}
		}

		private void CopyAll()
		{
			try
			{
				if (_text.TextLength > 0) Clipboard.SetText(_text.Text);
			}
			catch { }
		}

		private void SaveAs()
		{
			using (SaveFileDialog dialog = new SaveFileDialog())
			{
				dialog.Title = "保存为";
				dialog.FileName = string.IsNullOrEmpty(_filePath) ? "CMakeLists.txt" : Path.GetFileName(_filePath);
				dialog.Filter = "所有文件|*.*";
				if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;

				try
				{
					File.WriteAllText(dialog.FileName, _text.Text, new UTF8Encoding(false));
					MessageBox.Show(FindForm(), "已保存：\n" + dialog.FileName, "可见构建",
						MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				catch (Exception ex)
				{
					MessageBox.Show(FindForm(), "保存失败：" + ex.Message, "可见构建",
						MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
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
