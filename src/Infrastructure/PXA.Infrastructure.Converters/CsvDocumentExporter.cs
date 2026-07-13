using System.Text;
using PXA.Core.Abstractions;
using PXA.Core.Contracts;

namespace PXA.Infrastructure.Converters;

public sealed class CsvDocumentExporter : DocumentExporter
{
    public string FormatKey     => "csv";
    public string MimeType      => "text/csv; charset=utf-8";
    public string FileExtension => ".csv";
    public IExporterCapabilities Capabilities => new ExporterCapabilities(SupportsImages: false, SupportsRichText: false, SupportsFormFields: false);

    public byte[] Export(DesignExportDto design)
    {
        var sb = new StringBuilder();

        // UTF-8 BOM so Excel opens it correctly
        sb.Append('﻿');

        var allElements = design.Pages
            .SelectMany(p => p.Elements)
            .Concat(design.SharedElements)
            .Where(e => e.Hidden != true)
            .ToList();

        var tables   = allElements.Where(e => e.Type == "table").ToList();
        var nonTable = allElements.Where(e => e.Type != "table").ToList();

        // Metadata section
        sb.AppendLine("# Metadata");
        sb.AppendLine(Csv("type", "name", "x", "y", "content"));
        foreach (var el in nonTable)
        {
            var content = el.Type switch
            {
                "text"       => el.Content ?? "",
                "richtext"   => StripTags(el.HtmlContent ?? ""),
                "link"       => el.Href ?? el.Content ?? "",
                "number"     => el.NumberValue?.ToString() ?? "",
                "field"      => el.FieldLabel ?? "",
                "checkbox"   => el.FieldLabel ?? "",
                "signature"  => el.SignatureLabel ?? "",
                "note"       => el.NoteTitle ?? "",
                "optionlist" => string.Join("; ", el.Options ?? []),
                "dropdown"   => string.Join("; ", el.Options ?? []),
                "radio"      => string.Join("; ", el.Options ?? []),
                _            => el.Content ?? "",
            };
            sb.AppendLine(Csv(el.Type, el.Name ?? el.Id, el.X.ToString("F0"), el.Y.ToString("F0"), content));
        }

        // Table sections
        foreach (var table in tables)
        {
            sb.AppendLine();
            sb.AppendLine($"# Table: {table.Name ?? table.Id}");

            var cellData = table.CellData;
            if (cellData is null || cellData.Length == 0) continue;

            foreach (var row in cellData)
                sb.AppendLine(string.Join(",", (row ?? []).Select(CsvField)));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Csv(params string[] fields)
        => string.Join(",", fields.Select(CsvField));

    private static string CsvField(string? value)
    {
        if (value is null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string StripTags(string html)
    {
        // Minimal HTML tag stripping — no external dependencies
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "").Trim();
    }
}
