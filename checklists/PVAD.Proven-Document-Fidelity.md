# PXA Proven Document Fidelity

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

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
- [x] Add a repeatable Spreadsheet-to-XLSX-to-Spreadsheet fidelity report.
- [x] Add adapters for PDF code migration, Spreadsheet code migration, and Report Designer
  migration reports.

## P1 - Representative Corpus

- [ ] Add legally redistributable, real-world PDF fixtures for invoices, forms, reports, books,
  certificates, annotations, signatures, RTL, accessibility, and mixed page sizes.
- [ ] Add legally redistributable, real-world DOCX fixtures covering sections, headers, footers,
  fields, tables, images, lists, page breaks, RTL, and tracked changes.
- [ ] Add legally redistributable, real-world XLSX fixtures covering formulas, formatting,
  validation, merged cells, charts, images, print areas, RTL, and multiple sheets.
- [x] Store immutable source hashes and licensing notes for every current external fidelity fixture.
- [ ] Keep confidential customer documents outside the repository and publish only anonymized,
  approved measurements.
- [ ] Review and mark the supported subset explicitly; an accepted extension alone is not proof of
  fidelity.
- [x] Generate a reproducible visual and semantic review packet for the initial PDF, DOCX, and XLSX
  corpus and record the rejected baseline findings.

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
- [x] Run the initial real-world PDF, DOCX, and XLSX import adapter in CI.
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
- The synthetic XLSX roundtrip currently preserves all 18 measured feature groups, including typed
  values, formulas, styles, merges, row and column metadata, page setup, validation, conditional
  formatting, protection, and defined names.
- PDF code, Spreadsheet code, and Report Designer provider-smoke suites now feed the shared evidence
  contract. They remain `reviewRequired` because provider execution does not prove operation-level
  fidelity.
- PDF Trust includes independently produced files, but these prove standards and security
  interoperability rather than general document-layout fidelity.
- An initial OGL v3 real-world corpus now covers a 62-page tagged PDF report, a DOCX guidance
  document, and a large XLSX public-data workbook. All three pass their production import adapters,
  but their first visual and semantic review is rejected: PDF text preservation is 86.46 percent,
  DOCX collapses from three pages to one with 25.82 percent text preservation, and XLSX changes from
  696 to 756 print pages with 74.24 percent text preservation.
- This initial corpus does not yet cover the full representative PDF, DOCX, and XLSX feature lists.
- No migration fixture is counted as officially supported until its provenance, expected outcome,
  and reviewed result are recorded in the manifest.
- Therefore the official automatic-migration percentage is currently `notMeasured`, not 100%.
