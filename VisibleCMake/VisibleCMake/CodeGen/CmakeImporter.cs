using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VisibleCMake.Model;

namespace VisibleCMake.CodeGen
{
	/// <summary>
	/// 把已有的 CMakeLists.txt 反向读成图形界面里的工程模型（尽力而为的导入）。
	/// 只认常见的工程 / 目标 / target_* 命令；认不出来的语句会原样保留到“附加 CMake 代码”里，
	/// 由使用者确认后再决定去留。
	/// </summary>
	public static class CmakeImporter
	{
		public static ProjectModel ImportFile(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("路径为空。", "path");
			if (!File.Exists(path)) throw new FileNotFoundException("找不到 CMakeLists.txt：" + path, path);

			string text = File.ReadAllText(path, Encoding.UTF8);
			return Import(text, Path.GetDirectoryName(Path.GetFullPath(path)));
		}

		public static ProjectModel Import(string text, string rootDirectory)
		{
			ProjectModel model = new ProjectModel();
			model.RootDirectory = rootDirectory ?? "";
			model.Name = DefaultName(rootDirectory);
			model.ExtraCmakeCode = "";

			List<string> unknownStatements = new List<string>();
			Dictionary<string, TargetModel> targets = new Dictionary<string, TargetModel>(StringComparer.OrdinalIgnoreCase);

			foreach (string statement in ReadStatements(text))
			{
				string command;
				List<string> args;
				if (!SplitStatement(statement, out command, out args)) continue;

				switch (command.ToLowerInvariant())
				{
					case "cmake_minimum_required":
					{
						int index = IndexOf(args, "VERSION");
						if (index >= 0 && index + 1 < args.Count) model.CmakeMinimumVersion = args[index + 1];
						break;
					}

					case "project":
					{
						if (args.Count > 0 && !IsVariable(args[0])) model.Name = args[0];
						int versionIndex = IndexOf(args, "VERSION");
						if (versionIndex >= 0 && versionIndex + 1 < args.Count) model.Version = args[versionIndex + 1];
						break;
					}

					case "set":
					{
						if (args.Count >= 2)
						{
							string name = args[0];
							if (string.Equals(name, "CMAKE_CXX_STANDARD", StringComparison.OrdinalIgnoreCase))
								model.CppStandard = NumericOrEmpty(args[1]);
							else if (string.Equals(name, "CMAKE_C_STANDARD", StringComparison.OrdinalIgnoreCase))
								model.CStandard = NumericOrEmpty(args[1]);
							else if (string.Equals(name, "CMAKE_BUILD_TYPE", StringComparison.OrdinalIgnoreCase))
								model.DefaultBuildType = args[1];
							else if (string.Equals(name, "CMAKE_EXPORT_COMPILE_COMMANDS", StringComparison.OrdinalIgnoreCase))
								model.GenerateCompileCommands = args[1].Equals("ON", StringComparison.OrdinalIgnoreCase);
							else
								unknownStatements.Add(statement);
						}
						break;
					}

					case "enable_testing":
						model.EnableTesting = true;
						break;

					case "include_directories":
					{
						foreach (string dir in CleanArguments(args, new string[] { "SYSTEM", "AFTER", "BEFORE" }))
						{
							if (!IsGeneratorExpression(dir)) model.GlobalIncludeDirectories.Add(NormalizePath(dir));
						}
						break;
					}

					case "add_compile_definitions":
					{
						foreach (string item in CleanArguments(args, null))
						{
							if (!IsGeneratorExpression(item)) model.GlobalDefinitions.Add(item);
						}
						break;
					}

					case "add_compile_options":
					{
						foreach (string item in CleanArguments(args, null))
						{
							if (!IsGeneratorExpression(item)) model.GlobalCompileOptions.Add(item);
						}
						break;
					}

					case "link_directories":
					{
						foreach (string dir in CleanArguments(args, new string[] { "AFTER", "BEFORE" }))
						{
							if (!IsGeneratorExpression(dir)) model.GlobalLinkDirectories.Add(NormalizePath(dir));
						}
						break;
					}

					case "add_executable":
					{
						if (args.Count == 0) break;
						TargetModel target = NewTarget(model, targets, args[0], TargetKind.Executable);
						foreach (string rest in CleanArguments(args.Skip(1).ToList(), new string[] { "WIN32", "MACOSX_BUNDLE", "EXCLUDE_FROM_ALL" }))
						{
							AddSource(target, rest);
						}
						break;
					}

					case "add_library":
					{
						if (args.Count == 0) break;
						TargetKind kind = TargetKind.StaticLibrary;
						int start = 1;
						if (args.Count > 1)
						{
							string keyword = args[1].ToUpperInvariant();
							if (keyword == "SHARED" || keyword == "MODULE") { kind = TargetKind.SharedLibrary; start = 2; }
							else if (keyword == "INTERFACE") { kind = TargetKind.InterfaceLibrary; start = 2; }
							else if (keyword == "STATIC" || keyword == "OBJECT" || keyword == "UNKNOWN") { kind = TargetKind.StaticLibrary; start = 2; }
						}
						TargetModel target = NewTarget(model, targets, args[0], kind);
						foreach (string rest in CleanArguments(args.Skip(start).ToList(), null))
						{
							AddSource(target, rest);
						}
						break;
					}

					case "target_sources":
					{
						TargetModel target = FindTarget(targets, args);
						if (target == null) break;
						foreach (string item in CleanArguments(args.Skip(1).ToList(), VisibilityKeywords))
						{
							AddSource(target, item);
						}
						break;
					}

					case "target_include_directories":
					{
						TargetModel target = FindTarget(targets, args);
						if (target == null) break;
						foreach (string item in CleanArguments(args.Skip(1).ToList(), VisibilityKeywords))
						{
							if (IsGeneratorExpression(item)) continue;
							target.IncludeDirectories.Add(NormalizePath(item));
						}
						break;
					}

					case "target_compile_definitions":
					{
						TargetModel target = FindTarget(targets, args);
						if (target == null) break;
						foreach (string item in CleanArguments(args.Skip(1).ToList(), VisibilityKeywords))
						{
							if (IsGeneratorExpression(item)) continue;
							target.Definitions.Add(item);
						}
						break;
					}

					case "target_compile_options":
					{
						TargetModel target = FindTarget(targets, args);
						if (target == null) break;
						foreach (string item in CleanArguments(args.Skip(1).ToList(), VisibilityKeywords))
						{
							if (IsGeneratorExpression(item)) continue;
							target.CompileOptions.Add(item);
						}
						break;
					}

					case "target_link_directories":
					{
						TargetModel target = FindTarget(targets, args);
						if (target == null) break;
						foreach (string item in CleanArguments(args.Skip(1).ToList(), VisibilityKeywords))
						{
							if (IsGeneratorExpression(item)) continue;
							target.LinkDirectories.Add(NormalizePath(item));
						}
						break;
					}

					case "target_link_libraries":
					{
						TargetModel target = FindTarget(targets, args);
						if (target == null) break;
						foreach (string item in CleanArguments(args.Skip(1).ToList(), VisibilityKeywords))
						{
							if (string.Equals(item, "debug", StringComparison.OrdinalIgnoreCase)
								|| string.Equals(item, "optimized", StringComparison.OrdinalIgnoreCase)
								|| string.Equals(item, "general", StringComparison.OrdinalIgnoreCase)) continue;
							if (IsGeneratorExpression(item)) continue;
							target.LinkLibraries.Add(item);
						}
						break;
					}

					case "set_target_properties":
					{
						if (args.Count < 3) break;
						TargetModel target = null;
						targets.TryGetValue(args[0], out target);
						if (target == null) break;

						int propertiesIndex = IndexOf(args, "PROPERTIES");
						if (propertiesIndex < 0) break;
						for (int i = propertiesIndex + 1; i + 1 < args.Count; i += 2)
						{
							string property = args[i].ToUpperInvariant();
							string value = args[i + 1];
							if (property == "OUTPUT_NAME") target.OutputName = value;
							else if (property == "WIN32_EXECUTABLE")
								target.WindowsSubsystem = value.Equals("ON", StringComparison.OrdinalIgnoreCase)
									|| value.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
						}
						break;
					}

					case "add_custom_command":
					{
						int targetIndex = IndexOf(args, "TARGET");
						int postIndex = IndexOf(args, "POST_BUILD");
						if (targetIndex < 0 || postIndex < 0) { unknownStatements.Add(statement); break; }

						TargetModel target = null;
						if (targetIndex + 1 < args.Count) targets.TryGetValue(args[targetIndex + 1], out target);
						if (target == null) break;

						List<string> commands = new List<string>();
						StringBuilder current = new StringBuilder();
						foreach (string token in args.Skip(postIndex + 1))
						{
							if (string.Equals(token, "COMMAND", StringComparison.OrdinalIgnoreCase))
							{
								if (current.Length > 0) commands.Add(current.ToString());
								current.Length = 0;
								continue;
							}
							if (string.Equals(token, "WORKING_DIRECTORY", StringComparison.OrdinalIgnoreCase)
								|| string.Equals(token, "COMMENT", StringComparison.OrdinalIgnoreCase)
								|| string.Equals(token, "VERBATIM", StringComparison.OrdinalIgnoreCase))
							{
								break;
							}
							if (current.Length > 0) current.Append(' ');
							current.Append(token);
						}
						if (current.Length > 0) commands.Add(current.ToString());
						target.PostBuildCommands = string.Join("\n", commands.ToArray());
						break;
					}

					case "install":
						if (args.Count > 0 && string.Equals(args[0], "TARGETS", StringComparison.OrdinalIgnoreCase)) model.InstallRules = true;
						break;

					case "include":
					case "message":
					case "endif":
					case "else":
					case "endforeach":
					case "endfunction":
					case "endwhile":
					case "endmacro":
						// include(GNUInstallDirs) 这类模板语句不需要保留
						if (command.Equals("include", StringComparison.OrdinalIgnoreCase) && args.Count > 0
							&& args[0].StartsWith("GNUInstallDirs", StringComparison.OrdinalIgnoreCase)) break;
						unknownStatements.Add(statement);
						break;

					default:
						unknownStatements.Add(statement);
						break;
				}
			}

			model.Targets = targets.Values.ToList();
			model.EnsureDefaults();

			if (unknownStatements.Count > 0)
			{
				StringBuilder extra = new StringBuilder();
				extra.Append("# ---- 以下语句由 VisibleCMake 从原 CMakeLists.txt 原样保留 ----\n");
				extra.Append("# ---- 它们可能和上面的图形化配置重复，请按需删减 ----\n");
				foreach (string statement in unknownStatements)
				{
					extra.Append(statement.TrimEnd() + "\n\n");
				}
				model.ExtraCmakeCode = extra.ToString().TrimEnd();
			}

			return model;
		}

