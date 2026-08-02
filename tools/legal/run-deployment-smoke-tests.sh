#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repository_root"

PXA_RUN_POSTGRES_TESTS=1 dotnet test \
  tests/PXA.Api.Tests/PXA.Api.Tests.csproj \
  --filter 'FullyQualifiedName~AccountRegistrationControllerTests.Registration_requires_current_legal_versions_and_records_exact_acceptances' \
  -p:SkipOcrWorkerPackaging=true

npm --prefix websites/PXA.Company test
npm --prefix pxa-designer run test:e2e:legal -- legal-company-fallback.spec.ts
tools/legal/run-backup-restore-smoke-test.sh
