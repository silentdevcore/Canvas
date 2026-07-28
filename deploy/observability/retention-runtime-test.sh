#!/usr/bin/env bash

set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
compose_file="${root}/deploy/observability/retention-test/docker-compose.yml"
project="pxa-retention-test"
trace_id="00000000000000000000000000000001"
now_epoch="$(date +%s)"
metric_epoch="$(( now_epoch - 14400 ))"
log_epoch="$(( now_epoch - 180000 ))"
trace_epoch="$(( now_epoch - 7200 ))"
log_nanos="${log_epoch}000000000"
trace_nanos="${trace_epoch}000000000"
metric_file="$(mktemp)"

compose() {
  docker compose --project-name "${project}" --file "${compose_file}" "$@"
}

cleanup() {
  compose down --volumes --remove-orphans >/dev/null 2>&1 || true
  rm -f "${metric_file}"
}

finish() {
  local status=$?
  if (( status != 0 )) && [[ "${PXA_RETENTION_KEEP_FAILED:-0}" == "1" ]]; then
    rm -f "${metric_file}"
    echo "Retention test resources retained for diagnostics." >&2
    return
  fi
  cleanup
}
trap finish EXIT
trap 'exit 130' INT TERM

wait_http() {
  local url="$1"
  local deadline=$((SECONDS + 90))
  until curl --fail --silent --show-error --max-time 3 "${url}" >/dev/null 2>&1; do
    if (( SECONDS >= deadline )); then
      echo "Timed out waiting for ${url}." >&2
      return 1
    fi
    sleep 2
  done
}

query_metric_count() {
  curl --fail --silent --show-error --get \
    --data-urlencode 'query=pxa_retention_marker{marker="expired"}' \
    --data-urlencode "time=${metric_epoch}" \
    http://127.0.0.1:19090/api/v1/query |
    jq -r '.data.result | length'
}

query_log_count() {
  curl --fail --silent --show-error --get \
    --data-urlencode 'query={service_name="pxa-retention-marker"}' \
    --data-urlencode "start=$((log_epoch - 60))000000000" \
    --data-urlencode "end=$((log_epoch + 60))000000000" \
    --data-urlencode 'limit=10' \
    http://127.0.0.1:13101/loki/api/v1/query_range |
    jq -r '.data.result | length'
}

query_trace_status() {
  curl --silent --output /dev/null --write-out '%{http_code}' \
    "http://127.0.0.1:13201/api/traces/${trace_id}"
}

tempo_block_state() {
  if docker run --rm \
    --volume "${project}_tempo-data:/var/tempo" \
    alpine:3.23 \
    sh -c 'set -- /var/tempo/blocks/*/*/meta.json; [ -f "$1" ]' \
    >/dev/null 2>&1; then
    echo "ready"
  else
    echo "pending"
  fi
}

wait_for_value() {
  local description="$1"
  local expected="$2"
  local timeout="$3"
  shift 3
  local deadline=$((SECONDS + timeout))
  local actual=""
  while (( SECONDS < deadline )); do
    actual="$("$@")"
    if [[ "${actual}" == "${expected}" ]]; then
      echo "${description}: ${actual}"
      return 0
    fi
    sleep 5
  done
  echo "Timed out waiting for ${description}=${expected}; last value was ${actual}." >&2
  return 1
}

cleanup
cat >"${metric_file}" <<EOF
# TYPE pxa_retention_marker gauge
pxa_retention_marker{marker="expired"} 1 ${metric_epoch}.000
# TYPE pxa_retention_anchor gauge
pxa_retention_anchor 1 ${now_epoch}.000
# EOF
EOF

docker volume create "${project}_prometheus-data" >/dev/null
docker run --rm --user root \
  --volume "${project}_prometheus-data:/prometheus" \
  alpine:3.23 \
  chown -R 65534:65534 /prometheus
