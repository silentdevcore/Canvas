# PVAD Electronic Invoicing

PVAD produces, embeds, reads and validates electronic-invoice documents. It reports what it
actually established — structural recognition, PVAD business validation and official conformance
are three separate states — and never presents one as another.

## What PVAD is not

Peppol transport, national tax submission, accounting, payment and legal retention are
**external integrations, not PDF-engine capabilities**. PVAD hands you an invoice document and an
honest account of what it checked; sending it to an access point, filing it with a tax authority,
posting it to a ledger, settling it, or retaining it for a statutory period are things your own
systems or your service providers do. PVAD deliberately has no API for any of them, because owning
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

XRechnung supports both UBL and CII. Embedding XRechnung XML in a PDF/A-3 document is a PVAD
workflow, not a claim that an XRechnung exchange requires a PDF.

## The three validation states

This is the single most important thing to get right when reading any PVAD invoice response.

| State | Field | What it means |
|---|---|---|
| Structural recognition | `isStructurallyValid` | The XML parsed under a bounded, DTD-free reader, was recognised as CII or UBL, and declares a guideline or customization identifier. **Not a conformance claim.** |
| PVAD business validation | `diagnostics` with `PXAEINV…` codes | PVAD's own checks: standard and profile agreement, arithmetic totals, required fields, bounded input. |
| Official conformance | `isConformanceValidated`, `conformanceStatus` | `validated`, `invalid` or `notEvaluated`, produced only by a pinned official XSD/Schematron artifact set identified by `validatorArtifactSetId` and `validatorArtifactSetSha256`. |

`conformanceStatus: "notEvaluated"` means no artifact set was configured for that standard,
version, profile and syntax combination. It does **not** mean the invoice is fine. An invoice is
never reported as conformant on the strength of structural validation alone.

## Generation

### Factur-X

```csharp
using PVAD.Generator;

var invoice = new PvadEInvoiceDocument
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

var options = new PvadEInvoiceOptions
{
    Standard = PvadEInvoiceStandard.FacturX,
    Version = EInvoice.CurrentFacturXVersion,   // 1.09.2
    Profile = "EN 16931",
};

var xml = EInvoice.GenerateXml(invoice, options);
var hybrid = EInvoice.CreatePdf(invoice, options, fontLoader);
```

PVAD calculates the line nets, the tax groups and the totals. It rounds half away from zero at the
currency's minor-unit scale, groups tax by `(taxCategoryCode, taxRate)`, and orders the groups by
rate and then by category code. Choosing a rate, claiming an exemption and deciding what is
taxable remain yours: those are billing-domain decisions and PVAD does not make them for you.

```http
POST /api/pvad/v1/pdf/e-invoices/generate
POST /api/pvad/v1/pdf/e-invoices/create
Content-Type: application/json

{ "standard": "FacturX", "version": "1.09.2", "profile": "EN 16931", "invoice": { … } }
```

`generate` returns the XML; `create` returns a visible PDF/A-3 hybrid invoice whose rendered page
and embedded XML carry the same values.

### ZUGFeRD

Identical to Factur-X apart from the product identity and version:

```csharp
var options = new PvadEInvoiceOptions
{
    Standard = PvadEInvoiceStandard.Zugferd,
    Version = EInvoice.CurrentZugferdVersion,   // 2.5.2
    Profile = "EXTENDED",
};
```

### XRechnung

```csharp
var options = new PvadEInvoiceOptions
{
    Standard = PvadEInvoiceStandard.XRechnung,
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
POST /api/pvad/v1/pdf/e-invoices/attach
Content-Type: multipart/form-data

file=@statement.pdf
invoice=@factur-x.xml
standard=Factur-X&version=1.09.2&profile=EN 16931
```

Two strategies, chosen by the source document:

- **Incremental update** when the source passes PVAD's structural PDF/A-3b validation. The original
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

`POST /api/pvad/v1/pdf/e-invoices/attach/preflight` runs every check and produces no document, so
you can show a user exactly what would happen before anything is written.

Every attachment is verified after it is written: the invoice is re-extracted byte for byte, the
per-page content digests are compared against the source, and the result is re-validated against
the PVAD PDF/A-3b structural validator. A failure at any of those points aborts the operation.

Response headers carry the evidence:

```
PVAD-Source-SHA256, PVAD-Result-SHA256, PVAD-Invoice-SHA256, PVAD-Invoice-Attachment,
PVAD-Attachment-Strategy, PVAD-Pages-Unchanged, PVAD-Compliance-Profile,
PVAD-Invoice-Consistency, PVAD-Invoice-Conformance, PVAD-Validation-Record
```

