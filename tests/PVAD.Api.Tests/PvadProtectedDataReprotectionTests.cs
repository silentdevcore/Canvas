using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PVAD.Domain.Entities;
using PVAD.Infrastructure.Persistence;
using PVAD.WebApi.Security;
using PVAD.WebApi.Services.Pdf;

namespace PVAD.Api.Tests;

public sealed class PvadProtectedDataReprotectionTests : IDisposable
{
    private const string FormerMailPurpose = "PXA.Mail.Outbox.Payload.v1";
    private const string FormerRevocationPurpose = "PXA.PdfTrust.RevocationEvidence.v1";
    private readonly string root = Directory.CreateTempSubdirectory("pvad-reprotection-").FullName;
    private string KeysPath => Path.Combine(root, "keys");
    private string CachePath => Path.Combine(root, "cache");

    [Fact]
    public async Task Former_payloads_are_reprotected_once_and_remain_readable_by_current_consumers()
    {
        var former = Provider(KeysPath, PvadProtectedDataReprotection.FormerApplicationName);
        var current = Provider(KeysPath, PvadDataProtection.ApplicationName);
        var foreign = Provider(Path.Combine(root, "foreign-keys"), PvadProtectedDataReprotection.FormerApplicationName);
        var options = new DbContextOptionsBuilder<PvadDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var payloads = new Dictionary<Guid, string>();
        await using (var context = new PvadDbContext(options))
        {
            foreach (var (protector, plaintext) in new[]
            {
                (former.CreateProtector(PvadDataProtection.MailPayloadPurpose), "{\"subject\":\"former current purpose\"}"),
                (former.CreateProtector(FormerMailPurpose), "{\"subject\":\"former historical purpose\"}"),
                (PvadDataProtection.MailPayload(current), "{\"subject\":\"already canonical\"}"),
                (foreign.CreateProtector(PvadDataProtection.MailPayloadPurpose), "{\"subject\":\"wrong keyring\"}"),
            })
            {
                var message = new MailOutboxMessage
                {
                    RecipientEmail = "recipient@example.test", TemplateKey = "synthetic", IdempotencyKey = Guid.NewGuid().ToString("N"),
                    ProtectedPayload = protector.Protect(plaintext),
                };
                payloads[message.Id] = plaintext;
                context.MailOutboxMessages.Add(message);
            }
            await context.SaveChangesAsync();
        }
        Directory.CreateDirectory(CachePath);
        var formerEvidence = new byte[] { 1, 2, 3 };
        await File.WriteAllBytesAsync(Path.Combine(CachePath, "former.bin"),
            former.CreateProtector(FormerRevocationPurpose).Protect(formerEvidence));
        await File.WriteAllBytesAsync(Path.Combine(CachePath, "current.bin"),
            PvadDataProtection.RevocationEvidence(current).Protect(new byte[] { 4 }));
        var originalBytes = await StoredPayloadsAsync(options);

        var dryRun = await Service(options, current).RunAsync(dryRun: true);
        Assert.Equal(new PvadReprotectionReport(true, 1, 2, 1, 1, 1, 0), dryRun);
        Assert.True(dryRun.RequiresConversion);
        Assert.Equal(originalBytes, await StoredPayloadsAsync(options));

        var run = await Service(options, current).RunAsync(dryRun: false);
        Assert.Equal(new PvadReprotectionReport(false, 1, 2, 1, 1, 1, 0), run);
        var readable = PvadDataProtection.MailPayload(current);
        await using (var context = new PvadDbContext(options))
            foreach (var message in await context.MailOutboxMessages.AsNoTracking().ToListAsync())
            {
                if (payloads[message.Id].Contains("wrong keyring", StringComparison.Ordinal))
                    Assert.Equal(originalBytes[message.Id], message.ProtectedPayload);
                else
                    Assert.Equal(payloads[message.Id], readable.Unprotect(message.ProtectedPayload));
            }
        Assert.Equal(formerEvidence, PvadDataProtection.RevocationEvidence(current)
            .Unprotect(await File.ReadAllBytesAsync(Path.Combine(CachePath, "former.bin"))));

        // A repeated or restarted run finds nothing left to convert and changes nothing.
        var converted = await StoredPayloadsAsync(options);
        var rerun = await Service(options, current).RunAsync(dryRun: true);
        Assert.Equal(new PvadReprotectionReport(true, 3, 0, 1, 2, 0, 0), rerun);
        Assert.False(rerun.RequiresConversion);
        Assert.Equal(converted, await StoredPayloadsAsync(options));
        Assert.Empty(Directory.EnumerateFiles(CachePath, "*.tmp"));
    }

