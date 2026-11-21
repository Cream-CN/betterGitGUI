using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Editior
{
	internal static class Program
	{
		[DllImport("user32.dll")]
		private static extern bool SetProcessDPIAware();

		/// <summary>
		/// 应用程序的主入口点。
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			try { SetProcessDPIAware(); }
			catch { }

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			try
			{
				// BetterGitGUI 会以 `Editior.exe --guid` 的方式调用，直接打开 GUID 生成器。
				if (args != null && args.Length > 0 && string.Equals(args[0], "--guid", StringComparison.OrdinalIgnoreCase))
				{
					Application.Run(new GuidGeneratorForm());
					return;
				}

				// `Editior.exe --datacode` 直接打开数据转代码窗口。
				if (args != null && args.Length > 0 && string.Equals(args[0], "--datacode", StringComparison.OrdinalIgnoreCase))
				{
					Application.Run(new DataCodeForm());
					return;
				}

				Application.Run(new MainForm(args));
			}
			catch (Exception ex)
			{
				try
				{
					File.WriteAllText(Path.Combine(Path.GetTempPath(), "editior-error.log"), ex.ToString());
				}
				catch { }
				MessageBox.Show("启动失败：" + ex.Message, "Editior",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
	}
}
