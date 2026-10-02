using System.Security.Cryptography;
using PXA.Core.Contracts;
using PXA.Core.ProjectFormats;

namespace PXA.WebApi.Infrastructure;

internal static class PxaProjectAssetMapper
{
    private const int MaximumAssetBytes = 20 * 1024 * 1024;

    public static IReadOnlyCollection<PxaProjectAsset> ExtractPdfAssets(DesignExportDto document)
    {
        var assets = new Dictionary<string, PxaProjectAsset>(StringComparer.Ordinal);
        foreach (var element in document.Pages.SelectMany(page => page.Elements).Concat(document.SharedElements))
        {
            if (!string.Equals(element.Type, "image", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(element.Content))
            {
                if (!string.IsNullOrWhiteSpace(element.AssetId))
                    throw Error("Portable projects cannot reference tenant-owned PDF assets.");
                continue;
            }

            var decoded = DecodeImageDataUrl(element.Content);
            var id = AssetId(decoded.Content);
            assets.TryAdd(id, new PxaProjectAsset(id, $"{id}{Extension(decoded.ContentType)}", decoded.ContentType, decoded.Content));
            element.AssetId = id;
            element.Content = null;
        }
        return assets.Values.OrderBy(asset => asset.Id, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyCollection<PxaProjectAsset> ExtractSpreadsheetAssets(SpreadsheetDto workbook)
    {
        var assets = new Dictionary<string, PxaProjectAsset>(StringComparer.Ordinal);
        foreach (var image in workbook.Sheets.SelectMany(sheet => sheet.Images))
        {
            if (string.IsNullOrWhiteSpace(image.Data))
            {
                if (image.AssetId.HasValue)
                    throw Error("Portable projects cannot reference tenant-owned Spreadsheet assets.");
                continue;
            }

            var decoded = DecodeImageDataUrl(image.Data);
            var id = DeterministicGuid(decoded.Content).ToString("N");
            assets.TryAdd(id, new PxaProjectAsset(id, $"{id}{Extension(decoded.ContentType)}", decoded.ContentType, decoded.Content));
            image.AssetId = Guid.ParseExact(id, "N");
            image.FileName = $"{id}{Extension(decoded.ContentType)}";
            image.ContentType = decoded.ContentType;
            image.Data = null;
        }
        return assets.Values.OrderBy(asset => asset.Id, StringComparer.Ordinal).ToArray();
    }

    public static void HydratePdfAssets(PxaProjectPackage<DesignExportDto> package)
    {
        var assets = package.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        foreach (var element in package.Document.Pages.SelectMany(page => page.Elements).Concat(package.Document.SharedElements))
        {
            if (string.IsNullOrWhiteSpace(element.AssetId))
                continue;
            if (!assets.TryGetValue(element.AssetId, out var asset))
                throw MissingAsset();
            element.Content = DataUrl(asset);
        }
    }

    public static void HydrateSpreadsheetAssets(PxaProjectPackage<SpreadsheetDto> package)
    {
        var assets = package.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
        foreach (var image in package.Document.Sheets.SelectMany(sheet => sheet.Images))
        {
            if (!image.AssetId.HasValue)
                continue;
            if (!assets.TryGetValue(image.AssetId.Value.ToString("N"), out var asset))
                throw MissingAsset();
            image.Data = DataUrl(asset);
            image.FileName = asset.FileName;
            image.ContentType = asset.ContentType;
            image.AssetId = null;
        }
    }

    private static DecodedAsset DecodeImageDataUrl(string value)
    {
        var separator = value.IndexOf(',');
        if (separator <= 5 || !value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            throw Error("Project images must be embedded PNG or JPEG data URLs.");

        var header = value[5..separator].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var contentType = header[0].ToLowerInvariant();
        if (contentType is not "image/png" and not "image/jpeg" ||
            !header.Skip(1).Any(part => part.Equals("base64", StringComparison.OrdinalIgnoreCase)))
            throw Error("Project images must be embedded PNG or JPEG Base64 data URLs.");

        try
        {
            var content = Convert.FromBase64String(value[(separator + 1)..]);
            if (content.Length is 0 or > MaximumAssetBytes)
                throw new PxaProjectFormatException("PXAPROJECT005", "A project image exceeds its size limit.");
            return new DecodedAsset(contentType, content);
        }
        catch (FormatException)
        {
            throw Error("A project image contains invalid Base64 data.");
        }
    }

    private static string AssetId(byte[] content) =>
        $"image-{Convert.ToHexStringLower(SHA256.HashData(content))[..24]}";

    private static Guid DeterministicGuid(byte[] content)
    {
        var bytes = SHA256.HashData(content)[..16];
        return new Guid(bytes);
    }

    private static string DataUrl(PxaProjectAsset asset) =>
        $"data:{asset.ContentType};base64,{Convert.ToBase64String(asset.Content)}";

    private static string Extension(string contentType) => contentType == "image/jpeg" ? ".jpg" : ".png";

    private static PxaProjectFormatException Error(string message) =>
        new("PXAPROJECT010", message);

    private static PxaProjectFormatException MissingAsset() =>
        new("PXAPROJECT008", "The project document references a missing package asset.");

    private sealed record DecodedAsset(string ContentType, byte[] Content);
}
