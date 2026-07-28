using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PXA.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PxaDbContext))]
[Migration("20260726225925_ProtectAuditEventsFromMutation")]
public sealed class ProtectAuditEventsFromMutation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE FUNCTION administration.reject_audit_event_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            SET search_path = pg_catalog
            AS $function$
            BEGIN
                RAISE EXCEPTION 'Audit events are append-only and cannot be updated or deleted.'
                    USING ERRCODE = '55000';
            END;
            $function$;

            CREATE TRIGGER reject_audit_event_mutation
            BEFORE UPDATE OR DELETE ON administration.audit_events
            FOR EACH ROW
            EXECUTE FUNCTION administration.reject_audit_event_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TRIGGER IF EXISTS reject_audit_event_mutation
                ON administration.audit_events;
            DROP FUNCTION IF EXISTS administration.reject_audit_event_mutation();
            """);
    }
}
