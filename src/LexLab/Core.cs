using LexLab.Core;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;
using Tesseract;

namespace LexLab.Core;


/// <summary>
/// 词法分析器的“可外部编辑”配置。
///
/// 设计要点：所有单词符号与种别码的对应关系（关键字、运算符/界符、以及标识符/常量
/// 的特殊种别码）全部存放于 exe 同目录下的 lexer.config.json 中。
/// 需要新增/修改一个种别码时，只改配置、不必改代码、不必重新编译：
///   · 新增关键字      —— 在 Keywords 中加一项即可（词法分析时按最长标识符整体查表）；
///   · 新增运算符/界符 —— 在 Operators 中加一项即可（扫描器按最长匹配自动识别）；
///   · 新增字符集/注释/字符串/数字规则 —— 修改 Chars / Comments / Strings / Numbers。
/// </summary>
public sealed class LexerConfig
{
    /// <summary>关键字是否区分大小写（C 语言为 true：if 是关键字，IF 是标识符）。</summary>
    public bool CaseSensitiveKeywords { get; set; } = true;

    /// <summary>标识符、常量、字符串等特殊单词的种别码。</summary>
    public SpecialCodes Special { get; set; } = new();

    /// <summary>关键字表：单词 → 种别码。</summary>
    public Dictionary<string, int> Keywords { get; set; } = new();

    /// <summary>运算符/界符表：符号 → 种别码。扫描时按最长匹配自动处理 &gt;=、==、++ 等多字符符号。</summary>
    public Dictionary<string, int> Operators { get; set; } = new();

    /// <summary>标识符构成字符集。</summary>
    public CharRules Chars { get; set; } = new();

    /// <summary>注释规则。</summary>
    public CommentRules Comments { get; set; } = new();

    /// <summary>字符串常量规则。</summary>
    public StringRules Strings { get; set; } = new();

    /// <summary>数值常量规则（对应指导书对整型/双精度常量的规定）。</summary>
    public NumberRules Numbers { get; set; } = new();

    // ---------------- 派生数据（读取配置后计算，方便扫描器快速使用） ----------------

    [JsonIgnore] public HashSet<char> IdStart { get; private set; } = new();
    [JsonIgnore] public HashSet<char> IdPart { get; private set; } = new();
    [JsonIgnore] public HashSet<char> WhiteSpace { get; private set; } = new();
    [JsonIgnore] public Dictionary<string, int> KeywordMap { get; private set; } = new(StringComparer.Ordinal);
    [JsonIgnore] public Dictionary<string, int> KeywordMapIgnoreCase { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>按首字符分组、长度由长到短排序的运算符表，用于最长匹配。</summary>
    [JsonIgnore] public Dictionary<char, List<KeyValuePair<string, int>>> OperatorIndex { get; private set; } = new();

    /// <summary>运算符的最大长度，扫描时据此确定试探上限。</summary>
    [JsonIgnore] public int MaxOperatorLength { get; private set; }

    /// <summary>在读取配置后调用，预处理出扫描期需要的派生结构。</summary>
    public void Build()
    {
        IdStart = CharSetSpec.Parse(Chars.IdentifierStart);
        IdPart = CharSetSpec.Parse(Chars.IdentifierPart);
        WhiteSpace = CharSetSpec.Parse(Chars.Whitespace);

        KeywordMap = new Dictionary<string, int>(StringComparer.Ordinal);
        KeywordMapIgnoreCase = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in Keywords)
        {
            KeywordMap[kv.Key] = kv.Value;
            if (!KeywordMapIgnoreCase.ContainsKey(kv.Key)) KeywordMapIgnoreCase[kv.Key] = kv.Value;
        }

        OperatorIndex = new Dictionary<char, List<KeyValuePair<string, int>>>();
        MaxOperatorLength = 0;
        foreach (var kv in Operators)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            if (!OperatorIndex.TryGetValue(kv.Key[0], out var list))
                OperatorIndex[kv.Key[0]] = list = new List<KeyValuePair<string, int>>();
            list.Add(kv);
            if (kv.Key.Length > MaxOperatorLength) MaxOperatorLength = kv.Key.Length;
        }
        // 每个首字符分组内按长度降序 → 保证“最长匹配”优先
        foreach (var list in OperatorIndex.Values)
            list.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
    }

    /// <summary>查询某个标识符字符串对应的关键字种别码；不是关键字时返回 null。</summary>
    public int? LookupKeyword(string word)
    {
        var map = CaseSensitiveKeywords ? KeywordMap : KeywordMapIgnoreCase;
        return map.TryGetValue(word, out var code) ? code : null;
    }

    // ---------------- 读写 ----------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip, // 允许配置里写 // 注释，便于说明与手工编辑
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,             // 配置里关键字/运算符等键名大小写不敏感
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>从指定文件读取配置；文件不存在或解析失败时抛出异常由调用方处理。</summary>
    public static LexerConfig Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    /// <summary>从 JSON 文本解析配置（允许 // 注释与末尾逗号）。</summary>
    public static LexerConfig Parse(string json)
    {
        var cfg = JsonSerializer.Deserialize<LexerConfig>(json, JsonOptions)
                  ?? throw new InvalidDataException("配置内容为空或格式不正确。");
        cfg.Build();
        return cfg;
    }

    /// <summary>内置默认配置解析结果（配置文件损坏时作为兜底）。</summary>
    public static LexerConfig CreateDefault() => Parse(DefaultTemplate);

    /// <summary>把当前配置写回文件（保留中文不转义）。</summary>
    public void Save(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions), new UTF8Encoding(true));
    }

    /// <summary>可执行文件所在目录（单文件发布时为 exe 实际所在目录）。</summary>
    public static string ExeDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath ?? AppContext.BaseDirectory) ?? AppContext.BaseDirectory;

    /// <summary>exe 同目录下的配置文件名。</summary>
    public const string FileName = "lexer.config.json";

    /// <summary>exe 同目录下的完整配置路径。</summary>
    public static string ExeSidePath => Path.Combine(ExeDirectory, FileName);

    /// <summary>若 exe 同目录下没有配置文件，则写出内置默认配置，并返回其路径。</summary>
    public static string EnsureExists()
    {
        string path = ExeSidePath;
        if (!File.Exists(path)) File.WriteAllText(path, DefaultTemplate, new UTF8Encoding(true));
        return path;
    }

    /// <summary>
    /// 内置默认配置模板（含注释）。种别码 1~50 与实验指导书表格完全一致，
    /// 51 起的“扩展区”为本实验自行扩充，用来验证“种别码可外部扩展”这一设计。
    /// </summary>
    public const string DefaultTemplate = """
{
  // ================= LexLab 词法分析配置 =================
  // 说明：本文件与 LexLab.exe 放在同一目录，可随时用记事本编辑；
  //       修改后重新点击界面上的“重新载入配置”即可生效，无需重新编译程序。
  //       种别码 1~50 与实验指导书一致，51 之后为实验自行扩展的种别码。

  // 关键字是否区分大小写（C 语言为 true：if 是关键字，IF 是标识符）
  "caseSensitiveKeywords": true,

  // 标识符 / 字符串的种别码（数值常量的种别码在下方 numbers 节中）
  "special": {
    "identifier": 10,     // 标识符 ID
    "string": 50,         // 字符串常量 STRING
    "stringOpen": 11,     // 字符串左引号 "
    "stringClose": 12,    // 字符串右引号 "
    "emitStringDelimiters": false  // true 时额外输出引号本身作为独立单词（11/12）
  },

  // ------------------------- 关键字表 -------------------------
  "keywords": {
    // —— 指导书表格中的关键字 ——
    "main": 1, "int": 2, "char": 3, "if": 4, "else": 5,
    "for": 6, "while": 7, "return": 8, "void": 9,

    // —— 以下为实验扩展（演示“不改代码即可新增种别码”） ——
    "double": 51, "float": 52, "long": 53, "short": 54,
    "unsigned": 55, "signed": 56, "const": 57, "static": 58,
    "struct": 59, "union": 60, "enum": 61, "typedef": 62,
    "sizeof": 63, "switch": 64, "case": 65, "default": 66,
    "break": 67, "continue": 68, "do": 69, "goto": 70,
    "extern": 71
  },

  // --------------------- 运算符 / 界符表 ---------------------
  // 扫描器按“最长匹配”自动识别，因此 >= 不会被拆成 > 和 =，
  // 多字符符号只需在此登记即可，不需要改动任何代码。
  "operators": {
    // —— 指导书表格中的运算符/界符 ——
    "=": 21,  "+": 22,  "-": 23,  "*": 24,  "/": 25,
    "(": 26,  ")": 27,  "[": 28,  "]": 29,  "{": 30,
    "}": 31,  ",": 32,  ":": 33,  ";": 34,  ">": 35,
    "<": 36,  ">=": 37, "<=": 38, "==": 39, "!=": 40,

    // —— 以下为实验扩展 ——
    "++": 81, "--": 82, "+=": 83, "-=": 84, "*=": 85,
    "/=": 86, "%=": 87, "<<": 88, ">>": 89, "<<=": 90,
    ">>=": 91, "&=": 92, "|=": 93, "^=": 94, "&&": 95,
    "||": 96, "!": 97,  "~": 98,  "^": 99,  "%": 100,
    "&": 101, "|": 102, "->": 103, ".": 104, "?": 105,
    "#": 106
  },

  // ------------------------- 标识符字符集 -------------------------
  "chars": {
    "identifierStart": "A-Za-z_",       // 标识符首字符：字母或下划线
    "identifierPart": "A-Za-z0-9_",     // 标识符后续字符：字母、数字或下划线
    "whitespace": " \\t\\r\\n\\f\\v"    // 空白符（空格、制表、回车、换行、换页、纵向制表）
  },

  // --------------------------- 注释规则 ---------------------------
  "comments": {
    "linePrefix": "//",     // 单行注释开始，直到行末
    "blockStart": "/*",     // 多行注释开始
    "blockEnd": "*/",       // 多行注释结束
    "allowNested": false    // 指导书规定多行注释不允许嵌套
  },

  // ------------------------ 字符串常量规则 ------------------------
  "strings": {
    "delimiter": "\"",      // 字符串由双引号围起
    "allowEscapes": false,  // 指导书未定义转义，字符串中反斜杠按普通字符处理
    "allowMultiLine": false // 字符串不能跨行
  },

  // -------------------------- 数值常量规则 --------------------------
  "numbers": {
    "hexPrefixes": ["0x", "0X"],              // 十六进制前缀
    "hexDigits": "0-9A-Fa-f",                 // 十六进制数字
    "decimalDigits": "0-9",                   // 十进制数字
    "exponentMarkers": "eE",                  // 指数标记
    "requireDecimalPointForExponent": true,   // 指导书：12.2E+2 合法，12E+2 非法
    "requireExponentSign": true,              // 指导书：指数符号是必须的（12.2E2 非法）
    "integerCode": 20,                        // 整型常量种别码
    "realCode": 20                            // 双精度常量种别码（与整型共用 NUM=20）
  }
}
""";
}

