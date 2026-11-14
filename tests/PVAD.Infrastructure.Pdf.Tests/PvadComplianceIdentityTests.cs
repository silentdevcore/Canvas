using System.Buffers.Binary;
using System.Text;
using PVAD.Pdf;

namespace PVAD.Infrastructure.Pdf.Tests;

public sealed class PvadComplianceIdentityTests
{
    [Fact]
    public void GeneratedSrgbProfile_UsesFourBytePvadSignaturesAndValidTagBounds()
    {
        var profile = PdfComplianceResources.CreateSrgbIccProfile();
        Assert.Equal(profile, PdfComplianceResources.CreateSrgbIccProfile());
        Assert.Equal((uint)profile.Length, Read(profile, 0));
        Assert.Equal("PVAD", Text(profile, 4, 4));
        Assert.Equal("PVAD", Text(profile, 80, 4));
        Assert.Equal(0x02100000u, Read(profile, 8));
        Assert.Equal("RGB ", Text(profile, 16, 4));
        Assert.Equal("acsp", Text(profile, 36, 4));

        var count = checked((int)Read(profile, 128));
        Assert.Equal(9, count);
        var previousEnd = 132 + count * 12;
        for (var index = 0; index < count; index++)
        {
            var entry = 132 + index * 12;
            var offset = checked((int)Read(profile, entry + 4));
            var length = checked((int)Read(profile, entry + 8));
            Assert.Equal(0, offset % 4);
            Assert.True(offset >= previousEnd);
            Assert.InRange(length, 1, profile.Length - offset);
            previousEnd = offset + length;
            if (Text(profile, entry, 4) == "desc")
            {
                Assert.Equal("desc", Text(profile, offset, 4));
                var textLength = checked((int)Read(profile, offset + 8));
                Assert.Equal("PVAD sRGB IEC61966-2.1\0", Text(profile, offset + 12, textLength));
            }
        }
    }

    [Theory]
    [InlineData(PdfComplianceProfile.PdfA2B)]
    [InlineData(PdfComplianceProfile.PdfA3B)]
    public void NewArchivalOutput_UsesCanonicalMetadataWithoutChangingDiagnosticIds(PdfComplianceProfile profile)
    {
        var document = new PdfDocument();
        document.AddPage();
        var options = new PdfSaveOptions { ComplianceProfile = profile };
        var report = PdfCompliancePreflight.Validate(document, options);
        Assert.True(report.IsValid);
        Assert.StartsWith("PVAD ", report.Validator);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PVADPDFA000");
        var bytes = document.ToBytes(options);
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("PVAD sRGB output intent", text);
        Assert.DoesNotContain("PXA sRGB", text);
        using var parsed = UglyToad.PdfPig.PdfDocument.Open(bytes);
        Assert.Equal(1, parsed.NumberOfPages);
        Assert.Equal(PdfPageSizes.A4Width, parsed.GetPage(1).Width, 2);
    }

    [Fact]
    public void AccessibilityReport_UsesCanonicalNameAndRetainsStableDiagnostics()
    {
        var document = new PdfDocument();
        document.AddPage();
        var report = PdfAccessibilityPreflight.Validate(document, new PdfSaveOptions
        {
            AccessibilityProfile = PdfAccessibilityProfile.PdfUa1,
        });
        Assert.StartsWith("PVAD ", PdfAccessibilityPreflight.ValidatorName);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PVADPDFUA002");
    }

    [Theory]
    [InlineData(PdfComplianceProfile.PdfA2B, "list")]
    [InlineData(PdfComplianceProfile.PdfA3B, "list")]
    [InlineData(PdfComplianceProfile.PdfA2B, "radio")]
    [InlineData(PdfComplianceProfile.PdfA3B, "radio")]
    [InlineData(PdfComplianceProfile.PdfA2B, "signature")]
    [InlineData(PdfComplianceProfile.PdfA3B, "signature")]
    [InlineData(PdfComplianceProfile.PdfA2B, "button")]
    [InlineData(PdfComplianceProfile.PdfA3B, "button")]
    public void ArchivalPreflight_RejectsAllUnsupportedInteractiveWidgetFamilies(PdfComplianceProfile profile, string kind)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        switch (kind)
        {
            case "list": page.AddListBox("choice", 20, 650, 100, 50, ["PDF"], ["PDF"]); break;
            case "radio":
                page.AddRadioButton("choice", "Yes", 0, 20, 650, 14, 14, true);
                page.AddRadioButton("choice", "No", 1, 20, 630, 14, 14);
                break;
            case "signature": page.AddSignatureField("approval", 20, 650, 100, 24); break;
            case "button": page.AddPushButton("reset", "Reset", 20, 650, 100, 24, "reset"); break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
        var options = new PdfSaveOptions { ComplianceProfile = profile };
        var report = PdfCompliancePreflight.Validate(document, options);
        Assert.False(report.IsValid);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "PVADPDFA004");
        Assert.Throws<PdfComplianceException>(() => document.ToBytes(options));
        Assert.NotEmpty(document.ToBytes());
    }

    private static uint Read(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
    private static string Text(byte[] bytes, int offset, int length) => Encoding.ASCII.GetString(bytes, offset, length);
}
