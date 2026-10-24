# PVAD Rename Test Report

> Naming history: **PXA has been renamed to PVAD (technical name). PVADE is the market name.** PDF uses PVADP and Spreadsheet uses PVADS. Historical PXA/Canvas product references, implementation notes and compatibility identifiers are retained; task statuses are unchanged.

## Operator And Hosting Examples (2026-10-23)

PDF/UA temporary paths, new secret/directory examples and capacity API-key
examples now use PVAD. Hosting and Company strategy list proposed `pvade.com`
origins separately from retained source fallbacks and mark targets unverified.
New examples prefer `VITE_PVAD_*`; old configuration aliases stay documented.

Corrected the AES-256 runner's local fallback to
`pvad-pdf-trust-external-tools:1`, matching its existing producer and README.
Fixture bytes/passwords, old report provenance, real mounts and host defaults
are unchanged. Backup output names and the restore confirmation remain the
actual retained operational contract and are explicitly explained, not falsely
documented as new values.

Documentation, tool-key, environment and site-link contracts: **24 passed,
0 failed**. These are local source/configuration checks. Docker image execution,
AES interoperability, backup/restore operations, DNS/TLS, browser deployment
and full application builds were not performed by this follow-up.

## Remaining Workflow Binding Review (2026-10-23)

All nine GitHub workflow files are covered by local YAML, dependency, action-pin,
working-directory, .NET source-path and Bash syntax checks. The five edited
workflow files receive explanatory comments only. Parsed before/after definitions
are equal: triggers, permissions, concurrency values, registry targets and
protected environment bindings are unchanged. PXA in the paired-binary workflow
identifies the real pinned historical worker, not active product branding.

