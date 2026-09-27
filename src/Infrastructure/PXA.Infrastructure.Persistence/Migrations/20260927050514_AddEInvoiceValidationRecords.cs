using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEInvoiceValidationRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "e_invoice_validation_records",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocumentSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ContainerSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    Standard = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StandardVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Profile = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Syntax = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    GuidelineIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsStructurallyValid = table.Column<bool>(type: "boolean", nullable: false),
                    IsConformanceValidated = table.Column<bool>(type: "boolean", nullable: false),
                    ConformanceStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ValidatorArtifactSetId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ValidatorArtifactSetSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    DiagnosticsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RevalidatesRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetainUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_e_invoice_validation_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_e_invoice_validation_records_audit_events_AuditEventId",
                        column: x => x.AuditEventId,
                        principalSchema: "administration",
                        principalTable: "audit_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_e_invoice_validation_records_e_invoice_validation_records_R~",
                        column: x => x.RevalidatesRecordId,
                        principalSchema: "administration",
                        principalTable: "e_invoice_validation_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_e_invoice_validation_records_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_e_invoice_validation_records_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_e_invoice_validation_records_ActorUserId",
                schema: "administration",
                table: "e_invoice_validation_records",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_e_invoice_validation_records_AuditEventId",
                schema: "administration",
                table: "e_invoice_validation_records",
                column: "AuditEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_e_invoice_validation_records_OrganizationId_CreatedAt",
                schema: "administration",
                table: "e_invoice_validation_records",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_e_invoice_validation_records_OrganizationId_DocumentSha256",
                schema: "administration",
                table: "e_invoice_validation_records",
                columns: new[] { "OrganizationId", "DocumentSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_e_invoice_validation_records_RetainUntil",
                schema: "administration",
                table: "e_invoice_validation_records",
                column: "RetainUntil");

            migrationBuilder.CreateIndex(
                name: "IX_e_invoice_validation_records_RevalidatesRecordId",
                schema: "administration",
                table: "e_invoice_validation_records",
                column: "RevalidatesRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_e_invoice_validation_records_ValidatorArtifactSetId",
                schema: "administration",
                table: "e_invoice_validation_records",
                column: "ValidatorArtifactSetId");

            // A stored verdict must never change: an UPDATE is always rejected. Bounded
            // retention still has to be able to remove an expired row in whole, so a DELETE is
            // permitted only inside a transaction that has deliberately set
            // pxa.e_invoice_retention_purge. Ordinary application code never sets it, so an
            // accidental or hostile delete fails at the database.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION administration.reject_e_invoice_validation_record_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    IF TG_OP = 'DELETE'
                        AND current_setting('pxa.e_invoice_retention_purge', true) = 'on' THEN
                        RETURN OLD;
                    END IF;
                    RAISE EXCEPTION
                        'Electronic-invoice validation records are append-only; they can only be removed by bounded retention.'
                        USING ERRCODE = '55000';
                END;
                $function$;

                CREATE TRIGGER reject_e_invoice_validation_record_mutation
                BEFORE UPDATE OR DELETE ON administration.e_invoice_validation_records
                FOR EACH ROW
                EXECUTE FUNCTION administration.reject_e_invoice_validation_record_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS reject_e_invoice_validation_record_mutation
                    ON administration.e_invoice_validation_records;
                DROP FUNCTION IF EXISTS administration.reject_e_invoice_validation_record_mutation();
                """);

            migrationBuilder.DropTable(
                name: "e_invoice_validation_records",
                schema: "administration");
        }
    }
}
