import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { canonicalPadesId, readPadesCorpus, resolvePadesEvidence } from './corpus-contract.mjs';

const toolDirectory = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(toolDirectory, '../..');
const published = path.join(root, 'websites/PVAD.Documentation/public/downloads/pdf-trust/pades');
const suffixes = ['.pdf', '-dss-simple.xml', '-dss-detailed.xml', '-dss-diagnostic.xml'];
const original = 'pxa-pades-';

function temporaryDirectory(t) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'pvad-pades-contract-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  return directory;
}

function write(directory, prefix, selected = suffixes) {
  for (const suffix of selected) fs.writeFileSync(path.join(directory, `${prefix}${suffix}`), prefix);
}

const sha256 = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');

test('original case IDs normalize to the current IDs and unknown IDs are rejected', () => {
  assert.equal(canonicalPadesId(`${original}revoked`), 'pvad-pades-revoked');
  assert.equal(canonicalPadesId('pvad-pades-b-lta'), 'pvad-pades-b-lta');
  assert.throws(() => canonicalPadesId('pvad-pades-unknown'), /Unknown PAdES case ID/);
  assert.throws(() => canonicalPadesId('other-pades-b-t'), /Unknown PAdES case ID/);
});

test('current-only, original-only, missing, incomplete and mixed generations', t => {
  const current = temporaryDirectory(t);
  write(current, 'pvad-pades-revoked');
  assert.deepEqual(resolvePadesEvidence(current, 'pvad-pades-revoked', suffixes),
    suffixes.map(suffix => `pvad-pades-revoked${suffix}`));

  const retained = temporaryDirectory(t);
  write(retained, `${original}revoked`);
  assert.deepEqual(resolvePadesEvidence(retained, 'pvad-pades-revoked', suffixes),
    suffixes.map(suffix => `${original}revoked${suffix}`));
  assert.deepEqual(resolvePadesEvidence(retained, `${original}revoked`, suffixes),
    suffixes.map(suffix => `${original}revoked${suffix}`));

  assert.throws(() => resolvePadesEvidence(temporaryDirectory(t), 'pvad-pades-revoked', suffixes),
    /Missing or ambiguous evidence generation/);

  const incomplete = temporaryDirectory(t);
  write(incomplete, 'pvad-pades-revoked', suffixes.slice(0, 3));
  assert.throws(() => resolvePadesEvidence(incomplete, 'pvad-pades-revoked', suffixes),
    /Incomplete evidence generation/);

  // Current PDF with original XML must never combine into one generation.
  const mixed = temporaryDirectory(t);
  write(mixed, 'pvad-pades-revoked', ['.pdf']);
  write(mixed, `${original}revoked`, suffixes.slice(1));
  assert.throws(() => resolvePadesEvidence(mixed, 'pvad-pades-revoked', suffixes),
    /Missing or ambiguous evidence generation/);

  // Two complete generations are ambiguous; neither is preferred implicitly.
  const both = temporaryDirectory(t);
  write(both, 'pvad-pades-revoked');
  write(both, `${original}revoked`);
  assert.throws(() => resolvePadesEvidence(both, 'pvad-pades-revoked', suffixes),
    /Missing or ambiguous evidence generation/);

  assert.throws(() => resolvePadesEvidence(current, 'pvad-pades-revoked', ['.txt']), /Invalid evidence suffixes/);
});

// Generator manifests in the current and the original generation, built from the published fixtures.
function generatorManifest(generation) {
  const positive = JSON.parse(fs.readFileSync(path.join(published, 'pades-corpus.json'), 'utf8'));
  const profiles = { 'b-t': 'B-T', 'b-lt': 'B-LT', 'b-lta': 'B-LTA' };
  const prefix = generation === 'current' ? 'pvad-pades-' : original;
  const profileKey = generation === 'current' ? 'pvadClassifiedProfile' : `${'pxa'}ClassifiedProfile`;
  return {
    schemaVersion: 1,
    generator: generation === 'current' ? 'PVAD PdfPadesSigningService' : `${'PXA'} PdfPadesSigningService`,
    fixtures: positive.entries.map(entry => {
      const name = entry.id.replace(/^[a-z]+-pades-/, '');
      return { id: `${prefix}${name}`, [profileKey]: profiles[name], pdf: `${prefix}${name}.pdf`,
        sha256: entry.sha256, fileSizeBytes: entry.fileSizeBytes };
    }),
    negativeFixtures: ['revoked', 'untrusted', 'revocation-unavailable', 'expired', 'tampered-lt', 'tampered-lta']
      .map(name => ({ id: `${prefix}${name}` })),
  };
}

