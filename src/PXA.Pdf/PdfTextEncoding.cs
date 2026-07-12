using System.Globalization;
using System.Text;

namespace PXA.Pdf;

/// <summary>
/// Utility methods for encoding text for multi-language PDF rendering.
/// </summary>
public static class PdfTextEncoding
{
    /// <summary>
    /// Encodes a string as a PDF hex string in UTF-16BE with BOM.
    /// Example: "A" → &lt;FEFF0041&gt;.
    /// Used with Identity-H encoded composite fonts (embedded TrueType/OpenType).
    /// </summary>
    public static string EncodeAsHexUtf16Be(string text)
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes(text);
        var sb = new StringBuilder(bytes.Length * 2 + 6);
        sb.Append("<FEFF");
        foreach (var b in bytes)
            sb.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        sb.Append('>');
        return sb.ToString();
    }

    /// <summary>
    /// Reverses a string by Unicode grapheme clusters for visual-order RTL rendering.
    /// Note: does not perform full Arabic cursive shaping (future enhancement).
    /// </summary>
    public static string ReverseForRtl(string text)
    {
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        var elements = new List<string>();
        while (enumerator.MoveNext())
            elements.Add((string)enumerator.Current);
        elements.Reverse();
        return string.Concat(elements);
    }
}
