# PVAD Repository Naming Follow-up

## Baseline And Scope

Baseline commit: `aa7c18cb`. The tracked-only case-insensitive deep search found
20,917 textual matches in 625 files, 42 matching file paths, and 153 raw byte
matches in 38 binary assets. These are inventory totals, not outstanding defects.
All 274 tracked Markdown files were clear. Private IDE files, untracked notes,
symlinks, ignored outputs and Git history are outside the scan.

Technical names use PVAD; product and Designer names use PVADE. Preserve exact
compatibility contracts and original evidence. Repository-wide zero matches are
not an acceptance criterion. Earlier inventories and release gates remain historical.

## P0 - Current Outputs

- [x] Normalize freely chosen synthetic users, labels, test passwords and runtime-generated certificate subjects together with their consumers.
- [x] Preserve explicit legacy inputs, stable diagnostic expectations and signed original fixtures.
- [x] Write the canonical chart-metadata recognition source and accept the previous value in both backend and Designer normalizers.
- [x] Keep typed recognition and its style projection consistent, without changing chart geometry or confidence.
- [x] Keep project audit event keys and existing format IDs stable; add canonicalFormatId to newly written audit details.
- [x] Leave historical audit rows untouched and document old/new field semantics.

## P1 - Search And Compatibility Boundaries

- [x] Restrict the residual scanner to tracked regular files, excluding private paths and symlinks.
- [x] Scan known source formats despite embedded null bytes and cover the ten baseline matches in the invoice attachment test.
- [x] Keep binary assets outside the textual guard without silently excluding source files.
- [x] Refresh only explicitly reviewed residual hashes and preserve unknown/changed/stale finding failures.
- [x] Keep authentication schemes, operator identifiers, monitoring bindings, wire diagnostics, cookies, headers and legacy routes unchanged.
- [x] Preserve prior Markdown/JSON audit bytes and historical checklist states.

## P1 - Validation And Delivery

- [x] Test mixed-case content and filenames, null-byte source, private/untracked paths, symlinks and binary exclusion.
- [x] Test legacy and canonical chart inputs, immutable source drafts and canonical roundtrip outputs.
- [x] Test unchanged audit actions and legacy fields, additive canonical fields and unchanged existing rows.
- [x] Pass focused .NET, Designer, naming and documentation tests plus typecheck and production builds.
- [x] Pass the Markdown naming gate, structural JSON guard and git diff --check.
- [x] Add a customer-facing Patch fragment without changing VERSION, published releases or live databases.

## Implementation Notes

The [current output guide](../docs/pvad-current-output-naming.md) describes the
canonical chart source and additive audit details. The public chart and native
project guides document the same behavior. Earlier embedded chart hashes are
validated before the recognition source is normalized; modified series remain
rejected. Original metadata bytes are never rewritten during validation.

Synthetic account and runtime signer names are updated with their expectations.
Explicit customer-content preservation tests, compatibility fixtures and the
visual-baseline fixture producer retain their original inputs. In particular,
baseline-bound visible signature subjects are not rebranded by this source batch.

The invoice attachment source keeps its one literal null byte. The ten baseline
matches are now covered by a regression test; the runtime certificate subject is
renamed while stable diagnostic expectations remain intact.

## Verified Results - Original Source Follow-up

These results describe the source batch committed as `f56ffd88`, before the
authentication source transition below. Its unchanged-identity statements and
test counts are historical checkpoint evidence, not claims about later changes.

- Node contract suites: 514 passed, none skipped.
- Designer: 92 suites, 812 tests and one snapshot passed. Typecheck and production build passed.
- Core: 152 passed; Importer: 646 passed; PDF Infrastructure: 57 passed, none skipped.
- Export: 221 passed, none skipped. The five .NET suites total 2,333 passing tests.
- API: 1,257 passed, 80 skipped, no failures. This does not certify the skipped integration scenarios or close external release gates.
- Documentation production build passed, including the updated public chart and project-format guides.
- Structural JSON guard: no unclassified findings. Prior audit bytes and their hash pins remain unchanged.
- Strict Markdown gate: zero matches in tracked files; the two new Markdown files are additionally checked before tracking.
- Authentication/session and monitoring identities, original PDF/certificate assets, Legal records, VERSION and published releases remain unchanged.
- Existing compiler, obsolete-API, native-library-license and bundler warnings are not resolved by this naming follow-up.

## Completion And Remaining Boundaries - Original Source Follow-up

