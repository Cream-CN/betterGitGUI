using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Editior
{
	// ---------------- 通用选项 ----------------

	/// <summary>位图转数组的几种输出形态。</summary>
	internal enum ImageOutputMode
	{
		BlackWhiteByte01,
		BlackWhiteByte0255,
		BlackWhiteBitsMsb,
		BlackWhiteBitsLsb,
		Grayscale,
		ColorRgb24,
		ColorRgb565,
		ColorPlanes
	}

	internal enum DitherMode
	{
		None,
		FloydSteinberg,
		Bayer4,
		Bayer8
	}

	/// <summary>生成常量数组的目标语言。</summary>
	internal enum CodeLanguage
	{
		C,
		Cpp,
		CSharp,
		Java,
		Python,
		Rust,
		Go,
		JavaScript,
		GasAssembly
	}

	internal enum NumberStyle
	{
		Hex,
		Decimal,
		Binary
	}

	/// <summary>字节流按什么规则切行。</summary>
	internal enum GroupingMode
	{
		PerInstruction,
		PerByte,
		PerWord,
		PerDWord,
		None
	}

	internal sealed class ImageConvertOptions
	{
		public ImageOutputMode Mode = ImageOutputMode.BlackWhiteByte01;
		public int Threshold = 128;
		public DitherMode Dither = DitherMode.FloydSteinberg;
		public bool Invert;
		public int MaxEdge;
		public Color Background = Color.White;
	}

	/// <summary>一份要输出的数据：元素宽度 + 按行分好的数值。</summary>
	internal sealed class DataPlane
	{
		public string Suffix = "";
		public string Title = "";
		public int ElementWidth = 1;
		public List<int[]> Lines = new List<int[]>();

		public int Count
		{
			get
			{
				int count = 0;
				for (int i = 0; i < Lines.Count; i++) count += Lines[i].Length;
				return count;
			}
		}

		public IEnumerable<int> Values
		{
			get
			{
				for (int i = 0; i < Lines.Count; i++)
				{
					int[] line = Lines[i];
					for (int j = 0; j < line.Length; j++) yield return line[j];
				}
			}
		}
	}

	internal sealed class ImageConvertResult
	{
		public int Width;
		public int Height;
		public string Summary = "";
		public List<DataPlane> Planes = new List<DataPlane>();
		/// <summary>转换后的效果预览（调用方负责释放）。</summary>
		public Bitmap Preview;
	}

	// ---------------- 位图 → 数值 ----------------

	internal static class ImageDataConverter
	{
		/// <summary>超过这个像素数直接拒绝，避免把内存吃满。</summary>
		public const int MaxPixels = 16 * 1000 * 1000;

		private static readonly int[] Bayer4 = new int[]
		{
			0, 8, 2, 10,
			12, 4, 14, 6,
			3, 11, 1, 9,
			15, 7, 13, 5
		};

		private static readonly int[] Bayer8 = new int[]
		{
			0, 32, 8, 40, 2, 34, 10, 42,
			48, 16, 56, 24, 50, 18, 58, 26,
			12, 44, 4, 36, 14, 46, 6, 38,
			60, 28, 52, 20, 62, 30, 54, 22,
			3, 35, 11, 43, 1, 33, 9, 41,
			51, 19, 59, 27, 49, 17, 57, 25,
			15, 47, 7, 39, 13, 45, 5, 37,
			63, 31, 55, 23, 61, 29, 53, 21
		};

		public static ImageConvertResult Convert(Bitmap source, ImageConvertOptions options)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (options == null) options = new ImageConvertOptions();

			Bitmap working = null;
			try
			{
				int width = source.Width;
				int height = source.Height;

				if (options.MaxEdge > 0 && Math.Max(width, height) > options.MaxEdge)
				{
					double scale = (double)options.MaxEdge / Math.Max(width, height);
					int scaledWidth = Math.Max(1, (int)Math.Round(width * scale));
					int scaledHeight = Math.Max(1, (int)Math.Round(height * scale));
					working = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format32bppArgb);
					using (Graphics graphics = Graphics.FromImage(working))
					{
						graphics.Clear(Color.Transparent);
						graphics.CompositingQuality = CompositingQuality.HighQuality;
						graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
						graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
						graphics.DrawImage(source, new Rectangle(0, 0, scaledWidth, scaledHeight));
					}
					width = scaledWidth;
					height = scaledHeight;
				}

				if ((long)width * height > MaxPixels)
					throw new InvalidOperationException("图片像素太多（" + width + " × " + height + "），请把「最长边」调小（例如 256）后再试。");

				int[] rgb = ReadRgb(working != null ? working : source, options.Background, width, height);
				int[] gray = new int[rgb.Length];
				for (int i = 0; i < rgb.Length; i++)
				{
					int color = rgb[i];
					// BT.601 亮度，整数近似（77 + 150 + 29 = 256）
					int value = ((((color >> 16) & 0xFF) * 77) + (((color >> 8) & 0xFF) * 150) + ((color & 0xFF) * 29)) >> 8;
					gray[i] = options.Invert ? 255 - value : value;
				}

				ImageConvertResult result = new ImageConvertResult();
				result.Width = width;
				result.Height = height;
				string size = width + " × " + height;

				switch (options.Mode)
				{
					case ImageOutputMode.BlackWhiteByte01:
					case ImageOutputMode.BlackWhiteByte0255:
					{
						bool[] white = Binarize(gray, width, height, options.Threshold, options.Dither);
						int on = options.Mode == ImageOutputMode.BlackWhiteByte01 ? 1 : 255;
						DataPlane plane = new DataPlane();
						plane.ElementWidth = 1;
						plane.Title = "黑白";
						for (int y = 0; y < height; y++)
						{
							int[] row = new int[width];
							for (int x = 0; x < width; x++) row[x] = white[y * width + x] ? on : 0;
							plane.Lines.Add(row);
						}
						result.Planes.Add(plane);
						result.Summary = size + " · 黑白（" + DitherText(options) + "）· 每像素 1 字节（0 黑 / " + on + " 白）· " + plane.Count + " 个元素";
						result.Preview = BuildPreview(width, height, BuildWhitePreview(white));
						break;
					}
					case ImageOutputMode.BlackWhiteBitsMsb:
					case ImageOutputMode.BlackWhiteBitsLsb:
					{
						bool[] white = Binarize(gray, width, height, options.Threshold, options.Dither);
						bool msbFirst = options.Mode == ImageOutputMode.BlackWhiteBitsMsb;
						int bytesPerRow = (width + 7) / 8;
						DataPlane plane = new DataPlane();
						plane.ElementWidth = 1;
						plane.Title = "黑白 1bpp";
						for (int y = 0; y < height; y++)
						{
							int[] row = new int[bytesPerRow];
							for (int x = 0; x < width; x++)
							{
								if (!white[y * width + x]) continue;
								int bit = msbFirst ? 7 - (x & 7) : (x & 7);
								row[x >> 3] |= 1 << bit;
							}
							plane.Lines.Add(row);
						}
						result.Planes.Add(plane);
						result.Summary = size + " · 黑白（" + DitherText(options) + "）· 1bpp 位打包（" + (msbFirst ? "MSB" : "LSB") + " 先，1 = 白，每行 " + bytesPerRow + " 字节）· " + plane.Count + " 个元素";
						result.Preview = BuildPreview(width, height, BuildWhitePreview(white));
						break;
					}
					case ImageOutputMode.Grayscale:
					{
						DataPlane plane = new DataPlane();
						plane.ElementWidth = 1;
						plane.Title = "灰度";
						for (int y = 0; y < height; y++)
						{
							int[] row = new int[width];
							for (int x = 0; x < width; x++) row[x] = gray[y * width + x];
							plane.Lines.Add(row);
						}
						result.Planes.Add(plane);
						result.Summary = size + " · 灰度 8bpp（每像素 1 字节" + (options.Invert ? "，已反色" : "") + "）· " + plane.Count + " 个元素";
						result.Preview = BuildPreview(width, height, BuildGrayPreview(gray));
						break;
					}
					case ImageOutputMode.ColorRgb24:
					{
						DataPlane plane = new DataPlane();
						plane.ElementWidth = 4;
						plane.Title = "全彩 0xRRGGBB";
						for (int y = 0; y < height; y++)
						{
							int[] row = new int[width];
							for (int x = 0; x < width; x++) row[x] = rgb[y * width + x];
							plane.Lines.Add(row);
						}
						result.Planes.Add(plane);
						result.Summary = size + " · 全彩 · 每像素 32 位 0xRRGGBB · " + plane.Count + " 个元素";
						result.Preview = BuildPreview(width, height, rgb);
						break;
					}
					case ImageOutputMode.ColorRgb565:
					{
						DataPlane plane = new DataPlane();
						plane.ElementWidth = 2;
						plane.Title = "全彩 RGB565";
						for (int y = 0; y < height; y++)
						{
							int[] row = new int[width];
							for (int x = 0; x < width; x++)
							{
								int color = rgb[y * width + x];
								int r = (color >> 16) & 0xFF;
								int g = (color >> 8) & 0xFF;
								int b = color & 0xFF;
								row[x] = ((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3);
							}
							plane.Lines.Add(row);
						}
						result.Planes.Add(plane);
						result.Summary = size + " · 全彩 RGB565（16 位，R5G6B5）· " + plane.Count + " 个元素";
						result.Preview = BuildPreview(width, height, rgb);
						break;
					}
					case ImageOutputMode.ColorPlanes:
					{
						for (int channel = 0; channel < 3; channel++)
						{
							DataPlane plane = new DataPlane();
							plane.ElementWidth = 1;
							plane.Suffix = channel == 0 ? "_r" : (channel == 1 ? "_g" : "_b");
							plane.Title = channel == 0 ? "R 通道" : (channel == 1 ? "G 通道" : "B 通道");
							int shift = channel == 0 ? 16 : (channel == 1 ? 8 : 0);
							for (int y = 0; y < height; y++)
							{
								int[] row = new int[width];
								for (int x = 0; x < width; x++) row[x] = (rgb[y * width + x] >> shift) & 0xFF;
								plane.Lines.Add(row);
							}
							result.Planes.Add(plane);
						}
						int total = 0;
						foreach (DataPlane plane in result.Planes) total += plane.Count;
						result.Summary = size + " · 全彩 · R / G / B 三个数组（每通道每像素 1 字节）· 共 " + total + " 个元素";
						result.Preview = BuildPreview(width, height, rgb);
						break;
					}
				}

				if (result.Preview != null) result.Preview = ShrinkForPreview(result.Preview, 900);
				return result;
			}
			finally
			{
				if (working != null) working.Dispose();
			}
		}

		private static string DitherText(ImageConvertOptions options)
		{
			switch (options.Dither)
			{
				case DitherMode.FloydSteinberg: return "阈值 " + options.Threshold + " · Floyd–Steinberg 抖动";
				case DitherMode.Bayer4: return "阈值 " + options.Threshold + " · Bayer 4×4 抖动";
				case DitherMode.Bayer8: return "阈值 " + options.Threshold + " · Bayer 8×8 抖动";
				default: return "阈值 " + options.Threshold;
			}
		}

		/// <summary>把位图读成 0xRRGGBB（透明像素按背景色合成）。</summary>
		private static int[] ReadRgb(Bitmap bitmap, Color background, int width, int height)
		{
			int[] rgb = new int[width * height];
			using (Bitmap copy = new Bitmap(width, height, PixelFormat.Format32bppArgb))
			{
				using (Graphics graphics = Graphics.FromImage(copy))
				{
					graphics.Clear(Color.Transparent);
					graphics.DrawImage(bitmap, new Rectangle(0, 0, width, height));
				}

				BitmapData data = copy.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
				try
				{
					int stride = data.Stride;
					int[] row = new int[width];
					int bgR = background.R;
					int bgG = background.G;
					int bgB = background.B;
					for (int y = 0; y < height; y++)
					{
						IntPtr rowPointer = stride >= 0
							? IntPtr.Add(data.Scan0, y * stride)
							: IntPtr.Add(data.Scan0, (height - 1 - y) * (-stride));
						Marshal.Copy(rowPointer, row, 0, width);
						int offset = y * width;
						for (int x = 0; x < width; x++)
						{
							int argb = row[x];
							int alpha = (argb >> 24) & 0xFF;
							int r = (argb >> 16) & 0xFF;
							int g = (argb >> 8) & 0xFF;
							int b = argb & 0xFF;
							if (alpha != 255)
							{
								r = (r * alpha + bgR * (255 - alpha)) / 255;
								g = (g * alpha + bgG * (255 - alpha)) / 255;
								b = (b * alpha + bgB * (255 - alpha)) / 255;
							}
							rgb[offset + x] = (r << 16) | (g << 8) | b;
						}
					}
				}
				finally
				{
					copy.UnlockBits(data);
				}
			}
			return rgb;
		}

		/// <summary>按阈值 / 抖动把灰度图二值化，返回值 true 表示白。</summary>
		private static bool[] Binarize(int[] gray, int width, int height, int threshold, DitherMode mode)
		{
			bool[] white = new bool[gray.Length];

			if (mode == DitherMode.None)
			{
				for (int i = 0; i < gray.Length; i++) white[i] = gray[i] >= threshold;
				return white;
			}

			if (mode == DitherMode.FloydSteinberg)
			{
				float[] buffer = new float[gray.Length];
				for (int i = 0; i < gray.Length; i++) buffer[i] = gray[i];
				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						int index = y * width + x;
						float old = buffer[index];
						bool isWhite = old >= threshold;
						float error = old - (isWhite ? 255f : 0f);
						if (x + 1 < width) buffer[index + 1] += error * 7f / 16f;
						if (y + 1 < height)
						{
							if (x > 0) buffer[index + width - 1] += error * 3f / 16f;
							buffer[index + width] += error * 5f / 16f;
							if (x + 1 < width) buffer[index + width + 1] += error * 1f / 16f;
						}
						white[index] = isWhite;
					}
				}
				return white;
			}

			int size = mode == DitherMode.Bayer4 ? 4 : 8;
			int[] matrix = mode == DitherMode.Bayer4 ? Bayer4 : Bayer8;
			int cells = size * size;
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int index = y * width + x;
					double limit = (matrix[(y % size) * size + (x % size)] + 0.5) * 256.0 / cells;
					white[index] = gray[index] >= limit - 127.5 + threshold;
				}
			}
			return white;
		}

		private static int[] BuildWhitePreview(bool[] white)
		{
			int[] colors = new int[white.Length];
			for (int i = 0; i < white.Length; i++) colors[i] = white[i] ? 0xFFFFFF : 0x000000;
			return colors;
		}

		private static int[] BuildGrayPreview(int[] gray)
		{
			int[] colors = new int[gray.Length];
			for (int i = 0; i < gray.Length; i++)
			{
				int value = gray[i];
				colors[i] = (value << 16) | (value << 8) | value;
			}
			return colors;
		}

		private static Bitmap BuildPreview(int width, int height, int[] colors)
		{
			Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			try
			{
				int[] row = new int[width];
				int stride = data.Stride;
				for (int y = 0; y < height; y++)
				{
					int offset = y * width;
					for (int x = 0; x < width; x++) row[x] = unchecked((int)0xFF000000) | colors[offset + x];
					IntPtr rowPointer = stride >= 0
						? IntPtr.Add(data.Scan0, y * stride)
						: IntPtr.Add(data.Scan0, (height - 1 - y) * (-stride));
					Marshal.Copy(row, 0, rowPointer, width);
				}
			}
			finally
			{
				bitmap.UnlockBits(data);
			}
			return bitmap;
		}

		private static Bitmap ShrinkForPreview(Bitmap bitmap, int max)
		{
			int width = bitmap.Width;
			int height = bitmap.Height;
			if (Math.Max(width, height) <= max) return bitmap;

			double scale = (double)max / Math.Max(width, height);
			int smallWidth = Math.Max(1, (int)Math.Round(width * scale));
			int smallHeight = Math.Max(1, (int)Math.Round(height * scale));
			Bitmap small = new Bitmap(smallWidth, smallHeight, PixelFormat.Format32bppArgb);
			using (Graphics graphics = Graphics.FromImage(small))
			{
				graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
				graphics.PixelOffsetMode = PixelOffsetMode.Half;
				graphics.DrawImage(bitmap, new Rectangle(0, 0, smallWidth, smallHeight));
			}
			bitmap.Dispose();
			return small;
		}
	}

	// ---------------- 十六进制文本 / 二进制文件 ----------------

	internal static class ByteTextParser
	{
		/// <summary>解析 0x55 / 55h / \x55 / 0b10101010 / 裸两位十六进制，并按原行分组。</summary>
		public static bool TryParse(string text, out byte[] bytes, out List<int[]> lineGroups, out string error)
		{
			bytes = null;
			lineGroups = new List<int[]>();
			error = null;
			if (string.IsNullOrEmpty(text))
			{
				error = "没有可解析的内容。";
				return false;
			}

			string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
			string[] lines = normalized.Split('\n');
			List<byte> all = new List<byte>();

			for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
			{
				string line = StripComments(lines[lineIndex]);
				if (line.Trim().Length == 0) continue;

				List<byte> lineBytes = new List<byte>();
				foreach (string token in Tokenize(line))
				{
					byte value;
					string reason;
					if (!TryParseToken(token, out value, out reason))
					{
						error = "第 " + (lineIndex + 1) + " 行「" + token + "」：" + reason;
						return false;
					}
					lineBytes.Add(value);
				}

				if (lineBytes.Count > 0)
				{
					int[] group = new int[lineBytes.Count];
					for (int i = 0; i < lineBytes.Count; i++) group[i] = lineBytes[i];
					lineGroups.Add(group);
					all.AddRange(lineBytes);
				}
			}

			if (all.Count == 0)
			{
				error = "没有解析到字节。支持写法：0x55、55h、\\x55、0b10101010，或两位十六进制。";
				return false;
			}

			bytes = all.ToArray();
			return true;
		}

		private static string StripComments(string line)
		{
			StringBuilder text = new StringBuilder(line.Length);
			bool inString = false;
			for (int i = 0; i < line.Length; i++)
			{
				char c = line[i];
				if (c == '"' || c == '\'') inString = !inString;
				if (!inString && (c == ';' || c == '#')) break;
				if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '/') break;
				if (!inString && c == '/' && i + 1 < line.Length && line[i + 1] == '*')
				{
					int end = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
					if (end < 0) break;
					i = end + 1;
					continue;
				}
				text.Append(c);
			}
			return text.ToString();
		}

		private static IEnumerable<string> Tokenize(string line)
		{
			StringBuilder token = new StringBuilder();
			foreach (char c in line)
			{
				if (char.IsWhiteSpace(c) || c == ',' || c == '{' || c == '}' || c == '(' || c == ')' ||
					c == '[' || c == ']' || c == '=' || c == '|' || c == '&')
				{
					if (token.Length > 0)
					{
						yield return token.ToString();
						token.Length = 0;
					}
				}
				else
				{
					token.Append(c);
				}
			}
			if (token.Length > 0) yield return token.ToString();
		}

		private static bool TryParseToken(string token, out byte value, out string reason)
		{
			value = 0;
			reason = null;
			if (token == null || token.Length == 0)
			{
				reason = "空标记";
				return false;
			}

			string body = token;
			bool hex = false;

			if (body.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
			{
				body = body.Substring(2);
				hex = true;
			}
			else if (body.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
			{
				return ParseBinary(body.Substring(2), out value, out reason);
			}
			else if (body.StartsWith("\\x", StringComparison.OrdinalIgnoreCase))
			{
				body = body.Substring(2);
				hex = true;
			}
			else if (body.StartsWith("$", StringComparison.Ordinal))
			{
				body = body.Substring(1);
				hex = true;
			}
			else if ((body.EndsWith("h", StringComparison.OrdinalIgnoreCase) || body.EndsWith("H", StringComparison.OrdinalIgnoreCase)) && body.Length > 1)
			{
				body = body.Substring(0, body.Length - 1);
				hex = true;
			}
			else if (body.Length == 2 && IsHexText(body))
			{
				hex = true;
			}
			else if (body.Length <= 3 && IsDigitText(body))
			{
				int number = int.Parse(body, CultureInfo.InvariantCulture);
				if (number > 255)
				{
					reason = "超过 255，只支持 8 位字节";
					return false;
				}
				value = (byte)number;
				return true;
			}

			if (!hex || body.Length == 0 || body.Length > 2 || !IsHexText(body))
			{
				reason = hex ? "十六进制必须是 1~2 位（最大 0xFF）" : "无法识别为字节（支持 0x55 / 55h / \\x55 / 0b10101010）";
				return false;
			}

			value = (byte)Convert.ToInt32(body, 16);
			return true;
		}

		private static bool ParseBinary(string body, out byte value, out string reason)
		{
			value = 0;
			reason = null;
			if (body.Length == 0 || body.Length > 8)
			{
				reason = "二进制必须是 1~8 位";
				return false;
			}
			int number = 0;
			for (int i = 0; i < body.Length; i++)
			{
				if (body[i] != '0' && body[i] != '1')
				{
					reason = "二进制里出现了 0 / 1 之外的字符";
					return false;
				}
				number = (number << 1) | (body[i] == '1' ? 1 : 0);
			}
			value = (byte)number;
			return true;
		}

		private static bool IsHexText(string text)
		{
			if (text.Length == 0) return false;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
			}
			return true;
		}

		private static bool IsDigitText(string text)
		{
			if (text.Length == 0) return false;
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] < '0' || text[i] > '9') return false;
			}
			return true;
		}
	}

	internal static class BytePlaneBuilder
	{
		/// <summary>把字节流按分组规则整理成一份 8 位数据。</summary>
		public static DataPlane FromBytes(byte[] bytes, List<int[]> lineGroups, GroupingMode grouping, int perLine)
		{
			DataPlane plane = new DataPlane();
			plane.ElementWidth = 1;
			if (bytes == null || bytes.Length == 0) return plane;

			if (grouping == GroupingMode.PerInstruction && lineGroups != null && lineGroups.Count > 0)
			{
				foreach (int[] group in lineGroups) plane.Lines.Add(group);
				return plane;
			}

			int size;
			switch (grouping)
			{
				case GroupingMode.PerByte: size = 1; break;
				case GroupingMode.PerWord: size = 2; break;
				case GroupingMode.PerDWord: size = 4; break;
				case GroupingMode.PerInstruction: size = 1; break;
				default: size = 0; break;
			}

			if (size == 0)
			{
				// 不分组：交给生成器按「每行个数」切行。
				int[] all = new int[bytes.Length];
				for (int i = 0; i < bytes.Length; i++) all[i] = bytes[i];
				plane.Lines.Add(all);
				return plane;
			}

			for (int offset = 0; offset < bytes.Length; offset += size)
			{
				int length = Math.Min(size, bytes.Length - offset);
				int[] row = new int[length];
				for (int i = 0; i < length; i++) row[i] = bytes[offset + i];
				plane.Lines.Add(row);
			}
			return plane;
		}
	}

	// ---------------- 汇编 → 机器码 ----------------

	internal sealed class AssemblerTarget
	{
		public string Id = "";
		public string Display = "";
		public bool X86;
		public bool Risc;
		public bool Available;
		public string Detail = "";
		public string ToolPath;
		public string ToolKind = "as";
		public string ToolArguments = "";
		public string ObjdumpPath;

		public override string ToString()
		{
			return Display;
		}
	}

	internal sealed class AssembledProgram
	{
		public byte[] Bytes;
		public List<int[]> Instructions = new List<int[]>();
		public string Summary = "";
		public string Warning = "";
		public string Log = "";
		public string Error;
	}

	internal static class AssemblerCatalog
	{
		private static List<string> _directories;

		/// <summary>探测本机可用的 GNU as / objdump（含 MSYS2、MinGW、Cygwin、交叉工具链）。</summary>
		public static List<AssemblerTarget> Detect()
		{
			List<string> directories = GetSearchDirectories();
			List<AssemblerTarget> targets = new List<AssemblerTarget>();

			string asPath = FindTool(directories, "as.exe");
			string objdumpPath = FindTool(directories, "objdump.exe");
			string hostMissing = "未找到 GNU as / objdump。安装 MSYS2、MinGW 或 Git for Windows 的构建工具后点「重新探测」。";

			targets.Add(MakeTarget("x86-64", "x86-64（本机 GNU as）", true, false, "as", asPath, "", objdumpPath, hostMissing));
			targets.Add(MakeTarget("x86-32", "x86（32 位，本机 GNU as）", true, false, "as", asPath, "--32", objdumpPath, hostMissing));

			string armAs = FindTool(directories, "arm-none-eabi-as.exe", "arm-linux-gnueabihf-as.exe", "arm-none-eabi-gcc.exe");
			string armDump = FindTool(directories, "arm-none-eabi-objdump.exe", "arm-linux-gnueabihf-objdump.exe");
			targets.Add(MakeTarget("arm", "ARM / Thumb（arm-none-eabi-as）", false, true,
				armAs != null && armAs.EndsWith("gcc.exe", StringComparison.OrdinalIgnoreCase) ? "gcc" : "as",
				armAs, "", armDump, "未找到 arm-none-eabi-as / objdump。可安装 Arm GNU Toolchain，或改用十六进制输入。"));

			string aarch64As = FindTool(directories, "aarch64-none-elf-as.exe", "aarch64-linux-gnu-as.exe", "aarch64-none-elf-gcc.exe");
			string aarch64Dump = FindTool(directories, "aarch64-none-elf-objdump.exe", "aarch64-linux-gnu-objdump.exe");
			targets.Add(MakeTarget("aarch64", "AArch64（aarch64-none-elf-as）", false, true,
				aarch64As != null && aarch64As.EndsWith("gcc.exe", StringComparison.OrdinalIgnoreCase) ? "gcc" : "as",
				aarch64As, "", aarch64Dump, "未找到 aarch64 交叉汇编器。可安装 Arm GNU Toolchain，或改用十六进制输入。"));

			string riscvAs = FindTool(directories,
				"riscv64-unknown-elf-as.exe", "riscv32-unknown-elf-as.exe", "riscv64-linux-gnu-as.exe",
				"riscv64-unknown-elf-gcc.exe", "riscv32-unknown-elf-gcc.exe");
			string riscvDump = FindTool(directories,
				"riscv64-unknown-elf-objdump.exe", "riscv32-unknown-elf-objdump.exe", "riscv64-linux-gnu-objdump.exe");
			targets.Add(MakeTarget("riscv", "RISC-V（riscv-unknown-elf-as）", false, true,
				riscvAs != null && riscvAs.EndsWith("gcc.exe", StringComparison.OrdinalIgnoreCase) ? "gcc" : "as",
				riscvAs, "", riscvDump, "未找到 RISC-V 交叉汇编器。可安装 riscv-gnu-toolchain，或改用十六进制输入。"));

			return targets;
		}

		private static AssemblerTarget MakeTarget(string id, string display, bool x86, bool risc,
			string kind, string toolPath, string toolArguments, string objdumpPath, string missing)
		{
			AssemblerTarget target = new AssemblerTarget();
			target.Id = id;
			target.Display = display;
			target.X86 = x86;
			target.Risc = risc;
			target.ToolKind = kind;
			target.ToolPath = toolPath;
			target.ToolArguments = toolArguments;
			target.ObjdumpPath = objdumpPath;
			target.Available = toolPath != null && objdumpPath != null;
			target.Detail = target.Available
				? "可用：" + Path.GetFileName(toolPath) + " + " + Path.GetFileName(objdumpPath)
				: missing;
			return target;
		}

		/// <summary>把汇编源码汇编成机器码，并用 objdump 取出每条指令的字节。</summary>
		public static AssembledProgram Assemble(AssemblerTarget target, string syntax, string source)
		{
			AssembledProgram result = new AssembledProgram();
			if (target == null)
			{
				result.Error = "没有选择架构。";
				return result;
			}
			if (!target.Available)
			{
				result.Error = target.Detail;
				return result;
			}
			if (string.IsNullOrEmpty(source) || source.Trim().Length == 0)
			{
				result.Error = "请先在上面输入汇编代码。";
				return result;
			}

			string directory = null;
			try
			{
				directory = Path.Combine(Path.GetTempPath(), "editior-d2c-" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(directory);
				string sourcePath = Path.Combine(directory, "input.s");
				string objectPath = Path.Combine(directory, "input.o");

				StringBuilder text = new StringBuilder();
				if (target.X86)
				{
					if (string.Equals(syntax, "AT&T", StringComparison.OrdinalIgnoreCase)) text.Append(".att_syntax prefix\r\n");
					else text.Append(".intel_syntax noprefix\r\n");
				}
				text.Append(".section .d2c,\"x\"\r\n");
				text.Append(source);
				if (!source.EndsWith("\n", StringComparison.Ordinal)) text.Append("\r\n");
				// 末尾标记：它之后的字节是汇编器自动对齐补位，必须丢掉。
				text.Append("d2c_end:\r\n");
				File.WriteAllText(sourcePath, text.ToString(), new UTF8Encoding(false));

				string arguments;
				if (string.Equals(target.ToolKind, "gcc", StringComparison.OrdinalIgnoreCase))
					arguments = ("-c " + Quote(sourcePath) + " -o " + Quote(objectPath) + " " + target.ToolArguments).Trim();
				else
					arguments = (target.ToolArguments + " -o " + Quote(objectPath) + " " + Quote(sourcePath)).Trim();

				string stdout;
				string stderr;
				int exitCode = RunProcess(target.ToolPath, arguments, directory, out stdout, out stderr);
				result.Log = stdout + stderr;
				if (exitCode != 0)
				{
					result.Error = "汇编器返回错误（退出码 " + exitCode + "）：\r\n" + Trim(stderr + stdout, 4000);
					return result;
				}
				if (!File.Exists(objectPath))
				{
					result.Error = "汇编器没有生成目标文件。" + Trim(stderr + stdout, 1000);
					return result;
				}

				string sectionName = ".d2c";
				List<int[]> instructions = RunObjdump(target.ObjdumpPath, objectPath, ".d2c", out stderr);
				if (instructions.Count == 0)
				{
					// 用户源码里自带了 .section / .text：退回解析整个目标文件。
					instructions = RunObjdump(target.ObjdumpPath, objectPath, null, out stderr);
					sectionName = "整个目标文件";
					if (instructions.Count > 0)
						result.Warning = "你的源码里含有 .section / .text 之类的段指令，指令不在 .d2c 段里，已按整个目标文件解析；建议只写指令与标签。";
				}

				if (instructions.Count == 0)
				{
					result.Error = "汇编成功，但没有解析到任何指令。请只写指令与标签（不要写 .section / .text / .data 段），并确认语法正确。"
						+ (stderr != null && stderr.Trim().Length > 0 ? "\r\n" + Trim(stderr, 1200) : "");
					return result;
				}

				int total = 0;
				foreach (int[] instruction in instructions) total += instruction.Length;
				byte[] bytes = new byte[total];
				int offset = 0;
				foreach (int[] instruction in instructions)
				{
					for (int i = 0; i < instruction.Length; i++) bytes[offset++] = (byte)instruction[i];
				}

				result.Bytes = bytes;
				result.Instructions = instructions;
				result.Summary = "已汇编：" + instructions.Count + " 条指令，" + bytes.Length + " 字节（" + sectionName + "）";
				return result;
			}
			catch (Exception ex)
			{
				result.Error = "汇编失败：" + ex.Message;
				return result;
			}
			finally
			{
				try
				{
					if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
				}
				catch { }
			}
		}

		private static List<int[]> RunObjdump(string objdumpPath, string objectPath, string section, out string error)
		{
			error = null;
			if (objdumpPath == null)
			{
				error = "没有找到 objdump。";
				return new List<int[]>();
			}

			string arguments = "-d " + (section != null ? "-j " + section + " " : "") + Quote(objectPath);
			string stdout;
			string stderr;
			int exitCode = RunProcess(objdumpPath, arguments, Path.GetDirectoryName(objectPath), out stdout, out stderr);
			if (exitCode != 0 && stdout.Length == 0)
			{
				error = Trim(stderr, 1200);
				return new List<int[]>();
			}
			return ParseInstructions(stdout);
		}

		/// <summary>从 objdump 的 -d 输出里逐条解析「第几条指令的字节」，遇到 d2c_end 标记即停。</summary>
		private static List<int[]> ParseInstructions(string dump)
		{
			List<int[]> groups = new List<int[]>();
			if (dump == null) return groups;

			string[] lines = dump.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			foreach (string line in lines)
			{
				if (line.IndexOf("<d2c_end>", StringComparison.Ordinal) >= 0) break;

				int colon = line.IndexOf(':');
				if (colon <= 0) continue;
				string address = line.Substring(0, colon).Trim();
				if (!IsHexText(address)) continue;

				string rest = line.Substring(colon + 1).TrimStart('\t', ' ');
				int tab = rest.IndexOf('\t');
				string bytesText = (tab >= 0 ? rest.Substring(0, tab) : rest).Trim();
				if (bytesText.Length == 0) continue;

				string[] parts = bytesText.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length == 0) continue;

				int[] instruction = new int[parts.Length];
				bool ok = true;
				for (int i = 0; i < parts.Length; i++)
				{
					if (parts[i].Length != 2 || !IsHexText(parts[i]))
					{
						ok = false;
						break;
					}
					instruction[i] = Convert.ToInt32(parts[i], 16);
				}
				if (ok) groups.Add(instruction);
			}
			return groups;
		}

		private static bool IsHexText(string text)
		{
			if (text.Length == 0) return false;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
			}
			return true;
		}

		private static string Quote(string path)
		{
			return "\"" + path + "\"";
		}

		private static string Trim(string text, int max)
		{
			if (text == null) return "";
			text = text.Trim();
			return text.Length <= max ? text : text.Substring(0, max) + "\r\n…（输出过长，已截断）";
		}

		private static int RunProcess(string executable, string arguments, string workingDirectory, out string stdout, out string stderr)
		{
			stdout = "";
			stderr = "";
			try
			{
				ProcessStartInfo info = new ProcessStartInfo(executable, arguments);
				info.UseShellExecute = false;
				info.CreateNoWindow = true;
				info.RedirectStandardOutput = true;
				info.RedirectStandardError = true;
				info.WorkingDirectory = workingDirectory;
				info.StandardOutputEncoding = new UTF8Encoding(false);
				info.StandardErrorEncoding = new UTF8Encoding(false);

				using (Process process = Process.Start(info))
				{
					if (process == null)
					{
						stderr = "无法启动：" + executable;
						return -1;
					}

					StringBuilder outputBuilder = new StringBuilder();
					StringBuilder errorBuilder = new StringBuilder();
					process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
					{
						if (e.Data != null) outputBuilder.AppendLine(e.Data);
					};
					process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
					{
						if (e.Data != null) errorBuilder.AppendLine(e.Data);
					};
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();

					if (!process.WaitForExit(30000))
					{
						try { process.Kill(); } catch { }
						stderr = "汇编 / 反汇编超过 30 秒没有返回，已终止。";
						return -1;
					}
					process.WaitForExit();

					stdout = outputBuilder.ToString();
					stderr = errorBuilder.ToString();
					return process.ExitCode;
				}
			}
			catch (Exception ex)
			{
				stderr = ex.Message;
				return -1;
			}
		}

		private static string FindTool(List<string> directories, params string[] names)
		{
			foreach (string name in names)
			{
				foreach (string directory in directories)
				{
					string candidate = Path.Combine(directory, name);
					try
					{
						if (File.Exists(candidate)) return candidate;
					}
					catch { }
				}
			}
			return null;
		}

		private static List<string> GetSearchDirectories()
		{
			if (_directories != null) return _directories;

			List<string> directories = new List<string>();

			string path = Environment.GetEnvironmentVariable("PATH");
			if (!string.IsNullOrEmpty(path))
			{
				foreach (string part in path.Split(';'))
				{
					string candidate = part.Trim().Trim('"');
					if (candidate.Length > 0) directories.Add(candidate);
				}
			}

			string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

			AddIfExists(directories, Path.Combine(programFiles, @"msys64\ucrt64\bin"));
			AddIfExists(directories, Path.Combine(programFiles, @"msys64\mingw64\bin"));
			AddIfExists(directories, Path.Combine(programFiles, @"msys64\mingw32\bin"));
			AddIfExists(directories, Path.Combine(programFiles, @"msys64\usr\bin"));
			AddIfExists(directories, Path.Combine(programFilesX86, @"msys64\mingw32\bin"));
			AddIfExists(directories, Path.Combine(programFilesX86, @"msys64\usr\bin"));
			AddIfExists(directories, Path.Combine(programFiles, @"Git\usr\bin"));
			AddIfExists(directories, Path.Combine(userProfile, @"scoop\shims"));

			try
			{
				foreach (DriveInfo drive in DriveInfo.GetDrives())
				{
					if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
					string root = drive.RootDirectory.FullName;
					AddIfExists(directories, Path.Combine(root, @"msys64\ucrt64\bin"));
					AddIfExists(directories, Path.Combine(root, @"msys64\mingw64\bin"));
					AddIfExists(directories, Path.Combine(root, @"msys64\mingw32\bin"));
					AddIfExists(directories, Path.Combine(root, @"msys64\usr\bin"));
					AddIfExists(directories, Path.Combine(root, @"cygwin64\bin"));
					AddIfExists(directories, Path.Combine(root, @"cygwin\bin"));
				}
			}
			catch { }

			List<string> unique = new List<string>();
			foreach (string directory in directories)
			{
				string full;
				try
				{
					full = Path.GetFullPath(directory);
					if (!Directory.Exists(full)) continue;
				}
				catch { continue; }

				bool exists = false;
				for (int i = 0; i < unique.Count; i++)
				{
					if (string.Equals(unique[i], full, StringComparison.OrdinalIgnoreCase))
					{
						exists = true;
						break;
					}
				}
				if (!exists) unique.Add(full);
			}

			_directories = unique;
			return _directories;
		}

		private static void AddIfExists(List<string> directories, string path)
		{
			try
			{
				if (Directory.Exists(path)) directories.Add(path);
			}
			catch { }
		}
	}

	// ---------------- 生成各语言常量数组 ----------------

	internal enum CommentStyle
	{
		CBlock,
		Line,
		Hash
	}

	internal sealed class CodeGenerateOptions
	{
		public CodeLanguage Language = CodeLanguage.C;
		public string Name = "image_data";
		public NumberStyle Style = NumberStyle.Hex;
		public int PerLine = 12;
		public bool UseGroups = true;
		public bool EmitLength = true;
		public bool Static;
		public bool Constexpr;
		public bool Public;
		public string Comment = "";
	}

	internal static class CodeGenerator
	{
		public static string Generate(List<DataPlane> planes, CodeGenerateOptions options)
		{
			if (options == null) options = new CodeGenerateOptions();
			if (planes == null) planes = new List<DataPlane>();

			StringBuilder text = new StringBuilder();
			if (options.Language == CodeLanguage.C)
			{
				text.Append("#include <stdint.h>\r\n\r\n");
			}
			else if (options.Language == CodeLanguage.Cpp)
			{
				text.Append("#include <cstdint>\r\n");
				if (options.EmitLength) text.Append("#include <cstddef>\r\n");
				text.Append("\r\n");
			}

			for (int i = 0; i < planes.Count; i++)
			{
				if (i > 0) text.Append("\r\n");
				AppendPlane(text, planes[i], options);
			}
			return text.ToString();
		}

		private static void AppendPlane(StringBuilder text, DataPlane plane, CodeGenerateOptions options)
		{
			int width = plane.ElementWidth < 1 ? 1 : plane.ElementWidth;
			int count = plane.Count;
			string name = MakeIdentifier(options.Name + plane.Suffix);

			string comment = options.Comment ?? "";
			if (!string.IsNullOrEmpty(plane.Title))
				comment = comment.Length == 0 ? plane.Title : comment + " · " + plane.Title;

			List<string> lines = BuildLines(plane, options);

			switch (options.Language)
			{
				case CodeLanguage.C:
				{
					WriteComment(text, comment, CommentStyle.CBlock);
					text.Append(options.Static ? "static const " : "const ").Append(CType(width)).Append(' ');
					text.Append(name).Append('[').Append(count).Append("] = {\r\n");
					AppendElements(text, lines, "    ", true);
					text.Append("};\r\n");
					if (options.EmitLength)
						text.Append("const unsigned int ").Append(name).Append("_length = ").Append(count).Append(";\r\n");
					break;
				}
				case CodeLanguage.Cpp:
				{
					WriteComment(text, comment, CommentStyle.CBlock);
					text.Append(options.Static ? "static " : "").Append(options.Constexpr ? "constexpr " : "const ").Append(CType(width)).Append(' ');
					text.Append(name).Append('[').Append(count).Append("] = {\r\n");
					AppendElements(text, lines, "    ", true);
					text.Append("};\r\n");
					if (options.EmitLength)
						text.Append(options.Constexpr ? "constexpr " : "const ").Append("size_t ").Append(name).Append("_length = ").Append(count).Append(";\r\n");
					break;
				}
				case CodeLanguage.CSharp:
				{
					WriteComment(text, comment, CommentStyle.Line);
					string type = CSharpType(width);
					text.Append(options.Public ? "public" : "private").Append(" static readonly ").Append(type).Append("[] ").Append(name);
					text.Append(" = new ").Append(type).Append("[]\r\n{\r\n");
					AppendElements(text, lines, "    ", true);
					text.Append("};\r\n");
					if (options.EmitLength)
						text.Append(options.Public ? "public" : "private").Append(" const int ").Append(name).Append("_length = ").Append(count).Append(";\r\n");
					break;
				}
				case CodeLanguage.Java:
				{
					WriteComment(text, comment, CommentStyle.Line);
					string type = width == 1 ? "byte" : "int";
					text.Append(options.Public ? "public " : "private ").Append("static final ").Append(type).Append("[] ").Append(name);
					text.Append(" = new ").Append(type).Append("[] {\r\n");
					AppendElements(text, lines, "    ", true);
					text.Append("};\r\n");
					if (options.EmitLength)
						text.Append(options.Public ? "public " : "private ").Append("static final int ").Append(name).Append("_length = ").Append(count).Append(";\r\n");
					break;
				}
				case CodeLanguage.Python:
				{
					WriteComment(text, comment, CommentStyle.Hash);
					text.Append(name).Append(width == 1 ? " = bytes([\r\n" : " = [\r\n");
					AppendElements(text, lines, "    ", true);
					text.Append(width == 1 ? "])\r\n" : "]\r\n");
					if (options.EmitLength)
						text.Append(name).Append("_length = ").Append(count).Append("\r\n");
					break;
				}
				case CodeLanguage.Rust:
				{
					WriteComment(text, comment, CommentStyle.Line);
					text.Append(options.Public ? "pub " : "").Append("const ").Append(name).Append(": [").Append(RustType(width)).Append("; ").Append(count).Append("] = [\r\n");
					AppendElements(text, lines, "    ", true);
					text.Append("];\r\n");
					if (options.EmitLength)
						text.Append("pub const ").Append(name).Append("_len: usize = ").Append(count).Append(";\r\n");
					break;
				}
				case CodeLanguage.Go:
				{
					WriteComment(text, comment, CommentStyle.Line);
					text.Append("var ").Append(name).Append(" = []").Append(GoType(width)).Append("{\r\n");
					AppendElements(text, lines, "\t", true);
					text.Append("}\r\n");
					if (options.EmitLength)
						text.Append("var ").Append(name).Append("Len = ").Append(count).Append("\r\n");
					break;
				}
				case CodeLanguage.JavaScript:
				{
					WriteComment(text, comment, CommentStyle.Line);
					text.Append("const ").Append(name).Append(" = new ").Append(JavaScriptType(width)).Append("([\r\n");
					AppendElements(text, lines, "    ", true);
					text.Append("]);\r\n");
					if (options.EmitLength)
						text.Append("const ").Append(name).Append("Length = ").Append(count).Append(";\r\n");
					break;
				}
				default:
				{
					WriteComment(text, comment, CommentStyle.Hash);
					text.Append(name).Append(":\r\n");
					string directive = width == 1 ? ".byte" : (width == 2 ? ".short" : ".long");
					for (int i = 0; i < lines.Count; i++)
						text.Append("    ").Append(directive).Append(' ').Append(lines[i]).Append("\r\n");
					if (options.EmitLength)
						text.Append(".set ").Append(name).Append("_length, ").Append(count).Append("\r\n");
					break;
				}
			}
		}

		private static void WriteComment(StringBuilder text, string comment, CommentStyle style)
		{
			if (string.IsNullOrEmpty(comment)) return;
			comment = comment.Replace("\r", " ").Replace("\n", " ").Trim();
			if (comment.Length == 0) return;
			switch (style)
			{
				case CommentStyle.CBlock: text.Append("/* ").Append(comment).Append(" */\r\n"); break;
				case CommentStyle.Hash: text.Append("# ").Append(comment).Append("\r\n"); break;
				default: text.Append("// ").Append(comment).Append("\r\n"); break;
			}
		}

		private static void AppendElements(StringBuilder text, List<string> lines, string indent, bool trailingComma)
		{
			for (int i = 0; i < lines.Count; i++)
			{
				text.Append(indent).Append(lines[i]);
				if (trailingComma) text.Append(',');
				text.Append("\r\n");
			}
		}

		private static List<string> BuildLines(DataPlane plane, CodeGenerateOptions options)
		{
			List<string> result = new List<string>();

			if (options.UseGroups && plane.Lines.Count > 0)
			{
				foreach (int[] row in plane.Lines)
				{
					if (row.Length == 0) continue;
					StringBuilder line = new StringBuilder();
					for (int i = 0; i < row.Length; i++)
					{
						if (i > 0) line.Append(", ");
						line.Append(Literal(row[i], plane.ElementWidth, options));
					}
					result.Add(line.ToString());
				}
				return result;
			}

			int perLine = options.PerLine < 1 ? 1 : options.PerLine;
			StringBuilder current = new StringBuilder();
			int count = 0;
			foreach (int value in plane.Values)
			{
				if (count > 0) current.Append(", ");
				current.Append(Literal(value, plane.ElementWidth, options));
				count++;
				if (count >= perLine)
				{
					result.Add(current.ToString());
					current.Length = 0;
					count = 0;
				}
			}
			if (current.Length > 0) result.Add(current.ToString());
			return result;
		}

		private static string Literal(int value, int width, CodeGenerateOptions options)
		{
			if (options.Language == CodeLanguage.Java)
			{
				if (width == 1 && value > 127) return "(byte)" + RawLiteral(value, width, options);
				if (width == 4 && value < 0)
				{
					// Java 的 int 是有符号的：0xFFFFFFFF 会被当成「数字太大」，要转成 long 再强转。
					uint bits = (uint)value;
					if (options.Style == NumberStyle.Hex)
						return "(int)0x" + bits.ToString("X8", CultureInfo.InvariantCulture) + "L";
					if (options.Style == NumberStyle.Binary)
						return "(int)0b" + Convert.ToString(bits, 2).PadLeft(32, '0') + "L";
					return value.ToString(CultureInfo.InvariantCulture);
				}
			}
			return RawLiteral(value, width, options);
		}

		private static string RawLiteral(int value, int width, CodeGenerateOptions options)
		{
			NumberStyle style = options.Style;
			// GNU as 的 .byte 不保证支持 0b 前缀，统一退回十六进制。
			if (style == NumberStyle.Binary && options.Language == CodeLanguage.GasAssembly) style = NumberStyle.Hex;

			switch (style)
			{
				case NumberStyle.Decimal:
					return value.ToString(CultureInfo.InvariantCulture);
				case NumberStyle.Binary:
				{
					int bits = width * 8;
					string body = Convert.ToString(value, 2);
					if (body.Length < bits) body = new string('0', bits - body.Length) + body;
					return "0b" + body;
				}
				default:
				{
					int digits = width * 2;
					return "0x" + value.ToString("X" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
				}
			}
		}

		private static string CType(int width)
		{
			return width == 1 ? "uint8_t" : (width == 2 ? "uint16_t" : "uint32_t");
		}

		private static string CSharpType(int width)
		{
			return width == 1 ? "byte" : (width == 2 ? "ushort" : "uint");
		}

		private static string RustType(int width)
		{
			return width == 1 ? "u8" : (width == 2 ? "u16" : "u32");
		}

		private static string GoType(int width)
		{
			return width == 1 ? "byte" : (width == 2 ? "uint16" : "uint32");
		}

		private static string JavaScriptType(int width)
		{
			return width == 1 ? "Uint8Array" : (width == 2 ? "Uint16Array" : "Uint32Array");
		}

		internal static string MakeIdentifier(string raw)
		{
			if (string.IsNullOrEmpty(raw)) return "data";
			StringBuilder text = new StringBuilder(raw.Length);
			foreach (char c in raw)
			{
				if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_') text.Append(c);
				else if (!char.IsWhiteSpace(c)) text.Append('_');
			}
			if (text.Length == 0) return "data";
			if (text[0] >= '0' && text[0] <= '9') text.Insert(0, '_');
			return text.ToString();
		}
	}
}
