using PXA.Core.Contracts;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.VariantTypes;

namespace PXA.Infrastructure.Word;

/// <summary>
/// Writes user-defined key/value pairs to the DOCX custom document properties part.
/// </summary>
internal static class CustomPropertiesService
{
    internal static void Apply(WordprocessingDocument doc, IList<CustomDocumentPropertyDto>? props)
    {
        if (props is null || props.Count == 0) return;

        var part = doc.CustomFilePropertiesPart
            ?? doc.AddCustomFilePropertiesPart();

        part.Properties ??= new Properties();
        var properties = part.Properties;

        // Start PID at 2 (Word requires PIDs ≥ 2).
        int pid = properties.Elements<CustomDocumentProperty>().Count() + 2;

        foreach (var p in props)
        {
            // Remove duplicate by name.
            var existing = properties.Elements<CustomDocumentProperty>()
                .FirstOrDefault(e => e.Name?.Value == p.Name);
            existing?.Remove();

            var prop = new CustomDocumentProperty
            {
                FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
                PropertyId = pid++,
                Name = p.Name,
            };

            OpenXmlElement value = p.Type switch
            {
                "number"  when double.TryParse(p.Value, out var d) => new VTDouble(d.ToString()),
                "boolean" when bool.TryParse(p.Value, out var b)   => new VTBool(b.ToString().ToLowerInvariant()),
                "date"    when DateTime.TryParse(p.Value, out var dt) =>
                    new VTFileTime(dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")),
                _ => new VTLPWSTR(p.Value),
            };

            prop.Append(value);
            properties.Append(prop);
        }

        properties.Save();
    }
}
