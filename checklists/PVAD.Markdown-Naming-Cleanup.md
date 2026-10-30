# PVAD Markdown Naming Cleanup

## Goal

Remove every previous-brand substring from tracked Markdown, including historical
records, examples, paths and embedded identifiers. Technical naming is PVAD;
the market and Designer brand is PVADE. Original source remains in Git.

## P0 - Inventory And Contract Boundaries

- [x] Record all 272 baseline Markdown files and the 2,058 matches in 113 files.
- [x] Store the complete baseline occurrence inventory as adjacent JSON.
- [x] Separate public compatibility identifiers from restricted operator values.
- [x] Preserve real wire values, storage keys, diagnostic IDs and signed evidence.

## P0 - Markdown Cleanup

- [x] Normalize prose, tables, code, links and historical records without exceptions.
- [x] Separate recorded outcomes from normalized rerun references.
- [x] Resolve current paths or describe retired components without invented targets.
- [x] Replace exact compatibility literals with neutral register references.
- [x] Preserve existing checkbox states, dates, scores and recorded result numbers.

## P0 - Legal Candidates

- [x] Prepare seven English Swiss-law candidates at document version 1.1.
- [x] Use a neutral legal-entity placeholder and retain existing contractual rules.
- [x] Pin the public compatibility reference by version and SHA-256.
- [x] Keep imports draft-only, idempotent and non-destructive to existing versions.
- [x] Verify publication, acceptance records and historical snapshots remain untouched.

## P1 - Contracts And Delivery

- [x] Add a full-text guard for all tracked Markdown, including uppercase extensions.
- [x] Exclude private IDE files, untracked notes, symlinks and ignored outputs.
- [x] Package only the public register in public documentation builds.
- [x] Validate register provenance, integrity, references and corrected links.
- [x] Pass naming, Legal, Documentation and relevant API tests and builds.
- [x] Add a Patch release fragment without changing the product version or release history.

## Manual Release Gates

- [ ] Import the reviewed candidates through the existing authorized Admin workflow.
- [ ] Obtain independent Legal review and approval before publication.
- [ ] Publish through normal governance and assess renewed acceptance where required.

## Acceptance

The full-text guard reports zero previous-name occurrences in tracked Markdown.
This does not remove compatibility from runtime contracts or rewrite existing
database, signed-document, fixture or release evidence. No commit, push, live
database import or publication is authorized by this checklist.

## Evidence And Commands

- [Baseline occurrence inventory](PVAD.Markdown-Naming-Audit.json): source hashes,
  lines, columns and exact source tokens for every original match.
- [Public identifiers](../docs/reference/compatibility-identifiers.v1.json) are
  packaged identically by Company, Account, Admin and Documentation.
- [Operator identifiers](../operator-docs/compatibility-identifiers.v1.json) stay
  outside public output. They are not copied by the public asset plugin.
- Compared with `30496722`, 114 existing Markdown files preserve all 4,832
  checkbox states; assessment dates, percentage values and recorded result
  numbers remain unchanged. Naming policy text is updated, not a new test claim
  for the historical work.
- All 352 newly added or changed Markdown file links resolve.

```bash
node tools/configuration/markdown-path-audit.mjs --all --check
node --experimental-strip-types --test tools/configuration/*.test.mjs tools/legal/*.test.mjs
node --experimental-strip-types --test websites/shared/*.test.js websites/PVAD.Documentation/tests/*.test.js
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
git diff --check
```

API tests use disposable in-memory data for candidate replay, published-version
preservation, acceptance evidence and conflicting revision rejection. Optional
PostgreSQL integration cases require their separately configured test database;
they do not run against an installed customer or development database.

## Validation Result

- Full-text guard: zero matches across all 272 previously tracked Markdown files;
  the new checklist also contains no previous-brand substring.
- Node contracts: 478 passed, zero failed, including naming, Legal, all four
  affected websites, shared assets, versioning and release-author tests.
- Legal and registration API tests: 58 passed, 11 optional integration cases
  skipped because the PostgreSQL test database was not configured.
- WebApi compilation and Company, Account, Admin and Documentation production
  builds passed. Existing dependency, nullability and Vite loader warnings remain.
- All four website outputs contain the exact hash-pinned public register; no
  operator register is emitted by the public asset plugin.
- Version synchronization, release-fragment validation and diff whitespace
  validation passed. Product version remains `1.1.0`; published release metadata
  and binary evidence are unchanged.
- [Patch release fragment](../product-metadata/release-fragments/pvad-complete-markdown-naming.json)
  records the customer-facing naming correction without publishing a release.

No live database was imported or changed. Import, independent review and
publication remain the three explicit manual release gates above.
