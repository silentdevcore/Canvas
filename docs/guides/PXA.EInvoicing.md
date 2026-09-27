# PXA Electronic Invoicing

PXA produces, embeds, reads and validates electronic-invoice documents. It reports what it
actually established — structural recognition, PXA business validation and official conformance
are three separate states — and never presents one as another.

## What PXA is not

Peppol transport, national tax submission, accounting, payment and legal retention are
**external integrations, not PDF-engine capabilities**. PXA hands you an invoice document and an
honest account of what it checked; sending it to an access point, filing it with a tax authority,
posting it to a ledger, settling it, or retaining it for a statutory period are things your own
systems or your service providers do. PXA deliberately has no API for any of them, because owning
the document format does not make it safe to imply ownership of those obligations.

## Supported versions and profiles

The first maintained versions are the ones below. A request that names any other version is
rejected with `PXAEINV012` rather than silently accepted.

| Standard | Version | Syntax | Profiles | Embedded filename |
|---|---|---|---|---|
| Factur-X | 1.09.2 | UN/CEFACT CII | `MINIMUM`, `BASIC WL`, `BASIC`, `EN 16931`, `EXTENDED`, `EXTENDED-CTC-FR` | `factur-x.xml` |
| ZUGFeRD | 2.5.2 | UN/CEFACT CII | `MINIMUM`, `BASIC WL`, `BASIC`, `EN 16931`, `EXTENDED` | `factur-x.xml` |
| XRechnung | 3.0.2 | UBL Invoice, UBL CreditNote, CII | `XRECHNUNG` | `xrechnung.xml` |

ZUGFeRD and Factur-X share one technical hybrid format while keeping separate product identities
and version labels. ZUGFeRD 2.5.2 output uses the harmonized `factur-x.xml` filename and the
Factur-X XMP namespace; legacy ZUGFeRD metadata is read for import compatibility only.

XRechnung supports both UBL and CII. Embedding XRechnung XML in a PDF/A-3 document is a PXA
workflow, not a claim that an XRechnung exchange requires a PDF.

## The three validation states

This is the single most important thing to get right when reading any PXA invoice response.

| State | Field | What it means |
|---|---|---|
| Structural recognition | `isStructurallyValid` | The XML parsed under a bounded, DTD-free reader, was recognised as CII or UBL, and declares a guideline or customization identifier. **Not a conformance claim.** |
| PXA business validation | `diagnostics` with `PXAEINV…` codes | PXA's own checks: standard and profile agreement, arithmetic totals, required fields, bounded input. |
| Official conformance | `isConformanceValidated`, `conformanceStatus` | `validated`, `invalid` or `notEvaluated`, produced only by a pinned official XSD/Schematron artifact set identified by `validatorArtifactSetId` and `validatorArtifactSetSha256`. |

`conformanceStatus: "notEvaluated"` means no artifact set was configured for that standard,
version, profile and syntax combination. It does **not** mean the invoice is fine. An invoice is
never reported as conformant on the strength of structural validation alone.

## Generation

### Factur-X

```csharp
using PXA.Generator;

var invoice = new PxaEInvoiceDocument
{
    InvoiceNumber = "INV-2026-000042",
    IssueDate = new DateOnly(2026, 9, 27),
    DueDate = new DateOnly(2026, 10, 27),
    Currency = "EUR",
    Seller = new("Seller GmbH", TaxIdentifier: "DE123456789",
        Address: new("Main Street 1", "Berlin", "10115", "DE")),
    Buyer = new("Buyer AG", Address: new("Buyer Street 2", "Munich", "80331", "DE")),
    Lines =
    [
        new("1", "Consulting", Quantity: 2, UnitCode: "HUR", UnitPrice: 90m, TaxRate: 19m),
        new("2", "Handbook", Quantity: 1, UnitCode: "C62", UnitPrice: 40m, TaxRate: 7m),
    ],
};

var options = new PxaEInvoiceOptions
{
    Standard = PxaEInvoiceStandard.FacturX,
    Version = EInvoice.CurrentFacturXVersion,   // 1.09.2
    Profile = "EN 16931",
};

var xml = EInvoice.GenerateXml(invoice, options);
var hybrid = EInvoice.CreatePdf(invoice, options, fontLoader);
```

