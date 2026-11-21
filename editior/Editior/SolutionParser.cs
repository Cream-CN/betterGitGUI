using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Editior
{
	/// <summary>解决方案树节点：文件夹、项目或文件。</summary>
	public class SolutionNode
	{
		public string Name = "";
		public string Path;                 // 绝对路径；解决方案根节点为 null
		public bool IsFile;
		public List<SolutionNode> Children = new List<SolutionNode>();
	}

	/// <summary>用微软官方 NuGet 包读取 .sln / .slnx / .csproj / .proj。</summary>
	public static class SolutionParser
	{
		public static bool IsSolutionFile(string path)
		{
			string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
			return ext == ".sln" || ext == ".slnx";
		}

		public static SolutionNode LoadSolution(string solutionPath)
		{
			SolutionNode root = new SolutionNode();
			root.Name = System.IO.Path.GetFileName(solutionPath);
			root.Path = System.IO.Path.GetDirectoryName(solutionPath);

			string ext = System.IO.Path.GetExtension(solutionPath).ToLowerInvariant();
			if (ext == ".slnx") LoadSlnx(solutionPath, root);
			else LoadSln(solutionPath, root);

			Sort(root);
			return root;
		}

		private static void Sort(SolutionNode node)
		{
			node.Children.Sort(delegate (SolutionNode a, SolutionNode b)
			{
				if (a.IsFile != b.IsFile) return a.IsFile ? 1 : -1;
				return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
			});
			foreach (SolutionNode child in node.Children) Sort(child);
		}

		/// <summary>.slnx 走 Microsoft.VisualStudio.SolutionPersistence。</summary>
		private static void LoadSlnx(string path, SolutionNode root)
		{
			SolutionModel model;
			using (FileStream stream = File.OpenRead(path))
			{
				model = SolutionSerializers.SlnXml.OpenAsync(stream, CancellationToken.None)
					.GetAwaiter().GetResult();
			}
			if (model == null) return;

			string baseDir = System.IO.Path.GetDirectoryName(path);
			foreach (SolutionProjectModel project in model.SolutionProjects)
			{
				string projectPath = project.FilePath;
				if (projectPath != null && !System.IO.Path.IsPathRooted(projectPath))
					projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, projectPath));

				List<string> folders = new List<string>();
				SolutionFolderModel parent = project.Parent;
				while (parent != null)
				{
					string name = parent.ActualDisplayName ?? parent.Name;
					if (name != null && name.Length > 0) folders.Add(name);
					parent = parent.Parent;
				}
				folders.Reverse();

				string display = project.DisplayName ?? project.ActualDisplayName;
				if (display == null || display.Length == 0)
					display = System.IO.Path.GetFileNameWithoutExtension(projectPath);

				SolutionNode folderNode = EnsureFolder(root, folders);
				SolutionNode leaf = new SolutionNode();
				leaf.Name = display;
				leaf.Path = projectPath;
				folderNode.Children.Add(leaf);
			}
		}

		/// <summary>.sln 走 Microsoft.Build 的 SolutionFile。</summary>
		private static void LoadSln(string path, SolutionNode root)
		{
			Microsoft.Build.Construction.SolutionFile file = Microsoft.Build.Construction.SolutionFile.Parse(path);
			foreach (ProjectInSolution project in file.ProjectsInOrder)
			{
				string name = project.ProjectName ?? "";
				string[] parts = name.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
				List<string> folders = new List<string>();
				for (int i = 0; i < parts.Length - 1; i++) folders.Add(parts[i]);

				string display = parts.Length > 0 ? parts[parts.Length - 1] : name;
				string projectPath = project.AbsolutePath;

				SolutionNode folderNode = EnsureFolder(root, folders);
				SolutionNode leaf = new SolutionNode();
				leaf.Name = display;
				leaf.Path = projectPath;
				folderNode.Children.Add(leaf);
			}
		}

		private static SolutionNode EnsureFolder(SolutionNode root, List<string> folders)
		{
			SolutionNode current = root;
			foreach (string folder in folders)
			{
				SolutionNode found = null;
				foreach (SolutionNode child in current.Children)
				{
					if (!child.IsFile && child.Path == null && child.Name == folder) { found = child; break; }
				}
				if (found == null)
				{
					found = new SolutionNode();
					found.Name = folder;
					current.Children.Add(found);
				}
				current = found;
			}
			return current;
		}

		/// <summary>列出项目里的源码文件（未求值，直接读 MSBuild 项目文件的 ItemGroup）。</summary>
		public static List<string> GetProjectFiles(string projectPath)
		{
			List<string> result = new List<string>();
			if (projectPath == null || !File.Exists(projectPath)) return result;
			string baseDir = System.IO.Path.GetDirectoryName(projectPath);

			try
			{
				ProjectRootElement root = ProjectRootElement.Open(projectPath, ProjectCollectionInstance);
				foreach (ProjectItemElement item in root.Items)
				{
					string type = item.ItemType;
					if (!IsSourceItem(type)) continue;
					string include = item.Include;
					if (string.IsNullOrEmpty(include)) continue;

					foreach (string expanded in ExpandInclude(baseDir, include))
					{
						if (File.Exists(expanded) && !result.Contains(expanded, StringComparer.OrdinalIgnoreCase))
							result.Add(expanded);
					}
				}
			}
			catch { }

			// SDK 风格项目默认用通配符包含源码，这里补上目录扫描
			if (result.Count == 0)
			{
				try
				{
					foreach (string file in Directory.GetFiles(baseDir, "*.cs", SearchOption.AllDirectories))
					{
						string lower = file.ToLowerInvariant();
						if (lower.Contains("\\bin\\") || lower.Contains("\\obj\\") || lower.Contains("\\.git\\")) continue;
						result.Add(file);
					}
				}
				catch { }
			}

			result.Sort(StringComparer.OrdinalIgnoreCase);
			return result;
		}

		private static ProjectCollection _projectCollection;

		private static ProjectCollection ProjectCollectionInstance
		{
			get
			{
				if (_projectCollection == null) _projectCollection = new ProjectCollection();
				return _projectCollection;
			}
		}

		private static bool IsSourceItem(string itemType)
		{
			if (itemType == null) return false;
			string t = itemType.ToLowerInvariant();
			return t == "compile" || t == "clcompile" || t == "page" || t == "content"
				|| t == "additionalcompile" || t == "codeanalysisdictionary";
		}

		private static IEnumerable<string> ExpandInclude(string baseDir, string include)
		{
			include = include.Trim();
			if (include.IndexOf('*') < 0 && include.IndexOf('?') < 0)
			{
				string single = include;
				if (!System.IO.Path.IsPathRooted(single))
					single = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, single));
				yield return single;
				yield break;
			}

			// 简单通配符：去掉 ** 递归部分，按扩展名在项目目录里扫
			string ext = System.IO.Path.GetExtension(include);
			string dir = baseDir;
			int star = include.IndexOf("**", StringComparison.Ordinal);
			if (star > 0)
			{
				string prefix = include.Substring(0, star).TrimEnd('\\', '/');
				if (prefix.Length > 0) dir = System.IO.Path.Combine(baseDir, prefix);
			}
			if (ext == null || ext.Length == 0) yield break;

			string[] found;
			try { found = Directory.GetFiles(dir, "*" + ext, SearchOption.AllDirectories); }
			catch { yield break; }
			foreach (string file in found)
			{
				string lower = file.ToLowerInvariant();
				if (lower.Contains("\\bin\\") || lower.Contains("\\obj\\")) continue;
				yield return file;
			}
		}
	}
}
