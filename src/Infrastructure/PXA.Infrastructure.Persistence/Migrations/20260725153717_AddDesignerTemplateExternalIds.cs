using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDesignerTemplateExternalIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                schema: "designer",
                table: "designer_templates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_designer_templates_OrganizationId_ExternalId",
                schema: "designer",
                table: "designer_templates",
                columns: new[] { "OrganizationId", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_designer_templates_OrganizationId_ExternalId",
                schema: "designer",
                table: "designer_templates");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                schema: "designer",
                table: "designer_templates");
        }
    }
}
