# PVAD Final Rename Handoff

## Assignment And Authority

Baseline: `89bfe27987c93be1bc230e6d34fbe8f787482072`, branch
`chore/pvad-release-readiness`, product version `1.1.0`, reviewed 2026-11-08.
Refresh these facts before starting if the branch has advanced.

Finish every locally actionable naming follow-up in one coordinated workstream,
then report the exact retained contracts and separately blocked operational work.
Do not stop after an arbitrary number of replacements or claim completion from
a falling search count. This handoff is an internal task document, not permission
to publish, deploy, break supported contracts or erase original evidence.

Technical identity: **PVAD**. Market, website and Designer identity: **PVADE**.
PDF project extension: `.pvadp`. Spreadsheet project extension: `.pvads`.
Domain selected by the owner: `pvade.com`; support: `support@pvade.com`;
technical sender: `no-reply@pvade.com`. Selection does not prove provisioning.

The source-only rename has already passed its bounded acceptance audit. There
are no known freely renameable current findings awaiting implementation at this
baseline. Remaining matches are a review queue and retained contracts, not 20,170
known bugs. If new evidence reveals a current naming defect, fix it rather than
using the acceptance statement or a whole-file exception to hide it.

## Read First

- [Rename plan](PVAD.Rename-Plan.md): overall scope, historical gates and approvals.
- [Remaining items](PVAD.Rename-Remaining-Items.md): latest consolidated boundaries.
- [Source review and execution evidence](PVAD.Remaining-Code-Naming-Cleanup.md).
- [Source acceptance](../docs/pvad-rename-source-acceptance.md).
- [Exceptions and their scoped corrections](../docs/pvad-rename-exceptions.md).
- [Exact file/hash/reason ledger](../docs/pvad-rename-reviewed-exceptions.json).
- [HTTP contracts](../docs/pvad-http-compatibility.md).
- [Identity and protected data](../docs/pvad-identity-compatibility.md).
- [License and job compatibility](../docs/pvad-license-and-job-compatibility.md).
- [Public identifier register](../docs/reference/compatibility-identifiers.v1.json).
- [Restricted identifier register](../operator-docs/compatibility-identifiers.v1.json).
- [Release approvals and owner decisions](../docs/pvad-rename-release-approvals.md).
- [Final acceptance and gates G01-G08](../docs/pvad-rename-final-acceptance.md).
- [Versioning instructions](../tools/versioning/README.md).

Read newer dated decisions before treating an older unchecked sentence as an
unimplemented source defect. Historical results and recorded task states must
not be rewritten to imply a fresh test or approval.

## Safety And Execution Rules

- Preserve unrelated user changes. Private `.vscode/` is untracked: never inspect,
  stage, edit, delete or include it in reports. Also exclude other private IDE,
  credential and Git internals, untracked notes and symlinks.
- Search tracked regular files, including known source formats containing null
  bytes. Review tracked ignored vendor inputs separately; never recursively scan
  arbitrary ignored customer data or generated local storage.
- Do not run a repository-wide replacement across protocols, encoded payloads,
  checksums, signatures, existing credentials, certificates or evidence.
- Preserve the confirmed one-major compatibility period and stable diagnostics.
  This handoff does not revoke those decisions. Ask for a specific contract-family
  retirement decision only if removal is actually necessary; continue independent
  work while waiting. Otherwise retain and explain the exact occurrences.
- Do not reset databases, rotate keys, invalidate sessions, change installed
  infrastructure, register publishers or weaken authorization to reduce matches.
- Use focused manual patches or mechanical replacements only after confirming
  the exact candidate set and updating producers and consumers together.
- No commit, push, merge, tag, version increase, publication or deployment without
  a separate explicit request. A local test is not staging or production approval.

## Work Package A: Reproducible Complete Inventory

- [ ] Record HEAD, branch, dirty files and VERSION without reading private files.
- [ ] Run `node tools/configuration/repository-naming-inventory.mjs --write-report`.
- [ ] Run `node tools/configuration/rename-artifact-audit.mjs` and the residual,
  Markdown and structural JSON guards listed below.
