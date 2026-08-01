using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalDocumentGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legal_documents",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_legal_documents_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_document_versions",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Audience = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SourceMarkdown = table.Column<string>(type: "text", nullable: false),
                    RenderedHtml = table.Column<string>(type: "text", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequiresAcceptance = table.Column<bool>(type: "boolean", nullable: false),
                    IsAuthoritative = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PreviousVersionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_legal_document_versions_legal_document_versions_PreviousVer~",
                        column: x => x.PreviousVersionId,
                        principalSchema: "administration",
                        principalTable: "legal_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_document_versions_legal_documents_LegalDocumentId",
                        column: x => x.LegalDocumentId,
                        principalSchema: "administration",
                        principalTable: "legal_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_document_versions_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_document_versions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_document_versions_users_PublishedByUserId",
                        column: x => x.PublishedByUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_acceptance_events",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LegalDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_acceptance_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_legal_acceptance_events_legal_document_versions_LegalDocume~",
                        column: x => x.LegalDocumentVersionId,
                        principalSchema: "administration",
                        principalTable: "legal_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_acceptance_events_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "administration",
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_acceptance_events_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "legal_publication_approvals",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalDocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legal_publication_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_legal_publication_approvals_legal_document_versions_LegalDo~",
                        column: x => x.LegalDocumentVersionId,
                        principalSchema: "administration",
                        principalTable: "legal_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_legal_publication_approvals_users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_legal_acceptance_events_LegalDocumentVersionId_CreatedAt",
                schema: "identity",
                table: "legal_acceptance_events",
                columns: new[] { "LegalDocumentVersionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_acceptance_events_OrganizationId",
                schema: "identity",
                table: "legal_acceptance_events",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_acceptance_events_UserId_DocumentType_CreatedAt",
                schema: "identity",
                table: "legal_acceptance_events",
                columns: new[] { "UserId", "DocumentType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_legal_acceptance_events_user_global_version",
                schema: "identity",
                table: "legal_acceptance_events",
                columns: new[] { "UserId", "LegalDocumentVersionId" },
                unique: true,
                filter: "\"OrganizationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_legal_acceptance_events_user_org_version",
                schema: "identity",
                table: "legal_acceptance_events",
                columns: new[] { "UserId", "OrganizationId", "LegalDocumentVersionId" },
                unique: true,
                filter: "\"OrganizationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_versions_ApprovedByUserId",
                schema: "administration",
                table: "legal_document_versions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_versions_ContentHash",
                schema: "administration",
                table: "legal_document_versions",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_versions_CreatedByUserId",
                schema: "administration",
                table: "legal_document_versions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_versions_LegalDocumentId_Locale_Audience_Sta~",
                schema: "administration",
                table: "legal_document_versions",
                columns: new[] { "LegalDocumentId", "Locale", "Audience", "Status", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_versions_LegalDocumentId_Locale_Audience_Ver~",
                schema: "administration",
                table: "legal_document_versions",
                columns: new[] { "LegalDocumentId", "Locale", "Audience", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_versions_PreviousVersionId",
                schema: "administration",
                table: "legal_document_versions",
                column: "PreviousVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_document_versions_PublishedByUserId",
                schema: "administration",
                table: "legal_document_versions",
                column: "PublishedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_documents_CreatedByUserId",
                schema: "administration",
                table: "legal_documents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_legal_documents_Key",
                schema: "administration",
                table: "legal_documents",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legal_documents_Type",
                schema: "administration",
                table: "legal_documents",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legal_publication_approvals_LegalDocumentVersionId_CreatedAt",
                schema: "administration",
                table: "legal_publication_approvals",
                columns: new[] { "LegalDocumentVersionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_legal_publication_approvals_ReviewerUserId",
                schema: "administration",
                table: "legal_publication_approvals",
                column: "ReviewerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legal_acceptance_events",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "legal_publication_approvals",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "legal_document_versions",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "legal_documents",
                schema: "administration");
        }
    }
}
