# PXA Observability Deployment Contract

PXA uses one instrumentation and telemetry contract in local, On-Premise, and Cloud deployments.
Deployment variants may change storage and exposure, but they must not change telemetry names,
privacy filtering, trace propagation, dashboards, alert semantics, or application availability.

## Shared Application Contract

- PXA applications emit OTLP logs, metrics, and traces through the OpenTelemetry Collector.
- The OTLP endpoint and export signals are configured through `Observability__*`.
- W3C trace context crosses browser, WebApi, database, queue, mail, storage, and OCR boundaries.
- Operational telemetry excludes customer identifiers, document data, request bodies, secrets,
  arbitrary baggage, and business audit evidence.
- Collector or storage failure must not make document operations unavailable.
- Metric names, bounded labels, dashboard queries, alert names, severity, and runbook IDs are
  identical across deployment models.

## Deployment Matrix

| Concern | Local evaluation | On-Premise | Cloud |
| --- | --- | --- | --- |
| Collector | Compose profile | Compose or orchestrator | Orchestrator-managed |
| Metrics | Prometheus volume | Named volume or bind mount | Managed or durable Prometheus |
| Logs | Loki volume | Named volume or bind mount | Loki with S3-compatible storage |
| Traces | Tempo volume | Named volume or bind mount | Tempo with S3-compatible storage |
| Dashboards | Protected Grafana gateway | Protected Grafana gateway | Protected operator route |
| Alerts | Mailpit | Customer SMTP and optional relay | Transactional SMTP and optional relay |
| TLS checks | Optional file targets | Deployment-owned file targets | Deployment-owned service discovery |
| Secrets | Development-only files | Mounted secrets | Workload identity or secret manager |

## Required Configuration

On-Premise deployments start from `onprem.env.example`. Operators must set measured retention,
sampling, persistent storage paths, resource limits, and TLS target files before production use.

Cloud deployments start from `cloud-storage.env.example`. Loki and Tempo use separate
S3-compatible buckets. Static access keys are not part of the application contract; use workload
identity or inject credentials through the deployment secret manager.

Generic webhook delivery uses the stateless relay in `docker-compose.webhook.yml`. Alertmanager
sends only to the internal relay. The relay sanitizes the payload and signs it with a mounted
secret before sending it to one fixed, non-loopback HTTPS destination.

## Compatibility Rules

- New application telemetry must work with the existing Collector pipeline before release.
- Changes to metric names, labels, alert names, dashboard UIDs, or runbook IDs are compatibility
  changes and require migration notes.
- Environment-specific storage configuration must not be embedded in application code.
- Production configurations must not expose Prometheus, Loki, Tempo, Alertmanager, or Grafana
  directly to public networks.
- Collector ingestion and health ports may use the dedicated host bridge only when bound to
  loopback. The bridge must not contain storage, dashboards, alerting, or application services.
- A deployment is conformant only after configuration validation, readiness smoke tests, privacy
  tests, and the applicable failure/recovery tests pass.