    [Fact]
    public async Task Former_payloads_are_not_readable_through_the_current_identity()
    {
        var former = Provider(KeysPath, PvadProtectedDataReprotection.FormerApplicationName);
        var current = Provider(KeysPath, PvadDataProtection.ApplicationName);
        var protectedValue = former.CreateProtector(PvadDataProtection.MailPayloadPurpose).Protect("payload");
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() =>
            PvadDataProtection.MailPayload(current).Unprotect(protectedValue));
    }

    [Theory]
    [InlineData(true, false, false, false, new[] { PvadActivationBlocker.MailUnreadable })]
    [InlineData(false, true, false, false, new[] { PvadActivationBlocker.RevocationCacheUnreadable })]
    [InlineData(true, false, true, false, new[] { PvadActivationBlocker.ConversionRequired, PvadActivationBlocker.MailUnreadable })]
    [InlineData(false, false, false, true, new string[0])]
    public async Task Startup_guard_refuses_activation_for_each_unresolved_condition(
        bool unreadableMail, bool unreadableCache, bool convertibleMail, bool currentOnly, string[] expected)
    {
        var former = Provider(KeysPath, PvadProtectedDataReprotection.FormerApplicationName);
        var current = Provider(KeysPath, PvadDataProtection.ApplicationName);
        var foreign = Provider(Path.Combine(root, "foreign-keys"), PvadProtectedDataReprotection.FormerApplicationName);
        var options = new DbContextOptionsBuilder<PvadDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var context = new PvadDbContext(options))
        {
            context.MailOutboxMessages.Add(Message(PvadDataProtection.MailPayload(current).Protect("{\"subject\":\"current\"}")));
            if (unreadableMail) context.MailOutboxMessages.Add(Message(foreign.CreateProtector(FormerMailPurpose).Protect("{\"subject\":\"secret\"}")));
            if (convertibleMail) context.MailOutboxMessages.Add(Message(former.CreateProtector(FormerMailPurpose).Protect("{\"subject\":\"former\"}")));
            await context.SaveChangesAsync();
        }
        Directory.CreateDirectory(CachePath);
        await File.WriteAllBytesAsync(Path.Combine(CachePath, "current.bin"), PvadDataProtection.RevocationEvidence(current).Protect([1]));
        if (unreadableCache)
            await File.WriteAllBytesAsync(Path.Combine(CachePath, "foreign.bin"), foreign.CreateProtector(FormerRevocationPurpose).Protect([2]));
        var stored = await StoredPayloadsAsync(options);
        await using var services = Services(options, current);

        if (currentOnly)
        {
            await PvadProtectedDataReprotection.EnsureActivationSafeAsync(services);
            return;
        }
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => PvadProtectedDataReprotection.EnsureActivationSafeAsync(services));
        foreach (var blocker in new[] { PvadActivationBlocker.ConversionRequired, PvadActivationBlocker.MailUnreadable, PvadActivationBlocker.RevocationCacheUnreadable })
            Assert.Equal(expected.Contains(blocker), refused.Message.Contains(blocker, StringComparison.Ordinal));
        Assert.DoesNotContain("secret", refused.Message, StringComparison.Ordinal);
        Assert.Equal(stored, await StoredPayloadsAsync(options));
    }

    [Fact]
    public async Task Startup_succeeds_only_after_a_restored_key_has_been_used_for_conversion()
    {
        var foreignKeys = Path.Combine(root, "restored-keys");
        var missing = Provider(foreignKeys, PvadProtectedDataReprotection.FormerApplicationName);
        var current = Provider(KeysPath, PvadDataProtection.ApplicationName);
        _ = current.CreateProtector("initialize").Protect("key");
        var options = new DbContextOptionsBuilder<PvadDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var message = Message(missing.CreateProtector(FormerMailPurpose).Protect("{\"subject\":\"pending\"}"));
        await using (var context = new PvadDbContext(options))
        {
            context.MailOutboxMessages.Add(message);
            await context.SaveChangesAsync();
        }
        await using var services = Services(options, current);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => PvadProtectedDataReprotection.EnsureActivationSafeAsync(services));
        Assert.Contains(PvadActivationBlocker.MailUnreadable, refused.Message);

        // Restoring the missing key makes the payload convertible, but activation still waits for conversion.
        foreach (var key in Directory.EnumerateFiles(foreignKeys, "key-*.xml"))
            File.Copy(key, Path.Combine(KeysPath, Path.GetFileName(key)));
        refused = await Assert.ThrowsAsync<InvalidOperationException>(() => PvadProtectedDataReprotection.EnsureActivationSafeAsync(services));
        Assert.Contains(PvadActivationBlocker.ConversionRequired, refused.Message);
        Assert.DoesNotContain(PvadActivationBlocker.MailUnreadable, refused.Message);

        Assert.Equal(new PvadReprotectionReport(false, 0, 1, 0, 0, 0, 0), await Service(options, current).RunAsync(dryRun: false));
        await PvadProtectedDataReprotection.EnsureActivationSafeAsync(services);
        await using var verify = new PvadDbContext(options);
        Assert.Equal("{\"subject\":\"pending\"}", PvadDataProtection.MailPayload(current)
            .Unprotect((await verify.MailOutboxMessages.SingleAsync()).ProtectedPayload));
    }

    private static MailOutboxMessage Message(string protectedPayload) => new()
    {
        RecipientEmail = "recipient@example.test", TemplateKey = "synthetic", IdempotencyKey = Guid.NewGuid().ToString("N"),
        ProtectedPayload = protectedPayload,
    };

    private ServiceProvider Services(DbContextOptions<PvadDbContext> options, IDataProtectionProvider current)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new PvadDbContext(options));
        services.AddSingleton(current);
        services.AddSingleton(Options.Create(new PvadPdfRevocationEvidenceOptions { CacheDirectory = CachePath }));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DataProtection:KeysDirectory"] = KeysPath }).Build());
        services.AddSingleton<IHostEnvironment>(new Environment(root));
        services.AddSingleton<ILogger<PvadProtectedDataReprotection>>(NullLogger<PvadProtectedDataReprotection>.Instance);
        services.AddScoped<PvadProtectedDataReprotection>();
        return services.BuildServiceProvider();
    }

    private static IDataProtectionProvider Provider(string keys, string application) =>
        DataProtectionProvider.Create(new DirectoryInfo(keys), options => options.SetApplicationName(application));

    private PvadProtectedDataReprotection Service(DbContextOptions<PvadDbContext> options, IDataProtectionProvider current) => new(
        new PvadDbContext(options), current,
        Options.Create(new PvadPdfRevocationEvidenceOptions { CacheDirectory = CachePath }),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["DataProtection:KeysDirectory"] = KeysPath }).Build(),
        new Environment(root), NullLogger<PvadProtectedDataReprotection>.Instance);

    private static async Task<Dictionary<Guid, string>> StoredPayloadsAsync(DbContextOptions<PvadDbContext> options)
    {
        await using var context = new PvadDbContext(options);
        return await context.MailOutboxMessages.AsNoTracking().ToDictionaryAsync(value => value.Id, value => value.ProtectedPayload);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);

    private sealed class Environment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "PVAD.Api.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
