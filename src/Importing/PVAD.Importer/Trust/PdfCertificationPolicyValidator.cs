using System.Text;
using PVAD.Importer.Objects;
using PVAD.Importer.Parsing;
using PVAD.Importer.Xref;

namespace PVAD.Importer.Trust;

/// <summary>
/// Enforces DocMDP certification permissions and FieldMDP field locks for every change that a
/// later incremental revision applied to a certified document.
/// </summary>
internal static class PdfCertificationPolicyValidator
{
    private const int MaximumReportedChanges = 32;
    private const int MaximumReportedLockedFields = 64;
    private const int MaximumTraversalDepth = 64;
    private const int MaximumValidationMaterialObjects = 4096;

    private static readonly string[] CatalogKeysAllowedWhenLocked = ["DSS", "Extensions"];
    private static readonly string[] CatalogKeysAllowedWhenOpen =
        ["AcroForm", "Perms", "DSS", "Extensions", "Metadata", "Version"];
    private static readonly string[] PageKeysAllowedWhenOpen = ["Annots"];
    private static readonly string[] FieldKeysAllowedWhenOpen = ["V", "AP", "AS", "DV"];

    public static IReadOnlyList<PdfSignatureInspection> Apply(
        byte[] source,
        PdfObjectGraph? graph,
        PdfCrossReferenceTable? xref,
        IReadOnlyList<PdfSignatureInspection> signatures,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        if (graph is null || xref is null || signatures.Count == 0)
        {
            return signatures;
        }

        if (!TryResolveCertification(graph, signatures, diagnostics, out var certification))
        {
            return signatures;
        }

        var evaluation = Evaluate(source, graph, xref, certification, diagnostics);
        return signatures
            .Select(signature => signature.ObjectNumber == certification.SignatureId.Number &&
                    signature.Generation == certification.SignatureId.Generation
                ? ApplyEvaluation(signature, evaluation)
                : signature)
            .ToArray();
    }

    private static PdfSignatureInspection ApplyEvaluation(
        PdfSignatureInspection signature,
        PdfCertificationInspection evaluation)
    {
        var violated = evaluation.Status == PdfTrustStatus.Invalid ||
            evaluation.FieldLockStatus == PdfTrustStatus.Invalid;
        var status = violated
            ? PdfTrustStatus.Invalid
            : evaluation.Status == PdfTrustStatus.Indeterminate && signature.Status == PdfTrustStatus.Valid
                ? PdfTrustStatus.Indeterminate
                : signature.Status;
        return signature with
        {
            IsCertification = true,
            CertificationStatus = evaluation.Status,
            Certification = evaluation,
            Status = status
        };
    }