- [ ] Cross-check the current inventory against every entry in the exact review
  ledger and both identifier registers; do not work from a short list of examples.
- [ ] Add a separate tracked-ignored vendor inventory and a binary evidence review
  with paths and hashes. Include case variants, joined identifiers, expanded old
  product names and filenames. Distinguish neutral drawing APIs from branding.
- [ ] Give every current occurrence a concrete action: fix current output, retain
  supported input/contract, preserve original evidence, preserve third-party or
  incidental bytes, or block on a named approval. Record locations and hashes,
  not credentials, tokens, customer content or complete source lines.

The post-commit short-name inventory currently scans 3,003 nonignored tracked
regular files: 596 matching files, 20,170 text matches, 153 binary matches and 42
path matches. It excludes 228 tracked ignored vendor files. The earlier closeout
scanned 3,229 files including that vendor set; two new release fragments account
for the subsequent tracked-file increase. Counts describe scope, not completion.
The register itself is deliberately self-excluded by its review guard; inventory
it as register data rather than pretending it has an independent approval entry.

The generated reports live under `artifacts/rename/` and may not exist on another
checkout. Regenerate them there. The tracked review ledger is the portable exact
per-file index; do not attach a stale ignored report as the sole handoff evidence.

## Work Package B: API, Diagnostics And Artifact Contracts

- [ ] Review current and retained routes in `PVAD.WebApi/Infrastructure/`, especially
  `PvadLegacyApiMiddleware.cs`, `PvadAuthorizationPaths` under `Security/`, security
  header normalization, response headers, configuration aliases and API problems.
- [ ] Verify canonical requests and new response/header writers use current names;
  check controller attributes, OpenAPI generation, examples, CORS exposure and
  consumers. Reject near-prefix matches, conflicting aliases and blank credentials.
- [ ] Review `src/Core/PVAD.Core/Contracts/PvadDiagnosticAliases.cs`, configured
  diagnostic JSON projection, Worker diagnostics and all six language catalogs.
  Preserve authoritative IDs, severities, positions, meanings and lookup keys.
- [ ] Verify UI presentation prefers reviewed canonical aliases where appropriate
  without changing raw diagnostics, support traces, document text or stored data.
  Unknown and third-party diagnostics must not acquire invented prefix aliases.
- [ ] Review `src/Migrations/Common/PVAD.Migration.Abstractions/MigrationArtifactIdentities.cs`
  and unified migration controllers. Canonical routes must emit the current tuple;
  retained adapters keep their documented tuple. Do not duplicate artifacts.
- [ ] Review PDF, Spreadsheet and Report domains separately; verify permissions,
  preview, downloads, resource references and Open-in-Designer/Viewer behavior.
- [ ] Verify metadata producers and old readers for charts, redaction resources,
  XML export and generated element markers. Preserve conflicts and diagnostics;
  normalize only explicitly supported old inputs.

These are verification/remediation tasks, not evidence that the current writers
are still wrong. Removing old routes, raw diagnostic IDs, compiler-warning IDs,
headers or fields is conditional on a separately approved breaking transition.

## Work Package C: Identity, MCP, Licensing And Protected Storage

- [ ] Review `PVAD.WebApi/Security/` cookie names/managers, authentication schemes,
  Data Protection, claim/scope adapters, OAuth options, registry, bootstrap and
  client/scope transition utilities together with their callers.
- [ ] Verify new writers are canonical and retained readers fail safely. Preserve
  host-only cookies, separate Account/Designer tickets, PKCE, CSRF, revocation,
  expiry, tenant isolation, scopes, entitlements and feature gates.
- [ ] Review `tools/PVAD.Mcp/src/remote/config.ts`, `remote/app.ts` and the remaining
  MCP sources/package/scripts. Verify current discovery, CLI output, resources,
  OAuth/delegation writers and compatibility readers without expanding authority.
