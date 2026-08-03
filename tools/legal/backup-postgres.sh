#!/bin/sh

set -eu
umask 077

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_directory/postgres-common.sh"

load_database_url
require_command pg_dump
require_command pg_restore

output_directory=${1:-}
if [ -z "$output_directory" ]; then
  printf '%s\n' 'Usage: backup-postgres.sh <protected-output-directory>' >&2
  exit 1
fi

mkdir -p "$output_directory"
chmod 700 "$output_directory"
timestamp=$(date -u '+%Y%m%dT%H%M%SZ')
backup="$output_directory/pxa-$timestamp-$$.dump"
temporary="$backup.tmp"
checksum="$backup.sha256"

cleanup() {
  rm -f "$temporary"
}
trap cleanup EXIT INT TERM

pg_dump \
  --format=custom \
  --compress=9 \
  --no-owner \
  --no-privileges \
  --file="$temporary" \
  "$PXA_DATABASE_URL"
pg_restore --list "$temporary" >/dev/null
mv "$temporary" "$backup"
write_sha256 "$backup" "$checksum"
chmod 600 "$backup" "$checksum"

printf 'Created verified PostgreSQL backup: %s\n' "$backup"
