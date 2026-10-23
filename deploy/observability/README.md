# PVADE Local Observability

This optional Docker Compose profile provides the local and On-Premise evaluation stack for PVAD
telemetry:

- OpenTelemetry Collector receives OTLP logs, metrics, and traces.
- Prometheus stores metrics for 90 days by default.
- Loki stores logs for 30 days by default.
- Tempo stores traces for 14 days by default.
- Grafana provisions the PVAD datasources and platform overview dashboard.
- The Nginx operator gateway protects Grafana with the PVAD System Administrator session.
- Alertmanager groups, deduplicates, inhibits, silences, and sends operator email independently
  from PVAD.WebApi.
- PostgreSQL Exporter reports database availability, activity, connection capacity, and storage
  behavior.
- cAdvisor reports CPU, memory, filesystem, and runtime metrics for the PVAD containers.

Fresh defaults now use PVAD for services, storage/network keys and credential
defaults. The explicit `deploy/rename/docker-compose.legacy.yml` overlay retains
old database/user settings, DNS aliases and verified external resources for an
existing complete stack. Do not use fresh defaults as an automatic upgrade.
Grafana UIDs and metric job labels remain stable; `instance` labels may change.
Datasource display names now use PVAD while retaining their existing UIDs and
trace/log cross-links. Active datasources are updated in place, not deleted by
the provisioning deletion list. That list retains only the obsolete historical
Alertmanager datasource. Inspect provisioning logs and saved dashboard queries
after deployment; name-based custom queries may need manual updates.
The dashboard provider identity and existing legacy folder are intentionally retained
until dashboard placement and folder permissions can be migrated explicitly.
See the [infrastructure mapping](../../docs/pvad-infrastructure-mapping.md) before
an upgrade. Stop old writers first, preserve the installed project name and keys,
and never run both generations on the same storage. Configuration checks are not
runtime cutover tests. Scripts select that same project through
`COMPOSE_PROJECT_NAME`; explicit container overrides remain available.

## Start

Start the WebApi with OTLP export and an explicitly allowlisted development operator:

```bash
PVAD_BOOTSTRAP_ADMIN_EMAIL='operator@example.test' \
PVAD_BOOTSTRAP_ADMIN_PASSWORD='replace-this-development-password' \
AdminSecurity__RequireExplicitSystemOperators=true \
AdminSecurity__SystemOperatorEmails__0='operator@example.test' \
Observability__OtlpEndpoint=http://localhost:4317 \
  dotnet run --project PVAD.WebApi/PVAD.WebApi.csproj
```

Start PVAD.Admin on port `5177`, then start the observability profile:

```bash
npm run dev --prefix websites/PVAD.Admin
node tools/configuration/pvad-compose.mjs --profile observability up -d
```

Open `http://localhost:3001/login`, sign in with the allowlisted PVAD operator, and open
`http://localhost:3001/operator/grafana/`. Grafana has no direct host port and trusts only the
operator gateway at its fixed internal network address. The gateway validates every request
through `/api/pvad/v1/admin/operator/access`, removes client-supplied authentication headers, and
passes a pseudonymous operator name to Grafana Auth Proxy. Organization Administrators receive
`403`; anonymous requests receive `401`.

The
provisioned dashboards cover platform health, PostgreSQL and container infrastructure, Mail/OCR
operations, and document operations with persistent job queues. Document-operation metrics expose
Tempo exemplars where available; the correlated log panel links trace IDs from Loki to Tempo.
Queued jobs and isolated OCR worker requests preserve W3C trace context without customer baggage.
The document-operations dashboard also shows bounded object-storage throughput and outcomes,
licensing decisions, and aggregate offline-license expiry states.
Only the protected operator gateway and the Collector ingestion and health endpoints are bound to
localhost. Grafana itself is reachable only inside Docker networks.
Alertmanager, Prometheus, Loki, Tempo, PostgreSQL Exporter, and cAdvisor remain reachable only
inside the Docker network.
Local Alertmanager notifications are delivered directly to Mailpit at
`http://localhost:8025`; stopping PVAD.WebApi does not interrupt this path.
The local sender `pvad-alerts@example.test` and recipient
`pvad-operators@example.test` are synthetic Mailpit-only addresses, not production
mailboxes. Configure approved delivery identities separately for deployment.

Internal Nginx auth locations and helper variables use PVAD. The upstream
`X-PXA-Operator` response header and legacy request-header stripping remain
compatibility contracts; they are not public branding.

