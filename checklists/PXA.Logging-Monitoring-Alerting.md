# PXA Logging, Monitoring, and Alerting

## Goal

Deliver privacy-safe, vendor-neutral observability for the complete PXA platform. Logging, metrics,
distributed tracing, dashboards, and alerts must work in Cloud and On-Premise deployments without
making application availability depend on the telemetry backend.

## Priorities

- [x] P0: Establish structured WebApi logging, OpenTelemetry traces and metrics, health telemetry,
      and background-job instrumentation.
- [x] P1: Ship the optional Docker observability profile with Collector, Prometheus, Loki, Tempo,
      Grafana, and Alertmanager.
- [x] P1: Instrument browser applications, PostgreSQL, OCR, mail, storage, and reverse-proxy
      infrastructure end to end.
- [x] P2: Complete the production WebApi lifecycle, elapsed-retention, and final capacity gates;
      the implementation and local validation work below is otherwise delivered.

## P0 - Observability Foundation

- [x] Use stable OpenTelemetry packages compatible with .NET 10.
- [x] Emit compact structured JSON console logs with UTC timestamps, event IDs, W3C trace/span/
      parent IDs, bounded attributes, and no arbitrary scopes or formatted messages.
- [x] Configure the service name, namespace, version, environment, and instance as resource
      attributes.
- [x] Export logs, metrics, and traces over OTLP only when an endpoint is configured.
- [x] Keep OTLP export bounded and non-blocking so Collector outages do not stop PXA.
- [x] Instrument ASP.NET Core requests, outgoing HTTP calls, .NET runtime, Npgsql, and custom PXA
      activities and meters.
- [x] Exclude liveness and readiness probes from distributed traces.
- [x] Sanitize URL query strings and sensitive HTTP attributes before trace export.
- [x] Add low-cardinality job enqueue, completion, retry, dead-letter, duration, and queue-duration
      metrics.
- [x] Add job-processing activities without tenant, user, filename, or document-content tags.
- [x] Publish dependency health state and health-check duration metrics periodically.
- [x] Assign stable event IDs to operational logs.
- [x] Remove filenames and document identifiers from OCR operational logs.
- [x] Preserve `/health/live` and `/health/ready` behavior.

## Logging And Privacy

- [x] Never log passwords, cookies, authorization headers, API keys, action tokens, license keys,
      mail bodies, request bodies, document contents, or template JSON.
- [x] Do not use emails, customer names, tenant IDs, filenames, job IDs, or document IDs as metric
      labels.
- [x] Use only W3C trace, span, and parent IDs for operational correlation; do not export baggage,
      arbitrary scopes, customer identifiers, or document identifiers.
- [x] Keep production logging at `Information`; temporary `Debug` logging requires explicit
      configuration and an expiry procedure.
- [x] Keep append-only business audit events in PostgreSQL through the application persistence
      boundary; technical logs never replace audit
      evidence.
- [x] Add a PostgreSQL trigger that rejects direct SQL updates and deletes of
      business audit events.
- [x] Add automated checks for forbidden structured-log and telemetry attributes.

## P1 - Platform Coverage

- [x] Persist and propagate W3C trace context from WebApi requests through queued jobs and isolated
      OCR worker processes without propagating baggage or customer identifiers.
- [x] Extend W3C trace propagation from all six browser applications into WebApi requests.
- [x] Extend W3C trace propagation through object storage and the persistent mail outbox.
- [x] Extend W3C trace propagation through any remaining import, export, migration, and rendering
      boundaries.
- [x] Add metrics for authentication failures, authorization denials, rate limits, and API errors.
- [x] Add queue depth, oldest-job age, lease recovery, retry, dead-letter, and retention metrics.
- [x] Add OCR duration, timeout, worker restart, language, and failure metrics without filenames or
      recognized content.
- [x] Add mail delivery, duration, queue-depth, and oldest-message metrics using bounded outcome
      labels.
- [x] Add import, export, migration, and rendering metrics using bounded operation and outcome
      labels.
- [x] Add object-storage and licensing-operation metrics using bounded operation and outcome labels,
      plus aggregate active, expiring, and expired-active offline-license inventory.
- [x] Add privacy-safe browser error, rejected-promise, navigation, API-failure, and Web Vitals
      telemetry.
- [x] Do not add session replay, DOM capture, form-value capture, or product analytics.
- [x] Add PostgreSQL exporter and container/runtime infrastructure metrics.

## P1 - Open-Source Stack

- [x] Add an optional Docker Compose `observability` profile.
- [x] Add an OpenTelemetry Collector gateway with memory limiting, batching, tail sampling, and
      sensitive-attribute removal.
- [x] Store metrics in Prometheus, logs in Loki, and traces in Tempo.
- [x] Provision Grafana datasources and version-controlled dashboards.
- [x] Pin container versions and define health checks, resource limits, persistent volumes, and
      internal-only networks.
- [x] Keep management ports private and disable anonymous Grafana access.
- [x] Use local persistent volumes for evaluation and On-Premise deployments.
- [x] Prepare S3-compatible object storage for Cloud Loki and Tempo deployments.
- [x] Default to 30-day log, 14-day trace, and 90-day metric retention.
- [x] Make retention, sampling, storage, and resource limits configurable for On-Premise.

## Dashboards And Administration

- [x] Add dashboards for platform overview, WebApi, PostgreSQL, jobs and OCR, mail, document
      operations, object storage, licensing, and infrastructure.
