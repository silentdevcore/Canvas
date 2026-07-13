using System.Text.Json;
using PXA.FileImporter.ImageOcr;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
};

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: PXA.FileImporter.ImageOcr.Worker <request.json> <response.json>");
    return 2;
}

var requestPath = args[0];
var responsePath = args[1];

try
{
    var requestJson = await File.ReadAllTextAsync(requestPath);
    var request = JsonSerializer.Deserialize<OcrWorkerRequest>(requestJson, jsonOptions)
        ?? throw new InvalidOperationException("OCR worker request is invalid.");

    var engine = new EmbeddedTesseractOcrEngine(request.TessDataPath, request.NativeLibraryPath);
    var pages = request.Pages
        .Select(page => new OcrImagePage(
            page.PageIndex,
            page.WidthPx,
            page.HeightPx,
            File.ReadAllBytes(page.EncodedImagePath)))
        .ToArray();

    var options = new ImageToPdfConversionOptions
    {
        Languages = request.Languages,
        NativeLibraryPath = request.NativeLibraryPath,
        MaxOcrRuntimeSeconds = request.MaxOcrRuntimeSeconds,
    };

    var ocrPages = await engine.RecognizeAsync(pages, options);
    await WriteResponseAsync(new OcrWorkerResponse
    {
        Success = true,
        Pages = ocrPages,
    });

    return 0;
}
catch (Exception ex)
{
    await WriteResponseAsync(new OcrWorkerResponse
    {
        Success = false,
        Error = ex.Message,
    });
    Console.Error.WriteLine(ex);
    return 1;
}

async Task WriteResponseAsync(OcrWorkerResponse response)
{
    var directory = Path.GetDirectoryName(responsePath);
    if (!string.IsNullOrWhiteSpace(directory))
        Directory.CreateDirectory(directory);

    await File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(response, jsonOptions));
}
