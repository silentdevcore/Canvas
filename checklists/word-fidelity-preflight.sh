#!/usr/bin/env bash
set -euo pipefail

required=(soffice pdftoppm)
optional=(compare magick)

missing_required=()
missing_optional=()

echo "Word Fidelity Toolchain Preflight"
echo "================================"

for bin in "${required[@]}"; do
  if command -v "$bin" >/dev/null 2>&1; then
    echo "[OK] required: $bin -> $(command -v "$bin")"
  else
    echo "[MISSING] required: $bin"
    missing_required+=("$bin")
  fi
done

for bin in "${optional[@]}"; do
  if command -v "$bin" >/dev/null 2>&1; then
    echo "[OK] optional: $bin -> $(command -v "$bin")"
  else
    echo "[MISSING] optional: $bin"
    missing_optional+=("$bin")
  fi
done

echo
if [[ ${#missing_required[@]} -gt 0 ]]; then
  echo "Result: NOT READY for full visual fidelity scoring."
  echo "Missing required binaries: ${missing_required[*]}"
  exit 2
fi

echo "Result: READY for conversion-based fidelity scoring."
if [[ ${#missing_optional[@]} -gt 0 ]]; then
  echo "Note: missing optional diff tools: ${missing_optional[*]}"
fi
