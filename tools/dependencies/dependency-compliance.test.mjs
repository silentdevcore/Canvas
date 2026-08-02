import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
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
