using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignerProductExperience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "designer_feature_policies",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AlphaOptInAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    EnabledOverride = table.Column<bool>(type: "boolean", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_feature_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_feature_policies_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_feature_policies_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "designer_feature_preferences",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_feature_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_feature_preferences_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_feature_preferences_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "designer_notifications",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ActionLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ActionUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Dismissible = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_notifications_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_notifications_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "designer_release_reads",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_release_reads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_release_reads_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "designer_notification_states",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DismissedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_notification_states", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_notification_states_designer_notifications_Notific~",
                        column: x => x.NotificationId,
                        principalSchema: "designer",
                        principalTable: "designer_notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_notification_states_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_designer_feature_policies_OrganizationId_FeatureId",
                schema: "designer",
                table: "designer_feature_policies",
                columns: new[] { "OrganizationId", "FeatureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_designer_feature_policies_UpdatedByUserId",
                schema: "designer",
                table: "designer_feature_policies",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_feature_preferences_OrganizationId_UserId_FeatureId",
                schema: "designer",
                table: "designer_feature_preferences",
                columns: new[] { "OrganizationId", "UserId", "FeatureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_designer_feature_preferences_UserId",
                schema: "designer",
                table: "designer_feature_preferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_notification_states_NotificationId_UserId",
                schema: "designer",
                table: "designer_notification_states",
                columns: new[] { "NotificationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_designer_notification_states_UserId_ReadAt_DismissedAt",
                schema: "designer",
                table: "designer_notification_states",
                columns: new[] { "UserId", "ReadAt", "DismissedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_notifications_ExpiresAt",
                schema: "designer",
                table: "designer_notifications",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_designer_notifications_OrganizationId_CreatedAt",
                schema: "designer",
                table: "designer_notifications",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_notifications_UserId_CreatedAt",
                schema: "designer",
                table: "designer_notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_release_reads_UserId_Version",
                schema: "designer",
                table: "designer_release_reads",
                columns: new[] { "UserId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "designer_feature_policies",
                schema: "designer");

            migrationBuilder.DropTable(
                name: "designer_feature_preferences",
                schema: "designer");

            migrationBuilder.DropTable(
                name: "designer_notification_states",
                schema: "designer");

            migrationBuilder.DropTable(
                name: "designer_release_reads",
                schema: "designer");

            migrationBuilder.DropTable(
                name: "designer_notifications",
                schema: "designer");
        }
    }
}
