namespace Canvas.Core.Abstractions;

public interface IValueFormatter
{
    /// <summary>
    /// Formats a value according to the specified formatter configuration.
    /// </summary>
    /// <param name="value">The value to format</param>
    /// <param name="formatter">The formatter configuration</param>
    /// <returns>The formatted value</returns>
    object Format(object value, string formatter);
}