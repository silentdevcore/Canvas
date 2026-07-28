using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence.Identity;

namespace PXA.Infrastructure.Persistence.Configurations;

internal sealed class MailOutboxMessageConfiguration : IEntityTypeConfiguration<MailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<MailOutboxMessage> builder)
    {
        builder.ToTable("mail_outbox", DatabaseSchemas.Administration);
        builder.HasKey(message => message.Id);
        builder.Property(message => message.RecipientEmail).HasMaxLength(320).IsRequired();
        builder.Property(message => message.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(message => message.Locale).HasMaxLength(16).IsRequired();
        builder.Property(message => message.ProtectedPayload).IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(message => message.ProviderMessageId).HasMaxLength(200);
        builder.Property(message => message.FailureReason).HasMaxLength(1000);
        builder.Property(message => message.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(message => message.TraceParent).HasMaxLength(128);
        builder.Property(message => message.TraceState).HasMaxLength(512);
        builder.HasIndex(message => message.IdempotencyKey).IsUnique();
        builder.HasIndex(message => new { message.Status, message.ScheduledAt });
        builder.HasIndex(message => new { message.OrganizationId, message.CreatedAt });
        builder.HasOne<Organization>().WithMany().HasForeignKey(message => message.OrganizationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<PxaIdentityUser>().WithMany().HasForeignKey(message => message.RecipientUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
