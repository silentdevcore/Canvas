using Canvas.Core.Abstractions;
using System.Text.RegularExpressions;

namespace Canvas.Core.Primitives;

public sealed class ExpressionEvaluator : IExpressionEvaluator
{
    public async Task<ExpressionResult> EvaluateAsync(string expression, Dictionary<string, object> data)
    {
        try
        {
            // Basic security check - prevent dangerous operations
            if (ContainsDangerousPatterns(expression))
            {
                return new ExpressionResult
                {
                    IsValid = false,
                    Value = null,
                    Error = "Expression contains potentially dangerous operations"
                };
            }

            // Simple variable substitution for basic expressions
            var processedExpression = ProcessExpression(expression, data);

            // For now, implement basic evaluation - in production this would use a proper expression engine
            var result = EvaluateSimpleExpression(processedExpression);

            return new ExpressionResult
            {
                IsValid = true,
                Value = result,
                Error = null
            };
        }
        catch (Exception ex)
        {
            return new ExpressionResult
            {
                IsValid = false,
                Value = null,
                Error = $"Expression evaluation failed: {ex.Message}"
            };
        }
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

    private string ProcessExpression(string expression, Dictionary<string, object> data)
    {
        // Replace variable references with actual values
        var result = expression;

        // Simple variable replacement: data.field -> actual value
        foreach (var kvp in data)
        {
            var pattern = $@"\b{Regex.Escape(kvp.Key)}\b";
            if (kvp.Value is string strValue)
            {
                result = Regex.Replace(result, pattern, $"\"{strValue.Replace("\"", "\\\"")}\"");
            }
            else if (kvp.Value is bool boolValue)
            {
                result = Regex.Replace(result, pattern, boolValue.ToString().ToLower());
            }
            else if (kvp.Value is int || kvp.Value is long || kvp.Value is double || kvp.Value is float)
            {
                result = Regex.Replace(result, pattern, kvp.Value.ToString()!);
            }
            else
            {
                // For complex objects, convert to string representation
                result = Regex.Replace(result, pattern, $"\"{kvp.Value?.ToString() ?? ""}\"");
            }
        }

        return result;
    }

    private object EvaluateSimpleExpression(string expression)
    {
        // Very basic expression evaluation for common cases
        expression = expression.Trim();

        // Handle boolean literals
        if (expression.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (expression.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        // Handle string literals
        if (expression.StartsWith("\"") && expression.EndsWith("\""))
        {
            return expression.Substring(1, expression.Length - 2).Replace("\\\"", "\"");
        }

        // Handle number literals
        if (double.TryParse(expression, out var number))
        {
            return number;
        }

        // Handle simple comparisons (basic implementation)
        if (expression.Contains("=="))
        {
            var parts = expression.Split(new[] { "==" }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                return parts[0].Trim().Equals(parts[1].Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        if (expression.Contains("!="))
        {
            var parts = expression.Split(new[] { "!=" }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                return !parts[0].Trim().Equals(parts[1].Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        // For more complex expressions, return the processed string
        // In a production system, this would use a proper expression parser
        return expression;
    }
}