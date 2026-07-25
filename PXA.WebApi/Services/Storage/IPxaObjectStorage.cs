namespace PXA.WebApi.Services.Storage;

public interface IPxaObjectStorage
{
    Task PutAsync(string objectKey, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}
