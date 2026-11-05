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

### Combined Operator Browser Rehearsal

- [x] Start only owned, disposable PostgreSQL, Nginx and Grafana resources and an isolated real API process.
- [x] Build and serve the real Admin frontend without replacing an installed application's build output.
- [x] Exercise browser login, CSRF and protected documentation with actual persistent Identity sessions.
- [x] Verify the real Grafana auth proxy receives the authoritative operator identity and lists the provisioned dashboards.
- [x] Reject anonymous requests, forged headers and a real organization administrator without operator access.
- [x] Verify persistent expiry/revocation, logout replay denial and session continuity across API restart.
- [x] Capture synthetic desktop/mobile screenshots and bounded reports without credentials or browser storage snapshots.
- [x] Test failure cleanup and contracts, document exact limits and preserve production acceptance gates.

#### Combined Browser Result - 2026-11-04

All eight combined local rehearsal tasks are complete. The final real Chromium
run passes all eight browser scenarios through Kestrel, PostgreSQL, the real
Admin UI, Nginx and Grafana, with no authorization/API response mocks.

- Anonymous requests, forged headers and a real Organization Administrator cannot enter operator routes. The explicitly allowlisted System Administrator signs in through the actual Admin form.
- The gateway serves both protected runbooks. Grafana's actual proxy user matches the API-authorized identity, and all five provisioned dashboard titles appear in its real folder listing.
- API outage denies protected access; restarting only the owned API restores the existing persistent session using the same private keyring and database.
- Invalid CSRF cannot sign out the session. Valid logout persists revocation; replaying the saved pre-logout cookies cannot reopen runbooks or Grafana. Expiring a new session in the disposable database also denies access.
- Six screenshots cover login, dashboard, runbooks and Grafana, with desktop 1440x980 and mobile 390x844 captures. The Grafana and mobile runbook screenshots were visually inspected; query/data-source rendering is not asserted.
- The report pins 29 unchanged-during-execution integration sources, both runtime harness files, the actual API binary and three cached immutable image IDs. It records the source commit plus uncommitted-change state without exporting credentials, session cookies or browser storage.
- Only the new gateway and PostgreSQL expose loopback ports. Grafana accepts proxy identity only from the new gateway's private IP; protected upstreams have no published ports. Owned processes, four containers, the new network, temporary keyrings, schema/build output and database data are removed.
- Initial setup attempts exposed a PostgreSQL temporary-init readiness race and structured API startup events; final TCP readiness and a parsed owned-listener event now have explicit regression coverage.
- The real browser also found a genuine HTTP 502 login defect: the complete bootstrapped operator permission catalog expanded the first cookie beyond proxy response-header limits. Sign-in and validated renewal now omit derived claims and their resolution marker while retaining identity, security stamp, roles, active organization and session. Authorization is still resolved on the server; no proxy/cookie limit or permission check was weakened.
- Two new Account/Designer regression cases use thousands of synthetic claims and require compact tickets. All 28 focused cookie/session tests pass. The operator PostgreSQL fixture now includes the real complete built-in permission claims.
- Full API regression: 1,276 passed, 82 opt-in cases skipped, zero failed. The separately enabled PostgreSQL auth group passes 17 tests with none skipped, including 15 database-backed cases and two non-container checks. Other opt-in groups are not certified by this run.
- All 543 Node contracts pass. API and staged Admin production builds, Documentation build, naming guards, release validation and git diff --check pass. Existing dependency-license and future Vite-loader warnings remain separate and are not treated as production approval.
- A separate Patch fragment records the cookie correction; the internal browser harness has impact none. VERSION and published release history remain unchanged. Both original audits, both compatibility registers and the Legal manifest are preserved byte-for-byte.

