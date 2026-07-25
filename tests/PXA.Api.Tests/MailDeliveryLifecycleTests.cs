using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Services.Mail;
using Testcontainers.PostgreSql;

namespace PXA.Api.Tests;

public sealed class MailDeliveryLifecycleTests
{
    [PostgreSqlFact]
    public async Task Outbox_enforces_disabled_scheduled_retry_dead_letter_and_idempotency_boundaries()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync();
        using var factory = CreateFactory(postgres.GetConnectionString());

        await using (var migrationScope = factory.Services.CreateAsyncScope())
        {
            await migrationScope.ServiceProvider.GetRequiredService<PxaDbContext>()
                .Database.MigrateAsync();
        }

        Guid messageId;
        await using (var enqueueScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = enqueueScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var message = enqueueScope.ServiceProvider.GetRequiredService<IPxaMailQueue>().Enqueue(
                organizationId: null,
                recipientUserId: null,
                recipientEmail: "scheduled@pxa.test",
                templateKey: "identity.password-changed",
                payload: new { displayName = "Scheduled User" },
                idempotencyKey: "mail-lifecycle:scheduled");
            message.ScheduledAt = DateTimeOffset.UtcNow.AddHours(1);
            messageId = message.Id;
            await dbContext.SaveChangesAsync();
        }

        await using (var disabledScope = factory.Services.CreateAsyncScope())
        {
            var processor = new PxaMailProcessor(
                disabledScope.ServiceProvider.GetRequiredService<PxaDbContext>(),
                disabledScope.ServiceProvider.GetRequiredService<IPxaMailTransport>(),
                disabledScope.ServiceProvider.GetRequiredService<IDataProtectionProvider>(),
                Options.Create(new PxaMailOptions { Enabled = false, Transport = "Disabled" }));
            Assert.Equal(0, await processor.ProcessPendingAsync(CancellationToken.None));
        }

        await using (var scheduledScope = factory.Services.CreateAsyncScope())
        {
            var processor = scheduledScope.ServiceProvider.GetRequiredService<PxaMailProcessor>();
            Assert.Equal(0, await processor.ProcessPendingAsync(CancellationToken.None));
            var message = await scheduledScope.ServiceProvider.GetRequiredService<PxaDbContext>()
                .MailOutboxMessages.FindAsync(messageId);
            Assert.Equal(MailDeliveryStatus.Pending, message!.Status);
            Assert.Equal(0, message.Attempts);
        }

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await using var retryScope = factory.Services.CreateAsyncScope();
            var dbContext = retryScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var message = await dbContext.MailOutboxMessages.FindAsync(messageId);
            message!.ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await dbContext.SaveChangesAsync();

            Assert.Equal(1, await retryScope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
                .ProcessPendingAsync(CancellationToken.None));

            Assert.Equal(attempt, message.Attempts);
            Assert.Equal(
                attempt == 5 ? MailDeliveryStatus.DeadLetter : MailDeliveryStatus.Failed,
                message.Status);
        }

        Guid unsupportedMessageId;
        await using (var permanentScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = permanentScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var message = permanentScope.ServiceProvider.GetRequiredService<IPxaMailQueue>().Enqueue(
                organizationId: null,
                recipientUserId: null,
                recipientEmail: "unsupported@pxa.test",
                templateKey: "identity.password-changed",
                payload: new { displayName = "Unsupported User" },
                idempotencyKey: "mail-lifecycle:unsupported");
            await dbContext.SaveChangesAsync();
            message.TemplateKey = "identity.removed-template";
            message.ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            unsupportedMessageId = message.Id;
            await dbContext.SaveChangesAsync();

            Assert.Equal(1, await permanentScope.ServiceProvider.GetRequiredService<PxaMailProcessor>()
                .ProcessPendingAsync(CancellationToken.None));
            Assert.Equal(MailDeliveryStatus.DeadLetter, message.Status);
            Assert.Equal(1, message.Attempts);
        }

        await using (var duplicateScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = duplicateScope.ServiceProvider.GetRequiredService<PxaDbContext>();
            var queue = duplicateScope.ServiceProvider.GetRequiredService<IPxaMailQueue>();
            queue.Enqueue(null, null, "first@pxa.test", "identity.password-changed", new { },
                "mail-lifecycle:duplicate");
            queue.Enqueue(null, null, "second@pxa.test", "identity.password-changed", new { },
                "mail-lifecycle:duplicate");
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var finalDbContext = assertScope.ServiceProvider.GetRequiredService<PxaDbContext>();
        Assert.Equal(
            MailDeliveryStatus.DeadLetter,
            (await finalDbContext.MailOutboxMessages.FindAsync(messageId))!.Status);
        Assert.Equal(
            MailDeliveryStatus.DeadLetter,
            (await finalDbContext.MailOutboxMessages.FindAsync(unsupportedMessageId))!.Status);
        Assert.Empty(assertScope.ServiceProvider.GetRequiredService<AlwaysFailingMailTransport>().Messages);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PxaDatabase"] = connectionString,
                    ["Mail:Enabled"] = "true",
                    ["Mail:Transport"] = "Development",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<PxaDbContext>>();
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPxaMailTransport>();
                services.AddDbContext<PxaDbContext>(options => options.UseNpgsql(connectionString));
                services.AddSingleton<AlwaysFailingMailTransport>();
                services.AddSingleton<IPxaMailTransport>(
                    provider => provider.GetRequiredService<AlwaysFailingMailTransport>());
            });
        });

    private sealed class AlwaysFailingMailTransport : IPxaMailTransport
    {
        public IReadOnlyList<RenderedMail> Messages => [];

        public Task<string> SendAsync(RenderedMail message, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated transient transport failure.");
    }
}
