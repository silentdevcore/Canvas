import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const cases = new Set(['b-t', 'b-lt', 'b-lta', 'revoked', 'untrusted',
  'revocation-unavailable', 'expired', 'tampered-lt', 'tampered-lta']);
const evidenceSuffixes = new Set(['.pdf', '-dss-simple.xml', '-dss-detailed.xml', '-dss-diagnostic.xml']);

export function canonicalPadesId(id) {
  assert.equal(typeof id, 'string', 'Missing PAdES case ID.');
  const canonical = id.replace(/^pxa-pades-/, 'pvad-pades-');
  assert.ok(canonical.startsWith('pvad-pades-') && cases.has(canonical.slice(11)), 'Unknown PAdES case ID.');
  return canonical;
}

export function readPadesCorpus(source) {
  assert.equal(source?.schemaVersion, 1, 'Unsupported PAdES corpus schema.');
  const corpus = structuredClone(source);
  assert.ok(['PVAD PdfPadesSigningService', 'PXA PdfPadesSigningService'].includes(corpus.generator), 'Unknown corpus generator.');
  corpus.generator = 'PVAD PdfPadesSigningService';
  assert.ok(Array.isArray(corpus.fixtures) && corpus.fixtures.length === 3, 'Missing positive fixtures.');
  const ids = new Set();
  for (const fixture of corpus.fixtures) {
    const originalId = fixture.id;
    fixture.id = canonicalPadesId(originalId);
    assert.ok(['pvad-pades-b-t', 'pvad-pades-b-lt', 'pvad-pades-b-lta'].includes(fixture.id) &&
      !ids.has(fixture.id), 'Invalid or duplicate positive fixture.');
    ids.add(fixture.id);
    assert.equal(fixture.pdf, `${originalId}.pdf`, 'Fixture path does not match its ID.');
    assert.match(fixture.sha256, /^[a-f0-9]{64}$/);
    assert.ok(Number.isSafeInteger(fixture.fileSizeBytes) && fixture.fileSizeBytes > 0, 'Invalid fixture length.');
    const current = Object.hasOwn(fixture, 'pvadClassifiedProfile');
    const previous = Object.hasOwn(fixture, 'pxaClassifiedProfile');
    assert.ok(current || previous, 'Missing classified profile.');
    if (current && previous)
      assert.equal(fixture.pvadClassifiedProfile, fixture.pxaClassifiedProfile, 'Conflicting classified profiles.');
    fixture.pvadClassifiedProfile = current ? fixture.pvadClassifiedProfile : fixture.pxaClassifiedProfile;
    assert.ok(fixture.pvadClassifiedProfile === null || ['B-T', 'B-LT', 'B-LTA'].includes(fixture.pvadClassifiedProfile), 'Invalid classified profile.');
    delete fixture.pxaClassifiedProfile;
  }
  assert.ok(Array.isArray(corpus.negativeFixtures) && corpus.negativeFixtures.length === 6, 'Missing negative fixtures.');
  for (const fixture of corpus.negativeFixtures) {
    fixture.id = canonicalPadesId(fixture.id);
    assert.ok(!ids.has(fixture.id), 'Invalid or duplicate negative fixture.');
    ids.add(fixture.id);
  }
  return corpus;
}

// Select one complete generation; never combine current PDFs with older XML evidence.
export function resolvePadesEvidence(directory, id, suffixes) {
  const canonical = canonicalPadesId(id);
  assert.ok(Array.isArray(suffixes) && suffixes.length > 0 &&
    suffixes.every(suffix => evidenceSuffixes.has(suffix)), 'Invalid evidence suffixes.');
  const families = [canonical, canonical.replace(/^pvad-pades-/, 'pxa-pades-')].map(prefix => {
    const files = suffixes.map(suffix => `${prefix}${suffix}`);
    const present = files.map(file => existsSync(resolve(directory, file)));
    return { files, present };
  });
  const active = families.filter(family => family.present.some(Boolean));
  assert.equal(active.length, 1, 'Missing or ambiguous evidence generation.');
  assert.ok(active[0].present.every(Boolean), 'Incomplete evidence generation.');
  return active[0].files;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    assert.equal(process.argv.length, 3, 'Specify one generated corpus manifest.');
    const corpus = readPadesCorpus(JSON.parse(readFileSync(process.argv[2], 'utf8')));
    console.log(JSON.stringify({ fixtures: corpus.fixtures.length, negativeFixtures: corpus.negativeFixtures.length }));
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
