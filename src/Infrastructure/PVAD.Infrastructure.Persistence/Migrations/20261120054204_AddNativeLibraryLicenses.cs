using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNativeLibraryLicenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "native_library_licenses",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousLicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ArtifactJson = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: false),
                    ArtifactSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    LicenseModel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CustomerReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatesUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_native_library_licenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_native_library_licenses_audit_events_AuditEventId",
                        column: x => x.AuditEventId,
                        principalSchema: "administration",
                        principalTable: "audit_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_native_library_licenses_native_library_licenses_PreviousLic~",
                        column: x => x.PreviousLicenseId,
                        principalSchema: "administration",
                        principalTable: "native_library_licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_native_library_licenses_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_native_library_licenses_users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_native_library_licenses_AuditEventId",
                schema: "administration",
                table: "native_library_licenses",
                column: "AuditEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_native_library_licenses_IssuedByUserId_RequestId",
                schema: "administration",
                table: "native_library_licenses",
                columns: new[] { "IssuedByUserId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_native_library_licenses_OrganizationId_IssuedAt",
                schema: "administration",
                table: "native_library_licenses",
                columns: new[] { "OrganizationId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_native_library_licenses_PreviousLicenseId",
                schema: "administration",
                table: "native_library_licenses",
                column: "PreviousLicenseId",
                unique: true);

            migrationBuilder.Sql("""
                CREATE TRIGGER reject_native_library_license_mutation
                BEFORE UPDATE OR DELETE ON administration.native_library_licenses
                FOR EACH ROW EXECUTE FUNCTION administration.reject_audit_event_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "native_library_licenses",
                schema: "administration");
        }
    }
}
