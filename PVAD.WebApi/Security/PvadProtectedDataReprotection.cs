using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Services.Pdf;

namespace PVAD.WebApi.Security;

public sealed record PvadReprotectionReport(
    bool DryRun,
    int MailCurrent,
    int MailConverted,
    int MailUnreadable,
    int CacheCurrent,
    int CacheConverted,
    int CacheUnreadable)
{
    /// <summary>A dry run found payloads that still use the former protection identity.</summary>
    public bool RequiresConversion => DryRun && (MailConverted > 0 || CacheConverted > 0);

    /// <summary>Reasons that make normal activation unsafe; empty when every payload is readable canonically.</summary>
    public IReadOnlyList<string> ActivationBlockers =>
    [
        .. RequiresConversion ? [PvadActivationBlocker.ConversionRequired] : Array.Empty<string>(),
        .. MailUnreadable > 0 ? [PvadActivationBlocker.MailUnreadable] : Array.Empty<string>(),
        .. CacheUnreadable > 0 ? [PvadActivationBlocker.RevocationCacheUnreadable] : Array.Empty<string>(),
    ];
}

public static class PvadActivationBlocker
{
    public const string ConversionRequired = "conversion-required";
    public const string MailUnreadable = "mail-unreadable";
    public const string RevocationCacheUnreadable = "revocation-cache-unreadable";
}

