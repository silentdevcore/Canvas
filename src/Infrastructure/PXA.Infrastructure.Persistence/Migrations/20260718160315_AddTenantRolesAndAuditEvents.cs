using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRolesAndAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_events_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_events_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "organization_membership_roles",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationMembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_membership_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_membership_roles_organization_memberships_Orga~",
                        column: x => x.OrganizationMembershipId,
                        principalSchema: "administration",
                        principalTable: "organization_memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_membership_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_organization_membership_roles_users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ActorUserId",
                schema: "administration",
                table: "audit_events",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_OrganizationId_CreatedAt",
                schema: "administration",
                table: "audit_events",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_TargetType_TargetId",
                schema: "administration",
                table: "audit_events",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_organization_membership_roles_AssignedByUserId",
                schema: "administration",
                table: "organization_membership_roles",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_organization_membership_roles_OrganizationMembershipId_Role~",
                schema: "administration",
                table: "organization_membership_roles",
                columns: new[] { "OrganizationMembershipId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_membership_roles_RoleId",
                schema: "administration",
                table: "organization_membership_roles",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "organization_membership_roles",
                schema: "administration");
        }
    }
}
