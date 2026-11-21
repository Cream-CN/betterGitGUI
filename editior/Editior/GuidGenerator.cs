using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>
	/// 从 guidgen 合并过来的 GUID 生成器：多层熵源 + 多种源码写法。
	/// </summary>
	internal static class GuidFactory
	{
		private const int RecentCapacity = 64;
		private const int MaxAttempts = 8;

		private static readonly object Gate = new object();
		private static readonly RandomNumberGenerator Crypto = RandomNumberGenerator.Create();
		private static readonly Process CurrentProcess = Process.GetCurrentProcess();
		private static readonly byte[] MachineSalt = BuildMachineSalt();
		private static readonly Guid[] Recent = new Guid[RecentCapacity];

		private static ulong _sequence;
		private static int _recentIndex;

		public static Guid NewGuid()
		{
			lock (Gate)
			{
				for (int attempt = 0; attempt < MaxAttempts; attempt++)
				{
					Guid candidate = new Guid(Mix(attempt));
					if (!IsRecent(candidate))
					{
						Recent[_recentIndex] = candidate;
						_recentIndex = (_recentIndex + 1) % RecentCapacity;
						return candidate;
					}
				}
				throw new InvalidOperationException("GUID 生成失败：连续多次与最近结果重复。");
			}
		}

		private static byte[] Mix(int attempt)
		{
			byte[] random = new byte[32];
			Crypto.GetBytes(random);

			byte[] system = Guid.NewGuid().ToByteArray();

			long ticks = DateTime.UtcNow.Ticks;
			long qpc = Stopwatch.GetTimestamp();
			ulong sequence = unchecked(++_sequence);
			int tickCount = Environment.TickCount;
			int threadId = Thread.CurrentThread.ManagedThreadId;
			Guid previous = Recent[(_recentIndex + RecentCapacity - 1) % RecentCapacity];

			byte[] material = new byte[32 + 16 + 8 + 8 + 8 + 4 + 4 + 16 + MachineSalt.Length + 4];
			int offset = 0;
			offset = Append(material, offset, random);
			offset = Append(material, offset, system);
			offset = Append(material, offset, BitConverter.GetBytes(ticks));
			offset = Append(material, offset, BitConverter.GetBytes(qpc));
			offset = Append(material, offset, BitConverter.GetBytes(sequence));
			offset = Append(material, offset, BitConverter.GetBytes(tickCount));
			offset = Append(material, offset, BitConverter.GetBytes(threadId));
			offset = Append(material, offset, previous.ToByteArray());
			offset = Append(material, offset, MachineSalt);
			Append(material, offset, BitConverter.GetBytes(attempt));

			byte[] digest;
			using (SHA256 sha = SHA256.Create())
			{
				digest = sha.ComputeHash(material);
			}

			byte[] bytes = new byte[16];
			for (int i = 0; i < 16; i++)
			{
				bytes[i] = (byte)(digest[i] ^ digest[i + 16]);
			}

			bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
			bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
			return bytes;
		}

		private static int Append(byte[] target, int offset, byte[] data)
		{
			if (data.Length > 0) Buffer.BlockCopy(data, 0, target, offset, data.Length);
			return offset + data.Length;
		}

		private static bool IsRecent(Guid candidate)
		{
			for (int i = 0; i < RecentCapacity; i++)
			{
				if (Recent[i] == candidate) return true;
			}
			return false;
		}

		private static byte[] BuildMachineSalt()
		{
			StringBuilder text = new StringBuilder();
			text.Append(Environment.MachineName).Append('|');
			text.Append(Environment.UserName).Append('|');
			text.Append(Environment.OSVersion.VersionString).Append('|');
			text.Append(Environment.ProcessorCount).Append('|');
			text.Append(Environment.TickCount).Append('|');
			text.Append(AppDomain.CurrentDomain.Id).Append('|');
			text.Append(GetProcessSalt());
			text.Append(GetMacSalt());

			using (SHA256 sha = SHA256.Create())
			{
				return sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
			}
		}

		private static string GetProcessSalt()
		{
			try { return CurrentProcess.Id + "@" + CurrentProcess.StartTime.Ticks + "|"; }
			catch { return CurrentProcess.Id + "|"; }
		}

		private static string GetMacSalt()
		{
			try
			{
				StringBuilder text = new StringBuilder();
				foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
				{
					PhysicalAddress address = nic.GetPhysicalAddress();
					if (address != null && address.GetAddressBytes().Length > 0)
						text.Append(address).Append(';');
				}
				return text.ToString();
			}
			catch { return string.Empty; }
		}
	}

	/// <summary>把同一个 GUID 渲染成常见的源码 / 配置写法。</summary>
	internal static class GuidFormats
	{
		public static string OleCreate(Guid id, string name, bool upper)
		{
			StringBuilder text = new StringBuilder();
			text.Append("IMPLEMENT_OLECREATE(C").Append(name).Append(", \"");
			text.Append(name).Append(".1\", ");
			text.Append(Numbers(id, upper));
			text.Append(')');
			return text.ToString();
		}

		public static string DefineGuid(Guid id, string name, bool upper)
		{
			StringBuilder text = new StringBuilder();
			text.Append("DEFINE_GUID(").Append(name).Append(", ");
			text.Append(Numbers(id, upper));
			text.Append(')');
			return text.ToString();
		}

		public static string StaticConst(Guid id, string name, bool upper)
		{
			Parts parts = new Parts(id);
			StringBuilder text = new StringBuilder();
			text.Append("static const GUID ").Append(name).Append(" = { ");
			text.Append(Hex(parts.Data1, 8, upper)).Append(", ");
			text.Append(Hex(parts.Data2, 4, upper)).Append(", ");
			text.Append(Hex(parts.Data3, 4, upper)).Append(", { ");
			for (int i = 0; i < parts.Data4.Length; i++)
			{
				if (i > 0) text.Append(", ");
				text.Append(Hex(parts.Data4[i], 2, upper));
			}
			text.Append(" } };");
			return text.ToString();
		}

		public static string Registry(Guid id, bool upper)
		{
			return "{" + Dashed(id, upper) + "}";
		}

		public static string Bracket(Guid id, bool upper)
		{
			return "[GUID(\"" + Dashed(id, upper) + "\")]";
		}

		public static string Angle(Guid id, bool upper)
		{
			return "<GUID(\"" + Dashed(id, upper) + "\")>";
		}

		public static string ToIdentifier(string raw, string fallback)
		{
			if (raw == null || raw.Trim().Length == 0) return fallback;

			StringBuilder text = new StringBuilder(raw.Length);
			foreach (char c in raw.Trim())
			{
				if (char.IsLetterOrDigit(c) || c == '_') text.Append(c);
				else if (!char.IsWhiteSpace(c)) text.Append('_');
			}

			string identifier = text.ToString();
			if (identifier.Length == 0) return fallback;
			if (char.IsDigit(identifier[0])) identifier = "_" + identifier;
			return identifier;
		}

		private static string Dashed(Guid id, bool upper)
		{
			string dashed = id.ToString("D", CultureInfo.InvariantCulture);
			return upper ? dashed.ToUpperInvariant() : dashed;
		}

		private static string Numbers(Guid id, bool upper)
		{
			Parts parts = new Parts(id);
			StringBuilder text = new StringBuilder();
			text.Append(Hex(parts.Data1, 8, upper)).Append(", ");
			text.Append(Hex(parts.Data2, 4, upper)).Append(", ");
			text.Append(Hex(parts.Data3, 4, upper));
			for (int i = 0; i < parts.Data4.Length; i++)
			{
				text.Append(", ").Append(Hex(parts.Data4[i], 2, upper));
			}
			return text.ToString();
		}

		private static string Hex(uint value, int digits, bool upper)
		{
			string format = (upper ? "X" : "x") + digits;
			return "0x" + value.ToString(format, CultureInfo.InvariantCulture);
		}

		private sealed class Parts
		{
			public readonly uint Data1;
			public readonly ushort Data2;
			public readonly ushort Data3;
			public readonly byte[] Data4;

			public Parts(Guid id)
			{
				byte[] bytes = id.ToByteArray();
				Data1 = BitConverter.ToUInt32(bytes, 0);
				Data2 = BitConverter.ToUInt16(bytes, 4);
				Data3 = BitConverter.ToUInt16(bytes, 6);
				Data4 = new byte[8];
				Buffer.BlockCopy(bytes, 8, Data4, 0, 8);
			}
		}
	}

	/// <summary>GUID 生成器窗口（原 guidgen 的功能，合并进 Editior）。</summary>
	public class GuidGeneratorForm : Form
	{
		private const string FallbackName = "MyObject";
		private const string HintText = "双击文本框可复制；F5 重新生成";

		private TextBox _nameBox;
		private CheckBox _upperBox;
		private Label _countLabel;
		private Label _statusLabel;
		private TextBox[] _valueBoxes;
		private Guid? _current;
		private int _count;

		public GuidGeneratorForm()
		{
			Text = "GUID 生成器";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			ClientSize = new Size(800, 400);
			Font = new Font("Microsoft YaHei UI", 9f);

			Label nameLabel = new Label();
			nameLabel.Text = "名称：";
			nameLabel.SetBounds(16, 20, 50, 22);
			Controls.Add(nameLabel);

			_nameBox = new TextBox();
			_nameBox.Text = FallbackName;
			_nameBox.SetBounds(66, 18, 180, 24);
			_nameBox.TextChanged += delegate { RenderCurrent(); };
			Controls.Add(_nameBox);

			_upperBox = new CheckBox();
			_upperBox.Text = "大写";
			_upperBox.SetBounds(258, 19, 60, 22);
			_upperBox.CheckedChanged += delegate { RenderCurrent(); };
			Controls.Add(_upperBox);

			Button generate = new Button();
			generate.Text = "生成";
			generate.SetBounds(330, 16, 80, 28);
			generate.Click += delegate { Generate(); };
			Controls.Add(generate);

			Button copyAll = new Button();
			copyAll.Text = "复制全部";
			copyAll.SetBounds(420, 16, 90, 28);
			copyAll.Click += delegate { CopyAll(); };
			Controls.Add(copyAll);

			_countLabel = new Label();
			_countLabel.Text = "已生成 0 个";
			_countLabel.SetBounds(520, 22, 120, 22);
			Controls.Add(_countLabel);

			string[] titles = new string[] { "IMPLEMENT_OLECREATE", "DEFINE_GUID", "static const GUID", "注册表", "[GUID(...)]", "<GUID(...)>" };
			_valueBoxes = new TextBox[titles.Length];

			for (int i = 0; i < titles.Length; i++)
			{
				int y = 60 + i * 46;

				Label label = new Label();
				label.Text = titles[i];
				label.SetBounds(16, y + 4, 162, 22);
				Controls.Add(label);

				TextBox box = new TextBox();
				box.ReadOnly = true;
				box.Font = new Font("Consolas", 9f);
				box.SetBounds(178, y, 520, 24);
				box.Tag = i;
				box.DoubleClick += ValueBoxDoubleClick;
				box.Enter += delegate(object sender, EventArgs e) { ((TextBox)sender).SelectAll(); };
				Controls.Add(box);
				_valueBoxes[i] = box;

				Button copy = new Button();
				copy.Text = "复制";
				copy.SetBounds(706, y - 1, 66, 26);
				copy.Tag = box;
				copy.Click += delegate(object sender, EventArgs e)
				{
					Button button = sender as Button;
					TextBox target = button == null ? null : button.Tag as TextBox;
					if (target != null) CopyToClipboard(target.Text, "内容");
				};
				Controls.Add(copy);
			}

			_statusLabel = new Label();
			_statusLabel.Text = HintText;
			_statusLabel.ForeColor = SystemColors.GrayText;
			_statusLabel.SetBounds(16, 350, 400, 22);
			Controls.Add(_statusLabel);

			Button close = new Button();
			close.Text = "关闭";
			close.SetBounds(700, 344, 84, 30);
			close.DialogResult = DialogResult.OK;
			Controls.Add(close);
			AcceptButton = close;
			CancelButton = close;

			Generate();
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.F5)
			{
				Generate();
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void Generate()
		{
			_current = GuidFactory.NewGuid();
			_count++;
			_countLabel.Text = "已生成 " + _count + " 个";
			Render(_current.Value);
		}

		private void RenderCurrent()
		{
			if (_current.HasValue) Render(_current.Value);
		}

		private void Render(Guid id)
		{
			string name = GuidFormats.ToIdentifier(_nameBox.Text, FallbackName);
			bool upper = _upperBox.Checked;

			_valueBoxes[0].Text = GuidFormats.OleCreate(id, name, upper);
			_valueBoxes[1].Text = GuidFormats.DefineGuid(id, name, upper);
			_valueBoxes[2].Text = GuidFormats.StaticConst(id, name, upper);
			_valueBoxes[3].Text = GuidFormats.Registry(id, upper);
			_valueBoxes[4].Text = GuidFormats.Bracket(id, upper);
			_valueBoxes[5].Text = GuidFormats.Angle(id, upper);
		}

		private void ValueBoxDoubleClick(object sender, EventArgs e)
		{
			TextBox box = sender as TextBox;
			if (box != null) CopyToClipboard(box.Text, "内容");
		}

		private void CopyAll()
		{
			StringBuilder text = new StringBuilder();
			foreach (TextBox box in _valueBoxes) text.AppendLine(box.Text);
			CopyToClipboard(text.ToString(), "全部格式");
		}

		private void CopyToClipboard(string value, string what)
		{
			if (string.IsNullOrEmpty(value)) return;
			try
			{
				Clipboard.SetText(value);
				ShowStatus(what + "已复制到剪贴板");
			}
			catch (Exception ex)
			{
				ShowStatus("复制失败：" + ex.Message);
			}
		}

		private void ShowStatus(string text)
		{
			_statusLabel.Text = text;
			_statusLabel.ForeColor = Color.FromArgb(21, 128, 61);
		}
	}
}