/// <summary>标识符 / 字符串等特殊单词的种别码。</summary>
public sealed class SpecialCodes
{
    public int Identifier { get; set; } = 10;
    public int String { get; set; } = 50;
    public int StringOpen { get; set; } = 11;
    public int StringClose { get; set; } = 12;
    public bool EmitStringDelimiters { get; set; }
}

/// <summary>标识符构成字符集（用区间写法描述，如 A-Za-z_）。</summary>
public sealed class CharRules
{
    public string IdentifierStart { get; set; } = "A-Za-z_";
    public string IdentifierPart { get; set; } = "A-Za-z0-9_";
    public string Whitespace { get; set; } = " \t\r\n\f\v";
}

/// <summary>注释规则。</summary>
public sealed class CommentRules
{
    public string LinePrefix { get; set; } = "//";
    public string BlockStart { get; set; } = "/*";
    public string BlockEnd { get; set; } = "*/";
    public bool AllowNested { get; set; }
}

/// <summary>字符串常量规则。</summary>
public sealed class StringRules
{
    public string Delimiter { get; set; } = "\"";
    public bool AllowEscapes { get; set; }
    public bool AllowMultiLine { get; set; }
}

/// <summary>数值常量规则。</summary>
public sealed class NumberRules
{
    public List<string> HexPrefixes { get; set; } = new() { "0x", "0X" };
    public string HexDigits { get; set; } = "0-9A-Fa-f";
    public string DecimalDigits { get; set; } = "0-9";
    public string ExponentMarkers { get; set; } = "eE";
    public bool RequireDecimalPointForExponent { get; set; } = true;
    public bool RequireExponentSign { get; set; } = true;
    public int IntegerCode { get; set; } = 20;
    public int RealCode { get; set; } = 20;
}

/// <summary>
/// 字符集描述解析器。支持区间写法与转义，例如：
///   "A-Za-z_"        → 52 个大写字母 + 26 个小写字母 + 下划线
///   " \\t\\r\\n"     → 空格、制表、回车、换行
///   "0-9A-Fa-f"      → 十六进制数字
/// </summary>
public static class CharSetSpec
{
    public static HashSet<char> Parse(string spec)
    {
        var set = new HashSet<char>();
        if (string.IsNullOrEmpty(spec)) return set;

        var items = new List<char>();       // 先把转义解析成一个个“原始字符”
        for (int i = 0; i < spec.Length; i++)
        {
            char c = spec[i];
            if (c == '\\' && i + 1 < spec.Length)
            {
                i++;
                items.Add(spec[i] switch
                {
                    't' => '\t', 'n' => '\n', 'r' => '\r',
                    'f' => '\f', 'v' => '\v', '0' => '\0',
                    's' => ' ',
                    _ => spec[i]
                });
            }
            else items.Add(c);
        }

        // 再识别 "x-y" 形式的区间
        for (int i = 0; i < items.Count; i++)
        {
            if (i + 2 < items.Count && items[i + 1] == '-' && items[i + 2] >= items[i])
            {
                for (char c = items[i]; c <= items[i + 2]; c++) set.Add(c);
                i += 2;
            }
            else set.Add(items[i]);
        }
        return set;
    }

    /// <summary>单个字符的显示形式：把空格、制表等不可见字符显示成可读形式。</summary>
    public static string EscapeForDisplay(char c) => c switch
    {
        ' ' => "␣",
        '\t' => "\\t",
        '\n' => "\\n",
        '\r' => "\\r",
        '\f' => "\\f",
        '\v' => "\\v",
        _ => c.ToString()
    };

    /// <summary>把字符集合压缩成便于阅读的区间写法，用于界面显示（如 A-Za-z_）。</summary>
    public static string Describe(IEnumerable<char> chars)
    {
        var list = chars.Distinct().OrderBy(c => c).ToList();
        if (list.Count == 0) return "∅";
        var sb = new StringBuilder();
        int i = 0;
        while (i < list.Count)
        {
            int j = i;
            while (j + 1 < list.Count && list[j + 1] == list[j] + 1) j++;
            if (sb.Length > 0) sb.Append(' ');
            if (j - i >= 2) sb.Append(list[i]).Append('-').Append(list[j]);
            else for (int k = i; k <= j; k++)
            {
                if (k > i) sb.Append(' ');
                sb.Append(list[k]);
            }
            i = j + 1;
        }
        return sb.ToString();
    }
}


/// <summary>单词种类（用于结果分类、统计与着色显示）。</summary>
public enum TokenCategory
{
    Keyword,    // 关键字
    Identifier, // 标识符
    Integer,    // 整型常量
    Real,       // 实型（双精度）常量
    String,     // 字符串常量
    Operator,   // 运算符 / 界符
    Error       // 非法单词
}

/// <summary>
/// 一个单词符号（Token）。词法分析器的输出就是 Token 序列，
/// 每个 Token 至少包含“种别码 syn”和“单词自身值 lexeme”两项。
/// </summary>
public sealed class Token
{
    /// <summary>序号（从 1 开始，输出序列中的第几个单词）。</summary>
    public int Index { get; init; }

    /// <summary>种别码 syn。该值全部来自外部配置文件 lexer.config.json，可任意扩展。</summary>
    public int Syn { get; init; }

    /// <summary>单词自身值 token（如关键字 "if"、标识符 "x"、运算符 "&gt;="）。</summary>
    public string Lexeme { get; init; } = "";

    /// <summary>种类说明，如“关键字”“标识符”“整型常量”。</summary>
    public string Description { get; init; } = "";

    /// <summary>单词种类。</summary>
    public TokenCategory Category { get; init; }

    /// <summary>单词所在行号（从 1 开始）。</summary>
    public int Line { get; init; }

    /// <summary>单词所在列号（从 1 开始）。</summary>
    public int Column { get; init; }

    /// <summary>
    /// 数值常量的值文本：整型输出十进制值与十六进制原值，实型输出双精度值。
    /// 非数值常量为空字符串。
    /// </summary>
    public string ValueText { get; init; } = "";

    /// <summary>数值常量的解析结果（供后续实验使用）。非数值常量时为 null。</summary>
    public object? Value { get; init; }

    /// <summary>按指导书要求输出二元组形式，如 (4,if)。</summary>
    public override string ToString() => $"({Syn},{Lexeme})";
}

/// <summary>
/// 词法错误。包含有意义的错误信息以及错误发生的行号、列号。
/// </summary>
public sealed class LexError
{
    public int Line { get; init; }
    public int Column { get; init; }
    public string Message { get; init; } = "";

    /// <summary>出错位置附近的源程序片段，便于定位。</summary>
    public string Snippet { get; init; } = "";

    public override string ToString() =>
        $"第 {Line} 行 第 {Column} 列：{Message}" + (Snippet.Length > 0 ? $"   （{Snippet}）" : "");
}

/// <summary>一次词法分析的完整结果。</summary>
public sealed class LexResult
{
    public List<Token> Tokens { get; } = new();
    public List<LexError> Errors { get; } = new();

    /// <summary>按指导书样例给出的紧凑输出，如：(4,if) (10,x) (35,&gt;) (20,9) …</summary>
    public string ToSequenceString(int perLine = 8)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Tokens.Count; i++)
        {
            sb.Append(Tokens[i]);
            if ((i + 1) % perLine == 0) sb.AppendLine();
            else sb.Append(' ');
        }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// 内置示例源程序。放在程序内部，避免教师机器上找不到示例文件；
/// 同时在 samples 目录下也提供了同名文件，便于直接用“打开源文件”载入。
/// </summary>
public static class Samples
{
    public readonly record struct Sample(string Name, string Code);

    /// <summary>指导书中的最简样例。</summary>
    public const string Simple = """
if x>9
x=2*x+1/3;
""";

    /// <summary>综合示例：涵盖关键字、标识符、10/16 进制整数、双精度常量（含指数）、字符串、注释。</summary>
    public const string Comprehensive = """
/* 综合示例：编译原理实验一 词法分析
   涵盖关键字、标识符、十进制/十六进制整型常量、
   双精度常量（含科学计数法）、字符串常量与两类注释 */
int main() {
    int count = 100;
    int mask = 0x1F;            // 十六进制整型常量
    double pi = 3.14159;
    double big = 12.2E+2;       // 科学计数法：小数点与指数符号都不可省
    double whole = 25.;         // 小数点后可以没有数字
    char* msg = "hello, lexer";
    int sum = 0;

    for (int i = 0; i < count; i = i + 1) {
        if (i >= 10 && i != 20) {
            sum = sum + i * 2 / 3;
        } else {
            sum = sum - 1;
        }
    }

    while (sum > mask) {
        sum = sum / 2;
    }
    return sum;
}
""";

    /// <summary>错误示例：集中演示各类词法错误。</summary>
    public const string WithErrors = """
int 9abc = 5;              // 错误：数字后面直接跟字母
double a = 12E+2;          // 错误：科学计数法缺少小数点
double b = 3.5E3;          // 错误：指数缺少符号
int    c = 0x;             // 错误：0x 后面没有十六进制数字
int    d = 0x1FG;          // 错误：十六进制常量中出现非法字符
char   e = @;              // 错误：非法字符 @
char*  f = "unterminated;  // 错误：字符串常量没有右双引号
/* 这里开始一个多行注释但没有结束
int    g = 1;
""";

