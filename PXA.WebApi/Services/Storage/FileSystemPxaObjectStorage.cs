using Microsoft.Extensions.Options;

namespace PXA.WebApi.Services.Storage;

public sealed class FileSystemPxaObjectStorage : IPxaObjectStorage
{
    private readonly string rootPath;

    public FileSystemPxaObjectStorage(
        IOptions<PxaStorageOptions> options,
        IWebHostEnvironment environment)
    {
        var configuredPath = options.Value.RootPath;
        rootPath = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath));
        Directory.CreateDirectory(rootPath);
    }

    public async Task PutAsync(string objectKey, Stream content, CancellationToken cancellationToken)
    {
        var path = ResolvePath(objectKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var output = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await content.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = new FileStream(
            ResolvePath(objectKey),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = ResolvePath(objectKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(ResolvePath(objectKey)));
    }

    private string ResolvePath(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) ||
            objectKey.Contains('\\', StringComparison.Ordinal) ||
            objectKey.Split('/').Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')))
        {
            throw new ArgumentException("The object key is invalid.", nameof(objectKey));
        }

        var path = Path.GetFullPath(Path.Combine(rootPath, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith($"{rootPath}{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new ArgumentException("The object key escapes the storage root.", nameof(objectKey));
        return path;
    }
}
