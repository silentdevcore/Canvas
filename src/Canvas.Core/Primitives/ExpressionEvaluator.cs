using Canvas.Core.Abstractions;
using System.Text.RegularExpressions;

namespace Canvas.Core.Primitives;

/// <summary>
/// <see cref="IExpressionEvaluator"/> backed by the shared <see cref="CanvasExpressionEvaluator"/> — the
/// same recursive-descent engine the export <c>DesignLayoutPlanner</c> and the frontend
/// <c>expressionEngine.ts</c> use. Evaluates Canvas-grammar expressions (literals, identifiers/member
/// access, <c>* / % + - == != &lt; &lt;= &gt; &gt;=</c>, <c>&amp;&amp; || !</c>, and the helpers
/// <c>$iif $switch $concat $and $or $not $coalesce</c> plus dataset aggregates) against the data context.
/// A defensive dangerous-pattern guard is retained.
/// </summary>
public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    public Task<ExpressionResult> EvaluateAsync(string expression, Dictionary<string, object> data)
    {
        // Defensive guard (the engine has no eval/IO surface, but this preserves the documented contract).
        if (ContainsDangerousPatterns(expression))
            return Task.FromResult(new ExpressionResult
            {
                IsValid = false,
                Value = null,
                Error = "Expression contains potentially dangerous operations"
            });

        var result = CanvasExpressionEvaluator.TryEvaluate(expression, data!, out var value)
            ? new ExpressionResult { IsValid = true, Value = value, Error = null }
            : new ExpressionResult { IsValid = false, Value = null, Error = "Expression could not be evaluated" };

        return Task.FromResult(result);
    }

    private bool ContainsDangerousPatterns(string expression)
    {
        var dangerousPatterns = new[]
        {
            @"eval\s*\(",
            @"Function\s*\(",
            @"require\s*\(",
            @"import\s*\(",
            @"process\s*\.",
            @"global\s*\.",
            @"window\s*\.",
            @"document\s*\.",
            @"console\s*\.",
            @"alert\s*\(",
            @"prompt\s*\(",
            @"confirm\s*\(",
            @"XMLHttpRequest",
            @"fetch\s*\(",
            @"setTimeout\s*\(",
            @"setInterval\s*\("
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (Regex.IsMatch(expression, pattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}