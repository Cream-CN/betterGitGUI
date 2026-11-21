using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VisibleCMake.Model;

namespace VisibleCMake.CodeGen
{
	/// <summary>
	/// 根据 <see cref="ProjectModel"/> 生成一份独立的 GNU Makefile。
	/// 和 CMake 那条线互不依赖：没有装 CMake 的机器上用 make 也能直接构建。
	/// 生成结果是普通 Makefile，规则、变量都是通用写法。
	/// </summary>
	public static class MakefileGenerator
	{
		public static string Generate(ProjectModel model)
		{
			if (model == null) throw new ArgumentNullException("model");
			model.EnsureDefaults();

			StringBuilder sb = new StringBuilder();
			WriteHeader(sb, model);
			WriteToolchain(sb, model);
			WriteDirectories(sb, model);
			WriteCommonFlags(sb, model);

			foreach (TargetModel target in model.EnabledTargets)
			{
				WriteTargetVariables(sb, model, target);
			}

			WritePhonyAndAll(sb, model);

			foreach (TargetModel target in model.EnabledTargets)
			{
				WriteTargetRules(sb, model, target);
			}

			WriteDirectoryRules(sb, model);
			WriteRunTarget(sb, model);
			WriteClean(sb, model);
			WriteDepIncludes(sb, model);
			WriteExtraRules(sb, model);

			return sb.ToString().Replace("\r\n", "\n");
		}

		// ---------------- 头部 ----------------

		private static void WriteHeader(StringBuilder sb, ProjectModel model)
		{
			sb.Append("# ===========================================================================\n");
			sb.Append("#  Makefile — 由 VisibleCMake 生成\n");
			sb.Append("#  工程：" + model.Name + (string.IsNullOrWhiteSpace(model.Version) ? "" : "（版本 " + model.Version.Trim() + "）") + "\n");
			sb.Append("#\n");
			sb.Append("#  用法：\n");
			sb.Append("#    make                 构建全部目标\n");
			sb.Append("#    make -j8             并行构建\n");
			sb.Append("#    make BUILD=release   以 Release 方式构建\n");
			sb.Append("#    make clean           清理构建目录\n");
			if (model.PrimaryExecutable != null) sb.Append("#    make run             构建并运行 " + SafeToken(model.PrimaryExecutable.Name) + "\n");
			sb.Append("#\n");
			sb.Append("#  在 VisibleCMake 里修改配置后重新生成会覆盖本文件。\n");
			sb.Append("# ===========================================================================\n\n");
		}

		private static void WriteToolchain(StringBuilder sb, ProjectModel model)
		{
			sb.Append("# ---- 工具链（可以在命令行覆盖，例如 make CXX=clang++）----\n");
			sb.Append("CXX      ?= g++\n");
			sb.Append("CC       ?= gcc\n");
			sb.Append("AR       ?= ar\n\n");

			sb.Append("ifeq ($(OS),Windows_NT)\n");
			sb.Append("  EXE      := .exe\n");
			sb.Append("  SHLIB    := .dll\n");
			sb.Append("  MKDIR    := mkdir\n");
			sb.Append("  RM       := del /q\n");
			sb.Append("  RM_DIR   := rmdir /s /q\n");
			sb.Append("  RM_SILENT:= 2>nul\n");
			sb.Append("  RUN_EXE  := \n");
			sb.Append("else\n");
			sb.Append("  EXE      :=\n");
			sb.Append("  SHLIB    := .so\n");
			sb.Append("  MKDIR    := mkdir -p\n");
			sb.Append("  RM       := rm -f\n");
			sb.Append("  RM_DIR   := rm -rf\n");
			sb.Append("  RM_SILENT:=\n");
			sb.Append("  RUN_EXE  := ./\n");
			sb.Append("endif\n\n");

			sb.Append("BUILD ?= debug\n");
			sb.Append("ifeq ($(BUILD),release)\n");
			sb.Append("  OPT_FLAGS := -O2 -DNDEBUG\n");
			sb.Append("else\n");
			sb.Append("  OPT_FLAGS := -g -O0\n");
			sb.Append("endif\n\n");
		}

