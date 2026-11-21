using System;
using System.IO;
using System.Linq;
using System.Text;
using VisibleCMake.Core;

namespace VisibleCMake.Model
{
	/// <summary>工程文件的读写：*.vcmake 是一份可读的 JSON，记录了图形界面的全部设置。</summary>
	public static class ModelStore
	{
		public const string Extension = ".vcmake";

		public static void Save(ProjectModel model, string path)
		{
			if (model == null) throw new ArgumentNullException("model");
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("保存路径为空。", "path");

			model.EnsureDefaults();

			string directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

			File.WriteAllText(path, MiniJson.Serialize(model), new UTF8Encoding(false));
		}

		public static ProjectModel Load(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("路径为空。", "path");
			if (!File.Exists(path)) throw new FileNotFoundException("找不到工程文件：" + path, path);

			string text = File.ReadAllText(path, Encoding.UTF8);
			ProjectModel model = MiniJson.Deserialize<ProjectModel>(text);
			if (model == null) throw new FormatException("工程文件内容为空或格式不正确。");
			model.EnsureDefaults();

			string fileDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (string.IsNullOrWhiteSpace(model.RootDirectory))
			{
				model.RootDirectory = fileDirectory;
			}
			else
			{
				string root = model.RootDirectory.Replace('/', Path.DirectorySeparatorChar);
				if (!Path.IsPathRooted(root)) model.RootDirectory = Path.GetFullPath(Path.Combine(fileDirectory, root));
			}
			return model;
		}

		/// <summary>在目录里寻找第一个 *.vcmake。</summary>
		public static string FindProjectFile(string directory)
		{
			if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return null;
			try
			{
				string[] files = Directory.GetFiles(directory, "*" + Extension);
				if (files.Length == 0) return null;
				return files.OrderBy(f => f.Length).First();
			}
			catch
			{
				return null;
			}
		}

		/// <summary>工程名 → 文件名（去掉非法字符）。</summary>
		public static string SuggestFileName(ProjectModel model)
		{
			string name = model == null || string.IsNullOrWhiteSpace(model.Name) ? "Project" : model.Name.Trim();
			StringBuilder sb = new StringBuilder();
			foreach (char c in name)
			{
				if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') sb.Append(c);
			}
			if (sb.Length == 0) sb.Append("Project");
			return sb.ToString() + Extension;
		}

		public static string SuggestFilePath(ProjectModel model)
		{
			string root = model == null ? "" : model.RootDirectory;
			return Path.Combine(root ?? "", SuggestFileName(model));
		}
	}
}
