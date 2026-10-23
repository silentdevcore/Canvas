# PVAD Observability Identity Transition

## Current Compose Source Names

Fresh root/API Compose defaults and production/webhook overlays use canonical
resource names, database defaults and secret mounts. Explicit legacy overlays
retain old database settings and DNS aliases and require verified existing
external volumes/networks. Prometheus job labels and Grafana UIDs remain stable;
scrape `instance` labels may change. Operational scripts use canonical services
or resolved container IDs. The current focused configuration/source tests pass
(51/51), without starting containers. Builds and runtime cutover remain separate;
the earlier checks below record historical transition stages, not current defaults.
See [the cutover mapping](pvad-infrastructure-mapping.md).

## New OCR Executions

New isolated OCR runs emit `PVAD.ImageOcr` activities named
`pvad.ocr.worker.execute` and create disposable `pvad-image-ocr-*` directories.
Trace parent/state propagation, status handling and cleanup behavior are unchanged.
Existing work directories are not renamed or adopted by a new request.

The WebApi trace subscriber includes both `PVAD.ImageOcr` and `PXA.ImageOcr`.
This is subscriber compatibility, not duplicate emission: the current engine
emits one canonical span. Custom ActivityListeners or trace queries filtering only
the old name must accept both names before upgrading. Stored spans are not
rewritten, and the old ActivitySource constant is available explicitly as
`LegacyActivitySourceName` for transition consumers.

## Console Formatting

The default formatter is `pvad-json`. A registered `pxa-json` formatter alias
uses the same serializer and privacy filtering. Resource fields, trace identifiers
and user-supplied service names are not rewritten simply because the formatter
name changes. The formatter alias does not create a second log emission.

## Intentionally Retained Identities

| Value family | Decision | Reason |
| --- | --- | --- |
| `pxa-database`, `pxa-mail` health registrations | Retained | Health consumers, readiness and dependency labels share these names |
| Persistence datasource `pxa-database` | Retained | Physical dependency identity is distinct from service branding |
| Grafana `pxa-*` UIDs and datasource references | Retained | Preserve links and persisted dashboard identity |
| Old metric/log names in queries | Read both generations | Preserve historical measurements alongside new PVAD data |
| Collector environment inputs | PVAD through the Compose launcher | Legacy PXA launcher inputs resolve identically; unmanaged containers need paired mapping |
| Legacy service DNS, database and physical telemetry storage | Explicit legacy overlay only | Fresh defaults use PVAD; verified external bindings select old data |

The root Compose/Collector sampling and memory inputs now use PVAD together.
Managed Loki, Tempo and Operator proxy inputs also use PVAD. The Operator
entrypoint accepts legacy PXA inputs and rejects conflicting values before Nginx
starts. Its earlier Nginx configuration passed canonical-input and legacy-input
container checks; the conflict case failed as intended. Retained service aliases,
dashboard UIDs and upstream operator identity headers remain compatibility
identifiers, not new product names.
Actual Compose resolution matches for canonical and legacy-only launcher inputs,
and the real Collector configuration validator passed. That does not prove that
every unmanaged external container receives aliases. Broader image validation
and deployed trace/query checks remain open. No claim of production telemetry
continuity is inferred from local configuration checks alone.

See the [rename tracker](../checklists/Rename-PXA-To-PVAD.md) and
[test evidence](../checklists/PVAD.Rename-Test-Report.md).

## Remaining Operational Scripts

Performance, retention verification/runtime, Alertmanager mail, failure/recovery
and OCR failure/recovery scripts now resolve PVAD inputs through the shared shell
adapter. Existing PXA inputs remain accepted, with conflicts rejected before any
operational command. Retention fixture Compose/Loki/Tempo inputs and the production
Alertmanager overlay were changed together. Actual Compose resolution is equal for
canonical and legacy input sets; six script syntax/conflict contracts pass.
These tests do not execute failure injection or send mail to an existing stack.
The current follow-up replaces fixed physical container-name defaults with
canonical Compose selection. Mounted secret names and historical alert/runbook
IDs remain the previously documented compatibility boundaries.

The webhook Compose overlay also uses canonical `PVAD_ALERT_WEBHOOK_*` inputs.
The shared launcher accepts their old `PXA_` counterparts and rejects conflicting
values before Compose runs. Actual configuration comparison verifies destination,
limits and secret-file mapping for both generations. The mounted secret name,
network bindings remain unchanged; the service key now uses PVAD with a legacy
DNS alias. No webhook was sent by the earlier test.

## Current Operational Display Text

Prometheus summary/description annotations, dashboard search tags and Alertmanager
mail subject/body text now use PVAD. Dashboard UIDs, rule filenames, runbook IDs,
service/dependency labels and template definition names remain stable. Existing
mail is not rewritten. The operational smoke scripts use the new subject prefix
and canonical security headers while retaining their bounded failure-injection
guards and original restore confirmation phrase.

Local verification uses Prometheus 3.12.0 `promtool`: the rule tests pass and all
37 rules across the three files validate. Alertmanager 0.32.1 `amtool` renders
subject, text and HTML with its synthetic default data. This proves template
rendering, not actual delivery, production routing or dashboard continuity.