		private static void WriteDirectories(StringBuilder sb, ProjectModel model)
		{
			sb.Append("# ---- 目录 ----\n");
			sb.Append("BUILD_DIR := " + MakeDir(model.BuildDirectory, "build") + "\n");
			sb.Append("OBJ_DIR   := $(BUILD_DIR)/obj\n");
			sb.Append("BIN_DIR   := $(BUILD_DIR)/" + MakeDir(model.RuntimeOutputDirectory, "bin") + "\n");
			sb.Append("LIB_DIR   := $(BUILD_DIR)/" + MakeDir(model.ArchiveOutputDirectory, "lib") + "\n\n");
		}

		private static void WriteCommonFlags(StringBuilder sb, ProjectModel model)
		{
			sb.Append("# ---- 通用编译选项 ----\n");

			if (IsNumeric(model.CppStandard))
			{
				sb.Append("CXXSTD   := -std=c++" + model.CppStandard.Trim() + "\n");
			}
			if (IsNumeric(model.CStandard))
			{
				sb.Append("CSTD     := -std=c" + model.CStandard.Trim() + "\n");
			}

			if (model.EnableWarnings) sb.Append("WARNINGS := -Wall -Wextra\n");

			List<string> globalPieces = new List<string>();
			foreach (string include in model.GlobalIncludeDirectories)
			{
				if (!string.IsNullOrWhiteSpace(include)) globalPieces.Add("-I" + QuotePath(MakePath(include)));
			}
			foreach (string definition in model.GlobalDefinitions)
			{
				if (!string.IsNullOrWhiteSpace(definition)) globalPieces.Add("-D" + definition.Trim());
			}

			sb.Append("GLOBAL_FLAGS := " + (globalPieces.Count == 0 ? "" : string.Join(" ", globalPieces.ToArray())) + "\n");

			string extra = JoinOptions(model.GlobalCompileOptions);
			sb.Append("COMMON_CXXFLAGS := $(OPT_FLAGS) $(WARNINGS) $(GLOBAL_FLAGS) $(CXXSTD)");
			if (extra.Length > 0) sb.Append(" " + extra);
			sb.Append("\n");

			sb.Append("COMMON_CFLAGS   := $(OPT_FLAGS) $(WARNINGS) $(GLOBAL_FLAGS) $(CSTD)");
			if (extra.Length > 0) sb.Append(" " + extra);
			sb.Append("\n\n");
		}

		// ---------------- 目标变量 ----------------

