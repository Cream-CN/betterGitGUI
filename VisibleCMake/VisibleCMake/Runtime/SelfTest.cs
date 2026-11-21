using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using VisibleCMake.CodeGen;
using VisibleCMake.Model;

namespace VisibleCMake.Runtime
{
	/// <summary>
	/// 无界面自检（VisibleCMake.exe --selftest）：
	/// 建一个临时工程，生成 CMakeLists.txt 与 Makefile、存读 *.vcmake、再把 CMakeLists 反导回来，
	/// 最后在环境里真的调用 make -n 做语法检查。结果写日志并作为退出码返回。
	/// </summary>
	public static class SelfTest
	{
		public static bool Run(string logPath)
		{
			List<string> lines = new List<string>();
			bool passed = true;

			string directory = Path.Combine(Path.GetTempPath(), "VisibleCMakeSelfTest");
			string log = string.IsNullOrWhiteSpace(logPath)
				? Path.Combine(Path.GetTempPath(), "visiblecmake-selftest.log")
				: logPath;

			lines.Add("VisibleCMake 自检 — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			lines.Add("工作目录：" + directory);
			lines.Add("");

			try
			{
				PrepareSample(directory);

				ProjectModel model = BuildSampleModel(directory);
				ValidationResult validation = ModelValidator.Validate(model);
				Check(lines, ref passed, "模型校验没有错误", !validation.HasErrors, validation.Summary);

				string cmake = CmakeGenerator.Generate(model);
				Check(lines, ref passed, "CMakeLists.txt 含 cmake_minimum_required",
					cmake.Contains("cmake_minimum_required(VERSION 3.20)"), "");
				Check(lines, ref passed, "CMakeLists.txt 含 add_executable",
					cmake.Contains("add_executable(SelfTestApp"), "");
				Check(lines, ref passed, "CMakeLists.txt 含静态库目标",
					cmake.Contains("add_library(core STATIC"), "");
				Check(lines, ref passed, "CMakeLists.txt 把目标名解析成链接项",
					cmake.Contains("target_link_libraries(SelfTestApp PRIVATE") && cmake.Contains("core"), "");

				string makefile = MakefileGenerator.Generate(model);
				Check(lines, ref passed, "Makefile 含 .PHONY 与 all",
					makefile.Contains(".PHONY:") && makefile.Contains("all:"), "");
				Check(lines, ref passed, "Makefile 含编译与链接规则",
					makefile.Contains("-MMD -MP -c") && makefile.Contains("-o \"$@\""), "");
				Check(lines, ref passed, "Makefile 引用了静态库产物",
					makefile.Contains("$(LIB_DIR)/libcore.a"), "");
				Check(lines, ref passed, "Makefile 含 clean 规则", makefile.Contains("clean:"), "");

				WriteFile(Path.Combine(directory, "CMakeLists.txt"), cmake);
				WriteFile(Path.Combine(directory, "Makefile"), makefile);

				// 存读往返
				string projectFile = Path.Combine(directory, "selftest.vcmake");
				ModelStore.Save(model, projectFile);
				ProjectModel loaded = ModelStore.Load(projectFile);
				Check(lines, ref passed, "*.vcmake 读回一致",
					loaded != null && string.Equals(loaded.Name, model.Name, StringComparison.Ordinal)
					&& loaded.Targets.Count == model.Targets.Count
					&& loaded.Targets[0].Sources.Count == model.Targets[0].Sources.Count,
					loaded == null ? "读回为空" : loaded.Name + " / " + loaded.Targets.Count + " 个目标");

				// 反导入
				ProjectModel imported = CmakeImporter.Import(cmake, directory);
				Check(lines, ref passed, "导入 CMakeLists.txt 得到同名工程",
					imported != null && string.Equals(imported.Name, model.Name, StringComparison.Ordinal),
					imported == null ? "导入失败" : imported.Name);
				Check(lines, ref passed, "导入后目标数量一致",
					imported != null && imported.Targets.Count == model.Targets.Count,
					imported == null ? "" : imported.Targets.Count + " 个目标 vs 原 " + model.Targets.Count + " 个");
				Check(lines, ref passed, "导入后源文件没有丢",
					imported != null && imported.Targets.Count > 0 && imported.Targets[0].Sources.Count > 0,
					imported == null || imported.Targets.Count == 0 ? "" : string.Join("、", imported.Targets[0].Sources.ToArray()));

				// 真的让 make 解析一遍（-n 只打印不执行）
				ToolchainInfo toolchain = ToolchainInfo.Detect(null);
				if (toolchain.HasMake)
				{
					ProcessResult result = ProcessRunner.Run(toolchain.MakePath, "-n -f Makefile all", directory, null, CancellationToken.None, 30000);
					string output = result.Output ?? "";
					bool quotedIssue = output.Contains("*** missing separator") || output.Contains("No rule to make target");
					Check(lines, ref passed, "make -n 能解析生成的 Makefile",
						result.Started && result.ExitCode == 0 && !quotedIssue,
						"退出码 " + result.ExitCode + (result.Error.Length > 0 ? "，" + result.Error : ""));
					if (!result.Success && output.Length > 0)
					{
						lines.Add("---- make 输出 ----");
						lines.Add(FirstLines(output, 12));
					}
					lines.Add("make：" + toolchain.MakePath);
				}
				else
				{
					lines.Add("（跳过）本机没有 GNU make，未做 make -n 检查。");
				}

				// CMake 配置：仅作信息记录，缺编译器不算失败
				if (toolchain.HasCmake)
				{
					string buildDirectory = Path.Combine(directory, "build-check");
					if (Directory.Exists(buildDirectory)) DeleteDirectory(buildDirectory);
					ProcessResult configure = ProcessRunner.Run(
						toolchain.CmakePath,
						"-S \"" + directory + "\" -B \"" + buildDirectory + "\"",
						directory, null, CancellationToken.None, 60000);
					lines.Add("cmake 配置退出码：" + configure.ExitCode + (configure.Success ? "（成功）" : "（不影响自检结论：可能只是本机没有编译器）"));
					Check(lines, ref passed, "cmake 能识别生成的 CMakeLists.txt",
						configure.Started && !ContainsAny(configure.Output, "Parse error", "Error while processing"),
						FirstLines(configure.Output, 6));
					lines.Add("cmake：" + toolchain.CmakePath);
				}
				else
				{
					lines.Add("（跳过）本机没有 CMake，未做 cmake 配置检查。");
				}
			}
			catch (Exception ex)
			{
				passed = false;
				lines.Add("自检异常：" + ex.ToString());
			}

			lines.Add("");
			lines.Add(passed ? "结论：全部通过" : "结论：存在失败项");

			try
			{
				FileInfo info = new FileInfo(log);
				if (info.Directory != null && !info.Directory.Exists) info.Directory.Create();
				WriteFile(log, string.Join("\r\n", lines.ToArray()) + "\r\n");
			}
			catch { }

			return passed;
		}

		// ---------------- 样本 ----------------

		private static void PrepareSample(string directory)
		{
			if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
			if (!Directory.Exists(Path.Combine(directory, "src"))) Directory.CreateDirectory(Path.Combine(directory, "src"));
			if (!Directory.Exists(Path.Combine(directory, "include"))) Directory.CreateDirectory(Path.Combine(directory, "include"));

			WriteFile(Path.Combine(directory, "main.cpp"),
				"#include <iostream>\n#include \"core.h\"\n\nint main() {\n    std::cout << core_value() << std::endl;\n    return 0;\n}\n");
			WriteFile(Path.Combine(directory, "src", "core.cpp"),
				"#include \"core.h\"\n\nint core_value() { return 42; }\n");
			WriteFile(Path.Combine(directory, "include", "core.h"),
				"#pragma once\n\nint core_value();\n");
		}

		private static ProjectModel BuildSampleModel(string directory)
		{
			ProjectModel model = ProjectModel.CreateDefault("SelfTestApp", directory, "可执行文件", "17");
			model.Version = "1.0.0";

			TargetModel app = model.Targets[0];
			app.Name = "SelfTestApp";
			app.Sources.Add("main.cpp");
			app.IncludeDirectories.Add("include");
			app.Definitions.Add("SELFTEST=1");
			app.LinkLibraries.Add("core");

			TargetModel core = new TargetModel();
			core.Name = "core";
			core.Kind = TargetKind.StaticLibrary;
			core.Sources.Add("src/core.cpp");
			core.IncludeDirectories.Add("include");
			model.Targets.Add(core);

			TargetModel headers = new TargetModel();
			headers.Name = "core_headers";
			headers.Kind = TargetKind.InterfaceLibrary;
			headers.IncludeDirectories.Add("include");
			model.Targets.Add(headers);

			model.GlobalIncludeDirectories.Add("include");
			return model;
		}

		private static void Check(List<string> lines, ref bool passed, string title, bool condition, string detail)
		{
			lines.Add((condition ? "[通过] " : "[失败] ") + title + (string.IsNullOrWhiteSpace(detail) ? "" : "　—　" + detail));
			if (!condition) passed = false;
		}

		private static void WriteFile(string path, string content)
		{
			File.WriteAllText(path, content, new UTF8Encoding(false));
		}

		private static void DeleteDirectory(string path)
		{
			try
			{
				if (!Directory.Exists(path)) return;
				foreach (string file in Directory.GetFiles(path)) File.Delete(file);
				foreach (string sub in Directory.GetDirectories(path)) DeleteDirectory(sub);
				Directory.Delete(path);
			}
			catch { }
		}

		private static bool ContainsAny(string text, params string[] needles)
		{
			if (string.IsNullOrEmpty(text)) return false;
			foreach (string needle in needles)
			{
				if (text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
			}
			return false;
		}

		private static string FirstLines(string text, int count)
		{
			if (string.IsNullOrWhiteSpace(text)) return "";
			string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			StringBuilder sb = new StringBuilder();
			int written = 0;
			foreach (string line in lines)
			{
				if (line.Trim().Length == 0) continue;
				if (written >= count) break;
				if (written > 0) sb.Append('\n');
				sb.Append(line.TrimEnd());
				written++;
			}
			return sb.ToString();
		}
	}
}
