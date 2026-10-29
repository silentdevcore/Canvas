#!/usr/bin/env sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPO_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
. "$REPO_ROOT/tools/configuration/environment.sh"
pvad_environment_keys PVAD_MCP_SMOKE_TLS_PORT PVAD_MCP_SMOKE_MOCK_PORT
export COMPOSE_PROJECT_NAME="pvad-mcp-smoke-$$"
COMPOSE_FILE="$SCRIPT_DIR/mcp-smoke/docker-compose.yml"
TEMP_DIR=$(mktemp -d "$SCRIPT_DIR/mcp-smoke/.tls.XXXXXX")

export PVAD_MCP_SMOKE_CERT="$TEMP_DIR/tls.crt"
export PVAD_MCP_SMOKE_KEY="$TEMP_DIR/tls.key"
export PVAD_MCP_SMOKE_TLS_PORT=${PVAD_MCP_SMOKE_TLS_PORT:-3443}
export PVAD_MCP_SMOKE_MOCK_PORT=${PVAD_MCP_SMOKE_MOCK_PORT:-39090}

cleanup() {
  status=$?
  trap - EXIT INT TERM
  if [ "$status" -ne 0 ]; then
    docker compose -f "$COMPOSE_FILE" logs --no-color || true
  fi
  docker compose -f "$COMPOSE_FILE" down --volumes --remove-orphans >/dev/null 2>&1 || true
  rm -rf "$TEMP_DIR"
  exit "$status"
}
trap cleanup EXIT INT TERM

openssl req -x509 -newkey rsa:2048 -sha256 -nodes -days 1 \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \
  -keyout "$PVAD_MCP_SMOKE_KEY" \
  -out "$PVAD_MCP_SMOKE_CERT" >/dev/null 2>&1
chmod 644 "$PVAD_MCP_SMOKE_KEY" "$PVAD_MCP_SMOKE_CERT"

docker compose -f "$COMPOSE_FILE" up --detach --build --wait

# Keep the exact tested image available for the subsequent local security scan.
image_id=$(docker compose -f "$COMPOSE_FILE" images --quiet pvad-mcp)
docker image tag "$image_id" pvad-mcp-smoke:local

NODE_EXTRA_CA_CERTS="$PVAD_MCP_SMOKE_CERT" \
PVAD_MCP_SMOKE_ENDPOINT="https://localhost:$PVAD_MCP_SMOKE_TLS_PORT/mcp" \
PVAD_MCP_SMOKE_TOKEN_ENDPOINT="http://127.0.0.1:$PVAD_MCP_SMOKE_MOCK_PORT/token" \
node "$REPO_ROOT/tools/PVAD.Mcp/scripts/tls-client-smoke.mjs"

docker compose -f "$COMPOSE_FILE" stop mock >/dev/null
attempt=0
status=000
while [ "$attempt" -lt 30 ]; do
  status=$(curl --silent --output /dev/null --write-out '%{http_code}' \
    --cacert "$PVAD_MCP_SMOKE_CERT" "https://localhost:$PVAD_MCP_SMOKE_TLS_PORT/health/ready" || true)
  [ "$status" = 503 ] && break
  attempt=$((attempt + 1))
  sleep 1
done
[ "$status" = 503 ] || { echo "Expected MCP readiness 503 after dependency shutdown, got $status"; exit 1; }

docker compose -f "$COMPOSE_FILE" stop --timeout 10 pvad-mcp >/dev/null
container_id=$(docker compose -f "$COMPOSE_FILE" ps --all --quiet pvad-mcp)
exit_code=$(docker inspect --format '{{.State.ExitCode}}' "$container_id")
[ "$exit_code" = 0 ] || { echo "MCP did not shut down cleanly: exit $exit_code"; exit 1; }

echo "PVAD MCP TLS, protocol, dependency-failure, and shutdown smoke test passed."