		private static void WriteTargetVariables(StringBuilder sb, ProjectModel model, TargetModel target)
		{
			string var = VariableName(target.Name);
			sb.Append("# ---------------------------------------------------------------------------\n");
			sb.Append("#  目标 " + SafeToken(target.Name) + "（" + target.KindText + "）\n");
			sb.Append("# ---------------------------------------------------------------------------\n");

			List<string> sources = CleanSources(target);
			sb.Append(var + "_SOURCES := " + (sources.Count == 0 ? "" : string.Join(" ", sources.Select(s => EscapeMakePath(MakePath(s))).ToArray())) + "\n");

			List<string> objectList = new List<string>();
			foreach (string source in sources) objectList.Add(ObjectPath(target, source));
			sb.Append(var + "_OBJECTS := " + (objectList.Count == 0 ? "" : string.Join(" ", objectList.ToArray())) + "\n");

			string includeFlags = JoinIncludeFlags(model, target);
			string defineFlags = JoinDefinitions(target);
			string compileFlags = JoinOptions(target.CompileOptions);

			sb.Append(var + "_CXXFLAGS := $(COMMON_CXXFLAGS)" + AppendIfAny(includeFlags) + AppendIfAny(defineFlags) + AppendIfAny(compileFlags) + "\n");
			sb.Append(var + "_CFLAGS   := $(COMMON_CFLAGS)" + AppendIfAny(includeFlags) + AppendIfAny(defineFlags) + AppendIfAny(compileFlags) + "\n");

			List<string> linkDirs = new List<string>();
			foreach (string directory in target.LinkDirectories)
			{
				if (!string.IsNullOrWhiteSpace(directory)) linkDirs.Add("-L" + QuotePath(MakePath(directory)));
			}
			foreach (string directory in model.GlobalLinkDirectories)
			{
				if (!string.IsNullOrWhiteSpace(directory)) linkDirs.Add("-L" + QuotePath(MakePath(directory)));
			}
			sb.Append(var + "_LDFLAGS := " + string.Join(" ", linkDirs.ToArray()) + "\n");

			List<string> libraries = new List<string>();
			List<string> archives = new List<string>();
			ResolveLinkLibraries(model, target, libraries, archives);

			sb.Append(var + "_LDLIBS := " + string.Join(" ", libraries.ToArray()) + "\n");
			sb.Append(var + "_DEPS   := " + string.Join(" ", archives.ToArray()) + "\n\n");
		}

		private static void ResolveLinkLibraries(ProjectModel model, TargetModel target, List<string> libraries, List<string> archives)
		{
			List<string> items = new List<string>(model.GlobalLinkLibraries);
			items.AddRange(target.LinkLibraries);

			foreach (string raw in items)
			{
				if (string.IsNullOrWhiteSpace(raw)) continue;
				string item = raw.Trim();

				TargetModel dependency = null;
				foreach (TargetModel candidate in model.Targets)
				{
					if (string.Equals(candidate.Name, item, StringComparison.OrdinalIgnoreCase)) { dependency = candidate; break; }
				}

				if (dependency != null)
				{
					if (!dependency.Enabled) continue;
					if (dependency.Kind == TargetKind.StaticLibrary)
					{
						string archive = "$(LIB_DIR)/lib" + SafeToken(dependency.EffectiveOutputName) + ".a";
						archives.Add(archive);
						libraries.Add(archive);
					}
					else if (dependency.Kind == TargetKind.SharedLibrary)
					{
						string shared = "$(LIB_DIR)/lib" + SafeToken(dependency.EffectiveOutputName) + "$(SHLIB)";
						archives.Add(shared);
						libraries.Add(shared);
					}
					// 接口库没有二进制，跳过
					continue;
				}

				bool looksLikeFile = item.EndsWith(".a", StringComparison.OrdinalIgnoreCase)
					|| item.EndsWith(".lib", StringComparison.OrdinalIgnoreCase)
					|| item.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
					|| item.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)
					|| item.EndsWith(".so.1", StringComparison.OrdinalIgnoreCase)
					|| item.IndexOf('/') >= 0 || item.IndexOf('\\') >= 0;

				if (looksLikeFile)
				{
					libraries.Add(QuotePath(EscapeMakePath(MakePath(item))));
				}
				else if (item.StartsWith("-", StringComparison.Ordinal))
				{
					libraries.Add(item);
				}
				else
				{
					libraries.Add("-l" + item);
				}
			}
		}

		// ---------------- 汇总与规则 ----------------

		private static void WritePhonyAndAll(StringBuilder sb, ProjectModel model)
		{
			List<TargetModel> binaries = model.EnabledTargets.Where(t => t.ProducesBinary).ToList();
			List<string> phony = new List<string>();
			phony.Add("all");
			phony.Add("clean");
			foreach (TargetModel target in model.EnabledTargets) phony.Add(SafeToken(target.Name));
			if (model.PrimaryExecutable != null) phony.Add("run");

			sb.Append(".PHONY: " + string.Join(" ", phony.ToArray()) + "\n\n");

			List<string> outputs = new List<string>();
			foreach (TargetModel target in binaries) outputs.Add(BinaryPath(target));
			sb.Append("all: " + string.Join(" ", outputs.ToArray()) + "\n");
			if (outputs.Count == 0)
			{
				sb.Append("\t@echo \"这个工程还没有可构建的目标，请先在 VisibleCMake 里添加\"\n");
			}
			sb.Append("\n");
		}