    public static IReadOnlyList<Sample> All { get; } = new List<Sample>
    {
        new("① 指导书样例（if x>9 …）", Simple),
        new("② 综合示例（各类单词齐全）", Comprehensive),
        new("③ 错误示例（演示词法错误处理）", WithErrors)
    };
}

/// <summary>
/// 词法分析器（扫描器）。
///
/// 工作方式：把源程序看成一个字符流，主循环每次跳过空白与注释后，
/// 按“首字符”判断当前单词属于哪一类（标识符/关键字、数值常量、字符串常量、
/// 运算符或界符），再进入对应的子扫描过程把整个单词取出，最后产出一个 Token。
///
/// 本扫描器完全由 <see cref="LexerConfig"/> 驱动：
///   · 关键字、运算符与种别码均来自外部配置文件，新增种别码无需修改本文件；
///   · 运算符按“最长匹配”识别，>= 不会被拆成 > 和 =；
///   · 标识符字符集、注释标记、字符串与数值规则也都来自配置。
/// </summary>
public sealed class Lexer
{
    private readonly string _src;
    private readonly LexerConfig _cfg;
    private readonly LexResult _result = new();

    private int _pos;             // 当前字符在源串中的下标
    private int _line = 1;        // 当前行号（从 1 开始）
    private int _col = 1;         // 当前列号（从 1 开始）
    private int _index;           // 已产出的单词序号

    // 常用字符集（由配置派生，避免每次查询重复解析）
    private readonly HashSet<char> _hexDigits;
    private readonly HashSet<char> _decDigits;

    public Lexer(string source, LexerConfig config)
    {
        _src = source ?? string.Empty;
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
        _hexDigits = CharSetSpec.Parse(_cfg.Numbers.HexDigits);
        _decDigits = CharSetSpec.Parse(_cfg.Numbers.DecimalDigits);
    }

    /// <summary>对源程序做一遍词法分析，返回单词序列与错误列表。</summary>
    public LexResult Analyze()
    {
        while (!Eof)
        {
            // ---------- 1. 空白符：作为分隔符，直接跳过 ----------
            if (_cfg.WhiteSpace.Contains(Cur)) { Advance(); continue; }

            int startLine = _line, startCol = _col, startPos = _pos;

            // ---------- 2. 单行注释 //…… 直到行末 ----------
            if (_cfg.Comments.LinePrefix.Length > 0 && Match(_cfg.Comments.LinePrefix))
            {
                while (!Eof && Cur != '\n') Advance();
                continue;
            }

            // ---------- 3. 多行注释 /* …… */ ----------
            if (_cfg.Comments.BlockStart.Length > 0 && Match(_cfg.Comments.BlockStart))
            {
                ScanBlockComment(startLine, startCol, startPos);
                continue;
            }

            // ---------- 4. 标识符或关键字 ----------
            if (_cfg.IdStart.Contains(Cur)) { ScanIdentifierOrKeyword(startLine, startCol); continue; }

            // ---------- 5. 数值常量（0x… 也以数字 0 开头） ----------
            if (_decDigits.Contains(Cur)) { ScanNumber(startLine, startCol, startPos); continue; }

            // ---------- 6. 字符串常量 ----------
            if (_cfg.Strings.Delimiter.Length > 0 && Match(_cfg.Strings.Delimiter))
            {
                ScanString(startLine, startCol, startPos);
                continue;
            }

            // ---------- 7. 运算符 / 界符：最长匹配 ----------
            if (TryScanOperator(startLine, startCol)) continue;

            // ---------- 8. 其它字符一律视为词法错误 ----------
            // 例如 '@' 出现在注释与字符串之外时，不是 C 程序中的合法符号
            AddError(startLine, startCol,
                $"非法字符 '{Describe(Cur)}'：它不是 C 语言中的合法符号（注释和字符串常量中除外）。",
                startPos);
            Advance();
        }

        return _result;
    }

    // ==================================================================
    //                            词法错误处理
    // ==================================================================

    /// <summary>多行注释扫描；指导书规定注释不允许嵌套。</summary>
    private void ScanBlockComment(int line, int col, int startPos)
    {
        for (int i = 0; i < _cfg.Comments.BlockStart.Length; i++) Advance();

        int depth = 1;
        while (!Eof)
        {
            // 允许嵌套时遇到 /* 深度加一
            if (_cfg.Comments.AllowNested && Match(_cfg.Comments.BlockStart))
            {
                for (int i = 0; i < _cfg.Comments.BlockStart.Length; i++) Advance();
                depth++;
                continue;
            }
            if (Match(_cfg.Comments.BlockEnd))
            {
                for (int i = 0; i < _cfg.Comments.BlockEnd.Length; i++) Advance();
                if (--depth == 0) return;   // 注释正常结束
                continue;
            }
            Advance();
        }

        // 一直扫到文件末尾都没有找到 */
        AddError(line, col,
            $"多行注释未结束：缺少注释结束符 {_cfg.Comments.BlockEnd}。", startPos);
    }

    /// <summary>标识符 / 关键字扫描：先取出整个单词，再查关键字表。</summary>
    private void ScanIdentifierOrKeyword(int line, int col)
    {
        var sb = new StringBuilder();
        while (!Eof && _cfg.IdPart.Contains(Cur)) { sb.Append(Cur); Advance(); }

        string word = sb.ToString();
        int? keywordCode = _cfg.LookupKeyword(word);

        if (keywordCode.HasValue)
            AddToken(keywordCode.Value, word, TokenCategory.Keyword, "关键字", line, col);
        else
            // 不是关键字 → 标识符；大小写敏感，故 IF 是标识符而 if 是关键字
            AddToken(_cfg.Special.Identifier, word, TokenCategory.Identifier, "标识符 ID", line, col);
    }

    /// <summary>
    /// 数值常量扫描。严格按指导书规定实现：
    ///   整型：十进制，或 0x / 0X 开头的十六进制；
    ///   双精度：数字 + 小数点 + 可选数字，可带指数（12.2E+2 合法，12E+2 非法）；
    ///   小数点前的整数部分不能省略（.12 不是合法的双精度数）。
    /// </summary>
    private void ScanNumber(int line, int col, int startPos)
    {
        var sb = new StringBuilder();

        // -------------------- 情况一：十六进制整型 0x/0X 开头 --------------------
        foreach (string prefix in _cfg.Numbers.HexPrefixes)
        {
            if (!Match(prefix)) continue;

            for (int i = 0; i < prefix.Length; i++) { sb.Append(Cur); Advance(); }

            int hexCount = 0;
            while (!Eof && _hexDigits.Contains(Cur)) { sb.Append(Cur); Advance(); hexCount++; }

            if (hexCount == 0)
            {
                // 形如 0x 、0xG
                while (!Eof && _cfg.IdPart.Contains(Cur)) { sb.Append(Cur); Advance(); }
                AddError(line, col,
                    $"无效的十六进制常量“{sb}”：{prefix} 后面必须跟至少一位十六进制数字。", startPos);
                return;
            }
            if (!Eof && _cfg.IdPart.Contains(Cur))
            {
                // 形如 0x1FG
                while (!Eof && _cfg.IdPart.Contains(Cur)) { sb.Append(Cur); Advance(); }
                AddError(line, col,
                    $"无效的数值常量“{sb}”：十六进制常量中出现了非法字符。", startPos);
                return;
            }

            string hexText = sb.ToString();
            string digits = hexText.Substring(prefix.Length);
            string valueText = "";
            object? value = null;
            if (long.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hv))
            {
                valueText = hv.ToString(CultureInfo.InvariantCulture);
                value = hv;
            }
            else { valueText = hexText; }   // 超出 long 范围时保留原值文本

            AddToken(_cfg.Numbers.IntegerCode, hexText, TokenCategory.Integer,
                "整型常量（十六进制）", line, col, valueText, value);
            return;
        }

        // -------------------- 情况二：十进制整型 / 双精度 --------------------
        bool hasPoint = false, hasExponent = false;

        while (!Eof && _decDigits.Contains(Cur)) { sb.Append(Cur); Advance(); }

        if (!Eof && Cur == '.')
        {
            hasPoint = true;
            sb.Append(Cur); Advance();
            while (!Eof && _decDigits.Contains(Cur)) { sb.Append(Cur); Advance(); }
        }

        if (!Eof && _cfg.Numbers.ExponentMarkers.Contains(Cur))
        {
            // 指导书：科学计数法中小数点必须有（12E+2 非法，12.E+2 合法）
            if (_cfg.Numbers.RequireDecimalPointForExponent && !hasPoint)
            {
                sb.Append(Cur); Advance();
                if (!Eof && (Cur == '+' || Cur == '-')) { sb.Append(Cur); Advance(); }
                while (!Eof && _cfg.IdPart.Contains(Cur)) { sb.Append(Cur); Advance(); }
                AddError(line, col,
                    $"无效的数值常量“{sb}”：科学计数法必须带小数点，例如 12.2E+2 合法，而 12E+2 不合法。",
                    startPos);
                return;
            }

            hasExponent = true;
            sb.Append(Cur); Advance();

            bool hasSign = false;
            if (!Eof && (Cur == '+' || Cur == '-')) { hasSign = true; sb.Append(Cur); Advance(); }

            // 指导书：指数的符号是必须的（12.2E2 非法，12.2E+2 合法）
            if (_cfg.Numbers.RequireExponentSign && !hasSign)
            {
                while (!Eof && _cfg.IdPart.Contains(Cur)) { sb.Append(Cur); Advance(); }
                AddError(line, col,
                    $"无效的数值常量“{sb}”：指数部分的符号（+ 或 -）是必须的，例如 12.2E+2。", startPos);
                return;
            }

            int expDigits = 0;
            while (!Eof && _decDigits.Contains(Cur)) { sb.Append(Cur); Advance(); expDigits++; }
            if (expDigits == 0)
            {
                while (!Eof && _cfg.IdPart.Contains(Cur)) { sb.Append(Cur); Advance(); }
                AddError(line, col, $"无效的数值常量“{sb}”：指数部分缺少数字。", startPos);
                return;
            }
        }

