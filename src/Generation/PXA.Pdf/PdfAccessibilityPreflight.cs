using PXA.Pdf.Layout;
using System.Text.RegularExpressions;

namespace PXA.Pdf;

/// <summary>Structural preflight for PXA tagged PDF and PDF/UA-1 generation.</summary>
public static class PdfAccessibilityPreflight
{
    public const string ValidatorName = "PXA PDF/UA-1 generator preflight 1.0";
    private static readonly Regex LanguageTagPattern = new(
        "^(?:[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*|[ix](?:-[A-Za-z0-9]{1,8})+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static PdfAccessibilityValidationReport Validate(PdfDocument document, PdfSaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<PdfAccessibilityDiagnostic>();
        var profile = options.AccessibilityProfile;
        if (profile == PdfAccessibilityProfile.None)
            return new(profile, true, ValidatorName, diagnostics);

        if (profile != PdfAccessibilityProfile.PdfUa1)
            Add("PXAPDFUA001", "The requested accessibility profile is not supported.");
        if (string.IsNullOrWhiteSpace(document.Info.Title))
            Add("PXAPDFUA002", "PDF/UA-1 requires a document title.");
        if (string.IsNullOrWhiteSpace(document.Language))
            Add("PXAPDFUA003", "PDF/UA-1 requires a BCP 47 document language.");
        else if (!LanguageTagPattern.IsMatch(document.Language))
            Add("PXAPDFUA015", "The document language must be a valid BCP 47 language tag.");
        if (options.Encryption is not null || options.CertificateEncryption is not null)
            Add("PXAPDFUA004", "Encrypted PDF/UA output is not supported by this writer milestone.");

        var ids = new Dictionary<string, PdfAccessibilityProperties>(StringComparer.Ordinal);
        var semanticPages = new Dictionary<PdfAccessibilityProperties, int>(ReferenceEqualityComparer.Instance);
        for (var pageIndex = 0; pageIndex < document.Pages.Count; pageIndex++)
        {
            var page = document.Pages[pageIndex];
            foreach (var element in page.Elements)
            {
                if (element.IsArtifact)
                    continue;
                var semantic = element.Accessibility;
                if (semantic is null)
                {
                    Add("PXAPDFUA005", "Visible content must be tagged or explicitly marked as an artifact.", pageIndex + 1);
                    continue;
                }

                if (element is TextElement text && text.EmbeddedFont is null)
                    Add("PXAPDFUA006", "Text fonts must be embedded for reliable Unicode extraction.", pageIndex + 1, semantic.ElementId);
            }

            foreach (var semantic in page.SemanticElements)
            {
                if (!semanticPages.TryAdd(semantic, pageIndex + 1))
                    continue;
                if (semantic.Role == PdfStructureRole.Figure && string.IsNullOrWhiteSpace(semantic.AlternateText))
                    Add("PXAPDFUA007", "A meaningful Figure requires alternate text.", pageIndex + 1, semantic.ElementId);
                if (semantic.ElementId is { Length: > 0 } id)
                {
                    if (ids.TryGetValue(id, out var existing) && !ReferenceEquals(existing, semantic))
                        Add("PXAPDFUA008", $"Semantic element ID '{id}' is duplicated.", pageIndex + 1, id);
                    else
                        ids[id] = semantic;
                }
                if (semantic.Role == PdfStructureRole.TH && semantic.TableHeaderScope == PdfTableHeaderScope.None)
                    Add("PXAPDFUA009", "A table header must define row or column scope.", pageIndex + 1, semantic.ElementId);
            }

            foreach (var link in page.LinkAnnotations)
                ValidateAnnotation(link, PdfStructureRole.Link, "PXAPDFUA017", "A link requires meaningful accessible text.", pageIndex + 1);
            foreach (var field in page.ComboBoxAnnotations.Cast<IPdfAccessibleAnnotation>()
                         .Concat(page.MultilineTextFields)
                         .Concat(page.TextFields)
                         .Concat(page.CheckBoxAnnotations))
                ValidateAnnotation(field, PdfStructureRole.Form, "PXAPDFUA018", "A form control requires an accessible label.", pageIndex + 1);
            if (page.ReviewAnnotations.Count > 0)
                Add("PXAPDFUA010", "Review annotations require dedicated structure-parent support and are not yet accepted for PDF/UA-1 export.", pageIndex + 1);
        }

        foreach (var item in semanticPages)
        {
            var semantic = item.Key;
            var page = item.Value;
            if (semantic?.ParentElementId is { Length: > 0 } parentId)
            {
                if (semantic.ElementId == parentId)
                    Add("PXAPDFUA012", "A semantic element cannot be its own parent.", page, semantic.ElementId);
                else if (!ids.ContainsKey(parentId))
                    Add("PXAPDFUA013", $"Semantic parent '{parentId}' does not identify an element.", page, semantic.ElementId);
            }
            if (semantic?.TableHeaderIds is not { Count: > 0 })
                continue;
            foreach (var headerId in semantic.TableHeaderIds.Where(headerId => !ids.ContainsKey(headerId)))
                Add("PXAPDFUA011", $"Table header reference '{headerId}' does not identify a semantic element.", page, semantic.ElementId);
        }

        var annotationSemantics = document.Pages
            .SelectMany(page => page.LinkAnnotations.Cast<IPdfAccessibleAnnotation>()
                .Concat(page.ComboBoxAnnotations)
                .Concat(page.MultilineTextFields)
                .Concat(page.TextFields)
                .Concat(page.CheckBoxAnnotations))
            .Where(annotation => annotation.Accessibility is not null)
            .Select(annotation => annotation.Accessibility!)
            .ToHashSet(ReferenceEqualityComparer.Instance);

        foreach (var semantic in semanticPages.Keys)
        {
            var visited = new HashSet<PdfAccessibilityProperties>(ReferenceEqualityComparer.Instance);
            var current = semantic;
            while (current.ParentElementId is { Length: > 0 } parentId && ids.TryGetValue(parentId, out var parent))
            {
                if (!visited.Add(parent))
                {
                    Add("PXAPDFUA014", "The semantic parent hierarchy contains a cycle.", element: semantic.ElementId);
                    break;
                }
                current = parent;
            }

            if (semantic.Role == PdfStructureRole.Table)
            {
                if (string.IsNullOrWhiteSpace(semantic.ElementId))
                    Add("PXAPDFUA020", "A table requires a stable semantic element ID.", semanticPages[semantic]);
                else
                {
                    var rows = semanticPages.Keys.Where(candidate =>
                        candidate.Role == PdfStructureRole.TR && candidate.ParentElementId == semantic.ElementId).ToList();
                    if (rows.Count == 0 || rows.Any(row => string.IsNullOrWhiteSpace(row.ElementId)
                            || !semanticPages.Keys.Any(cell =>
                                cell.ParentElementId == row.ElementId && cell.Role is PdfStructureRole.TH or PdfStructureRole.TD)))
                        Add("PXAPDFUA020", "A table must contain TR rows with TH or TD cells.", semanticPages[semantic], semantic.ElementId);
                }
            }
            if (semantic.Role is PdfStructureRole.Link or PdfStructureRole.Form
                && !annotationSemantics.Contains(semantic))
                Add("PXAPDFUA010", $"The {semantic.Role} structure element is not associated with an annotation.",
                    semanticPages[semantic], semantic.ElementId);
        }

        if (diagnostics.Count == 0)
            diagnostics.Add(new("PXAPDFUA000", PdfAccessibilityDiagnosticSeverity.Information,
                "The document satisfies the PXA structural generator preflight for PDF/UA-1."));

        return new(profile, diagnostics.All(d => d.Severity != PdfAccessibilityDiagnosticSeverity.Error), ValidatorName, diagnostics);

        void Add(string code, string message, int? page = null, string? element = null) =>
            diagnostics.Add(new(code, PdfAccessibilityDiagnosticSeverity.Error, message, page, element));

        void ValidateAnnotation(
            IPdfAccessibleAnnotation annotation,
            PdfStructureRole expectedRole,
            string labelCode,
            string labelMessage,
            int page)
        {
            var semantic = annotation.Accessibility;
            if (semantic is null || semantic.Role != expectedRole)
            {
                Add("PXAPDFUA010", $"The annotation must be associated with a {expectedRole} structure element.", page,
                    semantic?.ElementId);
                return;
            }

            if (string.IsNullOrWhiteSpace(semantic.AlternateText) && string.IsNullOrWhiteSpace(semantic.ActualText))
                Add(labelCode, labelMessage, page, semantic.ElementId);
        }
    }
}
