namespace PXA.FileImporter.ImageOcr;

public sealed class OcrNativeDependencyMissingException : InvalidOperationException
{
    public OcrNativeDependencyMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