Readiness endpoints are:

- Collector: `http://localhost:13133/`
- Alertmanager: `http://pvad-alertmanager:9093/-/ready` inside the Docker network
- Operator gateway: `http://localhost:3001/operator-health`
- Grafana: `http://pvad-grafana:3000/api/health` inside the Docker network
- Prometheus: `http://pvad-prometheus:9090/-/ready` inside the Docker network
- PostgreSQL Exporter: `http://pvad-postgres-exporter:9187/metrics` inside the Docker network
- cAdvisor: `http://pvad-cadvisor:8080/healthz` inside the Docker network
- Loki: `http://pvad-loki:3100/ready` inside the Docker network
- Tempo: `http://pvad-tempo:3200/ready` inside the Docker network

Prometheus, Alertmanager, and Grafana use native Compose health checks. Collector exposes its
health-check extension. Loki, Tempo, and Collector use minimal images without a shell or HTTP
client, so their readiness is checked by the deployment smoke test instead of spawning duplicate
processes inside the containers.

## Configuration

The following environment variables override evaluation defaults:

| Variable | Default | Purpose |
| --- | --- | --- |
| `PVAD_OPERATOR_PUBLIC_URL` | `http://localhost:3001` | Public operator-gateway URL |
| `PVAD_OPERATOR_SERVER_NAME` | `localhost` | Hostname accepted by the operator gateway |
| `PVAD_OPERATOR_API_UPSTREAM` | `http://host.docker.internal:5086` | Same-origin WebApi upstream |
| `PVAD_OPERATOR_UI_UPSTREAM` | `http://host.docker.internal:5177` | PVAD.Admin upstream |
| `VITE_PVAD_OPERATOR_URL` | `https://operator.pvade.com/` in production (unverified deployment target) | Operator host used by the PVAD.Admin dashboard link |
| `PVAD_OPERATOR_IDENTIFIER` | none | System Operator login used only by the smoke test |
| `PVAD_OPERATOR_PASSWORD` | none | System Operator password used only by the smoke test |
| `PVAD_METRIC_RETENTION` | `90d` | Prometheus retention |
| `PVAD_LOG_RETENTION` | `720h` | Loki retention |
| `PVAD_TRACE_RETENTION` | `336h` | Tempo retention |
| `PVAD_TRACE_SAMPLE_PERCENTAGE` | `10` | Successful trace sample percentage |
| `PVAD_ALERT_RETENTION` | `120h` | Alertmanager silence and notification-log retention |
| `PVAD_ALERTMANAGER_ROOT_URL` | `http://pvad-alertmanager:9093` | Internal Alertmanager URL |

For an On-Premise deployment, copy `onprem.env.example` to deployment-owned configuration and
set measured retention, sampling, persistent storage paths, container resource limits, and the TLS
target directory:

```bash
node tools/configuration/pvad-compose.mjs --env-file /absolute/deployment/pvad-observability.env \
  --profile observability config --quiet
node tools/configuration/pvad-compose.mjs --env-file /absolute/deployment/pvad-observability.env \
  --profile observability up -d
```

Prometheus discovers HTTPS endpoints from JSON files in `PVAD_TLS_TARGETS_PATH`. Copy
`prometheus/targets/tls-targets.json.example`, replace its synthetic targets, and keep the directory
deployment-owned. The Blackbox Exporter checks availability and certificate expiry. Alerts fire
when a certificate has fewer than 14 days remaining or an HTTPS probe is unavailable.

For Cloud storage, start from `cloud-storage.env.example`. Loki and Tempo use separate
S3-compatible buckets through `loki/config.s3.yml` and `tempo/config.s3.yml`. Prefer workload
identity. Do not add static S3 credentials to environment files, Compose files, images, or source
control. The shared compatibility rules are defined in `DEPLOYMENT-CONTRACT.md`.

## Logging And Privacy

PVAD.WebApi writes compact JSON console logs and uses the same privacy filter before OTLP export.
Logs contain the UTC timestamp, severity, category, stable event ID, repository-controlled message
template, W3C trace/span/parent identifiers, and service resource fields. Arbitrary scopes,
baggage, formatted messages, exception messages and stack traces are not exported. Structured
values are limited to bounded primitive operational values; attributes whose names indicate
credentials, identity, tenant, document, template, request, response, mail, or license data are
removed. A message template containing a forbidden placeholder is suppressed completely.

