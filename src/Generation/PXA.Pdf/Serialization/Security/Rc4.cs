namespace PXA.Pdf.Serialization.Security;

/// <summary>
/// RC4 stream cipher. Not provided by the .NET BCL, but required by the PDF Standard Security
/// Handler (revisions 2–4) for the key, <c>/O</c>, <c>/U</c>, and per-object string/stream crypto.
/// RC4 is symmetric: the same operation encrypts and decrypts.
/// </summary>
internal static class Rc4
{
    public static byte[] Transform(byte[] key, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(data);
        if (key.Length == 0)
        {
            throw new ArgumentException("RC4 key must not be empty.", nameof(key));
        }

        // Key-scheduling algorithm (KSA).
        var s = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            s[i] = (byte)i;
        }

        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }

        // Pseudo-random generation algorithm (PRGA).
        var output = new byte[data.Length];
        int a = 0, b = 0;
        for (var k = 0; k < data.Length; k++)
        {
            a = (a + 1) & 0xFF;
            b = (b + s[a]) & 0xFF;
            (s[a], s[b]) = (s[b], s[a]);
            var keyStreamByte = s[(s[a] + s[b]) & 0xFF];
            output[k] = (byte)(data[k] ^ keyStreamByte);
        }

        return output;
    }
}