		// ---------------- 解析工具 ----------------

		private static readonly string[] VisibilityKeywords = new string[] { "PRIVATE", "PUBLIC", "INTERFACE", "SYSTEM", "BEFORE", "AFTER", "APPEND", "PREPEND", "SOURCE", "DEPENDS" };

		/// <summary>把文本切成一条条完整的命令（跨行的命令会被拼起来，注释被忽略）。</summary>
		private static IEnumerable<string> ReadStatements(string text)
		{
			if (text == null) yield break;

			string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			StringBuilder buffer = new StringBuilder();
			int depth = 0;

			foreach (string line in lines)
			{
				string stripped = StripComment(line);
				if (buffer.Length == 0 && stripped.Trim().Length == 0) continue;

				buffer.Append(stripped).Append('\n');
				depth += ParenthesisDelta(stripped);

				if (depth <= 0)
				{
					string statement = buffer.ToString().Trim();
					buffer.Length = 0;
					depth = 0;
					if (statement.Length > 0) yield return statement;
				}
			}

			if (buffer.ToString().Trim().Length > 0) yield return buffer.ToString().Trim();
		}

		private static string StripComment(string line)
		{
			bool quoted = false;
			for (int i = 0; i < line.Length; i++)
			{
				char c = line[i];
				if (c == '"') quoted = !quoted;
				else if (c == '#' && !quoted) return line.Substring(0, i);
			}
			return line;
		}

