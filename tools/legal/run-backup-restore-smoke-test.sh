#!/bin/sh

set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
suffix="$$-$(date -u '+%Y%m%d%H%M%S')"
network="pxa-legal-recovery-$suffix"
source_container="pxa-legal-source-$suffix"
target_container="pxa-legal-target-$suffix"
backup_directory=$(mktemp -d "${TMPDIR:-/tmp}/pxa-legal-backup.XXXXXX")
image=${PXA_POSTGRES_IMAGE:-postgres:17-alpine}
password='synthetic-recovery-password'
ready_attempts=${PXA_POSTGRES_READY_ATTEMPTS:-60}
ready_delay=${PXA_POSTGRES_READY_DELAY_SECONDS:-1}
startup_delay=${PXA_POSTGRES_STARTUP_DELAY_SECONDS:-0}

require_non_negative_integer() {
  name=$1
  value=$2
  case "$value" in
    ''|*[!0-9]*)
      printf '%s must be a non-negative integer.\n' "$name" >&2
      exit 2
      ;;
  esac
}

require_non_negative_integer PXA_POSTGRES_READY_ATTEMPTS "$ready_attempts"
require_non_negative_integer PXA_POSTGRES_READY_DELAY_SECONDS "$ready_delay"
require_non_negative_integer PXA_POSTGRES_STARTUP_DELAY_SECONDS "$startup_delay"
if [ "$ready_attempts" -eq 0 ]; then
  printf '%s\n' 'PXA_POSTGRES_READY_ATTEMPTS must be greater than zero.' >&2
  exit 2
fi

cleanup() {
  docker rm -f "$source_container" "$target_container" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
  rm -rf "$backup_directory"
}
trap cleanup EXIT INT TERM

docker network create "$network" >/dev/null
for container in "$source_container" "$target_container"; do
  if [ "$startup_delay" -gt 0 ]; then
    docker run -d --name "$container" --network "$network" \
      -e POSTGRES_DB=pxa -e POSTGRES_USER=pxa -e POSTGRES_PASSWORD="$password" \
      "$image" /bin/sh -c \
      "sleep $startup_delay; exec docker-entrypoint.sh postgres" >/dev/null
  else
    docker run -d --name "$container" --network "$network" \
      -e POSTGRES_DB=pxa -e POSTGRES_USER=pxa -e POSTGRES_PASSWORD="$password" \
      "$image" >/dev/null
  fi
done

database_is_ready() {
  docker exec "$1" psql -U pxa -d pxa -X -qAt -v ON_ERROR_STOP=1 \
    -c 'SELECT 1' 2>/dev/null | grep -qx '1'
}

for container in "$source_container" "$target_container"; do
  attempts=0
  until database_is_ready "$container"; do
    attempts=$((attempts + 1))
    if [ "$attempts" -ge "$ready_attempts" ]; then
      printf 'PostgreSQL database pxa did not become ready after %s attempts: %s\n' \
        "$attempts" "$container" >&2
      docker inspect --format='state={{.State.Status}} exit={{.State.ExitCode}}' \
        "$container" >&2 || true
      docker logs --tail 100 "$container" >&2 || true
      exit 1
    fi
    sleep "$ready_delay"
  done
done

docker exec -i "$source_container" psql -U pxa -d pxa -v ON_ERROR_STOP=1 <<'SQL'
CREATE SCHEMA administration;
CREATE SCHEMA identity;
CREATE TABLE public."__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
CREATE TABLE administration.legal_documents ("Id" uuid PRIMARY KEY, "Key" varchar(80) NOT NULL);
CREATE TABLE administration.legal_document_versions (
  "Id" uuid PRIMARY KEY,
  "LegalDocumentId" uuid NOT NULL REFERENCES administration.legal_documents("Id"),
  "Status" varchar(16) NOT NULL,
  "ContentHash" varchar(64) NOT NULL,
  "EffectiveAt" timestamptz,
  "RetiredAt" timestamptz
);
CREATE TABLE administration.legal_publication_approvals ("Id" uuid PRIMARY KEY, "LegalDocumentVersionId" uuid NOT NULL);
CREATE TABLE identity.legal_acceptance_events (
  "Id" uuid PRIMARY KEY,
  "LegalDocumentVersionId" uuid NOT NULL REFERENCES administration.legal_document_versions("Id"),
  "ContentHash" varchar(64) NOT NULL
);
INSERT INTO public."__EFMigrationsHistory" VALUES ('20260731194907_AddLegalDocumentGovernance', '10.0.0');
INSERT INTO administration.legal_documents VALUES
  ('11111111-1111-1111-1111-111111111111', 'terms'),
  ('22222222-2222-2222-2222-222222222222', 'privacy');
