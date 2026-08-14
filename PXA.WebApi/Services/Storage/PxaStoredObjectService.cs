using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Observability;

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

        using var activity = PxaTelemetry.StartStorageOperation("put");
        var stopwatch = Stopwatch.StartNew();
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
                PxaTelemetry.CompleteStorageOperation(activity, "completed", length);
                PxaTelemetry.RecordStorageOperation("put", "completed", stopwatch.Elapsed, length);
                return entity;
            }
            catch
            {
                await storage.DeleteAsync(objectKey, CancellationToken.None);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            PxaTelemetry.CompleteStorageOperation(activity, "cancelled");
            PxaTelemetry.RecordStorageOperation("put", "cancelled", stopwatch.Elapsed);
            throw;
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("size limit", StringComparison.Ordinal))
        {
            PxaTelemetry.CompleteStorageOperation(activity, "rejected");
            PxaTelemetry.RecordStorageOperation("put", "rejected", stopwatch.Elapsed);
            throw;
        }
        catch
        {
            PxaTelemetry.CompleteStorageOperation(activity, "failed");
            PxaTelemetry.RecordStorageOperation("put", "failed", stopwatch.Elapsed);
            throw;
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
        using var activity = PxaTelemetry.StartStorageOperation("get");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var metadata = await dbContext.StoredObjects.AsNoTracking().SingleOrDefaultAsync(
                value => value.Id == id &&
                         value.OrganizationId == organizationId &&
                         value.Status == PxaStoredObjectStatus.Available,
                cancellationToken);
            if (metadata is null)
            {
                PxaTelemetry.CompleteStorageOperation(activity, "not_found");
                PxaTelemetry.RecordStorageOperation("get", "not_found", stopwatch.Elapsed);
                return null;
            }

            var content = await storage.OpenReadAsync(metadata.ObjectKey, cancellationToken);
            PxaTelemetry.CompleteStorageOperation(activity, "completed", metadata.Length);
            PxaTelemetry.RecordStorageOperation("get", "completed", stopwatch.Elapsed, metadata.Length);
            return (metadata, content);
        }
        catch (OperationCanceledException)
        {
            PxaTelemetry.CompleteStorageOperation(activity, "cancelled");
            PxaTelemetry.RecordStorageOperation("get", "cancelled", stopwatch.Elapsed);
            throw;
        }
        catch
        {
            PxaTelemetry.CompleteStorageOperation(activity, "failed");
            PxaTelemetry.RecordStorageOperation("get", "failed", stopwatch.Elapsed);
            throw;
        }
    }

    public Task<PxaStoredObject?> GetMetadataAsync(
        Guid id,
        Guid organizationId,
        string purpose,
        CancellationToken cancellationToken) =>
        dbContext.StoredObjects.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == id &&
                     value.OrganizationId == organizationId &&
                     value.Purpose == purpose &&
                     value.Status == PxaStoredObjectStatus.Available,
            cancellationToken);

    public async Task<(PxaStoredObject Metadata, Stream Content)?> OpenAsync(
        Guid id,
        Guid organizationId,
        string purpose,
        CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(id, organizationId, purpose, cancellationToken);
        if (metadata is null)
            return null;
        var content = await storage.OpenReadAsync(metadata.ObjectKey, cancellationToken);
        return (metadata, content);
    }

    public async Task DeleteAsync(Guid id, Guid organizationId, CancellationToken cancellationToken)
    {
        using var activity = PxaTelemetry.StartStorageOperation("delete");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var metadata = await dbContext.StoredObjects.SingleOrDefaultAsync(
                value => value.Id == id && value.OrganizationId == organizationId,
                cancellationToken);
            if (metadata is null)
            {
                PxaTelemetry.CompleteStorageOperation(activity, "not_found");
                PxaTelemetry.RecordStorageOperation("delete", "not_found", stopwatch.Elapsed);
                return;
            }
            if (metadata.Status == PxaStoredObjectStatus.Deleted)
            {
                PxaTelemetry.CompleteStorageOperation(activity, "skipped");
                PxaTelemetry.RecordStorageOperation("delete", "skipped", stopwatch.Elapsed);
                return;
            }

            await storage.DeleteAsync(metadata.ObjectKey, cancellationToken);
            metadata.Status = PxaStoredObjectStatus.Deleted;
            metadata.DeletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            PxaTelemetry.CompleteStorageOperation(activity, "completed");
            PxaTelemetry.RecordStorageOperation("delete", "completed", stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            PxaTelemetry.CompleteStorageOperation(activity, "cancelled");
            PxaTelemetry.RecordStorageOperation("delete", "cancelled", stopwatch.Elapsed);
            throw;
        }
        catch
        {
            PxaTelemetry.CompleteStorageOperation(activity, "failed");
            PxaTelemetry.RecordStorageOperation("delete", "failed", stopwatch.Elapsed);
            throw;
        }
    }

    public async Task<int> ReconcileMissingAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var activity = PxaTelemetry.StartStorageOperation("reconcile");
        var stopwatch = Stopwatch.StartNew();
        try
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
            PxaTelemetry.CompleteStorageOperation(
                activity,
                missing > 0 ? "missing_found" : "completed");
            PxaTelemetry.RecordStorageOperation(
                "reconcile",
                missing > 0 ? "missing_found" : "completed",
                stopwatch.Elapsed);
            return missing;
        }
        catch (OperationCanceledException)
        {
            PxaTelemetry.CompleteStorageOperation(activity, "cancelled");
            PxaTelemetry.RecordStorageOperation("reconcile", "cancelled", stopwatch.Elapsed);
            throw;
        }
        catch
        {
            PxaTelemetry.CompleteStorageOperation(activity, "failed");
            PxaTelemetry.RecordStorageOperation("reconcile", "failed", stopwatch.Elapsed);
            throw;
        }
    }

    private static string? NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;
        var normalized = Path.GetFileName(fileName.Trim());
        return normalized.Length <= 255 ? normalized : normalized[..255];
    }
}
