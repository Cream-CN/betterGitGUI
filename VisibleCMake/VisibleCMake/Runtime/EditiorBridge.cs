using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace VisibleCMake.Runtime
{
	/// <summary>
	/// 与本仓库里的 Editior（代码编辑器）对接：
	/// 生成 CMakeLists.txt / Makefile 之后，可以一键把它丢进编辑器里继续手改。
	/// 只按目录约定查找 exe，不写注册表、不改任何全局状态。
	/// </summary>
	public static class EditiorBridge
	{
		/// <summary>在源码树里定位 Editior.exe；找不到返回 null。</summary>
		public static string FindEditior()
		{
			return FindSibling(new string[]
			{
				@"editior\Editior\bin\Debug\Editior.exe",
				@"editior\Editior\bin\Release\Editior.exe"
			});
		}

		/// <summary>定位 betterGitGUI.exe。</summary>
		public static string FindBetterGitGUI()
		{
			return FindSibling(new string[]
			{
				@"bin\Debug\betterGitGUI.exe",
				@"bin\Release\betterGitGUI.exe"
			});
		}

		/// <summary>用 Editior 打开一个文件；文件不存在时提示，不静默失败。</summary>
		public static bool OpenInEditior(IWin32Window owner, string path)
		{
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				MessageBox.Show(owner, "文件还没有生成：\n" + path, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return false;
			}

			string exe = FindEditior();
			if (exe == null)
			{
				MessageBox.Show(owner,
					"没有找到 Editior.exe。\n请先编译 editior\\Editior 项目，或直接用系统默认程序打开该文件。",
					"Editior", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			}

			return Launch(exe, "\"" + path + "\"", Path.GetDirectoryName(exe), owner, "启动 Editior 失败");
		}

		/// <summary>用 Editior 打开一个目录（编辑器会把它作为工程根）。</summary>
		public static bool OpenFolderInEditior(IWin32Window owner, string directory)
		{
			if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
			{
				MessageBox.Show(owner, "目录不存在：\n" + directory, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return false;
			}

			string exe = FindEditior();
			if (exe == null)
			{
				MessageBox.Show(owner,
					"没有找到 Editior.exe。\n请先编译 editior\\Editior 项目。",
					"Editior", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			}

			return Launch(exe, "\"" + directory + "\"", Path.GetDirectoryName(exe), owner, "启动 Editior 失败");
		}

		/// <summary>在 BetterGitGUI 里打开工程目录（必须是 Git 仓库）。</summary>
		public static bool OpenInBetterGitGUI(IWin32Window owner, string directory)
		{
			string exe = FindBetterGitGUI();
			if (exe == null)
			{
				MessageBox.Show(owner,
					"没有找到 betterGitGUI.exe。\n请先编译根目录的 betterGitGUI 项目。",
					"可见构建", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return false;
			}
			return Launch(exe, "\"" + directory + "\"", Path.GetDirectoryName(exe), owner, "启动 BetterGitGUI 失败");
		}

		/// <summary>在资源管理器里选中一个文件或目录。</summary>
		public static void RevealInExplorer(IWin32Window owner, string path)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(path)) return;

				if (Directory.Exists(path))
				{
					Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
					return;
				}
				if (File.Exists(path))
				{
					Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
					return;
				}

				string directory = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
				{
					Process.Start(new ProcessStartInfo("explorer.exe", "\"" + directory + "\"") { UseShellExecute = true });
					return;
				}

				MessageBox.Show(owner, "路径不存在：\n" + path, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show(owner, "打开资源管理器失败：" + ex.Message, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private static bool Launch(string exe, string arguments, string workingDirectory, IWin32Window owner, string errorTitle)
		{
			try
			{
				ProcessStartInfo info = new ProcessStartInfo(exe, arguments);
				info.UseShellExecute = false;
				if (!string.IsNullOrEmpty(workingDirectory)) info.WorkingDirectory = workingDirectory;
				Process.Start(info);
				return true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(owner, errorTitle + "：" + ex.Message, "可见构建",
					MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
		}

		/// <summary>
		/// 从本程序所在目录逐级向上找仓库根，再拼上候选相对路径。
		/// 例如 VisibleCMake\VisibleCMake\bin\Debug → 向上 5 级就是仓库根 betterGitGUI。
		/// </summary>
		private static string FindSibling(string[] relativeCandidates)
		{
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string directory = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			for (int level = 0; level < 6 && !string.IsNullOrEmpty(directory); level++)
			{
				foreach (string relative in relativeCandidates)
				{
					try
					{
						string candidate = Path.Combine(directory, relative);
						if (File.Exists(candidate)) return candidate;
					}
					catch { }
				}

				// 同级目录直接放着 exe 的情况
				try
				{
					foreach (string file in Directory.GetFiles(directory, "*.exe"))
					{
						string name = Path.GetFileNameWithoutExtension(file);
						if (string.Equals(name, "Editior", StringComparison.OrdinalIgnoreCase) && relativeCandidates.Length > 0)
							return file;
					}
				}
				catch { }

				string parent = Path.GetDirectoryName(directory);
				if (parent == null || parent == directory) break;
				directory = parent;
			}
			return null;
		}
	}
}