- [ ] Review issued API-key readers, license key IDs, signed envelopes, audit
  event keys, project `formatId`, added canonical values, usage and job payloads.
  Never rename issued secrets, signed payloads or persisted history in place.
- [ ] Verify mail and revocation-cache current-purpose writes and bounded previous-
  purpose reads, plus consumer ordering and rollback. Preserve the existing
  keyring, protection discriminator and access until an authorized migration.
- [ ] Review persistence environment aliases, design-time database selection,
  applied migration IDs/SQL and retained purge session keys. New source names do
  not authorize switching the selected database or rewriting applied migrations.
- [ ] Exercise prepared maintenance utilities only against disposable synthetic
  stores: dry-run, idempotency, conflicts, revoked grants, wrong audience, restart,
  credentials and rollback. Record real installed-store operations as blocked.

## Work Package D: Designer, Portals, Formats And Generated Artifacts

- [ ] Review Designer, Account, Admin, Company, Documentation, Demo and shared web
  helpers. Check visible labels, translated diagnostics, metadata, release views,
  support links, runtime configuration, storage notice and the agreed history note.
- [ ] Verify canonical browser storage writes, safe legacy reads/reset/migration,
  document handoffs, favorites, organization switching, auth bootstrap and recovery.
  Do not rename customer filenames, source drafts, uploaded text or audit details.
- [ ] Review `src/Core/PVAD.Core/ProjectFormats/`, chart codecs, code workspace
  markers, schema resources, importers, C# generators and their frontend adapters.
  New packages use current identities; both supported generations remain readable.
- [ ] Inspect actual fresh PDF, JSON, XML, DOCX, ODT, HTML, native PDF/Spreadsheet
  archives and image-export metadata, not just source literals. Verify generated
  C# Model, builder and Base64 representations preserve the canonical design.
- [ ] Inspect installed-app manifest/file handlers, project icons and native opener
  packaging. Existing acceptance is macOS with Chrome and generic project icons;
  do not claim other operating systems or final logo approval.
- [ ] Review ignored local generated outputs separately for stale build identities.
  Rebuild or propose a hash-bounded cleanup; never delete installed data, keyrings,
  backups, paired historical binaries or arbitrary bin/obj directories blindly.

## Work Package E: All Remaining Path Matches And Original Evidence

The current 42 matching paths have been classified as follows. Enumerate their
exact filenames through Work Package A; all must receive an individual outcome.

| Path family | Count | Required treatment |
| --- | ---: | --- |
| Schema resources under `docs/schema/` | 3 | Supported old resource identities. Verify current schema output and retained readers; retirement requires approval. |
| Deployment CLI entrypoint under `tools/deployment/` | 1 | Compatibility shim. Verify canonical entrypoint and delegation; do not delete supported invocations. |
| Native reference archives under `docs/fixtures/native-projects/` | 2 | Original archives. Preserve bytes and paths while testing current-format successors separately. |
| Signature PDFs and independent validator XML under Documentation downloads | 36 | Original signed/hashed evidence. Preserve content, filenames, provenance, links and validation pins. |

- [ ] Verify all 42 paths against current callers, hashes and classifications.
- [ ] Inspect the old local-path shortcut and other binary matches separately;
  do not hex-edit shortcuts, compressed images, OCR models or signed documents.
- [ ] Preserve original Markdown/JSON audit inventories, review notes, published
  releases, accepted Legal database versions, consent hashes and signed licenses.
- [ ] Keep vendor templates, provider API identifiers, RPX metadata and neutral
  Canvas/graphics APIs intact. Incidental encoded substrings are not rename tasks.
- [ ] Re-evaluate editable wrapper text or synthetic metadata if discovered; do
  not let whole-file evidence classification protect a demonstrably current label.
- [ ] Do not hide evidence in a renamed directory or broaden an allowlist to
  achieve zero hits. A real evidence successor needs new provenance and review.

