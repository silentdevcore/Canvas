namespace Canvas.Core.Abstractions;

public interface IExpressionEvaluator
{
    /// <summary>
    /// Evaluates an expression with the provided data context.
    /// </summary>
    /// <param name="expression">The expression to evaluate</param>
    /// <param name="data">The data context for variable resolution</param>
    /// <returns>Evaluation result with value and validation status</returns>
    Task<ExpressionResult> EvaluateAsync(string expression, Dictionary<string, object> data);
}

public class ExpressionResult
{
    public bool IsValid { get; set; }
    public object? Value { get; set; }
    public string? Error { get; set; }
}