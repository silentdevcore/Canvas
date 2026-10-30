import assert from 'node:assert/strict';
import test from 'node:test';
import { refreshDescriptions, rewriteDescriptionSource } from './refresh-openapi-descriptions.mjs';

function fixture() {
  const document = { paths: {}, components: { schemas: { Example: { properties: { minimumPxaVersion: { type: 'string' } } } } } };
  const rows = Array.from({ length: 16 }, (_, index) => {
    const path = `/api/pvad/v1/designer/projects/test-${index}`;
    document.paths[path] = { post: { operationId: `operation${index}`, description: 'Previous description', responses: { 200: { description: 'Unchanged response' } } } };
    return { path, method: 'post', operationId: `operation${index}`, description: 'Current PVAD description' };
  });
  return { document, rows };
}

test('OpenAPI refresh changes only operation descriptions without mutating the source', () => {
  const { document, rows } = fixture(), original = structuredClone(document);
  const updated = refreshDescriptions(document, rows);
  assert.deepEqual(document, original);
  for (const row of rows) {
    assert.equal(updated.paths[row.path].post.description, row.description);
    updated.paths[row.path].post.description = original.paths[row.path].post.description;
  }
  assert.deepEqual(updated, original);
});

test('OpenAPI source refresh preserves unrelated escaping, whitespace and final newline', () => {
  const { document, rows } = fixture();
  document.components.schemas.Example.description = 'A+B';
  const source = JSON.stringify(document, null, 4).replace('A+B', 'A\\u002BB') + '\n';
  const updated = rewriteDescriptionSource(source, rows);
  assert.ok(updated.includes('A\\u002BB'));
  assert.equal(updated.replaceAll('Current PVAD description', 'Previous description'), source);
  assert.equal(rewriteDescriptionSource(updated, rows), updated);
});

test('OpenAPI refresh rejects incomplete, duplicate, foreign or contract-changing projections', () => {
  const { document, rows } = fixture();
  assert.throws(() => refreshDescriptions(document, rows.slice(1)), /sixteen/);
  for (const field of [{ path: '/api/foreign' }, { operationId: 'changed' }, { method: 'get' }, { description: 'PXA' }]) {
    const invalid = structuredClone(rows);
    Object.assign(invalid[0], field);
    assert.throws(() => refreshDescriptions(document, invalid));
  }
  const duplicate = structuredClone(rows);
  duplicate[1] = duplicate[0];
  assert.throws(() => refreshDescriptions(document, duplicate), /duplicate/);
});
