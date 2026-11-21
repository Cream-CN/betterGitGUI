using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>
	/// 自绘的鸟瞰图（minimap）：把整篇文本压缩成一根根短线，
	/// 并画出当前可视区域，点击/拖动可以快速定位。
	/// </summary>
	public class MinimapControl : Control
	{
		private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
		private const int EM_LINESCROLL = 0x00B6;

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		private RichTextBox _target;
		private bool _dragging;
		private int _lineHeight = 14;

		private static readonly Color BackColorValue = Color.FromArgb(246, 246, 246);
		private static readonly Color CodeBar = Color.FromArgb(178, 182, 190);
		private static readonly Color CommentBar = Color.FromArgb(138, 176, 138);
		private static readonly Color ViewportFill = Color.FromArgb(55, 110, 150, 205);
		private static readonly Color ViewportBorder = Color.FromArgb(150, 60, 105, 160);

		public MinimapControl()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			BackColor = BackColorValue;
			Cursor = Cursors.Hand;
			Width = 92;
		}

		public RichTextBox Target
		{
			get { return _target; }
		}

		public void Attach(RichTextBox target)
		{
			if (_target == target)
			{
				Invalidate();
				return;
			}
			if (_target != null)
			{
				_target.VScroll -= OnTargetScroll;
				_target.TextChanged -= OnTargetScroll;
			}
			_target = target;
			if (_target != null)
			{
				_target.VScroll += OnTargetScroll;
				_target.TextChanged += OnTargetScroll;
				try
				{
					int h = TextRenderer.MeasureText("Ag", _target.Font,
						new Size(int.MaxValue, int.MaxValue),
						TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
					if (h > 0) _lineHeight = h;
				}
				catch { }
			}
			Invalidate();
		}

		private void OnTargetScroll(object sender, EventArgs e)
		{
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			g.Clear(BackColorValue);

			RichTextBox target = _target;
			if (target == null || target.TextLength == 0) return;

			string[] lines;
			try { lines = target.Lines; }
			catch { return; }
			int total = lines.Length;
			if (total <= 0) return;

			int width = Width - 8;
			float scale = (float)(Height - 4) / total;
			float barHeight = Math.Min(4f, Math.Max(1f, scale));

			// 行数远大于像素高度时按步长抽样，避免每条都画
			int step = Math.Max(1, (int)Math.Floor(total / Math.Max(1f, Height * 1.5f)));

			using (SolidBrush codeBrush = new SolidBrush(CodeBar))
			using (SolidBrush commentBrush = new SolidBrush(CommentBar))
			{
				for (int i = 0; i < total; i += step)
				{
					string line = lines[i];
					string trimmed = line.TrimStart();
					if (trimmed.Length == 0) continue;

					int y = (int)(i * scale) + (int)Math.Max(0f, (scale - barHeight) / 2f);
					int barWidth = Math.Min(width, 3 + (int)(Math.Min(line.Length, 120) * 1.2));
					if (barWidth < 3) barWidth = 3;

					bool comment = trimmed.StartsWith("//", StringComparison.Ordinal) ||
						trimmed.StartsWith("#", StringComparison.Ordinal) ||
						trimmed.StartsWith("/*", StringComparison.Ordinal) ||
						trimmed.StartsWith("*", StringComparison.Ordinal);
					g.FillRectangle(comment ? commentBrush : codeBrush, 4, y, barWidth, barHeight);
				}
			}

			// 当前可视区域
			int first = GetFirstVisibleLine();
			int visible = Math.Max(1, target.ClientSize.Height / Math.Max(1, _lineHeight));
			float viewY = first * scale;
			float viewH = Math.Max(6f, visible * scale);
			if (viewY + viewH > Height - 2) viewH = Math.Max(6f, Height - 2 - viewY);

			using (SolidBrush fill = new SolidBrush(ViewportFill))
			using (Pen border = new Pen(ViewportBorder))
			{
				RectangleF rect = new RectangleF(0.5f, viewY, Width - 1f, viewH);
				g.FillRectangle(fill, rect);
				g.DrawRectangle(border, rect.X, rect.Y, rect.Width, rect.Height);
			}
		}

		private int GetFirstVisibleLine()
		{
			if (_target == null) return 0;
			try
			{
				return (int)SendMessage(_target.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
			}
			catch { return 0; }
		}

		private void ScrollToLine(int line)
		{
			if (_target == null) return;
			string[] lines = _target.Lines;
			if (line < 0) line = 0;
			if (line >= lines.Length) line = Math.Max(0, lines.Length - 1);
			int first = GetFirstVisibleLine();
			int delta = line - first;
			try
			{
				SendMessage(_target.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
			}
			catch { }
			Invalidate();
		}

		private int LineAtY(int y)
		{
			if (_target == null) return 0;
			int total = _target.Lines.Length;
			float scale = (float)(Height - 4) / Math.Max(1, total);
			if (scale <= 0) return 0;
			return Math.Max(0, Math.Min(total - 1, (int)(y / scale)));
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (e.Button != MouseButtons.Left) return;
			_dragging = true;
			ScrollToLine(LineAtY(e.Y));
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (_dragging) ScrollToLine(LineAtY(e.Y));
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			_dragging = false;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && _target != null)
			{
				_target.VScroll -= OnTargetScroll;
				_target.TextChanged -= OnTargetScroll;
				_target = null;
			}
			base.Dispose(disposing);
		}
	}
}
