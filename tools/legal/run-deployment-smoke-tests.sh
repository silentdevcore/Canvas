#!/usr/bin/env bash
set -euo pipefail

. "$(dirname "$0")/../configuration/environment.sh"
pvad_environment_alias PVAD_DATABASE_URL PXA_DATABASE_URL "${PVAD_DATABASE_URL+x}" "${PVAD_DATABASE_URL-}" "${PXA_DATABASE_URL+x}" "${PXA_DATABASE_URL-}"
pvad_environment_alias PVAD_DATABASE_URL_FILE PXA_DATABASE_URL_FILE "${PVAD_DATABASE_URL_FILE+x}" "${PVAD_DATABASE_URL_FILE-}" "${PXA_DATABASE_URL_FILE+x}" "${PXA_DATABASE_URL_FILE-}"

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repository_root"

PVAD_RUN_POSTGRES_TESTS=1 dotnet test \
  tests/PVAD.Api.Tests/PVAD.Api.Tests.csproj \
  --filter 'FullyQualifiedName~AccountRegistrationControllerTests.Registration_requires_current_legal_versions_and_records_exact_acceptances' \
  -p:SkipOcrWorkerPackaging=true

npm --prefix websites/PVAD.Company test
npm --prefix pvad-designer run test:e2e:legal -- legal-company-fallback.spec.ts
tools/legal/run-backup-restore-smoke-test.sh
