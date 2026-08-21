# Code Designer Workspace

The Code workspace connects a saved Designer template to four independent source drafts:

- **JSON** is the normalized `DesignExportDto` document.
- **C# Model** is a readable object initializer made from `DesignExportDto`, `PageDto`, `ElementDto`,
  and their typed child contracts. It contains no hidden document payload.
- **C# PDF** uses the semantic `PxaPdfCodeBuilder` and returns a `PxaPdfCodeDocument`. The legacy
  low-level `PdfDocument` API remains supported for one major version with reduced fidelity.
- **FromBase64String** is the compact transport representation. It validates Base64, strict UTF-8,
  JSON, stable IDs, and the same 10 MiB canonical document limit before it can be applied.

Changing tabs never converts or replaces another draft. Use **Convert**, inspect the structural comparison and diagnostics, and then choose **Apply generated draft**. A generated draft remains separate until it is saved or applied.

## Workflow

1. Save the visual template so it has a tenant-owned template ID.
2. Open **Code** and select JSON, C# Model, C# PDF, or FromBase64String.
3. Choose **Validate** for syntax and contract diagnostics.
4. Choose **Run** to create the canonical preview. C# PDF also returns the actual backend PDF.
5. Choose a target language and **Convert**.
6. Review added, changed, and removed elements plus the fidelity result.
7. Review both document fidelity and source preservation, then accept the generated target draft.
8. Choose **Apply to Designer** to update the visual template atomically.

Autosave stores only the active language after a two-second pause. A stale workspace or template revision returns HTTP 409 and does not overwrite either side.

## Fidelity and source preservation

| Value | Meaning |
| --- | --- |
| `exact` | The canonical document is preserved without a known semantic change. |
| `compatible` | The visual result is supported, but original C# structure such as variables or control flow is not reconstructed. |
| `reviewRequired` | The conversion completed with simplifications or unsupported visual operations. |
| `unsupported` | Errors prevent a safe canonical result. |

Generated C# PDF includes `pxa-element-id` metadata comments. Source maps use these IDs to associate diagnostics and generated lines with Designer elements. Executed loops and conditions produce their resulting elements; their original source structure is not represented in the visual model.

`documentFidelity` describes the resulting document. `sourcePreservation` independently reports
`preserved`, `regenerated`, or `structureLost`. Converting handwritten C# can therefore produce an
exact document while correctly reporting that comments, variable names, loops, or conditions were
regenerated. The older `fidelity` property remains an alias for `documentFidelity` during the
compatibility period.

PXA-generated JSON, C# Model, C# PDF builder code, and FromBase64String code roundtrip through the
same normalized `DesignExportDto`. Pages, settings, bindings, localization, RTL, charts, tables,
forms, tenant asset IDs, encryption settings, and unknown extension properties remain part of that
canonical contract.

## Sandbox

C# runs in `PXA.CodeWorker`, never in the WebApi process. The restricted language supports PXA APIs, collections, mathematics, dates, conditions, loops, and bounded string or LINQ operations. It rejects:

- files, directories, environment variables, processes, and additional assemblies;
- network clients, sockets, URLs, and package references;
- reflection, native interop, `unsafe`, pointers, `dynamic`, and preprocessor directives;
- manually created threads and unbounded worker execution.

Execution is limited to 15 seconds, 32 MiB of source, 10 MiB of decoded canonical design,
configured page and element counts, and bounded output. The larger source limit is required because
a readable object initializer is larger than its JSON document. Production enables the feature only
when the worker deployment is marked as hardened with a non-privileged identity, read-only
filesystem, temporary working directory, resource limits, and blocked egress.

## Legacy Base64 migration

Older workspaces may contain `FromBase64String` code in the C# Model draft. On first load, PXA moves
that source unchanged into the FromBase64String draft and generates a readable C# Model initializer
from the canonical JSON. The migration is tenant-scoped, revisioned, and audited; the original source
is not discarded.

## Assets

Code cannot load images from a local path or URL. Upload assets through the Designer asset API and use tenant-bound asset IDs. Imported or generated images remain inaccessible to other organizations. If a PDF operation cannot preserve its asset reference as an editable element, the conversion reports `reviewRequired` rather than inventing content.

## API

The authenticated endpoints are rooted at:

`/api/pxa/v1/designer/templates/{templateId}/code-workspace`

They provide workspace load/save, validation, conversion, execution, atomic apply, restore, and source-map retrieval. Every operation requires a Designer session, active organization, Designer entitlement, the `designer.code-workspace` Beta feature, and applicable request limits.

The old `/api/templates/csharp-*` endpoints remain protected compatibility adapters for one major release. They delegate to the same isolated worker and return deprecation headers.