### Preserving unknown XML

XML PVAD does not recognise can be stored byte for byte, but only when you ask for it:

```http
POST /api/pvad/v1/pdf/e-invoices/attach
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
POST /api/pvad/v1/pdf/e-invoices/inspect
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
POST /api/pvad/v1/pdf/e-invoices/extract
file=@invoice.pdf&attachmentName=factur-x.xml
```

Returns the attachment exactly as stored, after verifying its declared size and PDF checksum. A
mismatch returns `PXAEINVAPI006` instead of the bytes.

## Validation

```http
POST /api/pvad/v1/pdf/e-invoices/validate
file=@factur-x.xml&standard=Factur-X&version=1.09.2&profile=EN 16931
```

Returns a report with the three states described above. Verdicts are retained per tenant:

```http
GET  /api/pvad/v1/pdf/e-invoices/validations
GET  /api/pvad/v1/pdf/e-invoices/validations/{id}
GET  /api/pvad/v1/pdf/e-invoices/validations/export
POST /api/pvad/v1/pdf/e-invoices/validations/{id}/revalidate
```

Each record is append-only and carries the tenant, the actor, the document checksum, the container
checksum, the standard, version and profile, the three states, the validator artifact-set
identifier and hash, the diagnostics as reported, and both timestamps. Records are never modified:
the database rejects an `UPDATE` outright and permits a `DELETE` only through the bounded retention
path. `revalidate` runs the currently configured artifact set over the same document bytes — proven
identical by checksum — and stores the new verdict **beside** the old one, so both remain provable.
`export` returns the retained evidence as one machine-readable JSON document.

### What each validation layer proves

Five different things are called "validation" around an invoice, and they are not interchangeable.

| Layer | What it proves | What it cannot prove |
|---|---|---|
| Structural recognition | The bytes parse safely and look like a CII or UBL invoice. | Anything about correctness or conformance. |
| PVAD business validation | PVAD's own rules hold: totals add up, identifiers are unique, limits are respected. Reported as `PXAEINV…` diagnostics. | That an official rule set agrees. |
| PDF/A validation | The container meets ISO 19005-3. Run through the pinned veraPDF workflow in `tools/pdfa/`. | Anything about the invoice inside it. |
| Official invoice conformance | The XML satisfies the published XSD and Schematron for its standard, version and profile. | Only available when a reviewed artifact set is mounted; otherwise `notEvaluated`. |
| Independent validation evidence | A second, independent implementation agrees. | Nothing PVAD can self-certify; see the limits below. |

A pass at one layer is never reported as a pass at another. In particular, a structurally recognised
invoice with clean business diagnostics still reports `conformanceStatus: "notEvaluated"`.

### PDF/A evidence

`tools/pdfa/validate-pdfa.sh` runs the pinned veraPDF container.
`tools/pdfa/einvoice-pdfa-evidence.mjs` drives it over a deterministic corpus of hybrid invoices —
Factur-X, ZUGFeRD and XRechnung, single-page, multi-page, long-description and many-tax-group, plus a
deliberately non-PDF/A control — and writes one machine-readable record beside the raw validator
reports.

```bash
dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj \
  --filter FullyQualifiedName~EInvoicePdfaFixture
node tools/pdfa/einvoice-pdfa-evidence.mjs
```

The fixtures are regenerated rather than committed, and every one's SHA-256 is pinned in
`test-assets/e-invoicing/pdfa-fixtures.json`. The runner refuses to hand a file to the validator
unless its checksum matches, because a report about different bytes is evidence about a different
document. The control exists so that a validator which rubber-stamps everything fails the run.

When the container is unavailable, every fixture is recorded `notEvaluated`. Nothing in the pipeline
can produce a pass without a report.

### Mounting official validator artifacts

PVAD ships no FeRD/FNFE or KoSIT artifacts; their redistribution terms are unresolved. An operator who
holds them mounts a set locally and PVAD reads it from disk. **Validation never downloads anything.**

A set is a directory containing the XSD and precompiled Schematron XSLT plus a manifest. From
manifest schema version 2 the manifest must carry a governance record. Version 1 manifests are
refused with `PXAEINV027`; this prevents an older manifest from bypassing the licence gate:

| Field | Why it is required |
|---|---|
| `family` | Which upstream published it, checked against the standard so a KoSIT set cannot validate Factur-X. |
| `upstreamVersion` | The upstream's own release, not PVAD's. |
| `retrievedOn` | When the files were fetched from `sourceUri`. |
| `licenseReview`, `licenseReviewedBy`, `licenseReviewedOn` | Who concluded PVAD may run them, and when. Only `approved` is usable. |
| `supportedProfiles` | Which profiles the set covers. |
| `updatePolicy` | How the set is kept current. |
| `expiresOn` | When it goes stale and is refused. |

Every artifact is pinned by SHA-256 and re-verified on each run. Paths must stay inside the mounted
directory and may not be symbolic links.

**Fail-closed behaviour.** Each of the following produces `conformanceStatus: "unavailable"` with a
stable diagnostic, never a verdict and never a silent downgrade to structural recognition:

| Situation | Diagnostic |
|---|---|
| A pinned artifact is missing | `PXAEINV020` |
| An artifact's SHA-256 does not match | `PXAEINV022` |
| The set does not match the requested standard, version, profile or syntax | `PXAEINV026` |
| The manifest schema version is unknown to this build | `PXAEINV027` |
| Provenance is incomplete | `PXAEINV028` |
| The licence review is not `approved` | `PXAEINV029` |
| The set has expired | `PXAEINV030` |

When a rule does fail, the finding is mapped to a stable PVAD code **and** the validator's own rule
identifier, severity, location, message and test are preserved alongside it, so a result can still be
discussed in the standards body's own terms.

#### Mount conventions

The directory is ignored by Git in every layout, so a mounted set can never be committed.

| Environment | Convention |
|---|---|
| Local development | `validator-artifacts/<family>/<version>/` at the repository root. |
| CI | A job-scoped secret mount located by an operator-defined variable such as `$PVAD_EINVOICE_ARTIFACT_ROOT`; absent on public pipelines, where conformance is simply `notEvaluated`. |
| Docker Compose | A read-only bind mount, for example `./validator-artifacts:/srv/pvad/validator-artifacts:ro`. |
| On-Premise, isolated | An operator-managed read-only volume; the runner needs no network access at any point. |

These are deployment conventions, not automatically consumed API environment
settings. Configure the validator runner's paths explicitly. Existing operator
mount destinations may remain unchanged when their runner configuration agrees;
the rename does not move mounted artifacts or approve their use.

### Synthetic corpus boundaries

Every fixture in this repository is PVAD-authored and synthetic. `test-assets/e-invoicing/corpus/` and
`test-assets/e-invoicing/pdfa-fixtures.json` each record provenance, licence, expected result and
SHA-256 for every entry. No customer document, no licensed real-world invoice and no official
reference fixture is present, and none of these fixtures is evidence of official conformance.

## Designer integration

The Electronic Invoice workspace lives at **`/pdf/e-invoice`**, reachable from the PDF hub sidebar
between *PDF Viewer* and *Migrations*. It requires the `designer.pdf.e-invoices.read` permission,
the `generator` product entitlement, and the `designer.e-invoice` feature gate; the page renders a
distinct unavailable state when the gate is off and the standard permission wall when the
permission is missing.

### What the workspace edits

One canonical invoice. The editor has sections for the standard and profile, the invoice number and
references, the issue and due dates, the currency and its minor units, the seller and buyer with
their addresses, the buyer reference or Leitweg-ID, and the invoice lines. Lines can be added,
removed and reordered. Tax groups, the net total, the tax total and the amount payable are rendered
as read-only `output` elements: they are computed by the same exact-decimal arithmetic the generator
uses, and a total a user could overtype would be a second source of truth.

Selecting a standard pins its maintained version — it is displayed, never editable — and filters the
profile list to that standard's profiles. Switching to XRechnung therefore leaves `XRECHNUNG` as the
only profile, and a profile only the previous standard offered does not survive the switch.

### One canonical copy

`template.eInvoice.invoice` is the only editable copy of any invoice value. The page elements the
Canvas, the Live Preview and the exported PDF render are a *projection* of that object: regenerated
whenever it changes, each keeping the `binding` path it came from, and never read back into the
invoice. Projected pages and elements keep deterministic IDs across invoice edits, so selection
and internal references remain stable. The XML and the PDF come from the backend, from the same object. That is what makes it
impossible for the preview, the XML, the visible PDF and a reopened Viewer inspection to disagree
about a figure.

### Layouts

The maintained layouts are offered in the workspace and bind only to canonical paths. Inserting one
is a **single undoable transaction**: the pages, the elements and the invoice payload land in one
store commit, so one undo reverses the whole insertion. Projected elements receive collision-free
deterministic ids, and
the layout definitions themselves hold no literal invoice value — their labels are i18n keys
resolved at insertion time, which is how one definition serves all six Designer languages.

