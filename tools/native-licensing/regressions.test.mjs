import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { apiClasses, auditRegressionTrx, projects, regressionEnvironment } from './regressions.mjs';

function fixture() {
  return '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>' +
    '<UnitTestResult testName="Synthetic.A" executionId="one" outcome="Passed" startTime="2026-11-21T10:00:00Z" endTime="2026-11-21T10:00:01Z"/>' +
    '<UnitTestResult testName="Synthetic.B" executionId="two" outcome="Passed" startTime="2026-11-21T10:00:00Z" endTime="2026-11-21T10:00:01Z"/>' +
    '</Results><ResultSummary outcome="Completed"><Counters total="2" executed="2" passed="2" failed="0" notExecuted="0" aborted="0"/></ResultSummary></TestRun>';
}

test('strict TRX audit requires complete passing rows, counters, identities and times', () => {
  assert.equal(auditRegressionTrx(fixture()).passed, 2);
  for (const [from, to] of [
    ['outcome="Passed"', 'outcome="NotExecuted"'], ['executed="2"', 'executed="1"'],
    ['passed="2"', 'passed="1"'], ['failed="0"', 'failed="1"'],
    ['executionId="two"', 'executionId="one"'], ['testName="Synthetic.A"', 'testName=""'],
    ['2026-11-21T10:00:01Z', '2026-11-21T09:00:01Z'], ['outcome="Completed"', 'outcome="Aborted"'],
  ]) assert.throws(() => auditRegressionTrx(fixture().replace(from, to)));
  assert.throws(() => auditRegressionTrx('<TestRun><ResultSummary outcome="Completed"><Counters total="0" executed="0" passed="0"/></ResultSummary></TestRun>'));
});

test('fixed regression scope keeps full libraries and explicit API/worker/roundtrip groups', () => {
  assert.equal(projects.length, 10);
  assert.equal(new Set(projects).size, projects.length);
  for (const project of projects) assert.ok(readFileSync(new URL(`../../tests/${project}/${project}.csproj`, import.meta.url)).length > 0);
  for (const name of ['CodeWorkspaceConversionTests', 'MigrationGeneratedCodeExecutionTests', 'PdfTrust', 'PdfRedaction', 'PvadNativeTrustLicenseTests'])
    assert.ok(apiClasses.includes(name));
  assert.ok(!apiClasses.includes('PvadNative'), 'Do not silently include issuer/PostgreSQL workflows in this filtered run.');
});

test('regression isolation preserves signed session configuration but discards rendering bypasses', () => {
  const original = { ArtifactsPath: '/synthetic/build', PvadLicenseTrustStorePath: '/synthetic/trust',
    PVAD_NATIVE_TEST_SESSION: '/synthetic/session', PVAD_NATIVE_LICENSE_PATH: '/synthetic/license',
    PVAD_VISUAL_PARITY_REUSE_ARTIFACTS: 'true', PVAD_PDF_TRUST_VISUAL_UPDATE_BASELINE: 'true',
    PVAD_PDF_TRUST_OPERATION_UPDATE_BASELINE: 'true', ConnectionStrings__PvadDatabase: 'do-not-use', VITE_ENABLE_VISUAL_PARITY: 'true' };
  const result = regressionEnvironment(original, '/synthetic/output');
  for (const key of ['ArtifactsPath', 'PvadLicenseTrustStorePath', 'PVAD_NATIVE_TEST_SESSION', 'PVAD_NATIVE_LICENSE_PATH']) assert.equal(result[key], original[key]);
  for (const key of ['PVAD_VISUAL_PARITY_REUSE_ARTIFACTS', 'PVAD_PDF_TRUST_VISUAL_UPDATE_BASELINE', 'PVAD_PDF_TRUST_OPERATION_UPDATE_BASELINE']) assert.equal(result[key], undefined);
  assert.match(result.ConnectionStrings__PvadDatabase, /Port=1;/);
  assert.equal(result.VITE_ENABLE_VISUAL_PARITY, 'false');
  assert.equal(result.PVAD_RUN_POSTGRES_TESTS, '1');
  assert.equal(result.TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE, '/var/run/docker.sock');
  assert.equal(regressionEnvironment({ TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE: '/run/custom.sock',
    TESTCONTAINERS_RYUK_DISABLED: 'true' }, '/synthetic/output').TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE, '/run/custom.sock');
  assert.equal(result.TESTCONTAINERS_RYUK_DISABLED, 'false');
  assert.equal(original.PVAD_VISUAL_PARITY_REUSE_ARTIFACTS, 'true');
});

test('launcher keeps existing reports, enforces baselines and cleans its owned runtime', () => {
  const source = readFileSync(new URL('./regressions.mjs', import.meta.url), 'utf8');
  for (const text of ['previous-artifacts', 'regression-source-hashes.json', 'auditRegressionTrx', 'temporaryRuntimeRemoved',
    "PVAD_PDF_TRUST_VISUAL_ENFORCE: 'true'", "PVAD_PDF_TRUST_OPERATION_ENFORCE: 'true'", "PVAD_VISUAL_PARITY_ENFORCE: 'true'", 'withNativeTestSession', 'SIGKILL', 'unix://']) assert.ok(source.includes(text));
  assert.ok(!source.includes('--updateSnapshot') && !source.includes('--passWithNoTests') && !source.includes('UPDATE_BASELINE:'));
});
