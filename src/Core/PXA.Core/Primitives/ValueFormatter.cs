using PXA.Core.Abstractions;

namespace PXA.Core.Primitives;

public sealed class ValueFormatter : IValueFormatter
{
    public object Format(object value, string formatter)
    {
        if (value == null) return "";

        try
        {
            // Parse formatter configuration (basic implementation)
            // In production, this would parse a more sophisticated format specification
            var parts = formatter.Split(':');
            var formatType = parts[0].ToLower();

            switch (formatType)
            {
                case "currency":
                    return FormatCurrency(value, parts.Length > 1 ? parts[1] : "USD");

                case "date":
                    return FormatDate(value, parts.Length > 1 ? parts[1] : "yyyy-MM-dd");

                case "number":
                    return FormatNumber(value, parts.Length > 1 ? parts[1] : "en-US");

                case "uppercase":
                    return value.ToString()?.ToUpper() ?? "";

                case "lowercase":
                    return value.ToString()?.ToLower() ?? "";

                case "titlecase":
                    return ToTitleCase(value.ToString() ?? "");

                case "truncate":
                    var length = parts.Length > 1 ? int.Parse(parts[1]) : 50;
                    var suffix = parts.Length > 2 ? parts[2] : "...";
                    return Truncate(value.ToString() ?? "", length, suffix);

                default:
                    // Unknown formatter, return original value
                    return value;
            }
        }
        catch
        {
            // If formatting fails, return original value
            return value;
        }
    }

    private string FormatCurrency(object value, string currencyCode)
    {
        if (decimal.TryParse(value.ToString(), out var amount))
        {
            // Basic currency formatting - in production would use culture-specific formatting
            return $"{currencyCode} {amount:N2}";
        }
        return value.ToString() ?? "";
    }

    private string FormatDate(object value, string format)
    {
        if (value is DateTime dateTime)
        {
            // Basic date formatting - in production would use proper date formatting
            return dateTime.ToString(format.Replace("yyyy", "yyyy").Replace("MM", "MM").Replace("dd", "dd"));
        }
        else if (DateTime.TryParse(value.ToString(), out var parsedDate))
        {
            return parsedDate.ToString(format.Replace("yyyy", "yyyy").Replace("MM", "MM").Replace("dd", "dd"));
        }
        return value.ToString() ?? "";
    }

    private string FormatNumber(object value, string locale)
    {
        if (double.TryParse(value.ToString(), out var number))
        {
            // Basic number formatting - in production would use culture-specific formatting
            return number.ToString("N2");
        }
        return value.ToString() ?? "";
    }

    private string ToTitleCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var words = value.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (!string.IsNullOrEmpty(words[i]))
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", words);
    }

    private string Truncate(string value, int length, string suffix)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= length)
        {
            return value;
        }

        return value.Substring(0, length - suffix.Length) + suffix;
    }
}