PXA calculates the line nets, the tax groups and the totals. It rounds half away from zero at the
currency's minor-unit scale, groups tax by `(taxCategoryCode, taxRate)`, and orders the groups by
rate and then by category code. Choosing a rate, claiming an exemption and deciding what is
taxable remain yours: those are billing-domain decisions and PXA does not make them for you.

```http
POST /api/pxa/v1/pdf/e-invoices/generate
POST /api/pxa/v1/pdf/e-invoices/create
Content-Type: application/json

{ "standard": "FacturX", "version": "1.09.2", "profile": "EN 16931", "invoice": { … } }
```

`generate` returns the XML; `create` returns a visible PDF/A-3 hybrid invoice whose rendered page
and embedded XML carry the same values.

### ZUGFeRD

Identical to Factur-X apart from the product identity and version:

```csharp
var options = new PxaEInvoiceOptions
{
    Standard = PxaEInvoiceStandard.Zugferd,
    Version = EInvoice.CurrentZugferdVersion,   // 2.5.2
    Profile = "EXTENDED",
};
```

### XRechnung

```csharp
var options = new PxaEInvoiceOptions
{
    Standard = PxaEInvoiceStandard.XRechnung,
    Version = EInvoice.CurrentXRechnungVersion, // 3.0.2
    Profile = "XRECHNUNG",
};
```

XRechnung normally requires a buyer reference (BT-10, the Leitweg-ID). Omitting it produces the
warning `PXAEINV117` rather than a silent omission.

## Attaching to an existing PDF

`attach` embeds validated invoice XML into a PDF you already have, without replacing its visible
pages.

```http
POST /api/pxa/v1/pdf/e-invoices/attach
Content-Type: multipart/form-data

file=@statement.pdf
invoice=@factur-x.xml
standard=Factur-X&version=1.09.2&profile=EN 16931
```

Two strategies, chosen by the source document:

- **Incremental update** when the source passes PXA's structural PDF/A-3b validation. The original
  bytes become the prefix of the result, so no page object, content stream, font or image can
  change. This is the default for unsigned sources and needs no approval.
- **Archive conversion** when the source's archival identification forbids embedded files. The
  file is converted to PDF/A-3b first, which is a rewrite, so it requires
  `allowArchiveConversion=true`.

Each of the following needs its own explicit approval, and without it the operation returns `409`
with the full preflight report while leaving your file untouched:

| Situation | Approval | Problem code when withheld |
|---|---|---|
| The source is not PDF/A-3b and must be rewritten | `allowArchiveConversion` | `PXAEINVAPI022` |
| The source is signed: a rewrite removes signatures, while an incremental update adds a later revision | `allowSignatureInvalidation` | `PXAEINVAPI022` |
| The PDF already carries an invoice attachment | `replaceExistingInvoice` | `PXAEINVAPI022` |
| The invoice values were not found in the visible page | `allowInconsistentPdf` | `PXAEINVAPI021` |

`POST /api/pxa/v1/pdf/e-invoices/attach/preflight` runs every check and produces no document, so
you can show a user exactly what would happen before anything is written.

Every attachment is verified after it is written: the invoice is re-extracted byte for byte, the
per-page content digests are compared against the source, and the result is re-validated against
the PXA PDF/A-3b structural validator. A failure at any of those points aborts the operation.

Response headers carry the evidence:

```
PXA-Source-SHA256, PXA-Result-SHA256, PXA-Invoice-SHA256, PXA-Invoice-Attachment,
PXA-Attachment-Strategy, PXA-Pages-Unchanged, PXA-Compliance-Profile,
PXA-Invoice-Consistency, PXA-Invoice-Conformance, PXA-Validation-Record
```

### Preserving unknown XML

XML PXA does not recognise can be stored byte for byte, but only when you ask for it:

```http
POST /api/pxa/v1/pdf/e-invoices/attach
preserveUnrecognized=true&attachmentFileName=legacy-billing.xml
```

