using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace VisibleCMake.Runtime
{
	/// <summary>
	/// 界面布局自检：把主窗体在屏幕外创建出来，逐个页签检查 StackPanel 里的控件有没有互相压住，
	/// 免得表单多得靠肉眼才能发现重叠。用法：VisibleCMake.exe --uitest [日志路径]
	/// </summary>
	public static class UiTest
	{
		public static bool Run(string logPath)
		{
			StringBuilder log = new StringBuilder();
			bool ok = true;

			try
			{
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(false);

				UI.MainForm form = new UI.MainForm(new string[0]);
				form.StartPosition = FormStartPosition.Manual;
				form.Location = new Point(-20000, -20000);
				form.ShowInTaskbar = false;
				form.Width = 1200;
				form.Height = 800;
				form.Show();
				Pump();

				TabControl tabs = FindTabControl(form);
				int pageCount = tabs == null ? 1 : tabs.TabCount;
				log.AppendLine("窗体大小：" + form.Width + " x " + form.Height + "，页签 " + pageCount + " 个");

				for (int index = 0; index < pageCount; index++)
				{
					if (tabs != null)
					{
						tabs.SelectedIndex = index;
						Pump();
					}

					List<string> problems = new List<string>();
					CheckStackPanels(form, problems, "");

					if (problems.Count == 0)
					{
						log.AppendLine("[通过] 第 " + (index + 1) + " 页布局没有重叠");
					}
					else
					{
						ok = false;
						foreach (string problem in problems) log.AppendLine("[重叠] " + problem);
					}
				}

				form.Hide();
				form.Dispose();
			}
			catch (Exception ex)
			{
				ok = false;
				log.AppendLine("检查过程抛出异常：" + ex);
			}

			string text = "VisibleCMake 界面布局检查 — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n"
				+ log.ToString() + "\r\n结论：" + (ok ? "全部通过" : "发现问题");

			try
			{
				string path = logPath ?? Path.Combine(Path.GetTempPath(), "visiblecmake-uitest.log");
				File.WriteAllText(path, text, new UTF8Encoding(false));
			}
			catch { }

			return ok;
		}

		/// <summary>递归找出所有 StackPanel，检查它的直接子控件有没有两两重叠。</summary>
		private static void CheckStackPanels(Control root, List<string> problems, string path)
		{
			if (root is UI.StackPanel)
			{
				UI.StackPanel stack = (UI.StackPanel)root;
				List<Control> children = new List<Control>();
				foreach (Control child in stack.Controls)
				{
					if (!child.Visible) continue;
					if (child is ScrollBar) continue;   // 自带的竖向滚动条不算内容
					children.Add(child);
				}

				for (int i = 0; i < children.Count; i++)
				{
					for (int j = i + 1; j < children.Count; j++)
					{
						Rectangle a = children[i].Bounds;
						Rectangle b = children[j].Bounds;
						Rectangle overlap = Rectangle.Intersect(a, b);
						if (overlap.Width > 2 && overlap.Height > 2)
						{
							problems.Add(path + " / " + Describe(children[i]) + " 与 " + Describe(children[j])
								+ " 重叠 " + overlap.Width + "x" + overlap.Height);
						}
					}
				}

				foreach (Control child in children)
				{
					if (child.Width < 80)
					{
						problems.Add(path + " / " + Describe(child) + " 宽度只有 " + child.Width);
					}
				}

				if (stack.HorizontalScroll.Visible)
				{
					problems.Add(path + " / 出现横向滚动条");
				}
			}

			foreach (Control child in root.Controls)
			{
				CheckStackPanels(child, problems, path + "/" + child.GetType().Name);
			}
		}

		private static string Describe(Control control)
		{
			string text = control.Text;
			if (string.IsNullOrEmpty(text)) text = control.GetType().Name;
			if (text.Length > 18) text = text.Substring(0, 18) + "…";
			return control.GetType().Name + "(" + text + ")";
		}

		private static TabControl FindTabControl(Control root)
		{
			if (root is TabControl) return (TabControl)root;
			foreach (Control child in root.Controls)
			{
				TabControl found = FindTabControl(child);
				if (found != null) return found;
			}
			return null;
		}

		private static void Pump()
		{
			for (int i = 0; i < 6; i++)
			{
				Application.DoEvents();
				Thread.Sleep(40);
			}
		}
	}
}
