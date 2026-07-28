namespace PXA.WebApi.Observability;

internal static class PxaLogPrivacy
{
    public const string SuppressedMessage = "Unstructured log message suppressed.";

    public static IReadOnlyList<KeyValuePair<string, object?>> SanitizeAttributes(
        IEnumerable<KeyValuePair<string, object?>>? attributes,
        Exception? exception = null)
    {
        var sanitized = new List<KeyValuePair<string, object?>>();
        if (attributes is not null)
        {
            foreach (var attribute in attributes)
            {
                if (attribute.Key == "{OriginalFormat}")
                {
                    sanitized.Add(attribute);
                    continue;
                }

                if (PxaTelemetrySanitizingProcessor.IsForbiddenAttribute(attribute.Key))
                    continue;
                if (TrySanitizeValue(attribute.Value, out var value))
                    sanitized.Add(new KeyValuePair<string, object?>(attribute.Key, value));
            }
        }

        if (exception is not null)
        {
            sanitized.Add(new KeyValuePair<string, object?>(
                "exception.type",
                exception.GetType().FullName ?? exception.GetType().Name));
        }

        return sanitized;
    }

    public static string ResolveMessageTemplate(
        IEnumerable<KeyValuePair<string, object?>>? attributes)
    {
        var template = attributes?
            .FirstOrDefault(value => value.Key == "{OriginalFormat}")
            .Value?
            .ToString();
        if (string.IsNullOrWhiteSpace(template))
            return SuppressedMessage;

        var placeholderStart = 0;
        while ((placeholderStart = template.IndexOf('{', placeholderStart)) >= 0)
        {
            var placeholderEnd = template.IndexOf('}', placeholderStart + 1);
            if (placeholderEnd < 0)
                break;

            var placeholder = template[(placeholderStart + 1)..placeholderEnd]
                .Split([':', ','], 2)[0]
                .TrimStart('@', '$')
                .Trim();
            if (PxaTelemetrySanitizingProcessor.IsForbiddenAttribute(placeholder))
                return SuppressedMessage;
            placeholderStart = placeholderEnd + 1;
        }

        return template;
    }

    private static bool TrySanitizeValue(object? value, out object? sanitized)
    {
        sanitized = value switch
        {
            null => null,
            bool or byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal => value,
            Enum enumValue => enumValue.ToString(),
            string text => text.Length <= 256 ? text : text[..256],
            char character => character.ToString(),
            TimeSpan duration => duration.ToString("c"),
            DateTime timestamp => timestamp.ToUniversalTime().ToString("O"),
            DateTimeOffset timestamp => timestamp.ToUniversalTime().ToString("O"),
            _ => null,
        };
        return value is null || sanitized is not null;
    }
}