The ignored report is artifacts/rename/operator-browser-rehearsal/report.json;
the [guide](../docs/pvad-grafana-provider-cutover.md#combined-operator-browser-rehearsal)
documents reproduction and the discovered initial-sign-in regression.
This proves local HTTP/development-cookie behavior only. The retained Grafana
folder/tags and identity contracts remain unchanged; the lab is not a live
identity cutover. Production TLS/CDN, SSO, public-host isolation, actual monitoring
query/alert delivery, customer backup recovery and independent deployment review
remain separate. Historical primary acceptance totals are not changed by these
supporting checks.

### Integrated Monitoring And Operator Closure

- [x] Run real Collector, Prometheus, Loki and Tempo against the isolated API with synthetic traffic only.
- [x] Query actual metrics, correlated logs and traces through the authenticated Grafana datasource proxy.
- [x] Exercise real Prometheus firing/recovery through Alertmanager and the unchanged signed HTTPS webhook relay.
- [x] Verify receiver-side signature, timestamp and idempotency checks, including a failed delivery and retry.
- [x] Rename the disposable Grafana folder/provider without changing dashboard/datasource UIDs or folder permissions.
- [x] Back up stopped Grafana, reject a corrupt archive and restore the original state behind the real operator gateway.
- [x] Verify browser access and unauthorized denial before cutover, after cutover and after restore.
- [x] Generate one checksum-bound report and screenshots, verify owned-resource cleanup and document remaining deployment limits.

This local integration extends the combined browser rehearsal. It never reloads
an installed monitoring stack, delivers an external notification or authorizes
production activation. Historical acceptance totals remain unchanged.

#### Integrated Monitoring Result - 2026-11-04

All eight local integration tasks are complete. The final real run passes all
12 browser/monitoring scenarios with no API/authorization response mocks.

- The actual API exports through Collector to Prometheus, Loki and Tempo. Authenticated Grafana proxy queries prove a version-route counter increase plus two log records and the same request's trace. The synthetic sensitive-query canary is absent from returned telemetry and API console logs.
- Stopping the owned Collector fires the real telemetry outage expression. The unchanged relay delivers to a private HTTPS receiver with certificate validation. Three delivery attempts prove one deliberate HTTP 503, a successful same-payload retry and a signed resolved notification. HMAC, timestamp and idempotency checks pass; no invalid delivery is accepted.
- The lab now assigns a fixed, validated loopback ingestion binding and verifies it survives Collector restart. Fresh API traffic must reach the live datasource again after rename and restore; old samples alone are not the recovery assertion.
- The candidate changes only disposable Grafana provider/folder display labels and pins the existing folder UID. All five dashboards, all three datasource UIDs, folder permissions and operator identity remain intact. Anonymous visitors and the real Organization Administrator are denied before rename, after rename and after restore.
- Grafana stops cleanly before the checksum-bound backup. A modified archive is rejected. Restored baseline/content/permission hashes are equal, and an annotation created after backup is absent after restore. Real metric queries and desktop/mobile browser access work again.
- Twelve screenshots cover the existing login/runbook flow and all three Grafana phases. The renamed mobile and restored desktop/mobile captures were visually inspected. These are synthetic UI snapshots, not approved production visual baselines.
- The report pins 42 integration sources, eight harness/helper sources, ten immutable cached image IDs and both actual API/relay binaries. All 11 owned service containers, temporary helper containers, both volumes, the dedicated network and temporary data/keys/build output were removed; independent Docker listings confirm no labeled resources remain.
- Five new privacy tests first reproduced query-string values leaking into diagnostic console logs. The shared sensitive-attribute policy now removes normalized query-string variants while preserving correlation. All 62 focused API tests and five relay tests pass. The owner approved the exact public Security/Patch summary; the rehearsal itself has impact none.
- Full API regression passes 1,281 tests with 82 opt-in cases skipped and zero failures. The real PostgreSQL browser flow does not certify those skipped groups. All 556 Node/website contracts pass, including public-output exclusion of the operator guides. Documentation production build, naming checks and release validation also pass.
- Early setup attempts exposed a gateway-address collision and a missing stable ingestion binding in the expanded test topology. They are corrected in the harness; no installed network/proxy policy was changed. Final source and binary pins match the successful run.

Reproduce with node tools/configuration/monitoring-operator-rehearsal.mjs.
The ignored report is artifacts/rename/monitoring-operator-rehearsal/report.json;
the [integrated guide](../docs/pvad-integrated-monitoring-rehearsal.md) explains
configuration overrides, evidence and limits. VERSION and published releases,
original audits, compatibility registers and the Legal manifest are unchanged.
Production TLS/CDN/SSO, real notification destinations, complete production
exporter/panel coverage, retained-history/silence migration, customer recovery and
independent rollout approval remain separate deployment gates. Primary historical
acceptance remains 90/98; supporting test tasks do not inflate that percentage.

### Isolated HTTPS Authentication And Recovery

- [x] Serve actual Account, Designer and Admin production builds on separate HTTPS test hosts with a temporary private CA.
- [x] Keep certificate/hostname validation enabled and reject untrusted and wrong-host certificates without modifying the host trust store.
- [x] Confirm actual Production startup remains blocked by unapproved retention governance; exercise real login, browser PKCE, Designer persistence and Admin login in Staging with production cookie/OAuth rules and PostgreSQL.
- [x] Verify Secure host-only cookies, cross-host isolation, Account/Designer ticket isolation, CSRF, handoff replay and safe redirects.
- [x] Renew actual validated legacy session/Designer tickets without creating replacement sessions or changing the retained keyring discriminator.
- [x] Verify session continuity across restart, reject the wrong keyring and restore a stopped synthetic database/keyring snapshot.
- [x] Verify persisted logout/replay denial, expiry, disabled users, suspended organizations and entitlement denial.
- [x] Produce source/binary/image-pinned reports and synthetic desktop/mobile screenshots, remove owned resources and document deployment limits.

Verified on 2026-11-05: **8/8 supporting tasks complete**. The real HTTPS runner
passed **12/12 scenarios**, with six checksummed screenshots and 30 source pins.
The actual Production process exited with controlled code 78 before listening;
positive flows ran in Staging with Secure cookies and real signing certificates.
The stopped PostgreSQL/keyring restore preserved existing and renewed tickets
and read the exact protected mail/cache ciphertext saved before backup.

Additional validation: **58/58 focused API tests**, **344/344 configuration,
naming and release contract tests**, **60/60 Documentation tests**, Account,
Designer, Admin and Documentation builds, version/fragment checks and naming
guards passed. The fragment has `none` impact: only internal validation tooling
and guidance changed. Product version and published release history are unchanged.

Evidence: ignored `artifacts/rename/https-authentication/report.json` and its
`browser/` screenshots. The Designer phone-size image records the existing
Desktop required screen, not a claim of phone editing support. The browser is
isolated Linux Chromium, not native macOS Chrome acceptance. All owned test
resources and temporary credentials, dumps and private keys were removed.
Reproduction and security limits are in the
[HTTPS authentication rehearsal guide](../docs/pvad-https-authentication-rehearsal.md).

These are local supporting checks, not public-host, SSO, customer recovery or
production rollout approval. No installed store, certificate trust, DNS record,
published release, primary acceptance total or production credential is changed.

### Final Acceptance Closeout

- [x] Map the eight original open gates to their primary checklist identities, concrete evidence and accountable roles.
- [x] Recount only primary acceptance tasks and reject denominator drift or duplicate/unclassified tasks.
- [x] Record fresh GET-only GitHub branch/protection/environment/open-PR observations without changing remote controls.
- [x] Verify current local source/screenshot evidence, explicitly classify stale reports and repeat the affected monitoring rehearsal.
- [x] Validate internal-guide exclusion, links, naming guards, release metadata and the final diff without closing production gates.

Verified on 2026-11-05: **5/5 supporting closeout tasks complete**. The integrated
monitoring rehearsal was repeated after a helper pin changed and passed all 12
scenarios with verified cleanup. The earlier HTTPS result remains hash-valid for
30 sources and six screenshots; its browser flow was not rerun in this closeout.
All 356 configuration/naming/release contracts, 60 Documentation tests and the
Documentation build pass. The 12 new acceptance contracts are a subset of 356,
not an additional suite total. Naming/version/fragment and whitespace checks pass.
Only the two reviewed, additively updated historical guide hashes were refreshed;
their original checkbox states and results remain unchanged. No full Solution/API,
native OS or production acceptance was re-executed. The eight primary gates remain
open: 90/98 (91.8%) complete, not 95% or 100% release approval.

The [final acceptance guide](../docs/pvad-rename-final-acceptance.md) is the
consolidated exit matrix. The offline report is generated under
artifacts/rename/final-acceptance/report.json. Completing this supporting review
does not alter the historical 90/98 primary total or authorize publication.

### Separate Deployment Gates

- [ ] Coordinate authentication/session identifier cutover separately.
- [ ] Coordinate operator and monitoring identity cutover with backup and rollback separately.

These deferred operations do not block this source follow-up. No commit, push,
deployment or restart of installed services is authorized by this checklist.
