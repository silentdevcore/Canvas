namespace PXA.WebApi.Security;

public static class PxaDesignerPermissions
{
    public const string Access = "designer.access";
    public const string TemplatesRead = "designer.templates.read";
    public const string TemplatesCreate = "designer.templates.create";
    public const string TemplatesEdit = "designer.templates.edit";
    public const string TemplatesArchive = "designer.templates.archive";
    public const string TemplatesVersion = "designer.templates.version";
    public const string TemplatesPublish = "designer.templates.publish";
    public const string AssetsRead = "designer.assets.read";
    public const string AssetsManage = "designer.assets.manage";
    public const string CodeRead = "designer.code.read";
    public const string CodeEdit = "designer.code.edit";
    public const string CodeExecute = "designer.code.execute";
    public const string CodeApply = "designer.code.apply";
    public const string PreferencesManage = "designer.preferences.manage";
    public const string NotificationsRead = "designer.notifications.read";
    public const string JobsRead = "designer.jobs.read";
    public const string JobsCancel = "designer.jobs.cancel";
    public const string SymbolsRender = "designer.symbols.render";
    public const string ExperimentalFeatures = "designer.features.experimental";
    public const string FeaturesManage = "designer.features.manage";
    public const string PdfAccess = "designer.pdf.access";
    public const string PdfCreate = "designer.pdf.create";
    public const string PdfEdit = "designer.pdf.edit";
    public const string PdfTemplatesRead = "designer.pdf.templates.read";
    public const string PdfImport = "designer.pdf.import";
    public const string PdfConvert = "designer.pdf.convert";
    public const string PdfExport = "designer.pdf.export";
    public const string PdfPrint = "designer.pdf.print";
    public const string PdfTrustInspect = "designer.pdf.trust.inspect";
    public const string PdfComplianceValidate = "designer.pdf.compliance.validate";
    public const string PdfComplianceConvert = "designer.pdf.compliance.convert";
    public const string PdfSignaturesValidate = "designer.pdf.signatures.validate";
    public const string PdfSignaturesSign = "designer.pdf.signatures.sign";
    public const string PdfSignatureTrustPoliciesManage = "designer.pdf.signatures.trust-policies.manage";
    public const string PdfSignatureKeyProvidersUse = "designer.pdf.signatures.key-providers.use";
    public const string PdfSignatureKeyProvidersManage = "designer.pdf.signatures.key-providers.manage";
    public const string PdfRedactionsInspect = "designer.pdf.redactions.inspect";
    public const string PdfRedactionsApply = "designer.pdf.redactions.apply";
    public const string PdfEInvoicesRead = "designer.pdf.e-invoices.read";
    public const string PdfEInvoicesValidate = "designer.pdf.e-invoices.validate";
    public const string PdfEInvoicesGenerate = "designer.pdf.e-invoices.generate";
    public const string PdfEInvoicesCreate = "designer.pdf.e-invoices.create";
    public const string DocumentEdit = "designer.document.edit";
    public const string Import = "designer.import";
    public const string Export = "designer.export";
    public const string MigrationRead = "designer.migration.read";
    public const string MigrationExecute = "designer.migration.execute";
    public const string MigrationPdfRead = "designer.migration.pdf.read";
    public const string MigrationPdfExecute = "designer.migration.pdf.execute";
    public const string MigrationSpreadsheetRead = "designer.migration.spreadsheet.read";
    public const string MigrationSpreadsheetExecute = "designer.migration.spreadsheet.execute";
    public const string MigrationReportRead = "designer.migration.report.read";
    public const string MigrationReportExecute = "designer.migration.report.execute";
    public const string SpreadsheetAccess = "designer.spreadsheet.access";
    public const string SpreadsheetCreate = "designer.spreadsheet.create";
    public const string SpreadsheetRead = "designer.spreadsheet.read";
    public const string SpreadsheetEdit = "designer.spreadsheet.edit";
    public const string SpreadsheetImport = "designer.spreadsheet.import";
    public const string SpreadsheetExport = "designer.spreadsheet.export";
    public const string PdfViewerRead = "designer.pdf-viewer.read";
    public const string PdfViewerAnnotate = "designer.pdf-viewer.annotate";
    public const string PdfViewerForms = "designer.pdf-viewer.forms";
    public const string PdfViewerRedact = "designer.pdf-viewer.redact";