Preservation withholds every standards signal. The file may not use a reserved invoice filename
(`PXAEINVAPI026`), no Factur-X extension schema is written, the `/AFRelationship` stays
`/Unspecified` rather than `/Alternative`, no semantic values are extracted, and `inspect` reports
`recognitionStatus: "unrecognized"` with `isEditable: false`. The same bounds still apply: the
10 MiB limit, a plain ASCII `.xml` filename, `application/xml`, the bounded DTD-free parse, and
the stored size and checksum.

## Inspection

```http
POST /api/pxa/v1/pdf/e-invoices/inspect
```

Returns one entry per document-associated file: filename, media type, relationship, size, whether
the declared size and PDF checksum match the extracted bytes, the recognised standard, syntax,
guideline identifier and profile, the three validation states, the parsed semantic values, and a
conservative comparison against the visible page text.

### PDF and XML consistency

`pdfConsistency.status` is one of:

- `consistent` — every comparable invoice value was found in the extractable page text.
- `reviewRequired` — at least one value was not found. The page may present it differently, or the
  document may genuinely contradict itself; a human decides.
- `notEvaluated` — no extractable page text was available.

A scanned or otherwise text-inaccessible PDF stays at `notEvaluated`. Missing text extraction is
never treated as agreement — that would be the one false positive a reader could not detect.
Amount matching covers the value's own minor-unit scale as well as two-digit and thousand-separated
forms, so an invoice in a currency with zero or three minor units is not reported as needing review
merely because of its scale.

## Extraction

```http
POST /api/pxa/v1/pdf/e-invoices/extract
file=@invoice.pdf&attachmentName=factur-x.xml
```

Returns the attachment exactly as stored, after verifying its declared size and PDF checksum. A
mismatch returns `PXAEINVAPI006` instead of the bytes.

## Validation

```http
POST /api/pxa/v1/pdf/e-invoices/validate
file=@factur-x.xml&standard=Factur-X&version=1.09.2&profile=EN 16931
```

Returns a report with the three states described above. Verdicts are retained per tenant:

```http
GET  /api/pxa/v1/pdf/e-invoices/validations
GET  /api/pxa/v1/pdf/e-invoices/validations/{id}
GET  /api/pxa/v1/pdf/e-invoices/validations/export
POST /api/pxa/v1/pdf/e-invoices/validations/{id}/revalidate
```

Each record is append-only and carries the tenant, the actor, the document checksum, the container
checksum, the standard, version and profile, the three states, the validator artifact-set
identifier and hash, the diagnostics as reported, and both timestamps. Records are never modified:
the database rejects an `UPDATE` outright and permits a `DELETE` only through the bounded retention
path. `revalidate` runs the currently configured artifact set over the same document bytes — proven
identical by checksum — and stores the new verdict **beside** the old one, so both remain provable.
`export` returns the retained evidence as one machine-readable JSON document.

## Designer integration status

The Designer integration is currently a development foundation, not a reachable product surface.
The typed invoice model mirrors `PxaEInvoiceDocument`, template definitions use canonical binding
paths (`invoice.seller.name`, `invoice.lines`, `totals.grandTotal` and so on), and the API client
posts that same model to `generate` and `create`. A React editor, template insertion workflow,
autosave integration, and visual-parity evidence are still required before customers can edit an
electronic invoice in the Designer.

The prepared Designer-side validation mirrors the backend's `PXAEINV1xx` diagnostics and has
catalog entries in all six Designer languages. These strings and modules are not yet a rendered
six-language UI. When the UI is added, its validation remains only a typing aid: the backend must
still validate, and the Designer must never display a conformance state it has not received.

Amounts in the Designer use exact scaled-integer arithmetic that reproduces .NET `decimal` with
half-away-from-zero rounding. `test-assets/e-invoicing/totals-parity.json` pins nine cases —
multiple rates, equal rates in different categories, half-way rounding, fractional quantities and
rates, 0-, 2- and 3-minor-digit currencies, and accumulation of values that binary floating point
cannot represent — and both the TypeScript and the C# implementations are asserted against it.

## Migration

`PdfEInvoiceMigrationCatalog` maps every PDF code-migration provider onto six capabilities:
Factur-X, ZUGFeRD, XRechnung, PDF/A-3 attachment, extraction and validation. Each mapping reports
`automatic`, `reviewRequired` or `unsupported` with a stated reason, so a source API can never
become a silent TODO.