		private static int ParenthesisDelta(string line)
		{
			int delta = 0;
			bool quoted = false;
			for (int i = 0; i < line.Length; i++)
			{
				char c = line[i];
				if (c == '"') quoted = !quoted;
				else if (!quoted)
				{
					if (c == '(') delta++;
					else if (c == ')') delta--;
				}
			}
			return delta;
		}

		private static bool SplitStatement(string statement, out string command, out List<string> args)
		{
			command = "";
			args = new List<string>();

			int open = statement.IndexOf('(');
			if (open <= 0) return false;

			command = statement.Substring(0, open).Trim();
			if (command.Length == 0) return false;
			if (!char.IsLetter(command[0])) return false;

			int close = statement.LastIndexOf(')');
			string inner = close > open
				? statement.Substring(open + 1, close - open - 1)
				: statement.Substring(open + 1);

			args = Tokenize(inner);
			return true;
		}

		private static List<string> Tokenize(string text)
		{
			List<string> tokens = new List<string>();
			if (string.IsNullOrEmpty(text)) return tokens;

			StringBuilder current = new StringBuilder();
			bool quoted = false;

			foreach (char c in text)
			{
				if (quoted)
				{
					if (c == '"') quoted = false;
					else current.Append(c);
					continue;
				}

				if (c == '"')
				{
					quoted = true;
					continue;
				}

				if (char.IsWhiteSpace(c) || c == ';')
				{
					if (current.Length > 0)
					{
						tokens.Add(current.ToString());
						current.Length = 0;
					}
					continue;
				}

				current.Append(c);
			}

			if (current.Length > 0) tokens.Add(current.ToString());
			return tokens;
		}

