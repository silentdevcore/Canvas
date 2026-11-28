import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { validateInventory, validateSurface, validateCoverage, surfaceHash } from './importer-surface.mjs';

const inventory = JSON.parse(readFileSync(new URL('../../product-metadata/native-importer-public-surface.v1.json', import.meta.url)));

test('reviewed importer inventory covers all four assemblies and preserves independent acceptance gates', () => {
  validateInventory(inventory);
  assert.equal(inventory.assemblies.reduce((n,a) => n+a.types.length, 0), 363);
  assert.equal(inventory.assemblies.flatMap(a => a.types).reduce((n,t) => n+t.protectedMembers.length, 0), 201);
  for (const patch of [{ category: 'unreviewed' }, { surfaceHash: 'unknown' }, { reason: '' }, { evidence: ['../private'] }, { bypass: true }]) {
    const invalid = structuredClone(inventory); Object.assign(invalid.assemblies[0].types[0], patch);
    assert.throws(() => validateInventory(invalid));
  }
  const duplicate = structuredClone(inventory); duplicate.assemblies[0].types.push(duplicate.assemblies[0].types[0]);
  assert.throws(() => validateInventory(duplicate));
});

function fixture() {
  const actual = { schemaVersion: 1, assemblies: inventory.assemblies.map(assembly => ({ assembly: assembly.assembly,
    types: [{ type: assembly.assembly + '.Synthetic', kind: 'Public', baseType: 'System.Object', isInterface: false,
      interfaces: [], explicitBindings: [], members: ['Method:Void Run()', 'Constructor:Void .ctor()'] }],
    methods: [{ type: assembly.assembly + '.Synthetic', name: 'Run', member: 'Method:Void Run()' }],
  })) };
  const reviewed = { schemaVersion: 1, scope: inventory.scope, assemblies: actual.assemblies.map(assembly => ({ assembly: assembly.assembly,
    types: assembly.types.map(type => ({ type: type.type, category: 'processing', surfaceHash: surfaceHash(type),
      reason: 'Synthetic native method used only by validation contracts.', protectedMembers: ['Method:Void Run()'], freeMethods: [],
      evidence: ['tools/native-licensing/ImporterEngineProbe/Program.cs'] })),
  })) };
  return { actual, reviewed };
}

test('surface guard rejects new types, methods, constructors, property and interface changes', () => {
  const { actual, reviewed } = fixture();
  assert.equal(validateSurface(reviewed, actual).protectedMembers, 4);
  const mutations = [
    value => value.assemblies[0].types.push({ ...value.assemblies[0].types[0], type: 'PVAD.Importer.NewType' }),
    value => value.assemblies[0].types[0].members.push('Method:Void NewProcessing()'),
    value => value.assemblies[0].types[0].members.push('Constructor:Void .ctor(System.IO.Stream)'),
    value => value.assemblies[0].types[0].members.push('Property:System.IO.Stream Content'),
    value => value.assemblies[0].types[0].interfaces.push('PVAD.FileImporter.IFileImporter'),
    value => value.assemblies[0].types[0].explicitBindings.push('Synthetic explicit import'),
    value => value.assemblies[0].types[0].baseType = 'System.IO.Stream',
    value => value.assemblies[0].methods.push({ type: 'PVAD.Importer.Synthetic', name: 'Unclassified', member: 'Method:Void Unclassified()' }),
  ];
  for (const mutate of mutations) { const invalid = structuredClone(actual); mutate(invalid); assert.throws(() => validateSurface(reviewed, invalid)); }
  const misclassified = structuredClone(reviewed); misclassified.assemblies[0].types[0].category = 'utility';
  assert.throws(() => validateSurface(misclassified, actual));
});

test('native coverage requires the exact registered processing members from both completed probes', () => {
  const { reviewed } = fixture();
  const members = reviewed.assemblies.map(a => a.assembly + '\n' + a.types[0].type + '\nMethod:Void Run()');
  const reports = [members.slice(0,2), members.slice(2)].map((testedSurface, index) => {
    const checks = [];
    const phaseEvidence = testedSurface.map((member, id) => {
      const phase = (name, diagnostic) => {
        const check = `${name}:${index}:${id}`; checks.push(check);
        return { check, mode: 'invoked', diagnostic, prerequisite: null };
      };
      return { member, missing: phase('missing', 'PVADLIC001'), incorrect: phase('incorrect', 'PVADLIC012'),
        valid: phase('valid', null) };
    });
    return { checks, testedSurface, phaseEvidence };
  });
  assert.equal(validateCoverage(reviewed, reports).protectedMembers, 4);
  assert.equal(validateCoverage(reviewed, reports).phaseVerifiedMembers, 4);
  assert.equal(validateCoverage(reviewed, reports).allPhasesVerified, true);
  for (const mutate of [
    value => value[0].testedSurface.pop(),
    value => value[0].testedSurface.push('Unknown API'),
    value => value[1].testedSurface.push(members[0]),
    value => value[0].checks = [],
    value => value[1].checks.push(value[1].checks[0]),
    value => delete value[0].phaseEvidence,
    value => value[0].phaseEvidence[0].valid = null,
    value => value[0].phaseEvidence[0].missing.check = 'not-executed',
    value => value[1].phaseEvidence[0].incorrect.diagnostic = 'PVADLIC001',
    value => value[0].phaseEvidence[0].missing.mode = 'assumed',
  ]) { const invalid = structuredClone(reports); mutate(invalid); assert.throws(() => validateCoverage(reviewed, invalid)); }
});

test('only the explicitly unexecuted embedded OCR success may remain pending on other hosts', () => {
  const reports = inventory.assemblies.reduce((result, assembly, index) => {
    const report = result[index < 2 ? 0 : 1];
    for (const type of assembly.types) for (const member of type.protectedMembers) {
      const symbol = `${assembly.assembly}\n${type.type}\n${member}`;
      const phase = (name, diagnostic) => {
        const check = `${name}:${symbol}`; report.checks.push(check);
        return { check, mode: 'invoked', diagnostic, prerequisite: null };
      };
      const pending = type.type === 'PVAD.FileImporter.ImageOcr.EmbeddedTesseractOcrEngine';
      report.testedSurface.push(symbol);
      report.phaseEvidence.push({ member: symbol, missing: phase('missing', 'PVADLIC001'),
        incorrect: phase('incorrect', 'PVADLIC012'), valid: pending ? null : phase('valid', null) });
    }
    return result;
  }, [0, 1].map(() => ({ checks: [], testedSurface: [], phaseEvidence: [], realNativeSucceeded: false })));
  const partial = validateCoverage(inventory, reports);
  assert.equal(partial.phaseVerifiedMembers, 200);
  assert.equal(partial.pendingPositiveMembers.length, 1);
  assert.equal(partial.allPhasesVerified, false);
  const unrelatedGap = structuredClone(reports);
  unrelatedGap[1].phaseEvidence.find(row => row.valid !== null).valid = null;
  assert.throws(() => validateCoverage(inventory, unrelatedGap));
  reports[1].realNativeSucceeded = true;
  assert.throws(() => validateCoverage(inventory, reports));
  const row = reports[1].phaseEvidence.find(row => row.valid === null);
  row.valid = { check: 'actual-embedded-ocr-success', mode: 'invoked', diagnostic: null, prerequisite: null };
  reports[1].checks.push(row.valid.check);
  assert.equal(validateCoverage(inventory, reports).phaseVerifiedMembers, 201);
});
