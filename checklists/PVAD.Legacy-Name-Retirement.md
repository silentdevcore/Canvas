# PVAD Legacy Name Retirement (Hard Cut)

## Notation

This document follows the Markdown rule that no tracked Markdown file contains the
former short name in any casing. `{L}` stands for the former three-letter short name
in the casing of the original identifier, for example `{L}API010`, `{L}_LICENSE_*`,
`X-{L}-*` or `/api/{l}/v1`. `{E}` stands for the expanded former product name. Use
the [current inventory](../tools/configuration/repository-naming-inventory.mjs) to
resolve exact occurrences; this checklist names families, not complete source lines.

## Owner Decision (2026-11-09)

- The application has not been rolled out and has no users or external API/MCP
  consumers. The owner wants the former short name replaced by PVAD wherever that
  is technically possible.
- Hard cut, no compatibility window: new writers use PVAD only, and legacy readers,
  aliases and dual writers are removed. This is the contract-family retirement
  decision requested by Work Package F of the
  [final rename handoff](PVAD.Rename-Final-Handoff.md) and supersedes the confirmed
  one-major compatibility period for the families listed below.
- Session, cookie, antiforgery and token invalidation is accepted.
- Stored data is migrated (option B), not discarded: a database data migration
  rewrites operational rows and the protected payload keyring is migrated before
  its discriminator changes. Audit and Legal history stay unchanged.
- No commit, push, tag, version increase or publication without a separate request.

## Baseline

Branch `chore/pvad-release-readiness`, HEAD `89bfe27987c93be1bc230e6d34fbe8f787482072`,
VERSION `1.1.0`, analysed 2026-11-09.

- Inventory: 3,003 scanned tracked files, 596 matching files, 20,170 text, 153 binary
  and 42 path matches; 228 tracked ignored vendor files excluded.
- About 12,000 text matches are audit inventories under `checklists/`; about 1,900
  are in 164 non-test product source files.
- All rename guards pass and the 573 tracked `tools/` and `deploy/` Node tests pass.
- Local database (Docker, database named `{l}`, 49 tables) contains the former name in:
  role claim types (67/67), the MCP OAuth application (client ID and scope
  permissions), role descriptions (5/5), code workspace drafts (4/24), background
  and PDF trust job diagnostics (3/37, 12/12), audit details (47/769), Legal
  document versions (7/7) and a few synthetic user names/e-mails. There are no
  stored native project archives (they are downloads, not database rows), no API
  keys, no offline licenses and no OAuth tokens. The mail outbox holds 88 protected rows.

## Never Rewritten

These keep the former name because changing the bytes would break or falsify them.
They need an individual, hash-pinned entry in the successor ledger (see I3).

| Family | Reason |
| --- | --- |
| Signed PAdES PDFs, CRLs, certificates and DSS validator XML under `websites/PVAD.Documentation/public/downloads/pdf-trust/` | Signatures and validator pins break; file names are part of published provenance. |
| External PDF/A and PDF/UA fixtures under `tests/PVAD.Importer.Tests/Fixtures/PdfTrustExternal/` | Third-party produced evidence. |
| Tesseract `tessdata/*.traineddata`, compressed PNG screenshots and icons | Incidental byte sequences, not identifiers. |
| Applied EF migrations and `__ef_migrations_history` rows | Rewriting applied migrations breaks migration state. |
| Published release history, accepted Legal versions, consent hashes | Legal and release evidence. A new Legal version is a separate decision (O2). |
| Audit inventories, review notes, historical checklists and dated results | Historical record; editorial normalization only where already allowed. |
| Vendor templates, provider API identifiers and RPX metadata | Not first-party branding. |

## Retirement Matrix

Columns: current writer, legacy reader/alias, stored data, main sources, action.
Every family changes producers, consumers, tests, docs and registers atomically.

