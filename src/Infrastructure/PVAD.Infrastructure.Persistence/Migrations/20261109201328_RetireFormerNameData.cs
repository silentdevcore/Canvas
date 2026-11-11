using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PVAD.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Rewrites operational identity and job data from the former short name to PVAD.
    /// Append-only audit and PDF trust operation evidence, Legal versions and applied migration
    /// history stay unchanged.
    /// </summary>
    public partial class RetireFormerNameData : Migration
    {
        // Reviewed diagnostic identifiers issued before the hard cut; configuration keys are excluded.
        private static readonly string[] FormerCodes =
        [
            "PXA-MD-001", "PXA-MD-002", "PXA-MD-003", "PXA-MD-004", "PXA-MD-005", "PXA-MD-006", "PXA-MD-100",
            "PXA-MD-201", "PXA-MD-202", "PXA-MD-203", "PXA-PDF-CHART-001", "PXA-PDF-CHART-002",
            "PXA-PDF-CHART-101", "PXA-PDF-CHART-102", "PXA-PDF-CHART-103", "PXA0001", "PXAAPI001",
            "PXAAPI002", "PXAAPI003", "PXAAPI004", "PXAAPI005", "PXAAPI006", "PXAAPI007", "PXAAPI008",
            "PXAAPI009", "PXAAPI010", "PXAAPI011", "PXAAPI012", "PXAAPI013", "PXAAPI014", "PXAAPI015",
            "PXAAPI016", "PXAAPI017", "PXAAPI018", "PXACHART001", "PXACHART002", "PXACODE000", "PXACODE001",
            "PXACODE002", "PXACODE003", "PXACODE004", "PXACODE005", "PXACODE006", "PXACODE007", "PXACODE008",
            "PXACODE009", "PXACODE010", "PXACODE011", "PXACODE012", "PXACODE020", "PXACODE021", "PXACODE022",
            "PXACODE023", "PXACODE030", "PXACODE031", "PXACODE032", "PXACODE040", "PXACODE041", "PXACODE042",
            "PXACODE043", "PXACODE044", "PXACODE100", "PXACODE101", "PXACODE102", "PXACODE103", "PXACODE104",
            "PXACODE105", "PXACODE120", "PXACODE121", "PXACODE122", "PXACODE123", "PXACODE130", "PXACODE131",
            "PXACODE132", "PXACODE140", "PXADESIGNER001", "PXAEINV000", "PXAEINV001", "PXAEINV002",
            "PXAEINV003", "PXAEINV004", "PXAEINV005", "PXAEINV006", "PXAEINV007", "PXAEINV008", "PXAEINV009",
            "PXAEINV010", "PXAEINV011", "PXAEINV012", "PXAEINV013", "PXAEINV014", "PXAEINV015", "PXAEINV016",
            "PXAEINV019", "PXAEINV020", "PXAEINV021", "PXAEINV022", "PXAEINV023", "PXAEINV024", "PXAEINV025",
            "PXAEINV026", "PXAEINV027", "PXAEINV028", "PXAEINV029", "PXAEINV030", "PXAEINV031", "PXAEINV032",
            "PXAEINV033", "PXAEINV034", "PXAEINV035", "PXAEINV100", "PXAEINV101", "PXAEINV102", "PXAEINV103",
            "PXAEINV104", "PXAEINV105", "PXAEINV106", "PXAEINV107", "PXAEINV108", "PXAEINV109", "PXAEINV110",
            "PXAEINV111", "PXAEINV112", "PXAEINV113", "PXAEINV114", "PXAEINV115", "PXAEINV116", "PXAEINV117",
            "PXAEINV119", "PXAEINV120", "PXAEINV121", "PXAEINV122", "PXAEINVAPI001", "PXAEINVAPI002",
            "PXAEINVAPI003", "PXAEINVAPI004", "PXAEINVAPI005", "PXAEINVAPI006", "PXAEINVAPI007",
            "PXAEINVAPI008", "PXAEINVAPI009", "PXAEINVAPI010", "PXAEINVAPI011", "PXAEINVAPI012",
            "PXAEINVAPI013", "PXAEINVAPI014", "PXAEINVAPI020", "PXAEINVAPI021", "PXAEINVAPI022",
            "PXAEINVAPI023", "PXAEINVAPI024", "PXAEINVAPI025", "PXAEINVAPI026", "PXAEINVAPI027",
            "PXAEINVAPI028", "PXAEINVAPI030", "PXAEINVAPI031", "PXAEINVAPI032", "PXAEINVAPI033",
            "PXAEINVAPI034", "PXAEINVATTACH000", "PXAEINVATTACH001", "PXAEINVATTACH002", "PXAEINVATTACH003",
            "PXAEINVATTACH004", "PXAEINVATTACH007", "PXAEINVATTACH008", "PXAEINVATTACH009",
            "PXAEINVATTACH010", "PXAEINVATTACH011", "PXAEINVATTACH012", "PXAEINVATTACH015",
            "PXAEINVATTACH016", "PXALAYOUT001", "PXALAYOUT002", "PXALAYOUT003", "PXALAYOUT004",
            "PXALAYOUT005", "PXALAYOUT006", "PXAMCP001", "PXAMCP002", "PXAMCP003", "PXAMCP010", "PXAMCP011",
            "PXAMCP020", "PXAMCP021", "PXAMCP022", "PXAMCP023", "PXAMCP030", "PXAMCP032", "PXAMCP040",
            "PXAMCP041", "PXAMCP042", "PXAMCP043", "PXAMCP044", "PXAMCP045", "PXAMIG001", "PXAMIG002",
            "PXAMIG003", "PXAMIG004", "PXAMIG005", "PXAMIG006", "PXAMIG007", "PXAMIG008", "PXAMIG009",
            "PXAMIG010", "PXAMIG011", "PXAPDFA000", "PXAPDFA001", "PXAPDFA002", "PXAPDFA003", "PXAPDFA004",
            "PXAPDFA005", "PXAPDFA006", "PXAPDFA007", "PXAPDFA008", "PXAPDFACONVERT000", "PXAPDFACONVERT004",
            "PXAPDFACONVERT005", "PXAPDFACONVERT006", "PXAPDFACONVERT007", "PXAPDFACONVERT008",
            "PXAPDFACONVERT009", "PXAPDFACONVERT010", "PXAPDFACONVERT011", "PXAPDFACONVERT012",
            "PXAPDFACONVERT013", "PXAPDFACONVERT014", "PXAPDFACONVERT015", "PXAPDFACONVERTAPI001",
            "PXAPDFACONVERTAPI002", "PXAPDFACONVERTAPI003", "PXAPDFACONVERTAPI004", "PXAPDFACONVERTAPI005",
            "PXAPDFACONVERTAPI006", "PXAPDFPARSE001", "PXAPDFPARSE002", "PXAPDFPARSE003", "PXAPDFPARSE004",
            "PXAPDFPARSE005", "PXAPDFPARSE006", "PXAPDFPARSE007", "PXAPDFPARSE008", "PXAPDFPARSE009",
            "PXAPDFPARSE010", "PXAPDFPARSE011", "PXAPDFPIPELINE100", "PXAPDFPIPELINE101", "PXAPDFPIPELINE102",
            "PXAPDFPIPELINE103", "PXAPDFPIPELINE104", "PXAPDFPIPELINE105", "PXAPDFPIPELINE106",
            "PXAPDFPIPELINE107", "PXAPDFPIPELINE200", "PXAPDFPIPELINE201", "PXAPDFPIPELINE202",
            "PXAPDFPIPELINE203", "PXAPDFPIPELINE204", "PXAPDFREDACT101", "PXAPDFREDACT102", "PXAPDFREDACT103",
            "PXAPDFREDACT104", "PXAPDFREDACT105", "PXAPDFREDACT106", "PXAPDFREDACT107", "PXAPDFREDACT108",
            "PXAPDFREDACT110", "PXAPDFREDACT111", "PXAPDFREDACT112", "PXAPDFREDACT113", "PXAPDFREDACT201",
            "PXAPDFREDACT202", "PXAPDFREDACT203", "PXAPDFREDACT204", "PXAPDFREDACT205", "PXAPDFREDACT206",
            "PXAPDFREDACT207", "PXAPDFREDACT209", "PXAPDFREDACT210", "PXAPDFREDACT211", "PXAPDFREDACT212",
            "PXAPDFREDACT213", "PXAPDFREDACT301", "PXAPDFREDACT302", "PXAPDFREDACT303", "PXAPDFREDACT304",
            "PXAPDFREDACT305", "PXAPDFREDACT306", "PXAPDFREDACT307", "PXAPDFREDACTAPI001",
            "PXAPDFREDACTAPI002", "PXAPDFREDACTAPI003", "PXAPDFREDACTAPI004", "PXAPDFREDACTAPI005",
            "PXAPDFREDACTAPI006", "PXAPDFREDACTAPI007", "PXAPDFREDACTAPI008", "PXAPDFREDACTAPI009",
            "PXAPDFREDACTAPI010", "PXAPDFREDACTAPI011", "PXAPDFREDACTAPI012", "PXAPDFREDACTVIEWER001",
            "PXAPDFREDACTVIEWER002", "PXAPDFREDACTVIEWER003", "PXAPDFTRUST001", "PXAPDFTRUST002",
            "PXAPDFTRUST010", "PXAPDFTRUST020", "PXAPDFTRUST102", "PXAPDFTRUST103", "PXAPDFTRUST104",
            "PXAPDFTRUST105", "PXAPDFTRUST106", "PXAPDFTRUST110", "PXAPDFTRUST111", "PXAPDFTRUST112",
            "PXAPDFTRUST113", "PXAPDFTRUST114", "PXAPDFTRUST115", "PXAPDFTRUST116", "PXAPDFTRUST117",
            "PXAPDFTRUST118", "PXAPDFTRUST119", "PXAPDFTRUST120", "PXAPDFTRUST121", "PXAPDFTRUST122",
            "PXAPDFTRUST123", "PXAPDFTRUST124", "PXAPDFTRUST125", "PXAPDFTRUST126", "PXAPDFTRUST127",
            "PXAPDFTRUST128", "PXAPDFTRUST129", "PXAPDFTRUST130", "PXAPDFTRUST131", "PXAPDFTRUST132",
            "PXAPDFTRUST133", "PXAPDFTRUST134", "PXAPDFTRUST135", "PXAPDFTRUST140", "PXAPDFTRUST141",
            "PXAPDFTRUST142", "PXAPDFTRUST143", "PXAPDFTRUST144", "PXAPDFTRUST145", "PXAPDFTRUST146",
            "PXAPDFTRUST147", "PXAPDFTRUST148", "PXAPDFTRUST149", "PXAPDFTRUST150", "PXAPDFTRUST151",
            "PXAPDFTRUST200", "PXAPDFTRUST201", "PXAPDFTRUST202", "PXAPDFTRUST203", "PXAPDFTRUST204",
            "PXAPDFTRUST205", "PXAPDFTRUST206", "PXAPDFTRUST300", "PXAPDFTRUST301", "PXAPDFTRUST310",
            "PXAPDFTRUST311", "PXAPDFTRUST312", "PXAPDFTRUST313", "PXAPDFTRUST314", "PXAPDFTRUST315",
            "PXAPDFTRUST316", "PXAPDFTRUST317", "PXAPDFTRUST318", "PXAPDFTRUST319", "PXAPDFTRUST320",
            "PXAPDFTRUST321", "PXAPDFTRUST322", "PXAPDFTRUST323", "PXAPDFTRUST324", "PXAPDFTRUST325",
            "PXAPDFTRUST326", "PXAPDFTRUST327", "PXAPDFTRUST328", "PXAPDFTRUST329", "PXAPDFTRUST330",
            "PXAPDFTRUST331", "PXAPDFTRUST332", "PXAPDFTRUST333", "PXAPDFTRUST334", "PXAPDFTRUST335",
            "PXAPDFTRUST336", "PXAPDFTRUST337", "PXAPDFTRUST338", "PXAPDFTRUST339", "PXAPDFTRUST340",
            "PXAPDFTRUST341", "PXAPDFTRUST343", "PXAPDFTRUST401", "PXAPDFTRUST501", "PXAPDFTRUST502",
            "PXAPDFTRUST503", "PXAPDFTRUSTAPI001", "PXAPDFTRUSTAPI002", "PXAPDFTRUSTAPI003",
            "PXAPDFTRUSTAPI101", "PXAPDFTRUSTAPI102", "PXAPDFTRUSTAPI103", "PXAPDFTRUSTAPI104",
            "PXAPDFTRUSTAPI105", "PXAPDFTRUSTAPI106", "PXAPDFTRUSTAPI111", "PXAPDFTRUSTAPI112",
            "PXAPDFTRUSTAPI113", "PXAPDFTRUSTAPI114", "PXAPDFTRUSTAPI121", "PXAPDFTRUSTAPI122",
            "PXAPDFTRUSTAPI123", "PXAPDFTRUSTAPI124", "PXAPDFTRUSTAPI125", "PXAPDFTRUSTAPI126",
            "PXAPDFTRUSTAPI901", "PXAPDFUA000", "PXAPDFUA001", "PXAPDFUA002", "PXAPDFUA003", "PXAPDFUA004",
            "PXAPDFUA005", "PXAPDFUA006", "PXAPDFUA007", "PXAPDFUA008", "PXAPDFUA009", "PXAPDFUA010",
            "PXAPDFUA011", "PXAPDFUA012", "PXAPDFUA013", "PXAPDFUA014", "PXAPDFUA015", "PXAPDFUA016",
            "PXAPDFUA017", "PXAPDFUA018", "PXAPDFUA019", "PXAPDFUA020", "PXAPDFUA021", "PXAPDFUA022",
            "PXAPDFUA023", "PXAPDFUA024", "PXAPDFUA025", "PXAPDFUA026", "PXAPDFUA027", "PXAPDFUA028",
            "PXAPDFXREF001", "PXAPDFXREF002", "PXAPDFXREF003", "PXAPDFXREF004", "PXAPDFXREF005",
            "PXAPDFXREF008", "PXAPDFXREF009", "PXAPDFXREF010", "PXAPROJECT001", "PXAPROJECT002",
            "PXAPROJECT003", "PXAPROJECT004", "PXAPROJECT005", "PXAPROJECT006", "PXAPROJECT007",
            "PXAPROJECT008", "PXAPROJECT009", "PXAPROJECT010", "PXAPROJECT011", "PXAPROJECT012",
            "PXAPROJECT013", "PXAPROJECT014", "PXAPROJECT015", "PXAPROJECT016", "PXAPROJECT_CANCELLED",
            "PXAPROJECT_EXTERNAL_LINK", "PXAPROJECT_MACRO_PRESERVED", "PXAPROJECT_NETWORK",
            "PXAPROJECT_PROVIDER_EXTENSION", "PXAPROJECT_REQUIRED", "PXAPROJECT_UPGRADED",
            "PXAPROJECT_UPGRADE_LOSSY", "PXAPROJECT_VERSION_COMPATIBLE", "PXAPROJECT_VERSION_CURRENT",
            "PXAPROJECT_VERSION_UPGRADE_REQUIRED", "PXA_API_VERSION_INCOMPATIBLE",
            "PXA_AUDIT_EXPORT_REQUIRES_ENTERPRISE", "PXA_AUTHENTICATION_REQUIRED",
            "PXA_DESIGNER_ACCOUNT_DISABLED", "PXA_DESIGNER_ACCOUNT_LOCKED", "PXA_DESIGNER_ALPHA_NOT_ALLOWED",
            "PXA_DESIGNER_ALPHA_OPT_IN_REQUIRED", "PXA_DESIGNER_ASSET_REQUIRED",
            "PXA_DESIGNER_ASSET_TOO_LARGE", "PXA_DESIGNER_ASSET_TYPE_UNSUPPORTED",
            "PXA_DESIGNER_FEATURE_CONTEXT_REQUIRED", "PXA_DESIGNER_FEATURE_DISABLED",
            "PXA_DESIGNER_FEATURE_ENABLED", "PXA_DESIGNER_FEATURE_UNKNOWN", "PXA_DESIGNER_HANDOFF_INVALID",
            "PXA_DESIGNER_MEMBERSHIP_INACTIVE", "PXA_DESIGNER_PERMISSION_REQUIRED",
            "PXA_DESIGNER_SESSION_EXPIRED", "PXA_DESIGNER_VERIFICATION_REQUIRED", "PXA_ENTITLEMENT_ALLOWED",
            "PXA_ENTITLEMENT_DENIED", "PXA_ENTITLEMENT_EXPIRED", "PXA_ENTITLEMENT_LIMIT_EXCEEDED",
            "PXA_ENTITLEMENT_MISSING", "PXA_ENTITLEMENT_QUANTITY_INVALID", "PXA_GRACE_PERIOD_EXPIRED",
            "PXA_LICENSE_DEPLOYMENT_MISMATCH", "PXA_LICENSE_DEPLOYMENT_NOT_ALLOWED",
            "PXA_LICENSE_ENVELOPE_MISMATCH", "PXA_LICENSE_EXPIRED", "PXA_LICENSE_INACTIVE",
            "PXA_LICENSE_INSTANCE_LIMIT_EXCEEDED", "PXA_LICENSE_MALFORMED", "PXA_LICENSE_NOT_YET_VALID",
            "PXA_LICENSE_REPLACED", "PXA_LICENSE_REVOKED", "PXA_LICENSE_SCHEMA_UPGRADE_REQUIRED",
            "PXA_LICENSE_SIGNATURE_INVALID", "PXA_LICENSE_SIGNING_METADATA_INVALID",
            "PXA_LICENSE_SUBSCRIPTION_MISMATCH", "PXA_LICENSE_VALID", "PXA_LICENSE_VERSION_INCOMPATIBLE",
            "PXA_ORGANIZATION_INACTIVE", "PXA_ORGANIZATION_REQUIRED", "PXA_SUBSCRIPTION_CANCELLED",
            "PXA_SUBSCRIPTION_INACTIVE", "PXA_SUBSCRIPTION_MISSING", "PXA_TRIAL_EXPIRED",
            "PXA_USAGE_ALREADY_RECORDED", "PXA_USAGE_INVALID", "PXA_USAGE_RECORDED",
        ];

        private static string FormerCodeArray => string.Join(", ", FormerCodes.Select(code => $"'{code}'"));

        // Rewrites exact codes in string array elements, array object "Code" fields, object
        // "diagnosticCodes" elements and whole FailureReason values; everything else is left untouched.
        private static string RewriteJobCodes(bool toCurrent) => $$"""
            CREATE TEMPORARY TABLE pvad_job_code_map (source text PRIMARY KEY, target text NOT NULL) ON COMMIT DROP;
            INSERT INTO pvad_job_code_map (source, target)
            SELECT {{(toCurrent ? "code, 'PVAD' || substring(code from 4)" : "'PVAD' || substring(code from 4), code")}}
            FROM unnest(ARRAY[{{FormerCodeArray}}]) AS code;

            CREATE TEMPORARY TABLE pvad_job_rewrite ON COMMIT DROP AS
            SELECT job."Id",
                CASE
                    WHEN jsonb_typeof(job."DiagnosticsJson") = 'array' THEN (
                        SELECT coalesce(jsonb_agg(CASE
                            WHEN jsonb_typeof(item) = 'string' AND code.target IS NOT NULL THEN to_jsonb(code.target)
                            WHEN jsonb_typeof(item) = 'object' AND item ? 'Code' AND codeField.target IS NOT NULL
                                THEN jsonb_set(item, '{Code}', to_jsonb(codeField.target))
                            ELSE item END ORDER BY ordinal), '[]'::jsonb)
                        FROM jsonb_array_elements(job."DiagnosticsJson") WITH ORDINALITY AS element(item, ordinal)
                        LEFT JOIN pvad_job_code_map code ON jsonb_typeof(item) = 'string' AND code.source = item #>> '{}'
                        LEFT JOIN pvad_job_code_map codeField ON jsonb_typeof(item) = 'object' AND codeField.source = item ->> 'Code')
                    WHEN jsonb_typeof(job."DiagnosticsJson") = 'object'
                        AND jsonb_typeof(job."DiagnosticsJson" -> 'diagnosticCodes') = 'array' THEN jsonb_set(
                        job."DiagnosticsJson", '{diagnosticCodes}', (
                        SELECT coalesce(jsonb_agg(CASE WHEN code.target IS NOT NULL THEN to_jsonb(code.target) ELSE item END
                            ORDER BY ordinal), '[]'::jsonb)
                        FROM jsonb_array_elements(job."DiagnosticsJson" -> 'diagnosticCodes') WITH ORDINALITY AS element(item, ordinal)
                        LEFT JOIN pvad_job_code_map code ON jsonb_typeof(item) = 'string' AND code.source = item #>> '{}'))
                    ELSE job."DiagnosticsJson" END AS "DiagnosticsJson",
                coalesce(failure.target, job."FailureReason") AS "FailureReason"
            FROM administration.background_jobs job
            LEFT JOIN pvad_job_code_map failure ON failure.source = job."FailureReason";

            UPDATE administration.background_jobs job
            SET "DiagnosticsJson" = rewrite."DiagnosticsJson", "FailureReason" = rewrite."FailureReason"
            FROM pvad_job_rewrite rewrite
            WHERE rewrite."Id" = job."Id"
              AND (rewrite."DiagnosticsJson" IS DISTINCT FROM job."DiagnosticsJson"
                OR rewrite."FailureReason" IS DISTINCT FROM job."FailureReason");
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop former claims that already have an identical canonical counterpart, then rename the rest.
            migrationBuilder.Sql("""
                DELETE FROM identity.role_claims former
                USING identity.role_claims current
                WHERE former."ClaimType" = 'pxa_permission' AND current."ClaimType" = 'pvad_permission'
                  AND former."RoleId" = current."RoleId" AND former."ClaimValue" = current."ClaimValue";
                UPDATE identity.role_claims SET "ClaimType" = 'pvad_' || substring("ClaimType" from 5)
                WHERE "ClaimType" LIKE 'pxa\_%';
                UPDATE identity.user_claims SET "ClaimType" = 'pvad_' || substring("ClaimType" from 5)
                WHERE "ClaimType" LIKE 'pxa\_%';
                UPDATE identity.roles SET "Description" = replace("Description", 'Built-in PXA ', 'Built-in PVAD ')
                WHERE "Description" LIKE 'Built-in PXA %';
                """);

            migrationBuilder.Sql("""
                DELETE FROM identity.oauth_applications former
                WHERE former."ClientId" IN ('pxa-mcp-public', 'pxa-mcp-server')
                  AND EXISTS (SELECT 1 FROM identity.oauth_applications current
                              WHERE current."ClientId" = 'pvad' || substring(former."ClientId" from 4));
                UPDATE identity.oauth_applications
                SET "ClientId" = 'pvad' || substring("ClientId" from 4),
                    "DisplayName" = replace("DisplayName", 'PXA MCP', 'PVADE MCP'),
                    "Permissions" = replace("Permissions", '"scp:pxa.', '"scp:pvad.')
                WHERE "ClientId" IN ('pxa-mcp-public', 'pxa-mcp-server') OR "ClientId" LIKE 'pxa-sa-%';
                UPDATE administration.api_keys SET "OAuthClientId" = 'pvad' || substring("OAuthClientId" from 4)
                WHERE "OAuthClientId" LIKE 'pxa-sa-%';
                """);

            // Only exact former diagnostic codes in known code fields change. Messages, file names,
            // source excerpts and unknown values stay byte-identical. No copy of job content is kept,
            // so job retention and deletion continue to govern all diagnostics.
            migrationBuilder.Sql(RewriteJobCodes(toCurrent: true));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback applies the exact inverse map to the same code fields of the jobs that exist now.
            // Newer processing results keep their content, purged or deleted content stays gone, and
            // a code that was already current before Up is also returned to its former value.
            migrationBuilder.Sql(RewriteJobCodes(toCurrent: false));
            migrationBuilder.Sql("""
                UPDATE identity.role_claims SET "ClaimType" = 'pxa_' || substring("ClaimType" from 6)
                WHERE "ClaimType" LIKE 'pvad\_%';
                UPDATE identity.user_claims SET "ClaimType" = 'pxa_' || substring("ClaimType" from 6)
                WHERE "ClaimType" LIKE 'pvad\_%';
                UPDATE identity.roles SET "Description" = replace("Description", 'Built-in PVAD ', 'Built-in PXA ')
                WHERE "Description" LIKE 'Built-in PVAD %';
                UPDATE identity.oauth_applications
                SET "ClientId" = 'pxa' || substring("ClientId" from 5),
                    "DisplayName" = replace("DisplayName", 'PVADE MCP', 'PXA MCP'),
                    "Permissions" = replace("Permissions", '"scp:pvad.', '"scp:pxa.')
                WHERE "ClientId" IN ('pvad-mcp-public', 'pvad-mcp-server') OR "ClientId" LIKE 'pvad-sa-%';
                UPDATE administration.api_keys SET "OAuthClientId" = 'pxa' || substring("OAuthClientId" from 5)
                WHERE "OAuthClientId" LIKE 'pvad-sa-%';
                """);
        }
    }
}
