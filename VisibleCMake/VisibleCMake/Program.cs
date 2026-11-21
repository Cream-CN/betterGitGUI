using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VisibleCMake.Runtime;
using VisibleCMake.UI;

namespace VisibleCMake
{
	internal static class Program
	{
		[DllImport("user32.dll")]
		private static extern bool SetProcessDPIAware();

		/// <summary>
		/// 应用程序入口。支持三种用法：
		///   VisibleCMake.exe                 直接打开（载入上次的工程）
		///   VisibleCMake.exe &lt;目录 | *.vcmake&gt;  由编辑器 / 资源管理器转交的工程
		///   VisibleCMake.exe --selftest      不开界面，自检生成器并把结果写进日志
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			try { SetProcessDPIAware(); }
			catch { }

			if (args != null && args.Length > 0 && IsSelfTestArgument(args[0]))
			{
				string logPath = args.Length > 1 ? args[1] : null;
				bool passed = SelfTest.Run(logPath);
				Environment.ExitCode = passed ? 0 : 1;
				return;
			}

			if (args != null && args.Length > 0 && IsUiTestArgument(args[0]))
			{
				string logPath = args.Length > 1 ? args[1] : null;
				bool passed = UiTest.Run(logPath);
				Environment.ExitCode = passed ? 0 : 1;
				return;
			}

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			try
			{
				Application.Run(new MainForm(args));
			}
			catch (Exception ex)
			{
				try
				{
					File.WriteAllText(Path.Combine(Path.GetTempPath(), "visiblecmake-error.log"), ex.ToString());
				}
				catch { }

				MessageBox.Show("启动失败：" + ex.Message, "VisibleCMake",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private static bool IsSelfTestArgument(string argument)
		{
			if (argument == null) return false;
			string value = argument.Trim().ToLowerInvariant();
			return value == "--selftest" || value == "/selftest" || value == "-selftest";
		}

		private static bool IsUiTestArgument(string argument)
		{
			if (argument == null) return false;
			string value = argument.Trim().ToLowerInvariant();
			return value == "--uitest" || value == "/uitest" || value == "-uitest";
		}
	}
}
