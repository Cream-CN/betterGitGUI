using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>一个打开的文档：路径、编码、换行符以及对应的编辑框。</summary>
	public class Document
	{
		public string FilePath;
		public string UntitledName;
		public Encoding Encoding = new UTF8Encoding(false);
		public bool EmitBom;
		public string LineEnding = "CRLF";
		public bool Dirty;
		public bool ReadOnly;
		public DateTime LastWriteTime;
		public RichTextBox Box;
		public TabPage Page;
		public SyntaxDefinition Syntax = SyntaxRegistry.Plain;

		public string DisplayName
		{
			get
			{
				if (FilePath != null) return Path.GetFileName(FilePath);
				return UntitledName ?? "未命名";
			}
		}

		public string EncodingDisplay
		{
			get
			{
				string name;
				if (Encoding is UTF8Encoding) name = "UTF-8";
				else if (Encoding.CodePage == 936) name = "GBK";
				else if (Encoding.CodePage == 54936) name = "GB18030";
				else if (Encoding.CodePage == 1200) name = "UTF-16 LE";
				else if (Encoding.CodePage == 1201) name = "UTF-16 BE";
				else if (Encoding.CodePage == 12000) name = "UTF-32 LE";
				else if (Encoding.CodePage == 12001) name = "UTF-32 BE";
				else name = Encoding.WebName.ToUpperInvariant();
				return EmitBom ? name + " BOM" : name;
			}
		}

		public string LineEndingDisplay
		{
			get { return LineEnding == "CRLF" ? "CRLF" : "LF"; }
		}

		/// <summary>更新标签页标题（未保存时带 *）。</summary>
		public void RefreshTitle()
		{
			if (Page != null) Page.Text = (Dirty ? "* " : "") + DisplayName;
		}
	}

	/// <summary>文本文件读写：自动识别 BOM / UTF-8 / GBK，保留换行符风格。</summary>
	public static class TextFile
	{
		public static void Load(string path, out string text, out Encoding encoding, out bool bom, out string lineEnding)
		{
			byte[] bytes = File.ReadAllBytes(path);
			encoding = new UTF8Encoding(false);
			bom = false;
			int offset = 0;

			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			{
				encoding = new UTF8Encoding(false);
				bom = true;
				offset = 3;
			}
			else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			{
				encoding = new UnicodeEncoding(false, false);
				bom = true;
				offset = 2;
			}
			else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			{
				encoding = new UnicodeEncoding(true, false);
				bom = true;
				offset = 2;
			}
			else
			{
				bool utf8 = true;
				try
				{
					UTF8Encoding strict = new UTF8Encoding(false, true);
					strict.GetString(bytes);
				}
				catch
				{
					utf8 = false;
				}
				if (!utf8)
				{
					try { encoding = Encoding.GetEncoding(936); }
					catch { encoding = Encoding.Default; }
				}
			}

			string content = encoding.GetString(bytes, offset, bytes.Length - offset);
			lineEnding = "LF";
			if (content.IndexOf("\r\n", StringComparison.Ordinal) >= 0) lineEnding = "CRLF";
			text = content.Replace("\r\n", "\n").Replace('\r', '\n');
		}

		/// <summary>按指定编码读取文件（不自动猜测）；BOM 按实际字节情况识别。</summary>
		public static void LoadWithEncoding(string path, Encoding encoding, out string text, out bool bom, out string lineEnding)
		{
			byte[] bytes = File.ReadAllBytes(path);
			int offset = 0;
			bom = false;
			int codePage = encoding.CodePage;

			if (codePage == 65001 && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			{
				offset = 3; bom = true;
			}
			else if (codePage == 1200 && bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			{
				offset = 2; bom = true;
			}
			else if (codePage == 1201 && bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			{
				offset = 2; bom = true;
			}
			else if (codePage == 12000 && bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
			{
				offset = 4; bom = true;
			}

			string content = encoding.GetString(bytes, offset, bytes.Length - offset);
			lineEnding = "LF";
			if (content.IndexOf("\r\n", StringComparison.Ordinal) >= 0) lineEnding = "CRLF";
			text = content.Replace("\r\n", "\n").Replace('\r', '\n');
		}

		/// <summary>该编码对应的 BOM 字节（不支持 BOM 的编码返回空）。</summary>
		private static byte[] PreambleFor(Encoding encoding)
		{
			switch (encoding.CodePage)
			{
				case 65001: return new byte[] { 0xEF, 0xBB, 0xBF };
				case 1200: return new byte[] { 0xFF, 0xFE };
				case 1201: return new byte[] { 0xFE, 0xFF };
				case 12000: return new byte[] { 0xFF, 0xFE, 0x00, 0x00 };
				case 12001: return new byte[] { 0x00, 0x00, 0xFE, 0xFF };
				default: return null;
			}
		}

		public static void Save(string path, string text, Encoding encoding, bool bom, string lineEnding)
		{
			string content = lineEnding == "CRLF" ? text.Replace("\n", "\r\n") : text;
			byte[] body = encoding.GetBytes(content);
			if (bom)
			{
				byte[] preamble = PreambleFor(encoding);
				if (preamble != null)
				{
					byte[] all = new byte[body.Length + preamble.Length];
					Buffer.BlockCopy(preamble, 0, all, 0, preamble.Length);
					Buffer.BlockCopy(body, 0, all, preamble.Length, body.Length);
					File.WriteAllBytes(path, all);
					return;
				}
			}
			File.WriteAllBytes(path, body);
		}
	}

	/// <summary>轻量 ini 配置：保存在 %APPDATA%\.ccnedit\settings.ini。</summary>
	public class EditorSettings
	{
		public string LastFolder = "";
		public string FontName = "Consolas";
		public float FontSize = 11f;
		public int TabSize = 4;
		public bool WordWrap;
		public bool ShowMinimap = true;
		public int MinimapWidth = 92;
		public bool RestoreSession = true;
		public bool ShowGitStatus = true;
		public string BetterGitGUIPath = "";
		public List<string> RecentFiles = new List<string>();
		public List<string> SessionFiles = new List<string>();

		/// <summary>记住最近打开的文件，最多 15 条。</summary>
		public void AddRecent(string path)
		{
			if (path == null || path.Length == 0) return;
			RecentFiles.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
			RecentFiles.Insert(0, path);
			while (RecentFiles.Count > 15) RecentFiles.RemoveAt(RecentFiles.Count - 1);
		}

		public static string ConfigDirectory
		{
			get
			{
				return Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
					".ccnedit");
			}
		}

		private static string ConfigPath
		{
			get { return Path.Combine(ConfigDirectory, "settings.ini"); }
		}

		private static bool ParseBool(string value, bool fallback)
		{
			if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
			if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
			return fallback;
		}

		public static EditorSettings Load()
		{
			EditorSettings s = new EditorSettings();
			try
			{
				if (!File.Exists(ConfigPath)) return s;
				foreach (string line in File.ReadAllLines(ConfigPath))
				{
					int eq = line.IndexOf('=');
					if (eq <= 0) continue;
					string key = line.Substring(0, eq).Trim();
					string value = line.Substring(eq + 1).Trim();
					switch (key)
					{
						case "LastFolder": s.LastFolder = value; break;
						case "FontName": if (value.Length > 0) s.FontName = value; break;
						case "FontSize":
							float fs;
							if (float.TryParse(value, out fs) && fs >= 6 && fs <= 48) s.FontSize = fs;
							break;
						case "TabSize":
							int ts;
							if (int.TryParse(value, out ts) && ts >= 1 && ts <= 8) s.TabSize = ts;
							break;
						case "WordWrap": s.WordWrap = ParseBool(value, false); break;
						case "ShowMinimap": s.ShowMinimap = ParseBool(value, true); break;
						case "MinimapWidth":
							int mw;
							if (int.TryParse(value, out mw) && mw >= 60 && mw <= 200) s.MinimapWidth = mw;
							break;
						case "RestoreSession": s.RestoreSession = ParseBool(value, true); break;
						case "ShowGitStatus": s.ShowGitStatus = ParseBool(value, true); break;
						case "BetterGitGUIPath": s.BetterGitGUIPath = value; break;
						case "RecentFiles": ReadList(value, s.RecentFiles); break;
						case "SessionFiles": ReadList(value, s.SessionFiles); break;
					}
				}
			}
			catch { }
			return s;
		}

		private static void ReadList(string value, List<string> target)
		{
			target.Clear();
			foreach (string p in value.Split('|'))
			{
				if (p.Trim().Length > 0) target.Add(p.Trim());
			}
		}

		public void Save()
		{
			try
			{
				if (!Directory.Exists(ConfigDirectory)) Directory.CreateDirectory(ConfigDirectory);
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("LastFolder=" + LastFolder);
				sb.AppendLine("FontName=" + FontName);
				sb.AppendLine("FontSize=" + FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
				sb.AppendLine("TabSize=" + TabSize);
				sb.AppendLine("WordWrap=" + (WordWrap ? "1" : "0"));
				sb.AppendLine("ShowMinimap=" + (ShowMinimap ? "1" : "0"));
				sb.AppendLine("MinimapWidth=" + MinimapWidth);
				sb.AppendLine("RestoreSession=" + (RestoreSession ? "1" : "0"));
				sb.AppendLine("ShowGitStatus=" + (ShowGitStatus ? "1" : "0"));
				sb.AppendLine("BetterGitGUIPath=" + BetterGitGUIPath);
				sb.AppendLine("RecentFiles=" + string.Join("|", RecentFiles.ToArray()));
				sb.AppendLine("SessionFiles=" + string.Join("|", SessionFiles.ToArray()));
				File.WriteAllText(ConfigPath, sb.ToString(), new UTF8Encoding(false));
			}
			catch { }
		}
	}

	/// <summary>简单的文本输入对话框（新建文件、重命名用）。</summary>
	public class InputDialog : Form
	{
		private TextBox _box;

		public string Value
		{
			get { return _box.Text.Trim(); }
		}

		public InputDialog(string title, string prompt, string initial)
		{
			Text = title;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			ClientSize = new Size(400, 132);
			Font = new Font("Microsoft YaHei UI", 9f);

			Label label = new Label();
			label.Text = prompt;
			label.SetBounds(16, 14, 368, 22);
			Controls.Add(label);

			_box = new TextBox();
			_box.SetBounds(16, 42, 368, 26);
			_box.Text = initial ?? "";
			Controls.Add(_box);

			Button ok = new Button();
			ok.Text = "确定";
			ok.SetBounds(214, 86, 80, 30);
			ok.DialogResult = DialogResult.OK;
			Controls.Add(ok);
			AcceptButton = ok;

			Button cancel = new Button();
			cancel.Text = "取消";
			cancel.SetBounds(304, 86, 80, 30);
			cancel.DialogResult = DialogResult.Cancel;
			Controls.Add(cancel);
			CancelButton = cancel;
		}
	}
}