| ID | Family | Writer today | Legacy reader / alias | Stored data | Main sources | Action |
| --- | --- | --- | --- | --- | --- | --- |
| R01 | Diagnostic and problem codes (`{L}API*`, `{L}PDFTRUST*`, `{L}EINV*`, `{L}PDFUA*`, `{L}PROJECT*`, `{L}CODE*`, `{L}_LICENSE_*`, `{L}_DESIGNER_*`, `{L}-MD-*`, `{L}-PDF-CHART-*`, `{L}MCP*`, `{L}MIG*`, `{L}UI*`) | Raw `code` uses `{L}`; additive `canonicalCode` for 494 reviewed IDs | `PvadDiagnosticAliases`, `PvadDiagnosticJsonContract`, UI `displayDiagnosticCode` prefix rewrite | Job diagnostics, audit details | PvadDiagnosticAliases.cs (`src/Core/PVAD.Core/Contracts/PvadDiagnosticAliases.cs`, removed 2026-11-09), [PvadApiProblems.cs](../PVAD.WebApi/Infrastructure/PvadApiProblems.cs), Importer/Generator/Pdf diagnostics, [designerAuth.ts](../pvad-designer/src/auth/designerAuth.ts), six locale catalogs, MCP `core/` | Rename every raw code to `PVAD*` keeping suffix, severity and meaning; remove `canonicalCode`, alias set and UI rewrite; rekey all six locales; rewrite job diagnostics (D4). |
| R02 | Compiler/analyzer warning IDs (`{L}0001` family) | Generator emits `{L}0*` | Suppression docs and tests | None | [PdfDocument.cs](../src/Generation/PVAD.Pdf/PdfDocument.cs), generator tests | Rename to `PVAD0*`; update `NoWarn`/pragma examples. |
| R03 | Problem type URIs on the expanded former domain | Tests only | Problem identity guide | None | API problem tests, [identity guide](../docs/pvad-api-problem-and-health-identities.md) | Remove legacy URI tests; keep only `pvade.com` problem URIs. |
| R04 | HTTP routes `/api/{l}/*` and `/api/{l}/v1` | None (canonical `/api/pvad`) | PvadLegacyApiMiddleware.cs (`PVAD.WebApi/Infrastructure/PvadLegacyApiMiddleware.cs`, removed 2026-11-09), [PvadAuthorizationPaths.cs](../PVAD.WebApi/Security/PvadAuthorizationPaths.cs) | None | Middleware, OpenAPI, examples, route tests | Delete legacy middleware and path branch; add 404 tests for old prefixes. |
| R05 | HTTP and mail headers (`X-{L}-*`, `{L}-*`, `X-{L}-Outbox-Id`, `X-{L}-API-Key`) | Dual writes | [PvadResponseHeaders.cs](../PVAD.WebApi/Infrastructure/PvadResponseHeaders.cs), security header normalization, CORS exposure | None | Response headers, [SmtpMailTransport.cs](../PVAD.WebApi/Services/Mail/SmtpMailTransport.cs), designer `pvadDocumentHeaders.ts`, CORS config | Write PVAD headers only; stop accepting old request headers; update CORS exposure. |
| R06 | Cookies (`__Host-{L}.Session`, `.Designer.Session`, `.Antiforgery` and development variants) | None | [PvadCookieNames.cs](../PVAD.WebApi/Security/PvadCookieNames.cs) legacy readers | Browser only | Cookie managers and tests | Delete legacy readers; sessions invalidate (accepted). |
| R07 | Authentication schemes (`{L}.Combined`, `{L}.ApiKey`, `{L}.McpDelegation`, `{L}.Designer.Cookie`) | None | [PvadAuthenticationSchemes.cs](../PVAD.WebApi/Security/PvadAuthenticationSchemes.cs) | None | Program.cs registration, policies | Remove legacy scheme names and forwarders. |
| R08 | Claims and scopes (`{l}_*` claim types, `{l}.*` scopes) | Canonical PVAD; DB still holds `{l}_permission` | PvadClaimCompatibility.cs (`PVAD.WebApi/Security/PvadClaimCompatibility.cs`, removed 2026-11-09), PvadScopeCompatibility.cs (`PVAD.WebApi/Security/PvadScopeCompatibility.cs`, removed 2026-11-09), MCP `scopeCompatibility.ts` | `role_claims` 67/67, OAuth permissions | Identity seeding, MCP | Data migration D1/D2 first, then delete adapters; scope requests with old names are rejected. |
| R09 | OAuth client IDs (`{l}-mcp-public`, `{l}-mcp-server`, `{l}-sa-<id>`) | Canonical PVAD | [PvadOAuthOptions.cs](../PVAD.WebApi/Security/PvadOAuthOptions.cs) legacy IDs | `oauth_applications` 1/1 | OAuth bootstrap, registry, MCP remote config | Migrate D2; remove legacy IDs and the `{l}-sa-` prefix branch. |
| R10 | API key secret prefix (`{l}_`) | Canonical `pvad_` | [PvadApiKeySecret.cs](../PVAD.WebApi/Security/PvadApiKeySecret.cs) | No API keys stored | API key issue/verify | Accept `pvad_` only. |
| R11 | Data Protection discriminator `{E}` and legacy purposes `{L}.Mail.Outbox.Payload.v1`, `{L}.PdfTrust.RevocationEvidence.v1` | Discriminator `{E}`; PVAD purposes with legacy fallback | [PvadDataProtection.cs](../PVAD.WebApi/Security/PvadDataProtection.cs) | 88 mail outbox rows, revocation evidence | Program.cs keyring setup | D5 re-protection, then discriminator `PVAD` and PVAD purposes only. |
| R12 | Configuration aliases (`{L}:*`, `ConnectionStrings:{L}Database`) and environment variables (`{L}_*` incl. `{L}_DATABASE_URL`, `{L}_VERSION`, `{L}_COMMIT`, `{L}_BUILD_TIME`, test/tool variables) | Canonical `PVAD_*` | PvadConfigurationAliases.cs (`PVAD.WebApi/Infrastructure/PvadConfigurationAliases.cs`, removed 2026-11-09), [PvadEnvironment.cs](../src/Infrastructure/PVAD.Infrastructure.Persistence/Configuration/PvadEnvironment.cs) | `.env` files (local, untracked) | Dockerfiles, compose, scripts, CI | Remove aliases and fallbacks; rename remaining `{L}_*` variables; document the local `.env` update. |
| R13 | Database name, user and password defaults (`:-{l}`, `:-{l}-local`) | Compose defaults | Persistence design-time selection | Local database `{l}` | `deploy/` compose files | D6 renames database and role to `pvad`; defaults become `pvad`. |
| R14 | Browser storage (`{l}_locale`, `{l}_signed_in`, `{l}_export_format`, `{l}-designer-*-v1`, `{l}-code-editor-draft-v2`, `{l}-status-recovery-v1`, `{l}_migration_designer_handoff`, `{l}.designer.auth-transaction`) | Canonical PVAD with migrating reads | `readMigratedStorage` helpers in Designer, Account, Admin | Browser only | [authI18n.ts](../websites/PVAD.Account/src/authI18n.ts), designer hooks, `legacyRecovery.ts` | Drop legacy keys and migration helpers; old local state is discarded (accepted). |
| R15 | Native project formats (`{l}p`/`{l}s` format IDs, `.{l}p`/`.{l}s` extensions, `application/vnd.{e}.*` media types, `{L}-{L}P-1` manifest IDs, `PVADPROJECT_LEGACY_FORMAT`) | Canonical `.pvadp`/`.pvads` | [PvadProjectModels.cs](../src/Core/PVAD.Core/ProjectFormats/PvadProjectModels.cs), [PvadProjectArchive.cs](../src/Core/PVAD.Core/ProjectFormats/PvadProjectArchive.cs), upgrade pipeline, controller | No stored archives; audit `formatId` history | Formats, `DesignerProjectsController`, manifest/file handlers, frontend adapters | Depends on O1. Default: remove legacy reading; keep the two original reference archives as evidence only. |
| R16 | Schema identities (`{l}://schema/*`, `docs/schema/{l}-*.schema.json`, `{l}.e-invoice.*/1` profile IDs) | Mixed | Schema resolver, retained schema files | Workbook JSON in drafts | [docs/schema/](../docs/schema/), e-invoice profiles, workbook API | Move to PVAD schema IDs; delete the three retained schema resources unless O1 keeps legacy import. |
| R17 | Generated-code and metadata markers (`{l}-authored`, `{l}Metadata`, `{L}Code`, `{L}Equivalent`, `{L}.StorageNotice`, `{l}-csharp`, `{l}-spreadsheet*`, `{l}-json`, `{l}-viewer-package`, `application/vnd.{l}.viewer`) | Mostly canonical with legacy readers; some legacy writers | Chart codecs, code element markers, XML export, redaction resources | Code workspace drafts 4/24 | [PvadCodeElementMarker.cs](../src/Core/PVAD.Core/Contracts/PvadCodeElementMarker.cs), [ChartMetadataCodec.cs](../src/Core/PVAD.Core/Primitives/ChartMetadataCodec.cs), [XmlDocumentExporter.cs](../src/Infrastructure/PVAD.Infrastructure.Converters/XmlDocumentExporter.cs), migration result JSON | Write PVAD only; remove readers; migrate drafts (D3). |
| R18 | Migration artifact identities and job payload keys (`{L}Code`, retention purge keys `{l}.e_invoice_retention_purge`) | Mixed | [MigrationArtifactIdentities.cs](../src/Migrations/Common/PVAD.Migration.Abstractions/MigrationArtifactIdentities.cs), job processor | Background jobs | Unified migration controllers, Worker | Canonical tuple only; rewrite queued payloads (D4). |
| R19 | License key IDs (`{l}-development-1`) and license diagnostics | Development config | Offline license validator | No issued licenses | [appsettings.json](../PVAD.WebApi/appsettings.json), [PvadOfflineLicenseValidator.cs](../PVAD.WebApi/Services/Licensing/PvadOfflineLicenseValidator.cs) | New development key ID `pvad-development-1`; no issued license exists, so nothing is re-signed. |
| R20 | MCP server names, resources, tools, CLI output and delegation | Mostly canonical | MCP compatibility readers | OAuth client (R09) | [tools/PVAD.Mcp/](../tools/PVAD.Mcp/) | Remove legacy names/readers together with R08/R09. |
| R21 | Telemetry globals (`__{l}BrowserTelemetry`, `__{L}_CONFIG__`), metric and job names (`({l})_*`, `{l}-otel-collector`), runbook IDs (`{L}-OBS-*`) | Dual matching | Prometheus rules and tests, Grafana dashboards | Local metrics history only | [deploy/observability/](../deploy/observability/), designer runtime config | PVAD names only; regenerate rule tests. |
| R22 | Grafana dashboard UIDs (`/d/{l}-*`) and local container/service names (`{l}-prometheus`, `{l}-loki`, `{l}-tempo`, `{l}-database` and others) | Retained infrastructure bindings | Admin documentation links, alert annotations | Local Grafana and volumes | Compose files, [admin-documentation.json](../PVAD.WebApi/AdminDocumentation/admin-documentation.json) | Rename UIDs and services; D6 moves local volumes. Remote registries and environments stay under gates G01 and G03. |
| R23 | CLI and deployment shims (`tools/deployment/{l}-deployment.mjs`, `deploy/rename/*legacy*` compose files, `tools/job-cutover/`) | Compatibility entrypoints | Delegating shims | None | `tools/deployment/`, `deploy/rename/` | Delete shims and legacy compose overlays; keep canonical entrypoints. |
| R24 | Test and tooling contract assertions (`runtime-brand-contract`, `current-writer-acceptance`, `internal-naming-contract` and other tests asserting retained names) | Tests require old names | Rename guards | None | `tools/configuration/*.test.mjs`, .NET compatibility tests | Replace retention assertions with rejection assertions; delete pure compatibility tests. |
| R25 | Visible product text using `{L}` or `{E}` (for example the rename notice on the company site) | Website copy | None | None | [websites/PVAD.Company/src/main.js](../websites/PVAD.Company/src/main.js) | Decide O3; default: remove the rename notice. |