    private static bool TryResolveCertification(
        PdfObjectGraph graph,
        IReadOnlyList<PdfSignatureInspection> signatures,
        ICollection<PdfTrustDiagnostic> diagnostics,
        out CertificationContext certification)
    {
        certification = default!;
        var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
        var permsReference = Resolve(graph, catalog?["Perms"]) is PdfDictionary perms
            ? perms["DocMDP"] as PdfReference
            : null;

        var declared = new List<(PdfObjectId Id, PdfDictionary Signature, PdfDictionary TransformParams)>();
        foreach (var indirectObject in graph.Objects.Values.OrderBy(static value => value.Id.Number))
        {
            if (indirectObject.Value is not PdfDictionary dictionary ||
                !TryReadTransformParams(graph, dictionary, "DocMDP", out var transformParams))
            {
                continue;
            }

            declared.Add((indirectObject.Id, dictionary, transformParams));
        }

        if (declared.Count == 0)
        {
            if (permsReference is not null)
            {
                diagnostics.Add(new PdfTrustDiagnostic(
                    "PXAPDFTRUST140",
                    PdfTrustDiagnosticSeverity.Error,
                    "The catalog declares /Perms /DocMDP but no signature carries a DocMDP transform.",
                    permsReference.Id.Number,
                    permsReference.Id.Generation));
            }

            return false;
        }

        var structuralViolations = new List<string>();
        if (declared.Count > 1)
        {
            structuralViolations.Add("The document contains more than one DocMDP certification signature.");
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST141",
                PdfTrustDiagnosticSeverity.Error,
                "The document contains more than one DocMDP certification signature.",
                declared[0].Id.Number,
                declared[0].Id.Generation));
        }

        var (signatureId, signatureDictionary, parameters) = declared[0];
        if (permsReference is null || permsReference.Id != signatureId)
        {
            structuralViolations.Add("The certification signature is not bound by the catalog /Perms /DocMDP entry.");
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST142",
                PdfTrustDiagnosticSeverity.Error,
                "The DocMDP certification signature is not the signature referenced by the catalog /Perms entry.",
                signatureId.Number,
                signatureId.Generation));
        }

        var inspection = signatures.FirstOrDefault(candidate =>
            candidate.ObjectNumber == signatureId.Number && candidate.Generation == signatureId.Generation);
        if (inspection?.SignedRevisionEndOffset is not { } certifiedRevisionEnd)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST143",
                PdfTrustDiagnosticSeverity.Error,
                "The certified revision boundary could not be resolved for the DocMDP signature.",
                signatureId.Number,
                signatureId.Generation));
            return false;
        }

        var earliest = signatures
            .Where(static candidate => candidate.SignedRevisionEndOffset is not null)
            .Min(static candidate => candidate.SignedRevisionEndOffset);
        if (certifiedRevisionEnd != earliest)
        {
            structuralViolations.Add("The certification signature does not cover the first signed revision.");
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST144",
                PdfTrustDiagnosticSeverity.Error,
                "A certification signature must cover the first signed revision of the document.",
                signatureId.Number,
                signatureId.Generation));
        }

        var level = ReadInteger(graph, parameters["P"]) ?? 2;
        if (level is < 1 or > 3)
        {
            structuralViolations.Add($"The DocMDP permission level {level} is invalid.");
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST145",
                PdfTrustDiagnosticSeverity.Error,
                $"The DocMDP transform declares the unsupported permission level {level}.",
                signatureId.Number,
                signatureId.Generation));
            level = 1;
        }

        certification = new CertificationContext(
            signatureId,
            signatureDictionary,
            level,
            certifiedRevisionEnd,
            catalog,
            structuralViolations);
        return true;
    }

    private static PdfCertificationInspection Evaluate(
        byte[] source,
        PdfObjectGraph graph,
        PdfCrossReferenceTable xref,
        CertificationContext certification,
        ICollection<PdfTrustDiagnostic> diagnostics)
    {
        var lockedFields = ResolveLockedFields(graph, certification);
        if (certification.StructuralViolations.Count > 0)
        {
            return new PdfCertificationInspection(
                certification.Level,
                PdfTrustStatus.Invalid,
                lockedFields.Status,
                certification.StructuralViolations.Count,
                certification.StructuralViolations.Take(MaximumReportedChanges).ToArray(),
                lockedFields.Names.Take(MaximumReportedLockedFields).ToArray());
        }

        var before = TryParsePrefix(source, certification.CertifiedRevisionEnd);
        if (before is null)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST146",
                PdfTrustDiagnosticSeverity.Error,
                "The certified revision could not be reparsed, so DocMDP permissions are indeterminate.",
                certification.SignatureId.Number,
                certification.SignatureId.Generation));
            return new PdfCertificationInspection(
                certification.Level,
                PdfTrustStatus.Indeterminate,
                lockedFields.Status == PdfTrustStatus.NotApplicable
                    ? PdfTrustStatus.NotApplicable
                    : PdfTrustStatus.Indeterminate,
                0,
                [],
                lockedFields.Names.Take(MaximumReportedLockedFields).ToArray());
        }

        if (!TryFindChangedObjects(xref, certification.CertifiedRevisionEnd, out var changedIds))
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST149",
                PdfTrustDiagnosticSeverity.Error,
                "The certified revision does not match a parsed cross-reference revision, so DocMDP permissions are indeterminate.",
                certification.SignatureId.Number,
                certification.SignatureId.Generation));
            return new PdfCertificationInspection(
                certification.Level,
                PdfTrustStatus.Indeterminate,
                lockedFields.Status == PdfTrustStatus.NotApplicable
                    ? PdfTrustStatus.NotApplicable
                    : PdfTrustStatus.Indeterminate,
                0,
                [],
                lockedFields.Names.Take(MaximumReportedLockedFields).ToArray());
        }

        var classifier = new ChangeClassifier(graph, before.Value.Graph, certification);
        var violations = new List<string>();
        var violationCount = 0;
        foreach (var id in changedIds.OrderBy(static id => id.Number).ThenBy(static id => id.Generation))
        {
            if (classifier.IsPermitted(id, out var reason))
            {
                continue;
            }

            violationCount++;
            if (violations.Count < MaximumReportedChanges)
            {
                violations.Add(reason);
            }
        }

        var fieldLockStatus = EvaluateFieldLocks(
            graph,
            before.Value.Graph,
            lockedFields,
            violations,
            ref violationCount);

        if (violationCount > 0)
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST147",
                PdfTrustDiagnosticSeverity.Error,
                $"{violationCount} change(s) after the certified revision are not permitted by DocMDP level {certification.Level}.",
                certification.SignatureId.Number,
                certification.SignatureId.Generation));
        }
        else
        {
            diagnostics.Add(new PdfTrustDiagnostic(
                "PXAPDFTRUST148",
                PdfTrustDiagnosticSeverity.Info,
                $"All changes after the certified revision are permitted by DocMDP level {certification.Level}.",
                certification.SignatureId.Number,
                certification.SignatureId.Generation));
        }

        return new PdfCertificationInspection(
            certification.Level,
            violationCount == 0 ? PdfTrustStatus.Valid : PdfTrustStatus.Invalid,
            fieldLockStatus,
            violationCount,
            violations,
            lockedFields.Names.Take(MaximumReportedLockedFields).ToArray());
    }

    private static PdfTrustStatus EvaluateFieldLocks(
        PdfObjectGraph graph,
        PdfObjectGraph before,
        LockedFieldSet lockedFields,
        ICollection<string> violations,
        ref int violationCount)
    {
        if (lockedFields.Status == PdfTrustStatus.NotApplicable)
        {
            return PdfTrustStatus.NotApplicable;
        }

        var currentFields = CollectFields(graph);
        var certifiedFields = CollectFields(before);
        var names = lockedFields.Action is PdfFieldLockAction.All or PdfFieldLockAction.Exclude
            ? certifiedFields.Keys.Union(currentFields.Keys, StringComparer.Ordinal).ToArray()
            : lockedFields.Names.ToArray();

        var locked = 0;
        var breached = false;
        foreach (var name in names.OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (lockedFields.Action == PdfFieldLockAction.Exclude && lockedFields.Names.Contains(name))
            {
                continue;
            }

            locked++;
            certifiedFields.TryGetValue(name, out var certified);
            currentFields.TryGetValue(name, out var current);
            if (certified is null && current is null)
            {
                continue;
            }

            if (certified is null || current is null)
            {
                violationCount++;
                breached = true;
                AddViolation(violations, $"FieldMDP: the locked field '{name}' was added or removed after certification.");
                continue;
            }

            if (!PdfValueComparer.AreEqual(certified["V"], current["V"]))
            {
                violationCount++;
                breached = true;
                AddViolation(violations, $"FieldMDP: the value of the locked field '{name}' changed after certification.");
            }
        }

        return locked == 0
            ? PdfTrustStatus.NotApplicable
            : breached
                ? PdfTrustStatus.Invalid
                : PdfTrustStatus.Valid;
    }

    private static void AddViolation(ICollection<string> violations, string reason)
    {
        if (violations.Count < MaximumReportedChanges)
        {
            violations.Add(reason);
        }
    }

    private static LockedFieldSet ResolveLockedFields(PdfObjectGraph graph, CertificationContext certification)
    {
        if (TryReadTransformParams(graph, certification.Signature, "FieldMDP", out var transformParams))
        {
            return ReadFieldLock(graph, transformParams);
        }

        foreach (var indirectObject in graph.Objects.Values)
        {
            if (indirectObject.Value is not PdfDictionary field ||
                field["V"] is not PdfReference value ||
                value.Id != certification.SignatureId ||
                Resolve(graph, field["Lock"]) is not PdfDictionary fieldLock)
            {
                continue;
            }

            return ReadFieldLock(graph, fieldLock);
        }

        return new LockedFieldSet(PdfFieldLockAction.None, new HashSet<string>(StringComparer.Ordinal), PdfTrustStatus.NotApplicable);
    }

    private static LockedFieldSet ReadFieldLock(PdfObjectGraph graph, PdfDictionary parameters)
    {
        var action = ReadName(graph, parameters["Action"]) switch
        {
            "All" => PdfFieldLockAction.All,
            "Include" => PdfFieldLockAction.Include,
            "Exclude" => PdfFieldLockAction.Exclude,
            _ => PdfFieldLockAction.None
        };
        if (action == PdfFieldLockAction.None)
        {
            return new LockedFieldSet(action, new HashSet<string>(StringComparer.Ordinal), PdfTrustStatus.NotApplicable);
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (Resolve(graph, parameters["Fields"]) is PdfArray fields)
        {
            foreach (var item in fields.Items)
            {
                if (Resolve(graph, item) is PdfString name)
                {
                    names.Add(ReadTextString(name));
                }
            }
        }

        return new LockedFieldSet(action, names, PdfTrustStatus.Valid);
    }

    private static bool TryReadTransformParams(
        PdfObjectGraph graph,
        PdfDictionary signature,
        string transformMethod,
        out PdfDictionary transformParams)
    {
        transformParams = default!;
        if (Resolve(graph, signature["Reference"]) is not PdfArray references)
        {
            return false;
        }

        foreach (var item in references.Items)
        {
            if (Resolve(graph, item) is not PdfDictionary reference ||
                !string.Equals(ReadName(graph, reference["TransformMethod"]), transformMethod, StringComparison.Ordinal))
            {
                continue;
            }

            transformParams = Resolve(graph, reference["TransformParams"]) as PdfDictionary ?? new PdfDictionary();
            return true;
        }

        return false;
    }

    private static bool TryFindChangedObjects(
        PdfCrossReferenceTable xref,
        long certifiedRevisionEnd,
        out HashSet<PdfObjectId> changed)
    {
        changed = [];
        var certifiedRevision = xref.Revisions.FirstOrDefault(revision => revision.EndOffset == certifiedRevisionEnd);
        if (certifiedRevision is null)
        {
            return false;
        }

        foreach (var entry in xref.Entries.Values)
        {
            if (entry.Revision < certifiedRevision.DistanceFromLatest)
            {
                changed.Add(entry.Id);
            }
        }

        return true;
    }

    private static IReadOnlyDictionary<string, PdfDictionary> CollectFields(PdfObjectGraph graph)
    {
        var result = new Dictionary<string, PdfDictionary>(StringComparer.Ordinal);
        if (Resolve(graph, graph.Trailer?["Root"]) is not PdfDictionary catalog ||
            Resolve(graph, catalog["AcroForm"]) is not PdfDictionary acroForm ||
            Resolve(graph, acroForm["Fields"]) is not PdfArray fields)
        {
            return result;
        }

        var visited = new HashSet<PdfObjectId>();
        foreach (var field in fields.Items)
        {
            CollectFields(graph, field, prefix: null, visited, result, depth: 0);
        }

        return result;
    }

    private static void CollectFields(
        PdfObjectGraph graph,
        PdfObject fieldObject,
        string? prefix,
        ISet<PdfObjectId> visited,
        IDictionary<string, PdfDictionary> result,
        int depth)
    {
        if (depth > MaximumTraversalDepth ||
            fieldObject is PdfReference reference && !visited.Add(reference.Id) ||
            Resolve(graph, fieldObject) is not PdfDictionary field)
        {
            return;
        }

        var partial = Resolve(graph, field["T"]) is PdfString name ? ReadTextString(name) : null;
        var qualified = partial is null
            ? prefix
            : prefix is null
                ? partial
                : $"{prefix}.{partial}";
        if (qualified is not null && field["FT"] is not null)
        {
            result[qualified] = field;
        }

        if (Resolve(graph, field["Kids"]) is not PdfArray kids)
        {
            return;
        }

        foreach (var kid in kids.Items)
        {
            CollectFields(graph, kid, qualified, visited, result, depth + 1);
        }
    }

    private static (PdfCrossReferenceTable Table, PdfObjectGraph Graph)? TryParsePrefix(byte[] source, long length)
    {
        if (length <= 0 || length > source.Length)
        {
            return null;
        }

        try
        {
            var prefix = source.AsSpan(0, checked((int)length)).ToArray();
            var context = new PdfParseContext(prefix, new PdfImporterOptions
            {
                LazyObjectLoading = false,
                DeferredStreamDecoding = true,
                ParsePagesInParallel = false,
                MaxParallelPageParsers = 1
            });
            var table = new PdfCrossReferenceParser().Parse(context);
            var graph = new PdfObjectParser(context, table).ParseDocumentGraph();
            return graph.Objects.Count == 0 ? null : (table, graph);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    internal static PdfObject? Resolve(PdfObjectGraph graph, PdfObject? value)
    {
        var visited = new HashSet<PdfObjectId>();
        while (value is PdfReference reference && visited.Add(reference.Id))
        {
            value = graph.Resolve(reference.Id)?.Value;
        }

        return value;
    }

    /// <summary>
    /// Decodes a PDF text string, honouring the UTF-16BE byte order mark that PVAD writes for
    /// non-Latin field names.
    /// </summary>
    private static string ReadTextString(PdfString value)
    {
        var bytes = value.GetDecodedBytes().Span;
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
            ? Encoding.BigEndianUnicode.GetString(bytes[2..])
            : Encoding.Latin1.GetString(bytes);
    }

    private static string? ReadName(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) is PdfName name ? name.Value : null;

    private static int? ReadInteger(PdfObjectGraph graph, PdfObject? value) =>
        Resolve(graph, value) switch
        {
            PdfInteger integer when integer.Value is >= int.MinValue and <= int.MaxValue => (int)integer.Value,
            PdfNumber number when number.Value is >= int.MinValue and <= int.MaxValue => (int)number.Value,
            _ => null
        };

    private enum PdfFieldLockAction
    {
        None,
        All,
        Include,
        Exclude
    }

    private sealed record LockedFieldSet(
        PdfFieldLockAction Action,
        IReadOnlySet<string> Names,
        PdfTrustStatus Status);

    private sealed record CertificationContext(
        PdfObjectId SignatureId,
        PdfDictionary Signature,
        int Level,
        long CertifiedRevisionEnd,
        PdfDictionary? Catalog,
        IReadOnlyList<string> StructuralViolations);

    private sealed class ChangeClassifier
    {
        private readonly PdfObjectGraph _graph;
        private readonly PdfObjectGraph _before;
        private readonly CertificationContext _certification;
        private readonly HashSet<PdfObjectId> _validationMaterial;
        private readonly HashSet<PdfObjectId> _appearances;
        private readonly PdfObjectId? _catalogId;
        private readonly PdfObjectId? _acroFormId;

        public ChangeClassifier(PdfObjectGraph graph, PdfObjectGraph before, CertificationContext certification)
        {
            _graph = graph;
            _before = before;
            _certification = certification;
            _catalogId = (graph.Trailer?["Root"] as PdfReference)?.Id;
            var catalog = Resolve(graph, graph.Trailer?["Root"]) as PdfDictionary;
            _acroFormId = (catalog?["AcroForm"] as PdfReference)?.Id;
            _validationMaterial = CollectReachable(graph, catalog?["DSS"]);
            _appearances = [];
            foreach (var indirectObject in graph.Objects.Values)
            {
                if (indirectObject.Value is PdfDictionary dictionary && IsField(dictionary))
                {
                    foreach (var appearance in CollectReachable(graph, dictionary["AP"]))
                    {
                        _appearances.Add(appearance);
                    }
                }
            }
        }

        public bool IsPermitted(PdfObjectId id, out string reason)
        {
            reason = string.Empty;
            var current = _graph.Resolve(id)?.Value;
            var certified = _before.Resolve(id)?.Value;

            if (_validationMaterial.Contains(id))
            {
                return true;
            }

            if (current is PdfDictionary signature && IsSignature(signature))
            {
                if (IsDocumentTimestamp(signature))
                {
                    return true;
                }

                if (_certification.Level == 1)
                {
                    reason = Describe(id, "an approval signature was added to a document certified with no changes allowed");
                    return false;
                }

                return true;
            }

            if (current is null)
            {
                if (certified is null)
                {
                    return true;
                }

                reason = Describe(id, "an object present in the certified revision was removed");
                return false;
            }

            if (_certification.Level == 1)
            {
                if (id == _catalogId)
                {
                    return OnlyChangedKeys(id, certified, current, CatalogKeysAllowedWhenLocked, "catalog", out reason);
                }

                reason = Describe(id, "the document is certified with no changes allowed");
                return false;
            }

            if (id == _catalogId)
            {
                return OnlyChangedKeys(id, certified, current, CatalogKeysAllowedWhenOpen, "catalog", out reason);
            }

            if (id == _acroFormId)
            {
                return true;
            }

            if (_appearances.Contains(id))
            {
                return true;
            }

            if (current is PdfDictionary dictionary)
            {
                if (IsField(dictionary))
                {
                    if (certified is not null)
                    {
                        return OnlyChangedKeys(id, certified, current, FieldKeysAllowedWhenOpen, "form field", out reason);
                    }

                    if (dictionary["FT"] is PdfName { Value: "Sig" })
                    {
                        return true;
                    }

                    reason = Describe(id, "a new form field that is not a signature field was added");
                    return false;
                }

                if (IsPage(dictionary))
                {
                    return OnlyChangedKeys(id, certified, current, PageKeysAllowedWhenOpen, "page", out reason);
                }

                if (IsAnnotation(dictionary))
                {
                    if (_certification.Level >= 3)
                    {
                        return true;
                    }

                    reason = Describe(id, "annotations may not be created or modified at DocMDP level 2");
                    return false;
                }
            }

            if (certified is null)
            {
                reason = Describe(id, "an object unrelated to form filling or signing was added");
                return false;
            }

            reason = Describe(id, "an object unrelated to form filling or signing was modified");
            return false;
        }

        private bool OnlyChangedKeys(
            PdfObjectId id,
            PdfObject? certified,
            PdfObject current,
            IReadOnlyList<string> allowedKeys,
            string label,
            out string reason)
        {
            reason = string.Empty;
            if (certified is not PdfDictionary before || current is not PdfDictionary after)
            {
                reason = Describe(id, $"the {label} object was replaced by an incompatible object");
                return false;
            }

            var keys = before.Values.Keys.Union(after.Values.Keys, StringComparer.Ordinal);
            foreach (var key in keys)
            {
                if (PdfValueComparer.AreEqual(before[key], after[key]))
                {
                    continue;
                }

                if (allowedKeys.Contains(key, StringComparer.Ordinal))
                {
                    continue;
                }

                reason = Describe(id, $"the {label} entry /{key} changed after certification");
                return false;
            }

            return true;
        }

        private static string Describe(PdfObjectId id, string reason) =>
            $"Object {id.Number} {id.Generation} R: {reason}.";

        private static HashSet<PdfObjectId> CollectReachable(PdfObjectGraph graph, PdfObject? root)
        {
            var result = new HashSet<PdfObjectId>();
            if (root is null)
            {
                return result;
            }

            var pending = new Stack<(PdfObject Value, int Depth)>();
            pending.Push((root, 0));
            while (pending.Count > 0 && result.Count < MaximumValidationMaterialObjects)
            {
                var (value, depth) = pending.Pop();
                if (depth > MaximumTraversalDepth)
                {
                    continue;
                }

                switch (value)
                {
                    case PdfReference reference:
                        if (!result.Add(reference.Id))
                        {
                            continue;
                        }

                        if (graph.Resolve(reference.Id)?.Value is { } resolved)
                        {
                            pending.Push((resolved, depth + 1));
                        }

                        break;
                    case PdfArray array:
                        foreach (var item in array.Items)
                        {
                            pending.Push((item, depth + 1));
                        }

                        break;
                    case PdfDictionary dictionary:
                        foreach (var item in dictionary.Values.Values)
                        {
                            pending.Push((item, depth + 1));
                        }

                        break;
                    case PdfStreamObject stream:
                        pending.Push((stream.Dictionary, depth + 1));
                        break;
                }
            }

            return result;
        }

        private static bool IsSignature(PdfDictionary dictionary) =>
            dictionary["Type"] is PdfName { Value: "Sig" or "DocTimeStamp" } ||
            dictionary["ByteRange"] is not null && dictionary["Contents"] is not null;

        private static bool IsDocumentTimestamp(PdfDictionary dictionary) =>
            dictionary["Type"] is PdfName { Value: "DocTimeStamp" } ||
            dictionary["SubFilter"] is PdfName { Value: "ETSI.RFC3161" };

        private static bool IsField(PdfDictionary dictionary) => dictionary["FT"] is not null;

        private static bool IsPage(PdfDictionary dictionary) => dictionary["Type"] is PdfName { Value: "Page" };

        private static bool IsAnnotation(PdfDictionary dictionary) =>
            dictionary["Type"] is PdfName { Value: "Annot" } || dictionary["Subtype"] is PdfName { Value: "Widget" };
    }
}
