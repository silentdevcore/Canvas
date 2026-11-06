# Integrated Monitoring And Operator Rehearsal

This local test closes the gap between the separate authentication, Grafana
cutover and telemetry rule checks. It is not a production rollout command.

## Run

```bash
node tools/configuration/monitoring-operator-rehearsal.mjs
node --test tools/configuration/monitoring-operator-rehearsal.test.mjs
```

The runner accepts no arguments, deployment destinations, installed resource
names or database connection strings. Docker, the restored .NET/EF toolchain,
OpenSSL, installed Admin/Designer dependencies and Playwright Chromium are
required. It uses the Compose-selected Grafana, Nginx, PostgreSQL, Collector,
Prometheus, Loki, Tempo and Alertmanager images, plus Node 22 and the .NET 10
ASP.NET runtime used by the relay Dockerfile. These images must already be
cached; missing images fail before resource creation. Every run pins their
local immutable IDs and never pulls or publishes an image.

The shared [browser runner](pvad-grafana-provider-cutover.md#combined-operator-browser-rehearsal)
builds the actual API and Admin frontend, creates a disposable PostgreSQL schema
and starts the unmodified operator gateway. Accounts and documents in installed
applications are never read. The synthetic operator and organization administrator
use real Identity sessions. Browser requests are restricted to the owned gateway;
there are no mocked API or authorization responses.

## Telemetry Evidence

The real API exports OTLP traces, metrics and sanitized logs to the real Collector.
Its original privacy processors and exporters are retained. Collector traffic
goes to Loki and Tempo, and Prometheus scrapes its actual metric endpoint.
Grafana uses the repository's unchanged datasource definitions and UIDs.

One synthetic version request carries a fresh W3C trace identifier. The checks
query all three datasources through the authenticated Grafana proxy, require an
increase in the actual version-route request counter, find logs for that same
trace and retrieve its Tempo trace. A synthetic query-string canary must not
appear in the returned logs, trace or API console log. This is one concrete privacy regression,
not certification that every possible log field is safe.

The lab reduces scrape/evaluation intervals to two seconds, the outage rule's
pending duration to four seconds and the API metric export interval to one
second. It enables complete local trace sampling and informational HTTP lifecycle
logs for correlation. These test overrides are reported and never written back
to deployment configuration. Only the Collector scrape and real telemetry-pipeline
outage rule are included. Other production alert rules remain covered separately.

### Diagnostic Query Privacy Regression

The first integrated run exposed an existing gap when informational ASP.NET HTTP
diagnostics were explicitly enabled: their structured query-string property was
not part of the common sensitive-attribute filter. The shared filter now excludes
normalized query-string attribute variants from console and OTLP logging as well
as trace attributes. Request correlation remains available. Five focused cases
reproduced the original failure before the fix; all five and the existing
observability/session group pass afterward. The complete real run also inspects
the API console output for its synthetic query canary.

The public Security/Patch summary was explicitly approved by the owner on
2026-11-04. It is separate from the internal-only rehearsal release fragment.

## Signed Alert Delivery

Stopping the owned Collector must fire the repository's telemetry outage rule in
Prometheus. Alertmanager then calls the actual compiled webhook relay. The relay's
sanitizer, HTTPS-only destination policy and signing code are unchanged.

A private Node HTTPS receiver uses a one-day synthetic certificate for its Docker
network alias. Only that generated certificate is trusted by the relay; TLS
validation is not disabled. The receiver verifies the HMAC, timestamp freshness,
payload digest and sanitized field shape. Its first valid request deliberately
returns HTTP 503. A subsequent real Alertmanager retry must succeed with the same
payload digest. Restarting the Collector must restore the scrape target and
deliver a signed resolved notification. Collector ingestion uses a fixed,
validated loopback binding, which is checked again after restart; an automatically
reassigned host port must not strand the API's OTLP exporter. Fresh API requests
must reach the live metric datasource after cutover and after restore.

No email, external webhook or customer notification is sent. The receiver exports
only bounded synthetic delivery metadata; passwords, signing secrets, certificate
private keys, cookies and raw message payloads are not report artifacts.

## Protected Grafana Cutover And Restore

The same browser session inspects Grafana before rename, after rename and after
restore through Nginx and real API authorization. Anonymous access remains denied.
Each phase captures desktop and mobile folder screenshots with the five dashboards.

The runner stops its Grafana cleanly before copying the SQLite state. It creates a
SHA-256-bound archive in a separately owned temporary volume and confirms that a
corrupt copy fails checksum validation before extraction. The candidate changes
only the provider/folder display labels and explicitly pins the original folder
UID. Dashboard JSON, datasource UIDs and folder permission entries must remain
equal, apart from normal Grafana serialization revisions. A live metric query
must continue to work after rename.

A synthetic annotation written after the backup distinguishes true point-in-time
recovery from merely reverting labels. Restore occurs only while Grafana is
stopped and only into its owned disposable volume. Restored folder placement,
content and permissions must equal the baseline; the post-backup annotation must
be absent. Operator identity, browser access and the datasource query must work
again. Post-backup writes are deliberately not replayed.

## Outputs And Cleanup

Ignored output directory:

```text
artifacts/rename/monitoring-operator-rehearsal/
```

The consolidated report includes source/harness hashes, actual API binary hash,
image IDs, browser scenarios, correlation checks, alert delivery checks and
backup/baseline/restored hashes. Screenshots contain synthetic accounts only.
Phase logs assist local diagnosis; they are not public release evidence.

Every resource is tracked before creation and carries a fresh run ownership
label. Cleanup verifies exact ownership before removing containers, volumes or
the dedicated network and checks that deletion succeeded. It never prunes an
installed stack. Temporary builds, certificates, keys and database data are
removed. A forcibly killed runner or unavailable Docker daemon requires explicit
inspection of ownership labels before manual recovery.

The run publishes only explicit loopback ports for the gateway, disposable
database, Collector ingestion/readiness, read-only receiver metadata and monitoring
query/readiness endpoints. Grafana, Admin UI and the relay/HTTPS delivery endpoint
have no host-published ports. This loopback exposure is a lab convenience, not a
production monitoring network policy.

## Boundaries

- Local HTTP browser cookies are not production TLS, CDN or SSO acceptance.
- An isolated private-certificate receiver is not a real external delivery test.
- API metrics/logs/traces are tested; cAdvisor, PostgreSQL exporter, TLS probes,
  every dashboard panel and every production alert are not certified here.
- Grafana recovery uses synthetic SQLite state and one image version, not a
  customer database backup or cross-version migration.
- Existing operator identity contracts, installed provisioning, retained series,
  active silences and production notification routes remain untouched.
- Actual deployment still needs owner-approved backups, alert/silence migration,
  maintenance planning, independent review and production verification.

See the [dated checklist](../checklists/PVAD.Repository-Naming-Followup.md#integrated-monitoring-and-operator-closure)
for actual results. Adding this test does not change historical acceptance totals.

## Earlier Verified Local Result

The 2026-11-04 final run passed all 12 scenarios and produced 12 synthetic
desktop/mobile screenshots. It pinned 42 integration sources, eight harness/helper
sources, ten cached image IDs and the API/relay binaries. The baseline and restored
Grafana snapshot hashes match. Three signed delivery attempts cover transient
failure, retry and recovery. No owned container, network, volume or temporary
workspace remained after cleanup.

Full API regression: 1,281 passed, 82 opt-in tests skipped, zero failed. The focused
observability/session/privacy group passes 62 cases and the unchanged relay suite
passes five. Skipped opt-in groups remain unverified by those suites. The real
browser test additionally uses disposable PostgreSQL; it is not the complete
database integration matrix.

All 556 Node/website contracts and the Documentation production build pass. The
operator recovery guides remain excluded from public Documentation assets and
navigation, with an explicit regression contract.

## Post-Closeout Revalidation (2026-11-05)

After the scoped source closeout, the offline acceptance reviewer found five
changed monitoring sources/helpers. The previous report was therefore retained
as historical evidence, not relabeled as current. Its original report and 12
screenshots were archived without byte changes in ignored
`artifacts/rename/monitoring-history/`, in the directory named by report SHA-256
`ec0c66691d206cdc00a0dbd89dd346843b29e48c005f032ef2177f77ba08b559`.
An archive receipt lists and verifies all 13 original file hashes.

The full real integration test passed again on tracked-clean commit
`09d68f22e1d6d02b006b3329305d7637fca0cf1b`. Its report SHA-256 is
`7c09a889969a73b98ea6ae531768c063f5d772f63822c36a992180a9115ce251`.
It records 12 unique passing scenarios, 42 integration sources, eight harness
sources, ten cached image IDs and 12 new desktop/mobile screenshots.

- The real request counter increased, two correlated logs were found, and the
  same request trace was returned through the Grafana datasource proxy. The query
  canary was absent from exported telemetry and the API console log.
- Three actual HTTPS delivery attempts covered firing, a deliberate transient
  failure, retry and recovery; signatures, certificate trust and retry idempotency
  passed. Invalid delivery count was zero.
- Folder/datasource UIDs, dashboard content, ACLs and operator identity survived
  the isolated rename and restore. Anonymous and organization-administrator
  access stayed denied. Corrupt backup rejection and removal of the post-backup
  annotation proved point-in-time recovery rather than a display-only rollback.
- Baseline and restored snapshot SHA-256 both equal
  `f2a81d54a3f6ff9d0552e34fd744958d6444adb6c229720585ac67acecff6aa8`.
- Cleanup succeeded. Exact run-label-filtered Docker queries found no owned
  containers, networks or volumes, and no temporary workspace remained.

The desktop screenshot shows the canonical folder and all five dashboards.
The mobile capture uses Grafana's compact, truncated list presentation; it is
not a full mobile accessibility or every-panel layout certification.

The acceptance reviewer now verifies current monitoring source pins. The unchanged
HTTPS report also retains current pins; its real browser run was not repeated.
The broad API, .NET and Node counts above remain dated results, not new totals
from this run. Production configuration, installed accounts/databases, real mail,
external receivers and live Grafana state were untouched. No operational gate,
version, deployment authorization or publication approval changed.

Post-run contracts passed: 425 configuration/versioning/Admin tests and 60
Documentation tests, with no failures or skips. The 28 focused rehearsal and
acceptance cases also passed separately and are already included in the larger
contract count. The Documentation production build passed with the existing Vite
native-config-loader advisory; its 132 files were checked for the five internal
guide names, with no leaked asset or reference. Naming and release checks passed.
This is not a new full Solution, Designer or deployed-host test run.