## Work Package F: Optional Compatibility Retirement

This package is blocked by the existing preservation decisions, not by missing
search-and-replace tooling. Preparation and tests may proceed; activation may not.

- [ ] Produce one exact retirement matrix covering API/header/field aliases,
  diagnostics/compiler warnings, schemas/formats, generated-code markers, browser
  storage/cookies, OAuth/claims/scopes, MCP resources/configuration, CLI shims,
  cryptographic readers and infrastructure adapters. Link every affected source,
  consumer, test and existing register entry; mark families that must remain.
- [ ] For each family record canonical writer, retained reader, affected stored
  data, removal impact, migration/conversion path, backup, rollback and evidence.
- [ ] Ask the owner to approve precise removals and the compatibility window before
  implementing breaking changes. No published customers does not mean no local
  data, active sessions, old fixtures or protected consumers exist.
- [ ] After approval, change each producer/consumer/test family atomically, add
  explicit rejection/migration tests and a Major breaking-change fragment. Preserve
  historical originals and document any deliberate session invalidation separately.
- [ ] Do not manufacture a second runtime implementation or duplicate every
  diagnostic merely to reduce search matches. Reuse existing transition utilities.

## Work Package G: External Cutover And Release Gates

These are the complete historical gate families, not unfinished repository
branding. Leave them open until the corresponding real evidence exists.

| Gate | Remaining action | Dependency |
| --- | --- | --- |
| G01 | Confirm NuGet/npm publishers and publication rights; verify selected domain/mail identities. | NuGet/npm not registered; GHCR owner selection is not publication permission. |
| G02 | Verify installed key access, signing-key recovery, retained consumers and storage rollback. | Named real environment, owner authorization and secure backups. |
| G03 | Drain workflow generations; migrate protected environments and live monitoring identities, silences, routing and history. | Current remote inventory, preserved controls and explicit activation approval. |
| G04 | Validate actual DNS/TLS/proxy, CORS/CSP, OAuth callbacks, sender authorization, SPF/DKIM/DMARC and webhook destinations. | Real provisioned hosts/providers; local configuration is insufficient. |
| G05 | Authorize a target-specific cutover with maintenance window, scope, backups and rollback point. | Explicit environment/release owner decision. |
| G06 | Run representative non-local staging, authorization negatives, delivery and recovery. | No non-local staging is currently confirmed. Localhost is not a substitute. |
| G07 | Approve and prepare Major release through normal protected PR, CI, tags, artifacts and registries. | Publisher rights, release approval and validated prerequisites. VERSION stays unchanged now. |
| G08 | Name production/recovery operators and a genuinely independent reviewer; verify cutover/restore/rollback. | No independent reviewer confirmed; the owner's second account is not one. |

- [ ] Re-read actual current owner decisions before acting; do not reopen already
  settled domain/sender choices or infer registration from example names.
- [ ] Keep all eight gates open where required evidence is absent. Prepare exact
  commands, expected results and rollback offline instead of performing live work.
- [ ] Do not restore paused automatic dependency PRs, change GitHub protection,
  transfer repositories, alter secrets or introduce unrelated dependency upgrades.

## Implementation Order And Ownership

One coordinated delivery does not mean concurrent edits to shared contracts.
Use this order: inventory, approval classification, independent domain changes,
consumer integration, exact ledger review, tests, consolidated acceptance report.

Independent agents may review API/identity, frontend/formats, and infrastructure/
evidence in parallel. Give one integrator ownership of shared diagnostic aliases,
registries, schema/OpenAPI snapshots, root plan, version metadata and review ledger.
Review each agent's actual diff against this assignment before integration.

## Required Validation

Run from the repository root unless a working directory is specified:

