using System.Text;
using System.Text.RegularExpressions;

namespace Canvas.Migration.Abstractions;

/// <summary>
/// Translates report-designer expression dialects (RDL/SSRS, DevExpress) into the grammar the Canvas
/// frontend expression engine evaluates: identifiers for fields, simple binary operators, and a small set
/// of helper functions (<c>$iif</c>, <c>$switch</c>, <c>$concat</c>, <c>$and</c>, <c>$or</c>, <c>$not</c>,
/// <c>$coalesce</c>). Single-row scope plus dataset aggregates (<c>Sum/Avg/Count/Min/Max/First/Last</c> →
/// <c>$sum(DataSet, "Field")</c> …) when a dataset name is supplied; custom <c>&lt;Code&gt;</c> is not handled.
/// The transform is precedence-aware and quote/paren-safe; it returns null when nothing useful translates.
/// </summary>
public static class ExpressionTranslator
{
    /// <summary>Translate an RDL/SSRS expression (e.g. <c>=IIf(Fields!Paid.Value, "Y", "N")</c>).
    /// Pass <paramref name="dataSetName"/> (the element's owning dataset) to enable aggregate translation.</summary>
    public static string? TranslateRdl(string? expression, string? dataSetName = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        var expr = expression.Trim();
        if (expr.StartsWith('=')) expr = expr[1..].Trim();          // RDL expression marker
        expr = RdlFieldRefs(expr);                                   // Fields!X.Value → X (quote-safe)
        var result = Translate(expr, rdl: true, dataSetName).Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    /// <summary>Translate a DevExpress expression (e.g. <c>[Qty] * [Price]</c>, <c>Iif([Ok], 1, 0)</c>).
    /// Pass <paramref name="dataSetName"/> (the element's owning dataset) to enable aggregate translation.</summary>
    public static string? TranslateDevExpress(string? expression, string? dataSetName = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;
        var expr = DevExpressFieldRefs(expression.Trim());           // [X] / [Ds.X] → X (quote-safe)
        var result = Translate(expr, rdl: false, dataSetName).Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    // RDL/DevExpress aggregate function → Canvas helper. Single-field, whole-dataset scope (v1).
    private static readonly Dictionary<string, string> Aggregates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sum"] = "$sum", ["Avg"] = "$avg", ["Average"] = "$avg", ["Count"] = "$count",
        ["Min"] = "$min", ["Max"] = "$max", ["First"] = "$first", ["Last"] = "$last",
    };

    // ── field references (applied only outside string literals) ──────────────────────────────────────
    private static string RdlFieldRefs(string expr) => OutsideStrings(expr, code =>
    {
        code = Regex.Replace(code, @"(?:Fields|Parameters|ReportItems|Globals|Variables)!(\w+)(?:\.\w+)?",
            m => m.Groups[1].Value);
        return code;
    });

    private static string DevExpressFieldRefs(string expr) => OutsideStrings(expr, code =>
        // [Field] or [DataSource.Field] → last identifier segment.
        Regex.Replace(code, @"\[([^\]]+)\]", m =>
        {
            var name = m.Groups[1].Value.Trim();
            var dot = name.LastIndexOf('.');
            return dot >= 0 ? name[(dot + 1)..] : name;
        }));

    // ── precedence-aware recursive transform ─────────────────────────────────────────────────────────
    // Lowest precedence first so it binds last: Or → And → Not → concat(&) → comparison → IIf/Switch → leaf.
    private static string Translate(string expr, bool rdl, string? dataSet)
    {
        expr = expr.Trim();
        if (expr.Length == 0) return expr;

        // Unwrap a fully-enclosing pair of parentheses.
        if (expr[0] == '(' && MatchingParen(expr, 0) == expr.Length - 1)
            return "(" + Translate(expr[1..^1], rdl, dataSet) + ")";

        // Logical OR / AND (word operators in RDL; symbols too).
        if (SplitWord(expr, "OrElse", "Or", "||") is { Count: > 1 } orParts)
            return Wrap("$or", orParts, rdl, dataSet);
        if (SplitWord(expr, "AndAlso", "And", "&&") is { Count: > 1 } andParts)
            return Wrap("$and", andParts, rdl, dataSet);
        if (StartsWithWord(expr, "Not"))
            return "$not(" + Translate(expr[3..].Trim(), rdl, dataSet) + ")";
        if (expr.StartsWith('!') && expr.Length > 1 && expr[1] != '=')
            return "$not(" + Translate(expr[1..].Trim(), rdl, dataSet) + ")";

        // String concatenation (RDL '&').
        if (SplitChar(expr, '&') is { Count: > 1 } concatParts)
            return "$concat(" + string.Join(", ", concatParts.Select(p => Translate(p, rdl, dataSet))) + ")";

        // Comparison (single top-level operator).
        if (FindTopLevelComparison(expr) is { } cmp)
            return Translate(cmp.Left, rdl, dataSet) + " " + cmp.Op + " " + Translate(cmp.Right, rdl, dataSet);

        // IIf / Switch / aggregates → helper calls.
        if (FunctionCall(expr) is { } call)
        {
            var name = call.Name;
            if (name.Equals("IIf", StringComparison.OrdinalIgnoreCase) || name.Equals("Iif", StringComparison.OrdinalIgnoreCase))
                return "$iif(" + TranslateArgs(call.Args, rdl, dataSet) + ")";
            if (name.Equals("Switch", StringComparison.OrdinalIgnoreCase))
                return "$switch(" + TranslateArgs(call.Args, rdl, dataSet) + ")";
            if (name.Equals("IsNothing", StringComparison.OrdinalIgnoreCase) || name.Equals("IsNull", StringComparison.OrdinalIgnoreCase))
                return "$isEmpty(" + TranslateArgs(call.Args, rdl, dataSet) + ")";
            // Dataset aggregate over a single field: Sum(Field) → $sum(DataSet, "Field"). Needs a dataset
            // and a bare-identifier field (field refs were already normalized to identifiers upstream).
            if (Aggregates.TryGetValue(name, out var helper)
                && dataSet?.Trim() is { Length: > 0 } ds && Regex.IsMatch(ds, @"^[A-Za-z_]\w*$")
                && call.Args is [var only]
                && Regex.IsMatch(only.Trim(), @"^[A-Za-z_]\w*$"))
                return $"{helper}({ds}, \"{only.Trim()}\")";
            // Unknown function: translate its arguments but keep the original name (engine may have/lack it).
            return name + "(" + TranslateArgs(call.Args, rdl, dataSet) + ")";
        }

        // Leaf: identifier / number / string literal / simple binary arithmetic — left as-is.
        return expr;
    }

    private static string Wrap(string fn, List<string> parts, bool rdl, string? dataSet) =>
        fn + "(" + string.Join(", ", parts.Select(p => Translate(p, rdl, dataSet))) + ")";

    private static string TranslateArgs(List<string> args, bool rdl, string? dataSet) =>
        string.Join(", ", args.Select(a => Translate(a, rdl, dataSet)));

    // ── top-level splitting (quote/paren aware) ──────────────────────────────────────────────────────
    private static List<string> SplitChar(string expr, char op)
    {
        var parts = new List<string>();
        int depth = 0, start = 0; char quote = '\0';
        for (var i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') { quote = c; continue; }
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == op && depth == 0)
            {
                if (op == '&' && i + 1 < expr.Length && expr[i + 1] == '&') { i++; continue; } // not '&&'
                parts.Add(expr[start..i]);
                start = i + 1;
            }
        }
        parts.Add(expr[start..]);
        return parts.Count > 1 ? parts.Select(p => p.Trim()).ToList() : [expr];
    }

