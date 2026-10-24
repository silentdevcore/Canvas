# Monitoring Identity Migration

New monitoring configuration uses PVAD identities. Existing measurement history,
dashboard UIDs, datasource UIDs and runbook IDs are not rewritten. This source
change is not a deployed-state migration or a successful live alert-delivery test.

## Coordinated Changes

| Area | New identity | Compatibility |
| --- | --- | --- |
| Prometheus external platform label | `pvad` | Does not relabel stored samples or external history |
| Collector job | `pvad-otel-collector` | Rules accept the old job as well |
| Database exporter job | `pvad-postgresql` | Rules and dashboard queries accept both generations |
| Container exporter job | `pvad-containers` | Rules and dashboard queries accept both generations |
| TLS probe job | `pvad-tls` | Probe-failure and expiry rules accept both generations |
| Main/container rule groups | `pvad-platform`, `pvad-api-container` | Alert names and thresholds are unchanged |
| New alert service labels | `pvad-*` | Existing silences and routing integrations need review |
| Maintenance interval | `pvad-maintenance-window` | Same weekend schedule and timezone in both examples |

The existing MCP job is already canonical. Application metric selectors continue
to read both metric-name generations. Resource queries now include both Compose
service prefixes instead of excluding current containers. The container heartbeat
rule accepts both instance prefixes independently of the metric prefix.

All five managed dashboards retain their identities and datasource references.
Only three infrastructure-panel expressions needed additional job/container-label
compatibility; all forty Prometheus expressions are validated together.

## Before Reloading An Existing Stack

1. Identify the actual Compose project, mounted configuration and retained storage.
   Save the original configuration and follow the existing backup/restore process.
   Do not create replacement volumes, rename stored series or delete dashboards.
2. Inventory custom PromQL, remote-write/external-label consumers, Alertmanager
   routes, silences, inhibition matchers and notification integrations that match
   the old job, platform or service labels. Match both prefixes during transition
   where appropriate. Do not rewrite immutable audit or notification history.
3. Treat the new `service` labels as new alert fingerprints and notification
   groups. Old exact-match silences will not cover them automatically. Pending
   alerts may restart their `for` timer and a changed group can notify again.
   Plan suppression explicitly and confirm no critical monitoring gap results.
4. Reload matching Prometheus rules, scrape configuration and Alertmanager
   configuration as one coordinated change. Do not run duplicate scrapers for the
   same source or dual-publish identical metrics; dual-name selectors are readers,
   not a deduplication system. Keep rule and scrape generations paired.
5. Confirm all canonical targets are up, inspect readiness and rule-evaluation
   errors, and open the existing dashboards. Verify CPU/RAM queries for current
   services, TLS checks and WebApi heartbeat recovery. Repeat the applicable
   failure/recovery and notification-delivery tests in the approved environment.
6. For rollback, restore the saved scrape/rule/Alertmanager configuration together
   and recheck target state, silences and group behavior. Existing stored history
   remains unchanged, but label changes and elapsed timers do not roll back time
   or retract delivered notifications. No automatic live rollback is supplied.

The Grafana provider/folder rename remains a separate
[placement and permissions cutover](pvad-grafana-provider-cutover.md). Its labels
and installed permissions are deliberately unchanged by this monitoring package.

## Offline Validation

```bash
node --test tools/configuration/telemetry-contract.test.mjs
node tools/configuration/pvad-telemetry-contract.mjs \
  artifacts/rename-pvad/observability/dashboard-queries.json \
  artifacts/rename-pvad/observability/identity-tests.json
```

CI uses the pinned Prometheus image to check all rule files and generated dashboard
queries, then runs the static alert fixtures and generated identity scenarios.
The new scenarios evaluate the actual dashboard CPU/RAM expressions, exclude
unrelated containers and exercise both job generations plus all four metric/instance
prefix combinations for heartbeat firing and recovery. The tests use synthetic
samples; no live services, customer documents, credentials or network are needed.

Run `amtool check-config` against local configuration and deployment-owned
production configuration before activation. Passing offline syntax and rule tests
does not prove live SMTP/webhook delivery, installed permissions or rollout safety.
