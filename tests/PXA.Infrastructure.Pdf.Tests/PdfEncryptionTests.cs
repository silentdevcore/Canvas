using System.Text;
using PXA.Pdf;
using PXA.Pdf.Serialization.Security;
using PigDocument = UglyToad.PdfPig.PdfDocument;
using PigParsingOptions = UglyToad.PdfPig.ParsingOptions;

namespace PXA.Infrastructure.Pdf.Tests;

public sealed class PdfEncryptionTests
{
    [Fact]
    public void Rc4_MatchesKnownAnswerVector()
    {
        // Classic RC4 test vector: key "Key", plaintext "Plaintext".
        var cipher = Rc4.Transform(Encoding.ASCII.GetBytes("Key"), Encoding.ASCII.GetBytes("Plaintext"));

        Assert.Equal("BBF316E8D940AF0AD3", Convert.ToHexString(cipher));
    }

    [Fact]
    public void Rc4_IsSymmetric_RoundTrips()
    {
        var key = Encoding.ASCII.GetBytes("a-secret-key");
        var data = Encoding.ASCII.GetBytes("the quick brown fox jumps over the lazy dog");

        var roundTripped = Rc4.Transform(key, Rc4.Transform(key, data));

        Assert.Equal(data, roundTripped);
    }

    [Fact]
    public void Save_WithEncryption_EmitsStandardEncryptDictionaryAndId()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("content", 40, 40);

        var bytes = document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions { UserPassword = "open" }
        });

        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("/Encrypt", text);
        Assert.Contains("/Filter /Standard", text);
        Assert.Contains("/V 2", text);
        Assert.Contains("/R 3", text);
        Assert.Contains("/Length 128", text);
        Assert.Contains("/ID [<", text);
    }

    [Fact]
    public void Save_WithAllPermissions_WritesExpectedPermissionBits()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("content", 40, 40);

        var bytes = document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions
            {
                UserPassword = "open",
                Permissions = PdfPermissions.All
            }
        });

        // All permissions granted with revision 3 → /P == -4 (only the two low reserved bits cleared).
        Assert.Contains("/P -4", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void Save_WithEncryption_HidesPlaintextStrings()
    {
        var document = new PdfDocument();
        document.Info.Title = "TopSecretTitleMarker";
        document.AddPage().DrawText("VisibleBodyMarker", 40, 40);

        var encrypted = document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions { UserPassword = "open" }
        });
        var plaintext = Encoding.ASCII.GetString(encrypted);

        // The Info title (a string) and the page text (in a stream) must not survive as plaintext.
        Assert.DoesNotContain("TopSecretTitleMarker", plaintext);
        Assert.DoesNotContain("VisibleBodyMarker", plaintext);
    }

    [Fact]
    public void Save_WithoutEncryption_HasNoEncryptDictionary()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("content", 40, 40);

        var bytes = document.ToBytes();

        var text = Encoding.ASCII.GetString(bytes);
        Assert.DoesNotContain("/Encrypt", text);
        Assert.DoesNotContain("/ID [<", text);
    }

    [Fact]
    public void Save_WithAes128_ThrowsNotSupported()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("content", 40, 40);

        var exception = Assert.Throws<NotSupportedException>(() => document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions
            {
                UserPassword = "open",
                Algorithm = PdfEncryptionAlgorithm.Aes128
            }
        }));

        Assert.Contains("AES-128", exception.Message);
    }

    [Fact]
    public void EncryptedDocument_OpensAndDecryptsWithUserPassword()
    {
        const string password = "correct horse battery staple";
        var document = new PdfDocument();
        document.AddPage().DrawText("HelloEncryptedWorld", 40, 700);

        var bytes = document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions { UserPassword = password }
        });

        // Independent reader (PdfPig) must decrypt with the password and recover the text.
        using var pig = PigDocument.Open(bytes, new PigParsingOptions { Password = password });
        var pageText = pig.GetPage(1).Text;

        Assert.Contains("HelloEncryptedWorld", pageText);
    }

    [Fact]
    public void EncryptedDocument_WithOwnerPassword_OpensWithUserPassword()
    {
        var document = new PdfDocument();
        document.AddPage().DrawText("OwnerProtected", 40, 700);

        var bytes = document.ToBytes(new PdfSaveOptions
        {
            Encryption = new PdfEncryptionOptions
            {
                UserPassword = "user",
                OwnerPassword = "owner",
                Permissions = PdfPermissions.Print
            }
        });

        using var pig = PigDocument.Open(bytes, new PigParsingOptions { Password = "user" });
        Assert.Contains("OwnerProtected", pig.GetPage(1).Text);
    }
}
