using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignerTemplatePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "designer");

            migrationBuilder.CreateTable(
                name: "designer_template_versions",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<long>(type: "bigint", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DesignJson = table.Column<string>(type: "jsonb", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DesignerVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_template_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_template_versions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_designer_template_versions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "designer_templates",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    DraftJson = table.Column<string>(type: "jsonb", nullable: false),
                    DraftChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DesignerVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_templates_designer_template_versions_PublishedVers~",
                        column: x => x.PublishedVersionId,
                        principalSchema: "designer",
                        principalTable: "designer_template_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_designer_templates_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_designer_templates_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_designer_templates_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_designer_template_versions_CreatedByUserId",
                schema: "designer",
                table: "designer_template_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_template_versions_OrganizationId_CreatedAt",
                schema: "designer",
                table: "designer_template_versions",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_template_versions_TemplateId_VersionNumber",
                schema: "designer",
                table: "designer_template_versions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_designer_templates_CreatedByUserId",
                schema: "designer",
                table: "designer_templates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_templates_OrganizationId_ArchivedAt",
                schema: "designer",
                table: "designer_templates",
                columns: new[] { "OrganizationId", "ArchivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_templates_OrganizationId_Name",
                schema: "designer",
                table: "designer_templates",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_templates_OrganizationId_Status_UpdatedAt",
                schema: "designer",
                table: "designer_templates",
                columns: new[] { "OrganizationId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_templates_PublishedVersionId",
                schema: "designer",
                table: "designer_templates",
                column: "PublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_templates_Tags",
                schema: "designer",
                table: "designer_templates",
                column: "Tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_designer_templates_UpdatedByUserId",
                schema: "designer",
                table: "designer_templates",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_designer_template_versions_designer_templates_TemplateId",
                schema: "designer",
                table: "designer_template_versions",
                column: "TemplateId",
                principalSchema: "designer",
                principalTable: "designer_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_designer_template_versions_designer_templates_TemplateId",
                schema: "designer",
                table: "designer_template_versions");

            migrationBuilder.DropTable(
                name: "designer_templates",
                schema: "designer");

            migrationBuilder.DropTable(
                name: "designer_template_versions",
                schema: "designer");
        }
    }
}
