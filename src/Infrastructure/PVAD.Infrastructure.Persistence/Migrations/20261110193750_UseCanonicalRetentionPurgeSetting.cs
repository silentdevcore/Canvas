using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Bounded e-invoice retention now marks its transaction with the canonical PVAD setting.
    /// Every UPDATE and every DELETE outside such a transaction remains rejected.
    /// </summary>
    public partial class UseCanonicalRetentionPurgeSetting : Migration
    {
        private static string Function(string setting) => $"""
            CREATE OR REPLACE FUNCTION administration.reject_e_invoice_validation_record_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            SET search_path = pg_catalog
            AS $function$
            BEGIN
                IF TG_OP = 'DELETE'
                    AND current_setting('{setting}', true) = 'on' THEN
                    RETURN OLD;
                END IF;
                RAISE EXCEPTION
                    'Electronic-invoice validation records are append-only; they can only be removed by bounded retention.'
                    USING ERRCODE = '55000';
            END;
            $function$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(Function("pvad.e_invoice_retention_purge"));

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            // Restores the function installed by AddEInvoiceValidationRecords together with the
            // writer that set its original transaction-local setting.
            migrationBuilder.Sql(Function("pxa.e_invoice_retention_purge"));
    }
}
