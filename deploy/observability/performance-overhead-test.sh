#!/usr/bin/env bash

set -euo pipefail

baseline_url="${PXA_BASELINE_API_URL:-}"
instrumented_url="${PXA_INSTRUMENTED_API_URL:-}"
request_file="${PXA_PERFORMANCE_REQUEST_FILE:-}"
baseline_pid="${PXA_BASELINE_API_PID:-}"
instrumented_pid="${PXA_INSTRUMENTED_API_PID:-}"
iterations="${PXA_PERFORMANCE_ITERATIONS:-200}"
maximum_overhead="${PXA_MAX_OBSERVABILITY_OVERHEAD_PERCENT:-5}"
export_format="${PXA_PERFORMANCE_EXPORT_FORMAT:-pdf}"
page_copies="${PXA_PERFORMANCE_PAGE_COPIES:-20}"
export_query="format=${export_format}"

if [[ -z "${baseline_url}" || -z "${instrumented_url}" || ! -f "${request_file}" ||
      -z "${baseline_pid}" || -z "${instrumented_pid}" ]]; then
  cat >&2 <<'EOF'
Set PXA_BASELINE_API_URL, PXA_INSTRUMENTED_API_URL, PXA_PERFORMANCE_REQUEST_FILE,
PXA_BASELINE_API_PID, and PXA_INSTRUMENTED_API_PID.
The two deployments must use the same build and dependencies; only observability may differ.
EOF
  exit 2
fi
if ! kill -0 "${baseline_pid}" 2>/dev/null || ! kill -0 "${instrumented_pid}" 2>/dev/null; then
  echo "Both API process IDs must identify running processes owned by the current user." >&2
  exit 2
fi
if (( iterations < 50 || iterations > 10000 )); then
  echo "PXA_PERFORMANCE_ITERATIONS must be between 50 and 10000." >&2
  exit 2
fi
if (( page_copies < 1 || page_copies > 100 )); then
  echo "PXA_PERFORMANCE_PAGE_COPIES must be between 1 and 100." >&2
  exit 2
fi
if [[ ! "${export_format}" =~ ^[a-z0-9]+$ ]]; then
  echo "PXA_PERFORMANCE_EXPORT_FORMAT must contain only lowercase letters and digits." >&2
  exit 2
fi
if [[ "${export_format}" == "pdf" ]]; then
  export_query="${export_query}&language=en"
fi

measure() {
  local base_url="$1"
  local output_file="$2"
  local iteration
  for ((iteration = 0; iteration < iterations; iteration++)); do
    curl --fail --silent --show-error \
      --output /dev/null \
      --write-out '%{time_total}\n' \
      --header "Content-Type: application/json" \
      --data-binary "@${request_file}" \
      "${base_url}/api/export?${export_query}" >>"${output_file}"
  done
}

percentile95() {
  local input_file="$1"
  sort -n "${input_file}" |
    awk -v count="${iterations}" 'NR == int((count * 95 + 99) / 100) { print; exit }'
}

cpu_seconds() {
  local pid="$1"
  ps -o time= -p "${pid}" |
    awk -F: '
      {
        gsub(/[[:space:]]/, "", $0)
        if (NF == 3) print ($1 * 3600) + ($2 * 60) + $3
        else if (NF == 2) print ($1 * 60) + $2
        else print $1
      }
    '
}

expanded_request="$(mktemp)"
baseline_results="$(mktemp)"
instrumented_results="$(mktemp)"
trap 'rm -f "${expanded_request}" "${baseline_results}" "${instrumented_results}"' EXIT

jq --argjson copies "${page_copies}" '
  .pages as $source
  | .pages = [
      range(0; $copies) as $copy
      | $source[]
      | .id = "\($copy)-\(.id)"
      | .elements |= map(.id = "\($copy)-\(.id)")
    ]
' "${request_file}" >"${expanded_request}"
request_file="${expanded_request}"

for warmup in 1 2 3 4 5; do
  curl --fail --silent --show-error --output /dev/null \
    --header "Content-Type: application/json" \
    --data-binary "@${request_file}" \
    "${baseline_url}/api/export?${export_query}"
  curl --fail --silent --show-error --output /dev/null \
    --header "Content-Type: application/json" \
    --data-binary "@${request_file}" \
    "${instrumented_url}/api/export?${export_query}"
done

baseline_cpu_before="$(cpu_seconds "${baseline_pid}")"
instrumented_cpu_before="$(cpu_seconds "${instrumented_pid}")"
measure "${baseline_url}" "${baseline_results}" &
baseline_measurement_pid=$!
measure "${instrumented_url}" "${instrumented_results}" &
instrumented_measurement_pid=$!
wait "${baseline_measurement_pid}"
wait "${instrumented_measurement_pid}"
baseline_cpu_after="$(cpu_seconds "${baseline_pid}")"
instrumented_cpu_after="$(cpu_seconds "${instrumented_pid}")"

baseline_p95="$(percentile95 "${baseline_results}")"
instrumented_p95="$(percentile95 "${instrumented_results}")"
latency_overhead="$(
  awk -v baseline="${baseline_p95}" -v instrumented="${instrumented_p95}" '
    BEGIN {
      if (baseline <= 0) exit 2;
      printf "%.2f", ((instrumented - baseline) / baseline) * 100
    }
  '
)"
baseline_cpu="$(
  awk -v before="${baseline_cpu_before}" -v after="${baseline_cpu_after}" \
    'BEGIN { printf "%.3f", after - before }'
)"
instrumented_cpu="$(
  awk -v before="${instrumented_cpu_before}" -v after="${instrumented_cpu_after}" \
    'BEGIN { printf "%.3f", after - before }'
)"
cpu_overhead="$(
  awk -v baseline="${baseline_cpu}" -v instrumented="${instrumented_cpu}" '
    BEGIN {
      if (baseline <= 0) exit 2;
      printf "%.2f", ((instrumented - baseline) / baseline) * 100
    }
  '
)"

printf 'Baseline p95: %ss\nInstrumented p95: %ss\nLatency overhead: %s%%\n' \
  "${baseline_p95}" "${instrumented_p95}" "${latency_overhead}"
printf 'Baseline CPU: %ss\nInstrumented CPU: %ss\nCPU overhead: %s%%\n' \
  "${baseline_cpu}" "${instrumented_cpu}" "${cpu_overhead}"
if ! awk -v overhead="${latency_overhead}" -v maximum="${maximum_overhead}" \
  'BEGIN { exit !(overhead <= maximum) }'; then
  echo "Observability latency overhead exceeds ${maximum_overhead}%." >&2
  exit 1
fi
if ! awk -v overhead="${cpu_overhead}" -v maximum="${maximum_overhead}" \
  'BEGIN { exit !(overhead <= maximum) }'; then
  echo "Observability CPU overhead exceeds ${maximum_overhead}%." >&2
  exit 1
fi

echo "PXA observability latency and CPU overhead are within the configured budget."