Workflow and deployment contracts: **17 passed, 0 failed**. Checks use synthetic
local release records/temporary repositories and do not execute GitHub jobs,
publish containers, alter remote environments or deploy. The exact pending
publisher/environment/lock decisions are in
[the binding register](../docs/pvad-rename-release-approvals.md#retained-workflow-bindings).

## Ignore Rule Follow-up

The root `.gitignore` has no obsolete branding literals. Root Designer outputs,
WebApi runtime directories and Company legal snapshots use bounded neutral
patterns covering current and older checkout names. Environment-specific `.env.*`
files are ignored while example templates stay visible. The `.vscode/*` parent
rule now allows its four explicitly shared JSON files to be included.

`node --test tools/configuration/rename-ignore-contract.test.mjs`: **3 passed,
0 failed**, using Git's own `check-ignore --no-index` without creating sample files.
Checks cover current/legacy paths, private configuration and build output, and
non-ignored source, evidence, templates and shared editor configuration.
Index inspection found **228 already tracked ignored files**, all under
`designer-simples/`. None were removed from Git or deleted locally; changing their
tracked status is a separate cleanup decision. No unexpected newly visible files
were introduced by the corrected ignore exceptions at this checkpoint.

## CI Artifact Naming Follow-up

`.github/workflows/ci.yml` contains no PXA, PowerDox or Canvas matches. Its seven
PDF/A input paths now match current PVAD producer names. All ten freshly emitted
PDF/A filenames in the three producer classes use PVAD, including optional and
signed-conversion outputs. Original fixture content, diagnostics and archived
signed/hashed evidence are unchanged. New SBOM namespace IDs use `pvade.com`;
this is identifier naming, not confirmation of a deployed download endpoint.

`node --test tools/configuration/workflow-identity.test.mjs`: **6 passed, 0 failed**.
Checks parse YAML, validate job dependencies and working directories, inspect
referenced .NET project paths, syntax-check every shell block without executing
it, and enforce producer/consumer and namespace consistency. Trigger branches,
permissions, pinned actions, job behavior and unrelated release/deployment
identity boundaries remain unchanged. The full GitHub workflow, .NET tests,
PDF generation, veraPDF and SBOM generation were not run for this naming batch.

## Compose Defaults And Compatibility Follow-up

Root/API Compose files now have no case-insensitive PXA matches. Services, storage,
networks, defaults and secret mounts use PVAD. Old database settings, aliases and
verified external bindings are isolated in the two `deploy/rename/*legacy.yml`
overlays. WebApi and EF development defaults match the fresh local stack.

Executed:

```bash
node --test tools/configuration/container-contract.test.mjs \
  tools/configuration/compose-contract.test.mjs \
  tools/configuration/pvad-compose.test.mjs \
  tools/configuration/rename-cutover-candidate.test.mjs \
  tools/configuration/runtime-brand-contract.test.mjs
```

Result: **51 passed, 0 failed, 0 skipped**. Includes actual Compose 5.1.4 config
resolution for defaults, production/webhook overlays, explicit external legacy
bindings, missing-binding rejection, shared database credentials and alias
conflicts. Initial assertions expected absent IPAM, while Compose normalizes it
to an empty object; corrected assertions verify no inherited IPAM configuration
and the final rerun passes. Tests do not start or modify Docker resources.
No .NET application build, live database cutover, image startup, recovery or
deployment check is claimed. Prior deferred/historical records below remain
separate from this limited current verification.

## Rename-only Continuation: Tests Deferred

Compose service-name follow-up is source-only: base/API files, alerting/webhook
overlays, internal DNS targets and gateway routes use `pvad-*`; legacy service
aliases and persistent database/volume/network/secret bindings are retained.
Operational scripts select canonical services or resolve their container IDs.
Contract/API test sources were adjusted and new source-level contracts added.
None of those tests, Compose resolution, container restart or rollback checks has
been run for this revision. This is not deployment or data-continuity approval.

Documentation-only follow-up updates the public corpus HTML/README headings,
target-host examples and current source-of-truth paths. Proposed hosts are marked
as unverified; shared runtime host defaults were not changed. No PDF/XML/expected
report/manifest evidence was regenerated. Documentation builds and browser/link
tests were not run for these changes.

The latest naming-only follow-up changes remaining helper console/error messages
to PVAD and uses the already supported canonical veraPDF environment variables at
internal call sites. Signed/corpus content and cryptographic fixture identities
remain untouched. No test or helper-generation command was executed for this pass.

Source-name review is provisionally complete for the reviewed scope. The current
decision is to defer final-source validation, not to mark unrun checks as passed.
The 88/97 acceptance checkpoint remains historical; no fresh percentage is
calculated from file renames alone. Resume instructions are in the
[rename tracker](Rename-PXA-To-PVAD.md#resume-with-validation-not-more-cosmetic-replacement).

Follow-up source review also renamed the internal DSS validator from
`com.powerdoxautomation.tools` to `com.pvad.tools`, its Maven artifact/shaded main
class/JAR copy path and the two local PDF-validation image tags. Existing signed
corpus files and their recorded tool-image provenance were not changed. Java,
container and interop execution remain deferred; no registry was published.

At the owner's request, the latest batch changes names and references only:
five Grafana JSON filenames, three Prometheus rule filenames, the Prometheus
fixture filename and the Alertmanager mail-template filename now use `pvad-`.
CI, smoke scripts, test source paths and current migration guidance follow the
new paths. Mail definitions/configuration use `pvad.email.*`, with old-name
delegating aliases retained. UIDs, selectors, labels, volume identities, addresses,
keys and live services are unchanged.

No build, unit/integration/browser/Docker test, release-preparation operation or
deployment was run for this batch. Earlier pass counts and index snapshots below
are historical. Follow-up must run the Observability/API contracts, Prometheus
rules/fixtures, actual template rendering and compatibility-alias checks.

## 2026-10-22 Designer CSP Draft

Optional Designer headers are included explicitly by each Nginx location. The
baseline restricts base/form/embedding/object destinations; the resource policy
is report-only and has no remote reporting endpoint. Default unprofiled hosts
are unchanged. See [the draft status](../deploy/websites/DESIGNER-SECURITY.md).

The earlier browser run passed baseline/report-only probes and both editor imports.
An added native PDF-frame probe then timed out even with CSP bypassed and no
corresponding violation. Native PDF viewing/print remains unverified. The final
profile revision permits local/blob object sources and has a synthetic HTML
blob-frame, worker and PDF-download check, but its final browser run was denied
permission and was not executed. Do not promote earlier results to this revision.

Final local website/version/release contract selection: 37 passed. Configuration
selection: 113 passed before the final profile-only adjustment. The previous
twelve-step report below is historical and predates the CSP draft. No production
policy or strict resource enforcement is approved. Current index target: 2,918
files. Completion stays 88/97 (90.7%). No commit, push or VERSION change.

## 2026-10-22 Bundled Local Validation

All twelve steps in `node tools/configuration/rename-local-validation.mjs`
passed. See [scope and execution](../deploy/rename/LOCAL-VALIDATION.md).

- Configuration/recovery selection: 31 tests passed; API keyring/CORS selection: 24 passed,
  zero skipped. Two new cases restore both protected payload generations after
  copying a fresh synthetic keyring through a backup into a different directory
  and deleting the source. An empty ring cannot read either generation.
- Real isolated PostgreSQL dump/restore passed, including rejection of corrupt
  dumps, missing confirmation and nonempty targets. Service switch and rollback
  retained both synthetic records. The backup drill now removes its own anonymous
  container volumes; existing/unidentified volumes are not pruned.
- Canonical/legacy/conflicting Operator proxy cases passed. Company hosting
  routing, cache, real 404 and unavailable-API responses passed for both config
  generations. These checks are local HTTP/configuration evidence, not public TLS.
- Built Designer behind Nginx: SPA intake 200, unknown route 404, API outage 503,
  manifest/icons served, worker active, zero critical manifest errors and zero
  Chromium installability errors. No app was installed or associated in the OS.
- Intake and full import browser tests passed, with synthetic API responses and
  both PDF/Spreadsheet project kinds on desktop/mobile.
- Release/version/deployment contracts: 37 tests passed. Version consistency
  passed. Preview proposes major 2.0.0 from 99 fragments; VERSION, release manifest
  and Designer package version remain byte-identical. No artifact was published.
- Scoped results/logs are generated under `artifacts/rename/local-validation/`;
  the production-hosting diagnostic is under `native-project-production/`.

The public website template has **no Designer Content-Security-Policy**. The
Operator policy does not cover Designer. Define and validate the Designer policy
against previews, workers, images, fonts, code editing and print before rollout.
This is an internal remaining prerequisite, not a third-party blocker.
Full live multi-host staging, installed OS associations, real deployed key access,
publisher registration, public TLS/OAuth/mail and release authorization remain
unverified. Completion stays **88/97 (90.7%)**; twelve passed local checks do not
close these combined gates. Current staged-index target is 2,914 files.

## 2026-10-22 Full Browser Import Follow-up

- Identified and corrected missing Designer/CSRF headers on progress uploads,
  which use XMLHttpRequest rather than the global Fetch wrapper. The upload now
  acquires a validated fresh token before constructing the request. Denied CSRF
  authorization and cancellation prevent sending file bytes.
- Full browser coverage exposed Spreadsheet initialization cancelling the one-use
  launch during StrictMode cleanup. Consumption is now deferred until mount
  cleanup completes, with a regression using the actual import-flow hook.
- Chromium launch-to-editor tests pass for PDF and Spreadsheet at 1280px and
  390px. Each verifies one read, one multipart upload with both required headers,
  no residual transfer parameter, visible imported content and no script errors.
  API responses and source bytes are synthetic; this does not certify archive
  validation, real server authorization or installed OS file associations.
  Reports/screenshots: `artifacts/rename/native-project-import/` (not committed).
- Designer: 83 suites / 729 tests pass; type-check and production build pass.
  Version/release-author tests: 27 pass. Release preparation dry-run proposes
  2.0.0 (major) from 98 fragments and leaves VERSION, the published manifest and
  Designer package version byte-identical. No release has been prepared or issued.
- Current index: 2,910 files with no structural or mode problems. Historical
  checkpoint counts below remain snapshots, not current totals.

Overall completion remains 88/97 (90.7%); external approval gates remain open.

## 2026-10-22 Native Opening And Local Cutover

The PWA file handler and public metadata-only intake are implemented. File bytes
are read only after explicit confirmation and authenticated permission checks;
the protected editor consumes an expiring, user/organization-bound transfer once.
PDF launch imports do not retain a previous template persistence identity.
The network-only worker introduces no document cache. Generic install icons are
not approved corporate logo assets. See [the opening guide](../docs/pvad-native-project-opening.md).

- Designer: 83 suites / 722 tests passed, plus type-check and production build.
- Configuration: 113 tests passed, including manifest, locale, icon and cutover
  ownership/failure-cleanup contracts. Version/release-author tests: 27 passed.
- Real isolated PostgreSQL 17 service-name cutover: passed. The old writer was
  stopped before the new container used the same synthetic volume; rollback
  retained the original and newly written row. Labeled test resources removed.
- Browser smoke: Chromium 1280px and 390px, synthetic launch handles and mocked
  sessions, unread anonymous intake, explicit access refresh, discard, no script
  errors or horizontal overflow. Four screenshots and report generated under
  `artifacts/rename/native-project-browser/` (not committed).
- Real installed-app OS associations, real customer/application binary cutover,
  publisher ownership, deployed hosts and mail remain unverified here.
- Updated staged-index review: 2,908 files, zero mode changes or structural
  problems. The retained-name ledger now covers 969 exact text-file decisions;
  the current review queue contains 1,058 matching files including binary data.

These are scoped internal results, not substitutes for external gate evidence.
Completion stays **88/97 (90.7%)**. VERSION remains 1.1.0; no commit or push.

## 2026-10-22 Cutover Preparation Follow-up

Completion remains **88/97 (90.7%)**; the nine combined release/cutover tasks are
not closed by preparation alone. See [the preparation guide](../deploy/rename/README.md).

- API CORS now supports explicit configured origins without retaining localhost
  implicitly. Origin validation rejects wildcard, credential, path, query,
  fragment, whitespace, empty and oversized inputs without echoing values.
- CORS configuration is resolved when the options are constructed so the final
  host configuration is honored. The first integration run exposed early capture
  of the defaults; moving resolution into options construction fixed it. Final
  CORS/API-prefix tests: 25 passed, zero skipped or failed. API test compilation
  passed with existing NuGet vulnerability-feed, NPOI, nullable and analyzer
  warnings; no claim of license clearance or security-feed availability is made.
- Offline configuration aligns candidate frontend links, Designer Account URL,
  CORS, return destinations, mail URLs and proposed OAuth hosts. Publisher fields
  remain unselected; mail/OAuth remain disabled and webhook selection is absent.
- Canonical service preparation preserves explicit original-volume IDs, secret
  mounts, network/IPAM and both DNS aliases. Actual Compose configuration accepts
  the synthetic API candidate; no containers or volumes are created. Missing
  volume IDs, service/DNS collisions and unsupported bindings are rejected.
- Native descriptors are prepared and checked against Core for Linux/macOS/
  Windows; no actual OS installation, native opener or file-open test is claimed.
- The focused candidate selection passed 18/18, zero skipped. The documented
  `--require-ready` operation deliberately fails while external evidence is
  missing, even if syntactically valid publishers are supplied.
- Combined configuration/versioning/deployment/Legal/hosting selection: 172/172
  passed, zero skipped. Candidate CORS does not grant credentialed cross-origin
  access between public sites and administrative sites; all browser applications
  retain their same-origin API proxy architecture.
- Follow-up index validation: 2,884 files, zero mode changes and zero structural
  problems. The updated residual ledger covers 968 exact file decisions; cached
  whitespace validation passes. All changes are staged, not committed.

VERSION and published release entries remain unchanged. No commit, push,
external registration, DNS change, mail delivery or deployment was performed.

## 2026-10-22 Final Repository Review

Current completion: **88/97 (90.7%)**. The three remaining internal review gates
are complete; nine publication/external cutover gates remain open. Earlier
87.6% snapshots below describe the review chronology, not the final status.

### Corrections And Review Evidence

- Corrected active feature descriptions, browser-storage owner labels,
  unapproved processing-inventory descriptions, deployment/backup runbooks,
  current version-tool console text and temporary importer image prefixes.
  Admin capture/smoke producers now accept canonical environment settings with
  conflict-checked legacy readers. Existing synthetic license/key display inputs,
  backup safety phrases, paths, signing subjects and corpus IDs remain deliberate.
- Completed the case-insensitive remaining-name context review. The exact
  file/hash/decision ledger requires explicit explanatory notes and existing
  boundary guides; its validator is part of CI. Broad category assignment alone
  does not approve a new or changed file. Binary evidence remains separately
  covered by the existing artifact and screenshot review.
- Staged the whole rename and reviewed phase changes and the actual index.
  The structural index check has no missing targets, unexpected mode changes,
  untracked files or unstaged bytes. All 1,984 original deletions have targets;
  36 retained external-signature files remain byte-identical. New untracked-file
  preflight found no runtime/private-key paths or embedded PEM key material.
- The first full staged whitespace check exposed CRLF and empty TSV end-cell
  issues not visible in unstaged-only checks. Normalized 83 affected text files,
  represented empty baseline-inventory cells as `-`, and removed two extra EOF
  blank lines. The final cached whitespace check passes.

### Final Validation

- Full Solution build: zero errors, 105 warnings, 58 seconds. Existing warnings
  include unavailable NuGet vulnerability metadata, NPOI licensing, nullable
  annotations and XML documentation. These remain warnings, not a successful
  dependency vulnerability scan or license clearance.
- Configuration, versioning, deployment, Legal and website-hosting selection:
  154/154 passed. The first run after staging exposed a historical-checklist
  test incorrectly treating new current audit files as legacy checklists; the
  test now distinguishes retained filenames from the separately checked rename
  map. The final rerun passes.
- Importer generator-bridge roundtrips: 33/33 passed, zero skipped. The initial
  sandbox run could not bind VSTest's local socket; the authorized retry outside
  the sandbox passed. That initial run is not counted as passed testing.
- Designer: 80 suites / 711 tests passed; production build passed with existing
  Vite ESM/native-config and chunk-size warnings. Admin: 37/37 tests passed.
- Documentation: 58/58 tests and production build passed. Compared the generated
  OpenAPI JSON structurally with the checked snapshot, found one stale severity
  description and refreshed it from generated output; the contracts now match.
- Artifact audit: 96 repository binaries, 2,393 generated artifacts and zero
  unexpected old assembly names. Source-name hits are contextual findings, not
  automatically errors. No archived signed fixture was regenerated in this pass.
- Version synchronization and release-fragment validation passed. VERSION stays
  1.1.0; the published release manifest, Git tags and external deployments remain
  unchanged. A patch fragment records the naming/operational corrections; the
  overall namespace rename still requires the already planned major release.

No commit or push was performed. The full reviewed change remains staged.

## 2026-10-22 Residual Review And CI Follow-up

- Corrected sample PDF metadata/headings, generated footer branding, importer
  descriptions, current integration guides and unused migration translation keys.
  Historical checklists now explain retained implementation names without
  changing their task states.
- Configuration contracts now install their locked Designer dependencies before
  execution in CI. Final configuration/versioning/deployment selection: 129
  passed, zero failed, including the tightened explicit-review guard tests.
- The remaining-name scan now includes lowercase legacy domains. Its draft
  ledger is a pending review queue, not approval of every retained occurrence.
  Full semantic review and the final staged-diff gate remain open.
- Version synchronization and release-fragment validation pass. VERSION stays
  1.1.0; no commit, push, publication or deployment was performed.
- The updated WebApi build passed with zero errors and 12 warnings (nullable,
  obsolete SkiaSharp APIs and the existing NPOI license notice). All five website
  production builds passed. Documentation tests passed 58/58. Shared Vite ESM
  configuration warnings remain; these builds are not deployment validation.
- The updated PDF sample builds with zero errors and two warnings: unavailable
  NuGet vulnerability metadata and the existing NPOI license notice. A successful
  build does not resolve those network/licensing warnings.

Overall checklist completion remains **85/97 (87.6%)**, with 12 open tasks.
Passing tests are not used to mark the three remaining review gates complete.

## 2026-10-21 Consolidated Identity Review Follow-up

### Corrections And Preserved Boundaries

- Downloadable Demo JSON now agrees with canonical contract/model labels.
  Explicit synthetic customer content and original input names remain unchanged.
- Current invoice prose/captions, new imported PDF font families, new job input
  filenames, validation-evidence download names, generated HTML named-page rules
  and the internal revocation HTTP client name use PVAD. Versioned schema strings,
  retained job inputs and existing signed/legal material remain unchanged.
- Shared UI CSS classes/tokens and matching consumers use the current prefix.
  Storage-notice DOM hooks match their selectors; legacy acknowledgement-cookie
  readers remain. Real isolated Chromium captures found and verified a fix for
  the collapsed Designer sidebar toggle covering the mobile brand. Three browser
  tests passed, including 320-pixel geometry checks. Captures are ignored review
  artifacts, not a production authenticated-session or cryptographic proof.
- Nutrient generated password expressions prefer canonical environment keys,
  accept legacy values and reject conflicting dual values. The 26 tests compile
  generated source and inspect the expression; they do not execute every possible
  environment combination. Literal passwords are not included in generated output.
- E-invoice capability/finding DTOs now expose PvadEquivalent with obsolete legacy
  aliases. Four input-generation tests preserve diagnostics/source markers and
  verify matching serialized values. The parent migration suite passed 103/103.
- Recursive C# identity coverage now includes all Generation, Importing and
  Infrastructure files. Exact path/literal exceptions are documented; unexpected
  joined/lowercase names fail. This found the separate OCR process activity still
  using the old prefix; it now agrees with the canonical parent activity.
- Company/Account cosmetic return markers write pvad_signed_in, read old links,
  prefer explicit canonical input and leave external origins unchanged. They do
  not establish sessions or authorize access. Account tests passed 52/52.

### Validation Evidence

- Full `dotnet build PVAD.sln --no-restore -m:1 -p:BuildInParallel=false --nologo -v:q`
  passed with zero errors and 73 warnings (24 minutes). Existing warnings include
  unavailable NuGet vulnerability metadata, NPOI licensing, nullable annotations
  and unused fields. This is compile evidence, not a clean security scan or a
  claim that third-party licensing requirements are resolved.
- Configuration and release/version selection: 104/104 passed, no skips/failures.
- Shared and website JavaScript selection: 123/123 passed. The initial direct
  invocation omitted Node's TypeScript stripping flag and failed to load one
  test module; rerunning with the established flag passed. Company package suite:
  57/57 passed; Admin: 37/37 passed. These selections overlap; do not add their
  counts as unique tests.
- Designer and all five website production builds passed. Designer still reports
  existing Vite CommonJS/native-loader and large-chunk warnings. Company and
  Account are rebuilt again after the cosmetic return-signal follow-up.
- Previously completed in this identity batch: API 1,092 passed / 76 skipped /
  zero failed; Importer 646 passed; Designer 711 tests passed. The API run predates
  the additive mapping-property aliases, which are covered by the migration suite.
  Skipped opt-in/PostgreSQL tests are not counted as passed integration checks.
- Generator initially detected its stale maintained PDF/A fixture manifest.
  Reviewed regeneration changed current fixture hashes/sizes and producer labels,
  not archived signed files. Generator rerun: 137/137 passed. Actual pinned local
  veraPDF execution: seven fixtures, six compliant, one expected negative control,
  zero unexpected/not-evaluated results. This does not imply all invoice business
  rules or externally archived validator reports were re-certified.
- File pairs: 1,984 paired / 36 byte-identical retained external-signature names /
  zero unresolved. A temporary Git integration fixture verifies equivalent results
  before and after staging. It does not stage or commit the application repository.
- Separate artifact audit: 96 repository binaries, 2,393 generated artifacts,
  zero unexpected old assembly names; 10,468 old-name locations remain unapproved
  context-review entries. Existing diagnostics, neutral canvas terms and supplied
  fixture contents are included. A scan result is not a reviewed exception ledger.
- Version/fragment checks pass; VERSION remains 1.1.0. No published release
  manifest, application commit, push, external host or registry was changed.

Overall status remains **85/97 (87.6%)**. The three repository-wide residual and
final staged-review gates are not closed by bounded tests or file pairing. The
90% target is therefore not yet claimed. Publisher registrations remain explicitly
open; selected domain/support names are not proof of DNS/mailbox ownership.

## Retained Object/Annotation Mounts And Worker Paths (2026-10-21)

- Reviewed filesystem object storage, metadata service, annotation persistence,
  worker command resolution, licensing key-path resolution, API packaging and
  Compose persistent paths. No runtime behavior changed in this batch.
- Real temporary-file tests preserve synthetic object bytes and old object keys
  across changed application roots using an explicit mount, reject overwrite,
  and leave no temporary files after the rejected overwrite. Another test
  demonstrates why a new content root needs an explicit original storage path.
- Annotation restart test preserves identifiers, source name, timestamps, JSON
  content and the exact persisted file bytes/name. The initial raw-JSON comparison
  was corrected to structural equality because existing serialization indents
  JSON; on-disk bytes are still compared exactly before and after restart.
- YAML-parsed Compose checks cover the four persistent store paths and retained
  volume, plus canonical OCR/Code worker locations and project-copy targets.
  No image build, worker execution or live-volume modification occurred here.
- Focused storage/assets/annotation API tests passed 11/11, zero skips. Full
  configuration/version/shared-site selection passed 133/133, zero skips.
  Version/fragment validation and whitespace checks passed; prior build warnings
  remain. No Designer/UI rebuild was needed for this tests/guidance-only batch.
- Internal-only fragment: `pvad-storage-path-review`. Reviewed test changes are
  staged, no commit/push performed. Checklist stays 81/97 (83.5%), VERSION 1.1.0.

## Persistence, Keyring Restarts And Migration Definitions (2026-10-21)

- Compared 62 historical migration source/designer files with the pinned
  pre-rename baseline, allowing only C# identity substitutions. SQL, migration
  IDs and the retention purge session variable remain unchanged. The mutable
  model snapshot is not frozen by this contract.
- Expanded isolated file-keyring restart tests to mail and revocation evidence,
  including new-write recovery on another restart and unrelated-app rejection.
  Old and new ciphertext tamper checks pass; no production keys were accessed.
- Added explicit database/login preservation tests for legacy, new and customer
  database names. Connections stay closed; no actual database was contacted.
- Focused API protection/configuration selection passed 13/13; persistence model
  selection passed 11/11; migration and content-review contracts passed 4/4.
  No PostgreSQL or production cutover was performed. Existing build warnings
  remain unchanged.
- Corrected the review classifier to retain UTF-8 BOMs. Corrected counts are
  1,035 identity-only candidates and 784 additional text changes, not semantic
  sign-off of those files. BOM removal remains an additional content change.
- Internal-only fragment: `pvad-persistence-review-contracts`. VERSION remains
  1.1.0; overall checklist remains 81/97 (83.5%). No commit or push.

## Authentication Semantic Review (2026-10-21)

- Read the current scheme, claim, permission, cookie, API-key, security-header
  and OAuth scope/client code. Compared baseline cookie/API-key checks with
  renamed handlers. No runtime authentication change was needed in this batch.
- Expanded API-key integration cases from 6 to 36: canonical/legacy secrets,
  canonical/legacy headers and Bearer, each with active, revoked, expired,
  disabled service, suspended organization and closed organization state.
  Denied requests retain null LastUsedAt; valid requests retain the exact tenant.
- API-key/scheme/cookie/claims/scope selection passed 62/62, zero skips.
  OAuth scope migration selection excluding PostgreSQL passed 2/2, zero skips;
  it includes the actual OpenIddict InMemory store and credential/grant checks.
- Version/release-author tests passed 27/27; synchronization and fragment checks
  passed. Existing NuGet vulnerability-feed, NPOI license, obsolete-constructor
  and xUnit warnings remain. These test passes do not resolve those warnings.
- No production keyring, PostgreSQL or external OAuth-client test was performed.
  The limited reviewed test batch is staged separately from remaining rename
  implementation changes. Full repository semantic review remains open.
- Internal-only fragment: `pvad-authentication-review-matrix` (impact none).
  VERSION remains 1.1.0; checklist remains 81/97 (83.5%). No commit or push.

## Content Triage, Core Exceptions And Ignore Protection (2026-10-21)

- Added a repeatable HEAD-to-working-tree content comparison, including staged
  and untracked files. Snapshot: 929 identity-only candidates, 890 additional
  text changes, 815 byte-identical files, 22 changed binaries and 200 new files;
  zero missing targets. Report is a queue, not approval of these files.
- Fixed lost legacy ignore protections for App_Data, generated Legal snapshots
  and local Designer outputs, retaining canonical ignores too. Tests use Git's
  real ignore matching without creating any private-data files.
- Added explicit joined/lowercase Core exception coverage; diagnostic IDs and
  serialized readers remain stable. No application wire contract was changed.
- 130/130 configuration, version/release-author and shared-website tests passed
  using Node's required `--experimental-strip-types` flag for statusPages.ts.
  An initial invocation without that flag failed to load the TS file; it was a
  test-runner invocation issue, not an application change.
- A limited tool/ignore batch is staged and its full cached diff reviewed.
  Remaining application/docs changes are unstaged. No commit, full staged
  semantic sign-off, new backend build or deployment is claimed.
- Checklist stays 81/97 (83.5%); the three repository-wide gates are still open.
  Patch fragment: `pvad-legacy-local-data-ignore-protection`; VERSION unchanged.

## Browser Font Verification And Parity Inputs (2026-10-21)

- Added `node tools/configuration/verify-designer-fonts.mjs`: real Chromium
  checks at 1280x720 and 390x844. It uses the current renderer's font name and
  current CSS face declarations, intercepting all requests locally. No customer
  data, external font service, authentication or API is involved.
- All four local font assets were requested at both sizes; font readiness and
  computed styles passed, text bounds remained inside each viewport. Desktop
  and mobile screenshots were inspected. Evidence is stored under ignored
  `artifacts/rename/designer-fonts/` (two PNGs and report.json).
- This is an isolated font-registration/browser check, not an authenticated
  Designer test or a complete Canvas/Preview/backend PDF comparison.
- Updated npm parity commands and spawned backend artifact-test inputs to
  canonical PVAD environment keys. Existing alias readers are unchanged.
- 45 configuration/runtime/version/release-author tests passed; version and
  fragment checks passed. No full visual-parity pipeline or .NET suite was
  rerun for this batch. Patch fragment: `pvad-visual-parity-inputs`.
- Overall checklist remains 81/97 (83.5%); VERSION stays 1.1.0. No commit,
  deployment, tag or publication occurred.

## Designer Font And Notification Styles (2026-10-21)

- Found a runtime mismatch: PdfTextRenderer requests PVADE Helvetica while all
  four local Liberation Sans faces were registered as PXA Helvetica. Corrected
  registrations, retaining font bytes, weights, styles and layout algorithms.
- Renamed internal release, feature, notification and toast classes to `pvad-`
  in both React consumers and their stylesheet. Shared status-page styling and
  legacy storage/protocol identifiers were not changed.
- Added regression contracts checking all four face registrations, referenced
  asset existence, matching component/style class names and current prefixes.
- Validation: full Designer suite 80/80 suites, 711/711 tests; configuration and
  version/release-author selection 37/37 tests; Designer typecheck and production
  build passed. Existing Vite configuration and large-chunk warnings remain.
  Version synchronization and fragment validation passed. No browser visual
  comparison or backend suite was run for this frontend-only batch.
- Patch fragment: `pvad-designer-font-and-notification-styles`. VERSION remains
  1.1.0. Overall checklist remains 81/97 (83.5%); no commit or publication.

## Designer Help And MCP Discovery Review (2026-10-21)

- Updated current PDF starter content, help text and source comments to PVAD.
- Replaced outdated migration provider totals with catalog-driven guidance and
  separate PDF, Spreadsheet and Report migration kinds. Replaced a missing
  technical-documentation path with the existing documentation entry point.
- MCP protected-resource discovery now displays PVADE MCP. Historical resource
  aliases, OAuth scopes, compatibility headers and stable diagnostic IDs remain.
- Added a scoped source contract and an HTTP metadata assertion.
- Validation: 35 configuration/version/release-author tests and 26 MCP tests
  passed. Designer and MCP typechecks passed; Designer production build passed
  with existing Vite configuration and chunk-size warnings. Version and fragment
  checks and `git diff --check` passed. No full .NET suite or browser smoke test
  was rerun for this copy/metadata-only batch.
- Patch fragment: `pvad-designer-help-and-mcp-name`. VERSION stays 1.1.0.
  Overall checklist remains 81/97 (83.5%); no commit or publication performed.

Date: 2026-10-17. Branch: feature/rename-pxa-to-pvad.
Scope: project, symbol, frontend-package and visible branding migration;
not a full release sign-off.

## Baseline

Commit: fce423cf8c5f5e71eff6178f712fe4e5eb5af69a.
The initial working tree contained only the new rename planning/audit documents;
implementation remains uncommitted on feature/rename-pxa-to-pvad.
Runtime: Node v22.14.0; .NET SDK 10.0.105. VERSION remains 1.1.0.

| Check | Result |
|---|---|
| Project filenames | All 115 use PVAD; no PXA project filename remains |
| ProjectReference paths | All 272 resolve |
| Solution membership and GUIDs | Original values preserved in SLN and SLNX |
| Designer TypeScript | Passed |
| Designer Jest | 69 suites, 639 tests passed |
| Designer production build | Passed; existing Vite config/chunk warnings |
| Core tests | 126 passed |
| Focused Importer attachment/regeneration tests | 24 passed |
| Generator tests | 137 passed |
| API code workspace, project formats and version tests | 70 passed, including actual worker execution |
| DevExpress PDF code migration | 25 passed |
| Nutrient PDF code migration | 26 passed |
| Nutrient Viewer migration | 24 passed |
| Four tools outside the main solution | All four builds passed |
| Documentation contracts | 56 passed after namespace/path expectations were corrected |
| Documentation production build | Passed; existing Vite config warning |
| Version synchronization | Passed at unchanged 1.1.0 |
| Release fragment validation | Passed; namespace rename carries Major impact |
| Versioning and release-author tests | 26 passed |
| Full solution build | Latest symbol-stage build passed serially: 208 warnings, 0 errors |
| PDF migration aggregate | 99 passed |
| Spreadsheet migration aggregate | 19 passed |
| Report migration aggregate | 20 passed |
| Company tests | 42 passed |
| Account tests and typecheck | 46 passed; typecheck passed |
| Documentation, Admin and shared contracts | 101 passed combined |
| All five website production builds | Passed; Company rerun after final branding edits |
| MCP typecheck, tests, build and distribution smoke | Passed; 18 tests passed |
| npm lockfile dependency comparison | Transitive dependencies unchanged in all three lockfiles |
| Synthetic desktop/mobile branding smoke | 12 checks passed across six applications |
| Complete backend test matrix | Pending |
| Production rollout / database cutover / external services | Not executed |

The initial restore could not access NuGet vulnerability metadata and produced
NU1900 warnings. This is not evidence that the dependency security audit passed.
The four standalone tool builds used NuGetAudit=false to validate compilation
with cached dependencies; they do not constitute dependency-security validation.
An early no-build Nutrient test invocation ran before its renamed test assembly
had been compiled. It was repeated after compilation and passed as listed above.

The first full build exposed one NUL-containing C# test source that the binary
guard had skipped; its imports and namespace were corrected. A subsequent
parallel sandbox build ended after five minutes with exit 1 and no reported
compiler errors. The final serial build outside the sandbox passed in 21 seconds.
The intermediate failed runs are not counted as successful validation.

## Symbol And Frontend Stage

The symbol map records 534 owned identifiers. Technical C# names and frontend
helpers use PVAD/Pvad/pvad, while public platform surfaces consume the separate
PVADE brand definition. Compatibility strings are not globally replaced.

The complete Core suite initially failed two deterministic archive-fixture
checks: renaming MinimumPxaVersion had unintentionally changed its JSON key.
An explicit JsonPropertyName retains minimumPxaVersion until the dedicated
format migration. Both existing binary fixtures remain untouched; the rerun
passes all 126 tests. API code-workspace/project/version tests pass all 70.

Browser evidence: artifacts/rename-pvad/branding/results.json and the associated
desktop/mobile PNG files. These checks use synthetic API responses; they do not
establish real Account handoff, production authorization or deployment readiness.
The Designer mobile screenshot was visually inspected; its navigation drawer is
open in the captured state. New screenshots do not replace the public docs asset
inventory or its pending regeneration gate.

Reproducible commands, run from the repository root unless noted:

```bash
dotnet build PVAD.sln --no-restore --disable-build-servers -m:1 -v:q
dotnet test tests/PVAD.Core.Tests/PVAD.Core.Tests.csproj --no-restore --disable-build-servers -m:1 -v:q
dotnet test tests/PVAD.Generator.Tests/PVAD.Generator.Tests.csproj --no-build --no-restore
dotnet test tests/PVAD.Migration.Pdf.Tests/PVAD.Migration.Pdf.Tests.csproj --no-build --no-restore
dotnet test tests/PVAD.Migration.Spreadsheet.Tests/PVAD.Migration.Spreadsheet.Tests.csproj --no-build --no-restore
dotnet test tests/PVAD.Migration.Report.Tests/PVAD.Migration.Report.Tests.csproj --no-build --no-restore
npm --prefix pvad-designer run type-check
npm --prefix pvad-designer run build
node tools/versioning/pxa-version.mjs check
node tools/versioning/pxa-version.mjs fragments
node --test tools/versioning/pxa-version.test.mjs tools/versioning/pxa-release-author.test.mjs
git diff --check
```

Release tooling now uses pvad-version.mjs, pvad-build-consistency.mjs and
pvad-releases.json. Earlier commands above record their names at execution time.
Checklist progress: 25/97 completed tasks (25.8%). Tasks are equally weighted;
this is a tracker count, not an estimate of effort or production readiness.

## Release Tooling Stage

- 32 versioning, release-author and build-consistency tests passed.
- Designer typecheck and all 639 Jest tests passed.
- Admin tests: 33 passed; Documentation tests: 56 passed.
- Focused API version and native-project OpenAPI tests: 7 passed after rebuilding
  WebApi and test dependencies. Existing compiler and NU1900 warnings remain.
- Deployment release-control tests: 5 passed.
- All six frontend production builds passed; Designer was rebuilt after removing
  its duplicated legacy product identity.
- The published releases array was compared with HEAD and is exactly unchanged.
  Only the manifest's root technical product identity changed to PVAD.
- VERSION remains 1.1.0; fragment validation and version synchronization pass.
- Build-consistency validation passed against the actual output directories of
  all six applications, each identifying PVAD 1.1.0.
- Real container builds, release publication and production deployment were not
  executed. Container-label verification uses the existing mocked contract test.
- Repository skill instructions and legacy build environment variables remain
  pending their separate migration stages.

Current commands:

```bash
node --test tools/versioning/pvad-version.test.mjs tools/versioning/pvad-build-consistency.test.mjs tools/versioning/pvad-release-author.test.mjs
node tools/versioning/pvad-version.mjs check
node tools/versioning/pvad-version.mjs fragments
node tools/versioning/pvad-build-consistency.mjs verify pvad-designer/dist websites/PVAD.Company/dist websites/PVAD.Documentation/dist websites/PVAD.Demo/dist websites/PVAD.Admin/dist websites/PVAD.Account/dist
```

## Canonical API And Skill Stage

- Repository skill renamed to pvad-release-author, including commands, agent
  metadata and evaluation catalog. Versioning/release-author tests: 26 passed.
- Controllers and path-based security/metrics rules use /api/pvad/v1.
  PvadLegacyApiMiddleware maps the previous prefix before explicit routing;
  both prefixes therefore share endpoint metadata and all downstream policies.
- API/migration/OpenAPI tests: 41 passed, including 13 dedicated adapter/prefix
  tests. The actual OpenAPI document contains canonical routes, not duplicates.
- Dedicated tests cover segment boundaries, case handling, unchanged body/method/
  credentials/query, restored request paths, deprecation/successor headers,
  equal version/catalog responses and anonymous rejection on both prefixes.
- Additional authentication, redirect-security and registration regression run:
  16 passed, 13 skipped. The skipped PostgreSQL-dependent cases are not counted
  as verified database-backed login/registration flows.
- Designer: 639 tests and typecheck passed. Account: 46 tests; Admin: 33 tests;
  Documentation: 56 tests; MCP: 18 tests passed. MCP was rerun outside the
  sandbox after tsx could not create its required local IPC pipe inside it.
- All six frontend production builds passed after client endpoint updates.
- First-party client URLs, current guides, protected Admin guide and checked-in
  OpenAPI paths were updated. Historical checklists and compatibility fixtures
  are not globally rewritten.
- Legacy prefix removal is planned for 3.0.0 after compatibility throughout 2.x.
  No dated Sunset header is fabricated before an approved retirement date.
- No cookie, Data Protection, signed-license or native-file contract migration
  is claimed by the API prefix work. Real deployment/rollout remains unrun.

## First Configuration Compatibility Stage

- Shared JavaScript alias resolver tests: 4 passed. Build consistency: 6 passed.
- C# alias resolution, version and API-prefix tests: 19 passed, none skipped.
- MCP tests: 19 passed; typecheck, build and distribution smoke passed.
- All six frontend production builds passed with the shared build-info reader.
- MCP Docker source paths now use the renamed directories and include the shared
  configuration module. No Docker image build is claimed.
- The new alias test is included in CI. Existing API build warnings, including
  unavailable NuGet vulnerability metadata, remain separate from test success.
- Name-only candidate inventory contains 131 entries, including non-environment
  diagnostics that still need classification. No actual values are included.
- Configuration work is partial: broad tasks stay unchecked. Progress remains
  22/97 (22.7%); completed subtasks are not counted as completed parent tasks.

## .NET Sections And API Compose Stage

- .NET alias/host tests: 6 passed. They cover provider precedence, secret-safe
  conflicts and actual DI connection-string selection for both spellings.
- The host tests initially exposed an eager connection-string capture. Persistence
  registration now resolves the connection through DI after host overrides and
  alias normalization. A later test attempt correctly selected the synthetic host
  but triggered OAuth bootstrap; the test now explicitly disables OAuth so it
  verifies configuration without requiring an external database.
- Configuration/versioning/container contract tests: 43 passed, none skipped on
  this host. Actual Docker Compose CLI interpolation was tested with controlled
  synthetic inputs: canonical and legacy settings produced equal configurations.
- Compose checks confirm unchanged database name and existing volume keys. No
  Compose up/down command, data migration or volume operation was executed.
- WebApi/WebhookRelay definitions use canonical build metadata and legacy ARG
  fallbacks with conflict guards; first-party CI/release build arguments match.
  The incorrect PxaBuildMetadata publish property was corrected.
- MCP container host defaults are set by an entrypoint only when neither spelling
  is provided, avoiding conflicts between baked defaults and user overrides.
  Shell entrypoint behavior and Dockerfile contracts are tested; complete image
  build/run validation remains unrun.
- The Compose launcher requires literal dotenv values, supports explicit env
  files and rejects unresolved interpolation. Raw Compose bypasses alias checks;
  this limitation and unchanged project/volume naming are documented.
- Source credential-key names are inventoried without values. External deployment
  credentials, registry ownership and actual production environment audits remain
  outside this completed source-inventory task.
- Latest checklist count: 23/97 (23.7%). Broad infrastructure/configuration tasks
  remain open because telemetry, other tools and deployed identities still follow.

The final .NET/configuration/API/migration regression after the connection fix
passed all 45 tests with no skips.

## Telemetry Identity Stage

- WebApi instruments/spans use pvad.*; MCP metrics use pvad_mcp_*; default resource
  service/namespace is pvad-webapi / PVAD. Customer-configured names remain valid.
- .NET Observability suite: 54 passed, none skipped. MCP suite: 19 passed; MCP
  production bundle rebuilt successfully.
- Tooling/configuration/release suites: 45 passed. The generated dashboard-query
  contract uses only Node built-ins and is covered by CI contract tests.
- promtool v3.12.0 validated 37 alarm rules and all 40 managed Grafana PromQL
  expressions. It also passed all 20 alarm scenarios, including nine new PVAD
  variants while retaining the legacy input scenarios.
- The first isolated promtool run could not create test storage on a read-only
  root filesystem. The rerun added a private, bounded /tmp tmpfs and passed;
  no customer volumes or running services were used by these test containers.
- Five dashboards preserve their UIDs/datasource references and use dual-name
  metric selectors; managed Loki queries accept both old/new service names.
- Recovery-script alert IDs and the Operator gateway API path now match current
  contracts. Neutral collector privacy rules and physical health identifiers
  remain unchanged. Existing privacy/cardinality tests passed.
- CI now runs actual promtool parsing/scenarios in isolated network-disabled
  containers. Local evidence is under artifacts/rename-pvad/observability.
- No stored metric/log/trace data was rewritten. Live collector/Grafana rollout,
  external dashboards and deployed incident/recovery drills remain unrun.
- Latest checklist count: 24/97 (24.7%). Remaining service/provisioning identifiers,
  environment variables, signed webhook headers and runbook IDs retain explicit
  compatibility status; this is not a complete infrastructure cutover.

## Tool And CI Variable Stage

- Eight JavaScript tool modules now use the shared canonical-key resolver;
  only controlled compatibility tests retain direct process.env.PXA_* reads.
- Fourteen .NET test source files use a linked test-only reader. The tests-level
  Directory.Build.props explicitly imports the root version properties and does
  not add dependencies to production libraries.
- Twelve shell tools normalize known aliases at their entry points. The POSIX
  adapter uses no eval, rejects conflicts without values and removes consumed
  legacy names from the child environment. Literal multiline/quoted values are
  covered by regression tests.
- CI and fuzz workflows publish canonical keys; source contracts guard against
  regressions to direct legacy reads or legacy CI env declarations.
- Node tooling/configuration/versioning/Legal suites: 64 passed.
- Core suite: 132 passed, including six new test-reader cases.
- Persistence suite: 8 passed, 2 PostgreSQL-dependent cases skipped.
- Focused PDF/A, PDF/UA and trust-artifact importer tests: 22 passed.
- PDF/A, secondary PDF/A and PDF/UA wrapper test scripts passed with synthetic
  validator stubs; these are not new third-party conformance validations.
- MCP typecheck, 19 tests, production bundle and distribution smoke passed.
- All 50 tool .mjs modules passed Node syntax checks. The syntax pass caught and
  corrected an import placed above the AES-256 tool's shebang during migration.
- SLNX scope matches its original six projects exactly after prefix replacement.
  No reference PDFs, synthetic passwords or signed evidence were rewritten.
- Complete Solution build after the shared test-source change: passed serially
  outside the sandbox in 3m49.68s, 157 warnings, 0 errors. NuGet audit metadata
  remains unavailable; compilation success is not a dependency-security approval.
- Latest checklist count: 25/97 (25.8%). The complete backend test matrix, remaining
  browser/deployment identifiers and externally authorized rollout are still open.

### Browser preference migration: first increment

- PDF and Spreadsheet sidebar-collapse preferences now use `pvad-designer:*`.
- Existing same-origin `pxa-designer:*` preferences migrate on first read.
  Canonical values win, including empty strings. Legacy values are removed only
  after a successful canonical write; failed writes retain a recoverable original.
- The storage inventory lists canonical and transitional legacy keys.
- Four focused Jest tests passed: migration/idempotence, canonical precedence,
  failed-write recovery, and exact-key isolation. Designer TypeScript check passed.
- No unscoped workbook or code drafts were assigned to a user. Authentication
  cookies, PKCE transactions and document handoffs remain unchanged in this increment.
- The browser migration checkbox remains open. Favorites, locale preferences,
  events, other caches and scoped document-state handling still require review.
- Checklist progress remains 25/97 (25.8%); this is partial work on an open task.

### Browser preferences: favorites and language

- Favorite caches now use `pvad-designer-element-favorites-v1:<userId>` and read
  only the exact current user's legacy key. Server data remains authoritative.
- Account switching resets favorite state and pending-result ownership. A late
  save response cannot update the next account; queued, not-yet-started mutations
  for the previous account are skipped. Already dispatched requests are not revoked.
- Cache-write failures no longer report a successful server save as a failure.
- Designer and Account use `pvad_locale`, with same-origin migration from
  `pxa_locale`. Browser storage failure does not prevent Account rendering.
- No cross-origin transfer or implicit ownership assignment for document drafts.
- Validation: 6 focused Designer tests, 47 Account tests and 43 Company/shared/
  legal tests passed; Account typecheck/build and Designer typecheck/build passed.
  Existing Vite module-format and chunk-size warnings remain.
- Storage inventory includes both canonical and transitional keys. The broad
  browser migration task remains open; overall progress is still 25/97 (25.8%).

### Browser events and non-document caches

- Account and Admin API-event producers and listeners now use `pvad:*` together.
  These internal events are not a public integration contract; no dual dispatch
  is used, avoiding duplicate session-expired handling.
- Element-group and recent-content preferences use canonical keys, retaining
  legacy reads through the recoverable storage helper. Blocked storage no longer
  crashes the element-group persistence effect.
- Content drag payloads use `application/x-pvad-content-block`; drops continue
  accepting `application/x-pxa-content-block` from an older tab.
- Status-loop and recovery caches use canonical session keys. Recovery consumes
  both aliases to prevent an old action replaying after the canonical copy is read.
- Validation: 35 Admin tests, 47 Account tests and 45 Company/shared/legal tests
  passed. Shared tests include legacy-cache recovery and paired API-event identifiers.
  Designer/Account typechecks and Designer/Account/Admin builds passed.
- Migration document handoff, PKCE transactions, workbook/code drafts and the
  telemetry global bridge are deliberately unchanged and remain pending.
- Overall checklist progress remains 25/97 (25.8%); the storage/event task is open.

### Unassigned code recovery and cleanup

- Old code drafts migrate to `pvad-code-editor-draft-v2` as unassigned recovery
  data only. The existing explicit Import action remains required before editing
  a template; migration does not call an API or assign template ownership.
- Import removes only stored copies matching the selected draft, preserving
  different or concurrently changed recovery content.
- Sign-out/organization-switch cleanup includes both code aliases and the legacy
  Canvas workbook key. Individual removal failures do not abort remaining cleanup.
  This is best-effort cleanup, not a replacement for owner-scoped persistence.
- Validation: 6 focused storage/cleanup tests and 45 Company/shared/legal tests
  passed. Storage inventory includes both code-draft aliases.
- Existing global spreadsheet persistence and document handoffs are not claimed
  to be owner-scoped; these remain required work before closing the browser task.
- Progress remains 25/97 (25.8%). No commit or version bump in this increment.

### Workbook owner scoping

- New workbook persistence uses `pvad-spreadsheet:v1:<encoded-user>:<encoded-org>`.
  Automatic module-load hydration is disabled. Authenticated document routes wait
  until the selected scope is reset and rehydrated; a missing owner disables writes.
- Switching scope resets workbook content, selection, history and formula engine
  before reading the new scope. Stored drafts remain isolated and can be restored
  by returning to the same user and organization.
- Neither `pxa-spreadsheet` nor `canvas-spreadsheet` is automatically loaded or
  assigned to the current user. No legacy recovery UI is claimed in this increment.
- Malformed JSON fails closed and disables subsequent persistence, preserving the
  original instead of overwriting it with a blank document.
- Validation: 24 store/owner/cleanup tests, 15 route smoke tests and 45 Company/
  shared/legal inventory tests passed. Designer typecheck and build passed.
  Route tests emitted jsdom navigation and React test warnings; build retained
  the existing Vite module-format and chunk-size warnings.
- Browser storage inventory now states the scoped-draft retention semantics.
  This is browser-state isolation, not a replacement for server authorization.
- Progress remains 25/97 (25.8%). Document handoff validation, explicit legacy
  recovery UI and remaining browser identifiers are still open.

### Scoped handoffs and explicit workbook recovery

- Migration-to-Designer and PDF-viewer handoffs share a versioned envelope with
  user, active organization and five-minute validity. Auth bootstrap selects the
  owner before routes mount; mismatched, expired, malformed and unassigned raw
  transfers are not loaded. Legacy tabs must repeat their conversion/preview.
- A shared memory fallback supports quota failures. Changing owner discards it;
  sign-out cleanup clears the memory and canonical storage envelopes.
- Spreadsheet recovery is explicitly requested, download-only and localized in
  all six languages. PXA, Canvas and interim unscoped drafts export to native
  workbook JSON without deleting the original or mutating the current workbook.
- The browser inventory distinguishes active keys from recovery-only legacy keys;
  the validator retains its prohibition on active Canvas keys and audits both lists.
- Validation: 82 focused Designer tests plus 2 recovery-UI tests passed (the 3
  handoff and 5 recovery data tests were rerun with the UI tests). Company/shared/
  legal suite: 45 passed. Designer typecheck/build passed. No real-auth browser
  session or production deployment is claimed by these component/route tests.
- Added a Major release fragment for changed local-state compatibility. VERSION
  and published releases are unchanged. Browser task remains open: 25/97 (25.8%).
- Company production build and all 27 versioning/release-author tests passed;
  version synchronization and fragment validation passed. `git diff --check` clean.

### Browser key, event and cache migration closure

- PKCE writes `pvad.designer.auth-transaction` and supports an exact legacy
  transaction only when no canonical transaction exists. Both keys are consumed
  before exchange. Invalid JSON/state/verifiers, expiry, future timestamps and
  storage-consumption failures prevent API exchange. Callback return destinations
  are checked using browser URL normalization against the Designer origin.
- Telemetry now uses `__pvadBrowserTelemetry` with one legacy alias to the same
  collector. Repeated initialization does not install duplicate instrumentation.
- Export-format preference, Viewer navigation event, imported-font style owners,
  Monaco marker owner, Account rerender and Admin/API-online events use PVAD.
- Complete Designer Jest run: 77 suites / 672 tests passed. Account: 47 passed;
  Admin: 36 passed; Company/shared/legal: 46 passed. Shared tests overlap across
  commands. Designer and Account typechecks passed; all six frontend builds passed.
- Remaining old browser identifiers are classified in
  [PVAD.Browser-Identifier-Audit.md](PVAD.Browser-Identifier-Audit.md). CSS/DOM
  naming, runtime globals, API headers/diagnostics and cookie/protection cutover
  remain separate tasks. This closes only the storage/event/cache checkbox.
- Current checklist count: 26/97 (26.8%). VERSION and published releases unchanged;
  no commit, push, deployment or real-auth production signoff in this stage.

### Designer runtime configuration

- Replaced the sole direct `__PXA_CONFIG__` consumer with a typed runtime resolver
  using `__PVAD_CONFIG__` and a controlled legacy reader. Canonical/legacy URLs
  must normalize equally when both are present; explicit empty conflicts fail.
- Missing/blank settings preserve existing default hosts. HTTP(S) base paths are
  supported; credentials, query, fragment, malformed objects and unsafe URL schemes
  are rejected. Errors contain key names only, not configured values.
- Authentication bootstrap validates configuration before network operations.
  Invalid settings display a retryable configuration state; callback parameters
  are removed even when configuration validation prevents login.
- Final consumer audit found no remaining direct legacy-global access outside the
  compatibility resolver. Migration notes include canonical script placement and
  On-Premise base-path examples. Existing Major rename fragment was updated.
- Complete Designer suite: 78 suites / 690 tests passed. Typecheck, production
  build, version synchronization, fragment validation and `git diff --check` passed.
  Existing Vite module-format and chunk-size warnings remain.
- No host, reverse-proxy, CORS, cookie or server allowlist was altered. The broad
  runtime/deployment verification task remains open; progress stays 26/97 (26.8%).

### Batched HTTP, website and diagnostic compatibility work

- Canonical security headers now work end to end through server authentication,
  Designer access/feature gates, CSRF validation and updated browser clients.
  The normalizer runs after bounded request metrics and before authentication.
  Conflicting, blank, duplicate and combined values fail without exposing values.
- Four response-header families write PVAD plus identical legacy aliases. Existing
  cookies, API-key secrets/prefixes, schemes, Operator pseudonyms, SMTP identifiers,
  signed webhooks and retained evidence are not changed by this stage.
- Operator proxy authorization strips both header generations. New CI coverage
  executes real antiforgery and API-key compatibility tests independently of Docker.
- All six website URL keys accept PVAD/PXA aliases with conflict rejection in both
  development and production. Old defaults remain unchanged. Three narrowly scoped
  Vite filesystem allowances support importing the existing shared config resolver.
  An ephemeral Company server verified the module responds HTTP 200 and then closed.
- Designer documentation/parity compiler constants use PVAD. The neutral visual
  parity opt-in flag and production route exclusion behavior remain unchanged.
- An immutable baseline records 432 production CANMIG IDs. The existing config CI
  test glob now checks that all remain present; the historical-ID checklist task
  is complete. This does not claim all diagnostic families have been migrated.
- Validation so far: 695 Designer tests; 49 Company/shared/legal tests; 47 Account
  tests; 36 Admin tests; 17 configuration tests. The first consolidated API run
  passed 136 tests, including real antiforgery and EF in-memory API-key auth.
  PostgreSQL behavior is not inferred from in-memory tests. Counts overlap where
  shared suites are reused. All six frontend builds and both frontend typechecks
  passed. Final backend source/doc rebuild and verification are recorded below.
- WebApi compilation passed with existing NU1900 audit-metadata warnings, the NPOI
  licensing warning and nullable/analyzer warnings; dependency security clearance
  is not claimed. Current checklist progress: 27/97 (27.8%). No commit or version bump.
- Final rebuilt API/source/documentation run: 139 passed, 0 failed, 0 skipped.
  Version/release-author suite: 27 passed; synchronization and fragment checks pass.
  Local documentation links and `git diff --check` are valid. No service remains
  running from the ephemeral dev-module test. No production-host signoff is claimed.

## OpenAPI, Schema And MCP Batch - 2026-10-18

- Nine unversioned branded controller aliases now advertise PVAD. Old PXA routes
  delegate through middleware; unknown and near-prefix paths are not rewritten.
- The generated OpenAPI snapshot contains 322 paths and no `/api/pxa/` operation.
  Neutral routes and independent migration API deprecations remain supported.
- Canonical design/workbook schema IDs have an offline alias manifest. The legacy
  workbook file is retained and structural constraints match the canonical copy.
- MCP exposes five canonical PVAD resources plus five readable legacy aliases.
  Packaged resources include both workbook schemas and the alias manifest.
  Client examples, tool descriptions and AI/C# reference branding use PVAD/PVADE.
  OAuth scopes, organization claims and signed delegation contracts remain unchanged.
- Passed: 21 API tests, 21 MCP tests (including remote protocol/auth contracts),
  20 configuration tests, 5 focused Designer schema tests and 56 Documentation tests.
  MCP typecheck/build and distribution resource smoke, Designer typecheck/build,
  and Documentation production build passed. Existing AJV format warnings mean
  URI/UUID format validation is not claimed. Existing .NET/Vite warnings remain.
- Native archive schemas, remaining SDK contracts and deployed-host verification
  remain open. Current checklist progress: 28/97 (28.9%). No version bump or commit.
- Source stdio smoke and npm package dry-run also passed. Package contents are
  explicitly allowlisted to exclude stale legacy build outputs; old CLI aliases
  point to current PVAD binaries. Version/release-author tests: 27 passed;
  version/fragment validation and `git diff --check` passed.

## Native Project Format Batch - 2026-10-18

- New writers emit `.pvadp` / `.pvads`, canonical magic, format IDs and application
  MIME types together. New manifests write `minimumPvadVersion` only.
- Inspect and full reads accept intact legacy envelopes, including the old MIME
  entry and `minimumPxaVersion`. Conflicting aliases, cross-generation identities
  and MIME/manifest disagreements are rejected. Existing archive limits remain.
- Legacy open responses require compatibility review before applying the document.
  Six-language guidance explains that Save project downloads a new canonical copy.
  The original uploaded bytes are never rewritten. Audit event identifiers remain
  stable across all operations; the native envelope has an independent identity.
- Designer filters accept both generations in the matching product. New download
  filenames, MIME validation, C# helpers, canonical schemas and public guide agree.
  New synthetic fixtures were added alongside unchanged published legacy fixtures;
  immutable legacy SHA-256 baselines guard against accidental regeneration.
- The generated OpenAPI snapshot is refreshed, including native response MIME and
  canonical minimum-version fields. A generator transformer retains the chart enum
  and recognition parameter contracts previously present only in the old snapshot.
- Passed: 76 Core project-format tests, 29 API tests, 24 focused Designer tests,
  21 MCP tests, 22 configuration tests and 56 Documentation tests. Designer and MCP
  typechecks, all three affected builds and packaged MCP stdio smoke passed.
  Existing .NET package-audit/licensing and Vite/AJV warnings remain.
- Browser-authenticated visual review, OS file associations and the remaining
  end-to-end release gates are still open. Checklist progress: 34/97 (35.1%).
  No database data, production settings, published release version or commit changed.

## Authorization, Credentials And Protected Data - 2026-10-18

- Closed a branded-route authorization regression: product entitlements, Designer
  permissions and feature gates now share bounded path normalization for neutral,
  canonical PVAD and historical PXA routes. Tests include anonymous denial,
  disabled features, method-specific permission parity and near-prefix rejection.
- New API keys use `pvad_`; `pxa_` keys remain accepted without modifying stored
  hashes. Actual authentication tests cover both headers and Bearer, organization
  claims, revocation and the combined-scheme selector. The existing database
  prefix column supports the new length; no schema migration was required.
- Mail outbox and revocation-evidence protectors write canonical purposes and read
  old purposes. Tests cover tampering, purpose isolation, child protectors, a
  synthetic file-backed keyring restart and wrong-application rejection. Actual
  queue/processor delivery works for both generations without rewriting old rows.
- Application discriminator, key directory, cookie names, OAuth registrations,
  scopes, claims and signed delegation/license contracts remain unchanged. The
  inventory and mixed-version rollback restriction are documented in
  [identity compatibility](../docs/pvad-identity-compatibility.md).
- Final selected API run: 146 passed, 0 failed, 1 PostgreSQL lifecycle test skipped.
  EF InMemory mail/API-key tests are not PostgreSQL evidence. Relevant WebApi and
  test projects compiled; existing NU1900, licensing and analyzer warnings remain.
- Additional mail/privacy/service-account/trust-security run: 29 passed and 12
  PostgreSQL-gated tests skipped. Configuration/release tooling: 49 passed.
  Version synchronization, release-fragment validation and `git diff --check`
  passed. No commit, version bump or running service was created by this batch.
- CI now runs the new compatibility tests without requiring a production database.
  The native-format/old-new API validation gate is complete using the recorded
  format and endpoint suites. Checklist progress: 35/97 (36.1%). Identity cutover,
  retained job encryption and production validation remain open.

## Cookie, Session-Ticket And CSRF Batch - 2026-10-18

- Account/Admin, Designer and antiforgery cookies now write canonical PVAD names.
  Development aliases are separate from production aliases. Path is explicitly
  `/`, Domain remains unset, HttpOnly/SameSite Strict and production Secure remain.
- Old authentication schemes and Data Protection ticket purposes are retained.
  Actual application-option tests prove retained Account ticket decryption and
  Account-to-Designer ticket isolation. Legacy renewal occurs only after the
  existing persistent session/security-stamp/organization validation succeeds.
- The cookie manager reads legacy chunks, removes legacy cookies on canonical
  writes and removes both generations on signout. Empty/invalid canonical cookies
  cannot fall back to another ticket; duplicate managed names/chunks fail closed.
- A bounded CSRF-cookie adapter retains real antiforgery validation for both header
  names. Matching aliases are accepted; conflicts/duplicates clear both cookies
  with a stable, value-free diagnostic so a fresh token can be requested.
- Structured raw-cookie parsing is necessary because ASP.NET's cookie collection
  omits empty values. Regression tests cover this exact fallback failure.
- Browser storage inventory, HTTP/identity guides, affected persistent-auth test
  expectations and CI coverage are updated. No published legal text was rewritten.
- Final selected backend run: 142 passed, 0 failed, 12 PostgreSQL-gated tests skipped.
  Actual cryptographic TestServer ticket/CSRF tests and real application options do
  not replace the skipped persistent Identity/handoff database tests.
- Designer PKCE/handoff/tenant-state: 17 passed; storage catalog: 2 passed;
  configuration/release tooling: 49 passed. Storage inventory, version/fragment
  checks and `git diff --check` passed. Existing backend warnings remain.
- Checklist progress: 36/97 (37.1%). Claims/OAuth migration, production browser/
  reverse-proxy rollout and persistent-session verification remain open. No commit,
  version change, real credential replacement or server restart was performed.

## Claims, Scope Readers And MCP Delegation - 2026-10-18

- New principals emit five canonical `pvad_*` claim families. Historical aliases
  normalize before cookie validation, permission expansion and authorization-code
  exchange. Conflicting aliases or multiple singleton values reject the principal
  before partial mutation. Values, issuers, identity ownership and metadata survive.
- MCP JWT readers accept canonical or historical organization claims, reject empty/
  conflicting aliases and validate both `scope` and `scp` when present. Only known
  read-only scope aliases are expanded internally; access-context output is canonical.
- MCP now emits `PVAD-MCP` with `pvad-mcp-server` / `pvad-api`. The API verifies the
  canonical pair and two explicit historical pairs. This fixes the already-emitted
  transitional issuer that the previous backend rejected. No arbitrary identity
  mixtures, write scopes, duplicate JSON fields or unbounded lifetimes are accepted.
- Existing OAuth registrations, discovery scopes, authorizations, client IDs and
  grant issuance remain unchanged. Scope-reader compatibility is not a completed
  stored-grant migration. Coordinated rollout and rollback restrictions are recorded
  in the identity guide; no zero-downtime or replay-consumption guarantee is claimed.
- Final backend suite: 149 passed, 0 failed, 12 PostgreSQL-gated tests skipped.
  MCP: 24 passed including signed JWT and real HTTP scope-gate tests; TypeScript
  typecheck, bundle build and packaged stdio smoke passed. Designer auth/PKCE/tenant
  tests: 17 passed. Configuration/release tooling: 49 passed. Existing .NET and AJV
  warnings remain. Version/fragment checks and `git diff --check` pass.
- The source/in-process host-boundary/CSRF/PKCE/tenant preservation task is complete.
  Live proxy/browser and PostgreSQL session/grant verification remain separate gates.
  Checklist progress: 37/97 (38.1%). No commit, version bump or service restart.

## OAuth Scope Registration Compatibility

- The server registers both names for each known scope. New public, dynamic and
  service-account registrations grant only the same three read capabilities under
  both aliases; write and unknown scopes are not added.
- Token-endpoint read-only validation and template-entitlement checks now recognize
  canonical aliases as well as historical names. Tokens retain requested scopes.
- Existing client IDs, secrets, registrations, authorizations and signed tokens
  are not rewritten. Database-backed client/grant migration remains uncompleted.
- Focused scope catalog, claim and signed delegation tests: 25 passed, no failures
  or skips. `git diff --check` passed. Existing build/package warnings remain.
- Broad checklist progress remains 37/97 (38.1%); no additional broad acceptance
  gate is complete. No commit, service restart or version bump.

## Persisted OAuth Scope Alias Migration

- Added an opt-in startup maintenance step and a read-only planning method.
  Known aliases are added through OpenIddict stores/managers; existing IDs,
  secret hashes, redirects, consent, requirements and custom scopes are retained.
- Valid authorizations receive only aliases of their existing known scopes.
  Revoked grants and token payloads remain byte-for-byte unchanged. Repeated
  execution produces no further updates. Partial failures are resumable, not
  an all-or-nothing database transaction; run with other replicas/traffic stopped.
- New dynamic registrations issue `pvad-mcp-dcr-*` identifiers. Existing dynamic,
  public and service-account identifiers are deliberately retained.
- Synthetic migration tests passed with EF InMemory and PostgreSQL 17. Final HTTP
  coverage confirms dual-scope discovery/registration, canonical authorization,
  PKCE enforcement, canonical service-account token issuance, write-scope denial
  and existing revocation/tenant behavior. The HTTP fixtures apply the application's
  database migrations; the isolated store fixture uses EnsureCreated.
- Final security regression: 118 passed, 1 PostgreSQL-gated test skipped. The
  separate PostgreSQL-enabled run passed all 5 selected tests with no skips,
  including that migration test and both real HTTP workflows. Release tooling:
  27 passed. Version/fragment validation and `git diff --check` passed.
- CI includes the new migration test in its PostgreSQL OAuth step. Existing
  NU1900, NPOI license and compiler/analyzer warnings remain. No real database
  migration, backup/restore proof, deployment, commit, restart or version bump.
- Checklist remains 37/97 (38.1%): these complete OAuth substeps, not the broader
  serialized-contract and production data-preservation acceptance gates.

## License And Job Contract Preservation

- Inventoried signed schema 1/2 properties, entitlement fields, key/algorithm
  metadata, job types, payload dispatch, object keys and retained storage paths.
  See [license/job compatibility](../docs/pvad-license-and-job-compatibility.md).
- New license issuance uses PVAD numbers and downloads; historical/custom-number
  downloads retain old MIME/filename contracts. A shared download formatter keeps
  controller behavior aligned and new wrappers include the exact signed JSON text.
- Existing signing keys and key IDs remain unchanged. An incomplete pair is now
  rejected rather than regenerating and overwriting the surviving key. Real ECDSA
  tests cover restart, retained schema 1/2 verification, byte-level tampering and
  canonical/legacy download metadata without rewriting stored envelopes.
- New temporary object buffers and import-result filenames use PVAD. Existing
  paths, keys, job type strings and object metadata are not renamed. Tests now
  observe canonical job/storage/licensing metrics and activity names.
- Focused compatibility suite: 28 passed, 2 PostgreSQL-gated tests skipped. The
  separate PostgreSQL-enabled license/job/account run passed 25 tests with no
  skips, including tenant isolation, historical license download and actual job
  processing into external storage. CI includes that suite.
- Version/fragment checks and `git diff --check` pass; existing package/compiler
  warnings remain. VERSION stays 1.1.0. No production keys, customer license rows,
  existing blobs or real databases were changed; no commit or service restart.
- The signing-key preservation source task is complete: 38/97 (39.2%). The combined
  license-inventory/replacement task remains open: the validator recognizes
  `Replaced` but no atomic replacement endpoint exists. Production key recovery,
  retained-job encryption and cross-binary in-flight cutover remain unverified.

## Atomic Explicit License Replacement

- Added `POST /api/pvad/v1/admin/licenses/{licenseId}/replace`, protected by
  licenses.manage, System Administrator role and CSRF. Tenant/subscription context
  is resolved from the original license, not supplied by the requester.
- A serializable transaction persists the successor, old Replaced state, linked
  audit event and notification outbox together. Original signed bytes/key metadata
  remain unchanged; validity, instance limits and entitlements cannot increase.
- Schema 1 requires explicit product/deployment bindings; schema 2 cannot rebind.
  Unknown signed fields are rejected rather than silently discarding constraints.
  Replay/concurrent conflicts return 409. PostgreSQL tests caught and corrected
  an outbox uniqueness race; only that known constraint is mapped to conflict.
- Final local compatibility/OpenAPI/protected-documentation suite: 28 passed,
  5 PostgreSQL-gated checks skipped. The separate PostgreSQL-enabled license,
  replacement, account and job suite passed all 31 tests with no skips, including
  parallel replacement and forced post-write rollback. Release tooling: 27 passed.
- Regenerated the OpenAPI snapshot from the application. Updated CI, technical
  migration guide and protected Admin documentation. No Admin replacement button
  was added, and no customer license or production database was migrated.
- Version/fragment validation and `git diff --check` pass. Existing package and
  compiler/analyzer warnings remain. No commit, service restart or version bump.
- Checklist: 39/97 (40.2%). Real cross-binary job cutover, retained-object encryption,
  production backup/restore and offline artifact distribution remain open gates.

## Background Job Restart And Lease Ownership

- Final publication now conditionally updates only the current, unexpired lease
  in Processing state. Stale workers cannot overwrite a replacement claim or
  cancellation, and discard their own newly generated result on claim loss.
- Pending cancellation uses a tenant-filtered atomic state predicate and rereads
  actual status. An enforced database-command interception test proves a concurrent
  claim is not overwritten. This does not implement cancellation of running work.
- Expired claims are recovered only below the retry limit; exhausted Pending or
  expired Processing records become DeadLetter with terminal retention. Active
  claims remain intact, and worker shutdown is recoverable after lease expiry.
- New PostgreSQL fixtures reopen contexts, processors and filesystem storage using
  retained PXA payload text/filenames. They check result access, foreign-tenant
  denial, persisted object paths, shutdown recovery, cancellation and claim loss.
- Final local permission/job suite: 69 passed, 3 PostgreSQL-gated checks skipped.
  Separate PostgreSQL-enabled job/PDF-Trust suite: 7 passed with no skips, including
  those checks. Release tooling: 27 passed. CI includes the new fixtures.
- Added a customer-facing Patch fragment `job-lease-cutover-reliability`; the
  overall pending rename release remains Major. VERSION is still 1.1.0.
  Version/fragment validation and `git diff --check` pass. Existing package and
  compiler/analyzer warnings remain. No commit, deployment or service restart.
- Progress remains 39/97 (40.2%). These complete internal substeps, not a real
  paired-binary/process cutover. Old PXA workers must be stopped before PVAD starts;
  heartbeat support, crash-orphan cleanup, representative backup/restore and
  retained-object encryption remain distinct limitations/gates.

## Real Paired-Binary Job And Data Cutover

- Exported immutable PXA source commit
  `fce423cf8c5f5e71eff6178f712fe4e5eb5af69a` into a temporary directory without
  rewriting application sources. The harness is compiled separately against real
  PXA/PVAD API, persistence and worker assemblies, with explicit assembly checks.
- PXA applies its real PostgreSQL migrations and seeds synthetic identity, role,
  membership, subscription, entitlement, template, object and job records. The
  parent kills the actual paused PXA worker OS process and confirms exit before
  launching PVAD. Lease expiry is advanced explicitly only for this test.
- PVAD cancels the old Pending job and completes the interrupted one on attempt 2.
  Fresh PXA and PVAD reader processes both verify IDs, links, template revision/
  checksum, old object key/checksum, original input and new output, tenant denial
  and unchanged migration IDs/no pending migrations. Physical column/table metadata
  before and after cutover compares equal.
- Baseline and current harness builds passed. Initial sandbox/multi-node restore
  and macOS /tmp versus /private/tmp tracking issues were resolved by single-node,
  non-shared compilation and canonical source paths. Existing compiler, XML-doc,
  NPOI and package-audit warnings are not reported as clean builds.
- Final local job/permission suite: 69 passed, 4 gated tests skipped. Separate
  PostgreSQL/binary-enabled suite: all 8 passed with no skips. Configuration tests:
  22 passed. Release tooling: 27 passed. Node runner syntax and `git diff --check`
  pass. VERSION stays 1.1.0; pending release impact remains Major.
- Added a reproducible runner, guarded test executable and manual CI workflow.
  Locally verified the sequential export/build/test steps; the newly added GitHub
  workflow has not run remotely. Internal-only release fragment:
  `paired-job-binary-cutover` (impact none).
- Checklist: 42/97 (43.3%). The representative job cutover, neutral schema and
  retained identity/link tasks are complete. The fixture uses a controlled import
  adapter, not every PDF/OCR/code worker; it does not certify mixed-version rolling
  safety, whole-application rollback, production backup/restore or at-rest encryption.
- Confirmed still-open branded migration response/result fields `pxaCode`/`PxaCode`
  for the next contract stage. No customer data, running hosts, current branch,
  production database, published release or existing signing keys were changed.
  No commit, push or version bump.

## Migration Code Result Contract Stage

- Renamed the service result and Designer output state to `PvadCode`/`pvadCode`.
  Unified v1 responses retain neutral artifact content. Compatibility HTTP output
  adds `pvadCode` while retaining equal `pxaCode`; new migration-job output writes
  both Pascal-case fields without rewriting historical stored objects.
- Added canonical/legacy result serialization coverage and HTTP alias equality;
  provider service assertions now use the canonical property. A compatibility
  route test now checks the expected catalog successor among multiple Link values,
  rather than incorrectly requiring only one successor for a chained legacy route.
- All 47 targeted migration service/controller tests passed after correcting the
  Link assertion. Existing package-audit, NPOI and compiler/analyzer warnings remain.
- Designer type-check and all 27 release-tooling tests passed. Version and fragment
  validation passed; VERSION remains 1.1.0. The Major rename fragment already
  covers this contract transition. No commit, push or host restart.
- Broad checklist remains 42/97 (43.3%): this closes a migration-field substage,
  not the entire serialized-contract inventory or historical job-reader coverage.

## Diagnostic And Generated-Source Compatibility Stage

- Added explicit one-to-one aliases for all 18 shared PXAAPI diagnostics in
  `PvadApiProblems`. Existing `code` and type URLs remain stable; `canonicalCode`
  carries the corresponding PVADAPI ID. Tests exercise creation, completion and
  non-mapping of unknown/CANMIG/license families. No stored diagnostics are rewritten.
- C# Model and PDF generation now write canonical element markers. API source
  maps, semantic worker maps and low-level snapshot reconstruction share one reader
  accepting canonical and legacy markers, with the existing 200-character bound.
  Added marker tests and a two-generation actual-worker source-map test.
- Updated visible Account/Designer and migration descriptions without changing
  diagnostic IDs, permissions, provider IDs, customer document strings or hosts.
- Reviewed audit writes in account, identity, templates, licensing and jobs: neutral
  operation values are not product branding and remain unchanged. This targeted
  source review does not certify every persisted event or serialized type.
- Initial focused suite passed 67 tests. Final expanded run: all 115 tests passed,
  including both actual-worker marker generations, the full conversion suite and
  migration service/controller regressions. All 22 configuration/compatibility
  and 27 release-tooling tests passed; version/fragment validation
  and diff whitespace checks passed. Existing build/package warnings remain.
- Broad checklist remains 42/97 (43.3%). Diagnostic families beyond the common API
  family, the complete serialized-contract audit, and deployment verification remain
  open. API and worker must deploy together. No commit, push or VERSION increase.

## Document Headers, SMTP And PDF-Trust Diagnostic Stage

- PDF/A conversion, both redaction entry points and E-Invoice attachment now use
  one paired writer for 19 document-header suffixes. Legacy values remain equal to
  canonical PVAD values; no PDF, signed audit or retained evidence bytes change.
- The actual application CORS policy uses the shared header catalog. Tests verify
  exposure of both generations without exposing credentials, CSRF, cookies or
  internal Operator metadata. Controller and Viewer HTTP tests assert equal aliases.
- SMTP messages carry canonical and legacy outbox headers with the same UUID;
  tests inspect the composed message without contacting a mail server. Delivery
  IDs, queue rows and transport configuration remain unchanged.
- Viewer and E-Invoice browser readers prefer canonical headers, read legacy-only
  responses and reject conflicting aliases without disclosing evidence values.
- Added ten explicit PDF-Trust/PDF-A API diagnostic aliases. Common Problem
  Details completion retains original `code` and problem types. The disabled-trust
  middleware now also uses that completion and preserves problem+json delivery.
- Expanded PDF/controller/header/API suite passed 106 tests. Frontend: 42 tests,
  type-check and production build passed. Configuration/release tooling: 49 tests
  passed; version/fragment validation passed. Existing package/compiler and Vite
  module/chunk warnings remain. No SMTP delivery or deployed-host test is implied.
- Final focused run: all 61 tests passed, including the subsequently updated
  trust-availability middleware, paired headers, CORS and diagnostic aliases.
  Checklist remains 42/97 (43.3%); wider diagnostic/event contracts remain open.
  No commit, push, host restart or VERSION change.

## License, Access And Worker Diagnostic Stage

- Centralized reviewed diagnostic aliases in PVAD.Core.Contracts with an exact
  127-ID allowlist. Shared Problem Details, license responses, worker diagnostics,
  entitlement/feature decisions and handoff results derive aliases from legacy codes.
  Feature catalog entries use canonicalDecisionCode alongside decisionCode.
- Known families include common API, current PDF-Trust signing/timestamp checks,
  PDF/A conversion, workspace/worker, licenses, Designer access/assets/features
  and entitlement/subscription decisions. Unknown IDs and CANMIG are not renamed.
- Product and feature denial middleware now uses shared Problem Details completion.
  Existing decision codes, status decisions, permissions, signed license envelopes,
  persisted job payloads and historical diagnostic records remain unchanged.
- Legacy worker payloads gain a derived alias on read; a supplied alias cannot
  override the authoritative code. Alias changes track Code mutations rather than
  becoming stale stored state. Workspace revision-conflict output remains compatible.
- Added a source contract guarding reviewed families against missing or duplicate
  aliases. Generated OpenAPI from the actual application and synchronized the
  documentation snapshot; verified the five affected public schemas contain aliases.
- First run: 126 passed and 5 PostgreSQL-gated tests skipped, including the full
  converter suite. Updated-contract run: 81 passed and 5 gated tests skipped,
  including the OpenAPI export. Configuration/release tooling: all 50 passed.
- A separate PostgreSQL-enabled run checks retained-license validation and real
  rejected Designer exchanges. Production signing keys, customer DBs and running
  hosts are not modified. Version remains 1.1.0; no commit, push or restart.
- That run initially found a real 500 response where a locked-account exchange
  should return 403. MVC factory customization populated diagnostic extensions
  before ControllerBase.Problem added caller extensions, causing a duplicate-key
  ArgumentException. Completion now happens in the always-run MVC result filter;
  exception-handler and explicitly coded writer responses still use shared completion.
  Unknown final codes lose stale canonical aliases rather than inheriting false ones.
- Post-fix regression: 45 focused tests passed. PostgreSQL-enabled rerun: all
  18 tests passed with no skips, including retained-license validation and locked,
  inactive, expired-session and missing-entitlement handoff denials. API/auth/path
  suite: 29 passed, 3 PostgreSQL-gated tests skipped. Those remaining gated cases
  were not part of this targeted database run.
- Documentation production build, all 50 configuration/release tests, version and
  fragment validation, and diff whitespace checks passed. Existing package-audit,
  compiler/analyzer and Vite configuration warnings remain. The response fix has
  a separate Patch fragment: problem-details-extension-collision.
- Broad checklist remains 42/97 (43.3%). PXADESIGNER/PXAPROJECT, other provider
  report families and the wider serialized-contract audit remain open.

## Native Project And Template Conflict Diagnostic Stage

- Added 26 explicit aliases for PXADESIGNER001, PXAPROJECT001-016 and the nine
  named project validation/upgrade diagnostics. Shared catalog now has 153 IDs;
  source coverage includes native project producers and rejects unreviewed IDs.
- Template revision-conflict JSON adds canonicalCode while retaining legacy code,
  revision and updater data. Project rejection responses add the same alias.
- Project-open compatibility diagnostics use a separate API projection with an
  alias property. PvadProjectDiagnostic in archive manifests remains unchanged;
  no alias field is introduced into archived manifest or upgrade metadata.
- API tests assert aliases and the archive/API serialization boundary. Generated
  OpenAPI from the running application and synchronized the documentation snapshot;
  verified the response schema has canonicalCode and archive diagnostic schema does not.
- 72 API/contract tests, 76 native archive tests and all 50 configuration/release
  tests passed. Documentation production build and diff checks passed. Existing
  package-audit/compiler and Vite configuration warnings remain.
- PostgreSQL template workflow initially stopped after switching organizations:
  the existing fixture created an entitlement and membership but no role for the
  second organization, so the Designer gate correctly refused access. Corrected
  the fixture to assign the intended Organization Administrator role. Production
  authorization was not weakened to satisfy the test.
- Final PostgreSQL rerun passed (1/1, no skips), verifying concurrency aliases,
  versioning, organization switching and tenant isolation with that corrected fixture.
  Checklist remains
  42/97 (43.3%); other diagnostic/provider contracts remain open. No commit, push,
  server restart, published-release change or VERSION increase.

## Migration Diagnostics, Artifact Metadata And OpenAPI Stage

- Added the eleven PXAMIG API codes to the exact allowlist (164 reviewed IDs).
  Unified migration diagnostics expose canonicalCode only for reviewed IDs;
  the 432 historical CANMIG provider IDs remain unchanged and alias-free.
- Three known code/viewer artifact tuples have shared canonical ID, MIME and
  extension metadata in catalog and result responses. Legacy primary fields stay
  compatible. Mixed tuples and unknown/neutral artifacts receive no guessed alias.
- New Viewer package output adds canonicalKind without replacing legacy kind,
  duplicating artifacts or rewriting retained packages. This is metadata migration,
  not a new standalone upload/import feature. Existing Designer flows continue
  using neutral artifact content and design documents.
- Updated Nutrient-facing copy and a local converter variable; semantics, provider
  IDs, capabilities, permissions and diagnostic numbers remain unchanged.
- OpenAPI verification initially exposed missing migration response schemas because
  the endpoints returned untyped IActionResult/anonymous catalogs. Added typed
  catalog/provider projections and accurate successful-conversion response metadata,
  preserving the existing JSON fields. An actual-application OpenAPI test prevents
  those schemas from disappearing again. Regenerated and checked the docs snapshot.
- Final API/contract suite: 83 passed, no skips. Nutrient Viewer suite: 24 passed.
  Configuration/release tooling: all 50 passed. Documentation production build,
  version/fragment validation and whitespace checks passed. Existing package-audit,
  compiler/analyzer and Vite configuration warnings remain.
- A source inventory still finds additional E-Invoice, PDF-Trust engine, redaction,
  PDF/A, PDF/UA, parser/layout and chart families outside this completed substage.
  Checklist remains 42/97 (43.3%). No commit, push, restart or VERSION change.

## E-Invoice And Internal Trust Diagnostic Stage

- Added exact aliases for 28 E-Invoice API, 56 E-Invoice engine and 93 PDF-Trust
  engine IDs. Source coverage also exposed PXACODE140 (empty builder document),
  which now has an explicit alias. Reviewed catalog total: 342 IDs.
- Added one API-only JSON metadata modifier for PvadEInvoiceDiagnostic and
  PdfTrustDiagnostic. Known codes gain canonicalCode in nested API reports while
  the old code, severity, position, localization and OriginalFinding remain intact.
  Unknown/official third-party rule IDs receive no alias; incoming aliases cannot
  override the authoritative code.
- Ordinary engine models and serializer contracts are unchanged. Tests compare
  ordinary serialized JSON before/after API projection and assert that persisted
  E-Invoice DiagnosticsJson contains no alias properties. Official rule IDs, XPath,
  wording and rule expressions remain verbatim. No signed audit string, customer
  document, retained record or existing checksum is rewritten.
- Initial API run: 101 passed, one OpenAPI regression failed because MVC and HTTP
  JSON options were separate. Configured both through the same modifier, preserving
  their existing resolvers. Final API/evidence/contract run: all 102 passed, no skips.
  Regenerated the actual OpenAPI snapshot and checked both projected schemas.
- Configuration/release tooling: all 50 passed. Documentation production build and
  version/fragment checks passed. Existing package/compiler and Vite warnings remain.
- Final auth/redaction regression run: 38 passed, three PostgreSQL-gated tests
  skipped. Broad checklist remains 42/97 (43.3%); redaction,
  PDF/A/UA, parsing and layout diagnostic families remain outside this stage.
  No commit, push, service restart or VERSION change.
- Designer invoice and trust diagnostic displays prefer the supplied canonicalCode
  and retain the original code as a tooltip. Localization keys, navigation events,
  validation decisions and diagnostic data attributes retain their original IDs.
  Unknown IDs fall back unchanged. Four focused frontend suites passed (60 tests),
  including canonical display and unknown-code fallback; TypeScript checking and
  Designer production build passed. Existing Vite configuration/chunk warnings remain.

## Redaction, Archival, Accessibility And Parser Diagnostic Stage

- Added 110 reviewed IDs across redaction API/engine, PDF/A preflight, archival
  conversion, PDF/UA preflight, parser and cross-reference families. The catalog
  now contains 452 unique exact IDs; unallocated and external rule IDs are excluded.
- Extended the API-only JSON projection to six additional diagnostic models.
  Ordinary engine serialization remains unchanged. Parameterized tests compare
  every original JSON field, including capabilities, severities, offsets,
  localization and object/page positions, and require exactly one additive field.
- Extended the source contract to detect unmapped producers in the reviewed
  families. All 50 configuration/release tooling tests passed; version and fragment
  checks passed. The existing major rename fragment covers this continuation.
- Initial targeted API run: 65 passed, zero skipped. Regenerated actual OpenAPI,
  verified redaction/conversion schema aliases and synchronized the public snapshot.
- Final evidence/Problem Details run: 42 passed, zero skipped, including all twelve
  redaction API aliases and rejection of unknown redaction, parser and PDF/UA IDs.
- Viewer types accept optional aliases; redaction and archival findings prefer
  canonical display while retaining legacy IDs for navigation, data attributes and
  tooltips. Three frontend suites passed (25 tests); TypeScript, Designer production
  build and Documentation production build passed.
- Existing NuGet audit availability, NPOI, nullable/analyzer and Vite warnings remain.
  This stage changes identity projections only, not redaction or conformance claims.
  Layout/pipeline and attachment families remain open. Broad checklist: 42/97
  (43.3%). No commit, push, service restart, VERSION or published manifest change.

## Pipeline, Layout And Attachment Diagnostic Stage

- Added 32 exact aliases: 13 attachment, six layout and 13 pipeline identifiers.
  Catalog total: 484 unique IDs. Source coverage now includes Core primitives and
  these three diagnostic families. Unknown/unallocated IDs are not inferred.
- Attachment findings reuse the existing compliance-conversion API projection.
  ImportDiagnosticDto receives a derived API-only alias for reviewed codes; ordinary
  engine/design serialization remains unchanged. Real layout validation is checked
  for alias output, source preservation and unchanged A4 fallback normalization.
- Pipeline IDs are available to shared structured diagnostic consumers, without
  exposing a new pipeline endpoint or changing exception strings, job history,
  failure decisions or global exception classification.
- Added Problem Details checks for pipeline/layout/attachment aliases and unknown
  IDs. Extended generic evidence tests for layout and attachment objects, comparing
  every original JSON property and ordinary serialization before/after projection.
- Targeted API, attachment, evidence and actual OpenAPI export tests: 97 passed,
  zero skipped. Validated ImportDiagnosticDto and conversion-finding schema fields
  in the generated snapshot and synchronized docs/schema/openapi.json.
- All 50 configuration/release tooling tests passed. Three focused frontend suites
  passed (15 tests), including alias and localization-key preservation by the
  preflight reader. Documentation production build and version/fragment checks passed.
- Remaining numeric source families identified by inventory: chart diagnostics
  encoded in strings and Viewer-specific anonymous redaction errors. These need
  explicit output-contract decisions before closing the broad mapping task.
  Broad checklist remains 42/97 (43.3%); no commit, push, restart or VERSION change.

## Chart And Viewer Redaction Diagnostic Stage

- Added five exact aliases, bringing the catalog to 489 IDs. The source contract
  now scans chart producers and includes a full numeric production-ID inventory;
  all current PXA-letter-family/three-digit identifiers have explicit catalog entries.
- ChartRenderResult gains canonicalCode only through configured API serialization
  for the two exact recognized diagnostic prefixes. Original strings and ordinary
  serialization remain unchanged; unknown IDs, embedded mentions and successful
  results receive no alias. No new chart diagnostic endpoint is claimed.
- Viewer redaction refusals retain their anonymous response shapes, legacy codes,
  error text, preflight/verification payload and 409/503 statuses, adding an alias.
  Controller tests inject blocked preflight, failed verification and crypto failure
  and require an error response without PDF bytes.
- RedactionBlockedError accepts an optional canonicalCode after its existing
  constructor arguments; legacy callers and old servers remain compatible.
  Two frontend suites passed (17 tests), covering responses with and without aliases.
- Targeted API/controller/evidence/OpenAPI suite: 84 passed, zero skipped, including
  all three injected Viewer failure branches and five chart serialization cases.
  Synchronized the actual generated OpenAPI snapshot; anonymous Viewer responses
  are still anonymous contracts, not newly advertised typed endpoints.
- Configuration/release tooling passed all 51 tests. TypeScript checking, Designer
  production build, Documentation production build and version checks passed.
  Existing compiler/package and Vite warnings remain. The existing major rename
  fragment covers this continuation; VERSION and the published manifest are unchanged.
- Symbolic audit-export/authentication/usage result IDs remain a separate follow-up.
  Environment variables and metric-retention constants are not diagnostic codes.
  Broad checklist remains 42/97 (43.3%). No commit, push or service restart.

## Symbolic Authentication, Audit And Usage Diagnostic Closure

- Added five reviewed symbolic aliases. Catalog total: 494 unique exact IDs.
  Product-access middleware retains 401, legacy title/code, permission ordering
  and no endpoint execution for anonymous callers, with derived API aliases.
- Problem Details adds canonicalTitle only for reviewed identifier titles. The
  audit-export edition denial preserves its old title and generic PXAAPI002 code;
  the new title alias does not reclassify the code. Unknown final titles remove
  stale aliases. Existing type URLs are preserved.
- PvadUsageResult has an API-only alias. Three evidence cases preserve invalid,
  recorded and duplicate states, event IDs, reason/code and ordinary JSON. Usage
  service logic, quantities, persistence and idempotency/concurrency code were not
  modified. These tests validate projection, not a new database race experiment.
- The source inventory now classifies current numeric and symbolic production IDs.
  Six password-configuration/telemetry-retention identifiers are non-diagnostics;
  PXA0001 is an explicit retained compiler warning contract for existing pragma
  and NoWarn consumers. CANMIG/external rules and retained history stay unchanged.
- Targeted API, actual middleware, evidence and OpenAPI export tests: 86 passed,
  zero skipped. Configuration/release tooling: 51 passed. Documentation production
  build, version/fragment checks and diff whitespace checks passed. Existing package,
  compiler and Vite warnings remain. No frontend source changed in this stage.
- Closed the broad explicit diagnostic-mapping task after the inventory and output
  checks. Checklist now has 43/97 completed (44.3%), 54 remaining. The existing major
  rename fragment covers this stage; no VERSION/published manifest change, commit,
  push, deployment or service restart.

## Native Schema, OpenAPI Download And Implemented-Client Stage

- Added an offline native-reader schema for both generations/four coherent format
  identities. It references the unchanged legacy schema and current writer schema,
  rejecting mixed magic/format/media tuples without rewriting historical fixtures.
  Registered and packaged it through the existing schema manifest.
- Added positive/negative AJV manifest cases and distribution smoke checks resolving
  every bundled canonical/legacy schema ID. Optional AJV format keywords remain
  unasserted; structural schemas do not replace archive integrity/version validation.
- Native project OpenAPI success responses now declare binary schemas and
  Content-Disposition for PDF, PDF-with-workspace and Spreadsheet exports. Existing
  operation IDs, multipart input fields, auth and runtime archive bytes are unchanged.
  Two API/actual-snapshot export tests passed; the public snapshot was regenerated.
- MCP migration discovery uses the canonical branded compatibility route with its
  existing array response, not the differently shaped v1 catalog. Public language
  SDKs are still plans, not claimed as implemented. Added a dedicated schema/client
  inventory and corrected outdated delegation wording in the compatibility guide.
- MCP tests: 26 passed; typecheck/build and built-distribution stdio/offline-schema
  smoke passed. Tooling: 52 passed. Native Core archive tests: 76 passed, zero skips.
  Initial tsx/VSTest execution encountered sandbox IPC/socket restrictions; approved
  external reruns completed successfully. Package/format/Vite warnings remain.
- Frontend schema check initially found one failure caused solely by nested branding
  annotations. Comparisons now ignore string-valued schema annotations recursively,
  while retaining structural property definitions, types, enums and numeric bounds.
  Final frontend result: both suites passed (five tests); Designer TypeScript checking
  passed. Legacy fixture byte-hash contracts also passed in the tooling suite.
- Closed the OpenAPI/schema/client-resolution rename task for implemented clients.
  Checklist: 44/97 (45.4%), 53 remaining. Documentation production build and version/
  fragment checks passed. Existing major rename fragment links the new guide.
  No commit, push, VERSION change, external schema hosting or deployment.

## Generator, Worker And Executable Workspace Stage

- Updated all three generated C# headers to PVAD, the worker's temporary directory
  prefix and sandbox wording. Neutral protocol fields, limits, language IDs and
  diagnostic IDs remain unchanged. Renamed a private PDF-Tools rewriter flag and
  corrected the Spreadsheet facade's unknown-provider wording.
- Added real-worker checks for PVAD assembly names, request JSON fields and all
  three generated C# representations. Customer strings containing PXA/Canvas remain
  unchanged after execution. Existing twelve directed conversions, DTO coverage,
  source maps, legacy markers and sandbox regressions all passed: 50 tests, no skips.
- Source contracts now guard owned imports/declarations, worker packaging paths and
  generated header identities. Tooling total: 54 passed. No separate owned Roslyn
  analyzer/source-generator project was found; compiler PXA0001 remains retained.
  OCR worker paths already target PVAD binaries; its telemetry identity remains in
  the separate unfinished Observability work.
- Code Workspace PDF export uses pvad-code.pdf. Frontend regression asserts one
  download with that name; both focused suites passed (nine tests). Designer
  TypeScript and production build, Documentation build and version checks passed.
- PDF-Tools migration provider regression: 11 passed, zero skipped. This validates
  the private rewriter rename, not complete execution coverage for all providers.
- Spreadsheet migration facade/provider regression: 19 passed, zero skipped,
  including all eight registered provider factories. Total targeted .NET result:
  80 passed across the three suites.
- Initial sandboxed MSBuild failed on internal pipe permissions and stalled. The
  run ended after cancellation; the subsequent targeted kill found it already gone.
  Approved single-node external reruns passed. Existing NuGet/package/compiler and
  Vite warnings remain. No security-hardening certification is claimed.
- Added the code-generation compatibility guide and linked it from the existing
  major rename fragment. Closed worker/generator infrastructure mapping; full
  provider/snippet execution remains open. Checklist: 45/97 (46.4%), 52 remaining.
  No commit, push, VERSION/published manifest change or persistent service restart.

## Provider Code Execution And Workbook Example Closure

- Added a registry-driven execution matrix using Roslyn to retrieve existing basic
  fixtures, rather than duplicating provider/example lists. Coverage spans all 16
  PDF Code and eight Spreadsheet Code providers. Generated compilation references
  are limited to platform and PVAD libraries, excluding vendor SDK metadata.
- Fifteen PDF and eight Spreadsheet fixtures compile/execute and produce native
  output. XLSX is reopened through the PVAD importer; PDF checks cover header/EOF,
  not rendering fidelity. Pdftools deliberately retains its manual diagnostic and
  vendor source and is not executed. IronPDF remains a diagnosed scaffold.
- Initial matrix: 23 passed, one EPPlus case failed with CS1674 because a converted
  using declaration required IDisposable. Fixed declaration/block transformations
  while preserving scope and unrelated disposable resources. Typed declarations
  also target PvadWorkbook. Three focused lifetime regressions were added.
- Corrected stale Spreadsheet builder XML examples, private namespace-variable names
  and current diagnostic branding across nine owned source files. Vendor names,
  CANMIG IDs, historical reports and customer source are unchanged.
- Parsed and compiled the builder's actual XML documentation example, exported it
  and reopened its Sales worksheet. Final API execution suite: 25 passed, no skips
  (23 provider executions, one manual-contract check, one documentation example).
  EPPlus provider suite: seven passed, no skips. Total targeted .NET tests: 32 passed.
- Tooling: 54 passed. Documentation production build, version/fragment validation
  and diff checks passed. Existing package/compiler/Vite warnings remain; no new
  sandbox-security or full provider-feature-fidelity claim is made.
- Added patch fragment epplus-generated-workbook-lifetime, separate from the major
  rename fragment. Closed rename-level conversion/snippet/provider-code execution;
  complex source forms and provider-feature permutations remain functional coverage
  gates. Checklist: 46/97 (47.4%), 51 remaining. VERSION stays 1.1.0; no commit, push
  or service restart.

## Designer Locale And Export Metadata Stage

- Updated current translation values across all six Designer languages. UI copy
  uses PVADE, technical API/code references use PVAD, and native project actions
  use PVADP/PVADS. Translation keys, placeholders and historical diagnostic IDs
  remain unchanged. Existing partly renamed project labels were normalized too.
- Added locale branding regression coverage; locale and PDF Viewer suites passed
  all 33 tests. Designer type-check and production build passed. Existing Vite
  configuration and chunk-size warnings remain.
- New PDFs identify their Creator as PVAD PDF Renderer. A focused API test passed
  and confirms that customer title, author, subject and keywords containing legacy
  names are not rewritten. Local recovery download fallback now uses pvad-design.
- Updated shared Company template copy and the Designer workbook-schema reference.
- Version synchronization and diff checks passed. VERSION remains 1.1.0. No commit,
  push or service restart. Cross-application notification/navigation acceptance
  remains open, so overall completed checklist items remain 46/97 (47.4%).

## Account, Notifications And Admin Export Stage

- Updated 23 current Account authentication/verification/access strings across
  six languages. Legacy decision codes and storage migration inputs are retained.
- New Legal publication messages reference PVADE Account; persisted notification
  bodies and historical release titles are not rewritten. Designer tests confirm
  the current PVADE shell still displays and reopens historical content.
- Admin audit and Legal acceptance filenames now use pvad prefixes at both the
  server and frontend fallback. A mocked transport test verifies canonical API
  routes, credentials and CSRF headers while preserving customer-provided names.
- Corrected protected Admin handbook copy, operator handbook title, migration
  compiler guidance and release validation wording. Migration notes document
  current names versus retained content; the existing major rename fragment
  already covers these affected components and links to those notes.
- Passed: 49 Account tests, 37 Admin/shared tests, 37 Designer auth/product/toast/
  route tests, 22 Legal governance tests and 27 release-tooling tests (172 total).
  Account type-check and Account/Admin/Documentation production builds passed.
  Version synchronization, fragment validation and diff checks passed. Existing
  compiler, NuGet audit/NPOI and Vite warnings remain.
- A real authenticated cross-application browser smoke test was not performed.
  The aggregate UI checklist item therefore remains open: 46/97 (47.4%) complete.
  VERSION remains 1.1.0; no commit, push or service restart.

## Real Account And Designer Browser Closure

- Added `pvad-designer/e2e/rename-account-designer.spec.ts` using real local
  Account, Designer, API, PostgreSQL and test-mail delivery. No authentication or
  entitlement response is intercepted. Each run creates synthetic test accounts
  and personal workspaces; no existing customer account is changed.
- Both 1440x900 desktop and 390x844 mobile cases passed (2/2, 23.7 seconds).
  Verified anonymous Designer redirect, Account registration, verification email,
  email activation, login, PKCE return to Designer, callback parameter removal,
  authenticated `/auth/me` identity, session persistence after reload and template,
  code-migration and PDF Viewer navigation. No JavaScript page errors or horizontal
  overflow were observed on the inspected product routes.
- These are local development checks, not a production TLS/CDN/proxy certification
  or exhaustive feature test. Synthetic test rows remain available in the local
  test services; no database cleanup of unrelated records was performed.
- Closed the aggregate locale/navigation/notification/export naming item.
  Overall checklist: 47/97 (48.5%) complete, 50 remaining. VERSION remains 1.1.0;
  no commit or push.

## Brand Asset Inventory And Metadata Closure

- Inventoried six application entrypoints, 11 public Designer/Viewer captures and
  11 protected Admin captures in `docs/pvad-brand-asset-inventory.md`. No approved
  logo, favicon, social artwork or application web manifest is currently shipped.
  Data-contract manifests are explicitly excluded from branding renames.
- Removed the nonexistent Designer `/vite.svg` reference. Kept text branding
  instead of inventing replacement artwork; added Designer description/noindex
  metadata and the explicit PVADE Designer title. Account/Admin remain noindex.
- Corrected Company/Demo accessible home labels to the PVADE display identity.
  Added contract coverage for entrypoint metadata, scaffold-artwork references,
  accessible public brand links and technical package/lock identities.
- Nine shared brand/link/storage tests passed. All six frontend production builds
  passed; existing Vite configuration/chunk warnings remain. Screenshot bytes were
  not changed or visually approved. Protected Admin captures remain private.
- Marked inventory/text-fallback and title/accessibility naming items complete.
  Logo approval, current-UI screenshot recapture and deployment asset validation
  remain open. Overall: 49/97 (50.5%) complete, 48 remaining. No VERSION change,
  commit, push or server restart.

## Frontend URL And Proxy Validation Stage

- Account return destinations now reject embedded credentials, backslashes and
  raw controls/spaces rather than silently accepting browser URL normalization.
  Website/runtime URL readers reject ambiguous paths/hosts; encoded spaces and
  configured On-Premise base paths remain supported. Rejected values are not
  included in configuration error messages.
- Added a TypeScript-AST structural test for all six development API proxies,
  Designer application header and Account discovery/OAuth origin preservation.
  These source checks do not claim that every runtime proxy was exercised.
- Updated Nginx outage title to PVAD and added canonicalCode PVADAPI018 while
  preserving code PXAAPI018. The real Nginx container smoke passed canonical and
  legacy API outage paths, real 404, shell no-cache and immutable asset caching.
  Its initial readiness probe saw an empty response before the container became
  ready; all final assertions passed and the test container was removed.
- Passed 50 Account tests, 31 Designer runtime/auth tests, eight shared URL/hosting
  tests and 55 configuration/release-tooling tests (144 total). All six frontend
  production builds, Designer type-check and diff checks passed. Existing Vite
  warnings remain. Added patch security fragment account-url-validation; no
  exploit details or customer values are included in public release text.
- Documented scope and deployment gates in docs/pvad-frontend-host-validation.md.
  Real production TLS/CDN/forwarded-header/OAuth/cookie validation remains open;
  the local test intentionally uses an unavailable upstream and a Company build.
  Overall remains 49/97 (50.5%); VERSION is unchanged. No commit or push.

## Website Proxy Configuration Cutover

- Switched the Nginx template to PVAD_API_UPSTREAM/PVAD_SPA_ROUTE_PATTERN.
  Added executable 15-pvad-environment.envsh, sourced by the official Nginx
  entrypoint before envsubst. It resolves legacy PXA inputs, removes consumed
  aliases, rejects contradictory values without disclosure and validates required
  nonempty configuration and anchored route patterns.
- Updated route-pattern example names, hosting instructions and frontend host
  validation notes. Hook and template must be installed atomically; a template
  update without the hook is explicitly unsupported. Nginx still validates full
  upstream and regex syntax. No production deployment was changed.
- Smoke port accepts PVAD_STATUS_SMOKE_PORT and the old alias with conflict
  checks. The real container smoke now executes twice, with canonical-only and
  legacy-only inputs. Both passed SPA/404/cache and canonical/legacy API outage
  response checks; each temporary container was cleaned up. Readiness retry
  messages before Nginx became available were harmless.
- Added alias/missing/empty/conflict tests. All 62 hosting/configuration/release
  tooling tests, shell syntax checks, version checks and diff checks passed.
  The existing major rename fragment now links to the hosting compatibility note.
- Public-host TLS/CDN, complete route coverage and successful upstream OAuth/
  session behavior remain deployment gates. Overall remains 49/97 (50.5%) complete.
  VERSION is unchanged; no commit, push or application restart.

## OCR And Structured Logging Identity Stage

- New OCR activities use PVAD.ImageOcr and pvad.ocr.worker.execute. New temporary
  directories use pvad-image-ocr; existing directories are not rewritten/adopted.
  The API subscribes to canonical and explicit legacy source names without
  emitting duplicate spans. Custom listeners must be updated to accept both.
- Structured console logging defaults to pvad-json. The registered pxa-json alias
  shares the existing serializer/privacy implementation. Resource values supplied
  by operators and historical data remain unchanged; alias registration does not
  introduce another console provider or duplicate log records.
- Passed five isolated OCR tests (including W3C parent context, canonical names
  and cleanup), 55 API Observability tests (including both formatter privacy
  paths), and 63 hosting/configuration/release-tooling tests: 123 total.
  Documentation build, version synchronization and diff checks passed. Existing
  NuGet audit, NPOI, compiler and Vite warnings remain.
- Added docs/pvad-observability-identity.md and linked it from the rename release
  fragment. Classified retained health/datasource names, Grafana UIDs, Collector
  environment inputs and persisted telemetry separately from active branding.
  Deployed collector/query continuity and remaining external-container settings
  remain open; this does not close the broad configuration task.
- Overall remains 49/97 (50.5%) complete. VERSION unchanged; no commit, push or
  service restart.

## Sixty Percent Closure Block

- Added a reproducible current-name inventory generator and classified current
  Git-visible files without including source contents or secret values. The old
  inventory remains the baseline. Buckets do not automatically approve every
  match, and ignored build outputs remain a separate artifact-review gate.
- Added baseline guards for byte-identical Legal files, third-party .NET package
  IDs/vendor imports and neutral PdfDocument/SpreadsheetDto names. Documented
  historical/compatibility exceptions and a coordinated migration/rollback matrix.
  Rollback includes paired artifacts, keyrings, leases, irreversible writes and
  external owner authorization; this is a runbook, not production restore evidence.
- Designer: all 80 Jest suites/711 tests passed, type-check and production build
  passed. Fresh real desktop/mobile Account registration, email verification,
  PKCE login, callback cleanup, reload and product navigation passed 2/2 (26.2s).
- MCP: typecheck/build and all 26 tests passed, including the authenticated local
  Streamable HTTP SDK exchange. Packaged Stdio smoke passed resource aliases,
  offline schema resolution and tool contracts. AJV optional URI/date/UUID format
  assertions remain disabled and reported as warnings, not silently certified.
- Conversion/worker/generated-provider API filter: 76 tests passed (1m38s).
  Includes directed representation roundtrips and compiled provider fixtures;
  manual/scaffold and arbitrary-source fidelity limits remain unchanged.
- Legal governance: 22 tests passed. The first combined run skipped the opt-in
  audit test; reran it with PVAD_RUN_POSTGRES_TESTS=1, then its real isolated
  PostgreSQL update/delete protection test passed 1/1. No skipped test is counted
  as completed evidence. This does not certify production customer data restore.
- Configuration/release/build-consistency tests: 64 passed. Six frontend builds
  and their actual PVAD 1.1.0 build-info files validated. Distinguish these from
  OCI image inspection: the overall container-metadata gate remains open.
- Final successful automated tests in this block: 902 (711 + 26 + 76 + 22 + 1 +
  64 + 2 browser tests), plus packaged Stdio smoke and six builds. Existing
  package/compiler/Vite/AJV warnings remain. Temporary application test servers
  are stopped after validation; synthetic registration records remain local.
- Closed ten points: inventory, third-party/neutral preservation, immutable
  evidence policy, historical naming rules, rollback documentation, coordinated
  migration notes, consolidated test report, Designer validation, MCP validation
  and representation/generated-code validation. The original candidate list's
  full container-metadata check is not closed just to meet the percentage.
- Overall: 59/97 (60.8%) complete, 38 remaining. VERSION remains 1.1.0; no commit,
  push, tag, production deployment or history rewrite.

## Seventy Percent Validation Block - Partial Closure

- Four additional checklist items are complete: serialized contract inventory,
  representative database backup/restore, native archive error/cancellation matrix
  and explicit physical-infrastructure mapping. Overall is 63/97 (64.9%), with
  34 open. The 70% target has not been reached and is not claimed.
- Generated a 2,888-entry schema/property/operation/claim index and documented
  OAuth/event/serialized-type boundaries. No runtime credentials or customer data
  are present. Mapping preserves actual DB/user/service/network/volume identities.
- Real PostgreSQL pg_dump/pg_restore uses the actual migrated EF schema and
  synthetic user, organization, subscription, template, Legal acceptance, license
  and audit records. It preserves template JSON/revision, hashes and ECDSA-signed
  synthetic license bytes; restored audit triggers reject mutation. Final focused
  Admin-contract/restore rerun passed 2/2 with no skips. This is not production
  backup certification or product-license-envelope interoperability testing.
- Native archive suite passed 76/76. Five website suites passed 197 tests
  (Company 52, Documentation 56, Admin 37, Account 50, Demo 2). Demo now has a
  regression suite; it exposed and fixed a real sheet cells-to-table mapping bug.
  Six frontend production builds passed. All-host/link/runtime coverage remains
  broader than these tests and its aggregate checklist item stays open.
- Root Compose inputs now use PVAD interpolation. Collector sampling/memory
  settings are forwarded canonically; real Compose resolves canonical and legacy
  launcher inputs identically. The Collector image validated the actual config.
  Latest configuration/hosting suite: 40 passed (before release-tooling tests).
- Built the arm64 API image twice, checked both worker DLLs, non-root UID and
  actual OCI version 1.1.0. Runtime testing found and corrected an unwritable
  PDF Trust cache path in Docker/Compose. The rebuilt image starts with read-only
  root, temporary scratch and a new anonymous data volume: liveness 200, absent-
  database readiness 503, built-in healthcheck succeeds. No existing volumes were
  replaced. Positive database-backed readiness, other images/platforms and
  release-grade revision/build-time labels remain open; default labels are unknown.
- Initial full .NET attempt failed and was interrupted during the long Export
  phase. Importer: 644 passed/1 failed; Infrastructure.Pdf: 51 passed/2 failed;
  API: 1,045 passed/1 failed/76 skipped. Do not sum these into a completed green
  solution run. Fixed the stale Admin count and erroneous no-ID encryption
  assertion; each focused correction passed. Signed-corpus and golden mismatches
  were not blindly rebaselined. A bounded Export retry passed 219 tests before
  aborting its host; two active fidelity tests are recorded, not proven culprits.
- Findings, artifact locations and package-security warnings are recorded in
  docs/pvad-rename-validation-blockers.md. Online restore reported NU1902/NU1903
  for Pkcs/Xml dependencies; no dependency upgrade was hidden inside the rename.
- Added patch fragments api-container-trust-cache and demo-spreadsheet-preview.
  Diff/version checks pass; VERSION remains 1.1.0. No commit, push or deployment.

First-stage migration notes:
[pvad-rename-migration-notes.md](../docs/pvad-rename-migration-notes.md).

Remaining work and acceptance gates:
[Rename-PXA-To-PVAD.md](Rename-PXA-To-PVAD.md).

## Validation Follow-up: Deterministic Signature Corpus

- All 645 Importer tests passed, with zero failures or skips.
- The external signing fixture remains byte-identical, including its expected
  report. Its certificate is evaluated at a fixed point inside its validity
  window for historical corpus comparison only. Before/after-validity tests
  retain invalid certificate results and valid cryptographic integrity.
- The injected clock is internal and absent from serialized policies. Production
  still validates against system time unless a trusted timestamp applies.
- The isolated Word/PDF sample-pack test passed in 55 seconds with external
  conversion and image comparison enabled.
- The isolated immutable real-world PDF/DOCX/XLSX corpus passed in 109 seconds.
  Both previously active-at-abort tests complete independently; the original
  shared 120-second limit is not evidence of a deadlock.
- Another parallel run aborted with both heavy tests active. They now share a
  nonparallel integration collection, without skipping fixtures or weakening
  checks. The rebuilt complete Export suite passed 221/221 tests, zero skips,
  in 3 minutes 39 seconds. This is test resource isolation, not a claimed
  production rendering optimization.
- Stripping only the PDF trailer identity did not reproduce the old golden
  hash. The original expected hash remains enforced; no unreviewed rebaseline
  is accepted. The full solution gate therefore remains open.
- Progress remains 63/97 (64.9%); partial test repairs do not close the complete
  solution, host, container or release-metadata acceptance gates.

## Golden PDF Baseline Verification

- Extracted unmodified PXA.Pdf source from HEAD
  fce423cf8c5f5e71eff6178f712fe4e5eb5af69a into an ignored local probe directory.
- Compiled one probe against both the original PXA.Pdf and current PVAD.Pdf and
  generated the same text/shape/link snapshot with compression disabled.
- Both outputs are byte-identical, SHA-256
  `256569dcef40dd183fdc288ecdb6b4fc9507aaa023df23ecd2e4f8986d37b5b6`.
  This proves the old expected hash was stale before the rename, not a rename
  regression. No visual tolerance or content omission was introduced.
- Updated the expected hash with this evidence; added independent page count,
  physical A4 geometry and extracted-content assertions.
- Infrastructure.Pdf suite: 53/53 passed, zero skips.
- Versioning/build-consistency/release-author tooling: 33/33 tests passed.
- Full solution rerun is required before closing its aggregate validation gate.

## Fresh Frontend and API Validation

- Rebuilt Designer, Company, Documentation, Demo, Admin and Account production
  outputs successfully; all six build-info artifacts validate as PVAD 1.1.0.
- All five website test suites passed: Company 52, Documentation 56, Demo 2,
  Admin 37, Account 50 (197 total, no skips).
- Configuration/deployment contract suites: 40/40 passed. These structural checks
  do not replace actual host, reverse-proxy and redirect smoke tests.
- API suite in the full solution rerun: 1,046 passed, zero failures, 76 opt-in
  database tests skipped. Do not describe the database validation matrix as
  complete based on this default run. The earlier focused restore test remains
  additional, limited evidence rather than a replacement for the 76 tests.

## Completed Default Solution Run

- `dotnet test PVAD.sln --no-restore -m:1 -nr:false
  -p:UseSharedCompilation=false --logger trx
  --results-directory artifacts/rename/solution-validation --verbosity quiet`
  completed with exit code zero.
- XML/XPath aggregation of all 53 TRX files: 3,372 discovered, 3,294 passed,
  zero failed, 78 results with outcome NotExecuted (76 API, two Persistence).
  Skipped tests are not included in the passed count.
- Export passes 221/221 inside the solution run, not just in isolation.
- The default suite execution checklist item is complete. The separate database
  upgrade/rollback/auth/legal/license/job/asset/tenant gate remains open until
  its opt-in tests and remaining acceptance scenarios are verified.
- Checklist progress: 64/97 (66.0%), 33 open. No version bump, commit or push.
- Additional opt-in Persistence rerun with `PVAD_RUN_POSTGRES_TESTS=1`:
  10/10 passed, zero skips. This includes real isolated PostgreSQL migrations
  for identity/organization membership and tenant-scoped jobs/object metadata.
  Eight of these tests overlap the default run; do not add ten unique tests to
  its total. The 76 API opt-in cases still require their own full run.

## PostgreSQL Follow-up and Container Readiness - 2026-10-21

- The first 76-case API opt-in run returned 71 passed, four failed, one skipped.
  The skip is the separately gated paired-binary job cutover, not an ordinary
  PostgreSQL case. The four failures were investigated, not counted as passing.
- Updated the Trial assertion from a stale numeric count to the exact expected
  capability set, including PDF Trust. Aligned the workspace test with its
  existing persisted-on-first-open contract; revision zero remains asserted.
- Renamed the Admin operator guide source to its already registered filename,
  restoring the missing catalog entry; added source-file existence contracts.
  Protected access and historical Legal recovery wording remain unchanged.
- The four focused cases then passed. The original concurrent-template HTTP 500
  was not reproduced; five additional isolated repetitions passed. A test-only
  exception capture now reports the root exception if it recurs. No production
  concurrency fix is claimed from repeated passing results alone.
- The final complete opt-in selection plus three documentation contracts is
  being rerun with the two existing paired-binary harnesses explicitly enabled.
- Isolated API image `pvad-webapi:rename-validation`, image ID
  `sha256:7f1768dbb341fa80caadf6b2cee1312e4cc65f08dde9cc5867c02798e5b998e0`:
  actual EF-generated SQL applied to a new PostgreSQL database; readiness 200,
  database loss 503, liveness after database loss 200. Non-root UID, read-only
  root, both worker DLLs and built-in healthcheck verified. Only temporary test
  containers/network/anonymous volumes were created; cleanup was confirmed.
- Container readiness execution gate complete; other image platforms, release
  metadata, security scanning and external rollout remain separate open work.
- Interim checklist progress: 65/97 (67.0%), 32 open; database gate remains open
  until the final opt-in results are evaluated. VERSION remains 1.1.0.

### Final Opt-in Results

- Final selection completed successfully: 79 executed, 79 passed, zero failed,
  zero skipped, approximately eight minutes. TRX:
  `artifacts/rename/api-postgres-final/_My-3_2026-10-21_08_50_49.trx`.
- The selection contains the exact 76 previously skipped API cases plus three
  protected-runbook contract cases. The paired old/new process test was explicitly
  enabled using the existing separately compiled PXA/PVAD harnesses; no historical
  source or old artifact was rewritten to enable it. Real dump/restore also passed.
- Together with the 10/10 Persistence rerun, all 78 formerly skipped default-run
  cases now have passing follow-up evidence. This is combined-run evidence, not
  a claim that one full Solution invocation ran with every opt-in switch enabled.
- Initial template HTTP 500 remains an unexplained, non-reproduced observation:
  focused retry, five independent repetitions and final full selection all pass.
  Keep the exception capture for diagnosis if it recurs; do not claim a production
  race-condition fix without a root cause.
- Added patch fragment `operator-guide-packaging`; release tooling check,
  fragment validation and 27 release-author/versioning tests pass. Current rename
  inventory regenerated; two historical/vendor boundary checks pass.
- Final checklist progress: 66/97 (68.0%), 31 open. Host/redirect browser checks,
  release-grade image metadata, broader artifact/name review and external rollout
  remain open. No commit/push/version bump; VERSION remains 1.1.0.

## Host, Documentation And Build-Metadata Package - 2026-10-21

### Completed Work

- Moved 73 owned active documentation/checklist files; durable mappings are in
  `docs/pvad-documentation-path-migration.json`. Historical bodies retain an
  explicit naming-history note. All relative file links in moved documents resolve.
- Updated root/developer guides, current website prose, DocFX identity, cookbook,
  tool README examples, llms content and the release-author skill. The protected
  skill edit was explicitly authorized. Historical legal publication bytes,
  restore confirmation text, issued credentials and mounted secrets were not
  mechanically rebranded.
- Added a shared conflict-checked development API origin to all six Vite configs.
  Isolated real frontend servers verify proxies, runtime configuration, safe
  Account return paths, Company page boundaries and cross-site links. Authentication
  responses in this host suite are synthetic; real authenticated E2E evidence from
  earlier runs remains distinct.
- Fixed eight dead public Documentation anchors and packaged eight explicitly
  allowlisted Markdown guides with their public schemas/native examples. A guide
  request now returns Markdown rather than a successful SPA fallback. Operator
  guides, internal checklists and Legal evidence are not published by this plugin.
- Necessary-storage acknowledgements write PVAD.StorageNotice and recognize the
  prior PXA acknowledgement. The technical inventory, public storage explanation,
  unit tests and real-browser cookie checks agree. Existing published legal text
  remains immutable and requires its normal publication process if updated.
- Managed Loki, Tempo and Operator proxy inputs use PVAD. Shell adapters reject
  conflicting aliases without evaluating values or exposing them in errors.
  Existing physical service/volume/network names and public registry repositories
  remain documented exceptions, not unauthorized infrastructure migrations.
- The actual MCP TLS run found an old Nginx upstream service name. Corrected it
  and repeated the full TLS/protocol/dependency-loss/clean-shutdown smoke; it passed.
  Its unique temporary Compose project was removed by the test cleanup.
- Built both API and webhook-relay images with strict product-version, full
  commit identity and timestamp checks. Image IDs and labels are in
  `artifacts/rename/image-metadata-results.json`. Local images explicitly include
  the dirty-worktree suffix; stable release validation rejects that suffix.
  No image was pushed. Both worker publishes receive the same commit metadata.

### Passing Validation

| Scope | Result |
| --- | --- |
| Focused API/provider/worker/documentation selection | 95/95, zero skipped |
| Protected operator document contracts after final prose edits | 3/3 |
| Configuration, documentation links, versioning and release-author contracts | 74/74 |
| Company / Documentation / Demo / Admin / Account | 55 / 57 / 2 / 37 / 50 passed |
| Shared storage-notice tests | 3 passed, included in Company total |
| Legal inventory/recovery contracts | 15/15 |
| Browser-storage inventory validator | 17 entries validated; optional storage disabled |
| Six-host desktop/mobile browser suite | 7/7 |
| Actual Operator Nginx configuration | canonical, legacy and conflict cases passed |
| Actual MCP container test | typecheck/tests/build, TLS, dependency loss and shutdown passed |
| Designer typecheck | passed |
| Normal production builds | Designer plus all five websites passed |
| Version synchronization / fragments / diff whitespace | passed; VERSION 1.1.0 unchanged |

The final normal builds replace the temporary host-suite build origins. Browser
screenshots are under `artifacts/rename/`; these are local validation artifacts,
not replacements for all historical embedded screenshots. The complete Solution
and full Designer Jest results above are earlier runs, not new full-suite reruns
for this documentation/configuration package.

### Residual Gates

- The initial guide-anchor and MCP-upstream failures were corrected and rerun;
  they are not outstanding failures.
- Vite reports native-loader compatibility warnings and large Designer bundles.
  MCP image dependency installation reports four audit findings (two moderate,
  one high, one critical). No dependency upgrade was attempted in this rename
  package; security triage is required before calling an image production-ready.
- NuGet vulnerability-feed retrieval and the existing NPOI EULA warning remain
  visible; passing builds do not establish license or supply-chain approval.
- Real deployment owners, domains, registry scopes, customer volume IDs, retained
  encryption/key access, native OS associations, historical media and exhaustive
  remaining-name classification remain separate open gates.
- Current inventory: 1,198 files with historical/compatibility/binary/name matches;
  automatic classification is triage, not final per-match approval.
- Checklist progress: **78/97 (80.4%)**, **19 open**. No commit, push, tag,
  production deployment or product-version change was performed.

## Remaining-Name And Artifact Audit - 2026-10-21

### Fixes And Evidence

- Found and fixed a release/deployment mismatch: the stable producer emits seven
  `pvad-*` archives, while deployment verification still expected a mixed set.
  `tools/deployment/pvad-deployment.mjs` now verifies complete canonical sets and
  complete historical `pxa-*` sets, retains names/digests, rejects mixed, duplicate
  or incomplete sets, and records artifact identity separately from new PVAD
  deployment evidence. The historical CLI remains a tested delegating adapter.
- CI and deployment validation use the canonical tool/test paths. Updated owned
  workflow display text, the Operator wordmark and schema titles. SBOM component
  identities use PVAD; publisher display text uses PVADE. Existing GHCR repository,
  protected environment, concurrency-group and schema URL identities are preserved.
  No GitHub environment, branch policy, registry or deployment was changed.
- Added separate old-name-location and binary/generated-output audits. Source
  findings contain paths, line numbers, hashes and redacted brand families only;
  they do not copy source lines or matched suffix values. Findings remain an
  unapproved review queue, not automatically accepted legacy exceptions.
- Byte audit: 96 repository binary artifacts. 94 match their baseline Git blobs
  exactly; the only two additions are the canonical native-project fixtures.
  Source containing a literal NUL is still treated as source rather than falsely
  classified as a binary. No signed PDF, vendor font or OCR model was rebranded.
- Approved cleanup removed 4,211 untracked generated PXA assemblies/symbols/
  dependency manifests from bin/obj. The entire plan was path/hash validated
  before deletion. All job-cutover legacy bin/obj outputs were excluded. A repeat
  audit reports zero unexplained stale PXA build names; intentional paired legacy
  outputs remain. Cleanup evidence is retained and a no-op does not overwrite it.
- Local Tesseract scanned all 22 documentation screenshots. Ten protected Admin
  images contain old PXA/Power Dox text. Visual inspection of the dashboard confirms
  the old UI; the mixed-page image was inspected as an unbranded geometry example.
  None of these images was modified or falsely relabeled as current. All captures,
  including mobile, still require current-app recapture/privacy/pixel approval.

### Validation

- 99 configuration, deployment, versioning, release-author and Legal contracts
  passed with no skips.
- Solution build after cleanup passed: zero errors, 56 existing warnings.
  The first sandboxed attempt timed out after five minutes with no compiler errors;
  an explicitly authorized retry outside the sandbox completed in 24.45 seconds.
  NuGet vulnerability-feed retrieval and existing license warnings remain visible.
- Focused PDF corpus, visual artifact and security-handler regression: 41/41
  passed with no skips after the rebuild.
- Version/fragment validation and `git diff --check` passed. New patch fragment:
  `pvad-deployment-artifact-validation`. VERSION remains 1.1.0.
- Reports: `artifacts/rename/name-and-artifact-audit.json`,
  `artifacts/rename/legacy-build-cleanup.json` and
  `artifacts/rename/screenshot-ocr-audit.json`. They can be regenerated using the
  corresponding tools under `tools/configuration`; cleanup is dry-run by default.

### Progress And Next Gate

**79/97 (81.4%), 18 open.** The separate artifact-review task is complete as an
audit with findings, not a visual approval. The screenshot-regeneration task,
exhaustive source-match approval and external gates remain open. Reaching at
least 90% requires nine more complete checklist closures; this package does not
claim that threshold was reached. Next internal work is actual Admin screenshot
recapture and contextual resolution of the remaining source/configuration names.
No commit, push, tag, host restart or production deployment was performed.

## Internal Admin, Mail And Operations Follow-up - 2026-10-21

### Delivered

- Eleven protected Admin screenshots were recaptured from the actual Vite Admin
  application and running API using a newly migrated, isolated PostgreSQL
  container. Development bootstrap created only a synthetic administrator.
  The initial anonymous auth/me 401 is expected and checked separately. After
  login every captured workspace API request succeeded. No auth/workspace API
  response was mocked. The tool stages images, masks email text with a neutral
  color, and publishes them only after the full capture passes. Its own database
  container and child processes were removed; existing hosts were never reused.
- Capture evidence: `artifacts/rename/admin-capture/capture-evidence.json` with
  eleven image hashes. SQL input is explicit; bootstrap rejects a schema with
  pending migrations. Synthetic keys/logs are ignored local evidence, not shipped
  operator credentials. The recreated images remain under the protected Admin
  directory; none was moved to public Documentation.
- New development user/role descriptions use current names. Existing stored
  descriptions are not overwritten. Transactional template subjects, HTML,
  plain text and fallback user names use PVADE in all six locales. Default sender
  display text is updated; sender addresses, configured URLs, template keys,
  existing recipients, outbox IDs and retained ciphertext are unchanged.
- Six operational shell scripts now resolve canonical PVAD environment names
  and fail before executing commands if matching PXA inputs conflict. Updated
  Alertmanager production overlay and retention Compose/Loki/Tempo inputs
  atomically. Actual config resolution is equal for legacy-only/current-only
  inputs. No failure injection, retention deletion or operational mail delivery
  was run against an existing stack.
- Fixed `.gitignore` excluding `.dockerignore`, which would otherwise have kept
  build-context protections local rather than shipping them in this branch.
  Added the owned Observability secret directory to Docker exclusions. Contracts
  verify the context file is no longer Git-ignored. No actual secret was read,
  moved or modified as part of that correction.

### Verification And Boundaries

- API build passed. Focused API regression: **46/46 passed, zero skipped**,
  including mail rendering (13 templates across six locales), mail Data Protection,
  file storage, protected documentation and six new CORS origin cases against both
  canonical and historical API paths. Allowed local origins retain credentialed
  responses; untrusted, near-prefix and null origins receive no CORS grant.
- Node configuration/deployment/versioning/Legal selection: **106/106 passed**.
  The selection includes six pre-operation shell conflict tests and canonical/
  legacy Alertmanager/retention Compose equivalence. Version/fragment and
  whitespace checks passed. Existing NuGet-feed, NPOI and compiler warnings remain
  visible; no dependency/license approval is implied.
- Explicit Data Protection consumers are mail queue/delivery and revocation
  evidence. Source review confirms that job/object storage has no application
  Data Protection envelope to rename. Local restart/purpose/byte-preservation
  tests pass, but real production key permissions are still unverified. Do not
  add or claim storage encryption merely to close the rename checklist.
- OCR reports zero legacy matches after Admin recapture, but manual inspection
  found small PXA logo pixels in public Viewer and Favorites images that OCR
  missed. Public Designer/Viewer screenshots therefore remain an open gate.
  No image was edited to replace its old logo and pretend it was a new capture.
- Public-logo discovery supersedes any interpretation of the earlier OCR scan
  as approval. Global screenshot regeneration, first-party PDF metadata/prose,
  exhaustive old-name review, OS associations and external rollout remain open.

Repository-level representative cutover acceptance is now recorded using the
earlier paired PXA/PVAD retained-data processes, real dump/restore and native-format
tests together with current security and real Admin login evidence. This is not
a certificate for an uninspected customer installation. Checklist progress:
**80/97 (82.5%), 17 open**. Reaching 95% still requires thirteen complete closures;
none of the remaining external decisions is silently approved.

New patch fragments: `pvad-admin-mail-and-operations-branding` and
`pvad-container-context-packaging`. VERSION remains 1.1.0. No commit, push, tag,
registry publication or production deployment was performed.

## PDF Runtime Identity and Public Capture Follow-up

- Current generator/importer PDF metadata uses PVAD; ICC manufacturer/creator
  fields remain exactly four bytes. Stable diagnostic identifiers are preserved.
- Infrastructure.Pdf: 57/57 passed, including four new metadata/profile cases.
  The existing normal-render golden hash remains unchanged.
- Generator: initial run 136/137 passed; verbose repetition passed 137/137.
  The initial quiet log lacks assertion details, so no code fix or root cause is
  claimed for that first failure.
- All 36 migration projects passed: 690 executed, zero failed or skipped.
  The initial aggregate test caught an outdated e-invoice guide path expectation;
  source and test now point to the actual packaged PVAD Markdown guide.
- Importer corpus tests retain immutable historical JSON/PDF evidence. Only the
  exact historical validator display name is normalized on a cloned expected
  report. A new test verifies that other versions and historical input stay intact.
  Final full run passed 646/646 with zero skips (TRX in ignored
  `artifacts/rename/importer-final`). The preceding run reported one RFC3161 test
  failure; the retry passed without a production code change. A restricted retry
  could not bind the VSTest socket and was rerun with the required permission.
  Neither transient failure is claimed to have an established root cause.
- Designer: 80 suites, 711 tests passed; typecheck and production build passed.
- Documentation production build passed; configuration/documentation contracts
  passed 108/108. Version/release-author contracts passed 27/27; version and
  fragment validation passed without changing VERSION or published history.
- Public capture: three desktop/mobile browser cases passed before publishing
  eight Viewer and two Editor images. Synthetic API fixtures are explicitly
  disclosed; these screenshots are not cryptographic verification evidence.
  Together with the earlier eleven real Admin/API captures this closes repository
  screenshot regeneration. The unbranded geometry illustration is unchanged.
- Artifact audit: zero stale generated identities; 13,426 old-name locations
  remain inventory candidates, not blanket-approved exceptions.

Progress is **81/97 (83.5%), 16 open**. This is not 96%: production ownership,
compatibility decisions, complete residual-match review and real rollout gates
remain open. See [release approvals](../docs/pvad-rename-release-approvals.md).
New patch fragment: `pvad-pdf-runtime-identity-and-viewer-guidance`.

## Core/Infrastructure Residual Branding Review

- Updated current descriptions in eleven Core/Infrastructure files, plus the
  package-local image-reference description, without renaming serialized fields.
- Word comment and revision fallback authors now use PVADE. Three new cases
  inspect actual DOCX comments and tracked-insertion authors: null defaults to
  PVADE; explicit historical PXA and other supplied authors remain unchanged.
  Word suite passed 6/6; Core suite passed 143/143, including native archives.
- Branding API now reports PVADE/PVAD, `pvad`, primary extension `.pvadp`, and
  additive `.pvadp`/`.pvads` extension list. Legacy metadata remains explicit.
  Three focused HTTP route cases passed without skips. The broader System filter
  passed five cases and skipped one optional PostgreSQL case; it is not counted
  as a newly executed database validation. Initial restricted MSBuild could not
  bind its IPC socket; it was stopped and the permitted rerun succeeded.
- Exact-source regression check permits only `LegacyMagic` as standalone PXA
  wording in Core/Infrastructure C#; joined diagnostics, version readers, physical
  database names and old source markers have separately documented rationale.
  The legacy XML root remains an explicit pending compatibility decision.
- Configuration and versioning contracts passed 79/79. Version synchronization,
  fragment validation and whitespace validation passed. VERSION stays 1.1.0.
- Inventory refreshed: 1,161 matching files. These are review candidates, not
  1,161 defects or approved exceptions; neutral graphics APIs are also included.

The repository-wide contextual audit remains open: **81/97 (83.5%), 16 open**.
New patch fragment: `pvad-branding-api-and-word-authors`. No commit or publication.

## XML, Importer, API and Operational Completion Batch

- XML exports now default to `PvadDocument`; explicit .NET `ExportLegacy` preserves
  `PxaDocument`. A parsed-tree comparison verifies that the envelope name is the
  only difference, including preservation of document text containing PXA. The
  HTTP exporter uses the canonical root on both route generations. This is
  documented as a major-release contract change, not transparent XPath compatibility.
- Current importer messages/metadata and API descriptions/account messages now
  use PVAD/PVADE. Source contracts enumerate bounded diagnostic families and exact
  compatibility literals rather than exempting entire directories. Stable codes,
  authentication schemes, cookie/key purposes and stored evidence remain intact.
- Current legal-readiness and fidelity/parity report headings were updated.
  Twenty-nine unpublished fragment summaries use the current brand. The historical
  PXA 1.1.0 publication-verification text remains an exact reviewed exception.
  Published release entries, fragment identities and VERSION were not rewritten.
- The actual application exported a fresh OpenAPI snapshot; it is packaged by
  Documentation and its production build passed. Canonical-route and description
  tests passed. No hand-edited substitute OpenAPI was used.
- Webhook Compose inputs use PVAD with checked old aliases; actual Compose output
  matches for both generations, including destination, bounds and secret-file
  references. Physical service/network/secret names remain unchanged.
- Operational alert descriptions, dashboard search tags and mail subject/body
  text use PVAD. Matching smoke scripts use the new subject and canonical headers.
  Real Prometheus 3.12.0 rule tests passed; all 37 rules validate. Alertmanager
  0.32.1 rendered all three mail templates with synthetic default data. No actual
  mail, webhook, destructive failure injection or existing-stack mutation occurred.
- Sequential .NET validation: Converters 11/11, Export 221/221, FileImporter 23/23,
  ImageOcr 100/100, Importer 646/646 passed, zero skips in these five groups.
- Initial full API run: 1,049 passed, four failed, 76 skipped. Failures identified
  stale source-contract assertions for canonical environment keys, including
  changes from the preceding rename work. Corrected assertions retain the secret
  and bounded-operation checks; Node tests independently compare actual Compose
  resolution and reject conflicting aliases before operational commands.
  Final full API run: **1,053 passed, zero failed, 76 optional PostgreSQL skips**.
  TRX and logs remain in ignored `artifacts/rename/runtime-contracts/`.
- Combined configuration, fidelity, legal, versioning and Documentation contracts
  passed 173/173 before the final unpublished-summary check. Configuration plus
  versioning passed 83/83 with that additional check; its seven scoped identity
  cases were rerun after preserving the historical publication exception.
- Release-fragment validation and a release-preparation dry run passed. The dry
  run proposes major 2.0.0 and requires approval; no version change, tag or publish.
- Final `dotnet build PVAD.sln --no-restore -m:1` passed: zero errors, 58 warnings
  (existing NPOI license notice and unavailable NuGet vulnerability metadata).
  This is build success, not resolution of third-party licensing or security feeds.
- Final combined Node run passed **175/175**, zero failures/skips, including the
  historical-fragment exception and operational-mail subject consistency check.
  Final artifact audit: 13,225 old-name locations, zero stale generated identities.

Current inventory: 1,109 matching files. This is a context-review queue, not a
defect count. Exhaustive lowercase/joined-identifier and historical-document review
is not replaced by standalone source contracts. Progress remains **81/97 (83.5%)**
with **16 gates open**, including external decisions and the repository-wide audit.
New fragments: `pvad-xml-export-envelope` (major) and
`pvad-importer-api-and-webhook-identity` (patch). No commit or push was requested.

## Phase Review and Retention Status Correction

- Compared 1,984 unstaged tracked deletions with current targets; zero unresolved
  pairs. Thirty-six external-signature PDF/XML evidence files retain their physical
  names and exact HEAD bytes. Before/after hashes are in ignored
  `artifacts/rename/file-pairs.json`. Pairing is not semantic approval or a staged
  diff review. The new tool makes that limitation explicit.
- Corrected the two missed owned basenames found by this check: WebApi HTTP
  example and Designer workbook-schema test. The example uses the canonical route;
  the test retains the legacy schema comparison.
- Reviewed release/deployment workflow and shared build-property diffs. Existing
  external GHCR repositories and operational concurrency/environment identities
  remain pending owner decisions; no publication destination was silently changed.
- Corrected stale Company product-page HTML titles/descriptions and the shared
  Company template. The metadata test now recursively checks nested HTML pages.
  Demo descriptors use actual Core DTO names or an explicit illustrative session
  label; tests verify supplied filenames, sheet names, text and object references
  remain unchanged. Company tests passed 56/56; Demo tests passed 3/3.
- Fixed an actual retention-status mismatch: the current inventory and Admin
  service now advertise PVAD keys and read canonical/legacy input through the
  checked helper. Six focused tests pass: production gate plus canonical, legacy,
  identical dual inputs, defaults and conflicting values. These use EF InMemory,
  not PostgreSQL. Legal approval, holds and retention durations remain unchanged.
- Company and Demo production builds passed. Full Solution build passed with
  zero errors and 56 existing license/vulnerability-feed warnings. The full API
  suite from the preceding batch was not rerun for this narrow status correction;
  its six focused tests are reported separately, not added to the old full total.
- Combined configuration/legal/versioning/brand/Demo tests passed 109/109.
  Full Designer Jest rerun passed 80 suites / 711 tests after the test-file rename.
  Version and fragment validation passed. VERSION remains 1.1.0; published release
  history is untouched. New patch fragment: `pvad-public-metadata-and-retention-status`.

See [phase review](../docs/pvad-rename-phase-review.md) and the seven concrete
questions in [release approvals](../docs/pvad-rename-release-approvals.md).
Current inventory has 1,101 matching files; the artifact audit reports 13,232
matching locations and zero stale generated identities. Added regression tests
and review documents themselves contain intentional old-name references.

Progress remains **81/97 (83.5%), 16 open**. The repository-wide context audit and
final semantic/staged review are still incomplete, not merely blocked on outside
parties. No staging, commit, push, tag or deployment occurred.

## CI and Historical Release Checkout Review

- Found and fixed a rollback/preflight compatibility defect: verification always
  read the renamed manifest inside the selected release checkout, which fails
  against old tags containing `pxa-releases.json`. The reader now requires exactly
  one of the two known filenames. Missing, malformed and ambiguous inputs fail;
  there is no mixed-archive or arbitrary-name fallback.
- Actual CLI integration tests create isolated temporary Git checkouts with
  synthetic commits/tags and manifests from both generations. They verify the
  selected immutable commit and the complete matching seven-archive artifact set.
  Tests create no commit/tag in the workspace and contact no registry or GitHub.
- Renamed ephemeral CI artifact labels and matching deployment upload/download
  handoffs, temporary PR-body files and local SBOM image names. External GHCR
  repositories, concurrency locks and protected environment names remain stable.
- CI now passes an explicit container build timestamp and verifies version, full
  commit and timestamp through the existing strict validator, rather than checking
  the version label alone. No image was rebuilt or published in this batch.
- All nine workflow files parse with the installed YAML parser. Workflow identity,
  deployment and build-metadata tests passed 19/19; the larger configuration,
  deployment and versioning selection passed **104/104**, zero failures/skips.
- VERSION/fragment validation and `git diff --check` passed. No .NET, Designer or
  website implementation changed, so their earlier builds are not reported as new
  executions. No remote workflow dispatch or external deployment was performed.

Current inventory: 1,100 matching files, still a context-review queue. The overall
audit/final staged review and external gates remain open: **81/97 (83.5%)**.
New patch fragment: `pvad-historical-release-validation`. VERSION remains 1.1.0;
no repository staging, commit, push, tag or publication.

## Active Guides and Public Download Packaging

- Reviewed current documentation overview, Chart/Code Workspace guides, asset and
  existing-PDF architecture, Nutrient Java target inventory and API deployment
  README. Current first-party prose uses PVAD/PVADE; vendor names, snapshot dates,
  source limitations and stable diagnostics remain unchanged. No external vendor
  API revalidation is claimed by this local naming pass.
- Corrected instructions against actual repository contracts: Charts belong to
  Tables and Charts; new code markers use `pvad-element-id` with legacy readers;
  native exports use `.pvadp`/`.pvads` while old extensions remain accepted inputs.
- Clarified that e-invoice artifact-mount paths/variable names are operator-defined
  conventions, not automatically consumed API configuration. Existing mounts and
  artifact approval remain separate operational/legal decisions.
- Fixed missing public assets: release fragments linked to Chart/Code Workspace
  Markdown guides absent from the packaging allowlist. Both are now explicitly
  included, and production output bytes match the maintained sources.
- Public DocFX inputs now include linked native/architecture sources. Removed
  dangling operator navigation without adding mail operations to public inputs.
  Static tests verify this separation. **DocFX CLI is not installed**, so no DocFX
  metadata/HTML generation was run or reported as successful.
- Documentation production build passed. Configuration plus Documentation tests
  passed 120/120; including version/release-author contracts, the final selection
  passed **147/147**, zero failures/skips. VERSION/fragment validation and whitespace
  checks passed. No .NET or Designer implementation changed or was rebuilt here.
- Screenshot inventory wording now distinguishes initial findings from completed
  recapture evidence; no screenshot bytes or signed corpus artifacts changed.

Inventory now contains 1,096 matching files. The repository-wide context/staged
review and external gates remain open: **81/97 (83.5%)**, 16 open. The new patch
fragment is `pvad-active-guides-and-public-downloads`; VERSION remains 1.1.0.
No commit, push, tag, registry publication or deployment occurred.

## Internal Operator Naming Follow-up (2026-10-23)

- Renamed local Nginx helpers and all auth-subrequest consumers, including the
  two .NET source assertions. The upstream legacy operator header and stripped
  authentication header names remain unchanged.
- New backups use `pvad-<timestamp>-<pid>.dump`; the recovery drill discovers
  this filename. Existing backups and checksums are not moved or changed.
  Restore still accepts either basename with the existing confirmation guard.
- Disposable recovery/retention resources and synthetic retention telemetry use
  PVAD. Each retention run gets its own project; fixed local ports still require
  serial execution. Kept failed resources report their project for cleanup.
- Local Mailpit sender/recipient addresses use the reserved example.test domain;
  mail probes and expected synthetic service/message identifiers match. Existing
  dashboard and runbook references, API aliases and live alert-rule labels stay
  intact. The negative hosting probe now uses a PVAD-only nonexistent path.
- 21 naming/documentation/script contracts and 27 version/release-author tests
  pass (48 total, zero skips). Backup/restore checks use fake database executables
  and real file/checksum logic; they do not validate actual PostgreSQL recovery.
- Version synchronization and fragment validation pass. Patch fragment:
  `pvad-internal-operator-names`. VERSION remains 1.1.0.
- No real backup, database restore, container execution, Nginx runtime check, mail
  delivery, deployment, complete build or .NET test run is claimed by this batch.

## Operator Guides And Monitoring Labels (2026-10-23)

- Restricted Admin and recovery runbooks use current API paths, mounted secret
  paths and new backup names. Existing installed secrets/backups are not moved.
  The proposed Admin domain is not reported as a verified deployment.
- MCP setup uses the current Compose hostname and canonical scopes, explaining
  compatibility normalization and retained discovery spellings. No stored grants
  or runtime defaults were changed.
- Grafana's three datasource display names use PVAD. Existing UIDs, trace/log
  cross-links, dashboard provider/folder and permission bindings stay intact.
  Removed active datasource names from deleteDatasources so provisioning updates
  their existing UID entries instead of deleting/recreating them. The obsolete
  historical Alertmanager removal remains. Name-based custom queries require
  operator review after deployment; no automatic folder migration is claimed.
- 67 focused naming, documentation, runtime-source-contract and version tests
  pass without skips. Four new contracts cover source/API/example agreement,
  recovery guidance, MCP scope guidance and Grafana provisioning references.
  YAML is parsed locally; this does not establish a successful Grafana upgrade.
- Version and release-fragment checks pass. Patch fragment:
  `pvad-operator-guides-and-monitoring-labels`; VERSION remains 1.1.0.
- No production configuration, database, OAuth grant, deployment or published
  release was modified. Full builds and runtime validation remain deferred.

## Canonical Validation Caller Keys (2026-10-23)

- Four internal launchers now pass PVAD-prefixed test/validator settings to
  existing canonical readers. The Redaction Recovery command example matches.
- Added caller-to-receiver contracts and fake-dotnet shell launch tests. These
  verify flag propagation, then deliberately stop before downstream commands.
  Legacy receiver aliases and conflicting-value rejection remain unchanged.
- Six focused tool-key contracts pass; the combined naming, guide, configuration
  and release test selection passes 73/73 with no skips. Both edited JavaScript
  tools pass Node syntax checks; version and fragment validation pass.
- Existing evidence schema IDs, PDF fixtures, manifests and visual baselines
  were not rewritten or regenerated. No real database, .NET suite, Docker image,
  veraPDF validation or visual comparison was executed by this batch.
- Internal-only fragment: `pvad-validation-caller-keys` (impact none).
  VERSION remains 1.1.0; no commit, push or deployment was performed.

## Broad Build And Test Follow-up (2026-10-23)

Source checkpoint: `5a0f48c4` (internal operator names and validation callers).
The only subsequent executable-source correction changes two stale temporary
container-prefix expectations in `tools/legal/recovery-contract.test.mjs`.
No product implementation, dependency or golden baseline was changed in testing.

| Selection | Result |
| --- | --- |
| `dotnet build PVAD.sln --no-restore --disable-build-servers -m:1` | Passed; 0 errors, 171 warnings |
| Default complete Solution tests | Not complete; stopped after repeated API startup/database-authentication failures |
| Solution tests with `FullyQualifiedName!~PVAD.Api.Tests` | Passed; 2,264 passed, 2 skipped, 0 failed; 53 TRX files including the excluded API runner |
| Export suite within the non-API run | 221/221 passed, 0 skipped |
| Designer typecheck / Jest / production build | Passed; 729 tests in 83 suites |
| Company tests / build | Passed after stale test-prefix correction; 57 tests |
| Documentation tests / build | Passed; 58 tests |
| Demo tests / build | Passed; 4 tests |
| Account typecheck / tests / build | Passed; 52 tests |
| Admin tests / build | Passed; 37 tests |
| MCP typecheck / tests / build | Passed; 26 tests |
| Configuration, Legal, deployment and version Node contracts | 199/199 passed |

Counts are per selection and must not be added as unique coverage: some Website
commands include shared/Legal tests that also occur in the Node selection.
The two skipped tests are the opt-in PostgreSQL Persistence migration cases;
they are not successful database-validation evidence.

### Failure Analysis And Corrections

- The sandbox initially denied VSTest TCP sockets and tsx IPC sockets. Those
  starts were stopped/retried with approved access. They are not product failures.
- The unfiltered API run reached application startup, then repeatedly failed
  PostgreSQL authentication for the fresh local PVAD database/user defaults.
  Plain WebApplicationFactory fixtures start normal application services. No
  credentials were changed, database initialized or migrations applied to make
  these tests pass. API integration validation is still open; do not reuse an
  installed database merely to satisfy it.
- Company tests found two remaining assertions for old disposable recovery
  container prefixes. Only those expectations were changed to the canonical
  prefixes. Cleanup, synthetic-data, checksum and fail-closed assertions remain.
  Company and the broader Node selection pass after this correction.
- Build warnings include compiler nullability/obsolete API warnings and NuGet
  security-feed retrieval failures. Successful compilation is not an up-to-date
  dependency vulnerability assessment. The Designer build also warns about large
  chunks; this pass does not claim a performance improvement.

### Evidence And Remaining Work

Local logs: `/tmp/pvad-rename-solution-build.log`,
`/tmp/pvad-rename-solution-tests-unsandboxed.log`,
`/tmp/pvad-rename-nonapi-tests.log` and `/tmp/pvad-rename-nonapi-results/`.
Frontend/tool logs use `/tmp/pvad-designer-*`, `/tmp/pvad.*-*`,
`/tmp/pvad-mcp-*` and `/tmp/pvad-node-contracts-rerun.log`.
These temporary files are diagnostic artifacts, not committed immutable evidence.

1. Isolate API test startup from installed development databases and run the
   complete API suite against deliberate test services/configuration.
2. Run opt-in database integration tests against disposable databases and repeat
   the complete unfiltered Solution selection once the startup issue is resolved.
3. Complete authenticated browser/visual and live Docker/cutover validation on the
   final source; no such runtime claim is made by production builds/unit tests.
4. Refresh dependency security assessment when the package security feed is
   available and address advisories through a separate reviewed change.

VERSION stays 1.1.0. No release, tag, push, registry publication, protected-environment
change or production deployment was performed. Full release readiness remains open.

## Production Link Defaults (2026-10-23)

- Shared production defaults now match the selected candidate domain/subdomains.
  The Designer Account fallback matches; local ports remain unchanged. Canonical
  and legacy configuration overrides still work and reject conflicting values.
- Frontend return-origin tests use the new defaults. An old origin is rejected
  unless explicitly configured; no automatic second trust set was added.
- Hosting, Company strategy and operator instructions distinguish source defaults
  from actual DNS/TLS/host readiness. Backend registration/Designer allowlists,
  OAuth callbacks, SMTP sender and Storage Notice cookie scope are not migrated.
  Existing installations must set their approved origins explicitly; authentication
  cookies are not shared across subdomains by this change.
- 205 configuration/Legal/deployment/version/link contracts pass. All five Website
  test suites and production builds pass; Account typecheck passes. Designer
  typecheck, all 729 Jest tests and production build pass. Version and fragment
  checks pass. No new .NET or authenticated browser run was performed; the API
  startup/database authentication blocker above remains open.
- Local logs: `/tmp/pvad-production-links-contracts.log`,
  `/tmp/pvad-links-PVAD.*-*.log` and `/tmp/pvad-links-designer-*.log`.
  These are temporary diagnostic outputs, not remote deployment evidence.
- Patch fragment: `pvad-production-link-defaults`; VERSION remains 1.1.0.
  No remote DNS, TLS, mailbox, registry or protected-environment change was made.

## Backend Origin Defaults (2026-10-23)

- Registration return origins now use the canonical Company, Documentation,
  Demo, Designer and Account HTTPS hosts, retaining the five local product
  origins. Designer authorization uses only the canonical Designer HTTPS host
  and its two existing local origins.
- Neither Admin nor Operator is added as a customer return destination. Legacy
  domains are not implicitly allowed; existing installations can explicitly
  configure their approved origins. The guide explains .NET's indexed array
  merging, including that shorter overrides do not clear trailing defaults.
- Unchanged: mail sender/configuration, CORS defaults, OAuth client registrations,
  cookies, code handoff lifetime and compatibility route/diagnostic identifiers.
- The focused DesignerAuthorizationSecurityTests selection passes 22/22 with
  zero skips. It binds the actual JSON defaults and checks exact origin sets,
  negative host/port cases, origin normalization, return paths and PKCE. These
  are unit/configuration tests without WebApplicationFactory or database access;
  they do not prove a live authorization-code handoff.
- The complete current Node configuration/Legal/deployment/version/link contract
  selection passes 205/205; version and fragment validation pass. Local .NET
  compilation still reports existing warnings, including unavailable NuGet
  security-feed information. No dependency change or security-scan approval.
- Logs: `/tmp/pvad-origin-default-dotnet.log`,
  `/tmp/pvad-origin-default-tests/` and
  `/tmp/pvad-origin-default-contracts-final.log`. These are temporary evidence.
- Patch fragment: `pvad-backend-origin-defaults`. VERSION remains 1.1.0.
  Public host/DNS/TLS verification, authenticated browser testing and the earlier
  API integration/database-startup blocker remain open. No services were restarted,
  credentials changed, database migrated or remote configuration updated.

## Canonical Schema Domain And Offline Aliases (2026-10-23)

- Prerequisite URL/auth changes are committed as `fc223d97`. This follow-up changes
  13 maintained schema IDs to the selected domain. Each prior domain ID remains an
  explicit offline alias; older alias spellings remain present.
- The registry has 14 entries, including the intentionally separate original
  native-manifest schema. The three legacy schema files remain byte-identical.
  Native legacy archive hashes also remain unchanged. No legal document, signed
  evidence, approval or customer data is rewritten.
- ID/constraint tests pin each maintained schema after excluding root ID and
  normalizing current reference identities. Only schema IDs and the current
  native-reader reference change; validation constraints remain unchanged.
- Existing reference-wrapper aliases did not support fragment lookup. The registry
  now registers the same schema object under each alias, preserving access to
  `$defs`. Tests cover every definition under canonical and historical IDs.
- MCP packaging uses the existing registry as its schema-file inventory. It copies
  schema definitions, not actual metadata documents. Stdio tool/resource behavior
  and legacy raw resources remain intact. The distribution smoke starts the built
  server, validates good/bad examples and resolves every packaged schema identity.
- Validation: 207/207 Node configuration/Legal/deployment/version/link contracts;
  27/27 MCP tests; MCP typecheck/build/distribution smoke; Designer typecheck,
  729 Jest tests and build; Documentation 58 tests and build. All pass. Version,
  fragment and whitespace checks pass. No .NET or live browser/database run is
  claimed for this schema follow-up; earlier API integration blockers remain open.
- AJV's existing optional-format limitations remain: ignored date/time/URI/UUID
  formats are not independent conformance or security evidence. Schema IDs are
  identifiers, not tested public hosting endpoints. Offline resolution performs
  no remote schema lookup.
- Logs: `/tmp/pvad-schema-contracts.log`, `/tmp/pvad-schema-mcp-*.log`,
  `/tmp/pvad-schema-designer-*.log`, `/tmp/pvad-schema-documentation-*.log`.
  These temporary logs are not immutable approval records.
- Patch fragment: `pvad-schema-domain-aliases`; VERSION remains 1.1.0.
  No push, release, tag, infrastructure cutover or production deployment occurred.

## Offline Container Workflow Preparation (2026-10-23)

- Prerequisite schema changes are committed as `6875a258`. Active `.github/workflows`
  files are byte-unchanged in this follow-up. No GitHub setting or workflow run
  was modified or triggered.
- `rename-container-workflows.mjs` reads the current release/deployment definitions
  and emits ignored candidates under `artifacts/rename/container-workflows/`.
  It renames both image publishers and pull/metadata consumers together. The
  candidate deployment inspector has an explicit image-family choice, defaulting
  to PXA for historical versions rather than guessing from archive names.
- Workflow permissions, action pins, locks and protected-environment bindings are
  retained. Unknown source shapes or duplicate/missing image publishers/consumers
  fail preparation. There is no apply, publish or deploy command.
- The deployment evidence verifier accepts one complete canonical or historical
  image family with exact immutable tags and digests, bound to the repository-owner
  GHCR namespace. Mixed families, duplicate roles, wrong versions, mutable tags,
  alternate namespaces and registries are rejected. New evidence includes
  `containerIdentity` independently of `artifactIdentity`; no prior record or
  image digest is renamed. The verifier does not prove registry ownership or image
  availability by itself.
- 212 Node contracts pass without skips. Tests parse generated YAML, check Bash
  syntax and preserved workflow boundaries, simulate image selection and exercise
  both families for deploy/retry/rollback without mutating source evidence.
  No real Docker command, registry lookup or image publication was executed.
- Version/fragment validation passes. Patch fragment:
  `pvad-container-workflow-preparation`; VERSION stays 1.1.0.
- Logs: `/tmp/pvad-container-workflow-contracts.log`. Generated candidates and the
  report carry `deploymentAuthorized: false`. Actual publisher ownership, registry
  access, immutable-version mapping, staging validation and owner approval remain
  open; concurrency/environment renaming needs its own coordinated review.
  Prior API/database and live-host validation blockers remain unchanged.

## Offline Grafana Provider Preparation (2026-10-24)

- Added an offline generator for canonical provider/folder labels. Outputs include
  candidate YAML, exact original YAML and a source-checksum report with
  `deploymentAuthorized: false`. Active provisioning, Compose, databases,
  datasource and dashboard UIDs are unchanged.
- Three new tests cover the exact two-label change, YAML serialization, original
  byte preservation, configuration drift rejection and rejection of an apply
  command. These tests do not start Grafana or prove a persisted-state migration.
- The cutover guide requires a folder/permission inventory, consistent state
  backup and tested restoration, approved placement strategy, staging checks and
  rollback. An original provisioning copy is explicitly not a database backup.
- Validation: all 215 selected configuration, Legal, deployment, versioning and
  shared-link Node contracts pass; zero skips. Version and release-fragment
  checks pass. Log: `/tmp/pvad-grafana-rename-contracts.log`.
- Patch fragment: `pvad-grafana-rename-preparation`. VERSION stays 1.1.0. No
  container, registry, live Grafana, remote GitHub or deployment operation was
  executed. Earlier API/PostgreSQL and live-host validation blockers remain open.

## Approved Technical Mail Sender (2026-10-24)

- Owner selects `no-reply@pvade.com` for transactional sending; support remains
  `support@pvade.com`. WebApi defaults and the offline candidate use the approved
  sender. Explicit environment overrides and existing stored mail are untouched.
- Candidate validation requires a separate sender, rejects missing, cross-domain
  and control-character values without echoing them, and no longer falls back
  to the support contact. Mail transport remains Disabled. Publisher selections
  remain null; no published package is not proof of registry ownership.
- All 217 selected Node configuration, Legal, deployment, versioning and link
  contracts pass without skips; version/fragment checks pass. Log:
  `/tmp/pvad-mail-sender-contracts.log`. Patch fragment:
  `pvad-approved-mail-sender`; VERSION remains 1.1.0.
- No SMTP test, mail delivery, domain verification, application restart or remote
  configuration was executed. Existing API/database and live-host blockers remain.

## Workflow Concurrency Names (2026-10-24)

- Sender work is committed as `18f9cab6`. Follow-up source edits rename the four
  workflow concurrency groups to PVAD. Parsed YAML comparison against that commit
  confirms no semantic changes to triggers, job steps, pins, permissions,
  cancellation behavior or protected environment names.
- All 217 selected configuration, Legal, deployment, versioning and shared-link
  contracts pass without skips. Version/fragment checks pass; VERSION is 1.1.0.
  Log: `/tmp/pvad-workflow-lock-contracts.log`. The offline image candidates were
  regenerated to retain the new source lock names, without activating anything.
- Read-only GitHub queries found `pxa-staging` and `pxa-production`, both limited
  to protected branches; production has a required reviewer. No secret values
  were requested. The most recent 100 workflow runs returned no non-completed
  runs; this is not an exhaustive drain or an activation approval.
- Existing environment bindings remain unchanged. Actual lock activation needs
  a coordinated legacy-run drain across applicable refs; environment migration
  needs equivalent protection and secret/variable owner review. No remote write,
  dispatch, publication, push or deployment was executed. Prior API/database,
  Grafana and live-host blockers remain open.
- Patch fragment: `pvad-workflow-lock-names`.

## Protected Admin Links And Mail Fallback (2026-10-24)

- Workflow work is committed as `3f8ba8af`. The protected Admin handbook now uses
  the selected Admin/Operator domains for its host instruction and five reference
  links. Dashboard UIDs, local links, CSRF compatibility headers and diagnostics
  are retained. No protected guidance or images moved into public bundles.
- `PvadMailOptions` now matches the approved sender in appsettings and the offline
  profile. Default delivery stays disabled; customer sender, display-name and
  reply-to configuration remain supported. Existing queued mail is untouched.
- All 217 configuration/Legal/deployment/versioning/link contracts and 38 Admin
  tests pass without skips; Admin production build and version/fragment checks
  pass. Logs: `/tmp/pvad-admin-mail-contracts.log`,
  `/tmp/pvad-admin-guide-tests.log`, `/tmp/pvad-admin-guide-build.log`.
- The .NET test command built the API/test dependency graph, with existing
  compiler, unavailable vulnerability-feed and NPOI license warnings. Its first
  test launch was blocked by the sandbox's local socket restriction. An approved
  no-build rerun passes both isolated `PvadMailOptionsTests` (zero skips); it does
  not start WebApplicationFactory, contact PostgreSQL or close the API integration
  startup blocker. No package license acceptance was changed.
- Desktop/mobile production-preview handbook smoke passes using synthetic
  intercepted auth/handbook/image responses. It covers anonymous redirect,
  selected-topic focus, search, image loading and horizontal overflow. Chromium
  needed approved execution outside the sandbox. The temporary preview server
  was stopped. Log: `/tmp/pvad-admin-guide-browser.log` (successful silent exit).
- Tests do not prove actual customer authentication, DNS/TLS, external route
  availability or SMTP delivery. No mail, remote write, push or deployment ran.
  Patch fragment: `pvad-admin-guide-and-mail-defaults`; VERSION remains 1.1.0.

## Monitoring Identity Coverage (2026-10-24)

- Prerequisite Admin/guide/mail changes are committed as `f8f8545e`. Follow-up
  source changes canonicalize four Prometheus jobs, external platform label,
  main/container rule groups, alert service labels and both maintenance interval
  definitions. Scrape/recovery scripts select the matching canonical jobs.
- Readers accept both job/service generations and all heartbeat metric/instance
  prefix combinations. CPU/RAM alerts and dashboard expressions no longer exclude
  canonical Compose services. Three infrastructure-panel queries changed; all
  five dashboards and their stable UIDs/datasources remain present.
- Semantic comparison against the prerequisite commit verifies unchanged alert
  names, timers, severity, environment, descriptions, runbook IDs and dashboard
  references. Alertmanager routing, timing, SMTP/relay configuration and template
  settings differ only in the maintenance interval name and matching references.
- 220 selected Node configuration/Legal/deployment/versioning/link contracts pass
  without skips. Native promtool validates 37 alert rules and 40 dashboard
  expressions, and passes 20 retained fixtures plus nine generated compatibility
  scenarios. The generated scenarios evaluate actual CPU/RAM and exporter-health
  dashboard expressions, exclude unrelated sources, exercise both TLS/job
  generations, test heartbeat firing/recovery in all four prefix combinations and
  verify that fresh current heartbeats coexist with stale old history.
- amtool validates both local and production-example configuration, including
  templates. The existing pinned images run with no network, read-only mounts,
  dropped capabilities and no new privileges. No real SMTP credentials were
  supplied; syntax validation is not live SMTP/webhook delivery evidence.
- CI now runs the identity scenarios and both amtool checks. Local Node log:
  `/tmp/pvad-monitoring-contracts.log`; generated inputs remain ignored under
  `artifacts/rename-pvad/observability/`. Version/fragment validation passes.
- The migration guide explains new series identities, alert fingerprints,
  pending-timer resets, silence/routing review and coordinated rollback. The
  fragment `pvad-monitoring-identity-coverage` declares Major impact rather than
  claiming exact-match external consumers remain compatible. VERSION is 1.1.0.
- No running stack, database, stored measurements, Grafana provider/folder or
  permission was migrated. No failure injection, live delivery, push, deployment
  or remote write ran. Prior API/database, publisher and live-host gates remain.
