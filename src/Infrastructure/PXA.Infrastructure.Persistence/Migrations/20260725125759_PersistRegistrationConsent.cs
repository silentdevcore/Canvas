using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistRegistrationConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarketingConsentGrantedAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketingConsentSource",
                schema: "identity",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MarketingConsentWithdrawnAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PrivacyAcknowledgedAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyAcknowledgedVersion",
                schema: "identity",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TermsAcceptedAt",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsAcceptedVersion",
                schema: "identity",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketingConsentGrantedAt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MarketingConsentSource",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MarketingConsentWithdrawnAt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PrivacyAcknowledgedAt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "PrivacyAcknowledgedVersion",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedVersion",
                schema: "identity",
                table: "users");
        }
    }
}
