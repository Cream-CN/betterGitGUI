using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using VisibleCMake.Model;

namespace VisibleCMake.UI
{
	/// <summary>新建工程：选目录、模板和 C++ 标准，可选顺手生成一份能编译的示例源码。</summary>
	public sealed class NewProjectDialog : Form
	{
		private readonly TextBox _nameBox;
		private readonly TextBox _directoryBox;
		private readonly ComboBox _templateBox;
		private readonly ComboBox _standardBox;
		private readonly CheckBox _sampleBox;
		private readonly Label _hintLabel;

		public NewProjectDialog(string defaultDirectory)
		{
			Text = "新建工程";
			Font = UiKit.UiFont;
			BackColor = UiKit.Surface;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MinimizeBox = false;
			MaximizeBox = false;
			ShowInTaskbar = false;
			ClientSize = new Size(560, 322);

			Label nameLabel = UiKit.FieldLabel("工程名称");
			nameLabel.SetBounds(20, 22, 84, 24);
			_nameBox = UiKit.Input("MyProject");
			_nameBox.SetBounds(108, 20, 432, 24);

			Label directoryLabel = UiKit.FieldLabel("工程目录");
			directoryLabel.SetBounds(20, 58, 84, 24);
			_directoryBox = UiKit.Input(string.IsNullOrWhiteSpace(defaultDirectory) ? Environment.CurrentDirectory : defaultDirectory);
			_directoryBox.SetBounds(108, 56, 344, 24);

			Button browse = UiKit.Secondary("浏览…", delegate
			{
				using (FolderBrowserDialog dialog = new FolderBrowserDialog())
				{
					dialog.Description = "选择工程根目录（CMakeLists.txt 与 Makefile 会写在这里）";
					dialog.SelectedPath = _directoryBox.Text;
					if (dialog.ShowDialog(this) == DialogResult.OK) _directoryBox.Text = dialog.SelectedPath;
				}
			});
			browse.SetBounds(458, 54, 82, 28);

			Label templateLabel = UiKit.FieldLabel("工程模板");
			templateLabel.SetBounds(20, 94, 84, 24);
			_templateBox = UiKit.Combo(new string[] { "可执行文件", "静态库", "共享库", "空工程" });
			_templateBox.SetBounds(108, 92, 200, 24);
			_templateBox.SelectedIndexChanged += delegate { UpdateHint(); };

			Label standardLabel = UiKit.FieldLabel("C++ 标准");
			standardLabel.SetBounds(330, 94, 72, 24);
			_standardBox = UiKit.Combo(new string[] { "17", "20", "14", "11", "（不指定）" });
			_standardBox.SetBounds(404, 92, 136, 24);

			_sampleBox = UiKit.Check("同时创建一份可以直接编译的示例源码", true);
			_sampleBox.SetBounds(108, 128, 400, 24);

			_hintLabel = new Label();
			_hintLabel.SetBounds(20, 162, 520, 96);
			_hintLabel.Font = UiKit.SmallFont;
			_hintLabel.ForeColor = UiKit.Muted;
			_hintLabel.BackColor = Color.Transparent;

			Button ok = UiKit.Primary("创建工程", delegate { OnCreate(); });
			ok.SetBounds(352, 276, 96, 30);

			Button cancel = UiKit.Secondary("取消", delegate { DialogResult = DialogResult.Cancel; });
			cancel.SetBounds(456, 276, 84, 30);
			cancel.DialogResult = DialogResult.Cancel;

			Controls.Add(nameLabel);
			Controls.Add(_nameBox);
			Controls.Add(directoryLabel);
			Controls.Add(_directoryBox);
			Controls.Add(browse);
			Controls.Add(templateLabel);
			Controls.Add(_templateBox);
			Controls.Add(standardLabel);
			Controls.Add(_standardBox);
			Controls.Add(_sampleBox);
			Controls.Add(_hintLabel);
			Controls.Add(ok);
			Controls.Add(cancel);

			AcceptButton = ok;
			CancelButton = cancel;

			UpdateHint();
		}

		/// <summary>创建好的工程模型；对话框被取消时为 null。</summary>
		public ProjectModel Model { get; private set; }

