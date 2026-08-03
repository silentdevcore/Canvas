import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import test from 'node:test';

const root = new URL('../../', import.meta.url);
const read = (path) => readFile(new URL(path, root), 'utf8');

test('compliance catalog keeps unresolved licenses as production blockers', async () => {
  const catalog = JSON.parse(await read('product-metadata/dependency-compliance.json'));
  const blockers = catalog.licenseDecisions.filter((decision) => !decision.productionApproved);
  assert.equal(catalog.productionReady, false);
  assert.deepEqual(blockers.map((decision) => decision.id), ['npoi-osmf-eula']);
  assert.equal(blockers[0].status, 'pending-legal-review');
});

test('SBOM coverage includes every shipped execution surface', async () => {
  const catalog = JSON.parse(await read('product-metadata/dependency-compliance.json'));
  assert.deepEqual(
    new Set(catalog.sbom.artifacts),
    new Set(['webapi', 'designer', 'webapi-container']),
  );
  assert.equal(catalog.sbom.format, 'SPDX-JSON');
  assert.equal(catalog.sbom.version, '2.2-or-later');
});

test('external GitHub Actions use immutable commit pins', async () => {
  const workflowDirectory = new URL('.github/workflows/', root);
  const workflowFiles = (await readdir(workflowDirectory)).filter((name) => name.endsWith('.yml'));

  for (const name of workflowFiles) {
    const source = await read(`.github/workflows/${name}`);
    const uses = source.matchAll(/^\s*(?:-\s*)?uses:\s*([^@\s]+)@([^\s#]+)/gmu);
    for (const [, action, reference] of uses) {
      if (action.startsWith('./')) continue;
      assert.match(reference, /^[a-f0-9]{40}$/u, `${name}: ${action} is not SHA-pinned`);
    }
  }
});
