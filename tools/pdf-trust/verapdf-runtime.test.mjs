import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');

test('the independent validator keeps the reviewed version and verifies its installer and base image', () => {
  const dockerfile = read('./verapdf/Dockerfile');
  assert.match(dockerfile, /https:\/\/software\.verapdf\.org\/develop\/1\.31\/verapdf-greenfield-1\.31\.166-installer\.zip/);
  assert.match(dockerfile, /25cc4d87daf1c6eb9efd40487141a03f23bb65a92ba59e49e0f33a01343038da installer\.zip \| sha256sum -c -/);
  for (const line of dockerfile.split('\n').filter(line => line.startsWith('FROM '))) {
    assert.match(line, /@sha256:[a-f0-9]{64}(?: AS installer)?$/);
  }
  assert.match(dockerfile, /USER verapdf/);
  assert.doesNotMatch(dockerfile, /latest|ARG VERAPDF/);
});

test('CI explicitly selects the locally built validator without rewriting historical evidence', () => {
  const workflow = read('../../.github/workflows/ci.yml');
  assert.match(workflow, /PVAD_VERAPDF_IMAGE: pvad-verapdf:1\.31\.166/);
  assert.ok(workflow.indexOf('sh tools/pdf-trust/build-verapdf.sh') < workflow.indexOf('name: Validate generated PDF/UA-1 fixtures independently'));
  assert.match(read('./build-verapdf.sh'), /--network none "\$image" --version/);
  const evidence = JSON.parse(read('../../product-metadata/pdf-trust/external-evidence.json'));
  assert.ok(evidence.entries.filter(entry => entry.kind === 'pdfa' || entry.kind === 'pdfua')
    .every(entry => entry.validator === 'veraPDF 1.31.166'));
});
