import assert from 'node:assert/strict';
import test from 'node:test';
import { cpSync, mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { validateLegalContent } from './validate-legal-content.mjs';

test('Legal candidates are complete, English-authoritative, and Swiss-law based', () => {
  const result = validateLegalContent();

  assert.deepEqual(result.errors, []);
  assert.equal(result.manifest.authoritativeLocale, 'en');
  assert.equal(result.manifest.governingLaw, 'Switzerland');
  assert.equal(result.manifest.documents.length, 7);
});

function fixture(action) {
  const directory = mkdtempSync(join(tmpdir(), 'pvad-legal-candidates-'));
  try {
    mkdirSync(join(directory, 'product-metadata'), { recursive: true });
    cpSync(new URL('../../product-metadata/legal-documents', import.meta.url), join(directory, 'product-metadata/legal-documents'), { recursive: true });
    mkdirSync(join(directory, 'docs/reference'), { recursive: true });
    cpSync(new URL('../../docs/reference/compatibility-identifiers.v1.json', import.meta.url), join(directory, 'docs/reference/compatibility-identifiers.v1.json'));
    action(directory);
  } finally { rmSync(directory, { recursive: true, force: true }); }
}

test('Legal reference tampering and missing per-document hash pins are rejected', () => {
  fixture(directory => {
    const register = join(directory, 'docs/reference/compatibility-identifiers.v1.json');
    const bytes = readFileSync(register);
    writeFileSync(register, Buffer.concat([bytes, Buffer.from('\n')]));
    assert.ok(validateLegalContent(directory).errors.some(error => /hash does not match/.test(error)));
    writeFileSync(register, bytes);
    const terms = join(directory, 'product-metadata/legal-documents/en/terms.md');
    writeFileSync(terms, readFileSync(terms, 'utf8').replace(/[a-f0-9]{64}/g, 'removed-pin'));
    assert.ok(validateLegalContent(directory).errors.some(error => /terms.md must name its pinned/.test(error)));
  });
});

test('Legal candidate revision cannot silently reuse the previous document version', () => {
  fixture(directory => {
    const file = join(directory, 'product-metadata/legal-documents/en/manifest.json');
    const manifest = JSON.parse(readFileSync(file, 'utf8'));
    manifest.documents[0].version = '1.0';
    writeFileSync(file, JSON.stringify(manifest));
    assert.ok(validateLegalContent(directory).errors.some(error => /new version 1.1/.test(error)));
  });
});
