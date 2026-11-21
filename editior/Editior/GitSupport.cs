using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

namespace Editior
{
	/// <summary>和 git 命令行的轻量协作：找仓库根、取文件状态、定位 BetterGitGUI。</summary>
	public static class GitSupport
	{
		private static string _gitExe;
		private static bool _gitProbed;

		/// <summary>定位 git.exe：PATH 优先，其次常见安装目录与注册表。</summary>
		public static string FindGitExe()
		{
			if (_gitProbed) return _gitExe;
			_gitProbed = true;

			string[] candidates = new string[]
			{
				@"C:\Program Files\Git\cmd\git.exe",
				@"C:\Program Files\Git\bin\git.exe",
				@"C:\Program Files (x86)\Git\cmd\git.exe",
				@"D:\Program Files\Git\cmd\git.exe",
				@"D:\Program Files\Git\bin\git.exe",
				@"D:\Program Files (x86)\Git\cmd\git.exe",
				@"D:\Git\cmd\git.exe",
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\cmd\git.exe")
			};
			foreach (string candidate in candidates)
			{
				try { if (File.Exists(candidate)) { _gitExe = candidate; return _gitExe; } }
				catch { }
			}

			foreach (string key in new string[] { @"SOFTWARE\GitForWindows", @"SOFTWARE\WOW6432Node\GitForWindows" })
			{
				try
				{
					using (Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key))
					{
						if (registryKey == null) continue;
						string installPath = registryKey.GetValue("InstallPath") as string;
						if (string.IsNullOrEmpty(installPath)) continue;
						string exe = Path.Combine(installPath, @"cmd\git.exe");
						if (File.Exists(exe)) { _gitExe = exe; return _gitExe; }
						exe = Path.Combine(installPath, @"bin\git.exe");
						if (File.Exists(exe)) { _gitExe = exe; return _gitExe; }
					}
				}
				catch { }
			}

			_gitExe = "git";
			return _gitExe;
		}

		/// <summary>从某个路径向上查找 .git，返回仓库根目录。</summary>
		public static string FindRepositoryRoot(string path)
		{
			if (path == null || path.Length == 0) return null;
			string dir;
			try
			{
				dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
			}
			catch { return null; }

			while (dir != null && dir.Length > 0)
			{
				try
				{
					if (Directory.Exists(Path.Combine(dir, ".git")) || File.Exists(Path.Combine(dir, ".git")))
						return dir;
				}
				catch { }
				string parent = Path.GetDirectoryName(dir);
				if (parent == null || parent == dir) break;
				dir = parent;
			}
			return null;
		}

		/// <summary>执行 git 命令，返回标准输出；失败返回 null。</summary>
		public static string RunGit(string args, string workingDir)
		{
			try
			{
				ProcessStartInfo info = new ProcessStartInfo(FindGitExe(), args);
				info.WorkingDirectory = workingDir;
				info.UseShellExecute = false;
				info.CreateNoWindow = true;
				info.RedirectStandardOutput = true;
				info.RedirectStandardError = true;
				info.StandardOutputEncoding = Encoding.UTF8;
				info.StandardErrorEncoding = Encoding.UTF8;

				using (Process process = Process.Start(info))
				{
					if (process == null) return null;
					string output = process.StandardOutput.ReadToEnd();
					process.StandardError.ReadToEnd();
					if (!process.WaitForExit(10000))
					{
						try { process.Kill(); } catch { }
						return null;
					}
					if (process.ExitCode != 0) return null;
					return output;
				}
			}
			catch { return null; }
		}

		/// <summary>返回 绝对路径 → 两字符状态码 的字典。</summary>
		public static Dictionary<string, string> GetStatus(string repositoryRoot)
		{
			Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (repositoryRoot == null) return result;

			string output = RunGit("status --porcelain -z -uall", repositoryRoot);
			if (output == null) return result;

			string[] records = output.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < records.Length; i++)
			{
				string record = records[i];
				if (record.Length < 4) continue;
				string code = record.Substring(0, 2);
				string relative = record.Substring(3);

				// 重命名记录后面还会跟一个原路径，跳过它
				if (code.IndexOf('R') >= 0 && i + 1 < records.Length) i++;

				string full;
				try { full = Path.GetFullPath(Path.Combine(repositoryRoot, relative.Trim('"'))); }
				catch { continue; }
				result[full] = code;
			}
			return result;
		}

		public static Color StatusColor(string code)
		{
			if (code == null) return Color.Empty;
			if (code.IndexOf('U') >= 0 || code == "DD" || code == "AA") return Color.FromArgb(200, 40, 40);
			if (code.IndexOf("??", StringComparison.Ordinal) >= 0) return Color.FromArgb(40, 140, 60);
			if (code.IndexOf('M') >= 0) return Color.FromArgb(190, 120, 0);
			if (code.IndexOf('D') >= 0) return Color.FromArgb(200, 40, 40);
			if (code.IndexOf('A') >= 0) return Color.FromArgb(40, 140, 60);
			return Color.Empty;
		}

		public static string StatusText(string code)
		{
			if (code == null) return "";
			if (code.IndexOf("??", StringComparison.Ordinal) >= 0) return "未跟踪";
			if (code.IndexOf('U') >= 0) return "冲突";
			if (code.IndexOf('M') >= 0) return "已修改";
			if (code.IndexOf('D') >= 0) return "已删除";
			if (code.IndexOf('A') >= 0) return "新增";
			if (code.IndexOf('R') >= 0) return "重命名";
			return code.Trim();
		}

		/// <summary>定位 BetterGitGUI.exe：先用设置值，再在常见位置查找。</summary>
		public static string FindBetterGitGUI(string configuredPath)
		{
			if (configuredPath != null && configuredPath.Length > 0 && File.Exists(configuredPath))
				return configuredPath;

			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			List<string> roots = new List<string>();
			string dir = baseDir.TrimEnd(Path.DirectorySeparatorChar);
			for (int i = 0; i < 6 && dir != null; i++)
			{
				roots.Add(dir);
				string parent = Path.GetDirectoryName(dir);
				if (parent == null || parent == dir) break;
				dir = parent;
			}

			foreach (string root in roots)
			{
				string[] candidates = new string[]
				{
					Path.Combine(root, "betterGitGUI.exe"),
					Path.Combine(root, "bin", "Debug", "betterGitGUI.exe"),
					Path.Combine(root, "bin", "Release", "betterGitGUI.exe"),
					Path.Combine(root, "betterGitGUI", "bin", "Debug", "betterGitGUI.exe")
				};
				foreach (string candidate in candidates)
				{
					try { if (File.Exists(candidate)) return candidate; }
					catch { }
				}
			}
			return null;
		}
	}
}
