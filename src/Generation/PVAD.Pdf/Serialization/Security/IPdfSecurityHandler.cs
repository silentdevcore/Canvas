namespace PVAD.Pdf.Serialization.Security;

internal interface IPdfSecurityHandler : IDisposable
{
    byte[] DocumentId { get; }

    string PdfVersion { get; }

    byte[] EncryptObjectBody(int objectNumber, int generation, byte[] body);

    byte[] BuildEncryptDictionary();
}