## Database Data Migration (Option B)

One new EF migration plus one maintenance command, applied to the local database
first and written so that it is idempotent and rerunnable.

- [ ] D0 Back up the local database (`pg_dump -Fc`) and the Data Protection key
  directory before any change; record file hashes, not contents.
- [ ] D1 Rewrite `identity.role_claims.ClaimType` from the `{l}_` prefix to
  `pvad_`, keeping values. Abort if a role would get a duplicate canonical claim.
- [ ] D2 Rewrite the MCP OAuth application client ID, display name and `{l}.`
  scope permissions to their PVAD values; verify against `PvadOAuthOptions`.
- [ ] D3 Rewrite legacy markers in `designer.designer_code_workspaces.JsonDraft`
  and version rows using the same mapping as the R17 code writers; unknown text stays unchanged.
- [ ] D4 Rewrite `{L}` diagnostic codes in operational job tables
  (`background_jobs.DiagnosticsJson`, `FailureReason`,
  `pdf_trust_operation_jobs.DiagnosticCodesJson`) using an exact code map
  generated from R01; leave `audit_events` unchanged.
- [ ] D5 Maintenance command: unprotect mail outbox payloads and revocation evidence
  with the old discriminator/purposes and re-protect with `PVAD`; dry-run, count,
  idempotency and rollback test on a synthetic copy first. Only then switch R11.
