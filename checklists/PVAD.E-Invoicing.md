# PXA Electronic Invoicing

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

## Goal

Deliver versioned electronic-invoice generation, embedding, extraction,
validation, Viewer inspection, Designer binding, migration, and Documentation
without confusing secure XML parsing with standards conformance.

The first maintained versions are Factur-X 1.09.2, ZUGFeRD 2.5.2, and
XRechnung 3.0.2. Versioned official schemas, Schematron rules, code lists, and
independent validators remain authoritative.

## P0 - Foundation

- [x] Add generic document-level associated files to PXA.Pdf.
- [x] Emit embedded-file streams, file specifications, AFRelationship, catalog
  AF, and the embedded-files name tree.
- [x] Reject associated files in PDF/A-2b and permit them in PDF/A-3b.
- [x] Add bounded, DTD-free XML parsing and CII/UBL syntax recognition.
- [x] Distinguish structural recognition from XSD/Schematron conformance.
- [x] Add typed Factur-X, ZUGFeRD, and XRechnung options and stable PXAEINV
  diagnostics.
- [x] Add a typed business-invoice model with deterministic CII and UBL
  generation, calculated tax groups and totals, bounded input validation, and
  secure structural self-recognition.
- [x] Add the required Factur-X/ZUGFeRD XMP extension schema and profile metadata.
- [x] Add a fail-closed, hash-pinned local XSD and precompiled Schematron
  validation engine with stable conformance diagnostics.
- [ ] Pin licensed or distributable official validation artifacts with source,
  version, license, hash, and update policy.
- [ ] Validate profile-specific XSD, Schematron, code lists, totals, and business
  rules.

## P0 - API and Product Integration

- [x] Add authenticated create, attach, extract, inspect, and validate operations
  under /api/pxa/v1/pdf/e-invoices.
- [x] Add authenticated structural validation, attachment inspection, and
  byte-preserving extraction operations under `/api/pxa/v1/pdf/e-invoices`.
- [x] Add authenticated typed XML generation and visible PDF/A-3 hybrid-invoice
  creation after XMP metadata and semantic PDF/XML consistency became available.
- [x] Add attachment of invoice XML to an existing PDF without replacing its
  visible source pages.
- [x] Add dedicated read and validation permissions plus bounded PDF/XML uploads.
- [x] Add dedicated generate/create permissions, Generator product entitlement,
  bounded JSON input, synchronous line limits, and generated-PDF output limits.
- [x] Return stable Problem Details and never return an invoice as conformant after
  structural validation alone.
- [x] Add Viewer invoice summary, profile, validation evidence, diagnostics, and
  integrity-protected XML download.
- [x] Add parsed invoice identifiers, dates, parties, currency, taxes, totals,
  and line items plus conservative PDF/XML semantic consistency warnings to
  the Viewer.
- [x] Add Designer invoice data binding and templates without making the PDF engine
  depend on billing-domain services.
- [x] Add migration capability mappings for providers that expose Factur-X,
  ZUGFeRD, XRechnung, or generic PDF/A-3 invoice APIs.

## P1 - Evidence and Delivery

- [ ] Add official valid and invalid fixtures for every supported syntax/profile.
- [ ] Validate generated hybrid invoices with the official FeRD/FNFE artifacts and
  an independent PDF/A validator.
- [ ] Validate XRechnung XML with the pinned KoSIT validator configuration.
- [x] Add automated XML/PDF semantic consistency coverage for invoice identifiers,
  parties, currency, totals, taxes, dates, and line items.
- [ ] Validate semantic consistency against official and real-world multilingual
  fixtures, including scanned PDFs where comparison must remain not evaluated.
- [x] Add public Documentation, examples, known limits, compatibility matrix, and
  downloadable machine-readable evidence.
- [ ] Add a customer-facing minor release fragment after all P0 release gates pass.

## Security and Compatibility

- [x] Reject DTDs, external entities, oversized XML, duplicate attachments, unsafe
  filenames, invalid MIME types, and ambiguous profiles.
- [x] Preserve unknown invoice XML when explicitly requested without claiming it is
  editable or conformant.
- [x] Keep Peppol transport, tax submission, accounting, and legal retention outside
  the PDF engine.
- [x] Version validator artifacts independently from API routes and include the
  artifact-set ID and SHA-256 fingerprint in validation reports.
- [x] Persist historical validation reports with their artifact-set evidence.

## References

- Central roadmap:
  [PVAD.PDF-Specialized-Features-Roadmap.md](PVAD.PDF-Specialized-Features-Roadmap.md)
- PDF Trust:
  [PVAD.PDF-Trust-Foundation.md](PVAD.PDF-Trust-Foundation.md)
- Provider gaps:
  [PvadPdf-Provider-Feature-Gaps.md](PvadPdf-Provider-Feature-Gaps.md)
- Official FeRD ZUGFeRD 2.5.2 package:
  https://www.ferd-net.de/publikationen-produkte/publikationen/detailseite/zugferd-252-english
- Official KoSIT XRechnung validator configuration:
  https://github.com/itplr-kosit/validator-configuration-xrechnung
- Official KoSIT XRechnung Schematron rules:
  https://github.com/itplr-kosit/xrechnung-schematron

## Remaining Work Handoff

The following items are the complete remaining scope as of this handoff. Work
should proceed in the listed order because later evidence, UI, and release work
depends on the validation foundation.

### Status

Progress: 30 of 40 unique roadmap items complete (75%), up from 21 (52.5%).

Two Acceptance Criteria - fail-closed handling of invalid input, and cross-surface agreement on
standard, version, profile, syntax and conformance state - are now ticked because dedicated tests
prove them. They are **restatements** of scope already counted under EINV-04, EINV-05, EINV-06 and
EINV-12, so the unique figure stays 30 of 40 and the percentage is unchanged. For reference the raw
checkbox count across the two counted regions is 31 of 40 (27 of 34 P0-P3 items, 4 of 6 Acceptance
Criteria); the unique figure is the one to report.

EINV-05 is complete. The Designer projection and dedicated hybrid renderer now implement the same
versioned pagination contract, including deterministic continuation fragments, multi-page tax
summaries and safe segmentation of valid maximum-length tokens without losing Unicode characters.

| Item | State |
|---|---|
| EINV-01 Official validation artifact governance | open, externally blocked |
| EINV-02 Complete standards validation | open, depends on EINV-01 |
| EINV-03 Existing-PDF attachment workflow | done |
| EINV-04 Complete API contract | done |
| EINV-05 Designer invoice experience | done |
| EINV-06 Migration provider coverage | done |
| EINV-07 Maintained fixture corpus | PXA-authored part done; official fixtures blocked on EINV-01 |
| EINV-08 Independent hybrid-PDF validation | open, externally blocked |
| EINV-09 Real-world semantic consistency | synthetic part done; licensed corpus blocked |
| EINV-10 Unknown XML preservation | done |
| EINV-11 Historical validation evidence | done |
| EINV-12 Product boundaries and public documentation | done |
| EINV-13 Final release gates | open, depends on EINV-01, EINV-02 and EINV-08 |

Most of what is still open is blocked on one external dependency: the
redistribution and runtime-use terms of the official FeRD/FNFE and KoSIT
artifacts. Until those are reviewed and recorded, PXA reports
`conformanceStatus: "notEvaluated"` for every profile, ships no official artifact
or fixture, and publishes no independent validation result. The customer-facing
release fragment therefore stays un-finalized and `VERSION` is unchanged at 1.1.0.

