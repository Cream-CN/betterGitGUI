using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VisibleCMake.Model
{
	/// <summary>构建目标的种类。</summary>
	public enum TargetKind
	{
		Executable,
		StaticLibrary,
		SharedLibrary,
		InterfaceLibrary,
	}

	/// <summary>
	/// 一个构建目标（可执行文件 / 静态库 / 共享库 / 接口库）。
	/// 所有路径都相对于 <see cref="ProjectModel.RootDirectory"/>，生成代码时统一转成 "/" 分隔。
	/// </summary>
	public class TargetModel
	{
		public TargetModel()
		{
			Name = "app";
			Kind = TargetKind.Executable;
			Enabled = true;
			OutputName = "";
			Sources = new List<string>();
			Headers = new List<string>();
			IncludeDirectories = new List<string>();
			Definitions = new List<string>();
			CompileOptions = new List<string>();
			LinkDirectories = new List<string>();
			LinkLibraries = new List<string>();
			PostBuildCommands = "";
			ExtraCode = "";
		}

		public string Name { get; set; }
		public TargetKind Kind { get; set; }
		public bool Enabled { get; set; }

		/// <summary>输出文件名（不含前后缀）；留空则与目标名相同。</summary>
		public string OutputName { get; set; }

		/// <summary>仅对可执行文件有效：生成 Windows 窗口程序（-mwindows / WIN32 / SUBSYSTEM:WINDOWS）。</summary>
		public bool WindowsSubsystem { get; set; }

		public List<string> Sources { get; set; }
		public List<string> Headers { get; set; }
		public List<string> IncludeDirectories { get; set; }
		public List<string> Definitions { get; set; }
		public List<string> CompileOptions { get; set; }
		public List<string> LinkDirectories { get; set; }
		public List<string> LinkLibraries { get; set; }
		public string PostBuildCommands { get; set; }
		public string ExtraCode { get; set; }

		public string EffectiveOutputName
		{
			get
			{
				return string.IsNullOrWhiteSpace(OutputName) ? Name : OutputName.Trim();
			}
		}

		public bool IsLibrary
		{
			get
			{
				return Kind == TargetKind.StaticLibrary
					|| Kind == TargetKind.SharedLibrary
					|| Kind == TargetKind.InterfaceLibrary;
			}
		}

		public string KindText
		{
			get
			{
				switch (Kind)
				{
					case TargetKind.Executable: return "可执行文件";
					case TargetKind.StaticLibrary: return "静态库";
					case TargetKind.SharedLibrary: return "共享库";
					default: return "接口库";
				}
			}
		}

		/// <summary>是否参与实际编译（接口库只有头文件，不产生二进制）。</summary>
		public bool ProducesBinary
		{
			get { return Kind != TargetKind.InterfaceLibrary; }
		}

		public TargetModel Clone()
		{
			TargetModel t = new TargetModel();
			t.Name = Name;
			t.Kind = Kind;
			t.Enabled = Enabled;
			t.OutputName = OutputName;
			t.WindowsSubsystem = WindowsSubsystem;
			t.Sources = new List<string>(Sources);
			t.Headers = new List<string>(Headers);
			t.IncludeDirectories = new List<string>(IncludeDirectories);
			t.Definitions = new List<string>(Definitions);
			t.CompileOptions = new List<string>(CompileOptions);
			t.LinkDirectories = new List<string>(LinkDirectories);
			t.LinkLibraries = new List<string>(LinkLibraries);
			t.PostBuildCommands = PostBuildCommands;
			t.ExtraCode = ExtraCode;
			return t;
		}

		public void EnsureDefaults()
		{
			if (Sources == null) Sources = new List<string>();
			if (Headers == null) Headers = new List<string>();
			if (IncludeDirectories == null) IncludeDirectories = new List<string>();
			if (Definitions == null) Definitions = new List<string>();
			if (CompileOptions == null) CompileOptions = new List<string>();
			if (LinkDirectories == null) LinkDirectories = new List<string>();
			if (LinkLibraries == null) LinkLibraries = new List<string>();
			if (PostBuildCommands == null) PostBuildCommands = "";
			if (ExtraCode == null) ExtraCode = "";
			if (Name == null) Name = "app";
		}
	}

	/// <summary>
	/// 整个工程的数据模型：工程级设置 + 目标列表。
	/// 保存为 *.vcmake（JSON 文本），可以随时重新打开继续用图形界面编辑。
	/// </summary>
	public class ProjectModel
	{
		public ProjectModel()
        {
			Name = "MyApp";
			Version = "1.0.0";
			CmakeMinimumVersion = "3.20";
			CppStandard = "17";
			CStandard = "";
			DefaultBuildType = "Debug";
			BuildDirectory = "build";
			RuntimeOutputDirectory = "bin";
			ArchiveOutputDirectory = "lib";
			EnableWarnings = true;
			InstallRules = false;
			EnableTesting = false;
			GenerateCompileCommands = true;
			GlobalIncludeDirectories = new List<string>();
			GlobalDefinitions = new List<string>();
			GlobalCompileOptions = new List<string>();
			GlobalLinkDirectories = new List<string>();
			GlobalLinkLibraries = new List<string>();
			Targets = new List<TargetModel>();
			ExtraCmakeCode = "";
			ExtraMakeCode = "";
			MakefileFileName = "Makefile";
			RootDirectory = "";
		}

		public string Name { get; set; }
		public string Version { get; set; }
		public string CmakeMinimumVersion { get; set; }

		/// <summary>C++ 标准："" / 98 / 11 / 14 / 17 / 20 / 23。</summary>
		public string CppStandard { get; set; }

		/// <summary>C 标准："" / 99 / 11 / 17。</summary>
		public string CStandard { get; set; }

		public string DefaultBuildType { get; set; }
		public string BuildDirectory { get; set; }
		public string RuntimeOutputDirectory { get; set; }
		public string ArchiveOutputDirectory { get; set; }

		/// <summary>打开常用的编译警告（GCC/Clang 用 -Wall -Wextra，MSVC 用 /W4）。</summary>
		public bool EnableWarnings { get; set; }

		public bool InstallRules { get; set; }
		public bool EnableTesting { get; set; }
		public bool GenerateCompileCommands { get; set; }

		public List<string> GlobalIncludeDirectories { get; set; }
		public List<string> GlobalDefinitions { get; set; }
		public List<string> GlobalCompileOptions { get; set; }
		public List<string> GlobalLinkDirectories { get; set; }
		public List<string> GlobalLinkLibraries { get; set; }

		public List<TargetModel> Targets { get; set; }

		public string ExtraCmakeCode { get; set; }
		public string ExtraMakeCode { get; set; }
		public string MakefileFileName { get; set; }

		/// <summary>工程根目录（CMakeLists.txt 与 Makefile 所在目录），绝对路径。</summary>
		public string RootDirectory { get; set; }

		public IEnumerable<TargetModel> EnabledTargets
		{
			get { return Targets.Where(t => t.Enabled && t.Name != null && t.Name.Trim().Length > 0); }
		}

		public TargetModel PrimaryExecutable
		{
			get { return EnabledTargets.FirstOrDefault(t => t.Kind == TargetKind.Executable && t.ProducesBinary); }
		}

		public string CmakeListsPath
		{
			get { return Path.Combine(RootDirectory, "CMakeLists.txt"); }
		}

		public string MakefilePath
		{
			get
			{
				string fileName = string.IsNullOrWhiteSpace(MakefileFileName) ? "Makefile" : MakefileFileName.Trim();
				return Path.Combine(RootDirectory, fileName);
			}
		}

		public string BuildPath
        {
			get { return PathUtil.Combine(RootDirectory, BuildDirectory); }
		}

		public void EnsureDefaults()
		{
			if (GlobalIncludeDirectories == null) GlobalIncludeDirectories = new List<string>();
			if (GlobalDefinitions == null) GlobalDefinitions = new List<string>();
			if (GlobalCompileOptions == null) GlobalCompileOptions = new List<string>();
			if (GlobalLinkDirectories == null) GlobalLinkDirectories = new List<string>();
			if (GlobalLinkLibraries == null) GlobalLinkLibraries = new List<string>();
			if (Targets == null) Targets = new List<TargetModel>();
			if (ExtraCmakeCode == null) ExtraCmakeCode = "";
			if (ExtraMakeCode == null) ExtraMakeCode = "";
			if (string.IsNullOrWhiteSpace(CmakeMinimumVersion)) CmakeMinimumVersion = "3.20";
			if (string.IsNullOrWhiteSpace(BuildDirectory)) BuildDirectory = "build";
			if (string.IsNullOrWhiteSpace(RuntimeOutputDirectory)) RuntimeOutputDirectory = "bin";
			if (string.IsNullOrWhiteSpace(ArchiveOutputDirectory)) ArchiveOutputDirectory = "lib";
			if (string.IsNullOrWhiteSpace(DefaultBuildType)) DefaultBuildType = "Debug";
			if (string.IsNullOrWhiteSpace(MakefileFileName)) MakefileFileName = "Makefile";
			if (CppStandard == null) CppStandard = "";
			if (CStandard == null) CStandard = "";
			if (Version == null) Version = "";
			foreach (TargetModel target in Targets) target.EnsureDefaults();
		}

		/// <summary>把模型里的路径转成绝对路径。</summary>
		public string Resolve(string path)
		{
			return PathUtil.Combine(RootDirectory, path);
		}

		/// <summary>把绝对路径转成相对工程根目录的路径（不在根目录下时保持绝对路径）。</summary>
		public string Relativize(string absolutePath)
		{
			return PathUtil.Relative(RootDirectory, absolutePath);
		}

		public static readonly string[] CppStandardOptions = new string[] { "（不指定）", "98", "11", "14", "17", "20", "23" };
		public static readonly string[] CStandardOptions = new string[] { "（不指定）", "99", "11", "17" };
		public static readonly string[] BuildTypeOptions = new string[] { "Debug", "Release", "RelWithDebInfo", "MinSizeRel" };

		/// <summary>按模板创建新工程。</summary>
		public static ProjectModel CreateDefault(string name, string rootDirectory, string template, string cppStandard)
		{
			ProjectModel model = new ProjectModel();
			model.Name = name;
			model.RootDirectory = rootDirectory;
			if (!string.IsNullOrEmpty(cppStandard)) model.CppStandard = cppStandard;

			TargetModel target = new TargetModel();
			target.Name = name;
			switch (template)
			{
				case "静态库":
					target.Kind = TargetKind.StaticLibrary;
					break;
				case "共享库":
					target.Kind = TargetKind.SharedLibrary;
					break;
				case "空工程":
					target = null;
					break;
				default:
					target.Kind = TargetKind.Executable;
					break;
			}

			if (target != null) model.Targets.Add(target);
			model.ExtraCmakeCode = DefaultExtraCode;
			model.ExtraMakeCode = DefaultExtraMakeCode;
			return model;
		}

		public const string DefaultExtraCode =
			"# 这里可以补充任意 CMake 代码，会原样追加到 CMakeLists.txt 末尾。";

		public const string DefaultExtraMakeCode =
			"# 这里可以补充任意 GNU make 规则，会原样追加到 Makefile 末尾。";
	}

	/// <summary>路径处理小工具：统一使用 "/" 生成构建脚本，比较时忽略大小写。</summary>
	public static class PathUtil
	{
		public static string ToSlashes(string path)
		{
			if (path == null) return "";
			return path.Replace('\\', '/');
		}

		/// <summary>把 root 与相对路径拼接成绝对路径。（相对路径里可以是 "/"）</summary>
		public static string Combine(string root, string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return root ?? "";
			string normalized = path.Replace('/', Path.DirectorySeparatorChar);
			if (Path.IsPathRooted(normalized)) return normalized;
			if (string.IsNullOrWhiteSpace(root)) return normalized;
			return Path.GetFullPath(Path.Combine(root, normalized));
		}

		/// <summary>返回 path 相对 root 的路径；不在 root 内部时返回原绝对路径。</summary>
		public static string Relative(string root, string path)
		{
			if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path)) return ToSlashes(path);
			try
			{
				string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, '/');
				string fullPath = Path.GetFullPath(path);
				if (!fullRoot.EndsWith("\\") && !fullRoot.EndsWith("/")) fullRoot += Path.DirectorySeparatorChar;
				if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
				{
					return ToSlashes(fullPath.Substring(fullRoot.Length));
				}
			}
			catch
			{
				// 路径非法时退回原值，交给校验环节提示。
			}
			return ToSlashes(path);
		}

		/// <summary>去掉开头的 "./"。</summary>
		public static string TrimDotSlash(string path)
		{
			if (path == null) return "";
			string p = path.Trim();
			while (p.StartsWith("./")) p = p.Substring(2);
			return p;
		}

		/// <summary>文件夹 → 目录；文件 → 文件所在目录。</summary>
		public static string DirectoryOf(string path, bool isDirectory)
		{
			if (string.IsNullOrWhiteSpace(path)) return "";
			if (isDirectory) return path;
			return Path.GetDirectoryName(path) ?? "";
		}
	}
}
