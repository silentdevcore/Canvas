using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNativeRegistrationLicenseQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "native_license_registration_jobs",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceOrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ContractJson = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_native_license_registration_jobs", x => x.Id);
                    table.CheckConstraint("CK_native_registration_single_owner", "(\"OrganizationId\" IS NOT NULL) <> (\"OwnerUserId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_native_license_registration_jobs_native_library_licenses_Li~",
                        column: x => x.LicenseId,
                        principalSchema: "administration",
                        principalTable: "native_library_licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_native_license_registration_jobs_organizations_Organization~",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_native_license_registration_jobs_organizations_SourceOrgani~",
                        column: x => x.SourceOrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_native_license_registration_jobs_subscriptions_SourceSubscr~",
                        column: x => x.SourceSubscriptionId,
                        principalSchema: "administration",
                        principalTable: "subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_native_license_registration_jobs_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_native_license_registration_jobs_users_SourceUserId",
                        column: x => x.SourceUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_native_license_registration_jobs_LicenseId",
                schema: "administration",
                table: "native_license_registration_jobs",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_native_license_registration_jobs_OrganizationId",
                schema: "administration",
                table: "native_license_registration_jobs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_native_license_registration_jobs_OwnerKey",
                schema: "administration",
                table: "native_license_registration_jobs",
                column: "OwnerKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_native_license_registration_jobs_OwnerUserId",
                schema: "administration",
                table: "native_license_registration_jobs",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_native_license_registration_jobs_SourceOrganizationId",
                schema: "administration",
                table: "native_license_registration_jobs",
                column: "SourceOrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_native_license_registration_jobs_SourceSubscriptionId",
                schema: "administration",
                table: "native_license_registration_jobs",
                column: "SourceSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_native_license_registration_jobs_SourceUserId",
                schema: "administration",
                table: "native_license_registration_jobs",
                column: "SourceUserId");

            migrationBuilder.CreateIndex(
                name: "IX_native_license_registration_jobs_Status_ScheduledAt",
                schema: "administration",
                table: "native_license_registration_jobs",
                columns: new[] { "Status", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM administration.native_license_registration_jobs) THEN
                        RAISE EXCEPTION 'Retained native registration jobs prevent a lossy rollback';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "native_license_registration_jobs",
                schema: "administration");
        }
    }
}
