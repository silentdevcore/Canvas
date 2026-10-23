using System.Text;

namespace PVAD.Pdf.Serialization.Security;

/// <summary>Prepares revision-6 PDF passwords with the RFC 4013 SASLprep profile.</summary>
internal static partial class PdfPasswordPreparation
{
    private const int PasswordByteLimit = 127;

    internal static byte[] Prepare(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return [];
        }

        var mapped = new StringBuilder(password.Length);
        foreach (var rune in password.EnumerateRunes())
        {
            var value = rune.Value;
            if (IsNonAsciiSpace(value))
            {
                mapped.Append(' ');
            }
            else if (!IsCommonlyMappedToNothing(value))
            {
                mapped.Append(rune.ToString());
            }
        }

        var normalized = mapped.ToString().Normalize(NormalizationForm.FormKC);
        ValidatePreparedString(normalized, nameof(password));
        var bytes = Encoding.UTF8.GetBytes(normalized);
        return bytes.Length <= PasswordByteLimit
            ? bytes
            : bytes.AsSpan(0, PasswordByteLimit).ToArray();
    }

    private static void ValidatePreparedString(string value, string parameterName)
    {
        var first = -1;
        var last = -1;
        var containsRandAl = false;
        var containsL = false;

        foreach (var rune in value.EnumerateRunes())
        {
            var scalar = rune.Value;
            first = first < 0 ? scalar : first;
            last = scalar;

            if (IsInRanges(scalar, UnassignedRanges))
            {
                throw new ArgumentException(
                    "The password contains a code point that was unassigned in Unicode 3.2.",
                    parameterName);
            }

            if (IsProhibited(scalar))
            {
                throw new ArgumentException("The password contains a prohibited Unicode character.", parameterName);
            }

            containsRandAl |= IsInRanges(scalar, RandAlRanges);
            containsL |= IsInRanges(scalar, LeftToRightRanges);
        }

        if (containsRandAl &&
            (containsL || !IsInRanges(first, RandAlRanges) || !IsInRanges(last, RandAlRanges)))
        {
            throw new ArgumentException(
                "A password containing right-to-left characters must start and end with a right-to-left character and must not contain left-to-right characters.",
                parameterName);
        }
    }

    private static bool IsNonAsciiSpace(int value) =>
        value is 0x00A0 or 0x1680 or >= 0x2000 and <= 0x200B or 0x202F or 0x205F or 0x3000;

    private static bool IsCommonlyMappedToNothing(int value) =>
        value is 0x00AD or 0x034F or 0x1806 or >= 0x180B and <= 0x180D or
            >= 0x200B and <= 0x200D or 0x2060 or >= 0xFE00 and <= 0xFE0F or 0xFEFF;

    private static bool IsProhibited(int value) =>
        IsNonAsciiSpace(value) ||
        value is >= 0x0000 and <= 0x001F or 0x007F or >= 0x0080 and <= 0x009F or
            0x06DD or 0x070F or 0x180E or >= 0x200C and <= 0x200D or
            >= 0x2028 and <= 0x2029 or >= 0x2060 and <= 0x2063 or
            >= 0x206A and <= 0x206F or 0xFEFF or >= 0xFFF9 and <= 0xFFFC or
            >= 0x1D173 and <= 0x1D17A ||
        value is >= 0xE000 and <= 0xF8FF or >= 0xF0000 and <= 0xFFFFD or
            >= 0x100000 and <= 0x10FFFD ||
        value is >= 0xFDD0 and <= 0xFDEF || (value & 0xFFFF) is 0xFFFE or 0xFFFF ||
        value is >= 0xD800 and <= 0xDFFF ||
        value is >= 0xFFF9 and <= 0xFFFD ||
        value is >= 0x2FF0 and <= 0x2FFB ||
        value is 0x0340 or 0x0341 or 0x200E or 0x200F or
            >= 0x202A and <= 0x202E or >= 0x206A and <= 0x206F ||
        value is 0xE0001 or >= 0xE0020 and <= 0xE007F;

    private static bool IsInRanges(int value, int[] ranges)
    {
        var low = 0;
        var high = ranges.Length / 2 - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var start = ranges[middle * 2];
            var end = ranges[(middle * 2) + 1];
            if (value < start)
            {
                high = middle - 1;
            }
            else if (value > end)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
