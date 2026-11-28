using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalNativeLicenseOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "administration",
                table: "native_library_licenses",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                schema: "administration",
                table: "native_library_licenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_native_library_licenses_OwnerUserId_IssuedAt",
                schema: "administration",
                table: "native_library_licenses",
                columns: new[] { "OwnerUserId", "IssuedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_native_library_licenses_single_owner",
                schema: "administration",
                table: "native_library_licenses",
                sql: "(\"OrganizationId\" IS NOT NULL) <> (\"OwnerUserId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_native_library_licenses_users_OwnerUserId",
                schema: "administration",
                table: "native_library_licenses",
                column: "OwnerUserId",
                principalSchema: "identity",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE FUNCTION administration.require_native_license_renewal_owner() RETURNS trigger AS $$
                BEGIN
                    IF NEW."PreviousLicenseId" IS NOT NULL AND EXISTS (
                        SELECT 1 FROM administration.native_library_licenses prior
                        WHERE prior."Id" = NEW."PreviousLicenseId" AND
                            (prior."OrganizationId" IS DISTINCT FROM NEW."OrganizationId" OR
                             prior."OwnerUserId" IS DISTINCT FROM NEW."OwnerUserId")) THEN
                        RAISE EXCEPTION 'Native license renewal must retain its owner' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER require_native_license_renewal_owner
                BEFORE INSERT ON administration.native_library_licenses
                FOR EACH ROW EXECUTE FUNCTION administration.require_native_license_renewal_owner();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Personal ownership cannot be represented by the old schema. Refuse
            // rollback before any DDL instead of discarding immutable license owners.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM administration.native_library_licenses WHERE "OwnerUserId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Personal native licenses prevent rollback to organization-only ownership';
                    END IF;
                END $$;
                """);
            migrationBuilder.Sql("DROP TRIGGER require_native_license_renewal_owner ON administration.native_library_licenses; DROP FUNCTION administration.require_native_license_renewal_owner();");
            migrationBuilder.DropForeignKey(
                name: "FK_native_library_licenses_users_OwnerUserId",
                schema: "administration",
                table: "native_library_licenses");

            migrationBuilder.DropIndex(
                name: "IX_native_library_licenses_OwnerUserId_IssuedAt",
                schema: "administration",
                table: "native_library_licenses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_native_library_licenses_single_owner",
                schema: "administration",
                table: "native_library_licenses");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                schema: "administration",
                table: "native_library_licenses");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "administration",
                table: "native_library_licenses",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
