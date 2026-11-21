using System;
using System.Drawing;
using System.Windows.Forms;
using VisibleCMake.Model;
using VisibleCMake.Runtime;

namespace VisibleCMake.UI
{
	/// <summary>全局设置：cmake / make 的路径、生成器、并行数。</summary>
	public sealed class SettingsDialog : Form
	{
		private readonly AppOptions _options;
		private readonly TextBox _cmakeBox;
		private readonly TextBox _makeBox;
		private readonly ComboBox _generatorBox;
		private readonly NumericUpDown _jobsBox;
		private readonly CheckBox _autoBox;
		private readonly Label _probeLabel;

		public SettingsDialog(AppOptions options)
		{
			_options = options ?? new AppOptions();

			Text = "设置";
			Font = UiKit.UiFont;
			BackColor = UiKit.Surface;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MinimizeBox = false;
			MaximizeBox = false;
			ShowInTaskbar = false;
			ClientSize = new Size(640, 416);

			Label cmakeLabel = UiKit.FieldLabel("cmake.exe");
			cmakeLabel.SetBounds(20, 22, 96, 24);
			_cmakeBox = UiKit.Input(_options.CmakePath);
			_cmakeBox.SetBounds(120, 20, 400, 24);
			Button cmakeBrowse = UiKit.Secondary("浏览…", delegate { BrowseExe(_cmakeBox, "cmake.exe"); });
			cmakeBrowse.SetBounds(528, 18, 80, 28);

			Label makeLabel = UiKit.FieldLabel("make.exe");
			makeLabel.SetBounds(20, 58, 96, 24);
			_makeBox = UiKit.Input(_options.MakePath);
			_makeBox.SetBounds(120, 56, 400, 24);
			Button makeBrowse = UiKit.Secondary("浏览…", delegate { BrowseExe(_makeBox, "mingw32-make.exe"); });
			makeBrowse.SetBounds(528, 54, 80, 28);

			Label hint = UiKit.Caption("留空表示自动探测：先查 PATH，再查 CMake / Visual Studio / MSYS2 / MinGW 的常用安装目录。");
			hint.SetBounds(20, 82, 600, 20);

			Label generatorLabel = UiKit.FieldLabel("CMake 生成器");
			generatorLabel.SetBounds(20, 110, 96, 24);
			_generatorBox = UiKit.Combo(AppOptions.GeneratorOptions);
			_generatorBox.SetBounds(120, 108, 360, 24);
			_generatorBox.SelectedIndex = GeneratorIndex(_options.CmakeGenerator);

			Label jobsLabel = UiKit.FieldLabel("make 并行数");
			jobsLabel.SetBounds(20, 146, 96, 24);
			_jobsBox = new NumericUpDown();
			_jobsBox.Minimum = 1;
			_jobsBox.Maximum = 64;
			_jobsBox.Value = Math.Max(1, Math.Min(64, _options.Jobs));
			_jobsBox.Font = UiKit.UiFont;
			_jobsBox.SetBounds(120, 144, 70, 24);

			_autoBox = UiKit.Check("保存工程后立刻刷新 CMakeLists.txt / Makefile 预览", _options.AutoGenerateOnSave);
			_autoBox.SetBounds(20, 182, 460, 24);

			Button detect = UiKit.Secondary("检测本机工具链", delegate { DetectTools(); });
			detect.SetBounds(120, 214, 132, 28);

			_probeLabel = new Label();
			_probeLabel.SetBounds(20, 250, 600, 96);
			_probeLabel.Font = UiKit.MonoFont;
			_probeLabel.ForeColor = UiKit.Muted;
			_probeLabel.BackColor = Color.Transparent;
			_probeLabel.Text = "点“检测本机工具链”可以确认当前能不能直接构建。";

			Button ok = UiKit.Primary("保存设置", delegate { Apply(); });
			ok.SetBounds(440, 364, 100, 30);

			Button cancel = UiKit.Secondary("取消", delegate { DialogResult = DialogResult.Cancel; });
			cancel.SetBounds(548, 364, 72, 30);
			cancel.DialogResult = DialogResult.Cancel;

			Controls.Add(cmakeLabel);
			Controls.Add(_cmakeBox);
			Controls.Add(cmakeBrowse);
			Controls.Add(makeLabel);
			Controls.Add(_makeBox);
			Controls.Add(makeBrowse);
			Controls.Add(hint);
			Controls.Add(generatorLabel);
			Controls.Add(_generatorBox);
			Controls.Add(jobsLabel);
			Controls.Add(_jobsBox);
			Controls.Add(_autoBox);
			Controls.Add(detect);
			Controls.Add(_probeLabel);
			Controls.Add(ok);
			Controls.Add(cancel);

			AcceptButton = ok;
			CancelButton = cancel;
		}

		private static int GeneratorIndex(string generator)
		{
			if (string.IsNullOrWhiteSpace(generator)) return 0;
			for (int i = 0; i < AppOptions.GeneratorOptions.Length; i++)
			{
				if (string.Equals(AppOptions.GeneratorOptions[i], generator, StringComparison.Ordinal)) return i;
			}
			return 0;
		}

		private void BrowseExe(TextBox box, string fileName)
		{
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Title = "选择 " + fileName;
				dialog.Filter = "可执行文件|*.exe|所有文件|*.*";
				dialog.FileName = fileName;
				if (dialog.ShowDialog(this) == DialogResult.OK) box.Text = dialog.FileName;
			}
		}

		private void DetectTools()
		{
			try
			{
				_probeLabel.Text = "正在检测…";
				Application.DoEvents();

				ToolchainInfo info = ToolchainInfo.Redetect(_cmakeBox.Text, _makeBox.Text);
				info.ProbeVersions();

				if (info.HasCmake && _cmakeBox.Text.Trim().Length == 0) _cmakeBox.Text = info.CmakePath;
				if (info.HasMake && _makeBox.Text.Trim().Length == 0) _makeBox.Text = info.MakePath;

				_probeLabel.Text = info.Details();
			}
			catch (Exception ex)
			{
				_probeLabel.Text = "检测失败：" + ex.Message;
			}
		}

		private void Apply()
		{
			_options.CmakePath = _cmakeBox.Text.Trim();
			_options.MakePath = _makeBox.Text.Trim();
			_options.CmakeGenerator = _generatorBox.SelectedIndex <= 0
				? ""
				: (_generatorBox.SelectedItem as string);
			_options.Jobs = (int)_jobsBox.Value;
			_options.AutoGenerateOnSave = _autoBox.Checked;
			_options.Save();

			DialogResult = DialogResult.OK;
		}
	}
}