		private static List<string> CleanArguments(List<string> args, string[] skipKeywords)
		{
			List<string> result = new List<string>();
			if (args == null) return result;
			foreach (string arg in args)
			{
				if (string.IsNullOrWhiteSpace(arg)) continue;
				if (skipKeywords != null)
				{
					bool skip = false;
					foreach (string keyword in skipKeywords)
					{
						if (string.Equals(arg, keyword, StringComparison.OrdinalIgnoreCase)) { skip = true; break; }
					}
					if (skip) continue;
				}
				result.Add(arg);
			}
			return result;
		}

		private static int IndexOf(List<string> args, string keyword)
		{
			for (int i = 0; i < args.Count; i++)
			{
				if (string.Equals(args[i], keyword, StringComparison.OrdinalIgnoreCase)) return i;
			}
			return -1;
		}

		private static TargetModel NewTarget(ProjectModel model, Dictionary<string, TargetModel> targets, string name, TargetKind kind)
		{
			if (string.IsNullOrWhiteSpace(name)) name = "target" + (targets.Count + 1);

			TargetModel target;
			if (!targets.TryGetValue(name, out target))
			{
				target = new TargetModel();
				target.Name = name;
				target.Kind = kind;
				targets[name] = target;
			}
			else
			{
				target.Kind = kind;
			}
			return target;
		}

		private static TargetModel FindTarget(Dictionary<string, TargetModel> targets, List<string> args)
		{
			if (args == null || args.Count == 0) return null;
			TargetModel target;
			if (targets.TryGetValue(args[0], out target)) return target;
			return null;
		}

		private static void AddSource(TargetModel target, string item)
		{
			if (string.IsNullOrWhiteSpace(item)) return;
			if (IsGeneratorExpression(item)) return;

			string normalized = NormalizePath(item);
			string extension = Path.GetExtension(normalized).ToLowerInvariant();
			bool isHeader = extension == ".h" || extension == ".hpp" || extension == ".hxx"
				|| extension == ".inl" || extension == ".ipp";

			if (isHeader)
			{
				if (!target.Headers.Contains(normalized)) target.Headers.Add(normalized);
			}
			else
			{
				if (!target.Sources.Contains(normalized)) target.Sources.Add(normalized);
			}
		}

		private static string NormalizePath(string value)
		{
			if (value == null) return "";
			string path = value.Trim().Trim('"');
			path = path.Replace("${CMAKE_CURRENT_SOURCE_DIR}/", "")
				.Replace("${CMAKE_SOURCE_DIR}/", "")
				.Replace("${PROJECT_SOURCE_DIR}/", "")
				.Replace("${PROJECT_SOURCE_DIR}", "");
			path = PathUtil.TrimDotSlash(PathUtil.ToSlashes(path));
			return path;
		}

		private static bool IsGeneratorExpression(string value)
		{
			return value != null && value.IndexOf("$<", StringComparison.Ordinal) >= 0;
		}

		private static bool IsVariable(string value)
		{
			return value != null && value.StartsWith("${", StringComparison.Ordinal);
		}

		private static string NumericOrEmpty(string value)
		{
			if (string.IsNullOrWhiteSpace(value)) return "";
			foreach (char c in value.Trim())
			{
				if (!char.IsDigit(c)) return "";
			}
			return value.Trim();
		}

		private static string DefaultName(string rootDirectory)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(rootDirectory))
				{
					string name = Path.GetFileName(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
					if (!string.IsNullOrWhiteSpace(name)) return name;
				}
			}
			catch { }
			return "ImportedProject";
		}
	}
}