docker run --rm \
  --volume "${project}_prometheus-data:/prometheus" \
  --volume "${metric_file}:/tmp/marker.prom:ro" \
  --entrypoint /bin/promtool \
  prom/prometheus:v3.12.0 \
  tsdb create-blocks-from openmetrics /tmp/marker.prom /prometheus >/dev/null

PXA_TEST_METRIC_RETENTION=365d \
PXA_TEST_LOG_RETENTION=24h \
PXA_TEST_TRACE_RETENTION=1h \
  compose up --detach
wait_http http://127.0.0.1:19090/-/ready
wait_http http://127.0.0.1:13101/ready
wait_http http://127.0.0.1:13201/ready

curl --fail --silent --show-error \
  --header 'Content-Type: application/json' \
  --data-binary @- \
  http://127.0.0.1:13101/loki/api/v1/push >/dev/null <<EOF
{"streams":[{"stream":{"service_name":"pxa-retention-marker"},"values":[["${log_nanos}","synthetic expired retention marker"]]}]}
EOF

curl --fail --silent --show-error \
  --header 'Content-Type: application/json' \
  --data-binary @- \
  http://127.0.0.1:14318/v1/traces >/dev/null <<EOF
{"resourceSpans":[{"resource":{"attributes":[{"key":"service.name","value":{"stringValue":"pxa-retention-marker"}}]},"scopeSpans":[{"scope":{"name":"pxa.retention"},"spans":[{"traceId":"${trace_id}","spanId":"0000000000000001","name":"synthetic-expired-marker","kind":1,"startTimeUnixNano":"${trace_nanos}","endTimeUnixNano":"$((trace_epoch + 1))000000000","status":{"code":2}}]}]}]}
EOF

wait_for_value "seeded Prometheus marker" "1" 30 query_metric_count
wait_for_value "seeded Loki marker" "1" 30 query_log_count
wait_for_value "seeded Tempo marker" "200" 60 query_trace_status
wait_for_value "seeded Tempo backend block" "ready" 90 tempo_block_state
compose stop tempo
docker run --rm --user root \
  --volume "${project}_tempo-data:/var/tempo" \
  alpine:3.23 \
  sh -c '
    for metadata in /var/tempo/blocks/*/*/meta.json; do
      sed -i \
        -e "s/\"startTime\":\"[^\"]*\"/\"startTime\":\"2000-01-01T00:00:00Z\"/" \
        -e "s/\"endTime\":\"[^\"]*\"/\"endTime\":\"2000-01-01T00:00:01Z\"/" \
        "${metadata}"
    done
  '
PXA_TEST_METRIC_RETENTION=365d \
PXA_TEST_LOG_RETENTION=24h \
PXA_TEST_TRACE_RETENTION=1h \
  compose up --detach tempo
wait_http http://127.0.0.1:13201/ready
curl --fail --silent --show-error \
  --request POST \
  http://127.0.0.1:13101/flush >/dev/null
sleep 80

PXA_TEST_METRIC_RETENTION=2h \
PXA_TEST_LOG_RETENTION=24h \
PXA_TEST_TRACE_RETENTION=1h \
  compose up --detach --force-recreate prometheus loki
wait_http http://127.0.0.1:19090/-/ready
wait_http http://127.0.0.1:13101/ready
wait_http http://127.0.0.1:13201/ready
sleep 80
PXA_TEST_METRIC_RETENTION=2h \
PXA_TEST_LOG_RETENTION=24h \
PXA_TEST_TRACE_RETENTION=1h \
  compose up --detach --force-recreate loki
wait_http http://127.0.0.1:13101/ready
PXA_TEST_METRIC_RETENTION=2h \
PXA_TEST_LOG_RETENTION=24h \
PXA_TEST_TRACE_RETENTION=1h \
  compose up --detach --force-recreate tempo
wait_http http://127.0.0.1:13201/ready

wait_for_value "expired Prometheus marker" "0" 120 query_metric_count
wait_for_value "expired Loki marker" "0" 180 query_log_count
wait_for_value "expired Tempo marker" "404" 180 query_trace_status

echo "Synthetic Prometheus, Loki, and Tempo retention runtime test passed."