    public static IReadOnlyList<string> All { get; } =
    [
        Access,
        TemplatesRead,
        TemplatesCreate,
        TemplatesEdit,
        TemplatesArchive,
        TemplatesVersion,
        TemplatesPublish,
        AssetsRead,
        AssetsManage,
        CodeRead,
        CodeEdit,
        CodeExecute,
        CodeApply,
        PreferencesManage,
        NotificationsRead,
        JobsRead,
        JobsCancel,
        SymbolsRender,
        ExperimentalFeatures,
        FeaturesManage,
        PdfAccess,
        PdfCreate,
        PdfEdit,
        PdfTemplatesRead,
        PdfImport,
        PdfConvert,
        PdfExport,
        PdfPrint,
        PdfTrustInspect,
        PdfComplianceValidate,
        PdfComplianceConvert,
        PdfSignaturesValidate,
        PdfSignaturesSign,
        PdfSignatureTrustPoliciesManage,
        PdfSignatureKeyProvidersUse,
        PdfSignatureKeyProvidersManage,
        PdfRedactionsInspect,
        PdfRedactionsApply,
        PdfEInvoicesRead,
        PdfEInvoicesValidate,
        PdfEInvoicesGenerate,
        PdfEInvoicesCreate,
        DocumentEdit,
        Import,
        Export,
        MigrationRead,
        MigrationExecute,
        MigrationPdfRead,
        MigrationPdfExecute,
        MigrationSpreadsheetRead,
        MigrationSpreadsheetExecute,
        MigrationReportRead,
        MigrationReportExecute,
        SpreadsheetAccess,
        SpreadsheetCreate,
        SpreadsheetRead,
        SpreadsheetEdit,
        SpreadsheetImport,
        SpreadsheetExport,
        PdfViewerRead,
        PdfViewerAnnotate,
        PdfViewerForms,
        PdfViewerRedact,
        .. PxaDesignerMigrationPermissions.All,
    ];
}

