using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PXA.Domain.Entities;
using PXA.Infrastructure.Persistence;
using PXA.WebApi.Security;

namespace PXA.WebApi.Services.Mail;

public sealed class OrganizationNotificationService(
    PxaDbContext dbContext,
    IPxaMailQueue mailQueue,
    IOptions<PxaMailOptions> options)
{
    private readonly PxaMailOptions options = options.Value;

    public async Task<int> QueueAdministratorsAsync(
        Guid organizationId,
        string templateKey,
        string eventKey,
        IReadOnlyDictionary<string, string> details,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? additionalUserIds = null)
    {
        var additionalRecipients = additionalUserIds?.Distinct().ToArray() ?? [];
        var recipients = await (
                from membership in dbContext.OrganizationMemberships.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on membership.UserId equals user.Id
                where membership.OrganizationId == organizationId &&
                      membership.Status == OrganizationMembershipStatus.Active &&
                      user.IsActive &&
                      user.EmailConfirmed &&
                      user.Email != null &&
                      (additionalRecipients.Contains(user.Id) ||
                       (from membershipRole in dbContext.OrganizationMembershipRoles
                        join role in dbContext.Roles on membershipRole.RoleId equals role.Id
                        where membershipRole.OrganizationMembershipId == membership.Id &&
                              role.Name == PxaRoles.OrganizationAdministrator
                        select membershipRole.Id).Any())
                select new { user.Id, user.DisplayName, user.Email, user.Locale })
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            var payload = new Dictionary<string, string>(details, StringComparer.Ordinal)
            {
                ["displayName"] = recipient.DisplayName,
                ["actionUrl"] = options.AccountBaseUrl.TrimEnd('/'),
            };
            mailQueue.Enqueue(
                organizationId,
                recipient.Id,
                recipient.Email!,
                templateKey,
                payload,
                $"{eventKey}:{recipient.Id}",
                recipient.Locale);
        }

        return recipients.Count;
    }
}
