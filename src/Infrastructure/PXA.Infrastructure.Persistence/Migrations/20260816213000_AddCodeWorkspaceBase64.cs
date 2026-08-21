using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PxaDbContext))]
[Migration("20260816213000_AddCodeWorkspaceBase64")]
public partial class AddCodeWorkspaceBase64 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CSharpBase64Draft",
            schema: "designer",
            table: "designer_code_workspaces",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "CSharpBase64Checksum",
            schema: "designer",
            table: "designer_code_workspaces",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

        migrationBuilder.AddColumn<string>(
            name: "CSharpBase64Draft",
            schema: "designer",
            table: "designer_code_workspace_versions",
            type: "text",
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CSharpBase64Draft", schema: "designer", table: "designer_code_workspaces");
        migrationBuilder.DropColumn(name: "CSharpBase64Checksum", schema: "designer", table: "designer_code_workspaces");
        migrationBuilder.DropColumn(name: "CSharpBase64Draft", schema: "designer", table: "designer_code_workspace_versions");
    }
}
