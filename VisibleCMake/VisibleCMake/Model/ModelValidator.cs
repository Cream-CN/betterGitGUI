using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VisibleCMake.Model
{
	public class ValidationResult
	{
		public ValidationResult()
		{
			Errors = new List<string>();
			Warnings = new List<string>();
		}

		public List<string> Errors { get; private set; }
		public List<string> Warnings { get; private set; }

		public bool HasErrors
		{
			get { return Errors.Count > 0; }
		}

		public bool HasWarnings
		{
			get { return Warnings.Count > 0; }
		}

		public string Summary
		{
			get
			{
				if (HasErrors) return "有 " + Errors.Count + " 个错误，需要先修正";
				if (HasWarnings) return "有 " + Warnings.Count + " 条提示";
				return "配置检查通过";
			}
		}

		public IEnumerable<string> All
		{
			get
			{
				foreach (string error in Errors) yield return "错误：" + error;
				foreach (string warning in Warnings) yield return "提示：" + warning;
			}
		}
	}

	/// <summary>生成前的配置检查：把明显的错误挡在 CMake / make 之前。</summary>
	public static class ModelValidator
	{
		private static readonly Regex TargetNamePattern = new Regex(@"^[A-Za-z0-9_][A-Za-z0-9_.+-]*$", RegexOptions.Compiled);

		public static ValidationResult Validate(ProjectModel model)
		{
			ValidationResult result = new ValidationResult();
			if (model == null)
			{
				result.Errors.Add("没有工程数据。");
				return result;
			}

			if (string.IsNullOrWhiteSpace(model.Name)) result.Errors.Add("工程名不能为空。");
			else if (!TargetNamePattern.IsMatch(model.Name.Trim()))
				result.Errors.Add("工程名 \"" + model.Name + "\" 只能使用字母、数字、下划线、点、加号和减号。");

			if (string.IsNullOrWhiteSpace(model.RootDirectory))
				result.Errors.Add("还没有指定工程目录，生成的构建文件将没有地方可写。");
			else if (!Directory.Exists(model.RootDirectory))
				result.Warnings.Add("工程目录不存在：" + model.RootDirectory + "（生成文件时会自动创建）。");

			if (!string.IsNullOrWhiteSpace(model.Version) && !Regex.IsMatch(model.Version.Trim(), @"^[0-9]+(\.[0-9]+){0,3}$"))
				result.Warnings.Add("工程版本号 \"" + model.Version + "\" 不是 x.y.z 形式，CMake 的 project(VERSION ...) 可能报错。");

			if (!Regex.IsMatch(model.CmakeMinimumVersion ?? "", @"^[0-9]+\.[0-9]+(\.[0-9]+)?$"))
				result.Warnings.Add("cmake_minimum_required 版本号 \"" + model.CmakeMinimumVersion + "\" 格式不正确，建议写 3.20 这样的形式。");

			if (model.Targets == null || model.Targets.Count == 0)
			{
				result.Warnings.Add("工程里还没有目标，生成的 CMakeLists.txt 只有工程头信息。");
			}

			HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (TargetModel target in model.Targets ?? new List<TargetModel>())
			{
				string label = string.IsNullOrWhiteSpace(target.Name) ? "（未命名目标）" : target.Name;
				if (string.IsNullOrWhiteSpace(target.Name))
				{
					result.Errors.Add("有一个目标没有名字。");
					continue;
				}
				if (!TargetNamePattern.IsMatch(target.Name.Trim()))
					result.Errors.Add("目标名 \"" + target.Name + "\" 只能使用字母、数字、下划线、点、加号和减号。");
				if (!names.Add(target.Name.Trim()))
					result.Errors.Add("目标名 \"" + target.Name + "\" 重复了。");

				if (!target.Enabled) continue;

				if (target.Kind != TargetKind.InterfaceLibrary && target.Sources.Count == 0)
					result.Warnings.Add("目标 \"" + label + "\" 还没有源文件。");

				List<string> missing = new List<string>();
				foreach (string source in target.Sources)
				{
					if (string.IsNullOrWhiteSpace(source)) continue;
					string full = model.Resolve(source);
					if (!File.Exists(full)) missing.Add(source);
				}
				if (missing.Count > 0)
				{
					string preview = string.Join("、", missing.Take(3).ToArray());
					if (missing.Count > 3) preview += " 等 " + missing.Count + " 个";
					result.Warnings.Add("目标 \"" + label + "\" 引用的源文件不存在：" + preview);
				}

				if (target.LinkLibraries.Any(lib => lib.IndexOf(' ') >= 0))
					result.Warnings.Add("目标 \"" + label + "\" 的链接库里有带空格的项，链接时可能出错。");
			}

			List<string> spacedPaths = new List<string>();
			foreach (TargetModel target in model.EnabledTargets)
			{
				foreach (string source in target.Sources)
				{
					if (!string.IsNullOrEmpty(source) && (source.IndexOf(' ') >= 0 || source.IndexOf('$') >= 0))
						spacedPaths.Add(source);
				}
			}
			if (spacedPaths.Count > 0)
				result.Warnings.Add("以下路径包含空格或 $ 符号，GNU make 可能无法正确处理：" + string.Join("、", spacedPaths.Take(3).ToArray()));

			if (string.Equals(model.MakefileFileName, "makefile", StringComparison.OrdinalIgnoreCase))
				result.Warnings.Add("Makefile 文件名建议用 \"Makefile\" 或 \"GNUmakefile\"，便于 make 自动识别。");

			return result;
		}
	}
}
