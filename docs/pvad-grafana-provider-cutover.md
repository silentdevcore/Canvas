# Grafana Provider Rename Preparation

Fresh local installations use the PVAD provider and folder from
`deploy/observability/grafana/profiles/current/dashboards.yml`. The default Compose
model mounts this file separately from the shared datasource directory. The
original provider in `deploy/observability/grafana/provisioning/dashboards/`
remains byte-for-byte unchanged for existing installations, including existing
bind mounts of the whole original provisioning directory.

The minimal `deploy/rename/docker-compose.grafana-legacy.yml` overlay explicitly
selects that original provider without changing other resolved Compose settings.
The complete legacy overlay also retains the original provider. Review either
with the installed project/storage bindings; do not activate fresh defaults as an
upgrade. Existing folder placement and permissions are persisted authorization
boundaries, not just display labels. Dashboard and datasource UIDs stay stable.
These repository changes do not migrate or restart a running installation.

## Offline Candidate

```bash
node tools/configuration/rename-grafana-provider.mjs
node tools/configuration/rename-grafana-provider.mjs --legacy
node --test tools/configuration/rename-grafana-provider.test.mjs
```

The generator writes only ignored files under
`artifacts/rename/grafana-provider/current/` or, with `--legacy`,
`artifacts/rename/grafana-provider/legacy/`. The profiles cannot overwrite each
other's evidence; older reports at the parent path remain historical.

- `dashboards.candidate.yml`: canonical PVAD provider and folder labels.
- `dashboards.original.yml`: exact selected provisioning bytes.
- `report.json`: source checksum, preserved boundaries and outstanding gates.
- `inventory.json`: dashboard/datasource identities, cross-references, alert
  dashboard and runbook references, and exact checksums for both provider profiles
  and the shared provisioning, dashboard, rule and operator-integration sources.

The default profile is already canonical: regeneration reports no changes or
blockers and `migrationRequired: false`. The explicit legacy profile reports
the two label changes and the existing four installed-state cutover blockers.
Both profiles are validated against their declared identity before generation;
mixed names, swapped profiles and unreviewed policies fail closed.

The report pins the inventory and candidate bytes with separate SHA-256 values.
Preparation fails if the provisioning source changes during generation. The
inventory is deterministic for the same source bytes and exports only selected
metadata, not datasource connection settings, credential fields or queries.

The source checks reject duplicate identities, unresolved nested datasource
references, alert links to unknown dashboards, active datasource deletion and
datasources outside the expected organization. Unknown provisioning policies
still require explicit review. Variable-based or built-in datasource references,
recording rules and additional organizations are not supported by this bounded
inventory; do not weaken validation to activate an unreviewed configuration.

