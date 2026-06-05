namespace Canvas.FileImporter.ImageOcr;

public sealed class OcrLanguageDataMissingException : InvalidOperationException
{
    public OcrLanguageDataMissingException(string message) : base(message)
    {
    }
}
