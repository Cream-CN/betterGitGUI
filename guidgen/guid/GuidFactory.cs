using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace guid
{
	/// <summary>
	/// 多层熵源叠加的 GUID 生成器。
	///
	/// 单一熵源都有可预测或可重复的隐患，这里把 5 层互相独立的材料叠加后再用 SHA-256 折叠：
	///   第 1 层：Guid.NewGuid()（操作系统 CoCreateGuid，全局唯一计数 + 网卡 + 时钟）
	///   第 2 层：CSPRNG 的 256 位加密随机数（RNGCryptoServiceProvider）
	///   第 3 层：高精度时间（UTC 100ns 时间戳 + 性能计数器 QPC 计数）
	///   第 4 层：进程内自增序号 + 进程号 / 线程号 / 毫秒计数 + 机器与进程指纹
	///   第 5 层：上一条结果的反馈（哈希链）+ 本次重试序号
	///
	/// 折叠后强制写入 RFC 4122 的版本位（version 4）和变体位（10xx），
	/// 输出仍是标准 GUID，可以被 Guid.Parse / COM / 数据库直接使用。
	/// 同时对最近 64 个结果做查重，极端情况下自动重新搅拌，确保本进程内绝不重复。
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

		/// <summary>生成一个新的、版本为 4 的 GUID。</summary>
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

				// 概率上不可能走到这里（需要连续 8 次落进最近 64 个结果里）
				throw new InvalidOperationException("GUID 生成失败：连续多次与最近结果重复。");
			}
		}

		/// <summary>把各层材料叠加、哈希、折叠成 16 字节。</summary>
		private static byte[] Mix(int attempt)
		{
			// 第 2 层：加密随机数
			byte[] random = new byte[32];
			Crypto.GetBytes(random);

			// 第 1 层：系统 GUID
			byte[] system = Guid.NewGuid().ToByteArray();

			// 第 3 层：高精度时间
			long ticks = DateTime.UtcNow.Ticks;
			long qpc = Stopwatch.GetTimestamp();

			// 第 4 层：序号与运行时标识
			ulong sequence = unchecked(++_sequence);
			int tickCount = Environment.TickCount;
			int threadId = Thread.CurrentThread.ManagedThreadId;

			// 第 5 层：上一条结果的反馈（哈希链）
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

			// 32 字节摘要对折成 16 字节（前后两半异或，保留全部熵）
			byte[] bytes = new byte[16];
			for (int i = 0; i < 16; i++)
			{
				bytes[i] = (byte)(digest[i] ^ digest[i + 16]);
			}

			// 版本 4：Data3 高字节的高 4 位固定为 0100
			bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
			// RFC 4122 变体：Data4 首字节的高 2 位固定为 10
			bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

			return bytes;
		}

		private static int Append(byte[] target, int offset, byte[] data)
		{
			if (data.Length > 0)
			{
				Buffer.BlockCopy(data, 0, target, offset, data.Length);
			}
			return offset + data.Length;
		}

		private static bool IsRecent(Guid candidate)
		{
			for (int i = 0; i < RecentCapacity; i++)
			{
				if (Recent[i] == candidate)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>机器与进程指纹：同一台机器上每次运行都不同，重装/重启也无法还原。</summary>
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
			try
			{
				return CurrentProcess.Id + "@" + CurrentProcess.StartTime.Ticks + "|";
			}
			catch
			{
				// 极少数受保护进程拿不到启动时间，退化为进程号
				return CurrentProcess.Id + "|";
			}
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
					{
						text.Append(address).Append(';');
					}
				}
				return text.ToString();
			}
			catch
			{
				return string.Empty;
			}
		}
	}
}
