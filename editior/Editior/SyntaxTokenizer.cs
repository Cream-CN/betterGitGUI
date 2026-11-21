using System;
using System.Collections.Generic;
using System.Globalization;

namespace Editior
{
	/// <summary>
	/// 单行分词器：给定行文本和进入该行时的状态，输出着色片段与行尾状态。
	/// </summary>
	public static class SyntaxTokenizer
	{
		private const int StateNormal = 0;
		private const int StateBlockComment = 1;
		private const int StateMultiString = 2;
		private const int StateFence = 3;

		public static void Tokenize(SyntaxDefinition def, string line, int startState,
			List<SyntaxToken> tokens, out int endState)
		{
			tokens.Clear();
			endState = startState;

			int n = line.Length;

			// 继续上一行未结束的块注释
			if (startState == StateBlockComment && def.BlockClose.Length > 0)
			{
				int close = line.IndexOf(def.BlockClose, StringComparison.Ordinal);
				if (close < 0)
				{
					Add(tokens, 0, n, TokenKind.Comment);
					endState = StateBlockComment;
					return;
				}
				Add(tokens, 0, close + def.BlockClose.Length, TokenKind.Comment);
				endState = StateNormal;
				TokenizeFrom(def, line, close + def.BlockClose.Length, tokens, out endState);
				return;
			}

			// 继续上一行未结束的多行字符串
			if (startState == StateMultiString)
			{
				string delim = def.TripleQuotes ? TripleDelimiter(line, 0) : "\"\"\"";
				if (delim.Length == 0) delim = "\"\"\"";
				int close = line.IndexOf(delim, StringComparison.Ordinal);
				if (close < 0)
				{
					Add(tokens, 0, n, TokenKind.String);
					endState = StateMultiString;
					return;
				}
				Add(tokens, 0, close + delim.Length, TokenKind.String);
				endState = StateNormal;
				TokenizeFrom(def, line, close + delim.Length, tokens, out endState);
				return;
			}

			// 继续 Markdown 代码块
			if (startState == StateFence)
			{
				string t = line.TrimStart();
				if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal))
				{
					Add(tokens, 0, n, TokenKind.Comment);
					endState = StateNormal;
				}
				else
				{
					Add(tokens, 0, n, TokenKind.String);
					endState = StateFence;
				}
				return;
			}

