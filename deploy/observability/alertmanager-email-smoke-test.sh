#!/usr/bin/env bash

set -euo pipefail

alertmanager_container="${PXA_ALERTMANAGER_CONTAINER:-canvas-pxa-alertmanager-1}"
mailpit_url="${PXA_MAILPIT_URL:-http://127.0.0.1:8025}"
test_id="$(date +%s)"
payload="$(
  jq -nc --arg test_id "${test_id}" '[{
    labels: {
      alertname: ("PxaNotificationDeliveryTest" + $test_id),
      severity: "critical",
      service: "pxa-observability",
      environment: "local-evaluation"
    },
    annotations: {
      summary: ("PXA operator notification delivery test pxa-alert-email-" + $test_id),
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
    jq -r --arg alert_name "PxaNotificationDeliveryTest${test_id}" '
      first(
        .messages[]
        | select(.From.Address == "pxa-alerts@powerdox.local")
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
  echo "Alertmanager did not deliver pxa-alert-email-${test_id} to Mailpit within 50 seconds." >&2
  exit 1
fi

message="$(
  curl --fail --silent --show-error --max-time 10 \
    "${mailpit_url}/api/v1/message/${message_id}"
)"
jq -e --arg test_id "pxa-alert-email-${test_id}" --arg alert_name "PxaNotificationDeliveryTest${test_id}" '
  (.Subject | startswith("[PXA][FIRING][CRITICAL]")) and
  (.Subject | contains($alert_name)) and
  (.Text | contains($test_id)) and
  (.Text | contains("Service: pxa-observability")) and
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

echo "PXA Alertmanager email smoke test passed for pxa-alert-email-${test_id}."
