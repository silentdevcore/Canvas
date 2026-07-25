using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobsAndStoredObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stored_objects",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stored_objects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stored_objects_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stored_objects_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "background_jobs",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    MaximumAttempts = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultObjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_background_jobs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_background_jobs_stored_objects_ResultObjectId",
                        column: x => x.ResultObjectId,
                        principalSchema: "administration",
                        principalTable: "stored_objects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_background_jobs_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_CreatedByUserId",
                schema: "administration",
                table: "background_jobs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_LeaseExpiresAt",
                schema: "administration",
                table: "background_jobs",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_OrganizationId_CreatedAt",
                schema: "administration",
                table: "background_jobs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_ResultObjectId",
                schema: "administration",
                table: "background_jobs",
                column: "ResultObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_Status_ScheduledAt",
                schema: "administration",
                table: "background_jobs",
                columns: new[] { "Status", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stored_objects_CreatedByUserId",
                schema: "administration",
                table: "stored_objects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stored_objects_ObjectKey",
                schema: "administration",
                table: "stored_objects",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stored_objects_OrganizationId_Purpose_CreatedAt",
                schema: "administration",
                table: "stored_objects",
                columns: new[] { "OrganizationId", "Purpose", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stored_objects_OrganizationId_Status",
                schema: "administration",
                table: "stored_objects",
                columns: new[] { "OrganizationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_jobs",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "stored_objects",
                schema: "administration");
        }
    }
}
