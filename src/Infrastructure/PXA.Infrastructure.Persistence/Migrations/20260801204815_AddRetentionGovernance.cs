using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retention_legal_holds",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReleasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReleaseReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_legal_holds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_retention_legal_holds_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_retention_legal_holds_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_retention_legal_holds_users_ReleasedByUserId",
                        column: x => x.ReleasedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_retention_legal_holds_CreatedAt",
                schema: "administration",
                table: "retention_legal_holds",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_retention_legal_holds_CreatedByUserId",
                schema: "administration",
                table: "retention_legal_holds",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_retention_legal_holds_OrganizationId",
                schema: "administration",
                table: "retention_legal_holds",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_retention_legal_holds_ReleasedByUserId",
                schema: "administration",
                table: "retention_legal_holds",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_retention_legal_holds_category_global_active",
                schema: "administration",
                table: "retention_legal_holds",
                column: "Category",
                unique: true,
                filter: "\"OrganizationId\" IS NULL AND \"ReleasedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_retention_legal_holds_category_org_active",
                schema: "administration",
                table: "retention_legal_holds",
                columns: new[] { "Category", "OrganizationId" },
                unique: true,
                filter: "\"OrganizationId\" IS NOT NULL AND \"ReleasedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retention_legal_holds",
                schema: "administration");
        }
    }
}