The current inventory covers five dashboards, three datasources and 37 alerts.
All 34 existing dashboard links resolve and their runbook identifiers are retained.
The three MCP alerts currently have neither dashboard nor runbook annotations;
these fields are recorded as null, not represented as verified documentation.
This reference check does not evaluate PromQL/LogQL or validate runbook contents.
Use the separate [monitoring validation](pvad-monitoring-identity-migration.md#offline-validation)
for metric/rule semantics.

The generator does not alter provisioning, Compose mounts, datasource configuration,
dashboards, databases or volumes. Source drift, extra providers, explicit folder
UIDs or changed provisioning policies require manual review. There is no apply
command. The report always records `deploymentAuthorized: false`.
The inventory separately records `liveStateInspected: false`: source identities
and checksums are not an inventory of installed folders, users or permissions.

## Before Activation

This section applies only to a separately authorized transition of an existing
installation. These operational steps are not prerequisites for completing the
repository rename or validating a fresh synthetic installation.

1. Identify the Grafana instance, organization and storage backend. Inventory
   dashboards by UID, current folder UID, folder permissions, direct dashboard
   permissions, teams, service accounts and saved links. Use metadata only; do not
   export credentials into the repository.
2. Stop concurrent writers and capture an approved, consistent Grafana database
   backup plus provisioning and plugin configuration. Demonstrate restoration in
   an isolated instance. A provisioning file is not a database backup.
3. Decide explicitly whether to rename an existing folder in place or move
   dashboards into a new folder. The generated candidate alone does neither
   safely: folder names do not prove folder identity or equivalent permissions.
   Resolve any existing PVAD folder collision before proceeding.
4. In staging, test the chosen placement strategy with the pinned Grafana image.
   Check provisioning logs, duplicate or stale dashboards, UIDs, saved links,
   datasource queries, operator access and denied non-operator access. Compare
   effective permissions before and after; do not inherit broader access.
5. Record operator approval and a rollback window before changing the active
   source or mounting a candidate. Do not run competing provisioning generations
   against the same database.
6. Rehearse rollback of both provisioning and persisted state. Merely restoring
   the original YAML does not restore moved dashboards or folder permissions;
   `disableDeletion` is not a rollback mechanism. Verify access and dashboard
   inventory again after restoration.

Regenerate the candidate immediately before an approved rehearsal and compare
its recorded source checksums with the reviewed files. A changed hash requires
review and regeneration; passing a source inventory does not waive any step
above. Do not store live database dumps, credentials or operator identities in
the generated source inventory. Keep backups in the protected operator store.

The offline tests do not establish live cutover, permission migration, restore
or production approval. The isolated rehearsal below tests a narrower synthetic
case, not installed customer state. The local development owner remains
responsible for approving and exercising deployment-specific steps.

## Isolated SQLite Rehearsal

```bash
node --test tools/configuration/grafana-cutover-smoke.test.mjs
node tools/configuration/grafana-cutover-smoke.mjs
```

The runner requires the Compose-selected Grafana image to be cached locally and
uses its immutable local image ID. It never pulls images, accepts external
resource names, opens ports or joins an existing network. Uniquely labeled
containers and volumes hold only synthetic identities and copies of the five
managed dashboard definitions. Existing provisioning, databases and volumes are
never mounted. Temporary credentials travel through environment/stdin, not the
command line or result report.

The rehearsal creates two synthetic Viewer accounts and grants only one explicit
folder access. It checks all dashboard reads for allow/deny behavior, folder ACLs,
datasource IDs and dashboard content. It rejects a target-folder collision,
stops the writer, archives the full SQLite data directory and verifies its hash.
A deliberately damaged archive must fail integrity validation before extraction.

The chosen lab strategy renames the original folder in place, then restarts with
the canonical provider candidate pinned to the captured folder UID. This extra
UID pin exists only in temporary lab configuration: it is not silently added to
the offline candidate or active provisioning. The runner checks that no duplicate
folder remains and that content, UID and tested permissions are unchanged.

Restoration uses another synthetic volume and the original provisioning. The
restored folder, ACLs, dashboards and datasources must equal the pre-cutover
snapshot. A post-backup annotation must be absent after restore: this explicitly
tests point-in-time recovery, not automatic preservation of later writes.

After restoration, a separate fresh synthetic volume starts with the actual
canonical provider file. It must contain exactly one PVAD folder, the same five
dashboard identities and three datasource identities, and reject invalid
authentication. The old provider, shared datasources and managed dashboards
remain unchanged in the repository.

The result is written to `artifacts/rename/grafana-profiles-rehearsal/report.json` with
the image ID, source-inventory hash, backup hash, matching baseline/restore
hashes, both provider hashes, fresh-install checks, scope limits and
ownership-checked cleanup result. Earlier reports at
`artifacts/rename/grafana-rehearsal/report.json` remain unchanged historical evidence.
A new run first marks
the result as running and records failure if it fails; an older passing report
is not presented as the latest result. Synthetic backup volumes and temporary
configuration are removed after the run, so the report is evidence of a drill,
not a retained operational backup.

Handled failures also attempt ownership-checked cleanup. Forced process or
Docker-daemon termination can still leave labeled synthetic resources; inspect
that run's ownership label and never use broad volume/container pruning.

This uses synthetic Basic authentication only. It does not certify the PVADE
operator gateway, proxy authentication, SSO, production team/service-account ACLs,
external datasource connectivity, alert delivery, other database backends or a
different Grafana version. It authorizes no production deployment. The actual
SQLite stop-before-copy requirement follows the
[Grafana backup guidance](https://grafana.com/docs/grafana/latest/administration/back-up-grafana/).
The pinned lab image still supports the
[legacy Folder Permissions API](https://grafana.com/docs/grafana/latest/developer-resources/api-reference/http-api/api-legacy/folder_permissions/);
this is a test harness dependency, not a new PVADE API contract.

## Isolated Gateway HTTP Rehearsal

```bash
node --test tools/configuration/operator-gateway-rehearsal.test.mjs
node tools/configuration/operator-gateway-rehearsal.mjs
```

This complements the Grafana database drill. It runs the actual operator Nginx
template, environment adapter and protected documentation assets against small
synthetic API, UI and Grafana HTTP upstreams. The Compose-selected Nginx image
and pinned Node test image must already be cached; execution uses their immutable
IDs with pulls disabled. Only a new labeled internal Docker network and two
temporary containers are created. There are no published host ports or joins to
installed networks, and no existing container is restarted.

The 14 scenarios verify public entry routes, the hidden internal auth location,
anonymous/forbidden/disabled/expired decisions, forged request headers, approved
identity forwarding, credential stripping, Grafana cookie paths, no-store headers,
protected documentation, per-request authorization, API identity-header stripping,
auth errors/redirects, outage denial and recovery. The synthetic API models the
current dual-header response and the retained response-header reader; it never
turns a browser-supplied identity header into an authorization decision.

The ordinary API proxy deliberately preserves Authorization for the API's own
authentication while stripping trusted proxy-identity headers. The auth
subrequest strips Authorization and both generations of API-key/application
headers. The Grafana proxy strips browser cookies and Authorization and obtains
its identity only from the auth response. A successful mock auth decision without
an identity cannot fall back to the client's forged identity at the test upstream.

The report is `artifacts/rename/operator-gateway-rehearsal/report.json`. It records
scenario names, auth-subrequest count, immutable image IDs, exact checksums of
the eight integration sources and two harness files, limits and ownership-checked
cleanup. Header values, cookies, control tokens and documentation bodies are not
included. Running/failed status replaces stale pass status before each new run.
Normal completion and handled failures remove only resources with that run's
ownership label; a foreign resource is never deleted. After forced process or
daemon termination, inspect labeled resources rather than broadly pruning Docker.

This is a real Nginx HTTP test with mock authorization, not a real Identity login,
PostgreSQL session, CSRF/PKCE, browser, TLS/CDN, SSO or live Grafana auth-proxy
acceptance test. Disabled and expired cases are supplied mock decisions, not
evidence that Identity/database expiry was exercised here. API permission checks
and installed operator ACLs remain separate. The retained operator response-header
alias is intentionally still read; this drill does not approve its removal or a
canonical-only server rollout. Nginx's auth-subrequest status semantics are
documented by the [auth request module](https://nginx.org/en/docs/http/ngx_http_auth_request_module.html).

A separate [PostgreSQL authentication rehearsal](pvad-identity-compatibility.md#isolated-postgresql-login-and-restart-rehearsal)
now exercises real Account login, Designer PKCE handoff, persistent session
validation/revocation and operator authorization through the actual API.
It uses TestServer HTTP rather than this gateway; the two independent results
must not be presented as a combined Nginx/Identity/Grafana acceptance test.

## Combined Operator Browser Rehearsal

The combined local harness is separate from the synthetic HTTP drill:

```bash
node tools/configuration/operator-browser-rehearsal.mjs
node --test tools/configuration/operator-browser-rehearsal.test.mjs
```

It requires the Docker engine, cached Compose-selected Grafana and Nginx images,
cached PostgreSQL 17, the installed .NET/EF toolchain, restored backend packages,
installed Admin/Designer Node dependencies and Playwright Chromium. It accepts
no deployment arguments, existing resource names or database connection strings.
Missing cached images fail before resources are created; the runner never pulls.

The runner creates one owned Docker bridge and disposable PostgreSQL, real
Grafana, static Admin UI and operator-gateway containers. Only the gateway and
PostgreSQL publish loopback ports; Grafana and its UI upstream have no published
ports. The API is a new Kestrel process with a private file-backed keyring and
storage root. EF generates the current schema without contacting an existing
database; only the new database receives it. The Admin production build goes
to a temporary directory, not the installed site's distribution directory.

Development bootstrap creates a synthetic operator with an explicit operator
allowlist. A test-only SQL seed copies that synthetic account's password hash
into a separate organization administrator and assigns its membership role;
no system role is copied. This is not a registration/invitation test. Browser
login uses the real form, CSRF validator, Identity password checker and persistent
sessions. No API or authorization responses are mocked. Inherited application
credentials and configuration are excluded from child-process environments.

Standalone execution uses the current provider and shared datasource definitions;
the integrated monitoring transition harness explicitly starts with the retained
legacy provider in its temporary configuration so the transition is not a no-op.
Grafana uses the repository's actual dashboard definitions.
Its proxy allowlist contains only the gateway address in the newly allocated
private subnet. Auth proxy, disabled Basic/anonymous login and disabled Grafana
login tokens follow the Compose policy. Gateway, datasource, dashboard, folder
and operator identity contracts are not rewritten by this rehearsal.

The browser checks anonymous/spoofed denial, real operator login, protected
runbooks, authoritative Grafana identity, all five provisioned dashboards,
organization-administrator denial, API outage/restart, CSRF, logout, replay and
database-backed expiry. Desktop/mobile screenshots contain synthetic data only.
Grafana screenshots wait for all five dashboard titles in the actual folder
listing; they do not capture a still-loading or externally connected home page.
Browser contexts and their cookies remain in memory: no storage-state, tracing,
video, passwords, cookie values or full user payloads are exported to the report.
External page requests are blocked. The local report and phase logs are under
`artifacts/rename/operator-browser-rehearsal/` and are ignored by Git.

Owned processes stop before resource cleanup. Container/network deletion checks
the run's exact ownership label; unconfirmed or foreign resources are not removed.
Temporary keyrings, database data and generated UI/schema files are discarded.
If a process or Docker daemon is forcibly terminated, inspect the rehearsal's
ownership label before manual cleanup; never broadly prune installed resources.

This is a local HTTP/development-cookie test, not production TLS, SSO, CDN,
customer backup recovery or independent deployment approval. It exercises Admin
operator login, not the separate Account-to-Designer browser handoff. Provisioning
and UI access do not certify Prometheus/Loki/Tempo connectivity, alert delivery
or dashboard query results: those upstream data services are not started here.
The [dated checklist](../checklists/PVAD.Repository-Naming-Followup.md#combined-operator-browser-rehearsal)
records actual results; merely adding this harness does not close a production
gate or change historical primary acceptance totals.

### Initial Sign-In Regression Found By The Rehearsal

The real development bootstrap attaches the complete operator permission catalog
to its built-in role. The initial cookie writer previously serialized those
derived claims before the first request could remove them. Nginx rejected the
oversized login response headers with HTTP 502, even though reduced-seed
TestServer login tests passed. The rehearsal reproduced the actual proxy error.

Cookie sign-in and validated renewal now remove derived permission lists,
organization lists and the per-request resolution marker. Subject identity,
security stamp, roles, active organization and persistent session remain intact;
the existing transformation still resolves permissions from server state on
each request. No proxy buffer, cookie policy, entitlement or authorization rule
was relaxed. Existing expanded tickets renew only after normal validation.
Regression tests cover both Account and Designer cookies with thousands of
synthetic claims; the real browser also requires one compact session cookie and
no Domain attribute. A separate Patch fragment records this runtime correction;
the internal rehearsal itself has impact none.