INSERT INTO administration.legal_document_versions VALUES
  ('33333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'Published', repeat('a', 64), now() - interval '1 day', NULL),
  ('44444444-4444-4444-4444-444444444444', '22222222-2222-2222-2222-222222222222', 'Published', repeat('b', 64), now() - interval '1 day', NULL);
INSERT INTO administration.legal_publication_approvals VALUES
  ('55555555-5555-5555-5555-555555555555', '33333333-3333-3333-3333-333333333333');
INSERT INTO identity.legal_acceptance_events VALUES
  ('66666666-6666-6666-6666-666666666666', '33333333-3333-3333-3333-333333333333', repeat('a', 64));
SQL

docker run --rm --network "$network" \
  -v "$repository_root:/workspace:ro" -v "$backup_directory:/backup" \
  -e PXA_DATABASE_URL="postgresql://pxa:$password@$source_container:5432/pxa" \
  "$image" /bin/sh /workspace/tools/legal/backup-postgres.sh /backup

backup=$(find "$backup_directory" -name 'pxa-*.dump' -type f | head -n 1)
if [ -z "$backup" ]; then
  printf '%s\n' 'The recovery smoke test did not create a backup.' >&2
  exit 1
fi

if docker run --rm --network "$network" \
  -v "$repository_root:/workspace:ro" -v "$backup_directory:/backup:ro" \
  -e PXA_DATABASE_URL="postgresql://pxa:$password@$target_container:5432/pxa" \
  "$image" /bin/sh /workspace/tools/legal/restore-postgres.sh "/backup/$(basename "$backup")" \
  >/dev/null 2>&1; then
  printf '%s\n' 'Restore unexpectedly succeeded without explicit confirmation.' >&2
  exit 1
fi

corrupt_backup="$backup.corrupt"
docker run --rm -v "$backup_directory:/backup" \
  -e PXA_BACKUP_NAME="$(basename "$backup")" \
  "$image" /bin/sh -c '
    cp "/backup/$PXA_BACKUP_NAME" "/backup/$PXA_BACKUP_NAME.corrupt"
    cp "/backup/$PXA_BACKUP_NAME.sha256" "/backup/$PXA_BACKUP_NAME.corrupt.sha256"
    printf corrupt >> "/backup/$PXA_BACKUP_NAME.corrupt"
  '
if docker run --rm --network "$network" \
  -v "$repository_root:/workspace:ro" -v "$backup_directory:/backup:ro" \
  -e PXA_DATABASE_URL="postgresql://pxa:$password@$target_container:5432/pxa" \
  -e PXA_RESTORE_CONFIRM='RESTORE PXA DATABASE' \
  "$image" /bin/sh /workspace/tools/legal/restore-postgres.sh "/backup/$(basename "$corrupt_backup")" \
  >/dev/null 2>&1; then
  printf '%s\n' 'Restore unexpectedly accepted a corrupt backup.' >&2
  exit 1
fi

docker run --rm --network "$network" \
  -v "$repository_root:/workspace:ro" -v "$backup_directory:/backup:ro" \
  -e PXA_DATABASE_URL="postgresql://pxa:$password@$target_container:5432/pxa" \
  -e PXA_RESTORE_CONFIRM='RESTORE PXA DATABASE' \
  "$image" /bin/sh /workspace/tools/legal/restore-postgres.sh "/backup/$(basename "$backup")"

docker run --rm --network "$network" \
  -v "$repository_root:/workspace:ro" \
  -e PXA_DATABASE_URL="postgresql://pxa:$password@$target_container:5432/pxa" \
  "$image" /bin/sh /workspace/tools/legal/verify-legal-recovery.sh

if docker run --rm --network "$network" \
  -v "$repository_root:/workspace:ro" -v "$backup_directory:/backup:ro" \
  -e PXA_DATABASE_URL="postgresql://pxa:$password@$target_container:5432/pxa" \
  -e PXA_RESTORE_CONFIRM='RESTORE PXA DATABASE' \
  "$image" /bin/sh /workspace/tools/legal/restore-postgres.sh "/backup/$(basename "$backup")" \
  >/dev/null 2>&1; then
  printf '%s\n' 'Restore unexpectedly accepted a non-empty target.' >&2
  exit 1
fi

restored=$(docker exec "$target_container" psql -U pxa -d pxa -X -qAt \
  -c 'SELECT count(*) FROM identity.legal_acceptance_events;')
if [ "$restored" != '1' ]; then
  printf '%s\n' 'The restored acceptance evidence count is incorrect.' >&2
  exit 1
fi

printf '%s\n' 'Isolated Legal backup and restore smoke test passed.'
