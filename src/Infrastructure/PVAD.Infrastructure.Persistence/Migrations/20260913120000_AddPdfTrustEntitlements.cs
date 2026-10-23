using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PvadDbContext))]
[Migration("20260913120000_AddPdfTrustEntitlements")]
public sealed class AddPdfTrustEntitlements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO administration.subscription_entitlements
                ("Id", "SubscriptionId", "Capability", "Enabled", "Limit", "Unit", "Source", "ExpiresAt", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), source."SubscriptionId", mapping.capability,
                   source."Enabled", NULL, NULL, source."Source", source."ExpiresAt", NOW(), NOW()
            FROM administration.subscription_entitlements AS source
            JOIN (VALUES
                ('pdf-viewer', 'pdf-trust'),
                ('pdf-viewer', 'pdf-signing'),
                ('pdf-viewer', 'pdf-redaction'),
                ('generator', 'pdf-compliance')
            ) AS mapping(source_capability, capability)
              ON source."Capability" = mapping.source_capability
            WHERE NOT EXISTS (
                SELECT 1
                FROM administration.subscription_entitlements AS existing
                WHERE existing."SubscriptionId" = source."SubscriptionId"
                  AND existing."Capability" = mapping.capability
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM administration.subscription_entitlements
            WHERE "Capability" IN ('pdf-trust', 'pdf-signing', 'pdf-redaction', 'pdf-compliance');
            """);
    }
}
