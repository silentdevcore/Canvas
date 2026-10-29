import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, rmSync, writeFileSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { inspectDocfxOutput } from './verify-docfx-output.mjs';

test('DocFX links include local assets, directory indexes, encoded paths and anchors', () => {
  const root = mkdtempSync(join(tmpdir(), 'pvad-docfx-'));
  try {
    mkdirSync(join(root, 'api'));
    writeFileSync(join(root, 'api/index.html'), '<h1 id="type">Type</h1><a href="../index.html">Home</a>');
    writeFileSync(join(root, 'sample file.json'), '{}');
    writeFileSync(join(root, 'index.html'), '<a href="api/#type">API</a><a href="sample%20file.json">Schema</a><a href="https://example.invalid/">External</a>');
    assert.deepEqual(inspectDocfxOutput(root), { htmlFiles: 2, checkedLinks: 3, failures: [] });
    writeFileSync(join(root, 'index.html'), '<a href="api/#missing">API</a><img src="missing.png">');
    assert.deepEqual(inspectDocfxOutput(root).failures.map(item => item.reason), ['missing-anchor', 'missing-file']);
  } finally { rmSync(root, { recursive: true, force: true }); }
});

test('DocFX explicitly packages public schemas and synthetic downloads without operator assets', () => {
  const config = JSON.parse(readFileSync(new URL('../../docs/docfx.json', import.meta.url)));
  assert.deepEqual(config.build.resource, [
    { files: ['schema/design-export.schema.json', 'schema/pvad-project-manifest.schema.json', 'schema/pvad-workbook.schema.json'] },
    { files: ['downloads/pdf-trust/**'], src: '../websites/PVAD.Documentation/public' },
  ]);
});