        // 数字后紧跟字母或下划线（如 12abc）属于非法单词
        if (!Eof && _cfg.IdStart.Contains(Cur))
        {
            while (!Eof && _cfg.IdPart.Contains(Cur)) { sb.Append(Cur); Advance(); }
            AddError(line, col,
                $"无效的数值常量“{sb}”：数字后面不能紧跟字母或下划线。", startPos);
            return;
        }

        string text = sb.ToString();
        if (hasPoint || hasExponent)
        {
            double d = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv)
                ? dv : double.NaN;
            AddToken(_cfg.Numbers.RealCode, text, TokenCategory.Real, "双精度常量",
                line, col, FormatReal(d), d);
        }
        else
        {
            long iv = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lv) ? lv : 0;
            AddToken(_cfg.Numbers.IntegerCode, text, TokenCategory.Integer, "整型常量（十进制）",
                line, col, iv.ToString(CultureInfo.InvariantCulture), iv);
        }
    }

    /// <summary>
    /// 字符串常量扫描：一串被双引号围起来的字符，不能跨行，
    /// 若一行结束时仍未出现右双引号，则报告词法错误。
    /// </summary>
    private void ScanString(int line, int col, int startPos)
    {
        string delim = _cfg.Strings.Delimiter;

        for (int i = 0; i < delim.Length; i++) Advance();     // 越过左引号

        var content = new StringBuilder();
        bool closed = false;

        while (!Eof)
        {
            if (Match(delim))                                  // 找到右引号
            {
                for (int i = 0; i < delim.Length; i++) Advance();
                closed = true;
                break;
            }
            if (!_cfg.Strings.AllowMultiLine && (Cur == '\n' || Cur == '\r')) break;  // 行末仍未闭合

            if (_cfg.Strings.AllowEscapes && Cur == '\\' && _pos + 1 < _src.Length)
            {
                content.Append(Cur); Advance();
                content.Append(Cur); Advance();
                continue;
            }
            content.Append(Cur); Advance();
        }

        if (!closed)
        {
            AddError(line, col,
                "字符串常量未结束：本行结束前没有找到右双引号，字符串不能跨行书写。", startPos);
            return;                                            // 不产生 STRING 单词
        }

        if (_cfg.Special.EmitStringDelimiters)
        {
            // 若配置要求分别输出左右引号，则输出 (11,") (50,内容) (12,")
            AddToken(_cfg.Special.StringOpen, delim, TokenCategory.String, "字符串左引号", line, col);
            AddToken(_cfg.Special.String, content.ToString(), TokenCategory.String, "字符串常量 STRING",
                line, col + delim.Length, content.ToString());
            AddToken(_cfg.Special.StringClose, delim, TokenCategory.String, "字符串右引号", line, col);
        }
        else
        {
            // 默认：整个字符串字面量作为一个 STRING 单词，形如 (50,"hello")
            string literal = delim + content + delim;
            AddToken(_cfg.Special.String, literal, TokenCategory.String, "字符串常量 STRING",
                line, col, content.ToString());
        }
    }

    /// <summary>
    /// 运算符 / 界符扫描：以当前字符为索引，在配置表里按长度从大到小试探，
    /// 一旦匹配就说明找到了“最长匹配”的运算符，例如输入 &gt;= 会整体识别为种别码 37。
    /// </summary>
    private bool TryScanOperator(int line, int col)
    {
        if (!_cfg.OperatorIndex.TryGetValue(Cur, out var candidates)) return false;

        foreach (var kv in candidates)                 // candidates 已按长度降序排列
        {
            if (!Match(kv.Key)) continue;
            for (int i = 0; i < kv.Key.Length; i++) Advance();
            AddToken(kv.Value, kv.Key, TokenCategory.Operator,
                OperatorNames.TryGetValue(kv.Key, out string? name) ? name : "运算符/界符", line, col);
            return true;
        }
        return false;
    }

    // ==================================================================
    //                            基础工具
    // ==================================================================

    private bool Eof => _pos >= _src.Length;
    private char Cur => _pos < _src.Length ? _src[_pos] : '\0';

    /// <summary>前进一个字符，同时维护行号与列号。</summary>
    private void Advance()
    {
        if (_pos >= _src.Length) return;
        char c = _src[_pos];
        if (c == '\n') { _line++; _col = 1; }
        else if (c != '\r') _col++;
        _pos++;
    }

    /// <summary>判断从当前位置开始是否与给定字符串匹配。</summary>
    private bool Match(string s)
    {
        if (s.Length == 0 || _pos + s.Length > _src.Length) return false;
        for (int i = 0; i < s.Length; i++)
            if (_src[_pos + i] != s[i]) return false;
        return true;
    }

    private void AddToken(int syn, string lexeme, TokenCategory category, string description,
        int line, int col, string valueText = "", object? value = null)
    {
        _result.Tokens.Add(new Token
        {
            Index = ++_index,
            Syn = syn,
            Lexeme = lexeme,
            Category = category,
            Description = description,
            Line = line,
            Column = col,
            ValueText = valueText,
            Value = value
        });
    }

    private void AddError(int line, int col, string message, int snippetPos)
    {
        _result.Errors.Add(new LexError
        {
            Line = line,
            Column = col,
            Message = message,
            Snippet = MakeSnippet(snippetPos)
        });
    }

    /// <summary>截取错误位置附近的源程序片段，便于在界面上定位。</summary>
    private string MakeSnippet(int pos)
    {
        if (_src.Length == 0) return "";
        int start = Math.Clamp(pos, 0, _src.Length);
        int len = Math.Min(24, _src.Length - start);
        string s = _src.Substring(start, len);
        s = s.Replace("\r", " ").Replace("\n", " ");
        if (start + len < _src.Length) s += "…";
        return s.Trim();
    }

    /// <summary>把不可见字符显示成可读形式，用于错误信息。</summary>
    private static string Describe(char c) => c switch
    {
        '\t' => "\\t", '\n' => "\\n", '\r' => "\\r", '\0' => "EOF",
        _ => c.ToString()
    };

    /// <summary>双精度常量的显示值。</summary>
    private static string FormatReal(double d)
        => double.IsNaN(d) ? "" : d.ToString("G15", CultureInfo.InvariantCulture);

    /// <summary>常见运算符/界符的中文名称，仅用于界面显示（不影响词法分析结果）。</summary>
    private static readonly Dictionary<string, string> OperatorNames = new()
    {
        ["="] = "赋值号 =", ["+"] = "加号 +", ["-"] = "减号 -", ["*"] = "乘号 *", ["/"] = "除号 /",
        ["("] = "左圆括号 (", [")"] = "右圆括号 )", ["["] = "左方括号 [", ["]"] = "右方括号 ]",
        ["{"] = "左花括号 {", ["}"] = "右花括号 }", [","] = "逗号 ,", [":"] = "冒号 :",
        [";"] = "分号 ;", [">"] = "大于号 >", ["<"] = "小于号 <", [">="] = "大于等于 >=",
        ["<="] = "小于等于 <=", ["=="] = "等于号 ==", ["!="] = "不等于号 !=",
        ["++"] = "自增 ++", ["--"] = "自减 --", ["+="] = "加赋值 +=", ["-="] = "减赋值 -=",
        ["*="] = "乘赋值 *=", ["/="] = "除赋值 /=", ["%="] = "取模赋值 %=",
        ["<<"] = "左移 <<", [">>"] = "右移 >>", ["&&"] = "逻辑与 &&", ["||"] = "逻辑或 ||",
        ["!"] = "逻辑非 !", ["~"] = "按位取反 ~", ["&"] = "按位与 &", ["|"] = "按位或 |",
        ["^"] = "按位异或 ^", ["%"] = "取模 %", ["->"] = "箭头 ->", ["."] = "成员访问 .",
        ["?"] = "问号 ?", ["#"] = "预处理符号 #"
    };
}


/// <summary>正规式语法错误。</summary>
public sealed class RegexFormatException : Exception
{
    public int Position { get; }
    public RegexFormatException(string message, int position) : base(message) => Position = position;
}

// =====================================================================
//                        正规式语法树（AST）
// =====================================================================

/// <summary>正规式语法树结点基类。</summary>
public abstract class RegexNode { }

/// <summary>空串 ε（用于 a| 这类写法，以及 () 空组）。</summary>
public sealed class EpsilonNode : RegexNode { }

/// <summary>单个字符或字符类（如 a、0-9、A-Za-z_）。</summary>
public sealed class CharSetNode : RegexNode
{
    public SortedSet<char> Chars { get; } = new();
    /// <summary>原始书写形式，用于在界面上显示（如 [A-Za-z_]）。</summary>
    public string Display { get; set; } = "";
}

/// <summary>连接运算（把若干个子式顺序连接）。</summary>
public sealed class ConcatNode : RegexNode
{
    public List<RegexNode> Items { get; } = new();
}

/// <summary>或运算（|）。</summary>
public sealed class AltNode : RegexNode
{
    public List<RegexNode> Items { get; } = new();
}

/// <summary>重复运算：* 表示 (0,∞)，+ 表示 (1,∞)，? 表示 (0,1)。</summary>
public sealed class RepeatNode : RegexNode
{
    public RegexNode Child { get; init; } = new EpsilonNode();
    public int Min { get; init; }
    public int Max { get; init; } = int.MaxValue;   // int.MaxValue 表示无上界
}

// =====================================================================
//                          正规式解析器
// =====================================================================

/// <summary>
/// 正规式解析器。支持的正规式运算：
///   核心三种运算（指导书要求）：连接、或 |、闭包 *
///   实验扩展：分组 ( )、正闭包 +、可选 ?、字符类 [A-Za-z0-9_]、转义 \x
/// 优先级（由高到低）：闭包类 * + ?  &gt;  连接  &gt;  或 |
/// </summary>
public sealed class RegexParser
{
    private readonly string _s;
    private int _i;
    private readonly SortedSet<char> _alphabet = new();

    private RegexParser(string s) { _s = s; }