/// <summary>
/// Offline conversion of payloads protected before the 2026-11-09 hard cut. It reads with the
/// former application discriminator and both historical purposes, writes with the current
/// identity and skips payloads the current identity can already read. Plaintext is never logged.
/// </summary>
public sealed class PvadProtectedDataReprotection(
    PvadDbContext dbContext,
    IDataProtectionProvider currentProvider,
    IOptions<PvadPdfRevocationEvidenceOptions> revocationOptions,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<PvadProtectedDataReprotection> logger)
{
    // The former discriminator and purposes exist only to read data written before the hard cut.
    public const string FormerApplicationName = "PowerDoxAutomation";
    private static readonly string[] FormerMailPurposes =
        [PvadDataProtection.MailPayloadPurpose, "PXA.Mail.Outbox.Payload.v1"];
    private static readonly string[] FormerRevocationPurposes =
        [PvadDataProtection.RevocationEvidencePurpose, "PXA.PdfTrust.RevocationEvidence.v1"];
    private const int BatchSize = 100;

    /// <summary>
    /// Refuses activation before background consumers start while stored payloads still need the
    /// former identity or cannot be read at all; unreadable mail would otherwise be retried into Dead Letter.
    /// </summary>
    public static async Task EnsureActivationSafeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<PvadProtectedDataReprotection>()
            .RunAsync(dryRun: true, cancellationToken);
        var blockers = report.ActivationBlockers;
        if (blockers.Count == 0) return;
        var reasons = new List<string>();
        if (blockers.Contains(PvadActivationBlocker.ConversionRequired))
            reasons.Add($"{report.MailConverted + report.CacheConverted} entries still use the former Data Protection identity; " +
                "stop delivery, back up the database and keyring, then run the WebApi once with --reprotect-protected-data");
        if (blockers.Contains(PvadActivationBlocker.MailUnreadable))
            reasons.Add($"{report.MailUnreadable} mail outbox payloads are unreadable with the configured keyring; restore the missing key from backup");
        if (blockers.Contains(PvadActivationBlocker.RevocationCacheUnreadable))
            reasons.Add($"{report.CacheUnreadable} revocation cache entries are unreadable with the configured keyring; restore the missing key from backup");
        throw new InvalidOperationException(
            $"Activation refused ({string.Join(", ", blockers)}): {string.Join("; ", reasons)}. " +
            "Entry IDs are logged by a --reprotect-protected-data --dry-run run.");
    }

    public async Task<PvadReprotectionReport> RunAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        var keysPath = PvadDataProtection.KeysPath(configuration, environment);
        var former = DataProtectionProvider.Create(new DirectoryInfo(keysPath),
            options => options.SetApplicationName(FormerApplicationName));

        var mail = await ConvertMailAsync(dryRun, PvadDataProtection.MailPayload(currentProvider),
            FormerMailPurposes.Select(purpose => former.CreateProtector(purpose)).ToArray(), cancellationToken);
        var cache = await ConvertCacheAsync(dryRun, PvadDataProtection.RevocationEvidence(currentProvider),
            FormerRevocationPurposes.Select(purpose => former.CreateProtector(purpose)).ToArray(), cancellationToken);
        var report = new PvadReprotectionReport(dryRun, mail.Current, mail.Converted, mail.Unreadable,
            cache.Current, cache.Converted, cache.Unreadable);
        logger.LogInformation(
            "Protected data reprotection {Mode}: mail current {MailCurrent}, converted {MailConverted}, unreadable {MailUnreadable}; " +
            "revocation cache current {CacheCurrent}, converted {CacheConverted}, unreadable {CacheUnreadable}.",
            dryRun ? "dry run" : "run", report.MailCurrent, report.MailConverted, report.MailUnreadable,
            report.CacheCurrent, report.CacheConverted, report.CacheUnreadable);
        return report;
    }

    private async Task<(int Current, int Converted, int Unreadable)> ConvertMailAsync(
        bool dryRun, IDataProtector current, IDataProtector[] former, CancellationToken cancellationToken)
    {
        int currentCount = 0, converted = 0, unreadable = 0;
        Guid? after = null;
        while (true)
        {
            var batch = await dbContext.MailOutboxMessages
                .Where(message => after == null || message.Id.CompareTo(after.Value) > 0)
                .OrderBy(message => message.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
            if (batch.Count == 0) break;
            foreach (var message in batch)
            {
                switch (Classify(message.ProtectedPayload, current, former, out var plaintext))
                {
                    case Payload.Current: currentCount++; break;
                    case Payload.Former:
                        converted++;
                        if (!dryRun) message.ProtectedPayload = Reprotect(current, plaintext!);
                        break;
                    default:
                        unreadable++;
                        logger.LogWarning("Mail outbox payload {MessageId} is unreadable with current and former keys.", message.Id);
                        break;
                }
            }
            if (!dryRun) await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            after = batch[^1].Id;
        }
        return (currentCount, converted, unreadable);
    }

    private async Task<(int Current, int Converted, int Unreadable)> ConvertCacheAsync(
        bool dryRun, IDataProtector current, IDataProtector[] former, CancellationToken cancellationToken)
    {
        int currentCount = 0, converted = 0, unreadable = 0;
        var directory = PvadPdfRevocationEvidenceProvider.ResolveCacheDirectory(revocationOptions.Value, environment);
        if (!Directory.Exists(directory)) return (0, 0, 0);
        foreach (var path in Directory.EnumerateFiles(directory, "*.bin").Order(StringComparer.Ordinal))
        {
            var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            switch (Classify(protectedBytes, current, former, out var plaintext))
            {
                case Payload.Current: currentCount++; break;
                case Payload.Former:
                    converted++;
                    if (dryRun) break;
                    var replacement = current.Protect(plaintext!);
                    if (!current.Unprotect(replacement).AsSpan().SequenceEqual(plaintext))
                        throw new InvalidOperationException("Reprotected revocation evidence failed verification.");
                    var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
                    try
                    {
                        await File.WriteAllBytesAsync(temporary, replacement, cancellationToken);
                        File.Move(temporary, path, overwrite: true);
                    }
                    finally
                    {
                        if (File.Exists(temporary)) File.Delete(temporary);
                    }
                    break;
                default:
                    unreadable++;
                    logger.LogWarning("Revocation cache entry {CacheFile} is unreadable with current and former keys.", Path.GetFileName(path));
                    break;
            }
        }
        return (currentCount, converted, unreadable);
    }

    private static string Reprotect(IDataProtector current, string plaintext)
    {
        var replacement = current.Protect(plaintext);
        if (!string.Equals(current.Unprotect(replacement), plaintext, StringComparison.Ordinal))
            throw new InvalidOperationException("Reprotected mail payload failed verification.");
        return replacement;
    }

    private enum Payload { Current, Former, Unreadable }

    private static Payload Classify(string value, IDataProtector current, IDataProtector[] former, out string? plaintext)
    {
        plaintext = null;
        if (TryUnprotect(current, value, out _)) return Payload.Current;
        foreach (var protector in former)
            if (TryUnprotect(protector, value, out plaintext)) return Payload.Former;
        return Payload.Unreadable;
    }

    private static Payload Classify(byte[] value, IDataProtector current, IDataProtector[] former, out byte[]? plaintext)
    {
        plaintext = null;
        if (TryUnprotect(current, value, out _)) return Payload.Current;
        foreach (var protector in former)
            if (TryUnprotect(protector, value, out plaintext)) return Payload.Former;
        return Payload.Unreadable;
    }

    private static bool TryUnprotect(IDataProtector protector, string value, out string? plaintext)
    {
        try { plaintext = protector.Unprotect(value); return true; }
        catch (CryptographicException) { plaintext = null; return false; }
    }

    private static bool TryUnprotect(IDataProtector protector, byte[] value, out byte[]? plaintext)
    {
        try { plaintext = protector.Unprotect(value); return true; }
        catch (CryptographicException) { plaintext = null; return false; }
    }
}