```bash
git diff --check
node tools/configuration/repository-naming-inventory.mjs --write-report
node tools/configuration/rename-residual-review.mjs
node tools/configuration/markdown-path-audit.mjs --all --check
node tools/configuration/json-naming-audit.mjs --check
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
node --test tools/configuration/*.test.mjs tools/versioning/*.test.mjs
dotnet build PVAD.sln --configuration Release
npm --prefix pvad-designer run type-check
npm --prefix pvad-designer test -- --runInBand
npm --prefix pvad-designer run build
```

- [ ] Discover all tracked `tools/` and `deploy/` `.test.mjs` files through Git and
  run the complete selection; the shorter command above is not all tooling tests.
- [ ] Run affected .NET suites for Core, API, Worker, persistence, import/export,
  migrations and relay. If auth/persistence changes, use isolated PostgreSQL and
  existing explicit opt-ins; skipped integration tests are not successful tests.
- [ ] For collector verification build real Release hosts plus relay with correct
  dependency configuration; run the existing collector script and separate audit.
  Do not fabricate output manifests or reuse stale pre-change DLLs/reports.
- [ ] Run each affected website's declared tests/build, Account typecheck, MCP
  tests/build, DocFX and public output checks. Do not substitute a Vite build for
  DocFX. Keep protected operator guidance out of public output.
- [ ] Exercise actual authenticated Account-to-Designer handoff, permission denials,
  code workspace, templates, imports, migrations, native open/save, preview,
  export/download and PDF printing on desktop/mobile after affected changes.
- [ ] Verify protected evidence byte-for-byte and each updated exception against
  its exact changed occurrence. Preserve original reasons and append corrections;
  matching a newly generated hash does not establish semantic approval.
- [ ] Check fresh built bundles, OpenAPI and exported files separately from source.
  Regenerate only editable snapshots via their generators; never repin approved
  baselines or loosen thresholds just to obtain a pass.
- [ ] Scan new/untracked deliverables explicitly before commit because tracked-only
  guards do not see them yet. Every tracked Markdown file must remain free of the
  former short name in all casings, even inside identifiers or code blocks.

Prior evidence: 573 broad Node tests, 94 RDL tests, a 955-test/52-host collector
smoke run and seven separate relay tests passed in their recorded runs. They are
baseline evidence, not freshly executed tests of the next agent's changes. The
later RDL rebuild changes its ignored DLL; do not assert all earlier build hashes
still identify final outputs. Never combine overlapping test counts as a unique
total or present a smoke selection as the entire .NET suite.

## Documentation And Release Bookkeeping

- [ ] Update the source checklist, remaining-items list and acceptance guide only
  for actual new findings/results. Preserve historical inventories and dated results.
- [ ] Explain every retained contract in the final report; do not rewrite immutable
  public/operator registers or Legal hash pins. Use a separately versioned successor
  register if an approved transition needs new mappings.
- [ ] Use the local release-author skill. Internal handoff/audit-only work is
  `none`; compatible shipped fixes require appropriate fragments; approved removal
  of supported contracts is `major`, category `breaking`, with `breaking: true`.
- [ ] Keep VERSION and published release history unchanged until separately
  authorized release preparation. Do not describe planning as delivered behavior.

## Definition Of Done And Return Report

- [ ] No newly identified freely renameable current name remains unresolved.
- [ ] Every residual file/path has an exact verified action or preservation reason;
  no unclassified findings, silent exclusions or directory-wide new exceptions.
- [ ] Approved changes preserve behavior/security or explicitly document tested
  breaking transitions. Unapproved retirement and live cutover stay blocked.
- [ ] Report changed files/contracts, tests actually run, skips/failures, artifact
  locations, preserved hashes, owner decisions needed and per-gate remaining work.
- [ ] Give separate statuses for repository naming, compatibility retirement and
  deployment/release. Do not claim global zero matches or invent an overall percent.
- [ ] Provide the full diff for final review. Stop before commit or publication
  unless the owner explicitly requests those operations.

The previously noted stored-template adapter rendering limitation is a separate
product defect, not rename work. Other operating-system associations, final logo
approval and unrelated feature roadmaps are also not silently included here.
