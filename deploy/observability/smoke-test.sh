#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$root_dir/tools/configuration/environment.sh"
pvad_environment_keys PVAD_OPERATOR_PUBLIC_URL PVAD_OPERATOR_IDENTIFIER PVAD_OPERATOR_PASSWORD \
  PVAD_BOOTSTRAP_ADMIN_EMAIL PVAD_BOOTSTRAP_ADMIN_PASSWORD
operator_url="${PVAD_OPERATOR_PUBLIC_URL:-http://127.0.0.1:3001}"
grafana_url="${operator_url}/operator/grafana"
operator_identifier="${PVAD_OPERATOR_IDENTIFIER:-${PVAD_BOOTSTRAP_ADMIN_EMAIL:-}}"
operator_password="${PVAD_OPERATOR_PASSWORD:-${PVAD_BOOTSTRAP_ADMIN_PASSWORD:-}}"
cookie_jar="$(mktemp)"
trap 'rm -f "${cookie_jar}"' EXIT

cd "${root_dir}"

compose=(node "$root_dir/tools/configuration/pvad-compose.mjs" -f "$root_dir/docker-compose.yml" --profile observability)
"${compose[@]}" config --quiet
jq empty deploy/observability/grafana/dashboards/*.json

curl --fail --silent --show-error --max-time 10 http://127.0.0.1:13133/ >/dev/null
"${compose[@]}" exec -T pvad-alertmanager \
  wget -qO- http://127.0.0.1:9093/-/ready >/dev/null
"${compose[@]}" exec -T pvad-prometheus \
  wget -qO- http://pvad-blackbox-exporter:9115/-/healthy >/dev/null
curl --fail --silent --show-error --max-time 10 "${operator_url}/operator-health" >/dev/null

anonymous_status="$(
  curl --silent --output /dev/null --write-out '%{http_code}' --max-time 10 \
    "${grafana_url}/api/search"
)"
if [[ "${anonymous_status}" != "401" ]]; then
  echo "Expected anonymous Grafana API access to return 401, got ${anonymous_status}." >&2
  exit 1
fi

anonymous_documentation_status="$(
  curl --silent --output /dev/null --write-out '%{http_code}' --max-time 10 \
    "${operator_url}/documentation/"
)"
if [[ "${anonymous_documentation_status}" != "401" ]]; then
  echo "Expected anonymous operator documentation access to return 401, got ${anonymous_documentation_status}." >&2
  exit 1
fi

anonymous_documentation_root_status="$(
  curl --silent --output /dev/null --write-out '%{http_code}' --max-time 10 \
    "${operator_url}/documentation"
)"
if [[ "${anonymous_documentation_root_status}" != "401" ]]; then
  echo "Expected anonymous operator documentation root to return 401, got ${anonymous_documentation_root_status}." >&2
  exit 1
fi

if [[ -z "${operator_identifier}" || -z "${operator_password}" ]]; then
  echo "Set PVAD_OPERATOR_IDENTIFIER and PVAD_OPERATOR_PASSWORD to run authenticated smoke checks." >&2
  exit 1
fi

csrf="$(
  curl --fail --silent --show-error --max-time 10 \
    --cookie-jar "${cookie_jar}" \
    "${operator_url}/api/pxa/v1/auth/csrf" | jq -r '.token'
)"
login_payload="$(
  jq -nc \
    --arg identifier "${operator_identifier}" \
    --arg password "${operator_password}" \
    '{identifier: $identifier, password: $password, rememberMe: false}'
)"
curl --fail --silent --show-error --max-time 10 \
  --cookie "${cookie_jar}" \
  --cookie-jar "${cookie_jar}" \
  --header "Content-Type: application/json" \
  --header "X-PVAD-CSRF: ${csrf}" \
  --data "${login_payload}" \
  "${operator_url}/api/pxa/v1/auth/login" >/dev/null

operator_access_status="$(
  curl --silent --output /dev/null --write-out '%{http_code}' --max-time 10 \
    --cookie "${cookie_jar}" \
    "${operator_url}/api/pxa/v1/admin/operator/access"
)"
if [[ "${operator_access_status}" != "204" ]]; then
  echo "Expected authenticated operator access to return 204, got ${operator_access_status}." >&2
  exit 1
fi

curl --fail --silent --show-error --max-time 10 \
  --cookie "${cookie_jar}" "${operator_url}/documentation/" | \
  grep -q "Operator Documentation"
operator_documents="$(
  curl --fail --silent --show-error --max-time 10 --cookie "${cookie_jar}" \
    "${operator_url}/api/pxa/v1/admin/operator/documentation"
)"
if [[ "$(jq '.documents | length' <<<"${operator_documents}")" -lt 2 ]]; then
  echo "Expected protected operator runbooks to be available." >&2
  exit 1
fi
curl --fail --silent --show-error --max-time 10 --cookie "${cookie_jar}" \
  "${operator_url}/api/pxa/v1/admin/operator/documentation/legal-backup-restore-recovery" | \
  jq -e '.markdown | contains("RESTORE PXA DATABASE")' >/dev/null

direct_runbook_status="$(
  curl --silent --output /dev/null --write-out '%{http_code}' --max-time 10 \
    --cookie "${cookie_jar}" "${operator_url}/operator-docs/PVAD.Admin-Operations.md"
)"
if [[ "${direct_runbook_status}" != "404" ]]; then
  echo "Expected direct runbook paths to return 404, got ${direct_runbook_status}." >&2
  exit 1
fi

curl --fail --silent --show-error --max-time 10 \
  --cookie "${cookie_jar}" "${grafana_url}/api/health" >/dev/null

for datasource in pxa-prometheus pxa-loki pxa-tempo; do
  response="$(
    curl --fail --silent --show-error --max-time 10 --cookie "${cookie_jar}" \
      "${grafana_url}/api/datasources/uid/${datasource}/health"
  )"
  if [[ "$(jq -r '.status' <<<"${response}")" != "OK" ]]; then
    echo "Datasource ${datasource} is not healthy: ${response}" >&2
    exit 1
  fi
done

prometheus_datasource="$(
  curl --fail --silent --show-error --max-time 10 --cookie "${cookie_jar}" \
    "${grafana_url}/api/datasources/uid/pxa-prometheus"
)"
if ! jq -e \
  '.jsonData.exemplarTraceIdDestinations[] | select(.datasourceUid == "pxa-tempo")' \
  <<<"${prometheus_datasource}" >/dev/null; then
  echo "Prometheus exemplars are not linked to Tempo." >&2
  exit 1
fi

loki_datasource="$(
  curl --fail --silent --show-error --max-time 10 --cookie "${cookie_jar}" \
    "${grafana_url}/api/datasources/uid/pxa-loki"
)"
if ! jq -e \
  '.jsonData.derivedFields[] | select(.datasourceUid == "pxa-tempo")' \
  <<<"${loki_datasource}" >/dev/null; then
  echo "Loki trace IDs are not linked to Tempo." >&2
  exit 1
fi

tempo_datasource="$(
  curl --fail --silent --show-error --max-time 10 --cookie "${cookie_jar}" \
    "${grafana_url}/api/datasources/uid/pxa-tempo"
)"
if [[ "$(jq -r '.jsonData.tracesToLogsV2.datasourceUid' <<<"${tempo_datasource}")" != "pxa-loki" ]] ||
   [[ "$(jq -r '.jsonData.tracesToMetrics.datasourceUid' <<<"${tempo_datasource}")" != "pxa-prometheus" ]]; then
  echo "Tempo trace drilldowns are not linked to Loki and Prometheus." >&2
  exit 1
fi

"${compose[@]}" exec -T pvad-prometheus \
  promtool check rules /etc/prometheus/rules/pvad-alerts.yml >/dev/null
"${compose[@]}" exec -T pvad-prometheus \
  promtool test rules /etc/prometheus/tests/pvad-alerts.test.yml >/dev/null
"${compose[@]}" exec -T pvad-alertmanager \
  amtool check-config /etc/alertmanager/alertmanager.yml >/dev/null

targets_json="$(
  "${compose[@]}" exec -T pvad-prometheus \
    wget -qO- "http://localhost:9090/api/v1/targets?state=active"
)"
for job in pxa-otel-collector pxa-postgresql pxa-containers; do
  if ! jq -e --arg job "${job}" \
    '.data.activeTargets[] | select(.labels.job == $job and .health == "up")' \
    <<<"${targets_json}" >/dev/null; then
    echo "Prometheus target ${job} is not up." >&2
    exit 1
  fi
done

dashboards="$(
  curl --fail --silent --show-error --max-time 10 --cookie "${cookie_jar}" \
    "${grafana_url}/api/search?query=PVAD"
)"
for dashboard_uid in \
  pxa-platform-overview \
  pxa-infrastructure \
  pxa-operations \
  pxa-document-operations \
  pxa-browser-health; do
  if ! jq -e --arg uid "${dashboard_uid}" \
    '.[] | select(.uid == $uid)' <<<"${dashboards}" >/dev/null; then
    echo "The provisioned dashboard ${dashboard_uid} was not found." >&2
    exit 1
  fi
done

echo "PVAD observability smoke test passed."