		private static void WriteTargetRules(StringBuilder sb, ProjectModel model, TargetModel target)
		{
			string var = VariableName(target.Name);
			string name = SafeToken(target.Name);

			if (!target.ProducesBinary)
			{
				sb.Append("# 接口库（header-only），没有编译产物\n");
				sb.Append(name + ":\n");
				sb.Append("\t@echo \"" + name + " 是接口库，只有头文件，无需构建\"\n\n");
				return;
			}

			// 目标别名：make <名字>
			sb.Append(name + ": " + BinaryPath(target) + "\n\n");

			// 每个目录一条创建规则
			List<string> sourceDirectories = new List<string>();
			foreach (string source in CleanSources(target))
			{
				string directory = ObjectDirectory(target, source);
				if (!sourceDirectories.Contains(directory)) sourceDirectories.Add(directory);
			}

			foreach (string directory in sourceDirectories)
			{
				// 目标根目录由 WriteDirectoryRules 统一生成，这里只补子目录
				if (directory == ObjectRoot(target)) continue;
				sb.Append(directory + ": | " + ObjectRoot(target) + "\n");
				sb.Append("\t@$(MKDIR) \"$@\"\n");
				sb.Append("\n");
			}

			// 每个源文件一条显式规则，便于不同目标使用不同选项
			foreach (string source in CleanSources(target))
			{
				string escapedSource = EscapeMakePath(MakePath(source));
				string objectPath = ObjectPath(target, source);
				bool isC = source.EndsWith(".c", StringComparison.OrdinalIgnoreCase);
				string compiler = isC ? "$(CC)" : "$(CXX)";
				string flags = isC ? ("$(" + var + "_CFLAGS)") : ("$(" + var + "_CXXFLAGS)");

				sb.Append(objectPath + ": " + escapedSource + " | " + ObjectDirectory(target, source) + "\n");
				sb.Append("\t" + compiler + " " + flags + " -MMD -MP -c \"$<\" -o \"$@\"\n");
				sb.Append("\n");
			}

			// 链接（或打包）规则
			string output = BinaryPath(target);
			List<string> prerequisites = new List<string>();
			prerequisites.Add("$(" + var + "_OBJECTS)");
			if (!string.IsNullOrWhiteSpace(target.Name)) prerequisites.Add("$(" + var + "_DEPS)");

			string outputDirectory = (target.Kind == TargetKind.StaticLibrary || target.Kind == TargetKind.SharedLibrary)
				? "$(LIB_DIR)"
				: "$(BIN_DIR)";

			sb.Append(output + ": " + string.Join(" ", prerequisites.ToArray()) + " | " + outputDirectory + "\n");

			switch (target.Kind)
			{
				case TargetKind.StaticLibrary:
					// ar rcs 会就地创建/替换成员，不需要先删文件（make 会把找不到文件的错误也当成“忽略”打印出来）
					sb.Append("\t$(AR) rcs \"$@\" $(%VAR%_OBJECTS)\n".Replace("%VAR%", var));
					break;

				case TargetKind.SharedLibrary:
					sb.Append("\t$(CXX) -shared $(%VAR%_OBJECTS) $(%VAR%_LDFLAGS) $(%VAR%_LDLIBS) -o \"$@\"\n".Replace("%VAR%", var));
					break;

				default:
					sb.Append("\t$(CXX) $(%VAR%_OBJECTS) $(%VAR%_LDFLAGS) $(%VAR%_LDLIBS) -o \"$@\"\n".Replace("%VAR%", var));
					break;
			}

			sb.Append("\t@echo \"  已生成 $@\"\n");

			foreach (string command in CmakeGenerator.SplitLines(target.PostBuildCommands))
			{
				sb.Append("\t" + command + "\n");
			}
			sb.Append("\n");
		}

