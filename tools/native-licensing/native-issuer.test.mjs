import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { test } from 'node:test';

const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');
const require = createRequire(new URL('../../pvad-designer/package.json', import.meta.url));

test('manufacturer issuer CI runs real database tests and uploads only explicit safe evidence files', () => {
  const workflow = require('js-yaml').load(read('.github/workflows/native-library-licensing.yml'));
  const job = workflow.jobs['native-issuer'];
  assert.equal(job['runs-on'], 'ubuntu-latest');
  assert.equal(job.env.PVAD_RUN_POSTGRES_TESTS, '1');
  assert.ok(job.steps.some(step => step.run === 'node tools/native-licensing/with-test-license.mjs native-issuer'));
  const upload = job.steps.find(step => step.uses?.startsWith('actions/upload-artifact@'));
  assert.deepEqual(upload.with.path.trim().split('\n'), [
    'artifacts/native-engine-tests/*/licensed-tests.trx', 'artifacts/native-engine-tests/*/run.json',
  ]);
  for (const path of ['src/Core/PVAD.Domain/**', 'src/Infrastructure/PVAD.Infrastructure.Persistence/**'])
    assert.ok(workflow.on.pull_request.paths.includes(path));
});

test('native manufacturer signing configuration remains isolated from customer server keys and worker input', () => {
  const signer = read('PVAD.WebApi/Services/Licensing/PvadNativeLicenseSigner.cs');
  assert.ok(signer.includes('CreateEmbeddedVerifier().Verify(artifact)'));
  assert.ok(signer.includes('if (!configuration.Enabled)'));
  assert.ok(!signer.includes('PvadLicenseSigningService'));
  for (const path of ['src/Execution/PVAD.CodeWorker/Program.cs', 'docker-compose.yml']) {
    const source = read(path);
    assert.ok(!source.includes('PVAD_TEST_NATIVE_ISSUER_PRIVATE_KEY'));
    assert.ok(!source.includes('NativeLicenseIssuer__PrivateKeyPath'));
  }
  const controller = read('PVAD.WebApi/Controllers/AdminNativeLicensesController.cs');
  assert.ok(controller.includes('ApiExplorerSettings(IgnoreApi = true)'));
  assert.ok(controller.includes('PvadSecurityPolicies.RecentAuthentication'));
  assert.ok(controller.includes('PvadValidateAntiforgery'));
  const guide = read('operator-docs/PVAD.Native-Library-License-Issuance.md');
  assert.ok(guide.includes('native Admin workspace and tenant-authorized Account'));
  assert.ok(guide.includes('Customer deployments must keep native issuance disabled'));
});

test('public OpenAPI documents customer reads without exposing manufacturer operations', () => {
  const snapshot = JSON.parse(read('docs/schema/openapi.json'));
  const paths = Object.keys(snapshot.paths).filter(path => path.includes('/native-licenses'));
  assert.deepEqual(paths.sort(), [
    '/api/pvad/v1/account/native-licenses',
    '/api/pvad/v1/account/native-licenses/{licenseId}',
    '/api/pvad/v1/account/native-licenses/{licenseId}/download',
  ]);
  for (const path of paths) assert.deepEqual(Object.keys(snapshot.paths[path]).filter(key => ['get', 'post', 'put', 'delete', 'patch'].includes(key)), ['get']);
  assert.ok(snapshot.components.schemas.NativeLicensePage);
  assert.ok(snapshot.components.schemas.NativeLicenseDetails);
});