		private void UpdateHint()
		{
			string template = _templateBox.SelectedItem as string;
			if (template == null) template = "可执行文件";

			string text;
			if (template == "空工程")
			{
				text = "空工程只创建工程主体，不生成任何目标；之后可以在左侧“新建目标”里自己添加。";
			}
			else
			{
				text = "会创建一个名为工程名的“" + template + "”目标。勾选示例源码时，会在 src/ 下生成一份可直接编译的代码"
					+ "（" + (template == "可执行文件" ? "main.cpp" : "库的 .cpp + .h") + "），并自动加入源文件列表。";
			}

			_hintLabel.Text = text
				+ "\r\n\r\n目录不存在时会自动创建，已有文件不会被覆盖。";
		}

		private void OnCreate()
		{
			string name = _nameBox.Text.Trim();
			if (name.Length == 0)
			{
				MessageBox.Show(this, "工程名称不能为空。", "新建工程", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				_nameBox.Focus();
				return;
			}

			string directory = _directoryBox.Text.Trim();
			if (directory.Length == 0)
			{
				MessageBox.Show(this, "请选择工程目录。", "新建工程", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			try
			{
				directory = Path.GetFullPath(directory);
				if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "工程目录不可用：" + ex.Message, "新建工程",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			string template = _templateBox.SelectedItem as string;
			string standard = _standardBox.SelectedIndex < 0 ? "" : _standardBox.SelectedItem as string;
			if (standard == "（不指定）") standard = "";

			ProjectModel model = ProjectModel.CreateDefault(name, directory, template, standard);
			model.EnsureDefaults();

			if (_sampleBox.Checked) CreateSample(model, template);

			Model = model;
			DialogResult = DialogResult.OK;
		}

		private void CreateSample(ProjectModel model, string template)
		{
			try
			{
				string identifier = Identifier(model.Name);
				string sourceDirectory = Path.Combine(model.RootDirectory, "src");
				string includeDirectory = Path.Combine(model.RootDirectory, "include");

				if (template == "空工程")
				{
					if (model.Targets.Count == 0) return;
				}

				if (template == "可执行文件" || model.Targets.Count == 0)
				{
					if (!Directory.Exists(sourceDirectory)) Directory.CreateDirectory(sourceDirectory);

					string mainPath = Path.Combine(sourceDirectory, "main.cpp");
					if (!File.Exists(mainPath))
					{
						WriteFile(mainPath,
							"#include <iostream>\r\n\r\n"
							+ "int main()\r\n{\r\n"
							+ "    std::cout << \"" + Escape(model.Name) + " 已运行\" << std::endl;\r\n"
							+ "    return 0;\r\n}\r\n");
					}

					if (model.Targets.Count > 0)
					{
						TargetModel target = model.Targets[0];
						if (!target.Sources.Contains("src/main.cpp")) target.Sources.Add("src/main.cpp");
					}
					return;
				}

				if (!Directory.Exists(sourceDirectory)) Directory.CreateDirectory(sourceDirectory);
				if (!Directory.Exists(includeDirectory)) Directory.CreateDirectory(includeDirectory);

				string relativeSource = "src/" + identifier + ".cpp";
				string relativeHeader = "include/" + identifier + ".h";
				string headerPath = Path.Combine(model.RootDirectory, "include", identifier + ".h");
				string sourcePath = Path.Combine(model.RootDirectory, "src", identifier + ".cpp");

				if (!File.Exists(headerPath))
				{
					WriteFile(headerPath, "#pragma once\r\n\r\nint " + identifier + "_value();\r\n");
				}
				if (!File.Exists(sourcePath))
				{
					WriteFile(sourcePath,
						"#include \"" + identifier + ".h\"\r\n\r\n"
						+ "int " + identifier + "_value()\r\n{\r\n    return 42;\r\n}\r\n");
				}

				if (model.Targets.Count > 0)
				{
					TargetModel target = model.Targets[0];
					if (!target.Sources.Contains(relativeSource)) target.Sources.Add(relativeSource);
					if (!target.Headers.Contains(relativeHeader)) target.Headers.Add(relativeHeader);
					if (!target.IncludeDirectories.Contains("include")) target.IncludeDirectories.Add("include");
				}
			}
			catch
			{
				// 示例文件生成失败不影响工程创建本身，用户可以在界面里自己补。
			}
		}

		private static void WriteFile(string path, string content)
		{
			File.WriteAllText(path, content, new UTF8Encoding(false));
		}

		private static string Identifier(string name)
		{
			StringBuilder sb = new StringBuilder();
			foreach (char c in name ?? "")
			{
				if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
				else sb.Append('_');
			}
			if (sb.Length == 0) sb.Append("lib");
			if (char.IsDigit(sb[0])) sb.Insert(0, '_');
			return sb.ToString();
		}

		private static string Escape(string text)
		{
			return (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
		}
	}
}
