#!/usr/bin/env bash

set -euo pipefail

PVAD_CONFIG_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
. "$PVAD_CONFIG_ROOT/tools/configuration/environment.sh"
pvad_environment_keys PVAD_ALERTMANAGER_CONTAINER PVAD_MAILPIT_URL

alertmanager_container="${PVAD_ALERTMANAGER_CONTAINER:-$(node "$PVAD_CONFIG_ROOT/tools/configuration/pvad-compose.mjs" -f "$PVAD_CONFIG_ROOT/docker-compose.yml" --profile observability ps -q pvad-alertmanager)}"
mailpit_url="${PVAD_MAILPIT_URL:-http://127.0.0.1:8025}"
test_id="$(date +%s)"
payload="$(
  jq -nc --arg test_id "${test_id}" '[{
    labels: {
      alertname: ("PvadNotificationDeliveryTest" + $test_id),
      severity: "critical",
      service: "pvad-observability",
      environment: "local-evaluation"
    },
    annotations: {
      summary: ("PVAD operator notification delivery test pvad-alert-email-" + $test_id),
      description: "Synthetic technical alert used to verify independent Alertmanager SMTP delivery.",
      dashboard_path: "/d/pxa-platform-overview/pxa-platform-overview",
      runbook_id: "PXA-OBS-TEST"
    }
  }]'
)"

docker exec "${alertmanager_container}" \
  wget -qO- \
  --header="Content-Type: application/json" \
  --post-data="${payload}" \
  http://127.0.0.1:9093/api/v2/alerts >/dev/null

deadline=$((SECONDS + 50))
message_id=""
while (( SECONDS < deadline )); do
  messages="$(curl --fail --silent --show-error --max-time 10 "${mailpit_url}/api/v1/messages")"
  message_id="$(
    jq -r --arg alert_name "PvadNotificationDeliveryTest${test_id}" '
      first(
        .messages[]
        | select(.From.Address == "pvad-alerts@example.test")
        | select(.Subject | contains($alert_name))
        | .ID
      ) // empty
    ' <<<"${messages}"
  )"
  if [[ -n "${message_id}" ]]; then
    break
  fi
  sleep 2
done

if [[ -z "${message_id}" ]]; then
  echo "Alertmanager did not deliver pvad-alert-email-${test_id} to Mailpit within 50 seconds." >&2
  exit 1
fi

message="$(
  curl --fail --silent --show-error --max-time 10 \
    "${mailpit_url}/api/v1/message/${message_id}"
)"
jq -e --arg test_id "pvad-alert-email-${test_id}" --arg alert_name "PvadNotificationDeliveryTest${test_id}" '
  (.Subject | startswith("[PVAD][FIRING][CRITICAL]")) and
  (.Subject | contains($alert_name)) and
  (.Text | contains($test_id)) and
  (.Text | contains("Service: pvad-observability")) and
  (.Text | contains("Environment: local-evaluation")) and
  (.Text | contains("Dashboard: http://localhost:3001/operator/grafana/")) and
  (.Text | contains("Runbook: PXA-OBS-TEST")) and
  (.HTML | contains("Open protected dashboard"))
' <<<"${message}" >/dev/null

for forbidden in password cookie authorization api-key license-key request-body document-content template-json; do
  if jq -er '.Text + "\n" + .HTML' <<<"${message}" |
     tr '[:upper:]' '[:lower:]' |
     grep -F "${forbidden}" >/dev/null; then
    echo "Alert email unexpectedly contains forbidden term: ${forbidden}" >&2
    exit 1
  fi
done

echo "PVAD Alertmanager email smoke test passed for pvad-alert-email-${test_id}."
