namespace PXA.Core.Abstractions;

public interface IExporterCapabilities
{
    bool SupportsMultiPage { get; }
    bool SupportsImages { get; }
    bool SupportsRichText { get; }
    bool SupportsFormFields { get; }
}

public record ExporterCapabilities(
    bool SupportsMultiPage = true,
    bool SupportsImages = true,
    bool SupportsRichText = true,
    bool SupportsFormFields = true) : IExporterCapabilities;
