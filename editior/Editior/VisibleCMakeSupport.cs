using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>
	/// 和同仓库的 VisibleCMake（图形化 CMake / GNU Make 编辑器）对接：
	/// 工具菜单里一键把当前文件所在目录交给它，在图形界面里维护构建配置。
	/// 只按目录约定找 exe，找不到时明确提示，不静默失败。
	/// </summary>
	public static class VisibleCMakeSupport
	{
		/// <summary>从编辑器所在目录逐级向上找 VisibleCMake.exe。</summary>
		public static string FindVisibleCMake()
		{
			string directory = AppDomain.CurrentDomain.BaseDirectory
				.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			for (int level = 0; level < 7 && !string.IsNullOrEmpty(directory); level++)
			{
				string[] candidates = new string[]
				{
					Path.Combine(directory, @"VisibleCMake\VisibleCMake\bin\Debug\VisibleCMake.exe"),
					Path.Combine(directory, @"VisibleCMake\VisibleCMake\bin\Release\VisibleCMake.exe"),
					Path.Combine(directory, @"VisibleCMake\bin\Debug\VisibleCMake.exe"),
					Path.Combine(directory, @"VisibleCMake\bin\Release\VisibleCMake.exe"),
					Path.Combine(directory, "VisibleCMake.exe")
				};

				foreach (string candidate in candidates)
				{
					try { if (File.Exists(candidate)) return candidate; }
					catch { }
				}

				string parent = Path.GetDirectoryName(directory);
				if (parent == null || parent == directory) break;
				directory = parent;
			}
			return null;
		}

		/// <summary>
		/// 打开可视化构建界面。path 可以是目录，也可以是某个文件（会取它所在目录）。
		/// </summary>
		public static bool OpenDesigner(IWin32Window owner, string path)
		{
			string exe = FindVisibleCMake();
			if (exe == null)
			{
				MessageBox.Show(owner,
					"没有找到 VisibleCMake.exe。\n请先在解决方案里编译 VisibleCMake 项目（VisibleCMake\\VisibleCMake\\VisibleCMake.csproj）。",
					"可视化构建", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			}

			string target = ToDirectory(path);
			try
			{
				ProcessStartInfo info = new ProcessStartInfo(exe);
				info.Arguments = target.Length == 0 ? "" : "\"" + target + "\"";
				info.UseShellExecute = false;
				info.WorkingDirectory = Path.GetDirectoryName(exe);
				Process.Start(info);
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(owner, "启动 VisibleCMake 失败：" + ex.Message, "可视化构建",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}

		/// <summary>优先用当前文档所在目录，其次用已打开的文件夹，最后退回当前工作目录。</summary>
		public static string GuessTarget(string documentPath, string folderPath)
		{
			if (!string.IsNullOrWhiteSpace(documentPath)) return Path.GetDirectoryName(documentPath);
			if (!string.IsNullOrWhiteSpace(folderPath)) return folderPath;
			return Environment.CurrentDirectory;
		}

		private static string ToDirectory(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return "";
			try
			{
				if (Directory.Exists(path)) return Path.GetFullPath(path);
				if (File.Exists(path))
				{
					string directory = Path.GetDirectoryName(Path.GetFullPath(path));
					return directory ?? "";
				}
			}
			catch { }
			return path;
		}
	}
}
