using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Mail;

namespace PVAD.Api.Tests;

public sealed class PvadDataProtectionCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Relocated_keyring_backup_restores_both_generations_but_empty_keyring_cannot(bool mail)
    {
        var root = Directory.CreateTempSubdirectory("pvad-keyring-recovery-test-");
        try
        {
            var source = root.CreateSubdirectory("source");
            var backup = root.CreateSubdirectory("backup");
            var restored = root.CreateSubdirectory("restored");
            var missing = root.CreateSubdirectory("missing");
            var provider = DataProtectionProvider.Create(source, options => options.SetApplicationName(PvadDataProtection.ApplicationName));
            var legacyPurpose = mail ? PvadDataProtection.LegacyMailPayloadPurpose : PvadDataProtection.LegacyRevocationEvidencePurpose;
            var legacy = provider.CreateProtector(legacyPurpose).Protect("synthetic retained payload");
            var currentReader = mail ? PvadDataProtection.MailPayload(provider) : PvadDataProtection.RevocationEvidence(provider);
            var current = currentReader.Protect("synthetic current payload");
            var keys = source.GetFiles("key-*.xml");
            Assert.NotEmpty(keys);
            foreach (var key in keys)
            {
                var saved = key.CopyTo(Path.Combine(backup.FullName, key.Name));
                saved.CopyTo(Path.Combine(restored.FullName, saved.Name));
                Assert.Equal(File.ReadAllBytes(key.FullName), File.ReadAllBytes(Path.Combine(restored.FullName, key.Name)));
            }
            source.Delete(recursive: true);
            var restarted = DataProtectionProvider.Create(restored, options => options.SetApplicationName(PvadDataProtection.ApplicationName));
            var restoredReader = mail ? PvadDataProtection.MailPayload(restarted) : PvadDataProtection.RevocationEvidence(restarted);
            Assert.Equal("synthetic retained payload", restoredReader.Unprotect(legacy));
            Assert.Equal("synthetic current payload", restoredReader.Unprotect(current));
            var emptyProvider = DataProtectionProvider.Create(missing, options => options.SetApplicationName(PvadDataProtection.ApplicationName));
            var emptyReader = mail ? PvadDataProtection.MailPayload(emptyProvider) : PvadDataProtection.RevocationEvidence(emptyProvider);
            Assert.Throws<CryptographicException>(() => emptyReader.Unprotect(legacy));
            Assert.Throws<CryptographicException>(() => emptyReader.Unprotect(current));
        }
        finally { root.Delete(recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Existing_keyring_remains_readable_after_provider_restart_without_changing_application_identity(bool mail)
    {
        var directory = Directory.CreateTempSubdirectory("pvad-keyring-test-");
        try
        {
            var oldProvider = DataProtectionProvider.Create(directory, options => options.SetApplicationName("PowerDoxAutomation"));
            var legacyPurpose = mail ? PvadDataProtection.LegacyMailPayloadPurpose : PvadDataProtection.LegacyRevocationEvidencePurpose;
            var original = oldProvider.CreateProtector(legacyPurpose).Protect("synthetic retained payload");
            var restarted = DataProtectionProvider.Create(directory, options => options.SetApplicationName(PvadDataProtection.ApplicationName));
            var reader = mail ? PvadDataProtection.MailPayload(restarted) : PvadDataProtection.RevocationEvidence(restarted);
            Assert.Equal("synthetic retained payload", reader.Unprotect(original));
            var current = reader.Protect("synthetic current payload");
            var nextRestart = DataProtectionProvider.Create(directory, options => options.SetApplicationName(PvadDataProtection.ApplicationName));
            var nextReader = mail ? PvadDataProtection.MailPayload(nextRestart) : PvadDataProtection.RevocationEvidence(nextRestart);
            Assert.Equal("synthetic current payload", nextReader.Unprotect(current));
            var unrelated = DataProtectionProvider.Create(directory, options => options.SetApplicationName("UnrelatedSyntheticApplication"));
            var unrelatedReader = mail ? PvadDataProtection.MailPayload(unrelated) : PvadDataProtection.RevocationEvidence(unrelated);
            Assert.Throws<CryptographicException>(() => unrelatedReader.Unprotect(original));
            Assert.Throws<CryptographicException>(() => unrelatedReader.Unprotect(current));
        }
        finally { directory.Delete(recursive: true); }
    }

    [Fact]
    public async Task Mail_queue_and_processor_deliver_both_generations_without_rewriting_legacy_ciphertext()
    {
        await using var db = new PvadDbContext(new DbContextOptionsBuilder<PvadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var provider = new EphemeralDataProtectionProvider();
        var legacy = new MailOutboxMessage
        {
            RecipientEmail = "synthetic@example.invalid", TemplateKey = "identity.password-changed",
            IdempotencyKey = "legacy-synthetic",
            ProtectedPayload = provider.CreateProtector(PvadDataProtection.LegacyMailPayloadPurpose)
                .Protect("{\"displayName\":\"Synthetic legacy\"}"),
        };
        db.MailOutboxMessages.Add(legacy);
        var current = new PvadMailQueue(db, provider).Enqueue(null, null, "synthetic@example.invalid",
            "identity.password-changed", new { displayName = "Synthetic current" }, "current-synthetic");
        var originalCiphertext = legacy.ProtectedPayload;
        Assert.Contains("Synthetic current", provider.CreateProtector(PvadDataProtection.MailPayloadPurpose)
            .Unprotect(current.ProtectedPayload));
        await db.SaveChangesAsync();
        var transport = new DevelopmentMailTransport();
        var options = Options.Create(new PvadMailOptions { Enabled = true, Transport = "Development" });
        var processor = new PvadMailProcessor(db, transport, provider, options, new PvadMailTemplateRenderer(options));
        Assert.Equal(2, await processor.ProcessPendingAsync(CancellationToken.None));
        Assert.Equal(2, transport.Messages.Count);
        Assert.Equal(MailDeliveryStatus.Delivered, legacy.Status);
        Assert.Equal(MailDeliveryStatus.Delivered, current.Status);
        Assert.Equal(originalCiphertext, legacy.ProtectedPayload);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reads_old_payloads_writes_new_payloads_and_rejects_tampering(bool mail)
    {
        var provider = new EphemeralDataProtectionProvider();
        var canonicalPurpose = mail ? PvadDataProtection.MailPayloadPurpose : PvadDataProtection.RevocationEvidencePurpose;
        var legacyPurpose = mail ? PvadDataProtection.LegacyMailPayloadPurpose : PvadDataProtection.LegacyRevocationEvidencePurpose;
        var reader = mail ? PvadDataProtection.MailPayload(provider) : PvadDataProtection.RevocationEvidence(provider);
        var original = provider.CreateProtector(legacyPurpose).Protect("synthetic payload");
        Assert.Equal("synthetic payload", reader.Unprotect(original));
        var current = reader.Protect("new synthetic payload");
        Assert.Equal("new synthetic payload", provider.CreateProtector(canonicalPurpose).Unprotect(current));
        Assert.Throws<CryptographicException>(() => provider.CreateProtector(legacyPurpose).Unprotect(current));
        var corrupted = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(current);
        corrupted[^1] ^= 1;
        Assert.Throws<CryptographicException>(() => reader.Unprotect(corrupted));
        var corruptedLegacy = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(original);
        corruptedLegacy[^1] ^= 1;
        Assert.Throws<CryptographicException>(() => reader.Unprotect(corruptedLegacy));
    }

    [Fact]
    public void Purpose_boundaries_remain_isolated_including_child_protectors()
    {
        var provider = new EphemeralDataProtectionProvider();
        var mail = PvadDataProtection.MailPayload(provider);
        var evidence = PvadDataProtection.RevocationEvidence(provider);
        Assert.Throws<CryptographicException>(() => evidence.Unprotect(mail.Protect("synthetic")));
        var child = mail.CreateProtector("child");
        var legacyChild = provider.CreateProtector(PvadDataProtection.LegacyMailPayloadPurpose).CreateProtector("child");
        Assert.Equal("synthetic", child.Unprotect(legacyChild.Protect("synthetic")));
        Assert.Throws<CryptographicException>(() => mail.Unprotect(child.Protect("synthetic")));
    }
}
