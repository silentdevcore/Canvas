import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { copyFileSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import test from 'node:test';
import { readFuzzReport } from '../pdf-parser-fuzzer/report.mjs';
import { canonicalPadesId, readPadesCorpus, resolvePadesEvidence } from '../pdf-trust-pades-interop/corpus-contract.mjs';

const root = resolve(import.meta.dirname, '../..');
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const negativeCases = ['revoked', 'untrusted', 'revocation-unavailable', 'expired', 'tampered-lt', 'tampered-lta'];
const suffixes = ['.pdf', '-dss-simple.xml', '-dss-detailed.xml', '-dss-diagnostic.xml'];

function fuzzReport(identity = 'pvad') {
  return { SchemaVersion: 2, Seed: 0x504841, MaximumMutations: 96, MaximumInputBytes: 262144,
    ChildTimeoutMilliseconds: 2000, MaximumCapturedOutputCharacters: 8192,
    Parsers: [identity, 'qpdf'], MutationCount: 1, Cases: [{ Id: 'seed', Sha256: 'a'.repeat(64), Differential: true,
      Outcomes: [{ Parser: identity, Status: 'accepted', ExitCode: 0, DurationMilliseconds: 23, ErrorType: null },
        { Parser: 'qpdf', Status: 'rejected', ExitCode: 2, DurationMilliseconds: 15, ErrorType: null }] }] };
}

for (const identity of ['pvad', 'pxa']) {
  test(`parser reader normalizes ${identity} without modifying source or evidence`, () => {
    const source = fuzzReport(identity);
    source.ExtensionEvidence = { sha256: 'b'.repeat(64) };
    const original = JSON.stringify(source);
    const expected = structuredClone(source);
    expected.Parsers[0] = 'pvad';
    expected.Cases[0].Outcomes[0].Parser = 'pvad';
    assert.deepEqual(readFuzzReport(source), expected);
    assert.equal(JSON.stringify(source), original);
  });
}

test('parser reader rejects alias collisions, unknown outcomes and inconsistent counts or decisions', () => {
  const invalid = [
    report => report.SchemaVersion = 99,
    report => report.Parsers.push('pxa'),
    report => report.Cases[0].Outcomes[0].Parser = 'unknown',
    report => report.Cases[0].Outcomes.push(structuredClone(report.Cases[0].Outcomes[0])),
    report => report.Cases[0].Outcomes.pop(),
    report => report.MutationCount = 2,
    report => report.Cases[0].Differential = false,
    report => report.Cases[0].Outcomes[0].Status = 'skipped',
    report => report.Cases[0].Sha256 = 'invalid',
    report => report.Cases[0].Outcomes[0].DurationMilliseconds = -1,
  ];
  for (const mutate of invalid) {
    const source = fuzzReport();
    mutate(source);
    assert.throws(() => readFuzzReport(source));
  }
});

for (const status of ['accepted', 'crashed', 'timeout']) {
  test(`parser report CLI retains ${status} outcomes and never rewrites the report`, () => {
    const directory = mkdtempSync(resolve(tmpdir(), 'pvad-parser-report-'));
    try {
      const file = resolve(directory, 'report.json');
      const report = fuzzReport('pxa');
      report.Cases[0].Outcomes[0].Status = status;
      report.Cases[0].Differential = status === 'accepted';
      const bytes = JSON.stringify(report);
      writeFileSync(file, bytes);
      const run = spawnSync(process.execPath, [resolve(root, 'tools/pdf-parser-fuzzer/report.mjs'), file], { encoding: 'utf8' });
      assert.equal(run.status, status === 'accepted' ? 0 : 1, run.stderr);
      assert.deepEqual(JSON.parse(run.stdout), { cases: 1, parsers: ['pvad', 'qpdf'], failures: status === 'accepted' ? 0 : 1 });
      assert.equal(readFileSync(file, 'utf8'), bytes);
    } finally { rmSync(directory, { recursive: true, force: true }); }
  });
}

function padesCorpus(identity) {
  return { schemaVersion: 1, generator: `${identity.toUpperCase()} PdfPadesSigningService`,
    fixtures: ['b-t', 'b-lt', 'b-lta'].map((suffix, index) => ({
      id: `${identity}-pades-${suffix}`, pdf: `${identity}-pades-${suffix}.pdf`,
      sha256: 'b'.repeat(64), fileSizeBytes: 321, requestedProfile: ['BaselineT', 'BaselineLT', 'BaselineLTA'][index],
      [`${identity}ClassifiedProfile`]: ['B-T', 'B-LT', 'B-LTA'][index],
    })), negativeFixtures: negativeCases.map(suffix => ({ id: `${identity}-pades-${suffix}` })) };
}

for (const identity of ['pvad', 'pxa']) {
  test(`generated PAdES reader accepts ${identity} while preserving file names and hashes`, () => {
    const source = padesCorpus(identity);
    const original = JSON.stringify(source);
    const result = readPadesCorpus(source);
    assert.equal(result.generator, 'PVAD PdfPadesSigningService');
    result.fixtures.forEach((fixture, index) => {
      assert.match(fixture.id, /^pvad-pades-/);
      assert.equal(fixture.pdf, source.fixtures[index].pdf);
      assert.equal(fixture.sha256, source.fixtures[index].sha256);
      assert.equal(fixture.pvadClassifiedProfile, ['B-T', 'B-LT', 'B-LTA'][index]);
      assert.equal(Object.hasOwn(fixture, 'pxaClassifiedProfile'), false);
    });
    assert.ok(result.negativeFixtures.every(fixture => fixture.id.startsWith('pvad-pades-')));
    assert.equal(JSON.stringify(source), original);
  });
}

test('generated PAdES reader accepts identical profiles and rejects conflicting or missing profiles', () => {
  const source = padesCorpus('pxa');
  source.fixtures[0].pvadClassifiedProfile = 'B-T';
  assert.equal(readPadesCorpus(source).fixtures[0].pvadClassifiedProfile, 'B-T');
  source.fixtures[0].pvadClassifiedProfile = 'B-LTA';
  assert.throws(() => readPadesCorpus(source), /Conflicting classified/);
  delete source.fixtures[0].pvadClassifiedProfile;
  delete source.fixtures[0].pxaClassifiedProfile;
  assert.throws(() => readPadesCorpus(source), /Missing classified/);
  for (const id of ['pvad-pades-unknown', '../pvad-pades-b-t', 'other-pades-b-t'])
    assert.throws(() => canonicalPadesId(id), /Unknown/);
});

for (const identity of ['pvad', 'pxa']) {
  test(`negative manifest reads complete ${identity} evidence and keeps original bytes and hashes`, () => {
    const directory = mkdtempSync(resolve(tmpdir(), 'pvad-negative-corpus-'));
    const evidence = resolve(root, 'websites/PVAD.Documentation/public/downloads/pdf-trust/pades/negative');
    const manifestBytes = readFileSync(resolve(evidence, 'negative-corpus.json'));
    const original = JSON.parse(manifestBytes);
    try {
      for (const entry of original.entries) {
        const canonical = canonicalPadesId(entry.id);
        for (const suffix of suffixes)
          copyFileSync(resolve(evidence, `${entry.id}${suffix}`), resolve(directory, `${canonical.replace(/^pvad-/, `${identity}-`)}${suffix}`));
      }
      for (const item of original.trustEvidence)
        copyFileSync(resolve(evidence, item.file), resolve(directory, item.file));
      const run = spawnSync(process.execPath, [resolve(root, 'tools/pdf-trust-pades-interop/build-negative-manifest.mjs'), directory], { encoding: 'utf8' });
      assert.equal(run.status, 0, run.stderr);
      const result = JSON.parse(readFileSync(resolve(directory, 'negative-corpus.json'), 'utf8'));
      assert.match(result.description, /PVAD/);
      result.entries.forEach((entry, index) => {
        assert.equal(entry.id, canonicalPadesId(original.entries[index].id));
        assert.equal(entry.expectedSubIndication, original.entries[index].expectedSubIndication);
        for (const descriptor of [entry.pdf, ...Object.values(entry.reports)]) {
          assert.match(descriptor.file, new RegExp(`^${identity}-pades-`));
          assert.equal(hash(readFileSync(resolve(directory, descriptor.file))), descriptor.sha256);
        }
        assert.equal(entry.pdf.sha256, original.entries[index].pdf.sha256);
      });
      const duplicate = `${result.entries[0].id.replace(/^pvad-/, identity === 'pvad' ? 'pxa-' : 'pvad-')}.pdf`;
      copyFileSync(resolve(directory, result.entries[0].pdf.file), resolve(directory, duplicate));
      const ambiguous = spawnSync(process.execPath, [resolve(root, 'tools/pdf-trust-pades-interop/build-negative-manifest.mjs'), directory], { encoding: 'utf8' });
      assert.notEqual(ambiguous.status, 0);
      assert.match(ambiguous.stderr, /ambiguous evidence generation/);
      assert.deepEqual(readFileSync(resolve(evidence, 'negative-corpus.json')), manifestBytes);
    } finally { rmSync(directory, { recursive: true, force: true }); }
  });
}

test('PAdES evidence reader rejects incomplete generations without mixing them', () => {
  const directory = mkdtempSync(resolve(tmpdir(), 'pvad-evidence-reader-'));
  try {
    assert.throws(() => resolvePadesEvidence(directory, 'pvad-pades-revoked', suffixes), /Missing/);
    writeFileSync(resolve(directory, 'pvad-pades-revoked.pdf'), 'synthetic');
    assert.throws(() => resolvePadesEvidence(directory, 'pvad-pades-revoked', suffixes), /Incomplete/);
    writeFileSync(resolve(directory, 'pxa-pades-revoked-dss-simple.xml'), 'synthetic');
    assert.throws(() => resolvePadesEvidence(directory, 'pvad-pades-revoked', suffixes), /ambiguous/);
    assert.throws(() => resolvePadesEvidence(directory, 'pvad-pades-revoked', ['../../secret']), /Invalid evidence/);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('current parser and corpus producers write canonical identities and retain only deliberate contracts', () => {
  const read = file => readFileSync(resolve(root, file), 'utf8');
  const fuzzer = read('tools/pdf-parser-fuzzer/Program.cs');
  assert.doesNotMatch(fuzzer, /new ParserOutcome\("pxa"|Parsers: new\[\] \{ "pxa"/);
  assert.match(fuzzer, /Parsers: new\[\] \{ "pvad"/);
  assert.match(read('tools/pdf-parser-fuzzer/run.sh'), /node tools\/pdf-parser-fuzzer\/report\.mjs/);
  const pades = read('tools/pdf-trust-pades-interop/Program.cs').replace(/^#pragma warning (?:disable|restore) PXA0001$/gm, '');
  assert.doesNotMatch(pades, /pxa/i);
  assert.match(pades, /\["pvadClassifiedProfile"\] = fixture.PvadClassifiedProfile/);
  assert.match(read('tools/pdf-trust-pades-interop/generate-and-validate.sh'), /pvad-pades-\$profile\.pdf/);
  assert.doesNotMatch(read('tools/pdf-trust-external-corpus/sources/trust-profile.html'), /pxa/i);
  const external = read('tools/pdf-trust-external-corpus/generate-external-profiles.sh');
  assert.match(external, /CN=PVAD External Corpus Signer/);
  assert.match(external, /USER_PASSWORD="PXA-reader/); // Existing encrypted fixture credentials are not renamed.
  for (const file of ['tools/pdf-parser-fuzzer/run.sh', 'tools/pdf-trust-pades-interop/generate-and-validate.sh',
    'tools/pdf-trust-external-corpus/generate-external-profiles.sh'])
    assert.equal(spawnSync('bash', ['-n', resolve(root, file)], { encoding: 'utf8' }).status, 0);
});
