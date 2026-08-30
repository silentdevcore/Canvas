using PXA.Importer.Objects;

namespace PXA.Importer.Security;

internal interface IPdfDocumentSecurityHandler : IDisposable
{
    bool ShouldDecryptString { get; }

    bool ShouldDecryptStream(PdfDictionary dictionary);

    byte[] DecryptValue(ReadOnlySpan<byte> ciphertext, int objectNumber, int generation);
}
