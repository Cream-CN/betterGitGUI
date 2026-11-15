using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace guid
{
	/// <summary>
	/// 读取随程序嵌入的 .ico。
	///
	/// 现在很多图标文件的帧是 PNG 压缩的（Vista 之后很常见），这种帧
	/// System.Drawing.Icon.ToBitmap() 解析不了，会在 BmpFrame 里越界抛异常。
	/// 所以这里直接按 ICO 目录挑最合适的一帧：PNG 帧交给 Image.FromStream，
	/// 常规位图帧退回 Icon 绘制，两者都失败才用系统图标兜底。
	/// </summary>
	internal static class IconLoader
	{
		private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

		/// <summary>按资源名后缀找嵌入的图标文件，返回原始字节；找不到返回 null。</summary>
		public static byte[] FindEmbedded(string suffix)
		{
			Assembly assembly = typeof(IconLoader).Assembly;
			foreach (string resource in assembly.GetManifestResourceNames())
			{
				if (resource.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				{
					using (Stream stream = assembly.GetManifestResourceStream(resource))
					{
						if (stream == null)
						{
							continue;
						}
						using (MemoryStream buffer = new MemoryStream())
						{
							stream.CopyTo(buffer);
							return buffer.ToArray();
						}
					}
				}
			}
			return null;
		}

		/// <summary>嵌入的图标转成 Icon 对象，取不到时用系统应用图标。</summary>
		public static Icon LoadIcon(string suffix)
		{
			byte[] bytes = FindEmbedded(suffix);
			if (bytes != null)
			{
				using (MemoryStream stream = new MemoryStream(bytes))
				{
					return new Icon(stream);
				}
			}
			return SystemIcons.Application;
		}

		/// <summary>取指定尺寸的位图（供 PictureBox 显示），不依赖 Icon.ToBitmap。</summary>
		public static Bitmap LoadBitmap(string suffix, int size, Icon fallbackIcon)
		{
			byte[] bytes = FindEmbedded(suffix);
			Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.Clear(Color.Transparent);
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				graphics.SmoothingMode = SmoothingMode.HighQuality;

				Rectangle target = new Rectangle(0, 0, size, size);
				Bitmap frame = ExtractPngFrame(bytes, size);
				if (frame != null)
				{
					graphics.DrawImage(frame, target);
					frame.Dispose();
					return bitmap;
				}

				try
				{
					using (Icon icon = bytes != null ? new Icon(new MemoryStream(bytes)) : fallbackIcon)
					{
						graphics.DrawIcon(icon, target);
					}
				}
				catch (Exception)
				{
					graphics.DrawIcon(SystemIcons.Application, target);
				}
			}
			return bitmap;
		}

		/// <summary>从 ICO 目录里挑一帧最接近目标尺寸的 PNG，尺寸不符时高质量缩放。</summary>
		private static Bitmap ExtractPngFrame(byte[] bytes, int size)
		{
			try
			{
				if (bytes == null || bytes.Length < 6 + 16 || BitConverter.ToUInt16(bytes, 2) != 1)
				{
					return null;
				}

				int count = BitConverter.ToUInt16(bytes, 4);
				int bestOffset = -1;
				int bestLength = 0;
				int bestScore = int.MaxValue;

				for (int i = 0; i < count; i++)
				{
					int entry = 6 + i * 16;
					if (entry + 16 > bytes.Length)
					{
						break;
					}

					int width = bytes[entry] == 0 ? 256 : bytes[entry];
					int length = (int)BitConverter.ToUInt32(bytes, entry + 8);
					int offset = (int)BitConverter.ToUInt32(bytes, entry + 12);
					if (length <= 0 || offset + length > bytes.Length)
					{
						continue;
					}

					// 优先正好尺寸，其次略大（缩小比放大好看），最后才是偏小
					int score = width >= size ? width - size : (size - width) * 4;
					if (score < bestScore)
					{
						bestScore = score;
						bestOffset = offset;
						bestLength = length;
					}
				}

				if (bestOffset < 0 || !HasPngSignature(bytes, bestOffset))
				{
					return null;
				}

				using (MemoryStream stream = new MemoryStream(bytes, bestOffset, bestLength))
				using (Image image = Image.FromStream(stream))
				{
					return new Bitmap(image, size, size);
				}
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static bool HasPngSignature(byte[] bytes, int offset)
		{
			if (offset + PngSignature.Length > bytes.Length)
			{
				return false;
			}
			for (int i = 0; i < PngSignature.Length; i++)
			{
				if (bytes[offset + i] != PngSignature[i])
				{
					return false;
				}
			}
			return true;
		}
	}
}
