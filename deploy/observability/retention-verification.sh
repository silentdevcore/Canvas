#!/usr/bin/env bash

set -euo pipefail

PVAD_CONFIG_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
. "$PVAD_CONFIG_ROOT/tools/configuration/environment.sh"
pvad_environment_keys PVAD_PROMETHEUS_CONTAINER PVAD_RETENTION_LOG_QUERY PVAD_RETENTION_METRIC_QUERY PVAD_RETENTION_TRACE_ID

prometheus_container="${PVAD_PROMETHEUS_CONTAINER:-}"
metric_query="${PVAD_RETENTION_METRIC_QUERY:-}"
log_query="${PVAD_RETENTION_LOG_QUERY:-}"
trace_id="${PVAD_RETENTION_TRACE_ID:-}"

if [[ -z "${metric_query}" || -z "${log_query}" || -z "${trace_id}" ]]; then
  cat >&2 <<'EOF'
Set PVAD_RETENTION_METRIC_QUERY, PVAD_RETENTION_LOG_QUERY, and PVAD_RETENTION_TRACE_ID
to synthetic telemetry seeded before the configured retention boundary. Run this check only
after that boundary and the corresponding compaction interval have elapsed.
EOF
  exit 2
fi
if [[ ! "${trace_id}" =~ ^[a-fA-F0-9]{32}$ ]]; then
  echo "PVAD_RETENTION_TRACE_ID must be a 32-character hexadecimal trace ID." >&2
  exit 2
fi

prometheus_container="${prometheus_container:-$(node "$PVAD_CONFIG_ROOT/tools/configuration/pvad-compose.mjs" -f "$PVAD_CONFIG_ROOT/docker-compose.yml" --profile observability ps -q pvad-prometheus)}"

prometheus_result="$(
  docker exec "${prometheus_container}" \
    wget -qO- \
    "http://127.0.0.1:9090/api/v1/query?query=$(printf '%s' "${metric_query}" | jq -sRr @uri)"
)"
if ! jq -e '.status == "success" and (.data.result | length) == 0' \
  <<<"${prometheus_result}" >/dev/null; then
  echo "Expired Prometheus marker still exists." >&2
  exit 1
fi

loki_result="$(
  docker exec "${prometheus_container}" \
    wget -qO- \
    "http://pvad-loki:3100/loki/api/v1/query?query=$(printf '%s' "${log_query}" | jq -sRr @uri)"
)"
if ! jq -e '.status == "success" and (.data.result | length) == 0' \
  <<<"${loki_result}" >/dev/null; then
  echo "Expired Loki marker still exists." >&2
  exit 1
fi

tempo_status="$(
  docker exec "${prometheus_container}" \
    wget -qO /dev/null -S "http://pvad-tempo:3200/api/traces/${trace_id}" 2>&1 |
    awk '/HTTP\\// { status=$2 } END { print status }'
)"
if [[ "${tempo_status}" != "404" ]]; then
  echo "Expected expired Tempo trace to return 404, got ${tempo_status:-unknown}." >&2
  exit 1
fi

echo "Expired Prometheus, Loki, and Tempo telemetry is no longer queryable."
