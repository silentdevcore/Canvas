using System.Globalization;

namespace Canvas.Core.Primitives;

/// <summary>
/// Evaluates Canvas-grammar expressions (the form the migration <c>ExpressionTranslator</c> emits and the
/// frontend <c>expressionEngine.ts</c> evaluates) against a single-row data dictionary, server-side, so
/// exported documents compute conditional/arithmetic expressions instead of rendering literal templates.
/// Supports literals, identifiers/member access, the operators <c>* / % + - == != &lt; &lt;= &gt; &gt;= &amp;&amp; || !</c>
/// and the helpers <c>$iif $switch $concat $and $or $not $coalesce</c>. Returns false (caller falls back to
/// the token-substituted template) for anything it can't parse or resolve — unknown functions, malformed input.
/// </summary>
public static class CanvasExpressionEvaluator
{
    public static bool TryEvaluate(string? expression, IReadOnlyDictionary<string, object?> data, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(expression)) return false;
        try
        {
            var parser = new Parser(expression, data);
            var result = parser.ParseAndFinish();
            value = result;
            return true;
        }
        catch (EvalException)
        {
            return false;
        }
    }

    /// <summary>Format an evaluated value for placement into element text content.</summary>
    public static string FormatValue(object? value) => value switch
    {
        null => "",
        bool b => b ? "true" : "false",
        double d => d == Math.Floor(d) && !double.IsInfinity(d)
            ? ((long)d).ToString(CultureInfo.InvariantCulture)
            : d.ToString(CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    private sealed class EvalException(string message) : Exception(message);

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private readonly IReadOnlyDictionary<string, object?> _data;
        private int _i;

        public Parser(string expr, IReadOnlyDictionary<string, object?> data)
        {
            _tokens = Tokenize(expr);
            _data = data;
        }

        public object? ParseAndFinish()
        {
            var v = ParseOr();
            if (Current.Kind != TokenKind.End) throw new EvalException("trailing tokens");
            return v;
        }

        // ── precedence (low → high) ──────────────────────────────────────────────────────────────────
        private object? ParseOr()
        {
            var left = ParseAnd();
            while (Current is { Kind: TokenKind.Op, Text: "||" }) { _i++; var r = ParseAnd(); left = Truthy(left) || Truthy(r); }
            return left;
        }

        private object? ParseAnd()
        {
            var left = ParseEquality();
            while (Current is { Kind: TokenKind.Op, Text: "&&" }) { _i++; var r = ParseEquality(); left = Truthy(left) && Truthy(r); }
            return left;
        }

        private object? ParseEquality()
        {
            var left = ParseComparison();
            while (Current is { Kind: TokenKind.Op, Text: "==" or "!=" })
            {
                var op = Current.Text; _i++;
                var right = ParseComparison();
                var eq = LooseEquals(left, right);
                left = op == "==" ? eq : !eq;
            }
            return left;
        }

        private object? ParseComparison()
        {
            var left = ParseAdditive();
            while (Current is { Kind: TokenKind.Op, Text: "<" or "<=" or ">" or ">=" })
            {
                var op = Current.Text; _i++;
                var right = ParseAdditive();
                var c = Compare(left, right);
                left = op switch { "<" => c < 0, "<=" => c <= 0, ">" => c > 0, _ => c >= 0 };
            }
            return left;
        }

        private object? ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (Current is { Kind: TokenKind.Op, Text: "+" or "-" })
            {
                var op = Current.Text; _i++;
                var right = ParseMultiplicative();
                if (op == "+")
                    left = (IsNumeric(left) && IsNumeric(right))
                        ? ToNumber(left) + ToNumber(right)
                        : FormatValue(left) + FormatValue(right);   // string concat
                else
                    left = ToNumber(left) - ToNumber(right);
            }
            return left;
        }

        private object? ParseMultiplicative()
        {
            var left = ParseUnary();
            while (Current is { Kind: TokenKind.Op, Text: "*" or "/" or "%" })
            {
                var op = Current.Text; _i++;
                var right = ParseUnary();
                var (a, b) = (ToNumber(left), ToNumber(right));
                left = op switch { "*" => a * b, "/" => a / b, _ => a % b };
            }
            return left;
        }

        private object? ParseUnary()
        {
            if (Current is { Kind: TokenKind.Op, Text: "!" }) { _i++; return !Truthy(ParseUnary()); }
            if (Current is { Kind: TokenKind.Op, Text: "-" }) { _i++; return -ToNumber(ParseUnary()); }
            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            var t = Current;
            switch (t.Kind)
            {
                case TokenKind.Number: _i++; return t.Number;
                case TokenKind.String: _i++; return t.Text;
                case TokenKind.LParen:
                    _i++;
                    var inner = ParseOr();
                    Expect(TokenKind.RParen);
                    return inner;
                case TokenKind.Ident:
                    _i++;
                    if (Current.Kind == TokenKind.LParen) return CallFunction(t.Text);
                    return t.Text switch
                    {
                        "true" => true,
                        "false" => false,
                        "null" => null,
                        _ => Resolve(t.Text)
                    };
                default:
                    throw new EvalException($"unexpected token '{t.Text}'");
            }
        }

        private object? CallFunction(string name)
        {
            Expect(TokenKind.LParen);
            var args = new List<object?>();
            if (Current.Kind != TokenKind.RParen)
            {
                args.Add(ParseOr());
                while (Current.Kind == TokenKind.Comma) { _i++; args.Add(ParseOr()); }
            }
            Expect(TokenKind.RParen);

            return name switch
            {
                "$iif" => Truthy(args.ElementAtOrDefault(0)) ? args.ElementAtOrDefault(1) : args.ElementAtOrDefault(2),
                "$not" => !Truthy(args.ElementAtOrDefault(0)),
                "$and" => args.All(Truthy),
                "$or" => args.Any(Truthy),
                "$concat" => string.Concat(args.Select(FormatValue)),
                "$coalesce" => args.FirstOrDefault(a => a is not null),
                "$switch" => Switch(args),
                "$sum" or "$avg" or "$count" or "$min" or "$max" or "$first" or "$last" => Aggregate(name, args),
                _ => throw new EvalException($"unknown function {name}")   // e.g. Sum/Format → caller falls back
            };
        }

        // Dataset aggregate: arg0 = the dataset (collection of row dicts), arg1 = optional field name.
        // $sum(DataSet, "Total"), $count(DataSet), $first(DataSet, "Name"), … Mirrors the frontend helpers.
        private static object? Aggregate(string name, List<object?> args)
        {
            if (args.Count == 0 || args[0] is string || args[0] is not System.Collections.IEnumerable rowsRaw)
                throw new EvalException("aggregate requires a dataset");
            var field = args.Count > 1 && args[1] is not null ? FormatValue(args[1]) : null;
            var values = new List<object?>();
            foreach (var row in rowsRaw) values.Add(RowValue(row, field));

            switch (name)
            {
                case "$count": return (double)(field is null ? values.Count : values.Count(v => v is not null));
                case "$first": return values.Count > 0 ? values[0] : null;
                case "$last": return values.Count > 0 ? values[^1] : null;
            }
            var nums = values.Where(IsNumeric).Select(ToNumber).ToList();
            return name switch
            {
                "$sum" => nums.Sum(),
                "$avg" => nums.Count > 0 ? nums.Average() : 0d,
                "$min" => nums.Count > 0 ? nums.Min() : 0d,
                "$max" => nums.Count > 0 ? nums.Max() : 0d,
                _ => throw new EvalException($"unknown aggregate {name}")
            };
        }

        // The aggregate's per-row argument: a bare field name (fast dict read) or a sub-expression
        // (Qty * Price, $iif(Paid, Total, 0)) evaluated against the row — so Sum(Qty*Price) works.
        private static object? RowValue(object? row, string? field)
        {
            if (field is null) return row;
            if (IsBareIdentifier(field)) return FieldValue(row, field);
            if (AsRowData(row) is { } data && TryEvaluate(field, data, out var v)) return v;
            return FieldValue(row, field);
        }

        private static bool IsBareIdentifier(string s)
        {
            if (s.Length == 0 || !(char.IsLetter(s[0]) || s[0] == '_')) return false;
            foreach (var c in s) if (!(char.IsLetterOrDigit(c) || c == '_')) return false;
            return true;
        }

        private static IReadOnlyDictionary<string, object?>? AsRowData(object? row) => row switch
        {
            IReadOnlyDictionary<string, object?> d => d,
            IDictionary<string, object?> d => new Dictionary<string, object?>(d),
            _ => null
        };

        private static object? FieldValue(object? row, string? field) => field is null
            ? row
            : row switch
            {
                IReadOnlyDictionary<string, object?> d => d.TryGetValue(field, out var v) ? v : null,
                IDictionary<string, object?> d => d.TryGetValue(field, out var v) ? v : null,
                _ => null
            };

        private static object? Switch(List<object?> args)
        {
            for (var i = 0; i + 1 < args.Count; i += 2)
                if (Truthy(args[i])) return args[i + 1];
            return args.Count % 2 == 1 ? args[^1] : null;   // optional trailing default
        }

        private object? Resolve(string name)
        {
            if (_data.TryGetValue(name, out var v)) return v;
            // Dotted member access: try nested dictionaries (a.b.c).
            if (name.Contains('.'))
            {
                object? current = _data;
                foreach (var part in name.Split('.'))
                {
                    if (current is IReadOnlyDictionary<string, object?> d && d.TryGetValue(part, out var next)) current = next;
                    else return null;
                }
                return current;
            }
            return null;   // unknown identifier → null (matches frontend)
        }

        private Token Current => _tokens[_i];
        private void Expect(TokenKind kind)
        {
            if (Current.Kind != kind) throw new EvalException($"expected {kind}");
            _i++;
        }

        // ── value helpers ────────────────────────────────────────────────────────────────────────────
        private static bool Truthy(object? v) => v switch
        {
            null => false,
            bool b => b,
            double d => d != 0,
            string s => s.Length > 0,
            _ => true
        };

        private static bool IsNumeric(object? v) =>
            v is double or float or long or int or short or byte or decimal
            || (v is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _));

        private static double ToNumber(object? v) => v switch
        {
            double d => d,
            float f => f,
            long l => l,
            int i => i,
            short sh => sh,
            byte by => by,
            decimal m => (double)m,
            bool b => b ? 1 : 0,
            string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) => n,
            _ => throw new EvalException("non-numeric operand")
        };

        private static bool LooseEquals(object? a, object? b)
        {
            if (a is null || b is null) return a is null && b is null;
            if (IsNumeric(a) && IsNumeric(b)) return ToNumber(a) == ToNumber(b);
            if (a is bool || b is bool) return Truthy(a) == Truthy(b);
            return FormatValue(a) == FormatValue(b);
        }

        private static int Compare(object? a, object? b)
        {
            if (IsNumeric(a) && IsNumeric(b)) return ToNumber(a).CompareTo(ToNumber(b));
            return string.Compare(FormatValue(a), FormatValue(b), StringComparison.Ordinal);
        }

        // ── tokenizer ────────────────────────────────────────────────────────────────────────────────
        private static List<Token> Tokenize(string s)
        {
            var tokens = new List<Token>();
            var i = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                if (c is '"' or '\'')
                {
                    var quote = c; var sb = new System.Text.StringBuilder(); i++;
                    while (i < s.Length && s[i] != quote)
                    {
                        if (s[i] == '\\' && i + 1 < s.Length) { sb.Append(s[i + 1]); i += 2; }
                        else sb.Append(s[i++]);
                    }
                    if (i >= s.Length) throw new EvalException("unterminated string");
                    i++; // closing quote
                    tokens.Add(new Token(TokenKind.String, sb.ToString()));
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
                {
                    var start = i;
                    while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                    tokens.Add(new Token(TokenKind.Number, s[start..i])
                    {
                        Number = double.Parse(s[start..i], CultureInfo.InvariantCulture)
                    });
                    continue;
                }

                if (char.IsLetter(c) || c is '_' or '$')
                {
                    var start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] is '_' or '$' or '.')) i++;
                    tokens.Add(new Token(TokenKind.Ident, s[start..i]));
                    continue;
                }

                if (c == '(') { tokens.Add(new Token(TokenKind.LParen, "(")); i++; continue; }
                if (c == ')') { tokens.Add(new Token(TokenKind.RParen, ")")); i++; continue; }
                if (c == ',') { tokens.Add(new Token(TokenKind.Comma, ",")); i++; continue; }

                // two-char then one-char operators
                var two = i + 1 < s.Length ? s.Substring(i, 2) : "";
                if (two is "==" or "!=" or "<=" or ">=" or "&&" or "||") { tokens.Add(new Token(TokenKind.Op, two)); i += 2; continue; }
                if (c is '+' or '-' or '*' or '/' or '%' or '<' or '>' or '!') { tokens.Add(new Token(TokenKind.Op, c.ToString())); i++; continue; }

                throw new EvalException($"unexpected character '{c}'");
            }
            tokens.Add(new Token(TokenKind.End, ""));
            return tokens;
        }
    }

    private enum TokenKind { Number, String, Ident, Op, LParen, RParen, Comma, End }

    private sealed class Token(TokenKind kind, string text)
    {
        public TokenKind Kind { get; } = kind;
        public string Text { get; } = text;
        public double Number { get; init; }
    }
}
