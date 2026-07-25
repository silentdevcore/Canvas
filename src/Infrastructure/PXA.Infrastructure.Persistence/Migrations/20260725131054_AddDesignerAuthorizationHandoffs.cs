using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignerAuthorizationHandoffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "designer_authorization_codes",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PkceChallenge = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DesignerOrigin = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ReturnPath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_authorization_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_authorization_codes_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_authorization_codes_user_sessions_SourceSessionId",
                        column: x => x.SourceSessionId,
                        principalSchema: "identity",
                        principalTable: "user_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_authorization_codes_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_designer_authorization_codes_CodeHash",
                schema: "identity",
                table: "designer_authorization_codes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_designer_authorization_codes_OrganizationId",
                schema: "identity",
                table: "designer_authorization_codes",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_authorization_codes_SourceSessionId",
                schema: "identity",
                table: "designer_authorization_codes",
                column: "SourceSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_authorization_codes_UserId_ExpiresAt_ConsumedAt",
                schema: "identity",
                table: "designer_authorization_codes",
                columns: new[] { "UserId", "ExpiresAt", "ConsumedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "designer_authorization_codes",
                schema: "identity");
        }
    }
}