Visual parity status.

Four maintained fixtures are compared, each generated from one canonical invoice and drift-guarded
by `pxa-designer/src/__tests__/eInvoiceVisualParityFixture.test.ts`:

```
for fixture in e-invoice e-invoice-multipage e-invoice-long-description e-invoice-multi-tax; do
  PXA_VISUAL_PARITY_FIXTURE=test-assets/visual-parity/$fixture.json \
    PXA_VISUAL_PARITY_ENFORCE=true node tools/visual-parity/run.mjs
done
```

Artifacts land in `artifacts/visual-parity/`, which is ignored: `visual-parity-report.html`,
`visual-parity-report.json`, and per-page and per-element PNGs, diffs and overlays under `raw/`.

The run compares four surfaces. Three share the DesignExport `backend` PDF as reference — the
Canvas, the Live Preview and the JSON-to-C# generated PDF — and the fourth is the dedicated
`EInvoice.CreatePdf` hybrid invoice, rendered from the canonical invoice in the `.invoice.json`
sidecar beside each fixture. **Parity against the DesignExport PDF is not a statement about the
hybrid renderer**, and the report labels the two separately.

| Fixture | Pages | Elements | P0 | P1 | P2 | Match | Hybrid pages | Worst hybrid page RMSE | Enforced gate |
|---|---|---|---|---|---|---|---|---|---|
| `e-invoice.json` | 1 | 63 | 0 | 0 | 4 | 59 | 1 of 1 | 0.05180 | passes |
| `e-invoice-multipage.json` | 2 | 235 | 0 | 0 | 4 | 231 | 2 of 2 | 0.06446 | passes |
| `e-invoice-long-description.json` | 2 | 72 | 0 | 0 | 6 | 66 | 2 of 2 | 0.07538 | passes |
| `e-invoice-multi-tax.json` | 2 | 265 | 0 | 0 | 4 | 261 | 2 of 2 | 0.06558 | passes |

All four pass the enforced gate with the repository's approved thresholds unchanged. Enforcement now
also fails on a hybrid page-count disagreement and on a hybrid page scoring P0 or P1.

- The JSON-to-C#-generated PDF is pixel-identical to the backend PDF on every element and every
  page of all four fixtures (`csharp` RMSE 0), so the code-generation path is exact.
- Canvas and Live Preview report identical RMSE on every element and every page, so the two
  Designer surfaces agree with the normal backend export of the projected `DesignExportDto`.
- The hybrid renderer produces the same page count as the projection on every fixture, with the
  same lines, fragments and tax groups per page. It is **not** pixel-identical: the two draw text
  through different engines, so a page-level difference of 0.05-0.08 perceptual RMSE remains. That
  is P2 under the approved thresholds and is reported as such, not as parity.
- The four remaining P2 findings are the same on both fixtures: `einv-title`, `einv-invoiceNumber`,
  `einv-payableAmount` and `einv-payableCurrency`. Each one's trimmed ink box matches the backend
  exactly in width and position; only a single anti-aliased edge row differs, because Chromium and
  poppler distribute partial coverage differently on bold stems. These are genuinely
  rasterization-related and are not layout differences.

Shared pagination contract.

The Designer and `EInvoice.CreatePdf` previously paginated independently: the Designer used 18-point
rows from y 272, the hybrid renderer fixed 30-point rows from y 276 with its own columns, a dark
header bar and a 24-point description box that silently clipped anything longer. The same invoice
could therefore reach a different page count on screen and in the issued document.

