import assert from 'node:assert/strict';
import { mkdir, mkdtemp, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { classifyResult, evaluate, fromVisualParity, fromWordFidelity, mergeEvidence, validateManifest } from './fidelity.mjs';

const policy = {
  maximumGeometryDriftPoints: 1,
  minimumTextPreservationRatio: 1,
  maximumVisualDifferenceRatio: 0.15,
  minimumAutomaticMigrationPercent: 95
};

async function fixtureRoot() {
  const root = await mkdtemp(path.join(os.tmpdir(), 'pxa-fidelity-'));
  await mkdir(path.join(root, 'fixtures'));
  await writeFile(path.join(root, 'fixtures/source.pdf'), '%PDF-test');
  return root;
}

function fixture(overrides = {}) {
  return {
    id: 'pdf-import', domain: 'pdf', workflow: 'import-roundtrip', sourceFormat: 'pdf',
    source: 'fixtures/source.pdf', provenance: 'public-real-world', redistribution: 'allowed',
    realWorld: true, officiallySupported: true, tags: ['invoice'], ...overrides
  };
}

function manifest(fixtures = [fixture()]) {
  return { schemaVersion: 1, policy, fixtures };
}

function result(overrides = {}) {
  return {
    fixtureId: 'pdf-import',
    metrics: {
      sourceElements: 10, matchedElements: 10, missingElements: 0, duplicateElements: 0,
      maximumGeometryDriftPoints: 0, textPreservationRatio: 1,
      maximumVisualDifferenceRatio: 0, pageCountMatches: true, ...overrides
    },
    limitations: []
  };
}

test('classifies exact, compatible, review-required, and unsupported evidence honestly', () => {
  const item = fixture();
  assert.equal(classifyResult(item, result(), policy), 'exact');
  assert.equal(classifyResult(item, result({ maximumGeometryDriftPoints: 0.5 }), policy), 'compatible');
  assert.equal(classifyResult(item, result({ missingElements: 1 }), policy), 'reviewRequired');
  assert.equal(classifyResult(
    fixture({ workflow: 'standards-validation', officiallySupported: false }),
    { ...result(), forcedClassification: 'unsupported' }, policy), 'unsupported');
});

test('does not infer exact fidelity from omitted measurements or allow a forced positive migration result', () => {
  assert.equal(classifyResult(fixture(), {
    fixtureId: 'pdf-import',
    metrics: { sourceElements: 1, matchedElements: 1, missingElements: 0, duplicateElements: 0 },
    limitations: []
  }, policy), 'compatible');
  assert.throws(() => classifyResult(fixture(), { ...result(), forcedClassification: 'exact' }, policy), /positive fidelity/);
  assert.equal(classifyResult(fixture(), { ...result(), forcedClassification: 'unsupported' }, policy), 'unsupported');
});

test('rejects duplicate fixture ids and missing sources', async () => {
  const root = await fixtureRoot();
  assert.throws(() => validateManifest(manifest([fixture(), fixture()]), root), /Duplicate fixture id/);
  assert.throws(() => validateManifest(manifest([fixture({ source: 'fixtures/missing.pdf' })]), root), /does not exist/);
});

test('rejects synthetic or non-migration fixtures from the official denominator', async () => {
  const root = await fixtureRoot();
  assert.throws(() => validateManifest(manifest([fixture({ realWorld: false })]), root), /synthetic evidence/);
  assert.throws(() => validateManifest(manifest([fixture({ workflow: 'render-parity' })]), root), /not a migration workflow/);
});

test('rejects unknown domain, workflow, provenance, and redistribution values', async () => {
  const root = await fixtureRoot();
  assert.throws(() => validateManifest(manifest([fixture({ domain: 'slides' })]), root), /invalid domain/);
  assert.throws(() => validateManifest(manifest([fixture({ workflow: 'magic' })]), root), /invalid workflow/);
  assert.throws(() => validateManifest(manifest([fixture({ provenance: 'unknown' })]), root), /invalid provenance/);
  assert.throws(() => validateManifest(manifest([fixture({ redistribution: 'maybe' })]), root), /invalid redistribution/);
});

test('reports notMeasured rather than 100 percent for an empty official denominator', async () => {
  const root = await fixtureRoot();
  const input = manifest([fixture({ officiallySupported: false, realWorld: false, provenance: 'pxa-synthetic' })]);
  const report = evaluate(input, { schemaVersion: 1, results: [result()] }, root);
  assert.equal(report.summary.automaticMigrationPercent, 'notMeasured');
  assert.equal(report.summary.targetMet, 'notMeasured');
});

test('calculates the official automatic migration percentage', async () => {
  const root = await fixtureRoot();
  await writeFile(path.join(root, 'fixtures/second.pdf'), '%PDF-second');
  const input = manifest([
    fixture(),
    fixture({ id: 'pdf-second', source: 'fixtures/second.pdf' })
  ]);
  const evidence = {
    schemaVersion: 1,
    results: [result(), { ...result({ matchedElements: 9, missingElements: 1 }), fixtureId: 'pdf-second', limitations: ['One element requires review.'] }]
  };
  const report = evaluate(input, evidence, root);
  assert.equal(report.summary.automaticMigrationPercent, 50);
  assert.equal(report.summary.targetMet, false);
});

test('requires limitations for review findings and consistent official counts', async () => {
  const root = await fixtureRoot();
  assert.throws(() => evaluate(manifest(), {
    schemaVersion: 1,
    results: [result({ matchedElements: 9, missingElements: 1 })]
  }, root), /requires a limitation/);
  assert.throws(() => evaluate(manifest(), {
    schemaVersion: 1,
    results: [{ ...result(), metrics: { ...result().metrics, matchedElements: 8 } }]
  }, root), /inconsistent/);
});

test('adapts live visual parity evidence', () => {
  const adapted = fromVisualParity({
    summary: { elements: 2 },
    elements: [
      { geometry: { maxDrift: 0.25 }, comparisons: { preview: { perceptualRmse: 0.02 } } },
      { geometry: { maxDrift: 0.5 }, comparisons: { preview: { perceptualRmse: 0.04 } } }
    ]
  });
  assert.equal(adapted.metrics.maximumGeometryDriftPoints, 0.5);
  assert.equal(adapted.metrics.maximumVisualDifferenceRatio, 0.04);
  assert.equal(adapted.metrics.missingElements, 0);
});

test('adapts live Word evidence and replaces its reviewed baseline', () => {
  const adapted = fromWordFidelity([
    { ConvertedPdfGenerated: true, WidthDriftPt: 0.3, HeightDriftPt: 0.1, PixelDiffRatio: 0.08 },
    { ConvertedPdfGenerated: false, WidthDriftPt: null, HeightDriftPt: null, PixelDiffRatio: null }
  ]);
  assert.equal(adapted.metrics.matchedElements, 1);
  assert.equal(adapted.metrics.missingElements, 1);
  assert.equal(adapted.metrics.maximumVisualDifferenceRatio, 0.08);

  const merged = mergeEvidence(
    { schemaVersion: 1, results: [result(), { ...result(), fixtureId: 'word-fidelity-sample-pack' }] },
    [adapted]
  );
  assert.equal(merged.results.filter(item => item.fixtureId === 'word-fidelity-sample-pack').length, 1);
  assert.equal(merged.results.find(item => item.fixtureId === 'word-fidelity-sample-pack').metrics.missingElements, 1);
});
