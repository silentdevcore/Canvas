using System.Globalization;
using System.Text;
using PXA.Importer.Objects;

namespace PXA.Importer.Saving;

/// <summary>
/// Serializes PDF objects to bytes. Shared by the incremental and full-rewrite writers so
/// that both strategies produce identical syntax for the same value.
/// </summary>
internal static class PdfObjectSerializer
{
    public static void WriteIndirectObject(Stream stream, PdfObjectId id, PdfObject value)
    {
        WriteAscii(stream, $"{id.Number} {id.Generation} obj\n");
        WriteObject(stream, value);
        WriteAscii(stream, "\nendobj\n");
    }

    public static void WriteObject(Stream stream, PdfObject value)
    {
        switch (value)
        {
            case PdfName name:
                WriteName(stream, name.Value);
                break;
            case PdfInteger integer:
                WriteAscii(stream, integer.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case PdfNumber number:
                WriteAscii(stream, number.Value.ToString("0.###############", CultureInfo.InvariantCulture));
                break;
            case PdfString text:
                WriteAscii(stream, "<");
                foreach (var current in text.GetDecodedBytes().Span)
                {
                    WriteAscii(stream, current.ToString("X2", CultureInfo.InvariantCulture));
                }

                WriteAscii(stream, ">");
                break;
            case PdfBoolean boolean:
                WriteAscii(stream, boolean.Value ? "true" : "false");
                break;
            case PdfNull:
                WriteAscii(stream, "null");
                break;
            case PdfReference reference:
                WriteAscii(stream, $"{reference.Id.Number} {reference.Id.Generation} R");
                break;
            case PdfArray array:
                WriteAscii(stream, "[");
                for (var index = 0; index < array.Items.Count; index++)
                {
                    if (index > 0)
                    {
                        WriteAscii(stream, " ");
                    }

                    WriteObject(stream, array.Items[index]);
                }

                WriteAscii(stream, "]");
                break;
            case PdfDictionary dictionary:
                WriteAscii(stream, "<<");
                foreach (var entry in dictionary.Values.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
                {
                    WriteAscii(stream, " ");
                    WriteName(stream, entry.Key);
                    WriteAscii(stream, " ");
                    WriteObject(stream, entry.Value);
                }

                WriteAscii(stream, " >>");
                break;
            case PdfStreamObject streamObject:
                var streamDictionary = new PdfDictionary(
                    new Dictionary<string, PdfObject>(streamObject.Dictionary.Values, StringComparer.Ordinal))
                {
                    ["Length"] = new PdfInteger(streamObject.EncodedBytes.Length)
                };
                WriteObject(stream, streamDictionary);
                WriteAscii(stream, "\nstream\n");
                stream.Write(streamObject.EncodedBytes.Span);
                WriteAscii(stream, "\nendstream");
                break;
            default:
                throw new NotSupportedException($"PDF object type '{value.GetType().Name}' cannot be serialized.");
        }
    }

    public static IReadOnlyList<(int Start, List<(long Offset, int Generation)> Entries)> GroupSubsections(
        SortedDictionary<PdfObjectId, long> offsets)
    {
        var subsections = new List<(int Start, List<(long Offset, int Generation)> Entries)>();
        List<(long Offset, int Generation)>? currentEntries = null;
        var expectedNumber = -1;
        foreach (var entry in offsets)
        {
            if (currentEntries is null || entry.Key.Number != expectedNumber)
            {
                currentEntries = [];
                subsections.Add((entry.Key.Number, currentEntries));
            }

            currentEntries.Add((entry.Value, entry.Key.Generation));
            expectedNumber = entry.Key.Number + 1;
        }

        return subsections;
    }

    public static void WriteName(Stream stream, string value)
    {
        WriteAscii(stream, "/");
        foreach (var current in Encoding.UTF8.GetBytes(value))
        {
            if (current is >= 33 and <= 126 && current is not (byte)'#' && !IsDelimiter(current))
            {
                stream.WriteByte(current);
            }
            else
            {
                WriteAscii(stream, $"#{current:X2}");
            }
        }
    }

    public static void WriteAscii(Stream stream, string value) => stream.Write(Encoding.ASCII.GetBytes(value));

    private static bool IsDelimiter(byte value) => value is
        (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or
        (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    public sealed class PdfObjectIdComparer : IComparer<PdfObjectId>
    {
        public static PdfObjectIdComparer Instance { get; } = new();

        public int Compare(PdfObjectId left, PdfObjectId right)
        {
            var objectComparison = left.Number.CompareTo(right.Number);
            return objectComparison != 0 ? objectComparison : left.Generation.CompareTo(right.Generation);
        }
    }
}