The source follow-up is complete. Thirty-nine synthetic test-source files were
renamed together with their expectations; chart and audit regression tests are
additional coverage. The same tracked-text scan now finds 20,704 matches in 624
files, versus 20,917 in 625 baseline files. Counts can increase when explicit
legacy tests are added; raw match reduction is not a compatibility acceptance.

The two deferred operations below remain open. Old wire diagnostics, legacy
readers, historical inventories, original signed assets and baseline-bound test
inputs intentionally retain their exact identities. No release, migration or
deployment is certified by this checklist. The original batch is committed as
`f56ffd88`; the subsequent authentication work has not been committed or pushed.

## Deferred Operations

### Authentication Source Transition

- [x] Use canonical authentication schemes for new combined, API-key, MCP and Designer authentication.
- [x] Forward historical scheme names to the canonical handlers for the compatibility window.
- [x] Read earlier Designer ticket protection purposes while writing only the current purpose.
- [x] Renew migrated tickets only after persistent user, stamp, organization and session checks succeed.
- [x] Keep Account/Designer isolation, host-only cookies, expiry, CSRF and sign-out behavior enforced.
- [x] Test old/new tickets, conflicts, tampering, expiry, revocation, blocked users, restart and sign-out.
- [x] Preserve the existing keyring discriminator, stored session IDs and encrypted payload purposes.
- [x] Document the source transition and its deployment/rollback boundaries; validate and add a Patch fragment.

#### Authentication Validation - 2026-11-04

All eight source tasks above are complete. This is not a production cutover.

- Focused scheme, cookie and persistent-session transition tests: 28 passed, none skipped.
- Full API suite after the final changes: 1,274 passed, 80 skipped, no failures; the skipped tests are not accepted as verified integration coverage.
- Designer authentication regression: 17 passed; full Designer suite: 812 passed in 92 suites, one snapshot passed.
- Node configuration, Legal, fidelity, release-author and website contract suites: 514 passed, none skipped.
- Designer typecheck, Designer build and Documentation build passed; existing compiler, license and bundler warnings remain.
- Structural JSON guard: zero unclassified findings; Markdown guard: zero matches across 276 tracked Markdown files.
- Residual review remains exact-file/hash bounded. Only four reviewed entries were refreshed; original audit records and compatibility-register bytes remain unchanged.
- Product version, published releases, Legal manifest, live databases and keyring configuration are unchanged; a separate Patch fragment records this source change.

