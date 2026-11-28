namespace PVAD.Pdf.Layout;

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = false)]
internal sealed record ImageElement(
    PVAD.Pdf.PdfImageData Image,
    string CacheKey,
    double X,
    double Y,
    double Width,
    double Height,
    double Opacity = 1,
    bool ClipToBounds = false,
    double? ClipX = null,
    double? ClipY = null,
    double? ClipWidth = null,
    double? ClipHeight = null) : PdfPageElement;