Production defaults to `Information`. Enabling `Debug` or `Trace` outside Development requires
both `Observability__AllowTemporaryDebugLogging=true` and an absolute
`Observability__DebugLoggingExpiresAtUtc` no more than 24 hours in the future. When the window
expires, PVAD.WebApi stops so the deployment can restart it with its normal production
configuration. Do not use temporary debug logging to inspect document content or customer data.

Technical telemetry is not business audit evidence. Business audit events remain in PostgreSQL.
The EF Core persistence boundary rejects updates and deletions of tracked audit events, and a
PostgreSQL trigger rejects direct `UPDATE` and `DELETE` statements with SQLSTATE `55000`. Inserts
remain available to the application. Direct database access and trigger-management privileges must
remain restricted to deployment and migration identities.

## Alert Notifications

The evaluation receiver sends firing and resolved notifications from Alertmanager directly to
Mailpit. Its text and HTML templates include only repository-controlled operational fields:
status, severity, service, environment, alert name, start time, summary, description, protected
dashboard URL, and runbook identifier. They do not render arbitrary labels, metric values, customer
attributes, document metadata, or request data.

Run the independent delivery test after the profile is healthy:

```bash
deploy/observability/alertmanager-email-smoke-test.sh
```

Run controlled Collector and PostgreSQL failure/recovery tests only in an isolated development or
staging environment:

```bash
deploy/observability/failure-recovery-smoke-test.sh
```

The script stops one container at a time, waits for the real Prometheus alert and Alertmanager
firing email, restarts the dependency, and verifies recovery plus the resolved email. An exit trap
restores a stopped container after interruption or failure. Individual scenarios can be run with
`collector` or `postgresql`. Never run this script against production.

PVAD.WebApi is currently started as a host process rather than an observability-profile container.
The script accepts a `webapi` scenario when `PVAD_WEBAPI_CONTAINER` identifies the Compose-managed
PVAD.Api container. The API Compose override adds the instance-specific
`PvadWebApiContainerTelemetryMissing` rule so another development WebApi process cannot mask the
container outage.

OCR workers are isolated per-operation processes. A bounded injection is available only in
Development and Testing:

```bash
Observability__EnableOcrFailureInjection=true \
Observability__OcrFailureInjectionCount=4 \
  dotnet run --project PVAD.WebApi/PVAD.WebApi.csproj

PVAD_OCR_FAILURE_INJECTION_CONFIRMED=1 \
PVAD_OCR_TEST_IMAGE=/absolute/path/to/synthetic-image.png \
  deploy/observability/ocr-failure-recovery-smoke-test.sh
```

The application rejects this switch in every other environment and limits the count to 1-10.
Never corrupt OCR binaries, language data, or customer files to test failure alerts. The API
integration suite separately verifies that a document export succeeds when its OTLP destination is
unavailable.

For production, copy `alertmanager.production.yml.example` and the `templates` directory to
deployment-owned configuration. Replace the example SMTP values and replace the local operator URL
inside the copied email template with the protected deployment URL. Store the SMTP password in a
root-readable file outside the repository, then start Compose with the production override:

```bash
PVAD_ALERTMANAGER_CONFIG_FILE=/absolute/deployment/alertmanager.yml \
PVAD_ALERTMANAGER_TEMPLATES_DIR=/absolute/deployment/templates \
PVAD_ALERTMANAGER_SMTP_PASSWORD_FILE=/absolute/secrets/alertmanager-smtp-password \
docker compose \
  -f docker-compose.yml \
  -f deploy/observability/docker-compose.alerting-production.yml \
  --profile observability up -d
```

The override mounts the password as
`/run/secrets/pvad_alertmanager_smtp_password`; Alertmanager reads it through
`smtp_auth_password_file`. Never put the password in YAML, environment variables, images, email
templates, or source control. Production SMTP must require TLS.

To enable generic webhooks, create a random signing secret of at least 32 bytes in a protected
deployment file and set one fixed, non-loopback HTTPS destination. Start the production alerting
and relay overrides together:

```bash
PVAD_ALERT_WEBHOOK_URL=https://operator-webhook.example.test/pvad-alerts \
PVAD_ALERT_WEBHOOK_SIGNING_KEY_FILE=/absolute/secrets/pvad-alert-webhook-key \
docker compose \
  -f docker-compose.yml \
  -f deploy/observability/docker-compose.alerting-production.yml \
  -f deploy/observability/docker-compose.webhook.yml \
  --profile observability up -d
```

