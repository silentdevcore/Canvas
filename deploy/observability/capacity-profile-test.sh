#!/usr/bin/env bash

set -euo pipefail

profile="${1:-}"
expected_cpus="${2:-}"
expected_memory_bytes="${3:-}"
concurrency="${4:-}"
api_url="${PXA_CAPACITY_API_URL:-http://127.0.0.1:5087}"
container="${PXA_CAPACITY_API_CONTAINER:-canvas-pxa-webapi-1}"
iterations="${PXA_CAPACITY_ITERATIONS:-100}"
page_copies="${PXA_CAPACITY_PAGE_COPIES:-20}"
source_request="${PXA_CAPACITY_REQUEST_FILE:-deploy/observability/performance-request.json}"
api_key="${PXA_CAPACITY_API_KEY:-}"

if [[ -z "${profile}" || -z "${expected_cpus}" || -z "${expected_memory_bytes}" ||
      -z "${concurrency}" || -z "${api_key}" ]]; then
  echo "Usage: $0 <profile> <cpus> <memory-bytes> <concurrency>" >&2
  echo "Set PXA_CAPACITY_API_KEY to an active synthetic service-account key." >&2
  exit 2
fi
if (( iterations < 20 || iterations > 1000 || concurrency < 1 || concurrency > 64 )); then
  echo "Iterations must be 20-1000 and concurrency must be 1-64." >&2
  exit 2
fi

work="$(mktemp -d)"
expanded_request="${work}/request.json"
stats="${work}/stats.txt"
stop_stats="${work}/stop"
stats_pid=""
cleanup() {
  touch "${stop_stats}" 2>/dev/null || true
  if [[ -n "${stats_pid}" ]]; then
    kill "${stats_pid}" 2>/dev/null || true
    wait "${stats_pid}" 2>/dev/null || true
  fi
  rm -rf "${work}"
}
trap cleanup EXIT INT TERM

actual_nano_cpus="$(docker inspect --format '{{.HostConfig.NanoCpus}}' "${container}")"
actual_memory="$(docker inspect --format '{{.HostConfig.Memory}}' "${container}")"
expected_nano_cpus="$(awk -v cpus="${expected_cpus}" 'BEGIN { printf "%.0f", cpus * 1000000000 }')"
if [[ "${actual_nano_cpus}" != "${expected_nano_cpus}" ||
      "${actual_memory}" != "${expected_memory_bytes}" ]]; then
  echo "Container limits do not match ${profile}: CPU=${actual_nano_cpus}, memory=${actual_memory}." >&2
  exit 1
fi

jq --argjson copies "${page_copies}" '
  .pages as $source
  | .pages = [
      range(0; $copies) as $copy
      | $source[]
      | .id = "\($copy)-\(.id)"
      | .elements |= map(.id = "\($copy)-\(.id)")
    ]
' "${source_request}" >"${expanded_request}"

for warmup in 1 2 3; do
  curl --fail --silent --show-error --output /dev/null \
    --header "X-PXA-API-Key: ${api_key}" \
    --header 'Content-Type: application/json' \
    --data-binary "@${expanded_request}" \
    "${api_url}/api/export?format=pdf&language=en"
done

(
  while [[ ! -f "${stop_stats}" ]]; do
    docker stats --no-stream \
      --format '{{.CPUPerc}} {{.MemPerc}}' \
      "${container}" >>"${stats}" 2>/dev/null || true
    sleep 1
  done
) &
stats_pid=$!

started="$(perl -MTime::HiRes=time -e 'printf "%.6f", time')"
run_request() {
  local iteration="$1"
  if ! curl --fail --silent --show-error \
      --output /dev/null \
      --write-out "%{time_total}\n" \
      --header "X-PXA-API-Key: ${api_key}" \
      --header "Content-Type: application/json" \
      --data-binary "@${expanded_request}" \
      "${api_url}/api/export?format=pdf&language=en" >"${work}/duration-${iteration}"; then
    touch "${work}/failure-${iteration}"
  fi
}
request_pids=""
request_count=0
for ((iteration = 1; iteration <= iterations; iteration++)); do
  run_request "${iteration}" &
  request_pids="${request_pids} $!"
  request_count=$((request_count + 1))
  if (( request_count == concurrency )); then
    for request_pid in ${request_pids}; do
      wait "${request_pid}"
    done
    request_pids=""
    request_count=0
  fi
done
if (( request_count > 0 )); then
  for request_pid in ${request_pids}; do
    wait "${request_pid}"
  done
fi
finished="$(perl -MTime::HiRes=time -e 'printf "%.6f", time')"
touch "${stop_stats}"
wait "${stats_pid}" || true

failures="$(find "${work}" -name 'failure-*' -type f | wc -l | tr -d ' ')"
if (( failures > 0 )); then
  echo "${profile} produced ${failures} failed requests." >&2
  exit 1
fi

cat "${work}"/duration-* | sort -n >"${work}/durations"
p95_line="$(( (iterations * 95 + 99) / 100 ))"
p95="$(sed -n "${p95_line}p" "${work}/durations")"
elapsed="$(awk -v start="${started}" -v finish="${finished}" 'BEGIN { printf "%.3f", finish-start }')"
throughput="$(awk -v count="${iterations}" -v elapsed="${elapsed}" \
  'BEGIN { printf "%.2f", count/elapsed }')"
peak_cpu="$(awk '{ gsub(/%/, "", $1); if ($1 > max) max=$1 } END { printf "%.2f", max+0 }' "${stats}")"
peak_memory="$(awk '{ gsub(/%/, "", $2); if ($2 > max) max=$2 } END { printf "%.2f", max+0 }' "${stats}")"

printf 'Profile: %s\nRequests: %s\nConcurrency: %s\nPages per request: %s\n' \
  "${profile}" "${iterations}" "${concurrency}" "$((page_copies * 5))"
printf 'p95: %ss\nThroughput: %s requests/s\nPeak CPU: %s%%\nPeak memory: %s%%\n' \
  "${p95}" "${throughput}" "${peak_cpu}" "${peak_memory}"

if ! awk -v p95="${p95}" 'BEGIN { exit !(p95 <= 2) }'; then
  echo "${profile} p95 exceeds the two-second capacity budget." >&2
  exit 1
fi
if ! awk -v memory="${peak_memory}" 'BEGIN { exit !(memory <= 90) }'; then
  echo "${profile} memory usage exceeds 90% of its container limit." >&2
  exit 1
fi

echo "${profile} capacity profile passed."