public static class PxaDesignerPermissionResolver
{
    public static string Resolve(PathString path, string method)
    {
        var value = (path.Value ?? string.Empty).TrimEnd('/').ToLowerInvariant();
        var normalizedMethod = method.ToUpperInvariant();

        if (StartsWith(value, "/api/export") || StartsWith(value, "/api/pxa/export"))
            return PxaDesignerPermissions.PdfExport;

        if (StartsWith(value, "/api/pxa/v1/document-jobs"))
        {
            if (value.EndsWith("/import", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfImport;
            if (value.EndsWith("/export", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfExport;
            if (value.EndsWith("/code-migration", StringComparison.Ordinal))
                return PxaDesignerPermissions.MigrationExecute;
        }

        if (StartsWith(value, "/api/pxa/v1/jobs"))
            return value.EndsWith("/cancel", StringComparison.Ordinal)
                ? PxaDesignerPermissions.JobsCancel
                : PxaDesignerPermissions.JobsRead;

        if (StartsWith(value, "/api/pxa/v1/migrations"))
        {
            var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 4 || string.Equals(segments.ElementAtOrDefault(4), "catalog", StringComparison.Ordinal))
                return PxaDesignerPermissions.MigrationRead;

            var domain = segments.Length > 4 ? segments[4] : string.Empty;
            var readOnly = normalizedMethod is "GET" or "HEAD" or "OPTIONS";
            return domain switch
            {
                "spreadsheet" => readOnly
                    ? PxaDesignerPermissions.MigrationSpreadsheetRead
                    : PxaDesignerPermissions.MigrationSpreadsheetExecute,
                "report" => readOnly
                    ? PxaDesignerPermissions.MigrationReportRead
                    : PxaDesignerPermissions.MigrationReportExecute,
                _ => readOnly
                    ? PxaDesignerPermissions.MigrationPdfRead
                    : PxaDesignerPermissions.MigrationPdfExecute
            };
        }

        if (StartsWith(value, "/api/pxa/v1/pdf/compliance"))
            return value.EndsWith("/convert", StringComparison.Ordinal)
                ? PxaDesignerPermissions.PdfComplianceConvert
                : PxaDesignerPermissions.PdfComplianceValidate;

        if (StartsWith(value, "/api/pxa/v1/pdf/trust/policies"))
            return PxaDesignerPermissions.PdfSignatureTrustPoliciesManage;

        if (StartsWith(value, "/api/pxa/v1/pdf/trust/key-providers"))
            return PxaDesignerPermissions.PdfSignatureKeyProvidersManage;

        if (StartsWith(value, "/api/pxa/v1/pdf/trust"))
            return PxaDesignerPermissions.PdfTrustInspect;

        if (StartsWith(value, "/api/pxa/v1/pdf/redactions"))
            return value.EndsWith("/apply", StringComparison.Ordinal)
                ? PxaDesignerPermissions.PdfRedactionsApply
                : PxaDesignerPermissions.PdfRedactionsInspect;

        if (StartsWith(value, "/api/pxa/v1/pdf/e-invoices"))
            return value.EndsWith("/validate", StringComparison.Ordinal)
                ? PxaDesignerPermissions.PdfEInvoicesValidate
                : value.EndsWith("/generate", StringComparison.Ordinal)
                    ? PxaDesignerPermissions.PdfEInvoicesGenerate
                    : value.EndsWith("/create", StringComparison.Ordinal)
                        ? PxaDesignerPermissions.PdfEInvoicesCreate
                        : PxaDesignerPermissions.PdfEInvoicesRead;

        if (StartsWith(value, "/api/pxa/v1/pdf/signatures"))
            return value.EndsWith("/validate", StringComparison.Ordinal)
                ? PxaDesignerPermissions.PdfSignaturesValidate
                : PxaDesignerPermissions.PdfSignaturesSign;

        if (StartsWith(value, "/api/document") || StartsWith(value, "/api/pxa/document"))
        {
            if (value.Contains("/import-", StringComparison.Ordinal) ||
                value.EndsWith("/debug-pdf-engine", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfImport;
            if (value.EndsWith("/convert-image-to-pdf", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfConvert;
            if (value.EndsWith("/sign-docx", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfExport;
            return PxaDesignerPermissions.PdfEdit;
        }

        if (StartsWith(value, "/api/migration") || StartsWith(value, "/api/pxa/migration"))
            return PxaDesignerPermissions.Access;

        if (StartsWith(value, "/api/spreadsheet") || StartsWith(value, "/api/pxa/spreadsheet"))
        {
            if (value.EndsWith("/import", StringComparison.Ordinal))
                return PxaDesignerPermissions.SpreadsheetImport;
            if (value.EndsWith("/export", StringComparison.Ordinal) ||
                value.EndsWith("/render", StringComparison.Ordinal))
                return PxaDesignerPermissions.SpreadsheetExport;
            if (value.EndsWith("/validate", StringComparison.Ordinal))
                return PxaDesignerPermissions.SpreadsheetRead;
            return PxaDesignerPermissions.SpreadsheetEdit;
        }

        if (StartsWith(value, "/api/pdf-viewer") || StartsWith(value, "/api/pxa/pdf-viewer"))
        {
            if (value.Contains("/redact", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfViewerRedact;
            if (value.Contains("/forms/fill", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfViewerForms;
            if (normalizedMethod == "GET" ||
                value.EndsWith("/extract", StringComparison.Ordinal) ||
                value.Contains("/forms/extract", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfViewerRead;
            return PxaDesignerPermissions.PdfViewerAnnotate;
        }

        if (StartsWith(value, "/api/templates") || StartsWith(value, "/api/pxa/templates"))
        {
            if (value.Contains("/csharp", StringComparison.Ordinal))
                return PxaDesignerPermissions.CodeExecute;
            if (value.Contains("/render", StringComparison.Ordinal) ||
                value.EndsWith("/validate", StringComparison.Ordinal))
                return PxaDesignerPermissions.PdfExport;
            if (normalizedMethod == "GET")
                return PxaDesignerPermissions.PdfTemplatesRead;
            if (normalizedMethod == "POST" &&
                (value == "/api/templates" || value == "/api/pxa/templates"))
                return PxaDesignerPermissions.PdfCreate;
            return PxaDesignerPermissions.PdfEdit;
        }

        return PxaDesignerPermissions.Access;
    }

    private static bool StartsWith(string path, string prefix) =>
        path == prefix || path.StartsWith($"{prefix}/", StringComparison.Ordinal);
}
