# PXA Proven Document Fidelity

## Goal

Prove, rather than assume, that supported documents preserve content and layout across import,
migration, Designer, Preview, and export. The backend PDF remains the printable visual reference.

## P0 - Evidence Contract

- [x] Add one versioned corpus manifest covering PDF, Word, Spreadsheet, Designer, and migration
  evidence.
- [x] Record provenance, redistribution permission, synthetic versus real-world status, workflow,
  source format, and official-support status for every fixture.
- [x] Define one result contract using `exact`, `compatible`, `reviewRequired`, and `unsupported`.
- [x] Measure missing and duplicate elements, geometry drift, text preservation, page/sheet counts,
  and visual difference where the workflow supports those metrics.
- [x] Calculate automatic-migration percentages only from reviewed, officially supported migration
  fixtures.
- [x] Report `notMeasured` instead of claiming success when the official denominator is zero.
- [x] Add deterministic validation, aggregation, Markdown/JSON reports, and automated tests.
- [x] Validate the corpus and reviewed baseline in CI.

## P0 - Existing Evidence Integration

- [x] Register the all-elements Designer/Preview/backend/C# PDF visual fixture.
- [x] Register the mixed portrait, landscape, and custom-page layout fixture.
- [x] Register the current Word fidelity sample pack without presenting generated files as
  real-world source documents.
- [x] Register independently produced PDF Trust fixtures as validation-only evidence.
- [x] Feed the live Designer visual-parity report into the unified result contract.
- [x] Feed the live Word-to-PDF fidelity report into the unified result contract.
- [ ] Add a repeatable Spreadsheet-to-XLSX-to-Spreadsheet fidelity report.
- [ ] Add adapters for PDF code migration, Spreadsheet code migration, and Report Designer
  migration reports.

## P1 - Representative Corpus

- [ ] Add legally redistributable, real-world PDF fixtures for invoices, forms, reports, books,
  certificates, annotations, signatures, RTL, accessibility, and mixed page sizes.
- [ ] Add legally redistributable, real-world DOCX fixtures covering sections, headers, footers,
  fields, tables, images, lists, page breaks, RTL, and tracked changes.
- [ ] Add legally redistributable, real-world XLSX fixtures covering formulas, formatting,
  validation, merged cells, charts, images, print areas, RTL, and multiple sheets.
- [ ] Store immutable source hashes and licensing notes for every external fixture.
- [ ] Keep confidential customer documents outside the repository and publish only anonymized,
  approved measurements.
- [ ] Review and mark the supported subset explicitly; an accepted extension alone is not proof of
  fidelity.

## P1 - Quality Gates

- [ ] Require no missing or duplicated elements for `exact` and `compatible` results.
- [ ] Require no more than one point of geometry drift for supported visual workflows.
- [ ] Require stable text, page/sheet count, RTL, fonts, tables, and page breaks.
- [ ] Require at least 95 percent automatic migration across officially supported fixtures.
- [ ] Fail CI when an approved fixture regresses from automatic migration to manual review.
- [ ] Publish breakdowns by domain, format, provider, workflow, and fixture provenance.
- [ ] Require explicit limitations and diagnostics for every `reviewRequired` or `unsupported`
  result.

## P2 - Product And Documentation

- [ ] Expose a customer-safe fidelity report after import or migration.
- [ ] Link every manual-review item to the affected page, sheet, element, or source line.
- [ ] Add side-by-side source/result review in Designer and Viewer where licensing permits it.
- [ ] Document supported fidelity levels and known limitations in PXA.Documentation.
- [ ] Publish only reviewed aggregate numbers; do not expose customer files or internal paths.
- [ ] Add provider- and format-specific compatibility pages generated from approved evidence.

## Validation

- [x] Unit-test invalid manifests, duplicate IDs, missing files, ineligible official fixtures,
  classification rules, and zero-denominator reporting.
- [x] Unit-test exact, compatible, review-required, and unsupported aggregation.
- [x] Run `node tools/document-fidelity/fidelity.mjs validate`.
- [x] Run `node tools/document-fidelity/fidelity.mjs evaluate`.
- [x] Run `node --test tools/document-fidelity/fidelity.test.mjs`.
- [ ] Run all live visual, Word, Spreadsheet, and migration adapters in CI.
- [ ] Review and approve the first real-world cross-format baseline.

## Acceptance Criteria

- [x] One manifest identifies what evidence exists and what is still missing.
- [x] One command produces machine-readable and human-readable fidelity summaries.
- [x] Unmeasured support can never appear as a successful percentage.
- [ ] PDF, DOCX, and XLSX each have a representative reviewed real-world corpus.
- [ ] Every officially supported migration pipeline meets the 95 percent automatic-migration gate.
- [ ] Designer, Preview, normal PDF, and C# PDF remain within their approved visual thresholds.
- [ ] Known limitations are visible in product documentation and migration results.

## Current Baseline

- The repository has strong synthetic Designer/PDF and Word-export coverage.
- PDF Trust includes independently produced files, but these prove standards and security
  interoperability rather than general document-layout fidelity.
- No checked-in, reviewed real-world DOCX or XLSX fidelity corpus currently exists.
- No migration fixture is counted as officially supported until its provenance, expected outcome,
  and reviewed result are recorded in the manifest.
- Therefore the official automatic-migration percentage is currently `notMeasured`, not 100%.
