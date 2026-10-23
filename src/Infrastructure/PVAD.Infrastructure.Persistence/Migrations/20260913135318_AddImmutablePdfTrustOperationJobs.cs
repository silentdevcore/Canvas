using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImmutablePdfTrustOperationJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdf_trust_operation_jobs",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Policy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    InputSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OutputSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DiagnosticCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    KeyIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_trust_operation_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pdf_trust_operation_jobs_audit_events_AuditEventId",
                        column: x => x.AuditEventId,
                        principalSchema: "administration",
                        principalTable: "audit_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pdf_trust_operation_jobs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pdf_trust_operation_jobs_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pdf_trust_operation_jobs_ActorUserId",
                schema: "administration",
                table: "pdf_trust_operation_jobs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_trust_operation_jobs_AuditEventId",
                schema: "administration",
                table: "pdf_trust_operation_jobs",
                column: "AuditEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_trust_operation_jobs_InputSha256",
                schema: "administration",
                table: "pdf_trust_operation_jobs",
                column: "InputSha256");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_trust_operation_jobs_Operation_CreatedAt",
                schema: "administration",
                table: "pdf_trust_operation_jobs",
                columns: new[] { "Operation", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pdf_trust_operation_jobs_OrganizationId_CreatedAt",
                schema: "administration",
                table: "pdf_trust_operation_jobs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_pdf_trust_operation_jobs_OutputSha256",
                schema: "administration",
                table: "pdf_trust_operation_jobs",
                column: "OutputSha256");

            migrationBuilder.Sql(
                """
                CREATE TRIGGER reject_pdf_trust_operation_job_mutation
                BEFORE UPDATE OR DELETE ON administration.pdf_trust_operation_jobs
                FOR EACH ROW
                EXECUTE FUNCTION administration.reject_audit_event_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS reject_pdf_trust_operation_job_mutation
                    ON administration.pdf_trust_operation_jobs;
                """);

            migrationBuilder.DropTable(
                name: "pdf_trust_operation_jobs",
                schema: "administration");
        }
    }
}
