#!/usr/bin/env bash
set -euo pipefail

mkdir -p "$HOME"
workspace=$(mktemp -d /tmp/pvad-linux-parity.XXXXXX)
tar -xf /input/source.tar -C "$workspace"
cd "$workspace"
mkdir -p /evidence
exec > >(tee /evidence/run.log) 2>&1
trap 'code=$?; printf "\nLinux parity exit code: %s\n" "$code"; exit "$code"' EXIT

printf 'Source commit: %s\n' "${PVAD_BUILD_COMMIT:?A source commit is required}"
uname -a
dotnet --info
node --version
npm --version
pdftoppm -v
convert -version
cat /etc/os-release

npm ci --prefix pvad-designer --no-audit --no-fund
dotnet restore tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj --disable-parallel
node --test tools/visual-parity/font-families.test.mjs tools/visual-parity/fixture-layout.test.mjs

node --input-type=module -e 'import fs from "node:fs"; import { createRequire } from "node:module"; const require = createRequire(import.meta.url); const { chromium } = require("./pvad-designer/node_modules/@playwright/test"); const browser = await chromium.launch(); const metadata = { platform: process.platform, architecture: process.arch, node: process.version, playwright: require("./pvad-designer/node_modules/@playwright/test/package.json").version, browserVersion: browser.version(), sourceCommit: process.env.PVAD_BUILD_COMMIT }; await browser.close(); fs.writeFileSync("/evidence/environment.json", JSON.stringify(metadata, null, 2) + "\n");'

overall=0
for pack in font-families all-elements; do
  printf '\nRunning enforced Linux fixture: %s\n' "$pack"
  export PVAD_VISUAL_PARITY_FIXTURE="test-assets/visual-parity/$pack.json"
  export PVAD_VISUAL_PARITY_ENFORCE=true
  set +e
  node tools/visual-parity/run.mjs
  result=$?
  set -e
  if [ -d artifacts/visual-parity ]; then
    cp -R artifacts/visual-parity "/evidence/$pack"
  fi
  if [ "$result" -ne 0 ]; then
    overall=1
  fi
done

exit "$overall"