    /// <summary>解析正规式，返回语法树，并通过 alphabet 输出该正规式的字母表 Σ。</summary>
    public static RegexNode Parse(string pattern, out SortedSet<char> alphabet)
    {
        if (string.IsNullOrEmpty(pattern))
            throw new RegexFormatException("正规式不能为空。", 0);

        var parser = new RegexParser(pattern);
        RegexNode ast = parser.ParseAlt();
        if (parser._i < parser._s.Length)
            throw new RegexFormatException($"位置 {parser._i + 1} 处出现多余的字符 '{parser._s[parser._i]}'，可能是括号不匹配。", parser._i);

        alphabet = parser._alphabet;
        return ast;
    }

    private bool Eof => _i >= _s.Length;
    private char Cur => _i < _s.Length ? _s[_i] : '\0';

    /// <summary>Alt := Concat ('|' Concat)*</summary>
    private RegexNode ParseAlt()
    {
        var first = ParseConcat();
        if (Eof || Cur != '|') return first;

        var alt = new AltNode();
        alt.Items.Add(first);
        while (!Eof && Cur == '|')
        {
            _i++;                       // 跳过 '|'
            alt.Items.Add(ParseConcat());
        }
        return alt;
    }

    /// <summary>Concat := Repeat*</summary>
    private RegexNode ParseConcat()
    {
        var items = new List<RegexNode>();
        while (!Eof && Cur != '|' && Cur != ')')
            items.Add(ParseRepeat());

        if (items.Count == 0) return new EpsilonNode();       // 空前缀，如 (|a)、末尾 |
        if (items.Count == 1) return items[0];
        var concat = new ConcatNode();
        concat.Items.AddRange(items);
        return concat;
    }

    /// <summary>Repeat := Atom ('*' | '+' | '?')*</summary>
    private RegexNode ParseRepeat()
    {
        RegexNode atom = ParseAtom();
        while (!Eof && (Cur == '*' || Cur == '+' || Cur == '?'))
        {
            char op = Cur;
            _i++;
            atom = op switch
            {
                '*' => new RepeatNode { Child = atom, Min = 0, Max = int.MaxValue },
                '+' => new RepeatNode { Child = atom, Min = 1, Max = int.MaxValue },
                _   => new RepeatNode { Child = atom, Min = 0, Max = 1 }
            };
        }
        return atom;
    }

    /// <summary>Atom := '(' Alt ')' | '[' 字符类 ']' | '\' 转义字符 | 普通字符</summary>
    private RegexNode ParseAtom()
    {
        if (Eof)
            throw new RegexFormatException("正规式意外结束。", _i);

        char c = Cur;

        // 分组 ( )
        if (c == '(')
        {
            _i++;
            RegexNode inner = ParseAlt();
            if (Eof || Cur != ')')
                throw new RegexFormatException("括号不匹配：缺少右括号 ')'。", _i);
            _i++;
            return inner;
        }

        // 字符类 [ ... ]
        if (c == '[') return ParseCharClass();

        // 转义 \x ：把元字符当作普通字符处理
        if (c == '\\')
        {
            _i++;
            if (Eof)
                throw new RegexFormatException("转义符 '\\' 后面缺少字符。", _i - 1);
            char esc = _s[_i++];
            return MakeSingleChar(esc, "\\" + esc);
        }

        // 非法单独出现的元字符
        if (c == ')' || c == '*' || c == '+' || c == '?')
            throw new RegexFormatException($"位置 {_i + 1} 处的 '{c}' 前缺少操作数。", _i);

        // 普通字符
        _i++;
        return MakeSingleChar(c, c.ToString());
    }

    /// <summary>字符类：[A-Za-z0-9_] 这类写法，支持区间与转义。</summary>
    private RegexNode ParseCharClass()
    {
        int startPos = _i;
        _i++;                                       // 跳过 '['
        var node = new CharSetNode();

        if (Eof) throw new RegexFormatException("字符类 '[' 没有对应的 ']'。", startPos);

        // 支持形如 [abc] 与 [a-z] 的混合写法
        while (!Eof && Cur != ']')
        {
            char lo = ReadClassChar();
            if (!Eof && Cur == '-' && _i + 1 < _s.Length && _s[_i + 1] != ']')
            {
                _i++;                               // 跳过 '-'
                char hi = ReadClassChar();
                if (hi < lo)
                    throw new RegexFormatException($"字符类区间 [{lo}-{hi}] 的顺序颠倒。", _i);
                for (char t = lo; t <= hi; t++) { node.Chars.Add(t); _alphabet.Add(t); }
            }
            else
            {
                node.Chars.Add(lo);
                _alphabet.Add(lo);
            }
        }

        if (Eof)
            throw new RegexFormatException("字符类 '[' 没有对应的 ']'。", startPos);
        _i++;                                       // 跳过 ']'

        if (node.Chars.Count == 0)
            throw new RegexFormatException("字符类 '[]' 中没有任何字符。", startPos);

        node.Display = _s.Substring(startPos, _i - startPos);
        return node;
    }

    /// <summary>读取字符类中的一个字符，支持 \t \n \\ 等转义。</summary>
    private char ReadClassChar()
    {
        char c = _s[_i++];
        if (c != '\\') return c;
        if (Eof) throw new RegexFormatException("转义符 '\\' 后面缺少字符。", _i - 1);
        char esc = _s[_i++];
        return esc switch
        {
            't' => '\t', 'n' => '\n', 'r' => '\r', 'f' => '\f', 'v' => '\v', '0' => '\0',
            _ => esc
        };
    }

    private RegexNode MakeSingleChar(char c, string display)
    {
        var node = new CharSetNode { Display = display };
        node.Chars.Add(c);
        _alphabet.Add(c);
        return node;
    }
}

// =====================================================================
//                        NFA（非确定有限自动机）
// =====================================================================

/// <summary>NFA 中的一个状态。</summary>
public sealed class NfaState
{
    public int Id { get; init; }

    /// <summary>单字符转移：字符 → 后继状态编号。Thompson 构造出来的每条边只带一个字符。</summary>
    public Dictionary<char, int> Transition { get; } = new();

    /// <summary>ε 转移（空串转移）：可以不读入任何字符直接到达的后继状态。</summary>
    public List<int> Epsilon { get; } = new();
}

/// <summary>
/// ε-NFA。由正规式经 Thompson 构造法得到：每个基本正规式对应一小块结构，
/// 再用 ε 边把小块按“连接/或/闭包”组合起来。只要把首尾状态记录清楚，
/// 组合规则就是机械的——这正是 Thompson 构造法适合编码的原因。
/// </summary>
public sealed class Nfa
{
    public List<NfaState> States { get; } = new();
    public int Start { get; set; }
    public int Accept { get; set; }
    public SortedSet<char> Alphabet { get; } = new();

    public int NewState()
    {
        var s = new NfaState { Id = States.Count };
        States.Add(s);
        return s.Id;
    }

    /// <summary>求状态集合的 ε-闭包：不读入任何字符就能到达的全部状态。</summary>
    public HashSet<int> EpsilonClosure(IEnumerable<int> states)
    {
        var closure = new HashSet<int>();
        var stack = new Stack<int>();
        foreach (int s in states)
            if (closure.Add(s)) stack.Push(s);

        while (stack.Count > 0)
        {
            int cur = stack.Pop();
            foreach (int next in States[cur].Epsilon)
                if (closure.Add(next)) stack.Push(next);
        }
        return closure;
    }

    /// <summary>求从给定状态集合出发、读入字符 c 后到达的状态集合（不含 ε-闭包）。</summary>
    public HashSet<int> Move(IEnumerable<int> states, char c)
    {
        var result = new HashSet<int>();
        foreach (int s in states)
            if (States[s].Transition.TryGetValue(c, out int t)) result.Add(t);
        return result;
    }

    /// <summary>按指导书要求输出“确定化之前”的状态转换矩阵（含 ε 列）。</summary>
    public TransitionMatrix ToMatrix(bool compressColumns = true)
    {
        var matrix = new TransitionMatrix
        {
            Title = "NFA 状态转换矩阵（确定化之前）",
            Summary = $"状态数 {States.Count}　初态 q{Start}　终态 q{Accept}"
        };

        foreach (char c in Alphabet)
        {
            matrix.Columns.Add(CharSetSpec.EscapeForDisplay(c));
            matrix.ColumnSymbols.Add(c);
        }
        matrix.Columns.Add("ε");
        matrix.ColumnSymbols.Add(null);

        for (int i = 0; i < States.Count; i++)
        {
            matrix.RowNames.Add($"q{i}");
            matrix.RowNote.Add(
                (i == Start ? "初态" : "") + (i == Accept ? (i == Start ? " / 终态" : "终态") : ""));

            var row = new string[matrix.Columns.Count];
            int col = 0;
            foreach (char c in Alphabet)
            {
                row[col++] = States[i].Transition.TryGetValue(c, out int t) ? $"q{t}" : "—";
            }
            row[col] = States[i].Epsilon.Count == 0
                ? "—"
                : string.Join(", ", States[i].Epsilon.OrderBy(x => x).Select(x => $"q{x}"));
            matrix.Cells.Add(row);
        }

        if (compressColumns) matrix.CompressColumns();
        return matrix;
    }
}

// =====================================================================
//                        DFA（确定有限自动机）
// =====================================================================

/// <summary>DFA 中的一个状态。它由若干 NFA 状态构成的集合确定，这就是“子集法”的含义。</summary>
public sealed class DfaState
{
    public int Id { get; init; }

    /// <summary>该 DFA 状态对应的 NFA 状态集合（确定化时产生的信息，便于对照查看）。</summary>
    public SortedSet<int> NfaStates { get; } = new();

    /// <summary>状态转移表：字符 → 目标状态编号。</summary>
    public Dictionary<char, int> Transition { get; } = new();

    public bool IsAccept { get; set; }

    /// <summary>是否为“死状态”（补全 DFA 时人为加入的陷阱状态）。</summary>
    public bool IsDead { get; set; }
}

/// <summary>DFA。</summary>
public sealed class Dfa
{
    public List<DfaState> States { get; } = new();
    public int Start { get; set; } = 0;
    public List<char> Alphabet { get; } = new();

    /// <summary>死状态编号；-1 表示没有死状态。</summary>
    public int DeadStateId { get; set; } = -1;

    /// <summary>把状态编号转换成便于阅读的名字：A、B、…、Z、AA、AB…</summary>
    public static string StateName(int id)
    {
        var sb = new StringBuilder();
        int n = id;
        do
        {
            sb.Insert(0, (char)('A' + n % 26));
            n = n / 26 - 1;
        } while (n >= 0);
        return sb.ToString();
    }

