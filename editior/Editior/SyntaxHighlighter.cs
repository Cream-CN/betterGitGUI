using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Editior
{
	/// <summary>
	/// 用 RichTextBox 自带的字符着色能力实现语法高亮：
	/// 逐行分词后给每个 token 设置 SelectionColor，不修改文本内容。
	/// </summary>
	public static class SyntaxHighlighter
	{
		private const int WM_SETREDRAW = 0x000B;
		private const int MaxHighlightLength = 200000;

		[DllImport("user32.dll")]
		private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

		public static bool CanHighlight(RichTextBox box)
		{
			return box != null && box.TextLength <= MaxHighlightLength;
		}

		public static void Colorize(RichTextBox box, SyntaxDefinition def)
		{
			if (box == null || box.IsDisposed || def == null) return;
			if (box.TextLength == 0) return;
			if (box.TextLength > MaxHighlightLength) return;

			int selStart = box.SelectionStart;
			int selLength = box.SelectionLength;
			bool redrawOff = false;
			try
			{
				SendMessage(box.Handle, WM_SETREDRAW, 0, 0);
				redrawOff = true;
			}
			catch { }

			try
			{
				string[] lines = box.Lines;
				int state = 0;
				List<SyntaxToken> tokens = new List<SyntaxToken>(64);

				for (int i = 0; i < lines.Length; i++)
				{
					int lineStart = box.GetFirstCharIndexFromLine(i);
					if (lineStart < 0) break;
					if (lines[i].Length == 0) continue;

					tokens.Clear();
					SyntaxTokenizer.Tokenize(def, lines[i], state, tokens, out state);

					for (int t = 0; t < tokens.Count; t++)
					{
						SyntaxToken tok = tokens[t];
						if (tok.Length <= 0) continue;
						Color color = ColorFor(tok.Kind);
						if (color == Color.Empty) continue;
						box.Select(lineStart + tok.Start, tok.Length);
						box.SelectionColor = color;
					}
				}
			}
			catch
			{
				// 高亮只是锦上添花，失败不影响编辑
			}
			finally
			{
				box.Select(Math.Min(selStart, box.TextLength), Math.Min(selLength, Math.Max(0, box.TextLength - selStart)));
				if (redrawOff)
				{
					try { SendMessage(box.Handle, WM_SETREDRAW, 1, 0); } catch { }
				}
				box.Invalidate();
			}
		}

		private static Color ColorFor(TokenKind kind)
		{
			switch (kind)
			{
				case TokenKind.Keyword: return Color.FromArgb(0x00, 0x00, 0xC0);
				case TokenKind.ControlKeyword: return Color.FromArgb(0xAF, 0x00, 0xDB);
				case TokenKind.Type: return Color.FromArgb(0x26, 0x7F, 0x99);
				case TokenKind.Literal: return Color.FromArgb(0x00, 0x00, 0xC0);
				case TokenKind.String: return Color.FromArgb(0xA3, 0x15, 0x15);
				case TokenKind.Comment: return Color.FromArgb(0x00, 0x80, 0x00);
				case TokenKind.Number: return Color.FromArgb(0x09, 0x86, 0x58);
				case TokenKind.Function: return Color.FromArgb(0x79, 0x5E, 0x26);
				case TokenKind.Property: return Color.FromArgb(0x04, 0x51, 0xA5);
				case TokenKind.Tag: return Color.FromArgb(0x80, 0x00, 0x00);
				case TokenKind.Attribute: return Color.FromArgb(0xC8, 0x3A, 0x00);
				case TokenKind.Heading: return Color.FromArgb(0x00, 0x00, 0x80);
				case TokenKind.Link: return Color.FromArgb(0x04, 0x51, 0xA5);
				case TokenKind.Preprocessor: return Color.FromArgb(0x80, 0x80, 0x80);
				default: return Color.Empty;
			}
		}
	}
}