		private static void WriteDirectoryRules(StringBuilder sb, ProjectModel model)
		{
			sb.Append("# ---- 目录创建 ----\n");
			sb.Append("$(BUILD_DIR):\n");
			sb.Append("\t@$(MKDIR) \"$@\"\n\n");

			sb.Append("$(OBJ_DIR): | $(BUILD_DIR)\n");
			sb.Append("\t@$(MKDIR) \"$@\"\n\n");

			sb.Append("$(BIN_DIR): | $(BUILD_DIR)\n");
			sb.Append("\t@$(MKDIR) \"$@\"\n\n");

			sb.Append("$(LIB_DIR): | $(BUILD_DIR)\n");
			sb.Append("\t@$(MKDIR) \"$@\"\n\n");

			foreach (TargetModel target in model.EnabledTargets)
			{
				if (!target.ProducesBinary) continue;
				sb.Append(ObjectRoot(target) + ": | $(OBJ_DIR)\n");
				sb.Append("\t@$(MKDIR) \"$@\"\n\n");
			}
		}

		private static void WriteRunTarget(StringBuilder sb, ProjectModel model)
		{
			TargetModel executable = model.PrimaryExecutable;
			if (executable == null) return;

			string output = "$(BIN_DIR)/" + SafeToken(executable.EffectiveOutputName) + "$(EXE)";
			sb.Append("run: " + output + "\n");
			sb.Append("\t$(RUN_EXE)\"$<\"\n\n");
		}

		private static void WriteClean(StringBuilder sb, ProjectModel model)
		{
			sb.Append("clean:\n");
			sb.Append("\t-@$(RM_DIR) \"$(BUILD_DIR)\" $(RM_SILENT)\n");
			sb.Append("\t@echo \"  已清理 $(BUILD_DIR)\"\n\n");
		}

		private static void WriteDepIncludes(StringBuilder sb, ProjectModel model)
		{
			bool any = false;
			StringBuilder body = new StringBuilder();
			foreach (TargetModel target in model.EnabledTargets)
			{
				if (!target.ProducesBinary) continue;
				if (CleanSources(target).Count == 0) continue;
				body.Append("-include $(%VAR%_OBJECTS:.o=.d)\n".Replace("%VAR%", VariableName(target.Name)));
				any = true;
			}
			if (!any) return;

			sb.Append("# ---- 头文件依赖（由 -MMD 自动生成）----\n");
			sb.Append(body.ToString());
			sb.Append("\n");
		}

		private static void WriteExtraRules(StringBuilder sb, ProjectModel model)
		{
			if (string.IsNullOrWhiteSpace(model.ExtraMakeCode)) return;
			if (string.Equals(model.ExtraMakeCode.Trim(), ProjectModel.DefaultExtraMakeCode.Trim(), StringComparison.Ordinal)) return;

			sb.Append("# ---------------------------------------------------------------------------\n");
			sb.Append("#  附加 make 规则（在图形界面里手工补充）\n");
			sb.Append("# ---------------------------------------------------------------------------\n");
			sb.Append(model.ExtraMakeCode.TrimEnd() + "\n");
		}

		// ---------------- 路径与变量 ----------------

		private static List<string> CleanSources(TargetModel target)
		{
			List<string> sources = new List<string>();
			foreach (string source in target.Sources)
			{
				if (string.IsNullOrWhiteSpace(source)) continue;
				sources.Add(PathUtil.TrimDotSlash(PathUtil.ToSlashes(source.Trim())));
			}
			return sources;
		}

