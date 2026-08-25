import assert from 'node:assert/strict';
import test from 'node:test';

import { PxaMcpContentRepository } from './content.js';
import { DesignValidator } from './validation.js';

const content = new PxaMcpContentRepository();
const validator = new DesignValidator(content.readJson('docs/schema/design-export.schema.json'));

test('validates the complete canonical design schema', () => {
  const valid = validator.validateJson(JSON.stringify({
    id: 'document-1',
    name: 'Example',
    pages: [{
      id: 'page-1',
      elements: [{ id: 'text-1', type: 'text', x: 10, y: 10, width: 100, height: 20 }],
    }],
  }));
  assert.equal(valid.valid, true);

  const invalid = validator.validateJson(JSON.stringify({
    id: 'document-1',
    name: 'Example',
    pages: [{ id: 'page-1', elements: [{ id: 'bad', type: 'unknown' }] }],
  }));
  assert.equal(invalid.valid, false);
  assert.ok(invalid.diagnostics.some((diagnostic) => diagnostic.code === 'PXAMCP003'));
});

test('returns stable diagnostics for malformed and oversized JSON', () => {
  assert.equal(validator.validateJson('{').diagnostics[0].code, 'PXAMCP001');
  assert.equal(
    validator.validateJson(`"${'x'.repeat(10 * 1024 * 1024)}"`).diagnostics[0].code,
    'PXAMCP002',
  );
});
