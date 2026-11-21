using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Editior
{
	public enum TokenKind
	{
		Text,
		Keyword,
		ControlKeyword,
		Type,
		Literal,
		String,
		Comment,
		Number,
		Function,
		Property,
		Operator,
		Bracket,
		Tag,
		Attribute,
		Heading,
		Link,
		Preprocessor
	}

	public struct SyntaxToken
	{
		public int Start;
		public int Length;
		public TokenKind Kind;

		public SyntaxToken(int start, int length, TokenKind kind)
		{
			Start = start;
			Length = length;
			Kind = kind;
		}
	}

	/// <summary>
	/// 单行高亮结果缓存：EndState 是下一行开始时的状态。
	/// 状态 0 = 普通，1 = 块注释，2 = 多行字符串，3 = Markdown 代码块。
	/// </summary>
	public class SyntaxLine
	{
		public List<SyntaxToken> Tokens = new List<SyntaxToken>();
		public int EndState;
	}

	public class SyntaxDefinition
	{
		public string Name = "纯文本";
		public string[] Extensions = new string[0];
		public string[] FileNames = new string[0];

		public string[] LineComments = new string[0];
		public string BlockOpen = "";
		public string BlockClose = "";

		public string[] Keywords = new string[0];
		public string[] ControlKeywords = new string[0];
		public string[] Types = new string[0];
		public string[] Literals = new string[0];

		public string[] Quotes = new string[] { "\"", "'" };
		public bool TripleQuotes;          // Python 的 """ / '''
		public bool PreprocessorHash;      // 行首 # 当作预处理指令
		public bool Attributes;            // C# 的 [Attribute]
		public bool XmlLike;               // HTML / XML
		public bool MarkdownLike;          // Markdown
		public bool PropertyBeforeColon;   // JSON / CSS 里冒号前的标识符当属性
		public bool BacktickString;        // JS 模板字符串
		public bool AtString;              // C# 的 @"..."
		public bool CaseInsensitiveKeywords;

		public bool MatchesExtension(string ext)
		{
			string e = ext.ToLowerInvariant().TrimStart('.');
			return Extensions.Any(x => x == e);
		}

		public bool MatchesFileName(string fileName)
		{
			if (string.IsNullOrEmpty(fileName) || FileNames.Length == 0) return false;
			string name = fileName.ToLowerInvariant();
			return FileNames.Any(x => x.ToLowerInvariant() == name);
		}

		public bool IsKeyword(string word)
		{
			return Lookup(Keywords, word) || Lookup(ControlKeywords, word);
		}

		private bool Lookup(string[] set, string word)
		{
			if (set.Length == 0) return false;
			if (CaseInsensitiveKeywords)
			{
				string lower = word.ToLowerInvariant();
				return set.Any(x => x == lower);
			}
			return Array.IndexOf(set, word) >= 0;
		}

		internal bool IsControl(string word) { return Lookup(ControlKeywords, word); }
		internal bool IsType(string word) { return Lookup(Types, word); }
		internal bool IsLiteral(string word) { return Lookup(Literals, word); }
	}

	public static class SyntaxRegistry
	{
		private static readonly List<SyntaxDefinition> Defs = new List<SyntaxDefinition>();
		public static readonly SyntaxDefinition Plain = new SyntaxDefinition();

		static SyntaxRegistry()
		{
			Defs.Add(BuildCSharp());
			Defs.Add(BuildJavaScript());
			Defs.Add(BuildTypeScript());
			Defs.Add(BuildJson());
			Defs.Add(BuildHtml());
			Defs.Add(BuildXml());
			Defs.Add(BuildCss());
			Defs.Add(BuildPython());
			Defs.Add(BuildJava());
			Defs.Add(BuildCpp());
			Defs.Add(BuildGo());
			Defs.Add(BuildRust());
			Defs.Add(BuildPhp());
			Defs.Add(BuildSql());
			Defs.Add(BuildYaml());
			Defs.Add(BuildPowerShell());
			Defs.Add(BuildShell());
			Defs.Add(BuildBatch());
			Defs.Add(BuildMarkdown());
			Defs.Add(BuildIni());
			Defs.Add(BuildKotlin());
			Defs.Add(BuildSwift());
			Defs.Add(BuildRuby());
			Defs.Add(BuildDart());
			Defs.Add(BuildLua());
			Defs.Add(BuildScala());
			Defs.Add(BuildR());
			Defs.Add(BuildPerl());
			Defs.Add(BuildGroovy());
			Defs.Add(BuildVisualBasic());
			Defs.Add(BuildFSharp());
			Defs.Add(BuildDockerfile());
			Defs.Add(BuildMakefile());
			Defs.Add(BuildCMake());
			Defs.Add(BuildProtobuf());
			Defs.Add(BuildGraphQL());
			Defs.Add(BuildVue());
			Defs.Add(BuildSolidity());
			Defs.Add(BuildAssembly());
			Defs.Add(BuildHaskell());
			Defs.Add(BuildClojure());
		}

		public static IList<SyntaxDefinition> All
		{
			get { return Defs; }
		}

		public static SyntaxDefinition ForName(string name)
		{
			if (string.IsNullOrEmpty(name)) return Plain;
			string fileName = Path.GetFileName(name);
			foreach (SyntaxDefinition d in Defs)
			{
				if (d.MatchesFileName(fileName)) return d;
			}
			string ext = Path.GetExtension(name);
			foreach (SyntaxDefinition d in Defs)
			{
				if (d.MatchesExtension(ext)) return d;
			}
			return Plain;
		}

		public static SyntaxDefinition ForExtension(string ext)
		{
			foreach (SyntaxDefinition d in Defs)
			{
				if (d.MatchesExtension(ext)) return d;
			}
			return Plain;
		}

		private static SyntaxDefinition Base(string name, params string[] exts)
		{
			SyntaxDefinition d = new SyntaxDefinition();
			d.Name = name;
			d.Extensions = exts;
			return d;
		}

		private static SyntaxDefinition BuildCSharp()
		{
			SyntaxDefinition d = Base("C#", "cs", "csx");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "foreach", "while", "do", "switch", "case", "default", "break", "continue", "return", "try", "catch", "finally", "throw", "using", "lock", "goto", "yield", "await", "when", "checked", "unchecked", "fixed", "unsafe", "stackalloc", "nameof", "typeof", "sizeof", "is", "as", "in", "out", "ref", "params", "new", "base", "this", "delegate", "event", "operator" };
			d.Keywords = new[] { "abstract", "async", "class", "const", "enum", "explicit", "extern", "implicit", "interface", "internal", "namespace", "override", "partial", "private", "protected", "public", "readonly", "sealed", "static", "struct", "virtual", "volatile", "get", "set", "init", "record", "with", "var", "dynamic", "global", "assembly" };
			d.Types = new[] { "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint", "long", "ulong", "short", "ushort", "object", "string", "void", "nint", "nuint" };
			d.Literals = new[] { "true", "false", "null", "default" };
			d.PreprocessorHash = true;
			d.Attributes = true;
			d.AtString = true;
			return d;
		}

		private static SyntaxDefinition BuildJavaScript()
		{
			SyntaxDefinition d = Base("JavaScript", "js", "jsx", "mjs", "cjs");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue", "return", "try", "catch", "finally", "throw", "new", "typeof", "instanceof", "delete", "void", "in", "of", "yield", "await", "import", "from" };
			d.Keywords = new[] { "var", "let", "const", "function", "class", "extends", "super", "this", "static", "get", "set", "async", "export", "as", "debugger" };
			d.Types = new[] { "Number", "String", "Boolean", "Object", "Array", "Function", "Symbol", "BigInt", "Map", "Set", "Promise", "Date", "RegExp", "Error", "JSON", "Math" };
			d.Literals = new[] { "true", "false", "null", "undefined", "NaN", "Infinity" };
			d.BacktickString = true;
			return d;
		}

		private static SyntaxDefinition BuildTypeScript()
		{
			SyntaxDefinition d = BuildJavaScript();
			d.Name = "TypeScript";
			d.Extensions = new[] { "ts", "tsx", "mts", "cts" };
			d.Keywords = d.Keywords.Concat(new[] { "interface", "type", "enum", "namespace", "declare", "abstract", "implements", "readonly", "public", "private", "protected", "override", "infer", "keyof", "is", "satisfies", "module" }).ToArray();
			d.Types = d.Types.Concat(new[] { "any", "unknown", "never", "void", "string", "number", "boolean", "object", "symbol", "bigint", "Record", "Partial", "Required", "Readonly", "Pick", "Omit", "Exclude", "Extract", "ReturnType" }).ToArray();
			return d;
		}

		private static SyntaxDefinition BuildJson()
		{
			SyntaxDefinition d = Base("JSON", "json", "jsonc", "json5", "webmanifest");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.Literals = new[] { "true", "false", "null" };
			d.PropertyBeforeColon = true;
			d.TripleQuotes = false;
			return d;
		}

		private static SyntaxDefinition BuildHtml()
		{
			SyntaxDefinition d = Base("HTML", "html", "htm", "vue", "svelte", "razor", "cshtml");
			d.BlockOpen = "<!--";
			d.BlockClose = "-->";
			d.XmlLike = true;
			return d;
		}

		private static SyntaxDefinition BuildXml()
		{
			SyntaxDefinition d = Base("XML", "xml", "xaml", "csproj", "vbproj", "props", "targets", "resx", "svg", "plist", "config", "xsd", "xsl", "dtd", "nuspec");
			d.BlockOpen = "<!--";
			d.BlockClose = "-->";
			d.XmlLike = true;
			return d;
		}

		private static SyntaxDefinition BuildCss()
		{
			SyntaxDefinition d = Base("CSS", "css", "scss", "sass", "less");
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.PropertyBeforeColon = true;
			d.Literals = new[] { "important", "inherit", "initial", "unset", "auto", "none" };
			d.Keywords = new[] { "media", "import", "charset", "keyframes", "font-face", "supports", "extend", "mixin", "include", "if", "else", "for", "each", "while", "function", "return" };
			return d;
		}

		private static SyntaxDefinition BuildPython()
		{
			SyntaxDefinition d = Base("Python", "py", "pyw", "pyi");
			d.LineComments = new[] { "#" };
			d.ControlKeywords = new[] { "if", "elif", "else", "for", "while", "break", "continue", "return", "yield", "try", "except", "finally", "raise", "with", "as", "in", "is", "not", "and", "or", "pass", "assert", "del", "global", "nonlocal", "await", "match", "case", "lambda" };
			d.Keywords = new[] { "def", "class", "import", "from", "async", "self", "cls", "None" };
			d.Types = new[] { "int", "float", "str", "bool", "list", "dict", "set", "tuple", "bytes", "object", "Exception", "type" };
			d.Literals = new[] { "True", "False", "None" };
			d.TripleQuotes = true;
			return d;
		}

		private static SyntaxDefinition BuildJava()
		{
			SyntaxDefinition d = Base("Java", "java");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue", "return", "try", "catch", "finally", "throw", "throws", "new", "instanceof", "this", "super", "synchronized", "assert", "yield" };
			d.Keywords = new[] { "abstract", "class", "interface", "enum", "extends", "implements", "final", "native", "private", "protected", "public", "static", "strictfp", "transient", "volatile", "package", "import", "record", "sealed", "permits", "var" };
			d.Types = new[] { "boolean", "byte", "char", "double", "float", "int", "long", "short", "void", "String", "Object", "Integer", "Long", "Double", "Float", "Boolean", "Character", "List", "Map", "Set", "Optional", "Stream" };
			d.Literals = new[] { "true", "false", "null" };
			d.Attributes = true;
			return d;
		}

		private static SyntaxDefinition BuildCpp()
		{
			SyntaxDefinition d = Base("C/C++", "c", "h", "cpp", "cc", "cxx", "hpp", "hxx", "inl", "ipp");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue", "return", "try", "catch", "throw", "new", "delete", "this", "sizeof", "co_return", "co_yield", "co_await", "goto", "typedef" };
			d.Keywords = new[] { "class", "struct", "union", "enum", "namespace", "template", "typename", "public", "private", "protected", "virtual", "override", "final", "static", "const", "constexpr", "consteval", "constinit", "mutable", "explicit", "inline", "friend", "operator", "using", "extern", "volatile", "register", "thread_local", "decltype", "auto", "noexcept", "concept", "requires" };
			d.Types = new[] { "bool", "char", "wchar_t", "char8_t", "short", "int", "long", "float", "double", "void", "signed", "unsigned", "size_t", "int8_t", "int16_t", "int32_t", "int64_t", "uint8_t", "uint16_t", "uint32_t", "uint64_t", "string", "vector", "map", "set", "unique_ptr", "shared_ptr" };
			d.Literals = new[] { "true", "false", "nullptr", "NULL", "TRUE", "FALSE" };
			d.PreprocessorHash = true;
			d.AtString = false;
			return d;
		}

		private static SyntaxDefinition BuildGo()
		{
			SyntaxDefinition d = Base("Go", "go");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "range", "switch", "case", "default", "break", "continue", "return", "goto", "fallthrough", "select", "go", "defer", "chan", "go" };
			d.Keywords = new[] { "func", "package", "import", "type", "struct", "interface", "map", "var", "const", "make", "new", "append", "copy", "len", "cap", "delete" };
			d.Types = new[] { "bool", "string", "int", "int8", "int16", "int32", "int64", "uint", "uint8", "uint16", "uint32", "uint64", "uintptr", "byte", "rune", "float32", "float64", "complex64", "complex128", "error", "any" };
			d.Literals = new[] { "true", "false", "nil", "iota" };
			d.BacktickString = true;
			return d;
		}

		private static SyntaxDefinition BuildRust()
		{
			SyntaxDefinition d = Base("Rust", "rs");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "match", "loop", "while", "for", "in", "break", "continue", "return", "try", "await", "async", "move", "where", "as", "yield" };
			d.Keywords = new[] { "fn", "let", "mut", "const", "static", "struct", "enum", "trait", "impl", "use", "mod", "pub", "crate", "super", "self", "Self", "type", "dyn", "ref", "unsafe", "extern", "macro_rules", "union" };
			d.Types = new[] { "bool", "char", "str", "i8", "i16", "i32", "i64", "i128", "isize", "u8", "u16", "u32", "u64", "u128", "usize", "f32", "f64", "String", "Vec", "Option", "Result", "Box", "Rc", "Arc" };
			d.Literals = new[] { "true", "false", "None", "Some", "Ok", "Err" };
			return d;
		}

		private static SyntaxDefinition BuildPhp()
		{
			SyntaxDefinition d = Base("PHP", "php");
			d.LineComments = new[] { "//", "#" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "elseif", "for", "foreach", "while", "do", "switch", "case", "default", "break", "continue", "return", "try", "catch", "finally", "throw", "new", "echo", "print", "include", "require", "include_once", "require_once", "as", "instanceof", "clone", "yield", "match" };
			d.Keywords = new[] { "function", "class", "interface", "trait", "extends", "implements", "public", "private", "protected", "static", "abstract", "final", "const", "namespace", "use", "global", "var" };
			d.Types = new[] { "int", "float", "string", "bool", "array", "object", "callable", "iterable", "mixed", "void", "null", "self", "parent" };
			d.Literals = new[] { "true", "false", "null", "TRUE", "FALSE", "NULL" };
			return d;
		}

		private static SyntaxDefinition BuildSql()
		{
			SyntaxDefinition d = Base("SQL", "sql", "ddl", "dml");
			d.LineComments = new[] { "--", "#" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "FULL", "ON", "GROUP", "BY", "ORDER", "HAVING", "LIMIT", "OFFSET", "UNION", "ALL", "AS", "AND", "OR", "NOT", "IN", "EXISTS", "BETWEEN", "LIKE", "CASE", "WHEN", "THEN", "ELSE", "END", "DISTINCT", "DESC", "ASC", "WITH", "RETURNING", "USING", "TOP" };
			d.Keywords = new[] { "CREATE", "TABLE", "ALTER", "DROP", "INDEX", "VIEW", "PRIMARY", "FOREIGN", "KEY", "REFERENCES", "DEFAULT", "NULL", "CONSTRAINT", "UNIQUE", "CHECK", "TRUNCATE", "BEGIN", "COMMIT", "ROLLBACK", "DECLARE", "FUNCTION", "PROCEDURE", "TRIGGER", "GRANT", "REVOKE", "SCHEMA", "DATABASE", "IF", "EXPLAIN", "PARTITION", "OVER", "WINDOW" };
			d.Types = new[] { "INT", "INTEGER", "BIGINT", "SMALLINT", "TINYINT", "DECIMAL", "NUMERIC", "FLOAT", "REAL", "DOUBLE", "CHAR", "VARCHAR", "NVARCHAR", "TEXT", "DATE", "TIME", "DATETIME", "TIMESTAMP", "BOOLEAN", "BOOL", "BLOB", "JSON", "UUID", "SERIAL", "MONEY" };
			d.Literals = new[] { "TRUE", "FALSE", "NULL" };
			d.CaseInsensitiveKeywords = true;
			return d;
		}

		private static SyntaxDefinition BuildYaml()
		{
			SyntaxDefinition d = Base("YAML", "yml", "yaml");
			d.LineComments = new[] { "#" };
			d.Literals = new[] { "true", "false", "null", "yes", "no", "on", "off", "~", "True", "False", "Null" };
			d.PropertyBeforeColon = true;
			d.Keywords = new[] { "version", "name", "on", "jobs", "steps", "run", "uses", "with", "env", "if", "then", "else" };
			return d;
		}

		private static SyntaxDefinition BuildPowerShell()
		{
			SyntaxDefinition d = Base("PowerShell", "ps1", "psm1", "psd1");
			d.LineComments = new[] { "#" };
			d.BlockOpen = "<#";
			d.BlockClose = "#>";
			d.ControlKeywords = new[] { "if", "elseif", "else", "foreach", "for", "while", "do", "until", "switch", "break", "continue", "return", "try", "catch", "finally", "throw", "trap", "in", "param", "begin", "process", "end" };
			d.Keywords = new[] { "function", "filter", "class", "enum", "using", "workflow", "configuration", "module", "requires", "hidden", "static" };
			d.Literals = new[] { "$true", "$false", "$null", "True", "False" };
			d.Types = new[] { "int", "string", "bool", "array", "hashtable", "psobject", "void", "switch", "datetime", "double", "long" };
			d.BacktickString = false;
			return d;
		}

		private static SyntaxDefinition BuildShell()
		{
			SyntaxDefinition d = Base("Shell", "sh", "bash", "zsh", "fish", "ksh");
			d.LineComments = new[] { "#" };
			d.ControlKeywords = new[] { "if", "then", "elif", "else", "fi", "for", "in", "do", "done", "while", "until", "case", "esac", "break", "continue", "return", "function", "select", "time" };
			d.Keywords = new[] { "export", "local", "readonly", "declare", "typeset", "alias", "unalias", "source", "eval", "exec", "set", "unset", "shift", "trap", "wait", "exit", "echo", "printf", "read", "cd", "test" };
			d.Literals = new[] { "true", "false" };
			d.BacktickString = true;
			return d;
		}

		private static SyntaxDefinition BuildBatch()
		{
			SyntaxDefinition d = Base("Batch", "bat", "cmd");
			d.LineComments = new[] { "REM", "::" };
			d.ControlKeywords = new[] { "if", "else", "for", "goto", "call", "exit", "setlocal", "endlocal", "shift", "errorlevel", "exist", "not", "defined" };
			d.Keywords = new[] { "set", "echo", "cd", "dir", "copy", "move", "del", "start", "title", "pause", "cls", "pushd", "popd", "md", "rd" };
			d.Literals = new[] { "on", "off" };
			return d;
		}

		private static SyntaxDefinition BuildMarkdown()
		{
			SyntaxDefinition d = Base("Markdown", "md", "markdown", "mdx", "mdown");
			d.MarkdownLike = true;
			d.Literals = new[] { "true", "false" };
			return d;
		}

		private static SyntaxDefinition BuildIni()
		{
			SyntaxDefinition d = Base("INI", "ini", "cfg", "conf", "properties", "toml", "editorconfig", "env", "gitignore", "gitattributes", "npmrc");
			d.LineComments = new[] { ";", "#" };
			d.PropertyBeforeColon = true;
			d.Literals = new[] { "true", "false", "on", "off", "yes", "no" };
			return d;
		}

		private static SyntaxDefinition BuildKotlin()
		{
			SyntaxDefinition d = Base("Kotlin", "kt", "kts");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "when", "for", "while", "do", "break", "continue", "return", "try", "catch", "finally", "throw", "in", "is", "as", "object", "companion", "init", "constructor" };
			d.Keywords = new[] { "package", "import", "class", "interface", "fun", "val", "var", "open", "final", "abstract", "override", "private", "protected", "public", "internal", "suspend", "inline", "noinline", "crossinline", "reified", "lateinit", "data", "sealed", "enum", "annotation", "typealias", "where", "by", "out" };
			d.Types = new[] { "Int", "Long", "Short", "Byte", "Float", "Double", "Boolean", "Char", "String", "Any", "Unit", "Nothing", "Array", "List", "Map", "Set", "Pair", "Triple" };
			d.Literals = new[] { "true", "false", "null", "this", "super" };
			d.BacktickString = true;
			return d;
		}

		private static SyntaxDefinition BuildSwift()
		{
			SyntaxDefinition d = Base("Swift", "swift");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "guard", "for", "while", "repeat", "switch", "case", "default", "break", "continue", "return", "throw", "try", "catch", "defer", "in", "where", "do", "fallthrough" };
			d.Keywords = new[] { "func", "class", "struct", "enum", "protocol", "extension", "import", "let", "var", "init", "deinit", "subscript", "typealias", "static", "final", "override", "public", "private", "fileprivate", "internal", "open", "mutating", "nonmutating", "lazy", "weak", "unowned", "indirect", "convenience", "required", "some", "any", "actor", "async", "await" };
			d.Types = new[] { "Int", "Int8", "Int16", "Int32", "Int64", "UInt", "Float", "Double", "Bool", "String", "Character", "Void", "Any", "AnyObject", "Never", "Array", "Dictionary", "Set", "Optional" };
			d.Literals = new[] { "true", "false", "nil", "self", "super" };
			d.TripleQuotes = true;
			return d;
		}

		private static SyntaxDefinition BuildRuby()
		{
			SyntaxDefinition d = Base("Ruby", "rb", "rake", "gemspec", "ru");
			d.LineComments = new[] { "#" };
			d.BlockOpen = "=begin";
			d.BlockClose = "=end";
			d.ControlKeywords = new[] { "if", "elsif", "else", "unless", "case", "when", "while", "until", "for", "in", "do", "end", "begin", "rescue", "ensure", "retry", "return", "break", "next", "redo", "yield", "then" };
			d.Keywords = new[] { "def", "class", "module", "require", "require_relative", "include", "extend", "attr_accessor", "attr_reader", "attr_writer", "self", "super", "lambda", "proc", "alias", "undef", "raise", "private", "protected", "public", "alias_method" };
			d.Literals = new[] { "true", "false", "nil", "__FILE__", "__LINE__" };
			return d;
		}

		private static SyntaxDefinition BuildDart()
		{
			SyntaxDefinition d = Base("Dart", "dart");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue", "return", "try", "catch", "finally", "throw", "in", "is", "as", "yield", "await", "async" };
			d.Keywords = new[] { "class", "abstract", "extends", "implements", "with", "mixin", "enum", "typedef", "import", "export", "library", "part", "final", "const", "var", "late", "static", "factory", "get", "set", "operator", "required", "covariant", "external" };
			d.Types = new[] { "int", "double", "num", "bool", "String", "List", "Map", "Set", "Future", "Stream", "Object", "dynamic", "void", "Never" };
			d.Literals = new[] { "true", "false", "null", "this", "super" };
			d.TripleQuotes = true;
			return d;
		}

		private static SyntaxDefinition BuildLua()
		{
			SyntaxDefinition d = Base("Lua", "lua");
			d.LineComments = new[] { "--" };
			d.BlockOpen = "--[[";
			d.BlockClose = "]]";
			d.ControlKeywords = new[] { "if", "then", "elseif", "else", "end", "for", "in", "while", "repeat", "until", "do", "break", "return", "goto" };
			d.Keywords = new[] { "function", "local", "and", "or", "not", "nil", "true", "false", "self", "require", "pairs", "ipairs", "print", "type", "setmetatable", "getmetatable", "pcall", "xpcall", "assert", "error" };
			d.Literals = new[] { "true", "false", "nil" };
			return d;
		}

		private static SyntaxDefinition BuildScala()
		{
			SyntaxDefinition d = Base("Scala", "scala", "sc");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "while", "do", "match", "case", "try", "catch", "finally", "throw", "return", "yield", "break", "continue", "new" };
			d.Keywords = new[] { "def", "val", "var", "class", "object", "trait", "extends", "with", "implicit", "override", "final", "sealed", "abstract", "lazy", "private", "protected", "public", "package", "import", "type", "given", "using", "enum", "export", "derives", "inline", "transparent", "opaque", "extension" };
			d.Types = new[] { "Int", "Long", "Double", "Float", "Short", "Byte", "Char", "Boolean", "String", "Unit", "Any", "AnyRef", "Nothing", "List", "Seq", "Map", "Set", "Option", "Either", "Future" };
			d.Literals = new[] { "true", "false", "null", "this", "super" };
			return d;
		}

		private static SyntaxDefinition BuildR()
		{
			SyntaxDefinition d = Base("R", "r", "rmd");
			d.LineComments = new[] { "#" };
			d.ControlKeywords = new[] { "if", "else", "for", "while", "repeat", "break", "next", "return", "function" };
			d.Keywords = new[] { "library", "require", "source", "attach", "detach", "subset", "apply", "lapply", "sapply", "tapply", "mapply", "setwd", "print", "cat", "paste", "c", "list", "data.frame", "matrix", "factor" };
			d.Literals = new[] { "TRUE", "FALSE", "NULL", "NA", "NaN", "Inf", "T", "F" };
			return d;
		}

		private static SyntaxDefinition BuildPerl()
		{
			SyntaxDefinition d = Base("Perl", "pl", "pm", "t", "pod");
			d.LineComments = new[] { "#" };
			d.ControlKeywords = new[] { "if", "elsif", "else", "unless", "while", "until", "for", "foreach", "do", "last", "next", "redo", "return", "sub", "goto", "given", "when", "default" };
			d.Keywords = new[] { "my", "our", "local", "state", "use", "require", "package", "strict", "warnings", "print", "printf", "say", "chomp", "chop", "split", "join", "push", "pop", "shift", "unshift", "keys", "values", "each", "exists", "delete", "die", "warn", "eval" };
			d.Literals = new[] { "undef", "true", "false" };
			return d;
		}

		private static SyntaxDefinition BuildGroovy()
		{
			SyntaxDefinition d = Base("Groovy", "groovy", "gradle", "gvy");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "while", "switch", "case", "default", "break", "continue", "return", "try", "catch", "finally", "throw", "in", "instanceof", "new" };
			d.Keywords = new[] { "class", "interface", "trait", "enum", "def", "import", "package", "extends", "implements", "static", "final", "abstract", "public", "private", "protected", "synchronized", "volatile", "transient", "native", "assert", "as", "with", "closure" };
			d.Types = new[] { "int", "long", "double", "float", "boolean", "char", "byte", "short", "String", "Object", "List", "Map", "Set", "Closure", "void", "def" };
			d.Literals = new[] { "true", "false", "null", "this", "super" };
			return d;
		}

		private static SyntaxDefinition BuildVisualBasic()
		{
			SyntaxDefinition d = Base("Visual Basic", "vb", "vbs", "bas");
			d.LineComments = new[] { "'", "REM" };
			d.ControlKeywords = new[] { "If", "Then", "Else", "ElseIf", "End", "Select", "Case", "For", "Each", "Next", "While", "Wend", "Do", "Loop", "Until", "Exit", "GoTo", "Return", "Try", "Catch", "Finally", "Throw", "With" };
			d.Keywords = new[] { "Dim", "As", "New", "Class", "Module", "Namespace", "Imports", "Public", "Private", "Protected", "Friend", "Static", "Shared", "Sub", "Function", "Property", "Get", "Set", "ReadOnly", "WriteOnly", "Const", "Enum", "Structure", "Interface", "Inherits", "Implements", "Overrides", "Overloads", "MustInherit", "NotInheritable", "ByVal", "ByRef", "Optional", "ParamArray", "Handles", "AddHandler", "RemoveHandler", "Event", "Delegate", "Using", "SyncLock", "Me", "MyBase", "MyClass" };
			d.Types = new[] { "Integer", "Long", "Short", "Byte", "Single", "Double", "Decimal", "Boolean", "Char", "String", "Date", "Object", "Variant" };
			d.Literals = new[] { "True", "False", "Nothing", "Null" };
			d.CaseInsensitiveKeywords = true;
			return d;
		}

		private static SyntaxDefinition BuildFSharp()
		{
			SyntaxDefinition d = Base("F#", "fs", "fsi", "fsx", "fsscript");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "(*";
			d.BlockClose = "*)";
			d.ControlKeywords = new[] { "if", "then", "else", "elif", "match", "with", "for", "in", "while", "do", "done", "try", "catch", "finally", "failwith", "raise", "return", "yield", "begin", "end" };
			d.Keywords = new[] { "let", "mutable", "rec", "and", "module", "namespace", "open", "type", "member", "static", "abstract", "override", "default", "interface", "class", "struct", "record", "union", "exception", "use", "fun", "function", "inline", "private", "public", "internal", "async", "task", "seq", "lazy", "new", "base", "upcast", "downcast" };
			d.Types = new[] { "int", "int64", "float", "double", "decimal", "bool", "char", "string", "byte", "unit", "list", "array", "seq", "option", "Result", "obj" };
			d.Literals = new[] { "true", "false", "null", "None", "Some", "Ok", "Error" };
			return d;
		}

		private static SyntaxDefinition BuildDockerfile()
		{
			SyntaxDefinition d = Base("Dockerfile", "dockerfile");
			d.FileNames = new[] { "Dockerfile", "Containerfile", "Dockerfile.dev", "Dockerfile.prod" };
			d.LineComments = new[] { "#" };
			d.ControlKeywords = new[] { "FROM", "RUN", "CMD", "ENTRYPOINT", "COPY", "ADD", "ENV", "ARG", "WORKDIR", "EXPOSE", "VOLUME", "USER", "LABEL", "SHELL", "HEALTHCHECK", "ONBUILD", "STOPSIGNAL", "AS" };
			d.CaseInsensitiveKeywords = true;
			return d;
		}

		private static SyntaxDefinition BuildMakefile()
		{
			SyntaxDefinition d = Base("Makefile", "mk", "mak");
			d.FileNames = new[] { "Makefile", "makefile", "GNUmakefile" };
			d.LineComments = new[] { "#" };
			d.ControlKeywords = new[] { "ifeq", "ifneq", "ifdef", "ifndef", "else", "endif", "include", "sinclude", "define", "endef", "export", "unexport", "override", "vpath" };
			d.Literals = new[] { "true", "false" };
			return d;
		}

		private static SyntaxDefinition BuildCMake()
		{
			SyntaxDefinition d = Base("CMake", "cmake");
			d.FileNames = new[] { "CMakeLists.txt" };
			d.LineComments = new[] { "#" };
			d.BlockOpen = "#[[";
			d.BlockClose = "]]";
			d.ControlKeywords = new[] { "if", "elseif", "else", "endif", "foreach", "endforeach", "while", "endwhile", "function", "endfunction", "macro", "endmacro", "return", "break", "continue" };
			d.Keywords = new[] { "cmake_minimum_required", "project", "add_executable", "add_library", "add_subdirectory", "target_link_libraries", "target_include_directories", "target_compile_options", "target_compile_definitions", "set", "unset", "message", "option", "include", "find_package", "install", "configure_file" };
			d.Literals = new[] { "ON", "OFF", "TRUE", "FALSE", "YES", "NO" };
			d.CaseInsensitiveKeywords = true;
			return d;
		}

		private static SyntaxDefinition BuildProtobuf()
		{
			SyntaxDefinition d = Base("Protocol Buffers", "proto");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "message", "service", "rpc", "returns", "enum", "oneof", "extend", "extensions", "reserved", "optional", "required", "repeated", "group" };
			d.Keywords = new[] { "syntax", "import", "package", "option", "map", "stream" };
			d.Types = new[] { "double", "float", "int32", "int64", "uint32", "uint64", "sint32", "sint64", "fixed32", "fixed64", "sfixed32", "sfixed64", "bool", "string", "bytes" };
			d.Literals = new[] { "true", "false" };
			return d;
		}

		private static SyntaxDefinition BuildGraphQL()
		{
			SyntaxDefinition d = Base("GraphQL", "graphql", "gql");
			d.LineComments = new[] { "#" };
			d.ControlKeywords = new[] { "query", "mutation", "subscription", "fragment", "on", "type", "interface", "union", "enum", "input", "scalar", "schema", "extend", "implements", "directive" };
			d.Keywords = new[] { "true", "false", "null" };
			d.Literals = new[] { "true", "false", "null" };
			d.PropertyBeforeColon = true;
			return d;
		}

		private static SyntaxDefinition BuildVue()
		{
			SyntaxDefinition d = Base("Vue", "vue");
			d.XmlLike = true;
			d.Attributes = true;
			d.ControlKeywords = new[] { "if", "else", "for", "v-if", "v-else", "v-for", "v-show", "v-model", "v-bind", "v-on" };
			d.Literals = new[] { "true", "false", "null" };
			return d;
		}

		private static SyntaxDefinition BuildSolidity()
		{
			SyntaxDefinition d = Base("Solidity", "sol");
			d.LineComments = new[] { "//" };
			d.BlockOpen = "/*";
			d.BlockClose = "*/";
			d.ControlKeywords = new[] { "if", "else", "for", "while", "do", "break", "continue", "return", "try", "catch", "throw", "emit", "new", "delete" };
			d.Keywords = new[] { "pragma", "contract", "interface", "library", "function", "modifier", "event", "struct", "enum", "mapping", "constructor", "fallback", "receive", "public", "private", "internal", "external", "pure", "view", "payable", "virtual", "override", "immutable", "constant", "memory", "storage", "calldata", "indexed", "anonymous", "import", "using", "is", "abstract" };
			d.Types = new[] { "address", "bool", "string", "bytes", "uint", "int", "uint8", "uint16", "uint32", "uint64", "uint128", "uint256", "int8", "int16", "int32", "int64", "int128", "int256", "byte", "fixed", "ufixed" };
			d.Literals = new[] { "true", "false", "wei", "gwei", "ether", "seconds", "minutes", "hours", "days", "weeks" };
			return d;
		}

		private static SyntaxDefinition BuildAssembly()
		{
			SyntaxDefinition d = Base("Assembly", "asm", "s", "nasm");
			d.LineComments = new[] { ";", "#" };
			d.ControlKeywords = new[] { "mov", "lea", "add", "sub", "mul", "div", "and", "or", "xor", "not", "shl", "shr", "cmp", "test", "jmp", "je", "jne", "jz", "jnz", "jg", "jl", "call", "ret", "push", "pop", "inc", "dec", "int", "syscall", "nop" };
			d.Keywords = new[] { "section", "global", "extern", "segment", "ends", "db", "dw", "dd", "dq", "equ", "org", "align", "macro", "endm" };
			d.Types = new[] { "eax", "ebx", "ecx", "edx", "esi", "edi", "esp", "ebp", "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rsp", "rbp", "ax", "bx", "cx", "dx", "al", "ah", "bl", "bh" };
			return d;
		}

		private static SyntaxDefinition BuildHaskell()
		{
			SyntaxDefinition d = Base("Haskell", "hs", "lhs");
			d.LineComments = new[] { "--" };
			d.BlockOpen = "{-";
			d.BlockClose = "-}";
			d.ControlKeywords = new[] { "if", "then", "else", "case", "of", "let", "in", "do", "where", "data", "type", "newtype", "class", "instance", "deriving", "module", "import", "qualified", "as", "hiding", "forall", "foreign" };
			d.Keywords = new[] { "infix", "infixl", "infixr", "default", "instance", "family", "pattern", "mdo", "rec" };
			d.Types = new[] { "Int", "Integer", "Float", "Double", "Bool", "Char", "String", "Maybe", "Either", "IO", "Ordering", "Word" };
			d.Literals = new[] { "True", "False", "Nothing", "Just", "Nothing" };
			return d;
		}

		private static SyntaxDefinition BuildClojure()
		{
			SyntaxDefinition d = Base("Clojure", "clj", "cljs", "cljc", "edn");
			d.LineComments = new[] { ";" };
			d.ControlKeywords = new[] { "if", "if-not", "if-let", "when", "when-not", "cond", "case", "let", "loop", "recur", "do", "try", "catch", "finally", "throw", "and", "or", "not" };
			d.Keywords = new[] { "def", "defn", "defn-", "defmacro", "defmulti", "defmethod", "defprotocol", "defrecord", "deftype", "ns", "require", "use", "import", "fn", "quote", "var", "atom", "ref", "agent", "doseq", "for", "map", "reduce", "filter" };
			d.Literals = new[] { "true", "false", "nil" };
			return d;
		}
	}
}
