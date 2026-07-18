using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMailOutboxAndActionTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "identity_action_tokens",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identity_action_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_identity_action_tokens_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_identity_action_tokens_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mail_outbox",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProtectedPayload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mail_outbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mail_outbox_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mail_outbox_users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_identity_action_tokens_OrganizationId",
                schema: "administration",
                table: "identity_action_tokens",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_identity_action_tokens_TokenHash",
                schema: "administration",
                table: "identity_action_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_identity_action_tokens_UserId_Purpose_ExpiresAt",
                schema: "administration",
                table: "identity_action_tokens",
                columns: new[] { "UserId", "Purpose", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_mail_outbox_IdempotencyKey",
                schema: "administration",
                table: "mail_outbox",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mail_outbox_OrganizationId_CreatedAt",
                schema: "administration",
                table: "mail_outbox",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_mail_outbox_RecipientUserId",
                schema: "administration",
                table: "mail_outbox",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_mail_outbox_Status_ScheduledAt",
                schema: "administration",
                table: "mail_outbox",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "identity_action_tokens",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "mail_outbox",
                schema: "administration");
        }
    }
}