			TokenizeFrom(def, line, 0, tokens, out endState);
		}

		private static void TokenizeFrom(SyntaxDefinition def, string line, int start,
			List<SyntaxToken> tokens, out int endState)
		{
			endState = StateNormal;
			if (start >= line.Length) return;

			if (def.MarkdownLike)
			{
				TokenizeMarkdown(def, line, start, tokens, out endState);
				return;
			}

			if (def.XmlLike)
			{
				TokenizeXml(def, line, start, tokens, out endState);
				return;
			}

			TokenizeGeneric(def, line, start, tokens, out endState);
		}

		private static void TokenizeGeneric(SyntaxDefinition def, string line, int start,
			List<SyntaxToken> tokens, out int endState)
		{
			endState = StateNormal;
			int n = line.Length;
			int firstNonSpace = start;
			while (firstNonSpace < n && IsWhite(line[firstNonSpace])) firstNonSpace++;

			// 预处理指令整行着色
			if (def.PreprocessorHash && firstNonSpace == start && start < n && line[start] == '#')
			{
				Add(tokens, start, n - start, TokenKind.Preprocessor);
				return;
			}

			int i = start;
			while (i < n)
			{
				char c = line[i];

				// C# 特性 [ ... ]
				if (def.Attributes && c == '[' && i == firstNonSpace && i > 0)
				{
					int close = line.IndexOf(']', i);
					if (close > i)
					{
						Add(tokens, i, close - i + 1, TokenKind.Attribute);
						i = close + 1;
						continue;
					}
				}

				// 行注释
				bool matchedComment = false;
				for (int k = 0; k < def.LineComments.Length; k++)
				{
					string marker = def.LineComments[k];
					if (marker.Length == 0) continue;
					if (StartsWithAt(line, i, marker))
					{
						if (marker == "REM" && i > 0 && (IsIdentPart(line[i - 1]))) continue;
						Add(tokens, i, n - i, TokenKind.Comment);
						i = n;
						matchedComment = true;
						break;
					}
				}
				if (matchedComment) break;

				// 块注释
				if (def.BlockOpen.Length > 0 && StartsWithAt(line, i, def.BlockOpen))
				{
					int close = line.IndexOf(def.BlockClose, i + def.BlockOpen.Length, StringComparison.Ordinal);
					if (close < 0)
					{
						Add(tokens, i, n - i, TokenKind.Comment);
						endState = StateBlockComment;
						i = n;
						break;
					}
					Add(tokens, i, close + def.BlockClose.Length - i, TokenKind.Comment);
					i = close + def.BlockClose.Length;
					continue;
				}

				// 三引号字符串
				if (def.TripleQuotes && (c == '"' || c == '\'') && i + 2 < n + 1)
				{
					string triple = new string(c, 3);
					if (StartsWithAt(line, i, triple))
					{
						int close = line.IndexOf(triple, i + 3, StringComparison.Ordinal);
						if (close < 0)
						{
							Add(tokens, i, n - i, TokenKind.String);
							endState = StateMultiString;
							i = n;
							break;
						}
						Add(tokens, i, close + 3 - i, TokenKind.String);
						i = close + 3;
						continue;
					}
				}

				// 字符串
				if (IsQuote(def, c))
				{
					int strEnd = ScanString(line, i, c);
					Add(tokens, i, strEnd - i, TokenKind.String);
					i = strEnd;
					continue;
				}

				// 数字
				if (char.IsDigit(c) && (i == 0 || !IsIdentPart(line[i - 1])))
				{
					int numEnd = ScanNumber(line, i);
					Add(tokens, i, numEnd - i, TokenKind.Number);
					i = numEnd;
					continue;
				}

				// 标识符
				if (IsIdentStart(c))
				{
					int wordEnd = i + 1;
					while (wordEnd < n && IsIdentPart(line[wordEnd])) wordEnd++;
					string word = line.Substring(i, wordEnd - i);
					TokenKind kind = Classify(def, line, i, wordEnd, word);
					if (kind != TokenKind.Text) Add(tokens, i, wordEnd - i, kind);
					i = wordEnd;
					continue;
				}

				i++;
			}
		}

		private static TokenKind Classify(SyntaxDefinition def, string line, int wordStart, int wordEnd, string word)
		{
			if (def.IsControl(word)) return TokenKind.ControlKeyword;
			if (Contains(def.Keywords, word)) return TokenKind.Keyword;
			if (def.IsType(word)) return TokenKind.Type;
			if (def.IsLiteral(word)) return TokenKind.Literal;

			// 成员访问 a.b 中的 b
			int p = wordStart - 1;
			while (p >= 0 && IsWhite(line[p])) p--;
			if (p >= 0 && line[p] == '.') return TokenKind.Property;

			int q = wordEnd;
			while (q < line.Length && IsWhite(line[q])) q++;
			if (q < line.Length && line[q] == '(') return TokenKind.Function;

			if (def.PropertyBeforeColon && q < line.Length && line[q] == ':') return TokenKind.Property;
			if (def.PropertyBeforeColon && q + 1 < line.Length && line[q] == ':' && line[q + 1] == ':') return TokenKind.Property;

			return TokenKind.Text;
		}

		private static bool Contains(string[] set, string word)
		{
			if (set == null || set.Length == 0) return false;
			if (set == null) return false;
			for (int i = 0; i < set.Length; i++)
			{
				if (set[i] == word) return true;
			}
			return false;
		}

		private static void TokenizeXml(SyntaxDefinition def, string line, int start,
			List<SyntaxToken> tokens, out int endState)
		{
			endState = StateNormal;
			int n = line.Length;
			int i = start;
			while (i < n)
			{
				char c = line[i];

				if (StartsWithAt(line, i, "<!--"))
				{
					int close = line.IndexOf("-->", i + 4, StringComparison.Ordinal);
					if (close < 0)
					{
						Add(tokens, i, n - i, TokenKind.Comment);
						endState = StateBlockComment;
						return;
					}
					Add(tokens, i, close + 3 - i, TokenKind.Comment);
					i = close + 3;
					continue;
				}

				if (c == '<')
				{
					int tagEnd = ScanTag(def, line, i, tokens);
					i = tagEnd;
					continue;
				}

				if (c == '&')
				{
					int semi = line.IndexOf(';', i);
					if (semi > i && semi - i <= 10) { Add(tokens, i, semi - i + 1, TokenKind.Literal); i = semi + 1; continue; }
				}

				i++;
			}
		}

		private static int ScanTag(SyntaxDefinition def, string line, int start, List<SyntaxToken> tokens)
		{
			int n = line.Length;
			int i = start + 1;
			if (i >= n) { Add(tokens, start, 1, TokenKind.Tag); return n; }

			// 用户名 / 结束标签
			int nameStart = i;
			if (line[i] == '/' || line[i] == '!' || line[i] == '?') i++;
			while (i < n && (IsIdentPart(line[i]) || line[i] == '-' || line[i] == ':')) i++;
			if (i > nameStart) Add(tokens, start, i - start, TokenKind.Tag);

			bool inQuote = false;
			char quoteChar = '\0';
			while (i < n)
			{
				char c = line[i];
				if (inQuote)
				{
					int q = i;
					while (q < n)
					{
						if (line[q] == quoteChar) { q++; break; }
						if (line[q] == '\\' && q + 1 < n) q += 2; else q++;
					}
					Add(tokens, i, q - i, TokenKind.String);
					i = q;
					inQuote = false;
					continue;
				}

				if (c == '>' )
				{
					Add(tokens, i, 1, TokenKind.Tag);
					return i + 1;
				}

				if (c == '"' || c == '\'')
				{
					inQuote = true;
					quoteChar = c;
					continue;
				}

				if (IsIdentStart(c))
				{
					int s = i;
					while (i < n && (IsIdentPart(line[i]) || line[i] == '-' || line[i] == ':')) i++;
					Add(tokens, s, i - s, TokenKind.Attribute);
					continue;
				}

				i++;
			}
			return i;
		}

		private static void TokenizeMarkdown(SyntaxDefinition def, string line, int start,
			List<SyntaxToken> tokens, out int endState)
		{
			endState = StateNormal;
			int n = line.Length;
			int i = start;
			while (i < n && line[i] == ' ') i++;

			if (i < n && (StartsWithAt(line, i, "```") || StartsWithAt(line, i, "~~~")))
			{
				Add(tokens, start, n - start, TokenKind.Comment);
				endState = StateFence;
				return;
			}

			if (i < n && line[i] == '#')
			{
				int j = i;
				while (j < n && line[j] == '#') j++;
				if (j > i && j < n && line[j] == ' ')
				{
					Add(tokens, i, n - i, TokenKind.Heading);
					return;
				}
			}

			// 无序列表 / 引用标记
			if (i < n && ("-*+>".IndexOf(line[i]) >= 0) && i + 1 < n && line[i + 1] == ' ')
			{
				Add(tokens, i, 1, TokenKind.Keyword);
				i += 1;
			}

			while (i < n)
			{
				char c = line[i];
				if (c == '`')
				{
					int close = line.IndexOf('`', i + 1);
					if (close > i)
					{
						Add(tokens, i, close - i + 1, TokenKind.String);
						i = close + 1;
						continue;
					}
				}
				if (c == '[')
				{
					int rb = line.IndexOf(']', i + 1);
					if (rb > i && rb + 1 < n && line[rb + 1] == '(')
					{
						int rp = line.IndexOf(')', rb + 2);
						if (rp > rb)
						{
							Add(tokens, i + 1, rb - i - 1, TokenKind.Link);
							Add(tokens, rb + 2, rp - rb - 2, TokenKind.String);
							i = rp + 1;
							continue;
						}
					}
				}
				i++;
			}
		}

		private static string TripleDelimiter(string line, int i)
		{
			return "\"\"\"";
		}

		private static int ScanString(string line, int start, char quote)
		{
			int n = line.Length;
			int i = start + 1;
			while (i < n)
			{
				char c = line[i];
				if (c == '\\' && quote != '\'' || (c == '\\' && quote == '\'' && i + 1 < n && line[i + 1] == '\\'))
				{
					i += 2;
					continue;
				}
				if (c == '\\')
				{
					i += 2;
					continue;
				}
				if (c == quote) return i + 1;
				i++;
			}
			return n;
		}

		private static int ScanNumber(string line, int start)
		{
			int n = line.Length;
			int i = start;
			if (i + 1 < n && line[i] == '0' && (line[i + 1] == 'x' || line[i + 1] == 'X' || line[i + 1] == 'b' || line[i + 1] == 'B'))
			{
				i += 2;
				while (i < n && (Uri.IsHexDigit(line[i]) || line[i] == '_')) i++;
				return i;
			}
			while (i < n && (char.IsDigit(line[i]) || line[i] == '_' || line[i] == '.')) i++;
			if (i < n && (line[i] == 'e' || line[i] == 'E'))
			{
				i++;
				if (i < n && (line[i] == '+' || line[i] == '-')) i++;
				while (i < n && char.IsDigit(line[i])) i++;
			}
			while (i < n && "fFdDmMLluUnn".IndexOf(line[i]) >= 0) i++;
			return i;
		}

		private static bool IsQuote(SyntaxDefinition def, char c)
		{
			if (def.BacktickString && c == '`') return true;
			for (int i = 0; i < def.Quotes.Length; i++)
			{
				if (def.Quotes[i].Length == 1 && def.Quotes[i][0] == c) return true;
			}
			return false;
		}

		private static bool StartsWithAt(string line, int index, string value)
		{
			if (value.Length == 0) return false;
			if (index + value.Length > line.Length) return false;
			return string.CompareOrdinal(line, index, value, 0, value.Length) == 0;
		}

		private static bool IsWhite(char c)
		{
			return c == ' ' || c == '\t';
		}

		internal static bool IsIdentStart(char c)
		{
			return char.IsLetter(c) || c == '_' || c == '$' || c == '@';
		}

		internal static bool IsIdentPart(char c)
		{
			return char.IsLetterOrDigit(c) || c == '_' || c == '$';
		}

		private static void Add(List<SyntaxToken> tokens, int start, int length, TokenKind kind)
		{
			if (length <= 0) return;
			tokens.Add(new SyntaxToken(start, length, kind));
		}
	}
}
