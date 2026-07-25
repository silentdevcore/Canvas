using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;

namespace PXA.WebApi.Services.Storage;

public sealed class PxaStoredObjectService(
    PxaDbContext dbContext,
    IPxaObjectStorage storage,
    IOptions<PxaStorageOptions> options)
{
    private readonly PxaStorageOptions settings = options.Value;

    public async Task<PxaStoredObject> StoreAsync(
        Guid organizationId,
        Guid userId,
        string purpose,
        string contentType,
        string? fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"pxa-object-{Guid.NewGuid():N}.tmp");
        long length = 0;
        string checksum;
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var buffered = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var copyBuffer = new byte[81920];
                while (true)
                {
                    var read = await content.ReadAsync(copyBuffer, cancellationToken);
                    if (read == 0)
                        break;
                    if (length + read > settings.MaximumObjectBytes)
                        throw new InvalidOperationException("The stored object exceeds the configured size limit.");
                    await buffered.WriteAsync(copyBuffer.AsMemory(0, read), cancellationToken);
                    hash.AppendData(copyBuffer, 0, read);
                    length += read;
                }
            }
            checksum = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();

            var id = Guid.NewGuid();
            var objectKey = $"{organizationId:N}/{id:N}";
            await using (var buffered = new FileStream(
                             temporaryPath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await storage.PutAsync(objectKey, buffered, cancellationToken);
            }

            var entity = new PxaStoredObject
            {
                Id = id,
                OrganizationId = organizationId,
                CreatedByUserId = userId,
                ObjectKey = objectKey,
                Purpose = purpose.Trim(),
                ContentType = contentType.Trim(),
                FileName = NormalizeFileName(fileName),
                Length = length,
                Checksum = checksum,
            };
            dbContext.StoredObjects.Add(entity);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return entity;
            }
            catch
            {
                await storage.DeleteAsync(objectKey, CancellationToken.None);
                throw;
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task<(PxaStoredObject Metadata, Stream Content)?> OpenAsync(
        Guid id,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var metadata = await dbContext.StoredObjects.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == id &&
                     value.OrganizationId == organizationId &&
                     value.Status == PxaStoredObjectStatus.Available,
            cancellationToken);
        if (metadata is null)
            return null;
        return (metadata, await storage.OpenReadAsync(metadata.ObjectKey, cancellationToken));
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        var metadata = await dbContext.StoredObjects.SingleOrDefaultAsync(
            value => value.Id == id && value.OrganizationId == organizationId,
            cancellationToken);
        if (metadata is null || metadata.Status == PxaStoredObjectStatus.Deleted)
            return;
        await storage.DeleteAsync(metadata.ObjectKey, cancellationToken);
        metadata.Status = PxaStoredObjectStatus.Deleted;
        metadata.DeletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ReconcileMissingAsync(int batchSize, CancellationToken cancellationToken)
    {
        var candidates = await dbContext.StoredObjects
            .Where(value => value.Status == PxaStoredObjectStatus.Available)
            .OrderBy(value => EF.Functions.Random())
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
        var missing = 0;
        foreach (var metadata in candidates)
        {
            if (await storage.ExistsAsync(metadata.ObjectKey, cancellationToken))
                continue;
            metadata.Status = PxaStoredObjectStatus.Orphaned;
            missing++;
        }
        if (missing > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return missing;
    }

    private static string? NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        var normalized = Path.GetFileName(fileName.Trim());
        return normalized.Length <= 255 ? normalized : normalized[..255];
    }
}
