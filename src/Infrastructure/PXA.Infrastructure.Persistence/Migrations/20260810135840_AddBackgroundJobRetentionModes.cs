using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundJobRetentionModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContentPurgedAt",
                schema: "administration",
                table: "background_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MetadataExpiresAt",
                schema: "administration",
                table: "background_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResultDownloadedAt",
                schema: "administration",
                table: "background_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetentionMode",
                schema: "administration",
                table: "background_jobs",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Retained");

            migrationBuilder.Sql("""
                UPDATE administration.background_jobs
                SET "MetadataExpiresAt" = "ExpiresAt" + INTERVAL '23 days';
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "MetadataExpiresAt",
                schema: "administration",
                table: "background_jobs",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "RetentionMode",
                schema: "administration",
                table: "background_jobs",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(24)",
                oldMaxLength: 24,
                oldDefaultValue: "Retained");

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_MetadataExpiresAt",
                schema: "administration",
                table: "background_jobs",
                column: "MetadataExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_background_jobs_MetadataExpiresAt",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "ContentPurgedAt",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "MetadataExpiresAt",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "ResultDownloadedAt",
                schema: "administration",
                table: "background_jobs");

            migrationBuilder.DropColumn(
                name: "RetentionMode",
                schema: "administration",
                table: "background_jobs");
        }
    }
}