### Pages

A long invoice is projected onto as many A4 pages as it needs, and **the Designer and the hybrid-PDF
renderer paginate it identically**. They implement one versioned contract —
`test-assets/e-invoicing/pagination-contract.json`, schema `pxa.e-invoice.pagination/1` — which pins
the page box, the table geometry, the columns, the row metrics, the continuation headings, the
totals block and the footer-safe bound. Neither implementation depends on the other: the Designer's
half is `invoicePagination.ts`, the generator's is `EInvoicePageLayout`, and each asserts itself
against the pinned file. The same invoice therefore reaches the same page count with the same
content on each page, whichever surface renders it.

With the maintained standard layout that is **26 short line rows on the first page** — which also
carries the seller and buyer block — and **36 on each continuation page**, where the column headings
repeat. A row is never split across a page. Concretely, a one-tax-group invoice fits on a single
page up to 19 lines and on two pages up to 56, gaining a page every 36 lines after that.

### Long descriptions

A description that wraps grows its row, and the next row starts below it. A description longer than
a whole page is not clipped: it continues on the next page as a **continuation fragment**. Only the
text continues — the position, quantity, unit price, tax rate and net amount stay on the row that
carries them, so no amount is ever shown twice for one line. The canonical invoice still holds one
line with one identity; fragments are a visual device with deterministic identifiers
(`…-l<line>f<fragment>` in the Designer projection) and never appear in the XML.

Both renderers wrap greedily to the same column width using the same conservative metrics, so they
break at the same word and produce the same fragments.

**Limit.** A single description may occupy at most eight fragments. Beyond that `EInvoice.CreatePdf`
refuses the document with `PXAEINV122` rather than emitting one of unbounded length. The limit
cannot be reached through the public API, because `PXAEINV109` already caps a description at 4 096
characters — about four fragments at worst — so it guards the renderer rather than the caller.

### Totals and large tax breakdowns

The totals block follows the final line when there is room for it above the footer-safe bound, and
takes a page of its own when there is not. It grows with the number of tax groups, and a breakdown
too large for one page continues across further totals pages. However many pages it spans:

- every tax group appears exactly once, in rate then category order;
- the net total appears once, on the first totals page;
- the payable total appears once, on the last;
- with more than one tax group an aggregate **Tax total** row is drawn, so the figure the embedded
  XML declares can be confirmed on the page. With a single group the group row already is that
  total, and no aggregate row is added;
- nothing overlaps the footer note or runs past the page boundary.

Zero, one, two, eight and eighty tax groups are covered by pinned cases in the contract.

Stable Designer-only line IDs are stored alongside the canonical lines and move with them. They do
not enter the invoice XML. Whatever the page count, one edit stays one undoable transaction and
autosave persists every generated page.

### Generating

The workspace offers Validate, Generate XML and Create PDF/A-3 hybrid invoice, then Download XML,
Download PDF and Open in PVAD Viewer. Validation runs over the generated XML, so what is checked is
exactly what would be embedded. After a PDF is created the workspace inspects it and shows the
semantic PDF/XML comparison; a scanned or text-inaccessible result stays at `notEvaluated` and is
never shown as `consistent`.

Structural recognition, PVAD business validation and official conformance appear as three separate
rows. A clean local check reads "structurally recognised"; only a backend `valid` verdict — which
comes only from a pinned official artifact set — reads as validated, and the artifact-set identifier
is shown beside it. Diagnostics keep their `PXAEINV…` codes visible for support, and each local
finding has a control that focuses the field it is about.

### Saving

The invoice is part of the Designer document, so it uses the existing template persistence
pipeline: the same two-second debounce, the same revision conflict handling, the same offline
behaviour and the same unsaved-changes warning. The workspace surfaces Saving, Saved, Unsaved
changes, Retrying, Failed, Offline and Conflict, and offers *Load the server draft*, *Save as a new
draft* and *Download a local copy* where they apply. Reopening a draft reproduces the exact invoice
values and the same calculated totals.

### Localization and accessibility

The workspace is localized in English, German, French, Spanish, Italian and Arabic, with Arabic
rendering right-to-left through the shared logical-property layout. Every input has a real label,
the invoice lines use table semantics with `scope` on every header, icon-only buttons carry
accessible names, and validation and generation results are announced through a polite live region.