The relay accepts Alertmanager only on its internal network, removes arbitrary labels and
annotations, and forwards at most 100 alerts. It signs
`<unix-timestamp>\n<exact-json-body>` with HMAC-SHA256 and sends the lowercase hexadecimal result
as `X-PXA-Webhook-Signature: sha256=<digest>`. Receivers must reject stale timestamps, compare the
signature in constant time, and deduplicate `X-PXA-Webhook-Idempotency-Key`. Rotate the mounted
secret through the deployment secret manager; the relay rereads it for every delivery.

## Retention And Performance Verification

Retention is a deployment acceptance test, not a short unit test. Seed synthetic metric, log, and
trace markers, wait for the configured retention and compaction boundary, then run:

```bash
PVAD_RETENTION_METRIC_QUERY='pvad_retention_marker{marker="expired"}' \
PVAD_RETENTION_LOG_QUERY='{service_name="pvad-retention-marker"}' \
PVAD_RETENTION_TRACE_ID=00000000000000000000000000000001 \
  deploy/observability/retention-verification.sh
```

The script succeeds only when all three markers are no longer queryable. Do not use customer
telemetry as retention evidence.

For an accelerated, isolated storage acceptance test, run:

```bash
deploy/observability/retention-runtime-test.sh
```

The script uses a per-run `pvad-retention-test-<timestamp>-<pid>` project and
dedicated temporary volumes. Its local ports are fixed, so run only one instance
at a time. If failed resources are retained, record the project name before
manual cleanup; never clean up another installed stack.
It proves each synthetic marker is queryable before
aging it beyond the supported test retention boundary, then requires Prometheus and Loki to return
zero results and Tempo to return `404`. It removes only its own containers and volumes.

Compare the same PVAD.WebApi build with observability disabled and enabled:

```bash
PVAD_BASELINE_API_URL=http://127.0.0.1:5091 \
PVAD_INSTRUMENTED_API_URL=http://127.0.0.1:5092 \
PVAD_PERFORMANCE_REQUEST_FILE=deploy/observability/performance-request.json \
PVAD_BASELINE_API_PID=1234 \
PVAD_INSTRUMENTED_API_PID=1235 \
PVAD_PERFORMANCE_EXPORT_FORMAT=pdf \
PVAD_PERFORMANCE_PAGE_COPIES=20 \
PVAD_PERFORMANCE_ITERATIONS=300 \
  deploy/observability/performance-overhead-test.sh
```

Use the same release build and dependencies for both processes and change only observability.
The script compares p95 latency and process CPU independently against the 5% budget. Record the
result for each final Cloud and On-Premise deployment size; local results do not replace
environment-specific capacity tests.

The July 2026 local reference run rendered the five-page fixture 20 times per request, producing
a 100-page PDF. Across 300 requests per process, baseline p95 was `0.019036s`, instrumented p95 was
`0.018607s`, baseline CPU was `6.330s`, and instrumented CPU was `6.220s`. Both observed
differences were below the 5% budget.

Final v1 resource limits and protected workload measurements are recorded in
`deploy/observability/CAPACITY-PROFILES.md`. Run
`deploy/observability/capacity-profile-test.sh` with a short-lived synthetic service-account key
for each release environment. The local July 2026 gates passed for On-Premise Small at
1 vCPU / 2 GiB and Cloud Standard at 2 vCPU / 4 GiB.

Production deployments must use mounted secrets, protected operator routing, external object
storage, a dedicated least-privilege PostgreSQL monitoring role, and deployment-specific resource
sizing. cAdvisor requires privileged access to host cgroups and the container runtime. The
Rancher Desktop evaluation profile maps its Docker and Containerd sockets explicitly; production
orchestrators should use their native node-monitoring deployment model. The localhost ports in this
profile are intended for development and evaluation only.

The operator gateway must terminate TLS in production or sit behind a trusted TLS terminator.
Never expose port `3000` from `pvad-grafana`, weaken the Auth Proxy source whitelist, forward browser
`Authorization` or `X-WEBAUTH-*` headers to Grafana, or configure a parent-domain PVAD session
cookie. PVAD sessions remain host-only.

## Stop

```bash
docker compose --profile observability down
```

Add `--volumes` only when the local telemetry history should also be deleted.
