using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignerCodeWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "designer_code_workspaces",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JsonDraft = table.Column<string>(type: "text", nullable: false),
                    CSharpModelDraft = table.Column<string>(type: "text", nullable: false),
                    CSharpPdfDraft = table.Column<string>(type: "text", nullable: false),
                    CanonicalDesignJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceMapJson = table.Column<string>(type: "jsonb", nullable: false),
                    JsonChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CSharpModelChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CSharpPdfChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanonicalChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BaseTemplateRevision = table.Column<long>(type: "bigint", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_code_workspaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_code_workspaces_designer_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "designer",
                        principalTable: "designer_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_code_workspaces_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_designer_code_workspaces_users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "designer_code_workspace_versions",
                schema: "designer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceRevision = table.Column<long>(type: "bigint", nullable: false),
                    JsonDraft = table.Column<string>(type: "text", nullable: false),
                    CSharpModelDraft = table.Column<string>(type: "text", nullable: false),
                    CSharpPdfDraft = table.Column<string>(type: "text", nullable: false),
                    CanonicalDesignJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceMapJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designer_code_workspace_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_designer_code_workspace_versions_designer_code_workspaces_W~",
                        column: x => x.WorkspaceId,
                        principalSchema: "designer",
                        principalTable: "designer_code_workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_code_workspace_versions_designer_template_versions~",
                        column: x => x.TemplateVersionId,
                        principalSchema: "designer",
                        principalTable: "designer_template_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_code_workspace_versions_designer_templates_Templat~",
                        column: x => x.TemplateId,
                        principalSchema: "designer",
                        principalTable: "designer_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_designer_code_workspace_versions_organizations_Organization~",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_designer_code_workspace_versions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_designer_code_workspace_versions_CreatedByUserId",
                schema: "designer",
                table: "designer_code_workspace_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_code_workspace_versions_OrganizationId_CreatedAt",
                schema: "designer",
                table: "designer_code_workspace_versions",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_code_workspace_versions_TemplateId",
                schema: "designer",
                table: "designer_code_workspace_versions",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_code_workspace_versions_TemplateVersionId",
                schema: "designer",
                table: "designer_code_workspace_versions",
                column: "TemplateVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_designer_code_workspace_versions_WorkspaceId",
                schema: "designer",
                table: "designer_code_workspace_versions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_designer_code_workspaces_OrganizationId_UpdatedAt",
                schema: "designer",
                table: "designer_code_workspaces",
                columns: new[] { "OrganizationId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_designer_code_workspaces_TemplateId",
                schema: "designer",
                table: "designer_code_workspaces",
                column: "TemplateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_designer_code_workspaces_UpdatedByUserId",
                schema: "designer",
                table: "designer_code_workspaces",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "designer_code_workspace_versions",
                schema: "designer");

            migrationBuilder.DropTable(
                name: "designer_code_workspaces",
                schema: "designer");
        }
    }
}
