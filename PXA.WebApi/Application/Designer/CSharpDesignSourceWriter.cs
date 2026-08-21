using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using PXA.Core.Contracts;

namespace PXA.WebApi.Application.Designer;

internal sealed class CSharpDesignSourceWriter
{
    public string Write(DesignExportDto design) => WriteValue(design, typeof(DesignExportDto), 0);

    internal string WriteContractObject(object value) => WriteValue(value, value.GetType(), 0);

    public string WriteElement(ElementDto element, int indent = 0) =>
        WriteValue(element, typeof(ElementDto), indent);

    public string WritePageEnvelope(PageDto page, int indent = 0)
    {
        var envelope = new PageDto { Id = page.Id, Extensions = page.Extensions, Elements = [] };
        return WriteValue(envelope, typeof(PageDto), indent);
    }

    public string WriteDocumentEnvelope(DesignExportDto design)
    {
        var envelope = new DesignExportDto
        {
            Id = design.Id,
            Name = design.Name,
            Category = design.Category,
            Description = design.Description,
            Pages = [],
            SharedElements = design.SharedElements,
            PageSettings = design.PageSettings,
            ImportDiagnostics = design.ImportDiagnostics,
            Extensions = design.Extensions,
        };
        return WriteValue(envelope, typeof(DesignExportDto), 0);
    }

    private string WriteValue(object? value, Type declaredType, int indent)
    {
        if (value is null)
            return "null";

        var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (type == typeof(object))
            type = value.GetType();
        if (value is JsonElement jsonElement)
            return $"PxaCodeValue.Json({Literal(jsonElement.GetRawText())})";
        if (type == typeof(string)) return Literal((string)value);
        if (type == typeof(char)) return $"'{EscapeChar((char)value)}'";
        if (type == typeof(bool)) return (bool)value ? "true" : "false";
        if (type == typeof(Guid)) return $"Guid.Parse({Literal(value.ToString()!)})";
        if (type == typeof(DateTimeOffset)) return $"DateTimeOffset.Parse({Literal(((DateTimeOffset)value).ToString("O", CultureInfo.InvariantCulture))}, CultureInfo.InvariantCulture)";
        if (type == typeof(DateTime)) return $"DateTime.Parse({Literal(((DateTime)value).ToString("O", CultureInfo.InvariantCulture))}, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)";
        if (type.IsEnum) return $"{TypeName(type)}.{Enum.GetName(type, value)}";
        if (IsNumber(type)) return Number(value, type);
        if (value is IDictionary dictionary) return WriteDictionary(dictionary, type, indent);
        if (value is IEnumerable sequence) return WriteSequence(sequence, type, indent);
        return WriteObject(value, type, indent);
    }

    private string WriteObject(object value, Type type, int indent)
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.MetadataToken)
            .ToArray();
        var builder = new StringBuilder();
        builder.Append("new ").Append(TypeName(type)).AppendLine()
            .Append(Spaces(indent)).AppendLine("{");
        foreach (var property in properties)
        {
            builder.Append(Spaces(indent + 1)).Append(property.Name).Append(" = ")
                .Append(WriteValue(property.GetValue(value), property.PropertyType, indent + 1))
                .AppendLine(",");
        }
        builder.Append(Spaces(indent)).Append('}');
        return builder.ToString();
    }

    private string WriteDictionary(IDictionary dictionary, Type type, int indent)
    {
        var arguments = type.IsGenericType ? type.GetGenericArguments() : [typeof(object), typeof(object)];
        var keyType = arguments[0];
        var valueType = arguments[1];
        var builder = new StringBuilder();
        builder.Append("new Dictionary<").Append(TypeName(keyType)).Append(", ").Append(TypeName(valueType)).AppendLine(">")
            .Append(Spaces(indent)).AppendLine("{");
        foreach (DictionaryEntry entry in dictionary)
        {
            builder.Append(Spaces(indent + 1)).Append('[').Append(WriteValue(entry.Key, keyType, indent + 1)).Append("] = ")
                .Append(WriteValue(entry.Value, valueType, indent + 1)).AppendLine(",");
        }
        builder.Append(Spaces(indent)).Append('}');
        return builder.ToString();
    }

    private string WriteSequence(IEnumerable sequence, Type type, int indent)
    {
        var itemType = type.IsArray ? type.GetElementType()! :
            type.GetInterfaces().Append(type).FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments()[0]
            ?? typeof(object);
        var builder = new StringBuilder();
        builder.Append(type.IsArray ? $"new {TypeName(itemType)}[]" : $"new List<{TypeName(itemType)}>").AppendLine()
            .Append(Spaces(indent)).AppendLine("{");
        foreach (var item in sequence)
        {
            if (item is ElementDto element)
                builder.Append(Spaces(indent + 1)).Append("// pxa-element-id: ").AppendLine(SafeComment(element.Id));
            builder.Append(Spaces(indent + 1)).Append(WriteValue(item, itemType, indent + 1)).AppendLine(",");
        }
        builder.Append(Spaces(indent)).Append('}');
        return builder.ToString();
    }

    private static bool IsNumber(Type type) => type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
        type == typeof(ushort) || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) ||
        type == typeof(float) || type == typeof(double) || type == typeof(decimal);

    private static string Number(object value, Type type) => type switch
    {
        _ when type == typeof(float) => ((float)value).ToString("R", CultureInfo.InvariantCulture) + "f",
        _ when type == typeof(double) => ((double)value).ToString("R", CultureInfo.InvariantCulture) + "d",
        _ when type == typeof(decimal) => ((decimal)value).ToString(CultureInfo.InvariantCulture) + "m",
        _ when type == typeof(uint) => value.ToString() + "u",
        _ when type == typeof(long) => value.ToString() + "L",
        _ when type == typeof(ulong) => value.ToString() + "UL",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
    };

    private static string TypeName(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (!type.IsGenericType) return type.Name;
        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
    }

    private static string Literal(string value) => JsonSerializer.Serialize(value);
    private static string EscapeChar(char value) => value switch { '\\' => "\\\\", '\'' => "\\'", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", _ => value.ToString() };
    private static string Spaces(int indent) => new(' ', indent * 4);
    private static string SafeComment(string value) => value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal)[..Math.Min(value.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal).Length, 200)];
}
