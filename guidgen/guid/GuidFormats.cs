using System;
using System.Globalization;
using System.Text;

namespace guid
{
	/// <summary>
	/// 把同一个 GUID 渲染成常见的源码 / 配置写法，保证几种格式之间可以互相对照。
	/// </summary>
	internal static class GuidFormats
	{
		/// <summary>IMPLEMENT_OLECREATE 宏：ATL/MFC 里声明 COM 类工厂的写法。</summary>
		public static string OleCreate(Guid id, string name, bool upper)
		{
			StringBuilder text = new StringBuilder();
			text.Append("IMPLEMENT_OLECREATE(C").Append(name).Append(", \"");
			text.Append(name).Append(".1\", ");
			text.Append(Numbers(id, upper));
			text.Append(')');
			return text.ToString();
		}

		/// <summary>DEFINE_GUID 宏：头文件里声明 CLSID/IID 常量的写法。</summary>
		public static string DefineGuid(Guid id, string name, bool upper)
		{
			StringBuilder text = new StringBuilder();
			text.Append("DEFINE_GUID(").Append(name).Append(", ");
			text.Append(Numbers(id, upper));
			text.Append(')');
			return text.ToString();
		}

		/// <summary>静态常量结构：直接定义 GUID 变量的写法。</summary>
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
				if (i > 0)
				{
					text.Append(", ");
				}
				text.Append(Hex(parts.Data4[i], 2, upper));
			}
			text.Append(" } };");
			return text.ToString();
		}

		/// <summary>注册表格式：{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}。</summary>
		public static string Registry(Guid id, bool upper)
		{
			return "{" + Dashed(id, upper) + "}";
		}

		/// <summary>方括号 GUID("...") 写法。</summary>
		public static string Bracket(Guid id, bool upper)
		{
			return "[GUID(\"" + Dashed(id, upper) + "\")]";
		}

		/// <summary>尖括号 GUID("...") 写法。</summary>
		public static string Angle(Guid id, bool upper)
		{
			return "<GUID(\"" + Dashed(id, upper) + "\")>";
		}

		/// <summary>把用户输入的名称清洗成合法的 C/C++ 标识符。</summary>
		public static string ToIdentifier(string raw, string fallback)
		{
			if (raw == null || raw.Trim().Length == 0)
			{
				return fallback;
			}

			StringBuilder text = new StringBuilder(raw.Length);
			foreach (char c in raw.Trim())
			{
				if (char.IsLetterOrDigit(c) || c == '_')
				{
					text.Append(c);
				}
				else if (!char.IsWhiteSpace(c))
				{
					text.Append('_');
				}
			}

			string identifier = text.ToString();
			if (identifier.Length == 0)
			{
				return fallback;
			}
			if (char.IsDigit(identifier[0]))
			{
				identifier = "_" + identifier;
			}
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

		/// <summary>
		/// 按 COM 内存布局拆出 Data1 / Data2 / Data3 / Data4。
		/// Guid.ToByteArray() 返回的就是这个布局（前三段小端），可以直接用。
		/// </summary>
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
}