- [x] Add a dashboard for browser health.
- [x] Expose Grafana only through a protected operator host and reverse proxy.
- [x] Add a sanitized System Status workspace to PXA.Admin for System Administrators.
- [x] Add `/api/pxa/v1/admin/system/health` with coarse dependency, worker, and queue status.
- [x] Do not expose raw logs, traces, Grafana links, secrets, or customer data to Organization
      Administrators.
- [x] Keep operator dashboards and runbooks outside public PXA.Documentation.

## Alerting

- [x] Define `Info`, `Warning`, and `Critical` severities with grouping, deduplication, inhibition,
      silence, maintenance-window, and resolved-notification behavior.
- [x] Alert when readiness is unavailable for two minutes.
- [x] Alert when HTTP 5xx exceeds 5% for five minutes or API p95 latency exceeds two seconds for ten
      minutes.
- [x] Alert on PostgreSQL exporter/database unavailability and connection usage above 80%.
- [x] Alert on dead-letter jobs or mail, mail queue age above ten minutes, and repeated OCR failures.
- [x] Alert on document-operation failure rate and latency, job queue depth and age, recovered
      leases, authorization-denial spikes, and rate-limit spikes.
- [x] Alert on object-storage failure rate, critical license-validation failures, and active
      offline licenses that are expiring soon or already expired.
- [x] Alert on repeated browser errors, browser API failures, and poor Web Vitals using bounded
      application and route groups.
- [x] Alert on container-runtime disk below 15%, CPU or memory above 85% for 15 minutes, and missing
      service telemetry for five minutes.
- [x] Alert on TLS expiry within 14 days.
- [x] Deliver email directly from Alertmanager so WebApi outages do not suppress alerts.
- [x] Add a stateless relay for HMAC-signed generic webhook delivery.
- [x] Keep SMTP credentials in mounted secrets.
- [x] Keep webhook signing credentials in mounted secrets.
- [x] Include service, environment, severity, start time, dashboard, and runbook links without
      customer or document data.

## Testing

- [x] Test URL sanitization, bounded metric labels, sensitive trace attributes, and forbidden
      structured-log template fields.
- [x] Test resource attributes, emitted structured-log fields, message-template suppression,
      exception redaction, and bounded attributes.
- [x] Test against PostgreSQL that audit-event inserts remain available while direct updates and
      deletes fail and leave the persisted event unchanged.
- [x] Test custom metrics through `MeterListener` without requiring a Collector.
- [x] Test application startup and request handling with no OTLP endpoint and with an unavailable
      endpoint.
- [x] Test W3C parent-context continuation across queued jobs and isolated OCR worker requests.
- [x] Test W3C trace propagation from browser HTTP into WebApi.
- [x] Test trace propagation across storage and persistent mail producer/consumer boundaries.
- [x] Test trace propagation across database and remaining document-operation boundaries.
- [x] Test health metrics without changing public health endpoint behavior.
- [x] Validate Collector configuration, Prometheus rules, Grafana provisioning, and datasource
      health.
- [x] Stop PostgreSQL and the OpenTelemetry Collector independently and verify alerts, recovery,
      firing notifications, and resolved notifications.
- [x] Stop the containerized WebApi and verify alerts and recovery after PXA.Api Docker owns its
      lifecycle.
- [x] Add bounded non-production OCR failure injection that is rejected outside Development and
      Testing.
- [x] Verify the injected OCR failure alert and recovery against the running stack.
- [x] Verify a document export succeeds when the OTLP Collector is unavailable.
- [x] Verify Organization Administrators and anonymous users cannot access operator telemetry
      through the Grafana operator gateway.
- [x] Add a synthetic Prometheus, Loki, and Tempo retention-verification command.
- [x] Confirm retention removes seeded synthetic telemetry after the configured retention and
      compaction boundary.
- [x] Keep additional latency and CPU overhead below 5% under a representative local 100-page PDF
      workload.
- [x] Confirm CPU overhead and capacity against each production deployment profile.

## Acceptance Criteria

- [x] PXA emits correlated logs, metrics, and traces without document content or customer secrets.
- [x] Collector or telemetry-storage outages never make document operations unavailable.
- [x] Metrics use bounded labels and dashboards identify API, worker, database, and queue failures.
- [x] Raw observability data is available only to authorized operators.
- [x] Business audit history remains independent, immutable through application and PostgreSQL
      enforcement, and authoritative.
- [x] Cloud and On-Premise deployments use the same instrumentation and configuration contract.

## Dependencies

- PXA.WebApi and background-job services.
- PostgreSQL persistence and existing live/ready health checks.
- PXA.Api Docker and PXA.Admin roadmaps.
- Reverse-proxy, secret-management, mail, storage, and future object-storage deployment work.

The P2 gates are complete. The containerized WebApi stop/recovery scenario delivered FIRING and
RESOLVED notifications, the isolated retention test removed seeded Prometheus, Loki, and Tempo
markers, and both v1 capacity profiles passed their protected 100-page export workloads.

## Defaults

- OpenTelemetry is the instrumentation and transport standard.
- The PXA-hosted stack uses Collector, Prometheus, Loki, Tempo, Grafana, and Alertmanager.
- Raw telemetry is operator-only.
- Successful traces use 10% tail sampling; errors and slow traces are retained.
- Browser telemetry is operational only and excludes session replay and product analytics.
