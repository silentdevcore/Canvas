using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendBackgroundJobLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiagnosticsJson",
                schema: "administration",
                table: "background_jobs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "administration",
                table: "background_jobs",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "InputObjectId",
                schema: "administration",
                table: "background_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                schema: "administration",
                table: "background_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_ExpiresAt",
                schema: "administration",
                table: "background_jobs",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_InputObjectId",
                schema: "administration",
                table: "background_jobs",
                column: "InputObjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_background_jobs_stored_objects_InputObjectId",
                schema: "administration",
                table: "background_jobs",
                column: "InputObjectId",
                principalSchema: "administration",
                principalTable: "stored_objects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_background_jobs_stored_objects_InputObjectId",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropIndex(
                name: "IX_background_jobs_ExpiresAt",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropIndex(
                name: "IX_background_jobs_InputObjectId",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "DiagnosticsJson",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "InputObjectId",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                schema: "administration",
                table: "background_jobs");
        }
    }
}
