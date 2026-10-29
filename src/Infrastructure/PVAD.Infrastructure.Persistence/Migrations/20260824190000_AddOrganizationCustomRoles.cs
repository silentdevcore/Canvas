using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PvadDbContext))]
[Migration("20260824190000_AddOrganizationCustomRoles")]
public partial class AddOrganizationCustomRoles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CustomKey", schema: "identity", table: "roles",
            type: "character varying(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "DisplayName", schema: "identity", table: "roles",
            type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "IsArchived", schema: "identity", table: "roles",
            type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>(
            name: "OrganizationId", schema: "identity", table: "roles",
            type: "uuid", nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_roles_OrganizationId_CustomKey", schema: "identity", table: "roles",
            columns: new[] { "OrganizationId", "CustomKey" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_roles_OrganizationId_CustomKey", schema: "identity", table: "roles");
        migrationBuilder.DropColumn(name: "CustomKey", schema: "identity", table: "roles");
        migrationBuilder.DropColumn(name: "DisplayName", schema: "identity", table: "roles");
        migrationBuilder.DropColumn(name: "IsArchived", schema: "identity", table: "roles");
        migrationBuilder.DropColumn(name: "OrganizationId", schema: "identity", table: "roles");
    }
}
