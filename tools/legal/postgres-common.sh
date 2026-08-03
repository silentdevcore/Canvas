#!/bin/sh

set -eu

load_database_url() {
  if [ -n "${PXA_DATABASE_URL_FILE:-}" ]; then
    if [ ! -r "$PXA_DATABASE_URL_FILE" ]; then
      printf '%s\n' 'PXA_DATABASE_URL_FILE is not readable.' >&2
      exit 1
    fi
    PXA_DATABASE_URL=$(cat "$PXA_DATABASE_URL_FILE")
  fi

  if [ -z "${PXA_DATABASE_URL:-}" ]; then
    printf '%s\n' 'Set PXA_DATABASE_URL_FILE or PXA_DATABASE_URL.' >&2
    exit 1
  fi
  export PXA_DATABASE_URL
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    printf 'Required command is unavailable: %s\n' "$1" >&2
    exit 1
  fi
}

calculate_sha256() {
  file=$1
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file" | awk '{ print $1 }'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$file" | awk '{ print $1 }'
  else
    printf '%s\n' 'A SHA-256 utility is required.' >&2
    exit 1
  fi
}

write_sha256() {
  file=$1
  destination=$2
  printf '%s  %s\n' "$(calculate_sha256 "$file")" "$(basename "$file")" > "$destination"
}

verify_sha256() {
  file=$1
  checksum_file=$2
  expected=$(awk '{ print $1 }' "$checksum_file")
  actual=$(calculate_sha256 "$file")
  if [ "$expected" != "$actual" ]; then
    printf '%s\n' 'Backup SHA-256 verification failed.' >&2
    exit 1
  fi
}