Both now implement one versioned contract, `test-assets/e-invoicing/pagination-contract.json`
(schema `pxa.e-invoice.pagination/1`), which pins the page box, table geometry, the eight named
columns, row metrics, continuation headings, the totals block, the footer-safe bound and the
description-fragment limit. It carries twelve cases, each fixing the expected page count, rows per
page, tax groups per page, which page carries the net and payable totals, and which lines were
fragmented. `EInvoicePaginationContractTests` (C#) and `eInvoicePaginationContract.test.ts`
(TypeScript) each assert their own implementation against the file. Neither implementation depends
on the other; PXA.Generator takes no dependency on the Designer.

Aligning the two exposed three further divergences, all fixed:

- The Designer never implemented the `formatter` metadata its templates carried, so a tax rate
  rendered as `19` where the PDF wrote `19%`, and a unit price as `24.9` where the PDF wrote
  `24.90`. The binding context now publishes the rendered text for both.
- `PdfPage.ResolveFont` only honours `Bold` when a font family is named. The hybrid renderer passed
  neither, so every bold heading rendered regular. It now names the Helvetica slot the Designer's
  layouts ask for.
- The Designer's wrapped description used 9-point leading while the contract budgets 11, so the two
  stacked wrapped text differently. The wrapping cell now takes its leading from the contract.

Long descriptions no longer clip. A description longer than a page continues as deterministic
fragments carrying only text - never a repeated amount - and a description beyond eight fragments is
refused with `PXAEINV122` rather than rendered. Large tax breakdowns continue across totals pages
with each group once, the net total once on the first and the payable total once on the last;
zero, one, two, eight and eighty groups are pinned cases.

Four root causes were found and fixed, each verified against the actual text metrics rather than
inferred from the difference images:

- **The Canvas and the Live Preview were drawing Times.** `pdfFontFamily` classified a font stack
  with `normalized.includes('serif')`, which is true of any list ending in the generic `sans-serif`
  keyword. Every ordinary `Arial, Helvetica, sans-serif` stack therefore routed to the serif slot
  while the PDF used Helvetica. `wrapPdfText` carried the same test and so also picked the serif
  width factor. Both now share one `pdfFontSlot` classifier that strips `sans-serif` before the
  serif test. The maintained invoice layouts name the `Helvetica` slot directly, which the browser
  resolves to the `PXA Helvetica` web font that `editor.css` already loaded from the same
  Liberation Sans files the PDF engine embeds.
- **The browser kerned; the PDF does not.** The generator writes each string as one unkerned show
  operator, so a run is the plain sum of the font's advance widths. Browsers apply the font's kern
  pairs by default: `PARITY-EINV-2026-0042` in 12pt bold measures 137.39pt unkerned - which is what
  the PDF's own `/W` table sums to, and where its `Td` places the run - and 135.19pt kerned. Two
  points is enough to move every right-aligned amount and to shift a wrap point. Document elements
  now set `font-kerning: none` and disable ligatures, scoped to `[data-element-id]` so only
  rendered document content is affected and the Designer's own interface keeps normal typography.
- **The reference bitmap was distorting small text.** The harness rasterized at 72 DPI, where a
  12pt line has a 12-pixel em and poppler grid-fits every glyph to that coarse raster; the rounding
  accumulates into whole pixels of ink width. It now renders at 4x and box-filters down, so the
  comparison measures the geometry the PDF encodes instead of the hinter's error. Both the backend
  and the C# PDF take the identical path, so their parity is unaffected.
- **Two earlier layout defects**, carried over from the previous handoff: the maintained layouts
  pinned no typeface at all, and the payable amount shared its right edge with the currency code so
  the code was drawn over the last digits of the total. Both are fixed in `invoiceTemplates.ts`,
  whose column table is now non-overlapping by construction.

Known limitation - scripts outside Liberation Sans. The pixel fixtures deliberately contain no
Arabic, Hebrew or CJK content. Those need the Noto faces listed in `fonts/README.md`, which this
repository does not carry, and the PDF engine implements no cursive shaping, so an Arabic run is
drawn as unjoined glyphs in visual order while a browser shapes and joins it. Pixel parity is
unreachable there by design. An earlier revision of the fixture did include an Arabic line; it
reported a small, anti-aliasing-shaped difference that in fact concealed a row of missing-glyph
boxes in the reference PDF, which is precisely the kind of misleading evidence a parity gate must
not produce. Right-to-left content remains covered functionally, in `eInvoiceWorkspace.test.tsx`
and the canonical-invoice tests.

For reference, the pre-existing `all-elements` fixture reports **P0 1** (`optionlist`), **P1 0, P2
24, match 14**. That P0 is unrelated to this work and unchanged by it: the backend draws a real
AcroForm list box while the Canvas draws a bulleted list. It does not pass the enforced gate.

No remaining item is internally actionable: everything still open depends on the external artifact
dependency above. Per-provider migration conversion fixtures, previously the one internal gap, are
now covered by `PdfEInvoiceMigrationConversionTests`.

Two defects were found and fixed while proving the completed work, both of which
made correct invoices look wrong:

- The PDF/XML consistency comparison offered only two-decimal amount forms, so an
  invoice in a currency with zero or three minor units was reported as needing
  review.
- A multi-rate invoice's aggregate tax total was never rendered, so it could not
  be confirmed on the page. `EInvoice.CreatePdf` now draws a "Tax total" row when
  there is more than one tax group; single-rate output is unchanged.

Both mattered beyond the report, because `attach` refuses an inconsistent invoice
without an explicit approval.

Validation at handoff: solution build 0 errors; PXA.Generator.Tests 104/104;
PXA.Importer.Tests 645/645; PXA.Migration.Pdf.Tests 99/99; PXA.Api.Tests 692 pass
/ 66 skipped (all `[PostgreSqlFact]`) / 0 fail; Designer 604/604 jest, `tsc
--noEmit` clean, production build succeeds; Documentation 45/45 contract tests and
site build; all four enforced electronic-invoice visual-parity commands exit 0;
versioning `check`, `fragments`, `pxa-version` 22/22 and `pxa-release-author` 9/9
clean; `git diff --check` clean. The migration from an earlier handoff was applied
to the local development database, without which every host-booting test fails
closed on a pending migration.

Authenticated smoke workflow: pending execution. `pxa-designer/e2e/e-invoice-authenticated.spec.ts`
covers registration through the real Account handoff, the full editor workflow, save and reload, XML
and hybrid-PDF generation, reopening in the Viewer, byte-identical XML extraction, and the
unauthenticated, missing-entitlement and API-unavailable refusals, on desktop and at phone width. It
is gated behind `PXA_RUN_LIVE_AUTH_EINVOICE=1`, the same opt-in convention
`pdf-viewer-trust-authenticated.spec.ts` uses, so it skips rather than fails without the stack.

An opt-in run was attempted against a locally running API, PostgreSQL, Mailpit, Designer and Account.
It blocked in the shared registration helper: the Account consent checkboxes render `disabled`
because the legal-document endpoints return 403 to an anonymous caller in this local API
configuration. That is upstream of anything the spec does - the pre-existing live PDF Trust workflow
is gated off for the same class of reason - so the selectors past registration are unverified
against a live stack.

One PXA.Importer.Tests run failed a single test that the immediate re-run passed,
and the failure summary did not name it. The long-standing intermittent below is
the likely candidate.

`PdfViewerAnnotationsControllerTests.ExtractAnnotations_ReturnsNativeAnnotationsAsSidecar`
fails intermittently under full-suite parallel load - it passed in four of six
full runs and always passes in isolation. It exercises PDF annotation extraction
and touches no e-invoicing code path.

### EINV-01 - Official Validation Artifact Governance

- Confirm redistribution and runtime-use terms for every required FeRD/FNFE
  and KoSIT artifact before adding it to the repository or release image.
- Record source URL, upstream version, license, retrieval date, SHA-256,
  supported standard/profile, and update policy in the validator manifest.
- Make startup and CI fail closed when an enabled artifact is missing,
  modified, expired by policy, or inconsistent with its manifest entry.
- Completion evidence: reviewed license record, pinned artifacts, deterministic
  manifest verification tests, and no network access during invoice validation.

Internally complete; externally blocked. The offline runner is finished and tested. Manifest schema
version 2 requires a governance record - upstream family, upstream version, retrieval date, licence
review state with reviewer and date, supported profiles, update policy and expiry - and every
artifact is pinned by SHA-256 and re-verified per run. Missing files, altered hashes, unknown
manifest versions, profile or family mismatches, incomplete provenance, an unapproved licence review
and an expired set each produce `unavailable` with a stable diagnostic (`PXAEINV020`, `022`, `026`,
`027`, `028`, `029`, `030`), never a verdict and never a silent downgrade to structural recognition.
Legacy version 1 manifests are deliberately refused so they cannot bypass the governance gate.
Mounted directories are Git-ignored in every layout and validation performs no network access.
Evidence: `tests/PXA.Generator.Tests/EInvoiceArtifactGovernanceTests.cs` (27) and
`EInvoiceConformanceValidatorTests`. Mount conventions for local development, CI, Docker Compose and
isolated On-Premise installations are documented in the guide.

**Still blocked:** the licence and redistribution review itself. No FeRD/FNFE or KoSIT artifact is in
this repository, so no set has ever been run through the gate in anger.

### EINV-02 - Complete Standards Validation

- Execute the correct XSD, Schematron, code-list, arithmetic-total, tax, and
  business-rule set for each supported Factur-X, ZUGFeRD, and XRechnung profile.
- Keep structural recognition, PXA business validation, and official
  conformance as separate statuses in every report.
- Map validator output to stable `PXAEINV...` diagnostics without discarding
  the original rule identifier, location, severity, or artifact-set evidence.
  *Implemented.* Every finding now carries a `PxaEInvoiceOriginalFinding` with the validator's own source, rule
  identifier, severity, location, message and test, beside the PXA code. Schematron advisory rules
  (`flag="warning"`) are reported as warnings and do not invalidate a document.
- Completion evidence: profile matrix tests covering valid documents, each rule
  family, incorrect profiles, malformed totals, and unsupported versions. The rule families and
  profile matrix need the official artifact sets and remain blocked on EINV-01.

### EINV-03 - Existing-PDF Attachment Workflow - done

- [x] Add an authenticated `attach` operation below
  `/api/pxa/v1/pdf/e-invoices` that embeds validated XML into an existing PDF.
- [x] Preserve all visible source pages and supported metadata; never silently
  replace, rasterize, or redesign the source invoice.
- [x] Preflight PDF/A-3 conversion requirements, signatures, encryption,
  conflicting attachments, XMP, output size, and semantic PDF/XML consistency.
- [x] Require explicit approval for destructive transformations and for adding a
  later revision to a signed document; preserve the original file when the
  operation is blocked or requires review.
- Completion evidence: byte-level attachment extraction, unchanged-page visual
  comparison, PDF/A-3 validation, authorization, limits, and negative tests.

Implementation. `PdfEInvoiceAttacher` in `src/Importing/PXA.Importer/EInvoicing`
appends the invoice as an incremental revision when the source passes PXA's
structural PDF/A-3b validation, so the original bytes are the byte prefix of the
result and no page object can change. A source that merely claims PDF/A-3 or whose
archival identification forbids embedded files is converted through
`PdfA2bConverter` to PDF/A-3b first, which is a rewrite and therefore requires
`allowArchiveConversion`. Signed sources require a separate explicit approval:
a rewrite removes their signatures, while an incremental attachment preserves
the signed bytes but creates a later revision. Every attachment
is verified after it is written: the invoice is re-extracted byte for byte, the
per-page content digests are compared, and the result is re-validated against the
PXA PDF/A-3b structural validator. `PdfEInvoiceXmpWriter` merges pdfaid part 3
and the Factur-X extension schema into the existing XMP packet instead of
replacing it, so Dublin Core and other publisher metadata survive. Diagnostics
use the stable `PXAEINVATTACH0xx` range; the API adds `PXAEINVAPI020-024`.

Evidence.

- `tests/PXA.Importer.Tests/PdfEInvoiceAttacherTests.cs` - 24 tests: byte-prefix
  page preservation, byte-for-byte extraction, retained unrelated attachments,
  XMP schema and metadata preservation, blocked unapproved conversion, approved
  conversion, conflict detection including reserved invoice filenames, approved
  replacement, source and output size limits, unsafe filenames, invalid MIME
  types, empty payloads, encrypted sources, false PDF/A-3 claims, and signed
  incremental updates.
- `tests/PXA.Api.Tests/PdfEInvoicesAttachControllerTests.cs` - 17 tests covering
  the `attach` and `attach/preflight` endpoints, approval gates, the semantic
  consistency gate, recognized-versus-unknown preservation, and the stable
  Problem Details codes.
- Blocked operations throw `PdfEInvoiceAttachmentBlockedException` carrying the
  preflight report and return `409`; the caller's bytes are never written to.

### EINV-04 - Complete API Contract - done

- [x] Mark the API umbrella complete only after create, attach, extract, inspect,
  and validate are all authenticated, documented, rate-limited, entitled, and
  covered by stable Problem Details contracts.
- [x] Add OpenAPI examples for Factur-X, ZUGFeRD, and XRechnung and document the
  difference between structural, business-rule, and official conformance status.
- Completion evidence: endpoint contract tests, generated OpenAPI review, and
  permission tests for every role and operation.

Implementation. All seven operations - `generate`, `create`, `attach`,
`attach/preflight`, `validate`, `inspect`, `extract` - carry exactly one
registered Designer permission policy, share the `designer-code` rate-limit
policy, declare a `RequestSizeLimit`, and sit behind the `generator` product
entitlement enforced by `PxaProductAccessMiddleware`. `attach` received its own
`designer.pdf.e-invoices.attach` permission, which no read-only role holds.
`PxaEInvoiceOpenApiTransformer` documents each operation, its approvals and its
limits, and repeats the three-state validation table on every operation so a
caller cannot mistake structural recognition for conformance.

Evidence.

- `tests/PXA.Api.Tests/PdfEInvoicesApiContractTests.cs` - 28 tests: one policy
  per action, route-to-permission agreement, entitlement coverage, bounded
  request sizes, rate limiting, declared Problem Details responses, the
  role matrix across all five roles, and the generated OpenAPI content
  including one request example per maintained standard.
- `tests/PXA.Api.Tests/PxaSecurityContractsTests.cs` and
  `PxaPdfTrustSecurityTests.cs` extended with the attach routes.

### EINV-05 - Designer Invoice Experience - done

- [x] Add typed invoice bindings and maintained invoice templates without moving
  tax, accounting, or billing-domain logic into `PXA.Pdf`.
- [x] Support seller, buyer, dates, references, currency, lines, tax groups, and
  totals with localized validation and a visible conformance state.
- [x] Ensure Designer preview, generated XML, visible PDF, and reopened Viewer
  inspection use the same canonical invoice values.
- [x] Align the Designer projection and the dedicated hybrid-invoice renderer on one pagination
  and long-description overflow contract.
- Completion evidence: autosave/roundtrip tests, six-language UI coverage, export/reinspection
  fixtures, the shared versioned pagination contract, maximum-length unbroken-token tests and four
  passing enforced visual-parity gates covering normal backend and hybrid PDF output.

Route and entry point. The workspace is `/pdf/e-invoice`, reachable from the PDF
hub sidebar between *PDF Viewer* and *Migrations*, gated by the
`designer.pdf.e-invoices.read` permission, the `generator` product entitlement and
the `designer.e-invoice` feature entry in `product-metadata/designer-features.json`.
It renders distinct unavailable, loading, empty, unauthorized (through the shared
`RequireDesignerPermission` wall), offline and validation-failure states.

Editor. `EInvoicePage.tsx` with `InvoiceLinesTable`, `InvoiceTotalsPanel`,
`InvoiceValidationStates` and `InvoiceDiagnosticsPanel`. Sections cover the
standard and profile, invoice number and references, issue and due date, currency
and minor units, seller and buyer with addresses, buyer reference, and the invoice
lines with quantity, unit, unit price, tax category and rate. Lines can be added,
removed and reordered. Tax groups and the three totals are read-only `output`
elements computed by `calculateInvoice`, so no total is independently editable.
Selecting a standard pins its maintained version - displayed, never editable - and
filters the profile list; switching to XRechnung leaves `XRECHNUNG` as the only
option. Diagnostics keep their `PXAEINV...` codes visible and each carries a
control that focuses the field it is about.

One canonical copy. `template.eInvoice.invoice` is the only editable copy of any
invoice value. The page elements the Canvas, the Live Preview and the exported PDF
render are a projection of it, produced by `projectInvoiceElements`, regenerated on
every change, each keeping the `binding` path it came from, and never read back
into the invoice. The XML and the PDF come from the backend from the same object.

Pagination. A long invoice is projected onto as many A4 pages as it needs.
`invoicePagination.ts` plans the Designer pages from bounds recorded in `InvoiceTemplateLayout`:
the table starts at y 272 on page one and at y 82 on a continuation page, a row is 18pt unless its
description wraps, no row may cross y 740,
and the totals block - 150pt, plus 16pt for each tax group beyond the first two - must end above
y 790. An ordinary row is never split across a page; a description taller than a whole page is
continued in deterministic text-only fragments without repeating amounts. With the standard layout that gives **26 rows on page
one and 36 on each continuation page**; the totals follow the final line when there is room and
take a page of their own when there is not. Concretely, a one-tax-group invoice fits on a single
page up to 19 lines, on two pages up to 56, and gains a page every 36 lines after that; with eight
tax groups the taller totals block lowers the first-page figure to 14 and the two-page figure to
50. Tax summaries that exceed a page are split across safe totals pages. Tokens wider than the
description column are segmented at Unicode boundaries before planning, so a valid 4,096-character
description cannot be treated as one line and silently clipped.

Line identity is persisted next to the canonical lines as Designer-only `lineIds`; it is moved or
removed with its line and never enters generated invoice XML. A page is
`page-einv-<template>-<n>` and projected elements use their template id plus a stable line, group,
page or totals scope. Editing, removing or reordering one line therefore does not silently attach
another line's business content to its former element IDs.

Persistence. Because the invoice lives on the template,
`useDesignerTemplateAutosave` carries it: the same two-second debounce, revision
conflict handling, offline state and unsaved-changes warning as any other Designer
document, with no second save path. Saving, Saved, Unsaved changes, Retrying,
Failed, Offline and Conflict are surfaced, with *Load the server draft*, *Save as a
new draft* and *Download a local copy* where they apply.

Template insertion. `commitEInvoice` writes the pages, the elements and the invoice
payload in one store transaction, so inserting a maintained layout - and every later edit,
however many pages it regenerates - is a single undo step. Projected elements get collision-free
deterministic ids as described under Pagination; the layout definitions hold no literal invoice
value, because their labels are i18n keys resolved at insertion time.

Localization and accessibility. Rendered in all six Designer languages, with Arabic
right-to-left through the shared logical-property layout. Every input has a real
label, the lines use table semantics with `scope` on every header and a caption,
icon-only buttons carry accessible names, and results are announced through a
polite live region.

Two defects were found and fixed while proving this:

- `conformanceStateFor` and the Designer API client checked for a
  `"validated"` conformance status, but the backend emits `"valid"`,
  `"invalid"`, `"unavailable"` or `"notEvaluated"`. The Designer would therefore
  never have shown a real conformance verdict. The vocabulary now matches, an
  unknown status is never promoted to a claim, and `unavailable` has its own state.
- `VisualParityPage` wrapped only the canvas surface in
  `DesignerAuthContextProvider`, so `LivePreview` threw `useDesignerAuth must be
  used inside DesignerAuthGate` and the preview surface never rendered. The whole
  visual-parity pipeline was hanging on every fixture, not just the new one. The
  preview surface now gets the same context.

Evidence.

- `pxa-designer/src/__tests__/eInvoiceWorkspace.test.tsx` - 43 tests: route and
  gating states, permission-driven action enablement, standard/version/profile
  filtering, add/remove/reorder lines, exact totals and tax groups with a
  0-minor-unit currency, field-linked diagnostics and `aria-invalid`, the three
  validation states kept separate, template insertion as one undo step, projection
  re-rendering on edit, autosave through the two-second pipeline, revision conflict,
  offline, XML generation and download, PDF generation with consistency reporting
  and Viewer handoff, `notEvaluated` never shown as consistent, a stable Problem
  Details code on refusal, six-language rendering, Arabic RTL with table semantics,
  labelled and keyboard-reachable inputs, live-region announcements, and one
  canonical copy of every invoice value.
- `pxa-designer/src/__tests__/eInvoicePagination.test.ts` - 20 tests: a short invoice on one page
  with the totals under the last line; the first page filled exactly to capacity; one line beyond
  capacity breaking without loss; column headings repeated on every page that carries rows and the
  address block on page one only; several continuation pages; eight tax groups each projected once
  and stacked; the totals moving to a final page of their own; a 120-line invoice with no element
  outside the page box, no two rows overlapping and no line missing, duplicated or reordered; a
  wrapped description growing its cell and pushing the next row down; every id stable across an
  edit that adds no row; new ids only for an added row; a removed row dropping only its own ids;
  canonical order preserved after a reorder; a deterministic replan; and no row split across a
  page. Then, through the real store: every generated page persisted for autosave, one undoable
  transaction per edit however many pages it regenerates, the A4 page box the layout was measured
  against, and a canonical round-trip proving the stored invoice carries no page or element
  identity - so XML and PDF generation keep reading the canonical invoice, not the projection.
- `pxa-designer/src/__tests__/eInvoicePaginationContract.test.ts` - 19 tests asserting the Designer
  projection against the shared pagination contract: the schema version, page and table geometry,
  the eight non-overlapping columns, all twelve pinned cases (page count, rows per page, distinct
  lines per page, totals pages, tax groups per page, which page carries the net and payable totals,
  fragmented lines and overflow), every row inside the page and in canonical order with fragments
  in sequence, fragments rejoining to exactly the wrapped description, and a description beyond the
  bound being reported rather than rendered, and a 4,096-character unbroken Unicode token being
  segmented without clipping or character loss.
- `pxa-designer/src/__tests__/eInvoiceVisualParityFixture.test.ts` - 9 tests generating and
  drift-guarding four fixtures - single-page, multi-page, long-description and many-tax-group - and
  a canonical-invoice sidecar for each, which is what lets the parity run render the dedicated
  hybrid PDF. The single-page fixture is asserted to cover seller and buyer, three lines, two tax
  rates, a two-decimal currency, long wrapping text, and German, French and Central European
  diacritics, with the Factur-X 1.09.2 identity, and to contain no script the PDF engine cannot
  reach parity on. The multi-page fixture is asserted to break into real continuation pages
  carrying every line exactly once in canonical order, with the address block on one page, headings
  on each page that has rows, the totals on the last page, and every tax group present.
- `pxa-designer/src/__tests__/eInvoiceDesigner.test.ts` - 22 tests (model,
  templates, localized validation, conformance vocabulary).
- `pxa-designer/src/__tests__/eInvoiceTotalsParity.test.ts` - 17 tests over the shared corpus
  `test-assets/e-invoicing/totals-parity.json`, which now pins a `multi-page-projection` case of 60
  lines across three tax rates. The same case is asserted independently by
  `EInvoiceTotalsParityTests` in C# and put through the real endpoints by
  `PdfEInvoiceCanonicalValueParityTests`, which creates the visible PDF, embeds the XML and reads
  the values back out with `inspect`. A long invoice's seller, buyer, every line, every tax group
  and the net, tax and payable totals therefore survive the Designer projection, the generated XML,
  the hybrid PDF and a reopened Viewer inspection with one set of values.
- `appRouteSmoke.test.tsx` extended: the route is reachable and the sidebar lists
  the entry.
- `tests/PXA.Generator.Tests/EInvoicePaginationContractTests.cs` - 18 tests asserting
  `EInvoicePageLayout` against the same contract file from C#, independently of the Designer, plus
  a case-by-case replay of all twelve pinned plans, the description-fragment bound and safe
  maximum-length token segmentation.
- `tests/PXA.Api.Tests/PdfEInvoiceFailClosedTests.cs` - 31 tests for the refusals PXA can prove
  without any official artifact: malformed XML, a DTD, an external entity, non-invoice XML, a
  syntax contradicting the declared standard, an unmaintained version, a profile the standard does
  not define, non-positive quantities, negative prices, out-of-range tax rates, duplicate line
  identifiers, a due date before the issue date, an empty invoice, arithmetic reaching the XML
  unaltered, more lines than the synchronous limit, over-long descriptions and party names,
  malformed currencies and minor units, a non-PDF upload, an empty upload, a missing upload, page
  text contradicting the embedded XML, a document with no extractable text never being called
  consistent, and no validation path reporting `valid` without an artifact set.
- `tests/PXA.Api.Tests/PdfEInvoiceCrossSurfaceContractTests.cs` - 15 tests that the Designer, API,
  Viewer inspection, migration catalog, generator, importer recognition and published documentation
  agree on the maintained version per standard, the profiles offered, the syntax of each standard,
  the reserved filenames, the four-state conformance vocabulary, the canonical binding paths, the
  documentation anchors the migration catalog points at, and one set of figures for a two-rate
  invoice carried from generation through the finished PDF to a reopened inspection.
- `tests/PXA.Api.Tests/PdfEInvoiceBoundaryTests.cs` - 24 tests on both sides of every internal
  limit: a valid 4 096-character description with no whitespace at all (broken across lines, never
  shortened), astral-plane scalars that survive wrapping with no split surrogate and no replacement
  character, mixed-script text, one character above the limit refused, the maximum supported line
  count accepted and one beyond refused, zero-, two- and three-decimal currencies reaching the XML
  with their own digits, half-a-minor-unit rounding away from zero, negative line values refused
  because credit notes are not supported, the reserved attachment filenames enforced, unsafe
  filenames refused, and exactly one attachment under the reserved name.
- `tests/PXA.Generator.Tests/EInvoicePdfaFixtureTests.cs` - 4 tests generating the deterministic
  PDF/A corpus, proving each fixture is byte-identical across two generations, pinning every
  SHA-256, and asserting the multi-page, long-description and many-tax-group fixtures really
  exercise what they claim.
- `tests/PXA.Generator.Tests/EInvoiceArtifactGovernanceTests.cs` - 27 tests on the fail-closed gate
  in front of a mounted official artifact set, including that a refusal never reports structural
  recognition as a conformance result, and that a failing rule keeps the validator's own identifier,
  severity, location, wording and test.
- `pxa-designer/e2e/e-invoice-authenticated.spec.ts` - the authenticated desktop and mobile smoke
  workflow. **Written and type-checked, execution pending**; see the note below.
- `tests/PXA.Migration.Pdf.Tests/PdfEInvoiceMigrationConversionTests.cs` - 27 tests converting
  representative PXA-authored source snippets for every registered provider family, covering all
  six capabilities, the three dispositions, the stable `CANMIGEINV00x` identifiers, word-boundary
  matching, source-ordered diagnostics and markers, and the rule that migration output never claims
  conformance.

Visual parity. `test-assets/visual-parity/e-invoice.json` and
`test-assets/visual-parity/e-invoice-multipage.json` are deterministic fixtures generated from the
canonical invoice and drift-guarded by their test. `tools/visual-parity/run.mjs` compares the
Designer Canvas, the Live Preview, the backend PDF, the JSON-to-C#-generated PDF and the dedicated
hybrid invoice for each; the fixture key is derived from the filename. All four fixtures pass with
P0 0 and P1 0, matching Designer/hybrid page counts and `csharp` RMSE 0. See the "Visual parity
status" note for the exact measurements and scope.

### EINV-06 - Migration Provider Coverage - done

- [x] Add explicit capability mappings for providers exposing Factur-X,
  ZUGFeRD, XRechnung, PDF/A-3 attachment, or invoice-validation APIs.
- [x] Report `automatic`, `reviewRequired`, or `unsupported` per capability;
  never turn a source API into a silent TODO or an unsupported conformance claim.
- [x] Link each mapping to the unified migration catalog, permission, example,
  diagnostics, and documentation.
- Completion evidence: provider coverage report and representative conversion
  tests for every mapped provider family. Both present; see Conversions below.

Implementation. `PdfEInvoiceMigrationCatalog` covers all 16 PDF code-migration
providers across six capabilities: Factur-X, ZUGFeRD, XRechnung, PDF/A-3
attachment, extraction and validation. It reuses the existing
`PdfTrustMigrationDisposition` vocabulary, so the unified catalog reports one kind
of answer.

The mappings are keyed on API identifiers a customer's source may contain, not on
a claim about which invoice features each vendor ships - PXA can honestly say
"if your code calls this, here is the PXA equivalent and what a person still has
to confirm", and cannot honestly assert a third party's current feature list.
Every capability of every provider carries a disposition, a rationale and a
documentation link, which is what makes a silent TODO impossible.

Conversions. `PdfEInvoiceMigrationCatalog.Analyze` turns the table into an answer: given a source
file, it reports each invoice capability the code touches, the PXA API that replaces it, the
disposition, and a stable `CANMIGEINV00x` identifier - `001` Factur-X, `002` ZUGFeRD, `003`
XRechnung, `004` PDF/A-3 attachment, `005` extraction, `006` validation. Markers match on word
boundaries, so `Attachments` is not found inside `CustomAttachmentsHelper`. When the source names a
standard the provider has no API for - including through the reserved `factur-x.xml` filename - the
capability is reported `unsupported` with what to do instead, rather than passed over because only
a neighbouring call was recognised. `PdfEInvoiceMigrationConversionTests` covers every registered
provider family, all six capabilities, all three dispositions and the rule that no migration finding
claims conformance.

Dispositions follow from what the mapping actually knows:

- Attachment and extraction are `automatic` where the provider has the API,
  because embedding and reading back an embedded file is byte-preserving on both
  sides and PXA additionally verifies the declared size and checksum.
- Factur-X, ZUGFeRD and XRechnung generation is never `automatic`: the standard
  version and profile are a business obligation the source usually leaves implicit.
- Validation is never `automatic`: the detected call may validate PDF/A structure
  rather than invoice rules, and a migration must not turn that into a conformance
  claim.

Evidence.

- `tests/PXA.Migration.Pdf.Tests/PdfEInvoiceMigrationCatalogTests.cs` - 27 tests:
  full provider coverage against `PdfTrustMigrationCatalog`, a disposition and a
  rationale for every capability, markers and a PXA equivalent present exactly
  when a capability is supported and absent when it is not, no rationale that is a
  TODO, generation and validation never automatic, and unknown keys failing loudly.
- `tests/PXA.Migration.Pdf.Tests/PdfEInvoiceMigrationCoverageReportTests.cs` -
  renders the published table in
  `docs/guides/PVAD.Unified-Migration-Provider-Coverage.md` from the catalog and
  rewrites it when it drifts, so the report cannot fall behind the code.
- `tests/PXA.Migration.Pdf.Tests/PdfEInvoiceMigrationConversionTests.cs` - feeds
  representative PXA-authored source snippets through `PdfEInvoiceMigrationCatalog.Analyze`
  for every registered provider family, covering all six capabilities, the three
  dispositions, the stable `CANMIGEINV00x` identifiers, word-boundary matching and
  the rule that no finding claims conformance.
- `PXA.Migration.Pdf.Tests` pass in full.

### EINV-07 - Maintained Fixture Corpus - PXA-authored part done, official part blocked

- [ ] Add licensed official valid and invalid fixtures for every maintained
  syntax/profile combination, plus PXA-generated deterministic fixtures.
- [x] Include multilingual, multi-tax-rate, rounding, credit-note where
  supported, large-line-count, malformed, and adversarial XML cases.
- [x] Store provenance, license, expected result, and SHA-256 for every fixture.
- [x] Completion evidence: a machine-readable corpus manifest checked in CI.

Implementation. `test-assets/e-invoicing/corpus/` holds 14 deterministic
PXA-authored fixtures and `manifest.json`, which records for each one its path,
SHA-256, byte size, provenance, licence, standard, version, profile, syntax,
expected result, expected diagnostic codes, tags and a description. The manifest
declares three provenance kinds and permits only `pxa-authored` today:
`official-licensed` stays empty until EINV-01 has reviewed and recorded
redistribution terms, and `customer` is never permitted.

Coverage: valid Factur-X BASIC and EN 16931, multiple tax rates including a
zero-rated category-K line, multilingual content (German umlauts and a sharp s,
French accents, right-to-left Arabic, CJK), half-away-from-zero rounding, 500
lines, UBL Invoice and UBL CreditNote for XRechnung, inconsistent totals as a
business warning, a missing guideline identifier, unsupported vendor syntax, a
malformed document, and three adversarial cases - an internal DTD entity, a
billion-laughs entity chain and an external-entity file disclosure.

Evidence.

- `tests/PXA.Generator.Tests/EInvoiceFixtureCorpusTests.cs` - 40 tests: the
  SHA-256 and byte size of every fixture, the declared outcome of every fixture,
  provenance and licence recorded, no customer data and no unreviewed official
  artifact, no fixture claiming conformance, manifest and directory in step,
  the required categories still covered, both syntaxes and the standards
  represented, every adversarial fixture failing closed without any entity
  content reaching a diagnostic message, the large fixture staying within the
  bounded input limit, and the multilingual fixture surviving extraction
  byte-exact. The hash check is a gate: a changed fixture fails rather than
  silently redefining what the corpus proves.
- 85/85 `PXA.Generator.Tests` pass.

Blocked. Official FeRD/FNFE and KoSIT fixtures cannot be added until EINV-01
records their redistribution terms, so the first sub-item stays open. No fixture
here is presented as official, which is why the corpus makes no conformance
claim.

### EINV-08 - Independent Hybrid-PDF Validation

- Validate generated Factur-X/ZUGFeRD hybrid invoices with official
  FeRD/FNFE tooling and an independent PDF/A validator such as veraPDF.
- Validate XRechnung XML with the pinned KoSIT configuration independently
  of PXA's own validator.
- Publish tool versions, commands, hashes, raw reports, and normalized
  machine-readable summaries without overstating unavailable checks.
- Completion evidence: reproducible passing evidence for every supported output
  profile and intentional failing fixtures proving the gate is effective.

PDF/A half done. The pinned veraPDF workflow now runs over a deterministic corpus of hybrid invoices
and its evidence is retained. Latest run, veraPDF 1.31.166 via `ghcr.io/verapdf/cli:v1.31.166`:
**6 compliant, 1 non-compliant, 0 notEvaluated, 0 unexpected** across seven PDF/A-3b fixtures -
Factur-X, ZUGFeRD and XRechnung single-page, plus multi-page, long-description and many-tax-group,
plus a deliberately non-PDF/A control which veraPDF rejected on ISO 19005-3:2012 clauses 6.2.4.3 and
6.6.2.1. The control is what proves the gate is effective: a validator that passed it would not be
validating. Fixtures are regenerated rather than committed, each pinned by SHA-256 in
`test-assets/e-invoicing/pdfa-fixtures.json`, and the runner refuses to validate a file whose
checksum does not match. Reproduce with:

```
dotnet test tests/PXA.Generator.Tests/PXA.Generator.Tests.csproj \
  --filter FullyQualifiedName~EInvoicePdfaFixture
node tools/pdfa/einvoice-pdfa-evidence.mjs
```

Raw veraPDF reports and the normalized record land in the ignored `artifacts/e-invoicing/pdfa/`.

**Still blocked.** veraPDF proves PDF/A properties only - it is not Factur-X, ZUGFeRD or XRechnung
conformance. Official FeRD/FNFE and KoSIT validation still needs the artifact sets, and
`tools/pdfa/README.md` additionally requires a recorded pass from a second, separately licensed
validator before independent PDF/A conformance is claimed. Neither is available here, so this item
stays open.

### EINV-09 - Real-World Semantic Consistency - synthetic part done, licensed corpus blocked

- [ ] Exercise PDF/XML comparison against licensed real-world and multilingual
  invoices, including alternative number/date formatting and rounding.
- [x] Keep scanned or text-inaccessible PDFs at `notEvaluated` unless bounded OCR
  evidence is available; never treat missing text extraction as consistency.
- [ ] Completion evidence: reviewed corpus results with false-positive and
  false-negative thresholds and retained original fixtures.

Implementation. Two false negatives were found by putting the parity corpus
through the real endpoints and both were fixed:

- Amount matching offered only two-decimal forms, so a correct invoice in a
  currency with zero or three minor units was reported as needing review.
  Variants are now also generated at the value's own decimal scale.
- A multi-rate invoice's aggregate tax total appeared nowhere on the rendered
  page, so it could not be confirmed. `EInvoice.CreatePdf` now draws a
  "Tax total" row when there is more than one tax group; single-rate output is
  unchanged, because there the group row already is the total.

Both mattered beyond the report: `attach` refuses an inconsistent invoice unless
the caller approves it, so before the fix every multi-rate invoice and every
invoice in a 0- or 3-minor-digit currency would have needed an override.

Scanned and text-inaccessible documents stay at `notEvaluated`. Missing text
extraction is never treated as agreement, and `attach` distinguishes the two:
`notEvaluated` proceeds and is reported, `reviewRequired` is refused without an
explicit approval.

Evidence.

- `tests/PXA.Api.Tests/PxaEInvoicePdfConsistencyInspectorTests.cs` - 7 tests
  pinning both fixes, the scale actually reported for an amount, the single-rate
  layout staying as it was, a genuinely contradicting amount still being
  reported, and missing page text staying `notEvaluated`.
- `tests/PXA.Api.Tests/PdfEInvoiceScannedDocumentTests.cs` - 3 tests through the
  real `inspect` and `attach` paths with a graphics-only page: every finding
  `notCheckable`, none `confirmed`, the invoice still read and reported, the
  attachment proceeding while reporting `notEvaluated`, and a readable page that
  contradicts the invoice still being refused with `PXAEINVAPI021`.
- `tests/PXA.Api.Tests/PdfEInvoiceCanonicalValueParityTests.cs` - all nine parity
  cases now report `consistent` with no `notFound` finding.
- The multilingual corpus fixture proves extraction is encoding-safe for
  umlauts, accents, right-to-left Arabic and CJK.

Blocked. Licensed real-world invoices cannot be used until their redistribution
terms are recorded, and customer invoices are never permitted, so the first and
third sub-items stay open. Bounded OCR evidence is also not yet available, which
is why a scanned page is never compared rather than compared weakly.

### EINV-10 - Unknown XML Preservation - done

- [x] Allow explicitly requested byte-preserving storage and extraction of
  unknown invoice XML while marking it non-editable and non-conformant.
- [x] Apply the same size, filename, MIME, DTD/entity, checksum, and tenant
  boundaries used for recognized documents.
- Completion evidence: exact-byte roundtrip and UI/API status tests showing that
  preservation never becomes a standards claim.

Implementation. `attach` and `attach/preflight` accept `preserveUnrecognized`
plus an optional `attachmentFileName`. Preservation is opt-in: without the flag,
unrecognized XML is still refused with `PXAEINVAPI020`. In preservation mode PXA
deliberately withholds every standards signal - no reserved filename
(`PXAEINVAPI026`), no Factur-X XMP extension schema, `/AFRelationship` stays
`/Unspecified` rather than `/Alternative`, no semantic values are extracted, and
the validation report says not structurally valid, not conformance validated,
`notEvaluated`. The same bounds as a recognized invoice still apply: the 10 MiB
upload limit, a plain ASCII `.xml` filename (`PXAEINVAPI025`), the
`application/xml` media type, the bounded DTD-free and entity-free parse
(`PXAEINVAPI027`), the PDF `/Params` size and MD5 checksum written and
re-verified by the attacher, the `designer.pdf.e-invoices.attach` permission, and
the tenant-scoped `generator` entitlement enforced by `PxaProductAccessMiddleware`.
`inspect` reports `recognitionStatus` and `isEditable` so no surface can present
preserved XML as editable invoice data.

Evidence.

- `tests/PXA.Api.Tests/PdfEInvoicePreservedXmlTests.cs` - 17 tests: refusal
  without the flag, exact-byte roundtrip through `extract`, unrecognized and
  non-editable status from `inspect` and `attach/preflight`, absence of the
  invoice extension schema in the result, all four reserved filenames rejected,
  unsafe filenames rejected, DOCTYPE and malformed XML rejected, empty uploads
  rejected, and a recognized invoice coexisting with preserved XML in one PDF.

### EINV-11 - Historical Validation Evidence - done

- [x] Persist immutable validation reports with tenant, document checksum,
  validator artifact-set ID/hash, result, diagnostics, timestamps, and actor.
- [x] Add bounded retention, authorization, audit, export, and revalidation
  behavior when a newer artifact set becomes available.
- Completion evidence: tenant-isolation, immutability, retention, and historical
  reproducibility tests.

Implementation. `EInvoiceValidationRecord` (table
`administration.e_invoice_validation_records`, migration
`20260927050514_AddEInvoiceValidationRecords`) stores organization, actor, audit
event, operation, document SHA-256, container SHA-256, standard, version,
profile, syntax, guideline identifier, the three validation states, the validator
artifact-set ID and SHA-256, the verbatim diagnostics, the revalidation link and
both timestamps. A PostgreSQL trigger rejects every `UPDATE` and rejects a
`DELETE` unless the transaction has deliberately set
`pxa.e_invoice_retention_purge`, so retention is the only way a row can leave the
table. `PxaEInvoiceValidationEvidenceService` scopes every read to the caller's
organization inside the service - no route parameter can widen it - writes a
paired `AuditEvent` carrying only checksums, states and diagnostic codes (never
invoice content), and keeps a superseded record when a later revalidation cites
it. `validate` and `attach` retain their verdicts; `GET validations`,
`GET validations/{id}`, `GET validations/export` and
`POST validations/{id}/revalidate` read, export and extend them. Revalidation
requires the same document bytes, proven by checksum (`PXAEINVAPI032`), and
stores a new verdict beside the old one rather than replacing it.

Evidence.

- `tests/PXA.Api.Tests/PdfEInvoiceValidationEvidenceTests.cs` - 25 tests:
  complete stored evidence and its audit event, artifact-set identity retained,
  cross-tenant history and by-id reads blocked, cross-tenant revalidation
  chaining refused, tenant context required, checksum format enforced, expired
  records hidden then purged, a cited record kept, both verdicts retained across
  an artifact-set change, the four endpoints, fail-closed behaviour without an
  evidence store, and the route-to-permission mapping.
- `tests/PXA.Api.Tests/EInvoiceValidationRecordImmutabilityTests.cs` - verified
  against PostgreSQL 17 in a container: the migration applies, `UPDATE` and an
  unguarded `DELETE` are rejected with SQLSTATE 55000, the stored verdict is
  unchanged afterwards, and only the guarded retention delete removes the row.

### EINV-12 - Product Boundaries and Public Documentation - done

- [x] Document generation, attachment, inspection, extraction, validation,
  Designer binding, migration, supported versions/profiles, and known limits.
- [x] State clearly that Peppol transport, national tax submission, accounting,
  payment, and legal-retention services are external integrations, not PDF-engine
  capabilities.
- [x] Publish API and C# examples plus a compatibility matrix and downloadable,
  machine-readable validation evidence.
- Completion evidence: Documentation build, link checks, runnable examples, and
  terminology parity across API, Viewer, Designer, and Migration.

Implementation. `docs/guides/PVAD.EInvoicing.md` is the public guide: what PXA is
not, the supported versions and profiles, the three validation states, generation
with C# and HTTP examples per standard, the attachment workflow with its approval
table, unknown-XML preservation, inspection and consistency, extraction,
validation and retained evidence, Designer integration, migration, a
compatibility matrix, known limits, the permission table and the diagnostic
ranges. An "Electronic invoicing" topic in the documentation site carries the
same statements.

The boundary is stated first, not buried: Peppol transport, national tax
submission, accounting, payment and legal retention are external integrations,
and the guide says plainly that PXA deliberately has no API for any of them.

Downloadable machine-readable evidence is
`GET /api/pxa/v1/pdf/e-invoices/validations/export`, which returns the tenant's
retained verdicts - each with its validator artifact-set identifier and hash - as
one JSON document, plus the checked-in corpus manifest.

Evidence.

- `websites/PXA.Documentation/tests/e-invoice-documentation-contract.test.js` -
  13 tests: the topic exists, the three states are kept apart in both the site and
  the guide, the maintained versions and profiles are stated, every operation is
  documented, the attachment approvals and preservation rules are stated, missing
  page text is never presented as agreement, the product boundary is stated in
  both, the compatibility matrix and known limits are published, C# and HTTP
  examples are present, permissions and diagnostic ranges are documented, the
  Designer binding and migration mapping are explained, terminology matches the
  provider coverage report, and every relative link resolves to a file that
  exists. Prose assertions are whitespace-insensitive so they survive reflowing.
- 45/45 documentation contract tests pass; the documentation site builds.
- The known-limits section states outright that no official artifacts are shipped
  and no independent validation result is published, so the documentation does not
  imply a conformance PXA has not established.

### EINV-13 - Final Release Gates

- Prove generated PDF/A-3 output with PXA and independent validation.
- Prove supported XML against the pinned official XSD and Schematron sets.
- Prove invalid XML, profile mismatches, incorrect totals, unsafe payloads,
  and attachment conflicts fail closed with stable diagnostics.
- Verify Viewer, API, Designer, Migration, tests, and Documentation report the
  same standard, version, profile, syntax, and conformance state.
- Add or finalize the customer-facing `minor` release fragment only after all
  preceding P0 gates pass; do not change `VERSION` on this feature branch.
- Completion evidence: full solution/build matrix, external validation bundle,
  authenticated desktop/mobile smoke tests, and reviewed release text.

## Acceptance Criteria

- [x] A supported invoice XML can be embedded in a PDF/A-3 document and extracted
  byte-for-byte.
- [x] Typed invoice data can produce deterministic XML and a visible PDF/A-3
  hybrid invoice whose embedded semantic values match its rendered content.
- [ ] The generated PDF/A-3 structure passes PXA and independent validation.
- [ ] Supported XML passes the pinned official XSD and Schematron rules.
- [x] Invalid XML, mismatched profiles, invalid totals, and unsafe payloads fail
  closed with stable diagnostics.
  Evidence: `tests/PXA.Api.Tests/PdfEInvoiceFailClosedTests.cs` (31) and
  `tests/PXA.Api.Tests/PdfEInvoiceBoundaryTests.cs`.
- [x] Viewer, API, Designer, Migration, tests, and Documentation agree on standard,
  version, profile, syntax, and conformance state.
  Evidence: `tests/PXA.Api.Tests/PdfEInvoiceCrossSurfaceContractTests.cs` (15).

## Assumptions

- Structural recognition is useful but is never marketed as invoice conformance.
- ZUGFeRD and Factur-X share a technical hybrid format while retaining product
  identities and version labels. Current ZUGFeRD 2.5.2 output uses the harmonized
  `factur-x.xml` filename and Factur-X XMP namespace; legacy ZUGFeRD metadata is
  import compatibility only.
- XRechnung supports UBL and CII XML; embedding it in PDF/A-3 is a PXA workflow,
  not a claim that every XRechnung exchange requires a PDF.
- Official validation artifacts are pinned only after their redistribution terms
  have been reviewed.