- [ ] D6 Rename the local database and role from `{l}` to `pvad`, rename the
  compose project, services and volumes, and update defaults (R13, R22). Record the
  one-time local commands in the database guide.
- [ ] D7 Rename synthetic local users (user name, normalized names, e-mail, display
  name) per O4. Leave `audit_events`, existing `legal_document_versions` and applied
  migration history unchanged.
- [ ] D9 Publish new Legal document versions with PVADE text through the existing
  Legal publication path (not by editing accepted rows), per O2.
- [ ] D8 Down migration restores the previous values from a stored mapping table
  or a reversible map; verify with restore from the D0 backup.

## Implementation Order

- [ ] I1 Generate exact rename maps (code → PVAD code, key → PVAD key) from the
  inventory for R01, R12, R14, R17; reject collisions with existing PVAD names.
- [ ] I2 Implement families in this order, each producer/consumer/test set
  together: R24 test inversion scaffold → R01/R02/R03 → R04/R05 → R12/R13 →
  R14 → R15/R16/R17/R18 → R19/R20 → D1–D4 with R06–R10 → D5 with R11 → R21/R22/D6 → R23 → R25.
- [ ] I3 Replace the retention ledger and allowlists: add a successor review ledger
  and a successor public/operator identifier register (v2) that mark families as
  retired and list only the never-rewritten evidence. Do not edit the v1 registers
  or Legal hash pins.