The mappings are keyed on API identifiers your source may contain rather than on a claim about
which invoice features each vendor ships today. Attachment and extraction map automatically where
the provider has the API; invoice generation never does, because the standard version and profile
are a business obligation your code usually leaves implicit; validation never does, because the
detected call may validate PDF/A structure rather than invoice rules.

The full table is in
[PXA.Unified-Migration-Provider-Coverage.md](PXA.Unified-Migration-Provider-Coverage.md).

## Compatibility matrix

| Capability | Factur-X 1.09.2 | ZUGFeRD 2.5.2 | XRechnung 3.0.2 |
|---|---|---|---|
| Typed XML generation | yes | yes | yes (UBL) |
| Visible PDF/A-3 hybrid creation | yes | yes | yes |
| Attach to an existing PDF/A-3b | yes (incremental) | yes (incremental) | yes (incremental) |
| Attach with PDF/A-3b conversion | yes (approval required) | yes (approval required) | yes (approval required) |
| Byte-preserving extraction | yes | yes | yes |
| Structural recognition | yes | yes | yes |
| PXA business validation | yes | yes | yes |
| Official XSD/Schematron conformance | artifact set required | artifact set required | artifact set required |
| PDF/XML semantic consistency | yes | yes | yes |
| Retained validation evidence | yes | yes | yes |
| Unknown XML preservation | yes (explicit request) | yes (explicit request) | yes (explicit request) |

## Known limits

- **No official artifacts are shipped yet.** Until FeRD/FNFE and KoSIT redistribution terms have
  been reviewed and recorded, `conformanceStatus` is `notEvaluated` for every profile and PXA makes
  no conformance claim. This is a deliberate gap, not an oversight.
- **Independent validation is not yet published.** Generated output has not been through official
  FeRD/FNFE tooling or an independent PDF/A validator such as veraPDF in this repository, so no
  such result is asserted anywhere.
- **Encrypted sources cannot be attached to.** Decrypt first; `attach` refuses with
  `PXAEINVATTACH001` rather than guessing.
- **An incremental attachment leaves earlier revisions intact.** Replacing an existing invoice
  dereferences it in the new revision, but its bytes remain readable in the previous one. Use a
  full conversion when that matters.
- **A file specification referenced outside the catalog is not replaced.** If an existing invoice
  attachment is also reachable from a file-attachment annotation, PXA refuses to remove it
  (`PXAEINVATTACH016`) rather than change what the document shows.
- **`notEvaluated` consistency is not agreement.** Bounded OCR evidence is not yet available, so a
  scanned invoice's page is never compared.
- **One version per standard.** Multiple concurrent versions of the same standard are not
  supported; a mismatch is rejected with `PXAEINV012`.
- **Synchronous limits.** 100 MiB source PDF, 10 MiB invoice XML, 120 MiB attachment output,
  50 MiB generated PDF, 2 000 lines for synchronous PDF creation, 10 000 lines for XML.

## Permissions and entitlement

Every operation sits behind the `generator` product entitlement and one Designer permission:

| Operation | Permission |
|---|---|
| `generate` | `designer.pdf.e-invoices.generate` |
| `create` | `designer.pdf.e-invoices.create` |
| `attach`, `attach/preflight` | `designer.pdf.e-invoices.attach` |
| `validate`, `validations/{id}/revalidate` | `designer.pdf.e-invoices.validate` |
| `inspect`, `extract`, `validations`, `validations/export` | `designer.pdf.e-invoices.read` |

A read-only role holds `read` and `validate` and never `create` or `attach`. Preflighting an
attachment requires the same permission as performing one, because the report describes the
document's content.

## Diagnostics

| Range | Source |
|---|---|
| `PXAEINV0xx` | Recognition, options and structural validation |
| `PXAEINV1xx` | Typed invoice-document validation |
| `PXAEINVATTACH0xx` | Existing-PDF attachment preflight and execution |
| `PXAEINVAPI0xx` | API request handling and Problem Details |

Identifiers are stable. A diagnostic keeps its code across releases even when its message is
reworded.
