#!/bin/sh

set -eu

script_directory=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
. "$script_directory/postgres-common.sh"

load_database_url
require_command psql

query() {
  psql "$PXA_DATABASE_URL" -X -qAt -v ON_ERROR_STOP=1 -c "$1"
}

for relation in \
  'public."__EFMigrationsHistory"' \
  'administration.legal_documents' \
  'administration.legal_document_versions' \
  'administration.legal_publication_approvals' \
  'identity.legal_acceptance_events'
do
  if [ "$(query "SELECT to_regclass('$relation') IS NOT NULL;")" != 't' ]; then
    printf 'Required recovery relation is missing: %s\n' "$relation" >&2
    exit 1
  fi
done

if [ "$(query "SELECT count(*) FROM public.\"__EFMigrationsHistory\";")" = '0' ]; then
  printf '%s\n' 'No EF Core migration history was restored.' >&2
  exit 1
fi

invalid_hashes=$(query \
  "SELECT count(*) FROM administration.legal_document_versions WHERE \"ContentHash\" !~ '^[a-f0-9]{64}$';")
if [ "$invalid_hashes" != '0' ]; then
  printf '%s\n' 'Restored Legal versions contain invalid content hashes.' >&2
  exit 1
fi

mismatched_evidence=$(query \
  'SELECT count(*) FROM identity.legal_acceptance_events e JOIN administration.legal_document_versions v ON v."Id" = e."LegalDocumentVersionId" WHERE e."ContentHash" <> v."ContentHash";')
if [ "$mismatched_evidence" != '0' ]; then
  printf '%s\n' 'Restored Legal acceptance evidence does not match its immutable version.' >&2
  exit 1
fi

orphaned_versions=$(query \
  'SELECT count(*) FROM administration.legal_document_versions v LEFT JOIN administration.legal_documents d ON d."Id" = v."LegalDocumentId" WHERE d."Id" IS NULL;')
if [ "$orphaned_versions" != '0' ]; then
  printf '%s\n' 'Restored Legal versions contain orphaned document references.' >&2
  exit 1
fi

published_terms=$(query \
  "SELECT count(*) FROM administration.legal_document_versions v JOIN administration.legal_documents d ON d.\"Id\" = v.\"LegalDocumentId\" WHERE d.\"Key\" = 'terms' AND v.\"Status\" IN ('Published', 'Scheduled') AND v.\"EffectiveAt\" <= now() AND v.\"RetiredAt\" IS NULL;")
published_privacy=$(query \
  "SELECT count(*) FROM administration.legal_document_versions v JOIN administration.legal_documents d ON d.\"Id\" = v.\"LegalDocumentId\" WHERE d.\"Key\" = 'privacy' AND v.\"Status\" IN ('Published', 'Scheduled') AND v.\"EffectiveAt\" <= now() AND v.\"RetiredAt\" IS NULL;")
if [ "$published_terms" = '0' ] || [ "$published_privacy" = '0' ]; then
  printf '%s\n' 'Current Terms and Privacy versions are not both available; keep registration disabled.' >&2
  exit 1
fi

printf '%s\n' 'Legal recovery verification passed.'
