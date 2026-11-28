import assert from 'node:assert/strict';

const scopes = {
  pdf: { entries: 8, prerequisites: 6, dispatches: { direct: 6, facade: 1, interface: 1 } },
  word: { entries: 6, prerequisites: 0, dispatches: { direct: 3, facade: 1, interface: 2 } },
  spreadsheet: { entries: 43, prerequisites: 26, dispatches: { direct: 40, facade: 1, interface: 2 } },
  converter: { entries: 31, prerequisites: 0, dispatches: { direct: 13, facade: 0, interface: 18 } },
};

export function validateAdmissionMatrix(report, product) {
  const scope = scopes[product];
  assert.ok(scope, 'Unknown admission scope.');
  const rows = report.admissionMatrix;
  assert.ok(Array.isArray(rows), 'Missing per-entry admission evidence.');
  assert.equal(rows.length, scope.entries, 'Unreviewed admission entry count.');
  const keys = rows.map(row => `${row.member}\n${row.dispatch}\n${row.implementation ?? ''}`);
  assert.equal(new Set(keys).size, rows.length, 'Duplicate admission evidence.');
  const dispatches = { direct: 0, facade: 0, interface: 0 };
  let prerequisites = 0;
  const implementations = new Map();
  for (const row of rows) {
    assert.equal(typeof row.member, 'string');
    assert.match(row.member, /^PVAD\.[^\n]+\n(?:Constructor|Method):[^\n]+$/);
    assert.ok(Object.hasOwn(dispatches, row.dispatch), 'Unknown dispatch.');
    dispatches[row.dispatch]++;
    if (product === 'converter') {
      assert.match(row.implementation, /^PVAD\.Infrastructure\.Converters\.[A-Za-z]+DocumentExporter$/);
      const counts = implementations.get(row.implementation) ?? { direct: 0, interface: 0 };
      assert.ok(Object.hasOwn(counts, row.dispatch));
      counts[row.dispatch]++;
      implementations.set(row.implementation, counts);
      if (row.dispatch === 'interface') assert.ok(row.member.startsWith('PVAD.Core.Abstractions.IDocumentExporter\n'));
      else assert.ok(row.member.startsWith(row.implementation + '\n'));
    } else assert.equal(row.implementation ?? null, null, 'Unexpected implementation qualifier.');
    assert.deepEqual(Object.keys(row.phases).sort(), ['incorrect', 'missing', 'valid']);
    assert.deepEqual(row.phases.valid, { mode: 'invoked', outcome: 'accepted' });
    for (const [phase, diagnostic] of [['missing', 'PVADLIC001'], ['incorrect', 'PVADLIC012']]) {
      const result = row.phases[phase];
      assert.equal(result.outcome, 'denied');
      assert.equal(result.diagnostic, diagnostic);
      assert.equal(result.unchanged, true, 'Denial lacks unchanged input/output evidence.');
      if (result.mode === 'constructorPrerequisite') {
        assert.equal(phase, 'missing', 'Wrong-product checks must invoke retained instances.');
        assert.ok(['pdf', 'spreadsheet'].includes(product));
        assert.ok(row.member.includes('\nMethod:'), 'A constructor must be invoked directly.');
        const prerequisite = rows.find(candidate => candidate.member === result.prerequisite && candidate.dispatch === 'direct');
        assert.ok(prerequisite && prerequisite.member.includes('\nConstructor:'), 'Missing constructor denial reference.');
        assert.equal(prerequisite.phases.missing.mode, 'invoked');
        assert.equal(prerequisite.phases.missing.diagnostic, 'PVADLIC001');
        prerequisites++;
      } else {
        assert.equal(result.mode, 'invoked');
        assert.equal(result.prerequisite, null);
      }
    }
  }
  assert.deepEqual(dispatches, scope.dispatches);
  assert.equal(prerequisites, scope.prerequisites);
  if (product === 'spreadsheet') assert.equal(report.protectedMethods, 40);
  if (product === 'converter') {
    assert.equal(report.exporters, 9);
    assert.equal(report.publicExportMethods, 13);
    assert.equal(report.interfaceCalls, 18);
    assert.equal(implementations.size, 9, 'Missing exporter implementation.');
    for (const counts of implementations.values()) {
      assert.equal(counts.interface, 2, 'Every exporter needs both interface overloads.');
      assert.ok(counts.direct === 1 || counts.direct === 2);
    }
  }
  return { scope: `${product}-declared-processing-overloads-and-dispatch`, entries: rows.length,
    phases: rows.length * 3, directlyInvokedPhases: rows.length * 3 - prerequisites,
    missingConstructorPrerequisites: prerequisites };
}
