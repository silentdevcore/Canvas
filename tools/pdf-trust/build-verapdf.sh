#!/usr/bin/env sh
set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
image=pvad-verapdf:1.31.166
docker build --platform linux/amd64 --tag "$image" "$script_dir/verapdf"
version=$(docker run --rm --platform linux/amd64 --network none "$image" --version)
printf '%s\n' "$version"
printf '%s\n' "$version" | grep -Eq '(^|[^0-9])1\.31\.166([^0-9]|$)'
printf 'Pinned validator ready. Use PVAD_VERAPDF_IMAGE=%s\n' "$image"