		private static string BinaryPath(TargetModel target)
		{
			string name = SafeToken(target.EffectiveOutputName);
			switch (target.Kind)
			{
				case TargetKind.StaticLibrary:
					return "$(LIB_DIR)/lib" + name + ".a";
				case TargetKind.SharedLibrary:
					return "$(LIB_DIR)/lib" + name + "$(SHLIB)";
				default:
					return "$(BIN_DIR)/" + name + "$(EXE)";
			}
		}

		private static string ObjectRoot(TargetModel target)
		{
			return "$(OBJ_DIR)/" + VariableName(target.Name);
		}

		private static string ObjectDirectory(TargetModel target, string source)
		{
			int index = source.LastIndexOf('/');
			string directory = index < 0 ? "" : source.Substring(0, index);
			if (directory.Length == 0 || directory == ".") return ObjectRoot(target);
			return ObjectRoot(target) + "/" + directory;
		}

		private static string ObjectPath(TargetModel target, string source)
		{
			return ObjectRoot(target) + "/" + source + ".o";
		}

		private static string VariableName(string targetName)
		{
			string name = SafeToken(targetName);
			StringBuilder sb = new StringBuilder();
			foreach (char c in name)
			{
				if (char.IsLetterOrDigit(c)) sb.Append(c);
				else sb.Append('_');
			}
			if (sb.Length == 0) sb.Append("target");
			return sb.ToString();
		}

		private static string JoinIncludeFlags(ProjectModel model, TargetModel target)
		{
			// 全局包含目录已经写进 GLOBAL_FLAGS / COMMON_*FLAGS；目标里重复的也跳过，避免同一路径出现两次 -I
			List<string> flags = new List<string>();
			List<string> seen = new List<string>();
			foreach (string include in model.GlobalIncludeDirectories)
			{
				if (!string.IsNullOrWhiteSpace(include)) seen.Add(MakePath(include));
			}

			foreach (string include in target.IncludeDirectories)
			{
				if (string.IsNullOrWhiteSpace(include)) continue;
				string normalized = MakePath(include);
				bool duplicate = false;
				foreach (string item in seen)
				{
					if (string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)) { duplicate = true; break; }
				}
				if (duplicate) continue;

				seen.Add(normalized);
				flags.Add("-I" + QuotePath(normalized));
			}
			return string.Join(" ", flags.ToArray());
		}

		private static string JoinDefinitions(TargetModel target)
		{
			List<string> flags = new List<string>();
			foreach (string definition in target.Definitions)
			{
				if (!string.IsNullOrWhiteSpace(definition)) flags.Add("-D" + definition.Trim());
			}
			return string.Join(" ", flags.ToArray());
		}

		private static string JoinOptions(IEnumerable<string> options)
		{
			if (options == null) return "";
			List<string> pieces = new List<string>();
			foreach (string option in options)
			{
				if (!string.IsNullOrWhiteSpace(option)) pieces.Add(option.Trim());
			}
			return string.Join(" ", pieces.ToArray());
		}

		private static string AppendIfAny(string value)
		{
			return string.IsNullOrWhiteSpace(value) ? "" : " " + value;
		}

		/// <summary>Makefile 里路径用 "/"；空格用反斜杠转义。</summary>
		public static string EscapeMakePath(string path)
		{
			if (path == null) return "";
			string normalized = PathUtil.TrimDotSlash(PathUtil.ToSlashes(path));
			return normalized.Replace(" ", "\\ ");
		}

		private static string MakePath(string path)
		{
			return PathUtil.TrimDotSlash(PathUtil.ToSlashes((path ?? "").Trim()));
		}

		private static string MakeDir(string path, string fallback)
		{
			string value = MakePath(path);
			return value.Length == 0 ? fallback : value;
		}

		private static string QuotePath(string path)
		{
			if (path == null || path.Length == 0) return "\"\"";
			if (path.IndexOf(' ') >= 0 || path.IndexOf('#') >= 0) return "\"" + path.Replace("\"", "\\\"") + "\"";
			return path;
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
	}
}