    /// <summary>输出该 DFA 的状态转换矩阵（不含死状态，便于阅读）。</summary>
    public TransitionMatrix ToMatrix(string title, bool compressColumns = true)
    {
        var live = States.Where(s => !s.IsDead).Select(s => s.Id).ToList();
        var idToRow = new Dictionary<int, string>();
        for (int i = 0; i < live.Count; i++) idToRow[live[i]] = StateName(i);

        var matrix = new TransitionMatrix
        {
            Title = title,
            Summary = $"状态数 {live.Count}　初态 {idToRow[Start]}　终态 " +
                      (live.Where(i => States[i].IsAccept).Select(i => idToRow[i]).DefaultIfEmpty("无").Aggregate((a, b) => a + "、" + b))
        };

        foreach (char c in Alphabet)
        {
            matrix.Columns.Add(CharSetSpec.EscapeForDisplay(c));
            matrix.ColumnSymbols.Add(c);
        }

        foreach (int sid in live)
        {
            var st = States[sid];
            matrix.RowNames.Add(idToRow[sid]);
            string note = (sid == Start ? "初态" : "") + (st.IsAccept ? (sid == Start ? " / 终态" : "终态") : "");
            matrix.RowNote.Add(note);
            if (matrix.Note2Enabled)
                matrix.RowNote2.Add("{" + string.Join(",", st.NfaStates) + "}");

            var row = new string[Alphabet.Count];
            for (int k = 0; k < Alphabet.Count; k++)
            {
                row[k] = st.Transition.TryGetValue(Alphabet[k], out int t)
                    ? (States[t].IsDead ? "—" : idToRow[t])
                    : "—";
            }
            matrix.Cells.Add(row);
        }

        if (compressColumns) matrix.CompressColumns();
        return matrix;
    }
}

// =====================================================================
//                    状态转换矩阵（供界面直接显示）
// =====================================================================

/// <summary>
/// 通用的“状态转换矩阵”数据结构。界面上把它填充成一张表格即可显示，
/// 实验报告里也直接引用这张表（不必截图）。
/// </summary>
public sealed class TransitionMatrix
{
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";

    /// <summary>列标题（字母表符号，NFA 多一列 ε）。</summary>
    public List<string> Columns { get; } = new();

    /// <summary>与 <see cref="Columns"/> 一一对应的列符号；非字符列（如 ε）为 null。</summary>
    public List<char?> ColumnSymbols { get; } = new();

    /// <summary>行标题（状态名）。</summary>
    public List<string> RowNames { get; } = new();

    /// <summary>行备注（初态 / 终态）。</summary>
    public List<string> RowNote { get; } = new();

    /// <summary>第二个行备注（DFA 用来显示对应的 NFA 状态集合）。</summary>
    public List<string> RowNote2 { get; } = new();

    /// <summary>是否输出第二行备注。</summary>
    public bool Note2Enabled { get; set; } = true;

    /// <summary>矩阵内容：[行][列]。</summary>
    public List<string[]> Cells { get; } = new();

    /// <summary>压缩后是否发生过列合并（界面据此给出提示）。</summary>
    public bool ColumnsCompressed { get; private set; }

    /// <summary>
    /// 把转移行为完全相同的字符列合并成一列。
    /// 例如标识符自动机中，A~Z、a~z、_ 三类字符在同一个状态上的转移目标都一样，
    /// 于是 63 个单字符列会被压缩成 “A-Z”“a-z”“_” 三列，表格才具有可读性。
    /// 该操作只影响显示形式，不改变自动机本身。
    /// </summary>
    public void CompressColumns()
    {
        if (Cells.Count == 0 || Columns.Count == 0) return;

        int colCount = Columns.Count;
        var order = new List<string>();                       // 保持首次出现的顺序
        var groups = new Dictionary<string, (List<int> cols, List<char> chars)>();

        for (int j = 0; j < colCount; j++)
        {
            bool isChar = ColumnSymbols.Count > j && ColumnSymbols[j] is not null;
            // 非字符列用自身下标做唯一签名，保证不会被合并
            string sig = isChar
                ? "C:" + string.Join(",", Cells.Select(r => r[j]))
                : "N:" + j;

            if (!groups.TryGetValue(sig, out var g))
            {
                g = (new List<int>(), new List<char>());
                groups[sig] = g;
                order.Add(sig);
            }
            g.cols.Add(j);
            if (isChar) g.chars.Add(ColumnSymbols[j]!.Value);
        }

        if (order.Count == colCount) return;                  // 没有任何可合并的列

        var newColumns = new List<string>();
        var newSymbols = new List<char?>();
        var newCells = new List<string[]>();

        foreach (string sig in order)
        {
            var g = groups[sig];
            if (g.chars.Count > 0)
            {
                newColumns.Add(CharSetSpec.Describe(g.chars));
                newSymbols.Add(null);                         // 合并列不再对应单个字符
            }
            else
            {
                newColumns.Add(Columns[g.cols[0]]);
                newSymbols.Add(null);
            }
        }

        foreach (string[] row in Cells)
        {
            var newRow = new string[order.Count];
            for (int k = 0; k < order.Count; k++) newRow[k] = row[groups[order[k]].cols[0]];
            newCells.Add(newRow);
        }

        Columns.Clear(); Columns.AddRange(newColumns);
        ColumnSymbols.Clear(); ColumnSymbols.AddRange(newSymbols);
        Cells.Clear(); Cells.AddRange(newCells);
        ColumnsCompressed = true;
    }
}

// =====================================================================
//                      Thompson 构造 + 子集法确定化
// =====================================================================

/// <summary>自动机构造工具类。</summary>
public static class AutomataBuilder
{
    /// <summary>
    /// 第一步：由正规式的语法树用 Thompson 构造法建立 ε-NFA。
    /// 构造规则：
    ///   基本正规式 a        ：s —a→ f
    ///   连接 MN             ：M 的终态与 N 的初态用 ε 边相连
    ///   或 M|N              ：新初态 ε→ M,N 的初态，M,N 的终态 ε→ 新终态
    ///   闭包 M*             ：新初态可跳过 M，M 的终态可回到 M 的初态
    /// </summary>
    public static Nfa BuildNfa(RegexNode ast, SortedSet<char> alphabet)
    {
        var nfa = new Nfa();
        foreach (char c in alphabet) nfa.Alphabet.Add(c);
        (int start, int accept) = BuildNode(ast, nfa);
        nfa.Start = start;
        nfa.Accept = accept;
        return nfa;
    }

    private static (int start, int end) BuildNode(RegexNode node, Nfa nfa)
    {
        switch (node)
        {
            case EpsilonNode:
            {
                int s = nfa.NewState(), e = nfa.NewState();
                nfa.States[s].Epsilon.Add(e);
                return (s, e);
            }

            case CharSetNode cs:
            {
                int s = nfa.NewState(), e = nfa.NewState();
                foreach (char c in cs.Chars) nfa.States[s].Transition[c] = e;
                return (s, e);
            }

            case ConcatNode concat:
            {
                if (concat.Items.Count == 0) return BuildNode(new EpsilonNode(), nfa);
                (int s, int e) = BuildNode(concat.Items[0], nfa);
                for (int i = 1; i < concat.Items.Count; i++)
                {
                    (int ns, int ne) = BuildNode(concat.Items[i], nfa);
                    nfa.States[e].Epsilon.Add(ns);      // 用 ε 边把前后两块连接起来
                    e = ne;
                }
                return (s, e);
            }

            case AltNode alt:
            {
                int s = nfa.NewState(), e = nfa.NewState();
                foreach (RegexNode item in alt.Items)
                {
                    (int is_, int ie) = BuildNode(item, nfa);
                    nfa.States[s].Epsilon.Add(is_);     // 新初态分叉到各分支
                    nfa.States[ie].Epsilon.Add(e);      // 各分支汇合到新终态
                }
                return (s, e);
            }

            case RepeatNode rep:
            {
                if (rep.Min == 0 && rep.Max == int.MaxValue)        // M*：闭包
                {
                    int s = nfa.NewState(), e = nfa.NewState();
                    (int cs, int ce) = BuildNode(rep.Child, nfa);
                    nfa.States[s].Epsilon.Add(cs);
                    nfa.States[s].Epsilon.Add(e);       // 可以不经过 M 直接结束
                    nfa.States[ce].Epsilon.Add(cs);     // 循环：反复经过 M
                    nfa.States[ce].Epsilon.Add(e);
                    return (s, e);
                }
                if (rep.Min == 1 && rep.Max == int.MaxValue)        // M+：正闭包
                {
                    (int cs, int ce) = BuildNode(rep.Child, nfa);
                    int e = nfa.NewState();
                    nfa.States[ce].Epsilon.Add(cs);     // 至少一次，之后可循环
                    nfa.States[ce].Epsilon.Add(e);
                    return (cs, e);
                }
                // M?：可选的
                {
                    int s = nfa.NewState(), e = nfa.NewState();
                    (int cs, int ce) = BuildNode(rep.Child, nfa);
                    nfa.States[s].Epsilon.Add(cs);
                    nfa.States[s].Epsilon.Add(e);       // 直接跳过
                    nfa.States[ce].Epsilon.Add(e);
                    return (s, e);
                }
            }
        }
        throw new RegexFormatException("无法识别的正规式结点。", 0);
    }

