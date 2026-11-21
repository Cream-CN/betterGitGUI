using System;
using System.Drawing;
using System.Windows.Forms;

namespace VisibleCMake.UI
{
	/// <summary>关于窗口。</summary>
	public sealed class AboutDialog : Form
	{
		public AboutDialog()
		{
			Text = "关于 VisibleCMake";
			Font = UiKit.UiFont;
			BackColor = UiKit.Surface;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MinimizeBox = false;
			MaximizeBox = false;
			ShowInTaskbar = false;
			ClientSize = new Size(520, 360);

			Label title = UiKit.Heading("VisibleCMake");
			title.SetBounds(24, 20, 300, 28);

			Label version = UiKit.Caption("1.0 · 图形化编写 CMakeLists.txt 与 GNU Makefile");
			version.SetBounds(24, 50, 420, 20);

			Label body = new Label();
			body.SetBounds(24, 82, 472, 210);
			body.Font = UiKit.UiFont;
			body.ForeColor = UiKit.TextColor;
			body.BackColor = Color.Transparent;
			body.Text =
				"把构建配置从命令行搬到图形界面：\r\n\r\n"
				+ "· 用表单维护工程、目标和编译/链接选项，生成标准 CMakeLists.txt；\r\n"
				+ "· 同时生成一份独立的 GNU Makefile，没装 CMake 也能 make 构建；\r\n"
				+ "· 生成前做配置检查（重名目标、缺失源文件、非法字符等）；\r\n"
				+ "· 一键调用 cmake 配置 / 构建、make 构建，输出原样显示；\r\n"
				+ "· 生成的文件可以交给同仓库的 Editior 继续手改；\r\n"
				+ "· 也能把已有的 CMakeLists.txt 反导回图形界面。\r\n\r\n"
				+ "工程文件是 *.vcmake（可读的 JSON），可以和源码一起提交。";

			Button ok = UiKit.Primary("关闭", delegate { Close(); });
			ok.SetBounds(408, 306, 88, 30);
			ok.DialogResult = DialogResult.Cancel;

			Controls.Add(title);
			Controls.Add(version);
			Controls.Add(body);
			Controls.Add(ok);

			AcceptButton = ok;
			CancelButton = ok;
		}
	}
}
