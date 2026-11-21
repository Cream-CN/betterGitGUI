using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>设置窗口：字体、字号、缩进、鸟瞰图、会话恢复、Git 状态与 BetterGitGUI 路径。</summary>
	public class SettingsForm : Form
	{
		private ComboBox _fontBox;
		private NumericUpDown _sizeBox;
		private NumericUpDown _tabBox;
		private CheckBox _wrapBox;
		private CheckBox _minimapBox;
		private NumericUpDown _minimapWidthBox;
		private CheckBox _sessionBox;
		private CheckBox _gitBox;
		private TextBox _pathBox;

		public SettingsForm(EditorSettings settings)
		{
			Text = "设置";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			ClientSize = new Size(470, 430);
			Font = new Font("Microsoft YaHei UI", 9f);

			int y = 16;
			AddLabel("编辑器字体：", 16, y);
			_fontBox = new ComboBox();
			_fontBox.DropDownStyle = ComboBoxStyle.DropDownList;
			_fontBox.SetBounds(150, y - 3, 300, 26);
			try
			{
				using (InstalledFontCollection fonts = new InstalledFontCollection())
				{
					foreach (FontFamily family in fonts.Families) _fontBox.Items.Add(family.Name);
				}
			}
			catch { }
			if (_fontBox.Items.Count == 0) _fontBox.Items.Add("Consolas");
			_fontBox.SelectedItem = settings.FontName;
			if (_fontBox.SelectedIndex < 0) _fontBox.SelectedItem = "Consolas";
			Controls.Add(_fontBox);

			y += 38;
			AddLabel("字号：", 16, y);
			_sizeBox = new NumericUpDown();
			_sizeBox.Minimum = 6;
			_sizeBox.Maximum = 48;
			_sizeBox.Value = (decimal)Math.Max(6f, Math.Min(48f, settings.FontSize));
			_sizeBox.SetBounds(150, y - 3, 70, 26);
			Controls.Add(_sizeBox);

			AddLabel("缩进宽度：", 240, y, 84);
			_tabBox = new NumericUpDown();
			_tabBox.Minimum = 1;
			_tabBox.Maximum = 8;
			_tabBox.Value = Math.Max(1, Math.Min(8, settings.TabSize));
			_tabBox.SetBounds(330, y - 3, 60, 26);
			Controls.Add(_tabBox);

			y += 40;
			_wrapBox = new CheckBox();
			_wrapBox.Text = "自动换行";
			_wrapBox.Checked = settings.WordWrap;
			_wrapBox.SetBounds(150, y, 140, 24);
			Controls.Add(_wrapBox);

			y += 30;
			_minimapBox = new CheckBox();
			_minimapBox.Text = "显示鸟瞰图";
			_minimapBox.Checked = settings.ShowMinimap;
			_minimapBox.SetBounds(150, y, 140, 24);
			Controls.Add(_minimapBox);

			_minimapWidthBox = new NumericUpDown();
			_minimapWidthBox.Minimum = 60;
			_minimapWidthBox.Maximum = 200;
			_minimapWidthBox.Value = Math.Max(60, Math.Min(200, settings.MinimapWidth));
			_minimapWidthBox.SetBounds(300, y, 60, 24);
			Controls.Add(_minimapWidthBox);
			AddLabel("宽度：", 245, y - 3, 54);

			y += 30;
			_sessionBox = new CheckBox();
			_sessionBox.Text = "启动时恢复上次打开的文件";
			_sessionBox.Checked = settings.RestoreSession;
			_sessionBox.SetBounds(150, y, 280, 24);
			Controls.Add(_sessionBox);

			y += 30;
			_gitBox = new CheckBox();
			_gitBox.Text = "在资源管理器中标注 Git 状态";
			_gitBox.Checked = settings.ShowGitStatus;
			_gitBox.SetBounds(150, y, 280, 24);
			Controls.Add(_gitBox);

			y += 38;
			AddLabel("BetterGitGUI：", 16, y);
			_pathBox = new TextBox();
			_pathBox.Text = settings.BetterGitGUIPath ?? "";
			_pathBox.SetBounds(150, y - 3, 240, 26);
			Controls.Add(_pathBox);

			Button browse = new Button();
			browse.Text = "浏览…";
			browse.SetBounds(396, y - 4, 60, 28);
			browse.Click += delegate
			{
				using (OpenFileDialog dlg = new OpenFileDialog())
				{
					dlg.Filter = "可执行文件|*.exe|所有文件|*.*";
					if (_pathBox.Text.Length > 0) dlg.FileName = _pathBox.Text;
					if (dlg.ShowDialog(this) == DialogResult.OK) _pathBox.Text = dlg.FileName;
				}
			};
			Controls.Add(browse);

			Button defaults = new Button();
			defaults.Text = "恢复默认";
			defaults.SetBounds(16, 372, 90, 30);
			defaults.Click += delegate { ApplyDefaults(); };
			Controls.Add(defaults);

			Button ok = new Button();
			ok.Text = "确定";
			ok.SetBounds(272, 372, 84, 30);
			ok.DialogResult = DialogResult.OK;
			Controls.Add(ok);
			AcceptButton = ok;

			Button cancel = new Button();
			cancel.Text = "取消";
			cancel.SetBounds(366, 372, 84, 30);
			cancel.DialogResult = DialogResult.Cancel;
			Controls.Add(cancel);
			CancelButton = cancel;
		}

		private void AddLabel(string text, int x, int y)
		{
			AddLabel(text, x, y, 130);
		}

		private void AddLabel(string text, int x, int y, int width)
		{
			Label label = new Label();
			label.Text = text;
			label.SetBounds(x, y + 2, width, 22);
			Controls.Add(label);
		}

		private void ApplyDefaults()
		{
			_fontBox.SelectedItem = "Consolas";
			_sizeBox.Value = 11;
			_tabBox.Value = 4;
			_wrapBox.Checked = false;
			_minimapBox.Checked = true;
			_minimapWidthBox.Value = 92;
			_sessionBox.Checked = true;
			_gitBox.Checked = true;
			_pathBox.Text = "";
		}

		/// <summary>把界面上的选择写回设置对象。</summary>
		public void SaveTo(EditorSettings settings)
		{
			if (_fontBox.SelectedItem != null) settings.FontName = _fontBox.SelectedItem.ToString();
			settings.FontSize = (float)_sizeBox.Value;
			settings.TabSize = (int)_tabBox.Value;
			settings.WordWrap = _wrapBox.Checked;
			settings.ShowMinimap = _minimapBox.Checked;
			settings.MinimapWidth = (int)_minimapWidthBox.Value;
			settings.RestoreSession = _sessionBox.Checked;
			settings.ShowGitStatus = _gitBox.Checked;
			settings.BetterGitGUIPath = _pathBox.Text.Trim();
		}
	}
}
