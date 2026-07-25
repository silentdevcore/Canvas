namespace PXA.WebApi.Services.Storage;

public sealed class PxaStorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "FileSystem";
    public string RootPath { get; set; } = "App_Data/objects";
    public long MaximumObjectBytes { get; set; } = 100 * 1024 * 1024;
}
