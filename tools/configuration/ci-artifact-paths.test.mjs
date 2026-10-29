import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

test('test-host artifact directories are absolute paths in the repository workspace', () => {
  const workflow = readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');
  const artifacts = [...workflow.matchAll(/^\s+(PVAD_(?:SPREADSHEET_FIDELITY_ARTIFACTS|REAL_WORLD_FIDELITY_ARTIFACTS|PDFUA_ARTIFACT_DIR|PDFA_ARTIFACT_DIR|PDFA_CONVERT_ARTIFACT_DIR)):\s+(.+)$/gm)];
  assert.equal(artifacts.length, 5);
  for (const [, name, value] of artifacts) {
    assert.ok(value.startsWith('${{ github.workspace }}/artifacts/'), name);
    assert.ok(!value.includes('..'), name);
  }
});