    /// <summary>
    /// 第二步：对 NFA 确定化（子集法 / 子集构造法），得到 DFA。
    /// 算法：DFA 的每个状态是 NFA 状态的一个集合；
    ///   初态 = ε-closure({NFA 初态})；
    ///   对集合 S 与字母表符号 a，move(S,a) 再做 ε-闭包得到新状态；
    ///   集合中含 NFA 终态则该 DFA 状态为终态。
    /// </summary>
    public static Dfa Determinize(Nfa nfa)
    {
        var dfa = new Dfa();
        foreach (char c in nfa.Alphabet) dfa.Alphabet.Add(c);
        if (dfa.Alphabet.Count == 0) throw new RegexFormatException("正规式的字母表为空，无法构造自动机。", 0);

        var startSet = nfa.EpsilonClosure(new[] { nfa.Start });
        var index = new Dictionary<string, int>();
        var queue = new Queue<HashSet<int>>();

        int AddState(HashSet<int> set)
        {
            string key = Key(set);
            if (index.TryGetValue(key, out int exist)) return exist;
            var st = new DfaState { Id = dfa.States.Count };
            foreach (int s in set) st.NfaStates.Add(s);
            st.IsAccept = set.Contains(nfa.Accept);
            dfa.States.Add(st);
            index[key] = st.Id;
            queue.Enqueue(set);
            return st.Id;
        }

        dfa.Start = AddState(startSet);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            int curId = index[Key(cur)];
            foreach (char c in dfa.Alphabet)
            {
                var moved = nfa.Move(cur, c);
                if (moved.Count == 0) continue;                 // 无转移，保持空缺
                var target = nfa.EpsilonClosure(moved);
                int targetId = AddState(target);
                dfa.States[curId].Transition[c] = targetId;
            }
        }
        return dfa;
    }

    private static string Key(IEnumerable<int> set) => string.Join(",", set.OrderBy(x => x));
}

/// <summary>
/// DFA 最简化（分割法）。
///
/// 核心思想：把 DFA 的状态先按“终态/非终态”分成两组，然后反复检查——
/// 如果同一组内的两个状态对某个输入字符会走到不同的组，就必须把它们拆开。
/// 一直重复到所有组都“稳定”为止，每一个最终的分组就是一个最简 DFA 的状态。
///
/// 为了让分割法能够正常进行，先给 DFA 补一个“死状态”：凡是没有定义的转移
/// 全部指向它。最简化完成后，如果死状态所在的等价组是一个无法到达终态的
/// 陷阱状态，就在输出时把它去掉（它不影响所接受的语言）。
/// </summary>
public static class DfaMinimizer
{
    public static Dfa Minimize(Dfa dfa)
    {
        var alphabet = dfa.Alphabet;
        int n = dfa.States.Count;

        // ---------- 1. 补全 DFA：缺少的转移全部指向新加的死状态 ----------
        var trans = new List<Dictionary<char, int>>(n);
        foreach (var s in dfa.States)
        {
            var copy = new Dictionary<char, int>();
            foreach (char c in alphabet)
                if (s.Transition.TryGetValue(c, out int t)) copy[c] = t;
            trans.Add(copy);
        }

        int deadId = -1;
        bool needDead = trans.Any(t => alphabet.Any(c => !t.ContainsKey(c)));
        if (needDead)
        {
            deadId = trans.Count;
            var deadTrans = new Dictionary<char, int>();
            foreach (char c in alphabet) deadTrans[c] = deadId;
            trans.Add(deadTrans);
            foreach (var t in trans)
                foreach (char c in alphabet)
                    if (!t.ContainsKey(c)) t[c] = deadId;
        }

        int total = trans.Count;
        var isAccept = new bool[total];
        for (int i = 0; i < n; i++) isAccept[i] = dfa.States[i].IsAccept;

        // ---------- 2. 初始分组：终态一组、非终态一组 ----------
        var group = new int[total];
        bool hasAccept = false, hasNonAccept = false;
        for (int i = 0; i < total; i++)
        {
            if (isAccept[i]) { group[i] = 1; hasAccept = true; }
            else { group[i] = 0; hasNonAccept = true; }
        }
        int groupCount = (hasAccept ? 1 : 0) + (hasNonAccept ? 1 : 0);
        if (groupCount == 0) return dfa;    // 空自动机，无需化简

        // ---------- 3. 反复细分直到稳定 ----------
        // 关键点：新分组的“签名”必须包含状态自身的当前分组，
        // 否则原本属于不同分组的状态会因为转移相同而被错误地合并到一起。
        while (true)
        {
            var signature = new string[total];
            for (int i = 0; i < total; i++)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(group[i]).Append(':');
                foreach (char c in alphabet)
                {
                    sb.Append(group[trans[i][c]]);
                    sb.Append('|');
                }
                signature[i] = sb.ToString();
            }

            var map = new Dictionary<string, int>();
            var newGroup = new int[total];
            for (int i = 0; i < total; i++)
            {
                if (!map.TryGetValue(signature[i], out int g))
                {
                    g = map.Count;
                    map[signature[i]] = g;
                }
                newGroup[i] = g;
            }

            // 分组数不再增加 → 已经稳定
            bool stable = map.Count == groupCount;
            group = newGroup;
            groupCount = map.Count;
            if (stable) break;
        }

        // ---------- 4. 按分组构造最简 DFA ----------
        var members = new List<int>[groupCount];
        for (int g = 0; g < groupCount; g++) members[g] = new List<int>();
        for (int i = 0; i < total; i++) members[group[i]].Add(i);

        // 让初态所在的分组排在第一，其余按原编号顺序排列，结果更稳定易读
        int startGroup = group[dfa.Start];
        var order = new List<int> { startGroup };
        for (int g = 0; g < groupCount; g++)
            if (g != startGroup) order.Add(g);

        var groupToNew = new Dictionary<int, int>();
        for (int k = 0; k < order.Count; k++) groupToNew[order[k]] = k;

        var result = new Dfa();
        result.Alphabet.AddRange(alphabet);
        foreach (int g in order)
        {
            var st = new DfaState { Id = result.States.Count };
            int rep = members[g][0];
            st.IsAccept = isAccept[rep];
            foreach (int m in members[g])
                foreach (int ns in dfa.States.Count > m ? dfa.States[m].NfaStates : Enumerable.Empty<int>())
                    st.NfaStates.Add(ns);

            foreach (char c in alphabet)
                st.Transition[c] = groupToNew[group[trans[rep][c]]];

            result.States.Add(st);
        }
        result.Start = 0;

        // ---------- 5. 去掉等价于死状态的陷阱状态 ----------
        if (needDead)
        {
            int deadGroup = groupToNew[group[deadId]];
            var deadState = result.States[deadGroup];
            if (deadGroup != result.Start && !deadState.IsAccept &&
                alphabet.All(c => deadState.Transition[c] == deadGroup))
            {
                deadState.IsDead = true;
                result.DeadStateId = deadGroup;
            }
        }

        return result;
    }
}

/// <summary>状态转换图中的一个状态结点。</summary>
public sealed class GraphState
{
    public string Name { get; set; } = "";
    public bool IsStart { get; set; }
    public bool IsAccept { get; set; }
}

/// <summary>状态转换图中的一条边（带标号）。</summary>
public sealed class GraphEdge
{
    public int From { get; set; }
    public int To { get; set; }
    public string Label { get; set; } = "";
}

/// <summary>画“状态转换图”所需要的最小数据模型，与具体的自动机类型无关。</summary>
public sealed class GraphModel
{
    public string Title { get; set; } = "";
    public List<GraphState> States { get; } = new();
    public List<GraphEdge> Edges { get; } = new();
}

/// <summary>把 NFA / DFA 转换成绘图用的图形模型。</summary>
public static class GraphModelBuilder
{
    /// <summary>由 NFA 生成状态转换图（ε 转移标号为 ε）。</summary>
    public static GraphModel ToGraph(this Nfa nfa)
    {
        var model = new GraphModel { Title = "NFA 状态转换图（确定化之前）" };
        for (int i = 0; i < nfa.States.Count; i++)
            model.States.Add(new GraphState { Name = $"q{i}", IsStart = i == nfa.Start, IsAccept = i == nfa.Accept });

        // 先把 (起点, 终点) 相同的边合并，并把字符整理成字符集合，便于标号压缩显示
        var buckets = new Dictionary<(int, int), List<string>>();
        var chars = new Dictionary<(int, int), SortedSet<char>>();

        void AddEdge(int from, int to, string label, char? ch)
        {
            var key = (from, to);
            if (!buckets.TryGetValue(key, out var list))
            {
                buckets[key] = list = new List<string>();
                chars[key] = new SortedSet<char>();
            }
            if (ch is null) { if (!list.Contains(label)) list.Add(label); }
            else chars[key].Add(ch.Value);
        }

        for (int i = 0; i < nfa.States.Count; i++)
        {
            foreach (var kv in nfa.States[i].Transition) AddEdge(i, kv.Value, "", kv.Key);
            foreach (int e in nfa.States[i].Epsilon) AddEdge(i, e, "ε", null);
        }

        foreach (var kv in buckets)
        {
            var parts = new List<string>();
            if (chars[kv.Key].Count > 0) parts.Add(CharSetSpec.Describe(chars[kv.Key]));
            parts.AddRange(kv.Value);
            model.Edges.Add(new GraphEdge { From = kv.Key.Item1, To = kv.Key.Item2, Label = string.Join(", ", parts) });
        }
        return model;
    }

    /// <summary>由 DFA 生成状态转换图（不包含死状态）。</summary>
    public static GraphModel ToGraph(this Dfa dfa, string title)
    {
        var live = dfa.States.Where(s => !s.IsDead).ToList();
        var idToIndex = new Dictionary<int, int>();
        for (int i = 0; i < live.Count; i++) idToIndex[live[i].Id] = i;

        var model = new GraphModel { Title = title };
        for (int i = 0; i < live.Count; i++)
        {
            model.States.Add(new GraphState
            {
                Name = Dfa.StateName(i),
                IsStart = live[i].Id == dfa.Start,
                IsAccept = live[i].IsAccept
            });
        }

        var buckets = new Dictionary<(int, int), SortedSet<char>>();
        foreach (var st in live)
        {
            foreach (var kv in st.Transition)
            {
                if (!idToIndex.TryGetValue(kv.Value, out int to)) continue;  // 指向死状态的转移不画
                var key = (idToIndex[st.Id], to);
                if (!buckets.TryGetValue(key, out var set)) buckets[key] = set = new SortedSet<char>();
                set.Add(kv.Key);
            }
        }

        foreach (var kv in buckets)
            model.Edges.Add(new GraphEdge
            {
                From = kv.Key.Item1,
                To = kv.Key.Item2,
                Label = CharSetSpec.Describe(kv.Value)
            });

        return model;
    }
}


