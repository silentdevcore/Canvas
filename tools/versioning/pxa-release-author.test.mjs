import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { validateReleaseFragment } from './pxa-version.mjs';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const catalogPath = resolve(
  repoRoot,
  '.agents/skills/pxa-release-author/references/release-author-evals.v1.json',
);
const catalog = JSON.parse(await readFile(catalogPath, 'utf8'));

const requiredScenarios = new Map([
  ['fix', { impact: 'patch', category: 'fixed' }],
  ['feature', { impact: 'minor', category: 'added' }],
  ['breaking', { impact: 'major', category: 'breaking' }],
  ['refactor', { impact: 'none', category: 'improved' }],
  ['migration', { impact: 'minor', category: 'added' }],
  ['documentation', { impact: 'patch', category: 'fixed' }],
]);

const componentPathRules = [
  ['PXA.WebApi/', 'api'],
  ['pxa-designer/', 'designer'],
  ['websites/PXA.Documentation/', 'documentation'],
  ['checklists/', 'documentation'],
  ['src/Generation/', 'generator'],
  ['src/Infrastructure/', 'infrastructure'],
  ['src/Migrations/', 'migration'],
  ['tools/', 'infrastructure'],
  ['.agents/', 'infrastructure'],
];

function inferredComponents(changedFiles) {
  return [...new Set(changedFiles.flatMap(path => componentPathRules
    .filter(([prefix]) => path.startsWith(prefix))
    .map(([, component]) => component)))];
}

function rejectionFragment(rejection) {
  return {
    id: `rejected-${rejection.kind}`,
    impact: 'patch',
    components: ['infrastructure'],
    category: rejection.category ?? 'fixed',
    summary: rejection.summary,
    breaking: false,
  };
}

test('release-author evaluation catalog is versioned and covers representative changes', () => {
  assert.equal(catalog.schemaVersion, 1);
  assert.equal(catalog.skill, 'pxa-release-author');
  assert.ok(Array.isArray(catalog.scenarios));
  assert.ok(Array.isArray(catalog.rejections));

  const scenarioKinds = catalog.scenarios.map(scenario => scenario.kind);
  assert.equal(new Set(scenarioKinds).size, scenarioKinds.length, 'scenario kinds must be unique');
  for (const kind of requiredScenarios.keys())
    assert.ok(scenarioKinds.includes(kind), `missing required '${kind}' scenario`);
});

for (const scenario of catalog.scenarios) {
  test(`release-author scenario: ${scenario.kind}`, () => {
    assert.equal(typeof scenario.prompt, 'string');
    assert.ok(scenario.prompt.length >= 20);
    assert.ok(Array.isArray(scenario.changedFiles));
    assert.ok(scenario.changedFiles.length > 0);
    assert.ok(scenario.changedFiles.every(path =>
      typeof path === 'string' && path.length > 0 && !path.startsWith('/') && !path.includes('..')));

    const fragment = validateReleaseFragment(
      scenario.expectedFragment,
      `release-author scenario '${scenario.kind}'`,
    );
    const expected = requiredScenarios.get(scenario.kind);
    if (expected) {
      assert.equal(fragment.impact, expected.impact);
      assert.equal(fragment.category, expected.category);
    }

    for (const component of inferredComponents(scenario.changedFiles))
      assert.ok(fragment.components.includes(component),
        `${scenario.kind} must include inferred component '${component}'`);

    if (scenario.kind === 'breaking') assert.equal(fragment.breaking, true);
    if (scenario.kind === 'refactor') assert.match(fragment.reason, /internal|shipped behavior/i);
    if (scenario.kind === 'security') assert.equal(fragment.securityReviewed, true);
  });
}

test('release-author rejection fixtures cover unsafe public text and security review', () => {
  const rejectionKinds = new Set(catalog.rejections.map(rejection => rejection.kind));
  for (const kind of ['internal-ticket', 'customer-email', 'assigned-secret', 'unreviewed-security'])
    assert.ok(rejectionKinds.has(kind), `missing '${kind}' rejection fixture`);

  for (const rejection of catalog.rejections) {
    assert.throws(
      () => validateReleaseFragment(rejectionFragment(rejection), `rejection '${rejection.kind}'`),
      new RegExp(rejection.errorPattern),
    );
  }
});