- [ ] I4 Add a guard that fails on any former-name occurrence in product source,
  tests, configuration and generated output outside the v2 evidence list.
- [ ] I5 Update current guides ([HTTP](../docs/pvad-http-compatibility.md),
  [identity](../docs/pvad-identity-compatibility.md),
  [license and jobs](../docs/pvad-license-and-job-compatibility.md)) to describe the
  hard cut; add dated corrections, do not rewrite historical results.
- [ ] I6 Add a Major release fragment, category `breaking`, `breaking: true`,
  via the release-author skill. VERSION stays `1.1.0` until release preparation is requested.

## Validation

- [ ] V1 Commands from the [final handoff](PVAD.Rename-Final-Handoff.md#required-validation),
  plus all tracked `tools/` and `deploy/` `.test.mjs` files.
- [ ] V2 .NET suites for Core, API, Worker, persistence, import/export, migrations
  and relay against an isolated PostgreSQL; record skips as skips.
- [ ] V3 Rejection tests: old routes, headers, cookies, scopes, client IDs, API key
  prefix and configuration keys are rejected or ignored without weakening authorization.
- [ ] V4 D1–D8 against a disposable copy of the local database, then the local
  database itself; Account → Designer handoff, MCP OAuth, mail send and PDF trust
  revocation work after the migration.
- [ ] V5 Fresh PDF, JSON, XML, DOCX, ODT, HTML, native archive, image export, OpenAPI
  and built bundles contain no former name.
- [ ] V6 Evidence files are byte-identical to the baseline hashes.
- [ ] V7 Inventory rerun: every remaining occurrence is on the v2 evidence list;
  report counts by family, never as a completion percentage.

## Owner Decisions On Open Questions (2026-11-09)

- [x] O1 No legacy importer: `.{l}p`/`.{l}s` reading, legacy format IDs, media
  types and the three retained schema resources are removed (R15, R16). The two
  original reference archives stay as evidence only, without a reader test.
- [x] O2 Legal documents change to PVADE through new published versions (new
  consent hash, normal re-acceptance flow). Accepted versions and their hash pins
  stay unchanged as evidence (D9).
- [x] O3 The company site rename notice is removed (R25).
- [x] O4 Synthetic local users with the former name are renamed (D7).

## Implementation Status (2026-11-09, uncommitted working tree)

- [x] D0 Local database backup in the ignored `artifacts/rename/hard-cut-backup/`.
- [x] Mechanical rename of tracked text (494 files) with evidence, audit history,
  v1 registers, applied migrations and binaries excluded; negative guards and
  evidence references restored afterwards.
- [x] R01–R11, R14–R20, R25 in product code: aliases, legacy readers, dual writers,
  alias projections (`canonicalCode`, `canonical*`, `legacyType`) and the
  `{E}` Data Protection discriminator removed; security-relevant duplicate-cookie
  and single-valued-claim checks kept as canonical-only validators.
- [x] Defects introduced by the mechanical pass found and fixed: self-deleting
  PKCE transaction key, canonical security header deletion, shell `unset` of the
  canonical variable, broken MCP scope canonicalization, duplicated webhook
  signature headers, self-recursive alert template, inverted name detectors.
- [x] Published evidence kept byte-identical; tests normalize historical
  diagnostic codes when comparing with published expected reports.
- [x] Transition-only tooling, job cutover harness, rename-host e2e and their
  tests removed; CI filters updated.
- [x] .NET build clean; .NET suites green except the pre-existing image-analysis
  failure and the blocked PDF/A baseline (V-PDFA); Designer 828/828; MCP 35/35,
  contracts and distribution smoke pass.
- [x] V-PDFA New fixture candidates validated with veraPDF (6 compliant, control
  non-compliant); text and embedded invoices differ only by the name. Recorded in
  `test-assets/e-invoicing/baselines/pvad-hard-cut-validation.json`; previous
  baselines retained. The pinned veraPDF 1.31.166 image could not be pulled
  anonymously, so the run used the public 1.30.4 image recorded in the file.
- [x] R12/R13/R21/R23: environment, compose, observability, telemetry, deployment
  and shell tooling are canonical-only; `deploy/rename/` overlays, the CLI shim and
  transition tooling were removed. The release verifier keeps reading published
  v1.0.0/v1.1.0 artifacts, and the deployment workflow keeps the existing protected
  GitHub environments (G03) unchanged.
- [x] R22 (resolved 2026-11-11 with option C; see Review Remediation) Grafana provider cutover and monitoring operator rehearsal tooling
  (`rename-grafana-provider`, `grafana-cutover-smoke`, operator browser rehearsal)
  still model an installed old-profile cutover; 10 tests fail on the changed
  dashboard identities. Decision needed: remove the tooling or re-pin a reviewed
  inventory. The legacy provider profile file is retained until then.
- [x] D1–D8 data migration `RetireFormerNameData` tested on a disposable copy
  (up, idempotent rerun, down, up) and applied locally; append-only audit and PDF
  trust operation evidence unchanged. Local users renamed (O4). D6: database and
  bootstrap role renamed to `pvad`, compose services, volumes and networks moved to `pvad-*`
  names; the previous `{l}-*` volumes are retained for rollback.
  WebApi verified against the renamed database (canonical routes, 404 on former
  routes, OAuth discovery with `pvad.*` scopes only, single `pvad-mcp-public` client).
- [ ] D9 New Legal versions with PVADE: candidates are valid; publication needs
  the owner's two-person approval in PVAD.Admin.
- [x] I4 `former-name-guard.mjs` with the exact v2 allowlist
  (`docs/pvad-former-name-allowlist.v2.json`) replaces the residual review in CI;
  I5 transition guides carry a dated hard-cut banner and current guides were
  corrected; I6 release fragment `pvad-former-name-retirement` (major, breaking);
  OpenAPI snapshot regenerated from the running contract.
- [ ] I3 successor v2 identifier registers were not created; the v1 registers stay
  as history and the v2 allowlist lists every retained occurrence.

## Review Remediation (2026-11-10)

Findings R1–R8 of the [review handoff](PVAD.Rename-Review-Handoff.md) were addressed.
Its remediation report has the per-finding details and test results.

- [x] R1 Offline Data Protection re-protection (`--reprotect-protected-data`) with
  startup refusal. Locally, 79 mail payloads were converted after a backup.
- [x] R1 local: after an owner-approved key merge, the remaining 9 mail payloads were
  converted. All 88 are readable canonically (2026-11-11).
- [x] R2 Follow-up migration `UseCanonicalRetentionPurgeSetting`, tested on Postgres.
- [x] R2 local: `UseCanonicalRetentionPurgeSetting` applied after an owner approval
  (2026-11-11).
- [x] R3 PAdES evidence reader restored, with tests and a real negative-manifest rebuild.
- [x] R4 Branding browser test is canonical. It passes on fresh builds.
- [x] R5 (= R22) Grafana: option C. The former provider, the rename candidate and the
  folder-rename cutover were removed. The current-profile inventory and the
  backup/restore drill remain; the real drill passed.
- [x] R6 Canonical Prometheus scenarios pass `promtool` offline.
- [x] R7 `RetireFormerNameData` corrected in place to the exact-code map
  (owner-approved). Local data was verified.
- [x] R8 Operator proxy smoke uses real invalid-value cases. It passes against nginx.
- [x] Dated reviewer labels and redaction-fixture markers restored and pinned.

## Out Of Scope

External cutover (registries, protected GitHub environments, remote monitoring,
DNS/TLS, publishers) remains under gates G01–G08 of the
[final acceptance](../docs/pvad-rename-final-acceptance.md). Nothing here performs live work.

## Follow-Up Remediation (2026-11-11)

- [x] F1 Startup refuses activation for unreadable mail or cache as well as for
  pending conversion. Tested with the real guard, including recovery.
- [x] F2/F3 `RetireFormerNameData` keeps no job content copy. `Down` is the exact
  inverse code map on current rows. `DropFormerNameJobBackup` removes any leftover
  table. Applied locally; the local database never had the table.
- [x] F4 = R5 option C implemented; tooling 349/349.
