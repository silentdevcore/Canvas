# PXA Admin Operator Guide

> Internal deployment source. This file is intentionally excluded from the public PXA.Documentation navigation and build.

## Classification

This file is an internal operational guideline and runbook, not end-user product documentation. The protected product handbook is available only to authenticated administrators inside PXA.Admin.

## Purpose

This guide prepares a separately protected operator documentation deployment. It contains no credentials, signing material, customer identifiers, or usable bootstrap values.

## Operator Areas

- Configure `admin.powerdoxautomation.com` for Cloud or `admin.{customer-host}` for On-Premise and route same-origin `/api` traffic to PXA.WebApi.
- Require secure host-only cookies, HTTPS, trusted proxy headers, and an explicit production operator allowlist.
- Keep Development identity bootstrap disabled outside local Development.
- Apply EF Core migrations before routing traffic to a new API version.
- Validate database, mail, storage, and API readiness before enabling Admin access.
- Revoke compromised sessions and credentials through audited APIs.
- Suspend an organization only through an approved operational procedure.
- Treat mail outages as queued-delivery incidents; never extract protected outbox payloads.
- Recover databases and external object storage as one consistency boundary.
- Use break-glass access only through a separately approved and audited organizational process.

## Observability Gateway

- Publish Grafana only below `/operator/grafana/` on the protected operator host.
- Keep Grafana port `3000` private to Docker networks.
- Route `/api` on the operator host to PXA.WebApi so the PXA session cookie remains host-only.
- Require `/api/pxa/v1/admin/operator/access` through an internal Nginx `auth_request` before
  forwarding any Grafana request.
- Permit only explicitly allowlisted System Administrators; Organization Administrators and
  anonymous users must receive `403` and `401` respectively.
- Strip browser-supplied `Authorization`, `X-PXA-API-Key`, and `X-WEBAUTH-*` headers before
  forwarding to Grafana.
- Forward only the pseudonymous operator identifier returned by PXA.WebApi.
- Keep Grafana Auth Proxy limited to the fixed operator-gateway address on the internal ingress
  network.
- Terminate TLS at the gateway or a trusted upstream proxy and retain the security headers defined
  by the operator-gateway template.
- Do not place Grafana URLs, dashboards, logs, traces, or this runbook in public PXA.Documentation.

## Logging And Audit

- Keep production logging at `Information`.
- Permit temporary `Debug` or `Trace` only with
  `Observability__AllowTemporaryDebugLogging=true` and an expiry no more than 24 hours ahead.
- Expect PXA.WebApi to stop when the temporary logging window expires; restore the normal log level
  before restarting it.
- Use only W3C trace, span, and parent identifiers for operational correlation. Do not add customer
  identifiers, baggage, arbitrary scopes, document IDs, filenames, or email addresses.
- Treat formatted log messages, exception text, request and response bodies, document content,
  template JSON, credentials, action tokens, license material, and mail bodies as prohibited.
- Use PostgreSQL business audit events for privileged-action evidence. Technical logs and traces
  are diagnostic data and never replace the audit history.
- Restrict direct database and trigger-management access. The application persistence boundary and
  PostgreSQL trigger both reject audit-event updates and deletions; only inserts are part of normal
  operation. SQLSTATE `55000` for `administration.audit_events` indicates an attempted immutable
  audit-history mutation and must be investigated.
- Never disable or drop `administration.reject_audit_event_mutation()` during normal operation.
  Migration owners and database superusers remain trusted recovery identities and must be separately
  controlled and audited.

## Alert Delivery

- Deliver operational alerts directly from Alertmanager so PXA.WebApi and its transactional-mail
  outbox are not dependencies of incident notification.
- Use the local Mailpit receiver only for evaluation and delivery tests.
- Require TLS for production SMTP and provide its password only through the mounted
  `/run/secrets/pxa_alertmanager_smtp_password` file.
- Keep production recipient addresses, SMTP settings, templates, and dashboard bases in
  deployment-owned configuration.
- Render only status, severity, service, environment, start time, repository-controlled summary
  and description, protected dashboard link, and runbook identifier.
- Never render arbitrary Prometheus labels or annotations containing tenant, user, document,
  template, request, token, or credential data.
- Test firing and resolved notifications after every routing or template change.
- Route generic webhooks only through the stateless PXA relay. Never configure Alertmanager to
  call a customer destination directly.
- Mount an independently generated signing secret of at least 32 bytes at
  `/run/secrets/pxa_alert_webhook_signing_key`; never pass it through YAML or environment values.
- Configure one fixed, non-loopback HTTPS destination. Receivers must validate timestamp freshness,
  compare HMAC signatures in constant time, and deduplicate the supplied idempotency key.
- Rotate the mounted signing key under a maintenance procedure coordinated with the receiver. The
  relay rereads the key for each request and fails closed when the file is unreadable or invalid.

## Storage, Retention, And TLS

- Use deployment-owned values based on `deploy/observability/onprem.env.example`; size resource
  limits and storage from measured load.
- Keep Prometheus, Alertmanager, Loki, Tempo, and Grafana data on durable named volumes or explicit
  bind mounts. Back up configuration separately from disposable telemetry.
- For Cloud, use separate S3-compatible Loki and Tempo buckets and workload identity. Do not place
  static object-storage credentials in source-controlled environment or Compose files.
- Seed synthetic telemetry and run `retention-verification.sh` after every retention-policy change
  and after the full retention plus compaction interval.
- Maintain deployment-owned Blackbox Exporter file targets for every public PXA HTTPS endpoint.
  Investigate `PxaTlsCertificateExpiringSoon` before the 14-day threshold and treat
  `PxaTlsProbeUnavailable` as an availability incident.
- Keep metric names, labels, dashboards, alert names, and runbook IDs aligned with
  `deploy/observability/DEPLOYMENT-CONTRACT.md` in every deployment model.

## Failure And Recovery Validation

- Run `deploy/observability/failure-recovery-smoke-test.sh` only in isolated development or staging
  deployments.
- Confirm the baseline is healthy before allowing the script to stop Collector or PostgreSQL.
- Require a firing alert, firing notification, dependency recovery, resolved alert, and resolved
  notification for each scenario.
- Rely on the script exit trap to restore an interrupted container, then verify health manually
  before ending the maintenance window.
- Do not terminate a host-managed WebApi process. Add the WebApi scenario only after PXA.Api Docker
  owns a deterministic stop/start lifecycle.
- Test OCR failure handling through bounded non-production failure injection; never corrupt worker
  binaries, language data, or customer documents to provoke an alert.
- Never enable `Observability__EnableOcrFailureInjection` outside Development or Testing. PXA
  rejects such a configuration at startup.
- Verify document operations independently with the unavailable-OTLP integration test.

## Publication Boundary

- Do not import this file from `websites/PXA.Documentation`.
- Publish it only through a protected operator documentation pipeline.
- Replace deployment-specific placeholders at release time through secret-free configuration.
- Review every release for credentials, tokens, customer data, private URLs, and recovery material.
