using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace VisibleCMake.Core
{
	/// <summary>
	/// 极简 JSON 读写（无第三方依赖）：
	/// 序列化时按公共属性反射成对象图并输出带缩进的可读 JSON，
	/// 反序列化时按目标类型逐层还原（string / bool / 数值 / 枚举 / List&lt;T&gt; / 普通类）。
	/// 只用于本程序的 *.vcmake 工程文件与设置文件。
	/// </summary>
	internal static class MiniJson
	{
		// ---------------- 序列化 ----------------

		public static string Serialize(object value)
		{
			StringBuilder sb = new StringBuilder();
			WriteValue(sb, ToNode(value), 0);
			sb.Append("\n");
			return sb.ToString();
		}

		private static object ToNode(object value)
		{
			if (value == null) return null;
			Type type = value.GetType();

			if (value is string) return value;
			if (value is bool) return value;
			if (value is Enum) return value.ToString();
			if (value is int || value is long || value is short || value is byte
				|| value is double || value is float || value is decimal)
			{
				return Convert.ToDouble(value, CultureInfo.InvariantCulture);
			}
			if (value is IDictionary)
			{
				Dictionary<string, object> map = new Dictionary<string, object>();
				foreach (DictionaryEntry entry in (IDictionary)value)
				{
					map[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = ToNode(entry.Value);
				}
				return map;
			}
			if (value is IEnumerable)
			{
				List<object> list = new List<object>();
				foreach (object item in (IEnumerable)value) list.Add(ToNode(item));
				return list;
			}

			Dictionary<string, object> result = new Dictionary<string, object>();
			foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (property.GetIndexParameters().Length > 0) continue;
				if (property.GetGetMethod() == null) continue;
				result[property.Name] = ToNode(property.GetValue(value, null));
			}
			return result;
		}

		private static void WriteValue(StringBuilder sb, object node, int indent)
		{
			if (node == null)
			{
				sb.Append("null");
				return;
			}
			if (node is string)
			{
				WriteString(sb, (string)node);
				return;
			}
			if (node is bool)
			{
				sb.Append(((bool)node) ? "true" : "false");
				return;
			}
			if (node is double)
			{
				double number = (double)node;
				if (number == Math.Floor(number) && Math.Abs(number) < 1e15)
					sb.Append(((long)number).ToString(CultureInfo.InvariantCulture));
				else
					sb.Append(number.ToString("R", CultureInfo.InvariantCulture));
				return;
			}
			if (node is Dictionary<string, object>)
			{
				Dictionary<string, object> map = (Dictionary<string, object>)node;
				if (map.Count == 0)
				{
					sb.Append("{}");
					return;
				}
				sb.Append("{\n");
				int index = 0;
				foreach (KeyValuePair<string, object> pair in map)
				{
					WriteIndent(sb, indent + 1);
					WriteString(sb, pair.Key);
					sb.Append(": ");
					WriteValue(sb, pair.Value, indent + 1);
					index++;
					if (index < map.Count) sb.Append(',');
					sb.Append('\n');
				}
				WriteIndent(sb, indent);
				sb.Append('}');
				return;
			}
			if (node is List<object>)
			{
				List<object> list = (List<object>)node;
				if (list.Count == 0)
				{
					sb.Append("[]");
					return;
				}
				sb.Append("[\n");
				for (int i = 0; i < list.Count; i++)
				{
					WriteIndent(sb, indent + 1);
					WriteValue(sb, list[i], indent + 1);
					if (i < list.Count - 1) sb.Append(',');
					sb.Append('\n');
				}
				WriteIndent(sb, indent);
				sb.Append(']');
				return;
			}
			WriteString(sb, Convert.ToString(node, CultureInfo.InvariantCulture));
		}

		private static void WriteIndent(StringBuilder sb, int indent)
		{
			sb.Append(' ', indent * 2);
		}

		private static void WriteString(StringBuilder sb, string value)
		{
			sb.Append('"');
			foreach (char c in value)
			{
				switch (c)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					case '\b': sb.Append("\\b"); break;
					case '\f': sb.Append("\\f"); break;
					default:
						if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
						else sb.Append(c);
						break;
				}
			}
			sb.Append('"');
		}

		// ---------------- 反序列化 ----------------

		public static T Deserialize<T>(string json) where T : class, new()
		{
			object node = Parse(json);
			object result = FromNode(node, typeof(T));
			return result as T;
		}

		private static object FromNode(object node, Type type)
		{
			if (node == null) return null;

			if (type == typeof(string)) return Convert.ToString(node, CultureInfo.InvariantCulture);
			if (type == typeof(bool))
			{
				if (node is bool) return node;
				if (node is double) return (double)node != 0;
				return string.Equals(Convert.ToString(node), "true", StringComparison.OrdinalIgnoreCase);
			}
			if (type == typeof(int)) return Convert.ToInt32(node, CultureInfo.InvariantCulture);
			if (type == typeof(long)) return Convert.ToInt64(node, CultureInfo.InvariantCulture);
			if (type == typeof(double)) return Convert.ToDouble(node, CultureInfo.InvariantCulture);
			if (type == typeof(float)) return (float)Convert.ToDouble(node, CultureInfo.InvariantCulture);
			if (type.IsEnum)
			{
				if (node is double) return Enum.ToObject(type, Convert.ToInt32(node));
				return Enum.Parse(type, Convert.ToString(node), true);
			}
			if (node is List<object>)
			{
				List<object> list = (List<object>)node;
				if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
				{
					Type itemType = type.GetGenericArguments()[0];
					IList result = (IList)Activator.CreateInstance(type);
					foreach (object item in list) result.Add(FromNode(item, itemType));
					return result;
				}
				return null;
			}
			if (node is Dictionary<string, object>)
			{
				Dictionary<string, object> map = (Dictionary<string, object>)node;
				object instance = Activator.CreateInstance(type);
				foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
				{
					if (property.GetSetMethod() == null) continue;
					if (!map.ContainsKey(property.Name)) continue;
					object converted = FromNode(map[property.Name], property.PropertyType);
					if (converted != null) property.SetValue(instance, converted, null);
				}
				return instance;
			}
			return null;
		}

		public static object Parse(string text)
		{
			if (text == null) throw new FormatException("JSON 内容为空。");
			Parser parser = new Parser(text);
			return parser.ParseDocument();
		}

		private sealed class Parser
		{
			private readonly string _text;
			private int _pos;

			public Parser(string text)
			{
				_text = text;
				_pos = 0;
			}

			public object ParseDocument()
			{
				SkipWhitespace();
				object value = ParseValue();
				SkipWhitespace();
				if (_pos < _text.Length) throw new FormatException("JSON 结尾有多余内容（位置 " + _pos + "）。");
				return value;
			}

			private object ParseValue()
			{
				SkipWhitespace();
				if (_pos >= _text.Length) throw new FormatException("JSON 意外结束。");
				char c = _text[_pos];
				switch (c)
				{
					case '{': return ParseObject();
					case '[': return ParseArray();
					case '"': return ParseString();
					case 't': Expect("true"); return true;
					case 'f': Expect("false"); return false;
					case 'n': Expect("null"); return null;
					default: return ParseNumber();
				}
			}

			private Dictionary<string, object> ParseObject()
			{
				Dictionary<string, object> map = new Dictionary<string, object>();
				_pos++; // '{'
				SkipWhitespace();
				if (_pos < _text.Length && _text[_pos] == '}')
				{
					_pos++;
					return map;
				}
				while (true)
				{
					SkipWhitespace();
					if (_pos >= _text.Length) throw new FormatException("对象没有闭合。");
					if (_text[_pos] == '}') { _pos++; return map; }   // 容忍末尾多余的逗号
					if (_text[_pos] != '"') throw new FormatException("对象键必须是字符串（位置 " + _pos + "）。");
					string key = ParseString();
					SkipWhitespace();
					Expect(":");
					object value = ParseValue();
					map[key] = value;
					SkipWhitespace();
					if (_pos < _text.Length && _text[_pos] == ',') { _pos++; continue; }
					SkipWhitespace();
					Expect("}");
					return map;
				}
			}

			private List<object> ParseArray()
			{
				List<object> list = new List<object>();
				_pos++; // '['
				SkipWhitespace();
				if (_pos < _text.Length && _text[_pos] == ']')
				{
					_pos++;
					return list;
				}
				while (true)
				{
					SkipWhitespace();
					if (_pos >= _text.Length) throw new FormatException("数组没有闭合。");
					if (_text[_pos] == ']') { _pos++; return list; }
					list.Add(ParseValue());
					SkipWhitespace();
					if (_pos < _text.Length && _text[_pos] == ',') { _pos++; continue; }
					SkipWhitespace();
					Expect("]");
					return list;
				}
			}

			private string ParseString()
			{
				_pos++; // '"'
				StringBuilder sb = new StringBuilder();
				while (_pos < _text.Length)
				{
					char c = _text[_pos++];
					if (c == '"') return sb.ToString();
					if (c != '\\')
					{
						sb.Append(c);
						continue;
					}
					if (_pos >= _text.Length) break;
					char escape = _text[_pos++];
					switch (escape)
					{
						case '"': sb.Append('"'); break;
						case '\\': sb.Append('\\'); break;
						case '/': sb.Append('/'); break;
						case 'b': sb.Append('\b'); break;
						case 'f': sb.Append('\f'); break;
						case 'n': sb.Append('\n'); break;
						case 'r': sb.Append('\r'); break;
						case 't': sb.Append('\t'); break;
						case 'u':
							if (_pos + 4 > _text.Length) throw new FormatException("\\u 转义不完整。");
							sb.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
							_pos += 4;
							break;
						default:
							throw new FormatException("无法识别的转义 \\" + escape + "（位置 " + _pos + "）。");
					}
				}
				throw new FormatException("字符串没有闭合。");
			}

			private double ParseNumber()
			{
				int start = _pos;
				if (_pos < _text.Length && (_text[_pos] == '-' || _text[_pos] == '+')) _pos++;
				while (_pos < _text.Length)
				{
					char c = _text[_pos];
					if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') _pos++;
					else break;
				}
				if (_pos == start) throw new FormatException("无法识别的 JSON 值（位置 " + _pos + "）。");
				string token = _text.Substring(start, _pos - start);
				double value;
				if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
					throw new FormatException("数字格式不正确：" + token);
				return value;
			}

			private void Expect(string token)
			{
				if (_pos + token.Length > _text.Length) throw new FormatException("期望 \"" + token + "\"，但内容已结束。");
				if (!string.Equals(_text.Substring(_pos, token.Length), token, StringComparison.Ordinal))
					throw new FormatException("期望 \"" + token + "\"（位置 " + _pos + "）。");
				_pos += token.Length;
			}

			private void SkipWhitespace()
			{
				while (_pos < _text.Length)
				{
					char c = _text[_pos];
					if (c == ' ' || c == '\t' || c == '\r' || c == '\n') _pos++;
					else break;
				}
			}
		}
	}
}
