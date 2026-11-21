using System;
using System.IO;
using System.Text;
using VisibleCMake.Core;

namespace VisibleCMake.Model
{
	/// <summary>本程序的全局设置（工具链路径、生成器、并行数等），保存在 %APPDATA%\VisibleCMake\settings.json。</summary>
	public class AppOptions
	{
		public AppOptions()
		{
			CmakePath = "";
			MakePath = "";
			CmakeGenerator = "";
			Jobs = 4;
			LastProjectPath = "";
			LastOpenDirectory = "";
			AutoGenerateOnSave = true;
		}

		/// <summary>cmake.exe 路径；空表示自动探测。</summary>
		public string CmakePath { get; set; }

		/// <summary>make.exe / mingw32-make.exe 路径；空表示自动探测。</summary>
		public string MakePath { get; set; }

		/// <summary>cmake -G 生成器名称；空表示交给 CMake 自己选择。</summary>
		public string CmakeGenerator { get; set; }

		/// <summary>make 并行任务数。</summary>
		public int Jobs { get; set; }

		public string LastProjectPath { get; set; }
		public string LastOpenDirectory { get; set; }
		public bool AutoGenerateOnSave { get; set; }

		public static string SettingsDirectory
		{
			get
			{
				string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				return Path.Combine(appData, "VisibleCMake");
			}
		}

		public static string SettingsPath
		{
			get { return Path.Combine(SettingsDirectory, "settings.json"); }
		}

		public static AppOptions Load()
		{
			try
			{
				string path = SettingsPath;
				if (File.Exists(path))
				{
					AppOptions options = MiniJson.Deserialize<AppOptions>(File.ReadAllText(path, Encoding.UTF8));
					if (options != null)
					{
						if (options.Jobs < 1) options.Jobs = 1;
						return options;
					}
				}
			}
			catch
			{
				// 设置文件损坏时退回默认值，不影响启动。
			}
			return new AppOptions();
		}

		public void Save()
		{
			try
			{
				if (!Directory.Exists(SettingsDirectory)) Directory.CreateDirectory(SettingsDirectory);
				File.WriteAllText(SettingsPath, MiniJson.Serialize(this), new UTF8Encoding(false));
			}
			catch
			{
				// 设置保存失败不应该中断当前工作。
			}
		}

		public static readonly string[] GeneratorOptions = new string[]
		{
			"（由 CMake 自动选择）",
			"Visual Studio 17 2022",
			"Visual Studio 16 2019",
			"MinGW Makefiles",
			"Unix Makefiles",
			"Ninja",
			"Ninja Multi-Config",
			"NMake Makefiles",
		};
	}
}
