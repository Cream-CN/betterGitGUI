using System;
using System.Windows.Forms;

namespace betterGitGUI
{
	internal static class Program
	{
		/// <summary>
		/// 应用程序的主入口点。支持传入一个仓库路径（Editior 等外部程序会这样调用）。
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			string startupRepository = null;
			if (args != null && args.Length > 0)
			{
				string candidate = args[0];
				if (candidate != null && candidate.Length > 0) startupRepository = candidate;
			}

			Application.Run(new Form1(startupRepository));
		}
	}
}