Amounts use exact scaled-integer arithmetic that reproduces .NET `decimal` with half-away-from-zero
rounding. `test-assets/e-invoicing/totals-parity.json` pins nine cases — multiple rates, equal rates
in different categories, half-way rounding, fractional quantities and rates, 0-, 2- and
3-minor-digit currencies, and accumulation of values that binary floating point cannot represent —
and both the TypeScript and the C# implementations are asserted against it.

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
[PVAD.Unified-Migration-Provider-Coverage.md](PVAD.Unified-Migration-Provider-Coverage.md).

## Compatibility matrix

| Capability | Factur-X 1.09.2 | ZUGFeRD 2.5.2 | XRechnung 3.0.2 |
|---|---|---|---|
| Typed XML generation | yes | yes | yes (UBL) |
| Visible PDF/A-3 hybrid creation | yes | yes | yes |
| Attach to an existing PDF/A-3b | yes (incremental) | yes (incremental) | yes (incremental) |
| Attach with PDF/A-3b conversion | yes (approval required) | yes (approval required) | yes (approval required) |
| Byte-preserving extraction | yes | yes | yes |
| Structural recognition | yes | yes | yes |
| PVAD business validation | yes | yes | yes |
| Official XSD/Schematron conformance | artifact set required | artifact set required | artifact set required |
| PDF/XML semantic consistency | yes | yes | yes |
| Retained validation evidence | yes | yes | yes |
| Unknown XML preservation | yes (explicit request) | yes (explicit request) | yes (explicit request) |

## Known limits

- **No official artifacts are shipped yet.** Until FeRD/FNFE and KoSIT redistribution terms have
  been reviewed and recorded, `conformanceStatus` is `notEvaluated` for every profile and PVAD makes
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
  attachment is also reachable from a file-attachment annotation, PVAD refuses to remove it
  (`PXAEINVATTACH016`) rather than change what the document shows.
- **`notEvaluated` consistency is not agreement.** Bounded OCR evidence is not yet available, so a
  scanned invoice's page is never compared.
- **One version per standard.** Multiple concurrent versions of the same standard are not
  supported; a mismatch is rejected with `PXAEINV012`.
- **Synchronous limits.** 100 MiB source PDF, 10 MiB invoice XML, 120 MiB attachment output,
  50 MiB generated PDF, 2 000 lines for synchronous PDF creation, 10 000 lines for XML.
- **Non-Latin scripts are not visually verified.** Arabic, Hebrew and CJK need the Noto faces
  listed in `fonts/README.md`, which PVAD does not ship, and the PDF engine implements no cursive
  shaping. A browser shapes and joins an Arabic run while the PDF draws unjoined glyphs in visual
  order, so the Designer preview of such a line must not be treated as visually equivalent to the
  generated PDF. The invoice values themselves are unaffected: they travel through the canonical
  invoice and the XML, not through the rendered page.
- **Hybrid parity is pagination and content, not pixels.** The Designer and the hybrid renderer
  implement one pagination contract and are gated on producing the same page count and the same
  content per page for every maintained fixture. They are not pixel-identical: the two draw text
  through different engines, so page-level differences of a few hundredths RMSE remain and are
  reported as P2. Where an exact visual reference matters, the DesignExport PDF of the projection is
  the gated surface, and parity against it is not a statement about the hybrid renderer.
- **Unbroken text is bounded safely.** A token wider than the description column is segmented at
  Unicode boundaries for visual layout. The canonical invoice value and embedded XML remain
  unchanged, while the visible row receives enough lines and continuation pages to avoid clipping.

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
| `CANMIGEINV00x` | Electronic-invoice code-migration findings, one per capability |

Identifiers are stable. A diagnostic keeps its code across releases even when its message is
reworded.

Named codes worth knowing:

| Code | Meaning |
|---|---|
| `PXAEINV109` | A line description exceeds 4 096 characters. |
| `PXAEINV120` | PDF/A-3 invoice creation was asked for without an embedded-font loader. |
| `PXAEINV122` | A line description is too long to render within eight continuation fragments. |
| `CANMIGEINV001` / `002` / `003` | Factur-X / ZUGFeRD / XRechnung generation found in migrated source. |
| `CANMIGEINV004` | PDF/A-3 attachment found in migrated source. |
| `CANMIGEINV005` / `006` | Invoice extraction / validation found in migrated source. |

A `CANMIGEINV` finding carries a disposition — `automatic`, `reviewRequired` or `unsupported` — and
always names either the PVAD API that replaces the call or, when there is none, what to do instead.
Invoice generation is never `automatic`: the standard version and profile are a business obligation
the source code usually leaves implicit. No migration finding asserts conformance.
