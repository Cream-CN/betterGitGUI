using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using VisibleCMake.Model;

namespace VisibleCMake.Runtime
{
	/// <summary>
	/// 工具链探测：定位 cmake.exe 与 GNU make，并读出各自的版本号。
	/// 优先级：设置里手填的路径 → PATH → 常见安装目录。
	/// </summary>
	public sealed class ToolchainInfo
	{
		public string CmakePath = "";
		public string MakePath = "";
		public string CmakeVersion = "";
		public string MakeVersion = "";

		public bool HasCmake
		{
			get { return FileExists(CmakePath); }
		}

		public bool HasMake
		{
			get { return FileExists(MakePath); }
		}

		public static ToolchainInfo Detect(AppOptions options)
		{
			ToolchainInfo info = new ToolchainInfo();
			info.CmakePath = Locate(
				options == null ? "" : options.CmakePath,
				new string[] { "cmake.exe" },
				CmakeDirectories());
			info.MakePath = Locate(
				options == null ? "" : options.MakePath,
				new string[] { "mingw32-make.exe", "make.exe", "gmake.exe" },
				MakeDirectories());
			return info;
		}

		/// <summary>在后台线程调用，读出 cmake --version / make --version 的首行。</summary>
		public void ProbeVersions()
		{
			try
			{
				if (HasCmake)
				{
					ProcessResult result = ProcessRunner.Run(CmakePath, "--version", null, null, CancellationToken.None, 10000);
					if (result.Started) CmakeVersion = ProcessRunner.FirstLine(result.Output);
				}
			}
			catch { }

			try
			{
				if (HasMake)
				{
					ProcessResult result = ProcessRunner.Run(MakePath, "--version", null, null, CancellationToken.None, 10000);
					if (result.Started) MakeVersion = ProcessRunner.FirstLine(result.Output);
					// GNU make 的版本号有时只写 "GNU Make 4.3"，这里再兜一次。
					if (MakeVersion.Length == 0 && result != null) MakeVersion = ProcessRunner.FirstLine(result.Output);
				}
			}
			catch { }
		}

		/// <summary>一行摘要，状态栏用。</summary>
		public string Summary()
		{
			List<string> parts = new List<string>();
			parts.Add(HasCmake ? "CMake：" + ShortVersion(CmakeVersion, "已找到") : "CMake：未找到");
			parts.Add(HasMake ? "make：" + ShortVersion(MakeVersion, "已找到") : "make：未找到");
			return string.Join("　|　", parts.ToArray());
		}

		/// <summary>完整的路径信息，写日志用。</summary>
		public string Details()
		{
			List<string> lines = new List<string>();
			lines.Add("CMake：" + (HasCmake ? CmakePath : "未找到") + (CmakeVersion.Length > 0 ? "（" + CmakeVersion + "）" : ""));
			lines.Add("make ：" + (HasMake ? MakePath : "未找到") + (MakeVersion.Length > 0 ? "（" + MakeVersion + "）" : ""));
			return string.Join("\r\n", lines.ToArray());
		}

		private static string ShortVersion(string version, string fallback)
		{
			if (string.IsNullOrWhiteSpace(version)) return fallback;
			if (version.Length > 42) return version.Substring(0, 42) + "…";
			return version;
		}

		private static bool FileExists(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return false;
			try { return File.Exists(path); }
			catch { return false; }
		}

		/// <summary>手填 → PATH → 候选目录 → 返回空字符串。</summary>
		private static string Locate(string configured, string[] fileNames, List<string> candidateDirectories)
		{
			if (!string.IsNullOrWhiteSpace(configured))
			{
				string trimmed = configured.Trim().Trim('"');
				if (FileExists(trimmed)) return trimmed;

				// 允许只填目录名
				foreach (string name in fileNames)
				{
					string combined = Path.Combine(trimmed, name);
					if (FileExists(combined)) return combined;
				}
			}

			List<string> searchDirectories = new List<string>();
			searchDirectories.AddRange(PathDirectories());
			searchDirectories.AddRange(candidateDirectories);

			foreach (string directory in searchDirectories)
			{
				if (string.IsNullOrWhiteSpace(directory)) continue;
				foreach (string name in fileNames)
				{
					try
					{
						string candidate = Path.Combine(directory, name);
						if (FileExists(candidate)) return candidate;
					}
					catch { }
				}
			}
			return "";
		}

