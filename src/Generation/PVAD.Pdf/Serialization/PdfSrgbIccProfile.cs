using System.Buffers.Binary;
using System.Text;

namespace PVAD.Pdf.Serialization;

/// <summary>
/// Builds a compact ICC v2 matrix profile for the sRGB color space. The profile uses the
/// published sRGB D50 colorants and a sampled sRGB transfer curve, so PVAD does not depend on a
/// platform-installed or redistributable vendor profile.
/// </summary>
internal static class PdfSrgbIccProfile
{
    private const int HeaderSize = 128;

    public static byte[] Create()
    {
        var tags = new (string Signature, byte[] Data)[]
        {
            ("desc", BuildDescription("PVAD sRGB IEC61966-2.1")),
            ("cprt", BuildText("Generated from the public sRGB color-space definition")),
            ("wtpt", BuildXyz(0.9642, 1.0, 0.8249)),
            ("rXYZ", BuildXyz(0.4360747, 0.2225045, 0.0139322)),
            ("gXYZ", BuildXyz(0.3850649, 0.7168786, 0.0971045)),
            ("bXYZ", BuildXyz(0.1430804, 0.0606169, 0.7141733)),
            ("rTRC", BuildSrgbCurve()),
            ("gTRC", BuildSrgbCurve()),
            ("bTRC", BuildSrgbCurve())
        };

        var tagTableSize = 4 + (tags.Length * 12);
        var offset = Align4(HeaderSize + tagTableSize);
        var entries = new List<(string Signature, int Offset, byte[] Data)>();
        foreach (var tag in tags)
        {
            entries.Add((tag.Signature, offset, tag.Data));
            offset = Align4(offset + tag.Data.Length);
        }

        var profile = new byte[offset];
        WriteUInt32(profile, 0, (uint)profile.Length);
        WriteAscii(profile, 4, "PVAD");
        WriteUInt32(profile, 8, 0x02100000); // ICC 2.1
        WriteAscii(profile, 12, "mntr");
        WriteAscii(profile, 16, "RGB ");
        WriteAscii(profile, 20, "XYZ ");
        WriteDateTime(profile, 24, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        WriteAscii(profile, 36, "acsp");
        WriteAscii(profile, 40, "MSFT");
        WriteUInt32(profile, 64, 0); // perceptual rendering intent
        WriteS15Fixed16(profile, 68, 0.9642);
        WriteS15Fixed16(profile, 72, 1.0);
        WriteS15Fixed16(profile, 76, 0.8249);
        WriteAscii(profile, 80, "PVAD");

        WriteUInt32(profile, HeaderSize, (uint)tags.Length);
        for (var i = 0; i < entries.Count; i++)
        {
            var tableOffset = HeaderSize + 4 + (i * 12);
            WriteAscii(profile, tableOffset, entries[i].Signature);
            WriteUInt32(profile, tableOffset + 4, (uint)entries[i].Offset);
            WriteUInt32(profile, tableOffset + 8, (uint)entries[i].Data.Length);
            entries[i].Data.CopyTo(profile, entries[i].Offset);
        }

        return profile;
    }

    private static byte[] BuildDescription(string text)
    {
        var ascii = Encoding.ASCII.GetBytes(text);
        var data = new byte[12 + ascii.Length + 1];
        WriteAscii(data, 0, "desc");
        WriteUInt32(data, 8, (uint)(ascii.Length + 1));
        ascii.CopyTo(data, 12);
        return data;
    }

    private static byte[] BuildText(string text)
    {
        var ascii = Encoding.ASCII.GetBytes(text);
        var data = new byte[8 + ascii.Length + 1];
        WriteAscii(data, 0, "text");
        ascii.CopyTo(data, 8);
        return data;
    }

    private static byte[] BuildXyz(double x, double y, double z)
    {
        var data = new byte[20];
        WriteAscii(data, 0, "XYZ ");
        WriteS15Fixed16(data, 8, x);
        WriteS15Fixed16(data, 12, y);
        WriteS15Fixed16(data, 16, z);
        return data;
    }

    private static byte[] BuildSrgbCurve()
    {
        const int samples = 256;
        var data = new byte[12 + (samples * 2)];
        WriteAscii(data, 0, "curv");
        WriteUInt32(data, 8, samples);
        for (var i = 0; i < samples; i++)
        {
            var encoded = i / 255d;
            var linear = encoded <= 0.04045
                ? encoded / 12.92
                : Math.Pow((encoded + 0.055) / 1.055, 2.4);
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(12 + (i * 2), 2), (ushort)Math.Round(linear * ushort.MaxValue));
        }
        return data;
    }

    private static int Align4(int value) => (value + 3) & ~3;

    private static void WriteDateTime(byte[] target, int offset, DateTime value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset, 2), (ushort)value.Year);
        BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset + 2, 2), (ushort)value.Month);
        BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset + 4, 2), (ushort)value.Day);
        BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset + 6, 2), (ushort)value.Hour);
        BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset + 8, 2), (ushort)value.Minute);
        BinaryPrimitives.WriteUInt16BigEndian(target.AsSpan(offset + 10, 2), (ushort)value.Second);
    }

    private static void WriteS15Fixed16(byte[] target, int offset, double value) =>
        BinaryPrimitives.WriteInt32BigEndian(target.AsSpan(offset, 4), (int)Math.Round(value * 65536d));

    private static void WriteUInt32(byte[] target, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(target.AsSpan(offset, 4), value);

    private static void WriteAscii(byte[] target, int offset, string value) =>
        Encoding.ASCII.GetBytes(value).CopyTo(target, offset);
}
