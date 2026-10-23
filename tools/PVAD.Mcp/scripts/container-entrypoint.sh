#!/bin/sh
set -eu
if [ "${PVAD_MCP_HOST+x}" != x ] && [ "${PXA_MCP_HOST+x}" != x ]; then
    export PVAD_MCP_HOST=0.0.0.0
fi
exec "$@"