The [identity guide](../docs/pvad-identity-compatibility.md#browser-cookie-transition)
explains canonical writes, the historical ticket reader and rollback boundaries.
The new HTTP tests use real middleware and validation events with an isolated
EF InMemory store, not a PostgreSQL login/handoff acceptance run. Local keyring
restart tests use synthetic keys. No deployed session is migrated or certified.

### Monitoring And Operator Source Preparation

- [x] Extend the existing offline Grafana preparation with a deterministic source inventory.
- [x] Validate unique dashboard and datasource identities and their cross-references.
- [x] Resolve alert dashboard links and preserve runbook identifiers without rewriting them.
- [x] Bind provisioning, rules and operator integration sources to exact checksums.
- [x] Reject broken references, duplicate identities, active datasource deletion and unexpected provisioning changes.
- [x] Keep generated output separate from active configuration and deny all apply/deployment arguments.
- [x] Test the inventory, failure cases and unchanged active source bytes.
- [x] Document remaining live backup, permission, placement, alert-delivery and rollback gates; add a separate release fragment.

#### Monitoring Preparation Validation - 2026-11-04

All eight offline preparation tasks are complete; neither deployment gate below
is closed by this work.

- The generated inventory covers five dashboards, three datasources, 37 alerts and 17 exact source checksums.
- All 34 configured alert dashboard links resolve; the three MCP alerts without dashboard/runbook annotations remain explicitly represented as missing, not newly documented.
- Seven Grafana preparation tests pass, including broken references, duplicate identities, active datasource deletion, output checksum binding and unchanged source bytes.
- All 19 focused Grafana, telemetry and operator-guide tests pass; the broader Node contract suite passes 518 tests with none skipped.
- The active provisioning, dashboard files, datasource configuration, alert rules and operator integration sources compare byte-for-byte with HEAD.
- The residual guard passes with one additional, explicitly reviewed tool hash; the historical audit and Legal register bytes remain unchanged. Markdown has zero previous-name matches and JSON has zero unclassified findings.
- A separate Patch fragment records this preparation; VERSION and the published release manifest are unchanged.

See the [Grafana cutover guide](../docs/pvad-grafana-provider-cutover.md) for
report generation and the remaining backup, folder-permission, identity,
placement, live-delivery and rollback checks. Generated artifacts are ignored
local source evidence only; no live state was inspected or modified.

### Isolated Grafana Rehearsal

- [x] Use the cached, Compose-selected Grafana image by immutable local ID with no pulls, network or published ports.
- [x] Create only uniquely labeled synthetic containers, volumes and temporary configuration.
- [x] Verify allow/deny folder access and all managed dashboard/datasource identities before cutover.
- [x] Reject target-folder collisions and rehearse an in-place rename with the original folder UID.
- [x] Stop the writer before taking a checksummed full data-directory backup.
- [x] Restore into a different synthetic volume and verify placement, permissions and point-in-time state.
- [x] Test ownership-checked cleanup and failure paths without accepting external resource names.
- [x] Record the real isolated result and its limits; leave deployment, production recovery and gateway acceptance open.

#### Isolated Rehearsal Result - 2026-11-04

All eight synthetic rehearsal tasks are complete. The final real Docker run
passes with the cached `grafana/grafana:13.1.0` image selected by Compose.

- All five dashboards, three datasource UIDs and the captured folder UID survive the in-place rename and pinned-provider restart.
- The explicit folder ACL and tested Viewer allow/deny behavior remain unchanged before cutover, after cutover and after restoration.
- A conflicting target folder is rejected. A modified archive is rejected before extraction, leaving the restore target untouched.
- The writer stops cleanly before the full data-directory backup. Restoration uses a separate synthetic volume and matching archive checksum.
- Baseline and restored state hashes match. The post-backup annotation is absent after restore, proving point-in-time recovery rather than later-write replay.
- Ownership checks confirm temporary containers and all three synthetic volumes are removed. Temporary source copies are also removed.
- Eight unit/contract tests cover path constraints, collision handling, cached-image requirements, integrity-first restore, startup failure cleanup, lost volume-create replies, foreign-owner rejection and CLI argument refusal.
- The broader Node configuration, Legal, fidelity, release and website suites pass 526 tests with none skipped. Naming guards and git diff --check pass.
- The result is recorded in ignored local `artifacts/rename/grafana-rehearsal/report.json`; no credentials or operational database snapshot are exported.

This validates the same-image SQLite lab strategy only. Basic authentication
is confined to synthetic accounts; the PVADE operator gateway, production SSO,
customer/team ACLs, actual customer backup recovery, datasource connectivity,
alert delivery and deployment approval remain separate acceptance work. Existing
configuration, live data, VERSION and published releases are unchanged. The
release fragment has impact `none` because this adds internal validation only.

### Isolated Operator Gateway Rehearsal

- [x] Exercise the actual Nginx template and environment adapter with cached immutable images and synthetic upstreams.
- [x] Use only a new labeled internal network, unpublished containers and read-only fixture mounts.
- [x] Verify protected Grafana, documentation and UI routes reject anonymous, forbidden and expired sessions.
- [x] Verify forged identity/API-key headers cannot replace the authoritative auth decision.
- [x] Verify upstream identity replacement, credential stripping, cookie-path rewriting and no-store responses.
- [x] Verify internal auth-route hiding, public login/health routes and reauthorization after an allowed request.
- [x] Verify auth-service failure denies access and recovery restores authorized access; check ownership-safe cleanup.
- [x] Record real HTTP results and contract tests, document synthetic-auth limits and preserve production gates.

#### Gateway Rehearsal Result - 2026-11-04

All eight gateway-rehearsal tasks are complete within the synthetic HTTP scope.

- The real cached Nginx image and Node fixture image pass all 14 HTTP scenarios; the final run observes 23 auth subrequests.
- Anonymous, forbidden, disabled and expired mock decisions cannot reach protected Grafana/UI upstreams; the internal auth location is not publicly callable.
- Client identity/API-key headers cannot replace the mock API decision. Grafana receives only the approved identity and no browser Authorization or Cookie header.
- The auth subrequest strips Authorization and both credential-header generations. The ordinary API route strips proxy identity headers while preserving Authorization for API-side authentication.
- Grafana cookie paths, protected documentation content and gateway-originated security/no-store headers pass. The fixtures do not supply the cache header being tested.
- Auth errors, redirects and an actually stopped mock auth listener deny access; restarting that listener restores the allowed flow. A prior allowed request does not create a cached grant.
- The two containers and internal test network are removed after ownership verification. No host ports, existing networks, operational volumes or installed services are used.
- Seven unit/contract tests cover exact retained protocol aliases, restricted client targets, cached images, failed starts, foreign ownership, lost network-create replies and CLI refusal of deployment arguments.
- The broader Node suites pass 533 tests with none skipped. Naming guards, release validation and git diff --check pass.
- The ignored `artifacts/rename/operator-gateway-rehearsal/report.json` pins eight unchanged integration sources, two harness files and both local image IDs, without storing credentials or document bodies.

The [gateway guide](../docs/pvad-grafana-provider-cutover.md#isolated-gateway-http-rehearsal)
describes reproduction and boundaries. Identity login, PostgreSQL session state,
CSRF/PKCE, browser behavior, real Grafana auth proxy, TLS/CDN, SSO, production
permissions and operational release approval remain unverified by this mock run.
The active gateway and its retained response-header reader are unchanged. The
release fragment has impact `none`; no production gate or historical total is
closed by this supporting evidence.

### Real PostgreSQL Authentication Rehearsal

- [x] Run real Account login, CSRF, PKCE handoff and session validation against disposable PostgreSQL containers.
- [x] Verify real API operator authorization with an allowed system operator and a denied organization administrator.
- [x] Reuse only an isolated file-backed keyring and the same synthetic database across API host recreation.
- [x] Verify historical Designer ticket renewal under both supported cookie names without replacing the persistent session.
- [x] Verify current ticket protection, Account isolation and no repeated migration renewal.
- [x] Verify CSRF rejection, successful sign-out, persistent revocation and replay denial after another host restart.
- [x] Record actual passed, failed and skipped counts and distinguish TestServer HTTP from browser or production acceptance.
- [x] Validate naming/release contracts, document reproduction and preserve separate deployment gates.

#### PostgreSQL Authentication Result - 2026-11-04

All eight local rehearsal tasks are complete. The final opt-in group passes
17 tests with none failed or skipped: 15 PostgreSQL-backed cases and two
non-container access/authorization-contract checks.

- Actual Account login, CSRF, PKCE handoff and persistent Designer authorization run through the application controllers and Identity services, not a mock authenticator.
- Two added cases cover a historical Designer ticket under either supported cookie name. Each disposes and recreates the API host against the same disposable database and isolated file-backed keyring, then repeats host recreation after logout.
- Renewal writes only the current protection purpose and retains the same session ID. A subsequent valid request does not repeat migration renewal, and Designer-ticket substitution into the Account cookie cannot grant access.
- Invalid CSRF cannot terminate the session. Valid logout removes both cookie-name generations, persists revocation and its audit event, and makes both the old and renewed ticket unusable after another restart.
- Real operator authorization denies an organization administrator and allows the explicitly configured system operator. Current and retained operator-response headers carry the same identity.
- The initial environment attempt failed before PostgreSQL startup because the reaper was given the host's Rancher socket path. The documented VM-side socket override fixes the test setup without disabling cleanup.
- An existing runbook assertion still expected the old safety phrase inline. It now requires the exact protected-register reference. Neither the register nor the restore tool's actual confirmation contract changed.
- The initial restart probe also caught late test-host key-directory configuration. Explicit service registration now ensures the restart cases really write and reuse their temporary XML keyrings; other hosts in this test group use ephemeral providers.
- The separate focused cookie/scheme/session suite passes 26 tests with none skipped. The broader Node suite passes 533 tests, and the Documentation production build succeeds.
- Naming guards, release checks and git diff --check pass. Historical review notes and ledger entry order remain unchanged; both original audits, both compatibility registers, the Legal manifest, VERSION and published releases match HEAD byte-for-byte.
- PostgreSQL containers are disposed, no Testcontainers resources remain running after the final database run, and the restart-test keyrings are deleted. No installed service is restarted and no existing database is used.
- Local TRX evidence is ignored under artifacts/rename/postgres-auth/real-postgres-auth.trx and cookie-transition.trx. The new release fragment has impact none; VERSION remains 1.1.0.

Reproduction and limits are documented in the
[identity guide](../docs/pvad-identity-compatibility.md#isolated-postgresql-login-and-restart-rehearsal).
This is real API/Identity/PostgreSQL behavior over TestServer HTTP, not browser
or TLS acceptance. It does not combine the real API with the separate Nginx
and Grafana labs. Real-host cookie isolation, SSO, operational backup recovery,
independent review and production cutover remain separate gates. Historical
primary acceptance totals are unchanged; the existing dependency-license warning
is not treated as legal approval by this test run.

### Separate Deployment Gates

- [ ] Coordinate authentication/session identifier cutover separately.
- [ ] Coordinate operator and monitoring identity cutover with backup and rollback separately.

These deferred operations do not block this source follow-up. No commit, push,
deployment or restart of installed services is authorized by this checklist.
