using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace VisibleCMake.Runtime
{
	/// <summary>一次外部命令的结果。</summary>
	public sealed class ProcessResult
	{
		public ProcessResult()
		{
			ExitCode = -1;
			Output = "";
			Error = "";
		}

		/// <summary>进程退出码；没有真正启动起来时为 -1。</summary>
		public int ExitCode { get; set; }

		/// <summary>标准输出与标准错误合并后的全文。</summary>
		public string Output { get; set; }

		/// <summary>启动失败或进程异常时的说明。</summary>
		public string Error { get; set; }

		public bool Started { get; set; }
		public bool Canceled { get; set; }
		public bool TimedOut { get; set; }

		public bool Success
		{
			get { return Started && !Canceled && !TimedOut && ExitCode == 0; }
		}
	}

	/// <summary>
	/// 执行 cmake / make 这类命令行工具：逐行把输出交给回调，支持取消与超时。
	/// 回调在后台线程上触发，界面层需要自己切回 UI 线程。
	/// </summary>
	public static class ProcessRunner
	{
		public static ProcessResult Run(string executable, string arguments, string workingDirectory, Action<string> onLine, CancellationToken token)
		{
			return Run(executable, arguments, workingDirectory, onLine, token, 0);
		}

		/// <summary>
		/// 运行一个进程并等待结束。
		/// </summary>
		/// <param name="timeoutMs">超时毫秒数，0 表示一直等。</param>
		public static ProcessResult Run(string executable, string arguments, string workingDirectory, Action<string> onLine, CancellationToken token, int timeoutMs)
		{
			ProcessResult result = new ProcessResult();

			if (string.IsNullOrWhiteSpace(executable))
			{
				result.Error = "没有指定要运行的程序。";
				return result;
			}

			ProcessStartInfo info = new ProcessStartInfo();
			info.FileName = executable;
			info.Arguments = arguments ?? "";
			if (!string.IsNullOrEmpty(workingDirectory)) info.WorkingDirectory = workingDirectory;
			info.UseShellExecute = false;
			info.CreateNoWindow = true;
			info.RedirectStandardOutput = true;
			info.RedirectStandardError = true;
			try
			{
				info.StandardOutputEncoding = Encoding.UTF8;
				info.StandardErrorEncoding = Encoding.UTF8;
			}
			catch
			{
				// 个别系统上编码不可设置，忽略即可。
			}

			StringBuilder buffer = new StringBuilder();
			object gate = new object();

			Process process;
			try
			{
				process = Process.Start(info);
			}
			catch (Exception ex)
			{
				result.Error = "无法启动 " + Path.GetFileName(executable) + "：" + ex.Message;
				return result;
			}

			if (process == null)
			{
				result.Error = "无法启动进程。";
				return result;
			}
			result.Started = true;

			Thread outThread = new Thread(() => Pump(process.StandardOutput, onLine, buffer, gate));
			Thread errThread = new Thread(() => Pump(process.StandardError, onLine, buffer, gate));
			outThread.IsBackground = true;
			errThread.IsBackground = true;
			outThread.Start();
			errThread.Start();

			int startTick = Environment.TickCount;
			while (!process.HasExited)
			{
				if (token != null && token.IsCancellationRequested)
				{
					result.Canceled = true;
					KillTree(process);
					break;
				}
				if (timeoutMs > 0 && Environment.TickCount - startTick > timeoutMs)
				{
					result.TimedOut = true;
					KillTree(process);
					break;
				}
				Thread.Sleep(30);
			}

			try { outThread.Join(3000); } catch { }
			try { errThread.Join(3000); } catch { }

			if (result.Canceled) result.ExitCode = -1;
			else
			{
				try { result.ExitCode = process.HasExited ? process.ExitCode : -1; }
				catch { result.ExitCode = -1; }
			}

			lock (gate)
			{
				result.Output = buffer.ToString();
			}

			try { process.Dispose(); } catch { }
			return result;
		}

		private static void Pump(StreamReader reader, Action<string> onLine, StringBuilder buffer, object gate)
		{
			try
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					lock (gate) buffer.Append(line).Append('\n');
					if (onLine != null)
					{
						try { onLine(line); }
						catch { }
					}
				}
			}
			catch
			{
				// 进程被强制结束时读取会抛异常，忽略。
			}
		}

		/// <summary>连同子进程一起结束（cmake 会再起编译器，只杀父进程会留下孤儿）。</summary>
		private static void KillTree(Process process)
		{
			try
			{
				if (process.HasExited) return;
				string systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
				string taskkill = string.IsNullOrEmpty(systemRoot)
					? "taskkill.exe"
					: Path.Combine(systemRoot, "System32", "taskkill.exe");

				ProcessStartInfo info = new ProcessStartInfo(taskkill, "/T /F /PID " + process.Id);
				info.UseShellExecute = false;
				info.CreateNoWindow = true;
				Process.Start(info);
			}
			catch
			{
				try { process.Kill(); }
				catch { }
			}
		}

		/// <summary>给命令行参数加引号（内部再用双引号转义）。</summary>
		public static string Quote(string value)
		{
			if (value == null) return "\"\"";
			if (value.Length == 0) return "\"\"";
			if (value.IndexOf(' ') < 0 && value.IndexOf('"') < 0 && value.IndexOf('\t') < 0) return value;
			return "\"" + value.Replace("\"", "\\\"") + "\"";
		}

		/// <summary>把多段参数拼成命令行。</summary>
		public static string BuildArguments(params string[] parts)
		{
			if (parts == null) return "";
			List<string> pieces = new List<string>();
			foreach (string part in parts)
			{
				if (part == null) continue;
				if (part.Length == 0) continue;
				pieces.Add(part);
			}
			return string.Join(" ", pieces.ToArray());
		}

		/// <summary>取一段输出的第一行，用来展示版本号。</summary>
		public static string FirstLine(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return "";
			string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			foreach (string line in lines)
			{
				string trimmed = line.Trim();
				if (trimmed.Length > 0) return trimmed;
			}
			return "";
		}
	}
}