for (const generation of ['current', 'original']) {
  test(`reading a ${generation} corpus keeps classified profiles and leaves the input unchanged`, () => {
    const source = generatorManifest(generation);
    const before = structuredClone(source);
    const corpus = readPadesCorpus(source);
    assert.deepEqual(source, before);
    assert.equal(corpus.generator, 'PVAD PdfPadesSigningService');
    assert.deepEqual(corpus.fixtures.map(fixture => fixture.id), ['pvad-pades-b-t', 'pvad-pades-b-lt', 'pvad-pades-b-lta']);
    assert.deepEqual(corpus.fixtures.map(fixture => fixture.pvadClassifiedProfile), ['B-T', 'B-LT', 'B-LTA']);
    assert.ok(corpus.fixtures.every(fixture => !Object.hasOwn(fixture, `${'pxa'}ClassifiedProfile`)));
    assert.deepEqual(corpus.fixtures.map(fixture => fixture.pdf), before.fixtures.map(fixture => fixture.pdf),
      'Signed fixture paths are not renamed.');
    assert.equal(corpus.negativeFixtures[0].id, 'pvad-pades-revoked');
  });
}

test('profile normalization accepts equal duplicates and rejects conflicts, missing profiles and unknown generators', () => {
  const duplicate = generatorManifest('original');
  duplicate.fixtures[0].pvadClassifiedProfile = 'B-T';
  assert.equal(readPadesCorpus(duplicate).fixtures[0].pvadClassifiedProfile, 'B-T');
  const conflicting = generatorManifest('original');
  conflicting.fixtures[0].pvadClassifiedProfile = 'B-LTA';
  assert.throws(() => readPadesCorpus(conflicting), /Conflicting classified profiles/);
  const missing = generatorManifest('current');
  delete missing.fixtures[0].pvadClassifiedProfile;
  assert.throws(() => readPadesCorpus(missing), /Missing classified profile/);
  const unknown = generatorManifest('current');
  unknown.generator = 'Other PdfPadesSigningService';
  assert.throws(() => readPadesCorpus(unknown), /Unknown corpus generator/);
  const mismatched = generatorManifest('original');
  mismatched.fixtures[0].pdf = 'pvad-pades-b-t.pdf';
  assert.throws(() => readPadesCorpus(mismatched), /Fixture path does not match its ID/);
});

test('the negative manifest builder reproduces the published manifest from retained evidence', t => {
  const negative = path.join(published, 'negative');
  const copy = temporaryDirectory(t);
  const hashes = new Map();
  for (const file of fs.readdirSync(negative)) {
    fs.copyFileSync(path.join(negative, file), path.join(copy, file));
    hashes.set(file, sha256(path.join(negative, file)));
  }
  fs.rmSync(path.join(copy, 'negative-corpus.json'));
  execFileSync(process.execPath, [path.join(toolDirectory, 'build-negative-manifest.mjs'), copy], { stdio: 'pipe' });

  const built = JSON.parse(fs.readFileSync(path.join(copy, 'negative-corpus.json'), 'utf8'));
  const expected = JSON.parse(fs.readFileSync(path.join(negative, 'negative-corpus.json'), 'utf8'));
  // Rebuilt entries carry current case IDs but reference the retained original files byte for byte.
  assert.deepEqual(built.entries.map(entry => entry.id), expected.entries.map(entry => canonicalPadesId(entry.id)));
  assert.deepEqual(built.entries.map(({ id, ...entry }) => entry), expected.entries.map(({ id, ...entry }) => entry));
  assert.deepEqual(built.trustEvidence, expected.trustEvidence);
  for (const [file, hash] of hashes) {
    if (file !== 'negative-corpus.json') assert.equal(sha256(path.join(copy, file)), hash, `${file} changed`);
    assert.equal(sha256(path.join(negative, file)), hash, `published ${file} changed`);
  }
});