    private static List<string> SplitWord(string expr, params string[] words)
    {
        var parts = new List<string>();
        int depth = 0, start = 0; char quote = '\0';
        for (var i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') { quote = c; continue; }
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0)
            {
                foreach (var w in words)
                {
                    if (!MatchesWordAt(expr, i, w)) continue;
                    parts.Add(expr[start..i]);
                    i += w.Length - 1;
                    start = i + 1;
                    break;
                }
            }
        }
        parts.Add(expr[start..]);
        return parts.Count > 1 ? parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList() : [expr];
    }

    // Word operators must be whole words (e.g. "And" not inside "Brand"); symbol operators match directly.
    private static bool MatchesWordAt(string expr, int i, string word)
    {
        if (i + word.Length > expr.Length || !expr.AsSpan(i, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase))
            return false;
        var isAlpha = char.IsLetter(word[0]);
        if (!isAlpha) return true;
        var before = i == 0 || !char.IsLetterOrDigit(expr[i - 1]) && expr[i - 1] != '_';
        var afterIdx = i + word.Length;
        var after = afterIdx >= expr.Length || !char.IsLetterOrDigit(expr[afterIdx]) && expr[afterIdx] != '_';
        return before && after;
    }

    private static (string Left, string Op, string Right)? FindTopLevelComparison(string expr)
    {
        // RDL uses '=' and '<>'; both dialects use < > <= >=. Map to engine operators.
        int depth = 0; char quote = '\0';
        for (var i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') { quote = c; continue; }
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0)
            {
                var two = i + 1 < expr.Length ? expr.Substring(i, 2) : "";
                if (two is "<>") return (expr[..i], "!=", expr[(i + 2)..]);
                if (two is "<=" or ">=" or "==" or "!=") return (expr[..i], two, expr[(i + 2)..]);
                if (c is '<' or '>') return (expr[..i], c.ToString(), expr[(i + 1)..]);
                if (c == '=') return (expr[..i], "==", expr[(i + 1)..]);  // RDL equality
            }
        }
        return null;
    }

    private static (string Name, List<string> Args)? FunctionCall(string expr)
    {
        var open = expr.IndexOf('(');
        if (open <= 0 || expr[^1] != ')') return null;
        var name = expr[..open].Trim();
        if (!Regex.IsMatch(name, @"^[A-Za-z_]\w*$")) return null;       // must be a bare call, not (a)+b etc.
        if (MatchingParen(expr, open) != expr.Length - 1) return null;  // the '(' must close at the very end
        var inner = expr[(open + 1)..^1];
        return (name, SplitArgs(inner));
    }

    private static List<string> SplitArgs(string inner)
    {
        if (string.IsNullOrWhiteSpace(inner)) return [];
        var args = new List<string>();
        int depth = 0, start = 0; char quote = '\0';
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') { quote = c; continue; }
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0) { args.Add(inner[start..i].Trim()); start = i + 1; }
        }
        args.Add(inner[start..].Trim());
        return args;
    }

    private static int MatchingParen(string expr, int openIndex)
    {
        int depth = 0; char quote = '\0';
        for (var i = openIndex; i < expr.Length; i++)
        {
            var c = expr[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') { quote = c; continue; }
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static bool StartsWithWord(string expr, string word) =>
        MatchesWordAt(expr, 0, word) && expr.Length > word.Length && char.IsWhiteSpace(expr[word.Length]);

    // Run a transform on the code regions of an expression, leaving string-literal contents untouched.
    private static string OutsideStrings(string expr, Func<string, string> transform)
    {
        var sb = new StringBuilder();
        int start = 0; char quote = '\0';
        for (var i = 0; i < expr.Length; i++)
        {
            var c = expr[i];
            if (quote != '\0') { if (c == quote) { sb.Append(expr[start..(i + 1)]); start = i + 1; quote = '\0'; } continue; }
            if (c is '"' or '\'') { sb.Append(transform(expr[start..i])); start = i; quote = c; }
        }
        if (quote == '\0') sb.Append(transform(expr[start..]));
        else sb.Append(expr[start..]);   // unterminated string — append as-is
        return sb.ToString();
    }
}