		private static List<string> PathDirectories()
		{
			List<string> directories = new List<string>();
			try
			{
				string path = Environment.GetEnvironmentVariable("PATH");
				if (!string.IsNullOrEmpty(path))
				{
					foreach (string item in path.Split(';'))
					{
						string trimmed = item.Trim().Trim('"');
						if (trimmed.Length > 0) directories.Add(trimmed);
					}
				}
			}
			catch { }
			return directories;
		}

		private static List<string> CmakeDirectories()
		{
			List<string> directories = new List<string>();
			AddIfExists(directories, @"C:\Program Files\CMake\bin");
			AddIfExists(directories, @"C:\Program Files (x86)\CMake\bin");
			AddIfExists(directories, Path.Combine(ProgramFilesX86(), @"CMake\bin"));
			AddIfExists(directories, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\CMake\bin"));
			AddIfExists(directories, @"C:\ProgramData\chocolatey\bin");
			AddIfExists(directories, @"C:\ProgramData\scoop\shims");
			AddIfExists(directories, @"C:\msys64\mingw64\bin");

			// Visual Studio 自带的 CMake
			string[] visualStudioRoots = new string[]
			{
				@"C:\Program Files\Microsoft Visual Studio\2022",
				@"C:\Program Files (x86)\Microsoft Visual Studio\2019",
				@"C:\Program Files\Microsoft Visual Studio\2019",
				@"C:\Program Files (x86)\Microsoft Visual Studio\2017"
			};
			foreach (string root in visualStudioRoots)
			{
				if (!Directory.Exists(root)) continue;
				try
				{
					foreach (string edition in Directory.GetDirectories(root))
					{
						AddIfExists(directories, Path.Combine(edition, @"Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin"));
					}
				}
				catch { }
			}
			return directories;
		}

		private static List<string> MakeDirectories()
		{
			List<string> directories = new List<string>();
			AddIfExists(directories, @"C:\msys64\mingw64\bin");
			AddIfExists(directories, @"C:\msys64\usr\bin");
			AddIfExists(directories, @"C:\MinGW\bin");
			AddIfExists(directories, @"C:\MinGW\msys\1.0\bin");
			AddIfExists(directories, @"C:\mingw32\bin");
			AddIfExists(directories, @"C:\mingw64\bin");
			AddIfExists(directories, @"C:\TDM-GCC-64\bin");
			AddIfExists(directories, @"C:\Strawberry\c\bin");
			AddIfExists(directories, @"C:\Program Files\Git\usr\bin");
			AddIfExists(directories, @"C:\ProgramData\chocolatey\bin");
			AddIfExists(directories, @"C:\ProgramData\scoop\shims");
			AddIfExists(directories, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\mingw64\bin"));
			return directories;
		}

		private static string ProgramFilesX86()
		{
			string value = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			return string.IsNullOrEmpty(value) ? @"C:\Program Files (x86)" : value;
		}

		private static void AddIfExists(List<string> directories, string directory)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) directories.Add(directory);
			}
			catch { }
		}

		/// <summary>供设置对话框调用：按当前输入重新探测。</summary>
		public static ToolchainInfo Redetect(string cmakePath, string makePath)
		{
			ToolchainInfo info = new ToolchainInfo();
			info.CmakePath = Locate(cmakePath, new string[] { "cmake.exe" }, CmakeDirectories());
			info.MakePath = Locate(makePath, new string[] { "mingw32-make.exe", "make.exe", "gmake.exe" }, MakeDirectories());
			return info;
		}
	}
}