/// <summary>OCR 的可选参数。</summary>
public sealed class OcrOptions
{
    /// <summary>自动识别深色背景（如 VS Code 暗色主题）并反色。</summary>
    public bool AutoInvert { get; set; } = true;

    /// <summary>是否做二值化增强（对清晰截图有帮助，对照片可能不利）。</summary>
    public bool Binarize { get; set; }

    /// <summary>放大倍数；0 表示按图片宽度自动决定。</summary>
    public double Scale { get; set; }

    /// <summary>识别语言。</summary>
    public string Language { get; set; } = "eng";
}

/// <summary>OCR 识别结果。</summary>
public sealed class OcrResult
{
    public string Text { get; set; } = "";
    public string Info { get; set; } = "";
    public float MeanConfidence { get; set; }
    public int LineCount { get; set; }
}

/// <summary>
/// 源代码图片识别模块（探索实验二）。
///
/// 实现思路：
///   1) 图像预处理——自动反色（适配暗色主题截图）、按宽度自动放大
///      （OCR 对小字号代码识别率低，放大后显著改善）、可选二值化；
///   2) 调用 Tesseract OCR 引擎识别，并限定“代码可能出现的字符白名单”
///      （字母、数字、下划线与各种运算符），避免把 `l` 认成 `1`、把 `；` 认成 `;` 之类的混乱；
///      同时打开 preserve_interword_spaces 以保留代码缩进；
///   3) 输出识别文本，交给词法分析器完成单词识别。
///
/// OCR 语言数据（tessdata/eng.traineddata）与 Tesseract 原生库已随程序一起
/// 打包进单个 exe，运行时自动释放，因此目标机器无需安装任何额外软件。
/// </summary>
public static class CodeOcr
{
    /// <summary>
    /// 代码字符白名单。限定为 C 语言源代码中可能出现的字符，
    /// 可以明显提高代码图片的识别准确率（不会把结果识别成中文或奇怪符号）。
    /// </summary>
    public const string CharWhitelist =
        "abcdefghijklmnopqrstuvwxyz" +
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
        "0123456789" +
        "_+-*/%=<>!&|^~(){}[];:,.?\"'#\\ \t";

    private static TesseractEngine? _engine;
    private static string? _engineLang;

    /// <summary>Tesseract 语言数据所在目录（随 exe 一起发布/释放）。</summary>
    public static string TessDataPath => Path.Combine(AppContext.BaseDirectory, "tessdata");

    /// <summary>OCR 引擎是否可用（语言数据是否就绪）。</summary>
    public static bool IsAvailable => Directory.Exists(TessDataPath) &&
        File.Exists(Path.Combine(TessDataPath, "eng.traineddata"));

    /// <summary>程序退出时释放 OCR 引擎（Tesseract 为原生库，需显式释放）。</summary>
    public static void Shutdown()
    {
        _engine?.Dispose();
        _engine = null;
        _engineLang = null;
    }

    private static TesseractEngine GetEngine(string lang)
    {
        if (_engine != null && _engineLang == lang) return _engine;

        if (!IsAvailable)
            throw new FileNotFoundException(
                $"找不到 OCR 语言数据：{TessDataPath}\\eng.traineddata。\r\n" +
                "请确认程序目录完整（语言数据已随 exe 打包，正常运行时无需手工安装）。");

        _engine?.Dispose();
        _engine = new TesseractEngine(TessDataPath, lang, EngineMode.Default);
        // 限定字符集：只识别代码里可能出现的字符
        _engine.SetVariable("tessedit_char_whitelist", CharWhitelist);
        // 保留单词之间的空格，从而保留代码缩进层次
        _engine.SetVariable("preserve_interword_spaces", "1");
        _engineLang = lang;
        return _engine;
    }

    /// <summary>识别图片文件中的源代码。</summary>
    public static OcrResult Recognize(string imagePath, OcrOptions options)
    {
        using var origin = new Bitmap(imagePath);
        return Recognize(origin, options);
    }

    /// <summary>识别位图中的源代码。</summary>
    public static OcrResult Recognize(Bitmap origin, OcrOptions options)
    {
        var notes = new List<string>();

        // ---------------- 1. 图像预处理 ----------------
        using Bitmap prepared = Preprocess(origin, options, notes);

        // ---------------- 2. OCR 识别 ----------------
        using var ms = new MemoryStream();
        prepared.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        using var pix = Pix.LoadFromMemory(ms.ToArray());

        var engine = GetEngine(options.Language);
        using var page = engine.Process(pix, PageSegMode.SingleBlock);

        // ---------------- 3. 结果整理 ----------------
        string text = NormalizeText(page.GetText());
        var result = new OcrResult
        {
            Text = text,
            MeanConfidence = page.GetMeanConfidence(),
            LineCount = text.Length == 0 ? 0 : text.Split('\n').Length,
            Info = string.Join("，", notes)
        };
        return result;
    }

    /// <summary>
    /// 图像预处理：自动反色 + 自动放大 + 可选二值化。
    /// 这一步是提高代码图片识别率的关键。
    /// </summary>
    private static Bitmap Preprocess(Bitmap origin, OcrOptions options, List<string> notes)
    {
        // (1) 自动判断深色背景并反色
        bool inverted = false;
        Bitmap src = origin;
        Bitmap? invertedBmp = null;
        if (options.AutoInvert && IsDarkBackground(origin))
        {
            invertedBmp = Invert(origin);
            src = invertedBmp;
            inverted = true;
        }

        // (2) 计算缩放倍数：OCR 对过小的文字识别率很低，按宽度自动放大
        double scale = options.Scale;
        if (scale <= 0)
        {
            scale = 1600.0 / Math.Max(1, src.Width);
            scale = Math.Clamp(scale, 1.0, 4.0);
            if (src.Width > 4000) scale = 4000.0 / src.Width;
        }
        int w = Math.Max(1, (int)Math.Round(src.Width * scale));
        int h = Math.Max(1, (int)Math.Round(src.Height * scale));

        // (3) 缩放到目标尺寸（使用高质量插值，避免笔画断裂）
        var scaled = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.White);
            g.DrawImage(src, 0, 0, w, h);
        }

        invertedBmp?.Dispose();

        // (4) 可选二值化
        if (options.Binarize)
        {
            Binarize(scaled);
            notes.Add("已二值化");
        }

        var info = new List<string>();
        if (inverted) info.Add("检测到深色背景，已自动反色");
        info.Add($"放大 {scale:0.##} 倍 → {w}×{h}");
        if (options.Binarize) info.Add("已二值化");
        notes.Clear();
        notes.AddRange(info);

        return scaled;
    }

    /// <summary>判断位图是否为深色背景（取四角与边缘像素的平均亮度）。</summary>
    private static bool IsDarkBackground(Bitmap bmp)
    {
        long sum = 0;
        int count = 0;
        int stepX = Math.Max(1, bmp.Width / 40);
        int stepY = Math.Max(1, bmp.Height / 40);

        for (int y = 0; y < bmp.Height; y += stepY)
        {
            for (int x = 0; x < bmp.Width; x += stepX)
            {
                Color p = bmp.GetPixel(x, y);
                sum += (p.R * 299 + p.G * 587 + p.B * 114) / 1000;
                count++;
            }
        }
        return count > 0 && sum / count < 120;   // 平均亮度低于 120 认为是深色背景
    }

    /// <summary>整幅图像反色。</summary>
    private static Bitmap Invert(Bitmap bmp)
    {
        var result = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(result);
        g.Clear(Color.White);

        // 先用颜色矩阵求反色，再叠加绘制，效率高于逐像素循环
        var matrix = new ColorMatrix(new[]
        {
            new float[] { -1,  0,  0, 0, 0 },
            new float[] {  0, -1,  0, 0, 0 },
            new float[] {  0,  0, -1, 0, 0 },
            new float[] {  0,  0,  0, 1, 0 },
            new float[] {  1,  1,  1, 0, 1 }
        });
        using var attrs = new ImageAttributes();
        attrs.SetColorMatrix(matrix);
        g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height),
            0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attrs);
        return result;
    }

    /// <summary>用大津法（Otsu）自动求阈值后二值化。</summary>
    private static void Binarize(Bitmap bmp)
    {
        int[] histogram = new int[256];
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

        int total = bmp.Width * bmp.Height;
        unsafe
        {
            byte* p = (byte*)data.Scan0;
            int stride = data.Stride;
            for (int y = 0; y < bmp.Height; y++)
            {
                byte* row = p + y * stride;
                for (int x = 0; x < bmp.Width; x++)
                {
                    byte b0 = row[x * 3], b1 = row[x * 3 + 1], b2 = row[x * 3 + 2];
                    int gray = (b0 * 114 + b1 * 587 + b2 * 299) / 1000;
                    histogram[gray]++;
                }
            }

            // 大津法求阈值
            double sumAll = 0;
            for (int i = 0; i < 256; i++) sumAll += (double)i * histogram[i];
            double sumB = 0, maxVar = -1;
            int wB = 0, threshold = 128;
            for (int t = 0; t < 256; t++)
            {
                wB += histogram[t];
                if (wB == 0) continue;
                int wF = total - wB;
                if (wF == 0) break;
                sumB += (double)t * histogram[t];
                double mB = sumB / wB, mF = (sumAll - sumB) / wF;
                double var = (double)wB * wF * (mB - mF) * (mB - mF);
                if (var > maxVar) { maxVar = var; threshold = t; }
            }

            for (int y = 0; y < bmp.Height; y++)
            {
                byte* row = p + y * stride;
                for (int x = 0; x < bmp.Width; x++)
                {
                    byte b0 = row[x * 3], b1 = row[x * 3 + 1], b2 = row[x * 3 + 2];
                    int gray = (b0 * 114 + b1 * 587 + b2 * 299) / 1000;
                    byte v = gray > threshold ? (byte)255 : (byte)0;
                    row[x * 3] = row[x * 3 + 1] = row[x * 3 + 2] = v;
                }
            }
        }
        bmp.UnlockBits(data);
    }

    /// <summary>整理 OCR 文本：去掉行尾空白、压缩连续空行。</summary>
    private static string NormalizeText(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(l => l.TrimEnd())
            .ToList();

        // 去掉首尾的空行
        while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);

        return string.Join("\n", lines);
    }
}
