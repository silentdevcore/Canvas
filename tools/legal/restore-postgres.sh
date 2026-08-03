#!/bin/sh

set -eu
umask 077

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_directory/postgres-common.sh"

load_database_url
require_command pg_restore
require_command psql

backup=${1:-}
if [ -z "$backup" ] || [ ! -r "$backup" ]; then
  printf '%s\n' 'Usage: restore-postgres.sh <verified-backup.dump>' >&2
  exit 1
fi
if [ "${PXA_RESTORE_CONFIRM:-}" != 'RESTORE PXA DATABASE' ]; then
  printf '%s\n' 'Set PXA_RESTORE_CONFIRM="RESTORE PXA DATABASE" for an approved restore.' >&2
  exit 1
fi

checksum="$backup.sha256"
if [ ! -r "$checksum" ]; then
  printf '%s\n' 'The adjacent .sha256 file is required.' >&2
  exit 1
fi
verify_sha256 "$backup" "$checksum"
pg_restore --list "$backup" >/dev/null

existing_tables=$(psql "$PXA_DATABASE_URL" -X -qAt \
  -c "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema');")
if [ "$existing_tables" != '0' ]; then
  printf '%s\n' 'Restore target is not empty. Restore into a new isolated database.' >&2
  exit 1
fi

pg_restore \
  --exit-on-error \
  --no-owner \
  --no-privileges \
  --dbname="$PXA_DATABASE_URL" \
  "$backup"

printf '%s\n' 'PostgreSQL restore completed; run verify-legal-recovery.sh before routing traffic.'
