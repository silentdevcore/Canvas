import assert from 'node:assert/strict';
import { test } from 'node:test';
import { validateAdmissionMatrix } from './admission-matrix.mjs';

// Synthetic report shapes test the validator, not execution of native APIs.
function fixture(product) {
  if (product === 'converter') {
    const rows = Array.from({ length: 9 }, (_, index) => {
      const implementation = `PVAD.Infrastructure.Converters.Test${String.fromCharCode(65 + index)}DocumentExporter`;
      const member = (type, count) => `${type}\nMethod:Byte[] Export(PVAD.Core.Contracts.DesignExportDto${count === 2 ? ', PVAD.Core.Contracts.ExportOptions' : ''})`;
      return [
        ...Array.from({ length: index < 4 ? 2 : 1 }, (_, i) => ({ member: member(implementation, i + 1), dispatch: 'direct', implementation })),
        ...[1, 2].map(count => ({ member: member('PVAD.Core.Abstractions.IDocumentExporter', count), dispatch: 'interface', implementation })),
      ];
    }).flat().map(row => ({ ...row, phases: phases() }));
    return { admissionMatrix: rows, exporters: 9, publicExportMethods: 13, interfaceCalls: 18 };
  }
  const dispatches = product === 'pdf'
    ? ['direct', 'facade', 'direct', 'direct', 'direct', 'direct', 'direct', 'interface']
    : product === 'spreadsheet' ? [...Array(40).fill('direct'), 'facade', 'interface', 'interface']
      : ['direct', 'direct', 'interface', 'interface', 'facade', 'direct'];
  const constructor = 'PVAD.Tests.Document\nConstructor:Void .ctor()';
  const constructorIndex = product === 'spreadsheet' ? 13 : 0;
  return { admissionMatrix: dispatches.map((dispatch, index) => ({
    member: ['pdf', 'spreadsheet'].includes(product) && index === constructorIndex ? constructor : `PVAD.Tests.Entry${index}\nMethod:Void Run()`,
    dispatch,
    phases: phases(product === 'pdf' && index >= 2 || product === 'spreadsheet' && index >= 14 && index < 40 ? constructor : null),
  })), ...(product === 'spreadsheet' ? { protectedMethods: 40 } : {}) };
}

function phases(constructor = null) {
  const denied = diagnostic => ({ mode: 'invoked', outcome: 'denied', diagnostic, unchanged: true, prerequisite: null });
  return { missing: constructor ? { ...denied('PVADLIC001'), mode: 'constructorPrerequisite', prerequisite: constructor }
    : denied('PVADLIC001'), incorrect: denied('PVADLIC012'), valid: { mode: 'invoked', outcome: 'accepted' } };
}

test('reports direct calls separately from licensed-constructor prerequisite denials', () => {
  const pdf = validateAdmissionMatrix(fixture('pdf'), 'pdf');
  assert.equal(pdf.phases, 24);
  assert.equal(pdf.directlyInvokedPhases, 18);
  assert.equal(pdf.missingConstructorPrerequisites, 6);
  const word = validateAdmissionMatrix(fixture('word'), 'word');
  assert.equal(word.phases, 18);
  assert.equal(word.directlyInvokedPhases, 18);
});

test('Spreadsheet distinguishes retained-builder prerequisites from direct backend and interface calls', () => {
  const result = validateAdmissionMatrix(fixture('spreadsheet'), 'spreadsheet');
  assert.equal(result.entries, 43);
  assert.equal(result.phases, 129);
  assert.equal(result.directlyInvokedPhases, 103);
  assert.equal(result.missingConstructorPrerequisites, 26);
});

test('Converter preserves each concrete implementation behind the shared interface', () => {
  const result = validateAdmissionMatrix(fixture('converter'), 'converter');
  assert.equal(result.entries, 31);
  assert.equal(result.phases, 93);
  assert.equal(result.directlyInvokedPhases, 93);
  assert.equal(result.missingConstructorPrerequisites, 0);
});

test('Converter rejects missing implementation identity, redistributed interfaces and inconsistent counts', () => {
  const absent = fixture('converter');
  delete absent.admissionMatrix[0].implementation;
  assert.throws(() => validateAdmissionMatrix(absent, 'converter'));
  const redistributed = fixture('converter');
  const interfaces = redistributed.admissionMatrix.filter(row => row.dispatch === 'interface');
  interfaces[0].implementation = interfaces[2].implementation;
  assert.throws(() => validateAdmissionMatrix(redistributed, 'converter'));
  const wrongCount = fixture('converter');
  wrongCount.publicExportMethods = 12;
  assert.throws(() => validateAdmissionMatrix(wrongCount, 'converter'));
});

test('Spreadsheet cannot claim additional protected members without phase evidence', () => {
  const report = fixture('spreadsheet');
  report.protectedMethods = 41;
  assert.throws(() => validateAdmissionMatrix(report, 'spreadsheet'));
  report.protectedMethods = 40;
  report.admissionMatrix[14].phases.incorrect.mode = 'constructorPrerequisite';
  assert.throws(() => validateAdmissionMatrix(report, 'spreadsheet'));
});

for (const [name, mutate] of [
  ['missing phase', report => { delete report.admissionMatrix[0].phases.incorrect; }],
  ['duplicate entry', report => { report.admissionMatrix[1] = structuredClone(report.admissionMatrix[0]); }],
  ['changed input/output', report => { report.admissionMatrix[0].phases.missing.unchanged = false; }],
  ['wrong diagnostic', report => { report.admissionMatrix[0].phases.incorrect.diagnostic = 'PVADLIC001'; }],
  ['missing constructor reference', report => { report.admissionMatrix[2].phases.missing.prerequisite = 'unknown'; }],
  ['uninvoked wrong-product phase', report => { report.admissionMatrix[2].phases.incorrect.mode = 'constructorPrerequisite'; }],
  ['fake accepted phase', report => { report.admissionMatrix[0].phases.valid.mode = 'assumed'; }],
  ['unreviewed count', report => { report.admissionMatrix.pop(); }],
]) test(`rejects ${name}`, () => {
  const report = fixture('pdf');
  mutate(report);
  assert.throws(() => validateAdmissionMatrix(report, 'pdf'));
});

test('rejects old reports without phase evidence and unknown scopes', () => {
  assert.throws(() => validateAdmissionMatrix({ checks: ['old-success'] }, 'pdf'));
  assert.throws(() => validateAdmissionMatrix(fixture('pdf'), 'importer'));
});

test('Word cannot substitute prerequisite assumptions for real missing-license calls', () => {
  const report = fixture('word');
  report.admissionMatrix[0].phases.missing.mode = 'constructorPrerequisite';
  assert.throws(() => validateAdmissionMatrix(report, 'word'));
});
