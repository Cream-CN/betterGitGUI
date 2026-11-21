using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VisibleCMake.Model;

namespace VisibleCMake.CodeGen
{
	/// <summary>
	/// 根据 <see cref="ProjectModel"/> 生成 CMakeLists.txt。
	/// 生成结果是给人看的、可继续手改的普通 CMake 脚本，不含任何私有约定。
	/// </summary>
	public static class CmakeGenerator
	{
		public static string Generate(ProjectModel model)
		{
			if (model == null) throw new ArgumentNullException("model");
			model.EnsureDefaults();

			StringBuilder sb = new StringBuilder();
			WriteHeader(sb, model);
			WriteProject(sb, model);
			WriteOptions(sb, model);
			foreach (TargetModel target in model.Targets.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Name)))
			{
				WriteTarget(sb, model, target);
			}
			WriteInstall(sb, model);
			WriteExtraCode(sb, model);
			return sb.ToString().Replace("\r\n", "\n");
		}

		// ---------------- 各部分 ----------------

		private static void WriteHeader(StringBuilder sb, ProjectModel model)
		{
			sb.Append("# ===========================================================================\n");
			sb.Append("#  CMakeLists.txt — 由 VisibleCMake 生成\n");
			sb.Append("#  工程：" + model.Name + (string.IsNullOrWhiteSpace(model.Version) ? "" : "（版本 " + model.Version.Trim() + "）") + "\n");
			sb.Append("#  如需改动，建议在 VisibleCMake 的图形界面里修改后重新生成；\n");
			sb.Append("#  直接手改本文件也可以，下次生成会覆盖掉手改的内容。\n");
			sb.Append("# ===========================================================================\n");
			sb.Append("\n");
		}

		private static void WriteProject(StringBuilder sb, ProjectModel model)
		{
			sb.Append("cmake_minimum_required(VERSION " + SafeVersion(model.CmakeMinimumVersion) + ")\n");
			sb.Append("\n");

			string languages = "CXX";
			if (NeedsCLanguage(model)) languages = "C CXX";
			string version = string.IsNullOrWhiteSpace(model.Version) ? "" : " VERSION " + model.Version.Trim();
			sb.Append("project(" + SafeToken(model.Name) + version + " LANGUAGES " + languages + ")\n");
			sb.Append("\n");

			if (model.EnableTesting) sb.Append("enable_testing()\n\n");

			if (IsNumeric(model.CppStandard))
			{
				sb.Append("# C++ 标准\n");
				sb.Append("set(CMAKE_CXX_STANDARD " + model.CppStandard + ")\n");
				sb.Append("set(CMAKE_CXX_STANDARD_REQUIRED ON)\n");
				sb.Append("set(CMAKE_CXX_EXTENSIONS OFF)\n");
				sb.Append("\n");
			}
			if (IsNumeric(model.CStandard))
			{
				sb.Append("# C 标准\n");
				sb.Append("set(CMAKE_C_STANDARD " + model.CStandard + ")\n");
				sb.Append("set(CMAKE_C_STANDARD_REQUIRED ON)\n");
				sb.Append("\n");
			}

			sb.Append("# 单配置生成器（MinGW Makefiles / Ninja 等）需要显式给出构建类型\n");
			sb.Append("if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)\n");
			sb.Append("    set(CMAKE_BUILD_TYPE \"" + SafeToken(model.DefaultBuildType) + "\" CACHE STRING \"构建类型\" FORCE)\n");
			sb.Append("    set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS Debug Release RelWithDebInfo MinSizeRel)\n");
			sb.Append("endif()\n");
			sb.Append("\n");

			if (model.GenerateCompileCommands)
			{
				sb.Append("# 生成 compile_commands.json，配合 clangd / 编辑器跳转\n");
				sb.Append("set(CMAKE_EXPORT_COMPILE_COMMANDS ON)\n");
				sb.Append("\n");
			}

			sb.Append("# 输出目录\n");
			sb.Append("set(CMAKE_RUNTIME_OUTPUT_DIRECTORY \"${CMAKE_BINARY_DIR}/" + SafeDir(model.RuntimeOutputDirectory) + "\")\n");
			sb.Append("set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY \"${CMAKE_BINARY_DIR}/" + SafeDir(model.ArchiveOutputDirectory) + "\")\n");
			sb.Append("set(CMAKE_LIBRARY_OUTPUT_DIRECTORY \"${CMAKE_BINARY_DIR}/" + SafeDir(model.ArchiveOutputDirectory) + "\")\n");
			sb.Append("\n");

			if (model.EnableWarnings)
			{
				sb.Append("# 常用编译警告\n");
				sb.Append("if(MSVC)\n");
				sb.Append("    add_compile_options(/W4)\n");
				sb.Append("else()\n");
				sb.Append("    add_compile_options(-Wall -Wextra)\n");
				sb.Append("endif()\n");
				sb.Append("\n");
			}

			if (model.GlobalIncludeDirectories.Count > 0)
			{
				sb.Append("# 全局包含目录（对所有目标生效）\n");
				sb.Append("include_directories(\n");
				AppendItems(sb, model.GlobalIncludeDirectories, "    ");
				sb.Append(")\n\n");
			}
		}

		private static void WriteOptions(StringBuilder sb, ProjectModel model)
		{
			// 显式给出的全局编译定义与编译选项（target_* 只能按目标设置，这里用目录级变量统一加上）
			if (model.GlobalDefinitions.Count > 0)
			{
				sb.Append("# 全局编译定义\n");
				sb.Append("add_compile_definitions(\n");
				AppendItems(sb, model.GlobalDefinitions, "    ");
				sb.Append(")\n\n");
			}
			if (model.GlobalCompileOptions.Count > 0)
			{
				sb.Append("# 全局编译选项\n");
				sb.Append("add_compile_options(\n");
				AppendItems(sb, model.GlobalCompileOptions, "    ");
				sb.Append(")\n\n");
			}
			if (model.GlobalLinkDirectories.Count > 0)
			{
				sb.Append("# 全局链接目录\n");
				sb.Append("link_directories(\n");
				AppendItems(sb, model.GlobalLinkDirectories, "    ");
				sb.Append(")\n\n");
			}
		}

		private static void WriteTarget(StringBuilder sb, ProjectModel model, TargetModel target)
		{
			string name = SafeToken(target.Name);
			sb.Append("# ---------------------------------------------------------------------------\n");
			sb.Append("# 目标：" + name + "（" + target.KindText + "）\n");
			sb.Append("# ---------------------------------------------------------------------------\n");

			switch (target.Kind)
			{
				case TargetKind.Executable:
					sb.Append("add_executable(" + name + (target.WindowsSubsystem ? " WIN32" : "") + "\n");
					AppendSources(sb, target);
					break;
				case TargetKind.StaticLibrary:
					sb.Append("add_library(" + name + " STATIC\n");
					AppendSources(sb, target);
					break;
				case TargetKind.SharedLibrary:
					sb.Append("add_library(" + name + " SHARED\n");
					AppendSources(sb, target);
					break;
				default:
					sb.Append("add_library(" + name + " INTERFACE)\n");
					break;
			}

			if (target.Kind != TargetKind.InterfaceLibrary && target.Headers.Count > 0)
			{
				sb.Append("\n# 头文件（便于 IDE 显示，不参与编译）\n");
				sb.Append("target_sources(" + name + " PRIVATE\n");
				AppendItems(sb, target.Headers, "    ");
				sb.Append(")\n");
			}

			string visibility = target.Kind == TargetKind.InterfaceLibrary ? "INTERFACE" : "PRIVATE";

			if (!string.IsNullOrWhiteSpace(target.OutputName) && !string.Equals(target.EffectiveOutputName, target.Name, StringComparison.Ordinal))
			{
				sb.Append("\nset_target_properties(" + name + " PROPERTIES\n");
				sb.Append("    OUTPUT_NAME \"" + target.EffectiveOutputName + "\"\n");
				sb.Append(")\n");
			}

			List<string> includeDirs = new List<string>(model.GlobalIncludeDirectories);
			if (target.Kind != TargetKind.InterfaceLibrary) includeDirs.AddRange(target.IncludeDirectories);
			else includeDirs.AddRange(target.IncludeDirectories);
			if (includeDirs.Count > 0)
			{
				sb.Append("\ntarget_include_directories(" + name + " " + visibility + "\n");
				AppendItems(sb, includeDirs, "    ");
				sb.Append(")\n");
			}

			if (target.Definitions.Count > 0)
			{
				sb.Append("\ntarget_compile_definitions(" + name + " " + visibility + "\n");
				AppendItems(sb, target.Definitions, "    ");
				sb.Append(")\n");
			}

			if (target.CompileOptions.Count > 0)
			{
				sb.Append("\ntarget_compile_options(" + name + " " + visibility + "\n");
				AppendItems(sb, target.CompileOptions, "    ");
				sb.Append(")\n");
			}

			if (target.LinkDirectories.Count > 0)
			{
				sb.Append("\ntarget_link_directories(" + name + " " + visibility + "\n");
				AppendItems(sb, target.LinkDirectories, "    ");
				sb.Append(")\n");
			}

			List<string> linkItems = new List<string>(model.GlobalLinkLibraries);
			linkItems.AddRange(target.LinkLibraries);
			if (linkItems.Count > 0)
			{
				sb.Append("\ntarget_link_libraries(" + name + " " + visibility + "\n");
				foreach (string item in linkItems.Where(i => !string.IsNullOrWhiteSpace(i)))
					sb.Append("    " + LinkReference(model, item.Trim()) + "\n");
				sb.Append(")\n");
			}

			if (!string.IsNullOrWhiteSpace(target.PostBuildCommands) && target.ProducesBinary)
			{
				sb.Append("\n# 构建后执行的命令\n");
				sb.Append("add_custom_command(TARGET " + name + " POST_BUILD\n");
				foreach (string line in SplitLines(target.PostBuildCommands))
					sb.Append("    COMMAND " + line + "\n");
				sb.Append(")\n");
			}

			if (!string.IsNullOrWhiteSpace(target.ExtraCode) && !IsDefaultPlaceholder(target.ExtraCode))
			{
				sb.Append("\n# 目标 " + name + " 的附加 CMake 代码\n");
				sb.Append(target.ExtraCode.TrimEnd() + "\n");
			}

			sb.Append("\n");
		}

		private static void WriteInstall(StringBuilder sb, ProjectModel model)
		{
			if (!model.InstallRules) return;
			List<TargetModel> binaryTargets = model.EnabledTargets.Where(t => t.ProducesBinary).ToList();
			if (binaryTargets.Count == 0) return;

			sb.Append("# ---------------------------------------------------------------------------\n");
			sb.Append("# 安装规则（cmake --install）\n");
			sb.Append("# ---------------------------------------------------------------------------\n");
			sb.Append("include(GNUInstallDirs)\n");
			sb.Append("install(TARGETS " + string.Join(" ", binaryTargets.Select(t => SafeToken(t.Name)).ToArray()) + "\n");
			sb.Append("    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}\n");
			sb.Append("    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}\n");
			sb.Append("    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}\n");
			sb.Append(")\n\n");
		}

		private static void WriteExtraCode(StringBuilder sb, ProjectModel model)
		{
			if (string.IsNullOrWhiteSpace(model.ExtraCmakeCode) || IsDefaultPlaceholder(model.ExtraCmakeCode)) return;
			sb.Append("# ---------------------------------------------------------------------------\n");
			sb.Append("# 附加 CMake 代码（在图形界面里手工补充）\n");
			sb.Append("# ---------------------------------------------------------------------------\n");
			sb.Append(model.ExtraCmakeCode.TrimEnd() + "\n");
		}

		// ---------------- 工具方法 ----------------

		private static void AppendSources(StringBuilder sb, TargetModel target)
		{
			if (target.Sources.Count == 0)
			{
				sb.Append(")\n");
				return;
			}
			foreach (string source in target.Sources.Where(s => !string.IsNullOrWhiteSpace(s)))
				sb.Append("    " + QuoteFile(source) + "\n");
			sb.Append(")\n");
		}

		private static void AppendItems(StringBuilder sb, IEnumerable<string> items, string indent)
		{
			int written = 0;
			foreach (string item in items)
			{
				if (string.IsNullOrWhiteSpace(item)) continue;
				sb.Append(indent + QuoteFile(item) + "\n");
				written++;
			}
			if (written == 0) sb.Append(indent + "# （空）\n");
		}

		/// <summary>链接项：目标名直接引用；文件路径转成 ${CMAKE_SOURCE_DIR}/...；其它（ws2_32、pthread 等）原样输出。</summary>
		private static string LinkReference(ProjectModel model, string item)
		{
			TargetModel target = model.Targets.FirstOrDefault(t => string.Equals(t.Name, item, StringComparison.OrdinalIgnoreCase));
			if (target != null) return SafeToken(target.Name);

			bool looksLikePath = item.IndexOf('/') >= 0 || item.IndexOf('\\') >= 0
				|| item.EndsWith(".a", StringComparison.OrdinalIgnoreCase)
				|| item.EndsWith(".lib", StringComparison.OrdinalIgnoreCase)
				|| item.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
				|| item.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase);
			if (looksLikePath && !item.StartsWith("${"))
			{
				return "\"${CMAKE_SOURCE_DIR}/" + PathUtil.TrimDotSlash(PathUtil.ToSlashes(item)) + "\"";
			}
			return QuoteIfNeeded(item);
		}

		/// <summary>路径统一用 "/"；带空格或特殊字符时加引号。</summary>
		public static string QuoteFile(string path)
		{
			string normalized = PathUtil.TrimDotSlash(PathUtil.ToSlashes(path.Trim()));
			return QuoteIfNeeded(normalized);
		}

		private static string QuoteIfNeeded(string value)
		{
			if (value.Length == 0) return "\"\"";
			bool needsQuote = false;
			foreach (char c in value)
			{
				if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == ';' || c == '#' || c == '"') needsQuote = true;
			}
			return needsQuote ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;
		}

		private static string SafeToken(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return "app";
			StringBuilder sb = new StringBuilder();
			foreach (char c in value.Trim())
			{
				if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '+' || c == '.') sb.Append(c);
			}
			return sb.Length == 0 ? "app" : sb.ToString();
		}

		private static string SafeVersion(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return "3.20";
			return value.Trim();
		}

		private static string SafeDir(string value)
		{
			string dir = PathUtil.TrimDotSlash(PathUtil.ToSlashes((value ?? "").Trim()));
			return dir.Length == 0 ? "." : dir;
		}

		private static bool IsNumeric(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return false;
			foreach (char c in value.Trim())
			{
				if (!char.IsDigit(c)) return false;
			}
			return true;
		}

		private static bool NeedsCLanguage(ProjectModel model)
		{
			if (IsNumeric(model.CStandard)) return true;
			foreach (TargetModel target in model.Targets)
			{
				foreach (string source in target.Sources)
				{
					if (source != null && source.EndsWith(".c", StringComparison.OrdinalIgnoreCase)) return true;
				}
			}
			return false;
		}

		public static IEnumerable<string> SplitLines(string text)
		{
			if (text == null) yield break;
			foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
			{
				string trimmed = line.Trim();
				if (trimmed.Length > 0) yield return trimmed;
			}
		}

		private static bool IsDefaultPlaceholder(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return true;
			string trimmed = text.Trim();
			return trimmed == ProjectModel.DefaultExtraCode.Trim() || trimmed == ProjectModel.DefaultExtraMakeCode.Trim();
		}
	}
}
