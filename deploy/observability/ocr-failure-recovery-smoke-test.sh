#!/usr/bin/env bash

set -euo pipefail

PVAD_CONFIG_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
. "$PVAD_CONFIG_ROOT/tools/configuration/environment.sh"
pvad_environment_keys PVAD_API_URL PVAD_OCR_FAILURE_INJECTION_CONFIRMED PVAD_OCR_TEST_IMAGE PVAD_PROMETHEUS_CONTAINER

api_url="${PVAD_API_URL:-http://127.0.0.1:5086}"
image_path="${PVAD_OCR_TEST_IMAGE:-}"
prometheus_container="${PVAD_PROMETHEUS_CONTAINER:-}"
alert_name="PvadOcrRepeatedFailures"

if [[ "${PVAD_OCR_FAILURE_INJECTION_CONFIRMED:-}" != "1" ]]; then
  echo "Set PVAD_OCR_FAILURE_INJECTION_CONFIRMED=1 after enabling the bounded Development injection." >&2
  exit 2
fi
if [[ -z "${image_path}" || ! -f "${image_path}" ]]; then
  echo "Set PVAD_OCR_TEST_IMAGE to a synthetic PNG, JPEG, TIFF, BMP, or WebP file." >&2
  exit 2
fi

prometheus_container="${prometheus_container:-$(node "$PVAD_CONFIG_ROOT/tools/configuration/pvad-compose.mjs" -f "$PVAD_CONFIG_ROOT/docker-compose.yml" --profile observability ps -q pvad-prometheus)}"

alert_state() {
  docker exec "${prometheus_container}" \
    wget -qO- "http://127.0.0.1:9090/api/v1/alerts" |
    jq -r --arg alert_name "${alert_name}" '
      first(.data.alerts[] | select(.labels.alertname == $alert_name) | .state) // "inactive"
    '
}

wait_for_alert() {
  local expected="$1"
  local timeout="$2"
  local deadline=$((SECONDS + timeout))
  local actual=""
  while (( SECONDS < deadline )); do
    actual="$(alert_state)"
    if [[ "${actual}" == "${expected}" ]]; then
      echo "${alert_name}: ${actual}"
      return 0
    fi
    sleep 5
  done
  echo "Timed out waiting for ${alert_name}=${expected}; last state was ${actual}." >&2
  return 1
}

if [[ "$(alert_state)" != "inactive" ]]; then
  echo "${alert_name} must be inactive before failure injection starts." >&2
  exit 1
fi

inject_failure() {
  local attempt="$1"
  local response_file
  local status
  response_file="$(mktemp)"
  status="$(
    curl --silent --show-error \
      --output "${response_file}" \
      --write-out '%{http_code}' \
      --form "file=@${image_path}" \
      "${api_url}/api/document/convert-image-to-pdf"
  )"
  if [[ "${status}" != "400" ]] ||
     ! grep -q "Bounded non-production OCR failure injection is active" "${response_file}"; then
    echo "OCR injection attempt ${attempt} did not return the bounded synthetic failure." >&2
    rm -f "${response_file}"
    exit 1
  fi
  rm -f "${response_file}"
}

# Prometheus needs one pre-existing counter sample before increase(...[10m]) can observe
# three increments. Keep this baseline synthetic and inside the same bounded injection.
inject_failure "baseline"
echo "Waiting for the baseline counter sample to be scraped."
sleep 35

for attempt in 1 2 3; do
  inject_failure "${attempt}"
done

wait_for_alert "firing" 120
echo "Waiting for the ten-minute rolling failure window to expire."
wait_for_alert "inactive" 720
echo "PVAD OCR failure and recovery smoke test passed."
