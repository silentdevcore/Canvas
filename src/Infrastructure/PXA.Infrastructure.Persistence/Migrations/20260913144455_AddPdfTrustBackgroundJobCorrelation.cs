using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfTrustBackgroundJobCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BackgroundJobId",
                schema: "administration",
                table: "pdf_trust_operation_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_trust_operation_jobs_BackgroundJobId",
                schema: "administration",
                table: "pdf_trust_operation_jobs",
                column: "BackgroundJobId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pdf_trust_operation_jobs_BackgroundJobId",
                schema: "administration",
                table: "pdf_trust_operation_jobs");

            migrationBuilder.DropColumn(
                name: "BackgroundJobId",
                schema: "administration",
                table: "pdf_trust_operation_jobs");
        }
    }
}
