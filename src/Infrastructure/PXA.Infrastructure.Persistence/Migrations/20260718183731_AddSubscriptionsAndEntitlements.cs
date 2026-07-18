using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionsAndEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Edition = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BillingPeriod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DeploymentMode = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SeatLimit = table.Column<int>(type: "integer", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TrialEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationEffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GracePeriodEndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscriptions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_entitlements",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capability = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Limit = table.Column<long>(type: "bigint", nullable: true),
                    Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_entitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscription_entitlements_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "administration",
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscription_lifecycle_events",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CurrentStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_lifecycle_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscription_lifecycle_events_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "administration",
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "subscription_seat_assignments",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_seat_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_subscription_seat_assignments_organization_memberships_Orga~",
                        column: x => x.OrganizationMembershipId,
                        principalSchema: "administration",
                        principalTable: "organization_memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscription_seat_assignments_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "administration",
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_entitlements_SubscriptionId_Capability",
                schema: "administration",
                table: "subscription_entitlements",
                columns: new[] { "SubscriptionId", "Capability" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscription_lifecycle_events_SubscriptionId_CreatedAt",
                schema: "administration",
                table: "subscription_lifecycle_events",
                columns: new[] { "SubscriptionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_subscription_seat_assignments_OrganizationMembershipId",
                schema: "administration",
                table: "subscription_seat_assignments",
                column: "OrganizationMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_subscription_seat_assignments_SubscriptionId_OrganizationMe~",
                schema: "administration",
                table: "subscription_seat_assignments",
                columns: new[] { "SubscriptionId", "OrganizationMembershipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_OrganizationId",
                schema: "administration",
                table: "subscriptions",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_entitlements",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "subscription_lifecycle_events",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "subscription_seat_assignments",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "administration");
        }
    }
}
