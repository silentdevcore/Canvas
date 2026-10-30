# PVAD JSON Naming Cleanup

## Goal And Baseline

Normalize editable technical names to PVAD and product names to PVADE. Preserve
exact compatibility identifiers and immutable evidence rather than replacing
every textual match. No data migration, key rotation or publication is included.

Baseline: `0137a324`, 478 tracked JSON files, 6,169 previous technical-name matches
in 82 files. The old written-out market name also occurs in one additional
translation file. The completed Markdown cleanup remains a separate milestone.

## P0 - Inventory And Boundaries

- [x] Record every baseline finding with its file, JSON pointer, key/value kind and classification.
- [x] Classify findings as rename, compatibility contract, historical evidence or incidental integrity data.
- [x] Preserve Legal reference registers, their hash pins and all published or signed evidence.
- [x] Keep diagnosis keys, cookie names, license key IDs, schema aliases and operational bindings unchanged.

## P0 - Editable Content

- [x] Normalize Admin guidance, storage descriptions and migration error text.
- [x] Correct OpenAPI descriptions at their generators and refresh the snapshot reproducibly.
- [x] Canonicalize current fidelity format, provider and provenance labels while retaining legacy reads.
- [x] Update editable synthetic PDF, Spreadsheet, Word and performance examples and their expectations.
- [x] Preserve geometry, numerical results, review decisions and validation thresholds.

## P1 - Guard And Validation

- [x] Add a structural tracked-JSON guard covering keys, values, comments and filenames.
- [x] Exclude private IDE files, untracked notes, symlinks and ignored output.
- [x] Bound every retained finding by exact location and expected value hash, or immutable file hash.
- [x] Reject new, changed, stale or unclassified exceptions in CI.
- [x] Test six-language lookup coverage, legacy readers, schemas and operational references.
- [x] Pass naming, fidelity, Legal, API and website contracts plus Designer typecheck and build.
- [x] Verify protected originals byte-for-byte and retain the Markdown zero-match gate.
- [x] Add a Patch release fragment without changing VERSION or published releases.
- [x] Resolve every original editable finding and document the retained remainder.

## Evidence

The [JSON audit](PVAD.JSON-Naming-Audit.json) records baseline provenance and the disposition of each
finding. Its own historical tokens are evidence, not current product naming.
The exact audit bytes are pinned by the existing residual review ledger.

## Verified Results

- All 83 affected baseline files are classified: 60 key/value findings renamed,
  5,773 findings deliberately retained, and zero unclassified findings. Findings
  are whole keys or scalar values and can contain multiple substring matches.
- Twenty-one existing JSON files were updated. Fifty original evidence and Legal
  files remain byte-identical. No lockfile, published release or Legal hash pin changed.
- All six language catalogs retain their 234 diagnostic-key findings and lookup
  mappings. User-visible translation values contain no previous product names.
- The final structural scan covers 478 tracked JSON files. Sixty-nine still
  contain precisely approved compatibility identifiers or evidence; none is an
  unfinished editable rename. Contract tests also include the new audit and release
  fragment explicitly, before Git tracking includes them.
- OpenAPI changes are limited to 13 descriptions generated from the actual
  transformers. Routes, operation IDs, reader fields and wire codes remain unchanged.
- Node contract suites: 511 passed; Designer: 92 suites, 809 tests and one snapshot
  passed; API: 74 passed; Word/Spreadsheet exports: 13 passed. No tests skipped.
- Designer typecheck and production build passed. Company, Account, Admin,
  Documentation and Demo production builds passed. Existing compiler and bundler
  warnings remain outside this naming cleanup.
- Markdown naming remains at zero matches; fidelity and Legal validators pass.
  Product version remains 1.1.0. No database, runtime identifier or publication changed.

## Reproduction

```sh
node tools/configuration/json-naming-audit.mjs --check
node tools/configuration/markdown-path-audit.mjs --check
node tools/document-fidelity/fidelity.mjs validate
node tools/legal/validate-legal-content.mjs
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
```

For a reproducible OpenAPI description refresh, run
`OpenApiNamingSnapshotTests` with `PVAD_OPENAPI_NAMING_PATCH` set to an absolute
JSON path inside the repository's `artifacts/` directory. Then pass that projection
to `node tools/configuration/refresh-openapi-descriptions.mjs <projection.json>`.
The refresh rejects changed operation identities and preserves unrelated bytes.

## Separate Release Gates

- [ ] Perform previously deferred external deployment and infrastructure cutovers.
- [ ] Complete independent Legal review and authorized publication separately.

These release gates are not prerequisites for editorial source cleanup and are
not marked complete by this work. Do not commit or push without explicit approval.
