#!/usr/bin/env bash

set -euo pipefail

prometheus_container="${PXA_PROMETHEUS_CONTAINER:-canvas-pxa-prometheus-1}"
mailpit_url="${PXA_MAILPIT_URL:-http://127.0.0.1:8025}"
scenarios=("$@")
if (( ${#scenarios[@]} == 0 )); then
  scenarios=(collector postgresql)
fi

stopped_container=""

restore_containers() {
  if [[ -n "${stopped_container}" ]] &&
     [[ "$(docker inspect --format '{{.State.Running}}' "${stopped_container}" 2>/dev/null || true)" != "true" ]]; then
    docker start "${stopped_container}" >/dev/null || true
  fi
}
trap restore_containers EXIT INT TERM

prometheus_json() {
  docker exec "${prometheus_container}" \
    wget -qO- "http://127.0.0.1:9090${1}"
}

alert_state() {
  local alert_name="$1"
  prometheus_json "/api/v1/alerts" |
    jq -r --arg alert_name "${alert_name}" '
      first(.data.alerts[] | select(.labels.alertname == $alert_name) | .state) // "inactive"
    '
}

target_health() {
  local job="$1"
  prometheus_json "/api/v1/targets?state=active" |
    jq -r --arg job "${job}" '
      first(.data.activeTargets[] | select(.labels.job == $job) | .health) // "missing"
    '
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

wait_for_container_health() {
  local container="$1"
  local deadline=$((SECONDS + 90))
  local health=""
  while (( SECONDS < deadline )); do
    health="$(
      docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' \
        "${container}"
    )"
    if [[ "${health}" == "healthy" || "${health}" == "running" ]]; then
      echo "${container} recovery: ${health}"
      return 0
    fi
    sleep 3
  done
  echo "Timed out waiting for ${container} recovery; last state was ${health}." >&2
  return 1
}

wait_for_mail() {
  local status="$1"
  local alert_name="$2"
  local since_epoch="$3"
  local deadline=$((SECONDS + 100))
  local subject="[PXA][${status}][CRITICAL] ${alert_name}"

  while (( SECONDS < deadline )); do
    if curl --fail --silent --show-error --max-time 10 "${mailpit_url}/api/v1/messages" |
       jq -e --arg subject "${subject}" --argjson since "${since_epoch}" '
         any(
           .messages[];
           (.Subject | startswith($subject)) and
           ((.Created | sub("\\.[0-9]+Z$"; "Z") | fromdateiso8601) >= $since)
         )
       ' >/dev/null; then
      echo "${alert_name} ${status} notification: delivered"
      return 0
    fi
    sleep 5
  done

  echo "Timed out waiting for ${status} email for ${alert_name}." >&2
  return 1
}

stop_container() {
  local container="$1"
  stopped_container="${container}"
  docker stop --time 20 "${container}" >/dev/null
}

start_container() {
  local container="$1"
  docker start "${container}" >/dev/null
  stopped_container=""
}

run_collector_scenario() {
  local container="canvas-pxa-otel-collector-1"
  local alert_name="PxaTelemetryPipelineUnavailable"
  local started_at
  started_at="$(date +%s)"

  wait_for_value "collector target baseline" "up" 30 target_health "pxa-otel-collector"
  wait_for_value "${alert_name} baseline" "inactive" 30 alert_state "${alert_name}"
  stop_container "${container}"
  wait_for_value "${alert_name}" "firing" 210 alert_state "${alert_name}"
  wait_for_mail "FIRING" "${alert_name}" "${started_at}"

  start_container "${container}"
  wait_for_value "collector target recovery" "up" 90 target_health "pxa-otel-collector"
  wait_for_value "${alert_name} recovery" "inactive" 90 alert_state "${alert_name}"
  wait_for_mail "RESOLVED" "${alert_name}" "${started_at}"
}

run_postgresql_scenario() {
  local container="canvas-pxa-database-1"
  local alert_name="PxaPostgreSqlUnavailable"
  local started_at
  started_at="$(date +%s)"

  wait_for_value "PostgreSQL target baseline" "up" 30 target_health "pxa-postgresql"
  wait_for_value "${alert_name} baseline" "inactive" 30 alert_state "${alert_name}"
  stop_container "${container}"
  wait_for_value "${alert_name}" "firing" 210 alert_state "${alert_name}"
  wait_for_mail "FIRING" "${alert_name}" "${started_at}"

  start_container "${container}"
  wait_for_container_health "${container}"
  wait_for_value "${alert_name} recovery" "inactive" 120 alert_state "${alert_name}"
  wait_for_mail "RESOLVED" "${alert_name}" "${started_at}"
}

run_webapi_scenario() {
  local container="${PXA_WEBAPI_CONTAINER:-}"
  local alert_name="PxaWebApiContainerTelemetryMissing"
  local started_at
  started_at="$(date +%s)"

  if [[ -z "${container}" ]]; then
    echo "Set PXA_WEBAPI_CONTAINER to a Compose-managed PXA.WebApi container." >&2
    return 2
  fi

  wait_for_value "${alert_name} baseline" "inactive" 30 alert_state "${alert_name}"
  stop_container "${container}"
  wait_for_value "${alert_name}" "firing" 210 alert_state "${alert_name}"
  wait_for_mail "FIRING" "${alert_name}" "${started_at}"

  start_container "${container}"
  wait_for_container_health "${container}"
  wait_for_value "${alert_name} recovery" "inactive" 120 alert_state "${alert_name}"
  wait_for_mail "RESOLVED" "${alert_name}" "${started_at}"
}

for scenario in "${scenarios[@]}"; do
  case "${scenario}" in
    collector)
      run_collector_scenario
      ;;
    postgresql)
      run_postgresql_scenario
      ;;
    webapi)
      run_webapi_scenario
      ;;
    *)
      echo "Unknown scenario '${scenario}'. Use collector, postgresql, or webapi." >&2
      exit 2
      ;;
  esac
done

echo "PXA failure and recovery smoke test passed: ${scenarios[*]}."
