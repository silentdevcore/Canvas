#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPOSITORY_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
DIST_DIR=${1:-"$REPOSITORY_ROOT/websites/PXA.Company/dist"}
PORT=${PXA_STATUS_SMOKE_PORT:-4183}
CONTAINER="pxa-status-hosting-smoke-$$"

cleanup() {
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT INT TERM

test -f "$DIST_DIR/index.html"
test -f "$DIST_DIR/404.html"

docker run --rm -d \
  --name "$CONTAINER" \
  -p "$PORT:8080" \
  -e PXA_API_UPSTREAM=http://127.0.0.1:9/ \
  -e 'PXA_SPA_ROUTE_PATTERN=^/(|products|pricing|about|support|contact|terms|privacy|license)/?$' \
  -v "$DIST_DIR:/usr/share/nginx/html:ro" \
  -v "$SCRIPT_DIR/nginx.conf.template:/etc/nginx/templates/default.conf.template:ro" \
  nginx:1.29-alpine >/dev/null

attempt=0
until curl -fsS "http://localhost:$PORT/" >/dev/null; do
  attempt=$((attempt + 1))
  if [ "$attempt" -ge 30 ]; then
    docker logs "$CONTAINER"
    exit 1
  fi
  sleep 0.2
done

response_headers() {
  curl -sS -D - -o /dev/null "$1" | tr -d '\r'
}

valid=$(response_headers "http://localhost:$PORT/pricing")
unknown=$(response_headers "http://localhost:$PORT/not-a-pxa-route")
shell=$(response_headers "http://localhost:$PORT/index.html")
asset_path=$(find "$DIST_DIR/assets" -maxdepth 1 -type f \( -name '*.js' -o -name '*.css' \) | head -n 1)
asset=$(response_headers "http://localhost:$PORT/assets/$(basename "$asset_path")")
api=$(response_headers "http://localhost:$PORT/api/pxa/v1/version")

printf '%s\n' "$valid" | grep -q 'HTTP/1.1 200'
printf '%s\n' "$unknown" | grep -q 'HTTP/1.1 404'
printf '%s\n' "$unknown" | grep -qi 'Cache-Control: no-store'
printf '%s\n' "$shell" | grep -qi 'Cache-Control: no-cache, no-store, must-revalidate'
printf '%s\n' "$asset" | grep -qi 'Cache-Control: public, max-age=31536000, immutable'
printf '%s\n' "$api" | grep -q 'HTTP/1.1 503'
printf '%s\n' "$api" | grep -qi 'Content-Type: application/problem+json'

echo 'PXA website hosting smoke test passed.'
