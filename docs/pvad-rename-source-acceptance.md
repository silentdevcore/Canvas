# PVAD Remaining Code Source Acceptance

## Scope

This review closes the internal source acceptance criteria of the
[remaining-code checklist](../checklists/PVAD.Remaining-Code-Naming-Cleanup.md),
whose baseline is `e51a00aacfb2eb3148f119527ab4e2c1a0ca36cf`.
The preceding source batches are committed as `3c9d996c`, `3803580c` and
`6d37dd51`. The final review also corrects MCP compatibility-resource display
metadata without changing resource URIs or original schema payloads.

This is not a zero-occurrence guarantee for the repository, a human production
approval, an independent security audit or release authorization. The separate
[primary acceptance matrix](pvad-rename-final-acceptance.md) and
[release approvals](pvad-rename-release-approvals.md) retain their open gates.
Earlier execution counts and approvals remain point-in-time evidence.

## Reviewed Writer Boundaries

| Boundary | Current output and compatibility | Independent evidence |
| --- | --- | --- |
| Migration access and step-up | Canonical normalized route and all five internal policy names; permissions, tenant checks, recent authentication and CSRF remain required. | API migration-access, security and authorization compatibility tests; source mutation checks. |
| MCP runtime and resource metadata | Canonical runtime user/group/ownership and resource titles/descriptions. Former resource identifiers, CLI aliases and original schema files remain readable. | Real SDK client lists and reads both generations; exact payload comparison; runtime/toolchain tests; packaged stdio smoke. |
| Chart metadata | Canonical envelope key and recognition source. Former key/source are input aliases; conflicting dual payloads preserve original graphics. | Core codec/normalizer tests and Importer PDF roundtrips; direct writer guard. |
| Redacted image resources | Canonical marker and resource suffix. Rewriting removes the old marker; readers still verify old PDFs with existing pixel/mask/residue checks. | Importer redaction suites, repeated-redaction and forgery tests; direct writer guard. |
| Mail and webhook headers | Canonical headers with identical bounded compatibility aliases. Outbox correlation, signed bytes, HMAC algorithm and idempotency values are unchanged. | API mail/header contracts; relay integration tests for both receivers and a fixed signature vector; rejection of conflicting/partial pairs. |
| Parser and synthetic corpus output | Canonical newly generated IDs, metadata and subjects; explicit readers accept original reports without rewriting hashes or evidence. | Real bounded fuzz run and isolated generation recorded in Batch 4; read-only report, manifest, collision and evidence-selection tests. |
| Monitoring display | Canonical titles/tags, with exactly two reviewed JSON-pointer transitions. Datasource/dashboard UIDs, rule/runbook identities and folder placement remain operational bindings. | Dashboard-reference inventory, exact-value transition tests, unchanged original JSON audit. |
| Deployment defaults | Canonical image-family default with explicit historical-family selection. Protected environments, publisher, action pins, token permissions and lock groups are unchanged. | Actual workflow shell executed against a Docker stub; unknown-family rejection and idempotent old/new workflow preparation. |
| Browser and response aliases | Canonical new request/storage writers; explicit old readers and documented paired responses. One telemetry bridge serves both names without a second collector. | Shared storage/telemetry tests, Designer headers/configuration/tenant-state tests and API compatibility tests. |

The direct guards in
[current-writer-acceptance.test.mjs](../tools/configuration/current-writer-acceptance.test.mjs)
do not read the residual exception ledger. Reverting any of their eight writer
boundaries must fail even if somebody refreshes a file hash. These source checks
supplement the runtime tests; they do not replace execution or deployed checks.

## Retained Findings

| Category | Specific retention boundary | What is not permitted |
| --- | --- | --- |
| Stable diagnostics and translation keys | Exact diagnostic IDs and their six-language keys; existing bounded canonical display projections. | Inventing aliases for unknown codes or changing stored diagnostic evidence. |
| Input compatibility | Former API/resource URIs, schema IDs, project formats, claim/scope/cookie/configuration readers and migration response adapters. | Using an old identity as a new canonical default or dropping old input solely to reduce counts. |
| Transitional output pairs | Listed HTTP, SMTP and webhook aliases during the agreed compatibility major; explicit compatibility exports remain separate operations. | Treating paired output as a permanent product name, silently removing a generation, or signing different payloads. |
| Cryptographic and persisted contracts | Key IDs, protection purposes, accepted Legal versions, audit action/format IDs, applied migration SQL and retained job-binary harness contracts. | Re-signing evidence, rotating keys, backfilling original rows or relabeling a historical executable as a new build. |
| Operator bindings | Existing Grafana UIDs/provider/folder, protected GitHub environments, historical immutable images and installed physical storage. | Reprovisioning, bypassing permissions, deleting resources or selecting a different database as a cosmetic rename. |
| Historical and signed evidence | Original inventories, published release notes, approved baselines, signed PDFs, certificates and independent validator reports. | Editing bytes or changing a prior approval, test result or checklist state to improve rename counts. |
| Neutral and incidental substrings | Browser drawing APIs, third-party identifiers, preserved report-provider metadata keys and integrity-checked encoded data. | Renaming a vendor contract or replacing bytes inside a digest, signature or lockfile integrity field. |

The exact retained values live in existing public and restricted registers;
their versioned bytes are unchanged. Current-file reviews use specific path,
hash and reason records, not directory exceptions. The JSON audit remains
original evidence; separately recorded transitions accept only exact reviewed
former hashes and current values and cannot exempt protected evidence.

The tracked-only inventory includes source with embedded null bytes and counts
binary findings separately. Private IDE files, untracked notes, symlinks and
ignored outputs do not participate. Raw occurrence counts include deliberate
legacy fixtures and are not a progress metric.

## Operational Work Still Open

1. Authorize a concrete monitoring identity cutover, capture the actual state and
   backup, verify folder permissions and saved links, exercise real alert/mail
   delivery, and prove restore/rollback for that target.
2. Complete the separately governed production/external acceptance gates,
   including real host/keyring/storage access, protected integration, publishers,
   release authorization and deployed validation as applicable.

No new remote workflow, live database operation, resource migration, package
publication or deployment is performed by this source review. Prior local
rehearsals support preparation but do not authorize either open operation.

## Verification Record

Exact commands and results for this review are recorded in Batch 5 of the
[remaining-code checklist](../checklists/PVAD.Remaining-Code-Naming-Cleanup.md).
Skipped and unavailable external scenarios are not passing evidence. Product
VERSION and the published release manifest remain unchanged; a normal reviewed
integration and release-preparation workflow is still required.
