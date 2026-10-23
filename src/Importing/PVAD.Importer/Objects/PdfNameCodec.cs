using System.Text;

namespace PVAD.Importer.Objects;

internal static class PdfNameCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string Decode(string value)
    {
        if (!value.Contains('#', StringComparison.Ordinal))
        {
            return value;
        }

        using var bytes = new MemoryStream(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '#' && index + 2 < value.Length &&
                TryHex(value[index + 1], out var high) && TryHex(value[index + 2], out var low))
            {
                bytes.WriteByte((byte)((high << 4) | low));
                index += 2;
                continue;
            }

            foreach (var current in Encoding.UTF8.GetBytes(value[index].ToString()))
            {
                bytes.WriteByte(current);
            }
        }

        var decoded = bytes.ToArray();
        try
        {
            return StrictUtf8.GetString(decoded);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(decoded);
        }
    }

    private static bool TryHex(char value, out int result)
    {
        result = value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'A' and <= 'F' => value - 'A' + 10,
            >= 'a' and <= 'f' => value - 'a' + 10,
            _ => -1
        };
        return result >= 0;
    }
}
