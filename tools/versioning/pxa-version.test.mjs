import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import test from 'node:test';
import {
  aggregateReleaseFragments,
  bumpVersion,
  checkRepository,
  compareVersions,
  prepareRelease,
  prepareReleaseFromFragments,
  previewReleaseFromFragments,
  readReleaseFragments,
  releaseImpact,
  renderReleaseDryRun,
  renderReleaseNotes,
  synchronizeVersions,
  validateChangedReleaseFragment,
  validateNextVersion,
  validateReleaseFragment,
  validateReleasePullRequest,
} from './pxa-version.mjs';

const packagePaths = [
  'pxa-designer/package.json',
  'websites/PXA.Account/package.json',
  'websites/PXA.Admin/package.json',
  'websites/PXA.Company/package.json',
  'websites/PXA.Demo/package.json',
  'websites/PXA.Documentation/package.json',
  'tools/PXA.Mcp/package.json',
];
const lockPaths = [
  'pxa-designer/package-lock.json',
  'websites/PXA.Admin/package-lock.json',
];
const dockerPaths = [
  'PXA.WebApi/Dockerfile',
  'src/Observability/PXA.Observability.WebhookRelay/Dockerfile',
];
const composePaths = ['deploy/api/docker-compose.api.yml'];

async function fixture() {
  const root = await mkdtemp(join(tmpdir(), 'pxa-version-'));
  await writeFile(join(root, 'VERSION'), '1.0.0\n');
  for (const path of packagePaths) {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), '{"name":"test","version":"0.1.0"}\n');
  }
  for (const path of lockPaths) {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), '{"version":"0.1.0","packages":{"":{"version":"0.1.0"}}}\n');
  }
  for (const path of dockerPaths) {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), 'ARG PXA_VERSION=0.1.0\n');
  }
  for (const path of composePaths) {
    await mkdir(dirname(join(root, path)), { recursive: true });
    await writeFile(join(root, path), 'PXA_VERSION: ${PXA_VERSION:-0.1.0}\n');
  }
  await mkdir(join(root, 'product-metadata'), { recursive: true });
  await writeFile(join(root, 'product-metadata/pxa-releases.json'), JSON.stringify({
    schemaVersion: 1,
    product: 'PXA',
    releases: [{
      version: '1.0.0',
      publishedAt: '2026-07-28',
      channel: 'stable',
      title: 'PXA 1.0.0',
      summary: 'Baseline release.',
      documentationPath: '/#release-notes-1-0-0',
      components: ['api'],
      featureIds: [],
      changes: {
        added: ['Baseline.'],
        improved: [],
        fixed: [],
        security: [],
        deprecated: [],
        breaking: [],
      },
    }],
  }));
  return root;
}

function fragment(overrides = {}) {
  return {
    id: 'designer-page-navigation',
    impact: 'minor',
    components: ['designer'],
    category: 'improved',
    summary: 'Large Designer pages can be navigated horizontally and vertically.',
    featureIds: ['designer.page-navigation'],
    documentation: ['/#designer-page-settings'],
    breaking: false,
    ...overrides,
  };
}

async function writeFragment(root, name, value) {
  const directory = join(root, 'product-metadata/release-fragments');
  await mkdir(directory, { recursive: true });
  await writeFile(join(directory, `${name}.json`), `${JSON.stringify(value, null, 2)}\n`);
}

test('calculates strict stable Semantic Version bumps', () => {
  assert.equal(bumpVersion('1.2.3', 'patch'), '1.2.4');
  assert.equal(bumpVersion('1.2.3', 'minor'), '1.3.0');
  assert.equal(bumpVersion('1.2.3', 'major'), '2.0.0');
  assert.equal(compareVersions('2.0.0', '1.99.99') > 0, true);
  assert.equal(releaseImpact('1.2.3', '1.2.4'), 'patch');
  assert.equal(releaseImpact('1.2.3', '1.3.0'), 'minor');
  assert.equal(releaseImpact('1.2.3', '2.0.0'), 'major');
  assert.throws(() => releaseImpact('1.2.3', '1.4.0'), /not a single release increase/);
  assert.throws(() => bumpVersion('1.2.3-beta.1', 'patch'), /Invalid stable/);
});

test('synchronizes package manifests and lockfiles', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  assert.equal(await checkRepository(root), '1.0.0');
  const lock = JSON.parse(await readFile(join(root, lockPaths[0]), 'utf8'));
  assert.equal(lock.version, '1.0.0');
  assert.equal(lock.packages[''].version, '1.0.0');
  assert.match(await readFile(join(root, dockerPaths[0]), 'utf8'), /PXA_VERSION=1\.0\.0/);
  assert.match(await readFile(join(root, composePaths[0]), 'utf8'), /PXA_VERSION:-1\.0\.0/);
});

test('prepares the exact next version and requires curated release content', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  assert.equal(await prepareRelease(root, 'minor', '2026-08-01'), '1.1.0');
  await assert.rejects(checkRepository(root), /TODO content/);
});

test('validates one matching release label and allowed source branch', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  await validateReleasePullRequest({
    repoRoot: root,
    baseVersion: '0.9.0',
    labels: ['release:major'],
    headRef: 'develop',
  });
  await assert.rejects(validateReleasePullRequest({
    repoRoot: root,
    baseVersion: '0.9.0',
    labels: ['release:major', 'release:patch'],
    headRef: 'develop',
  }), /exactly one/);
  await assert.rejects(validateReleasePullRequest({
    repoRoot: root,
    baseVersion: '0.9.0',
    labels: ['release:major'],
    headRef: 'feature/example',
  }), /develop or hotfix/);
});

test('renders curated GitHub release notes from the shared manifest', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  const notes = await renderReleaseNotes(root, '1.0.0');
  assert.match(notes, /^# PXA 1\.0\.0/m);
  assert.match(notes, /## Added/);
  assert.match(notes, /Baseline\./);
});

test('accepts only one forward stable release step', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  assert.equal(await validateNextVersion(root, '0.9.0'), '1.0.0');
  await assert.rejects(validateNextVersion(root, '1.0.0'), /not a single/);
  await assert.rejects(validateNextVersion(root, '2.0.0'), /not a single/);
});

test('validates release fragment structure and safety boundaries', () => {
  assert.equal(validateReleaseFragment(fragment()).impact, 'minor');
  assert.throws(
    () => validateReleaseFragment(fragment({ impact: 'none' })),
    /meaningful reason/,
  );
  assert.throws(
    () => validateReleaseFragment(fragment({
      impact: 'patch',
      category: 'breaking',
      breaking: true,
    })),
    /breaking changes require impact 'major'/,
  );
  assert.throws(
    () => validateReleaseFragment(fragment({ documentation: ['https://example.com'] })),
    /safe root-relative paths/,
  );
  assert.throws(
    () => validateReleaseFragment(fragment({ components: ['unknown'] })),
    /unknown component/,
  );
  assert.throws(
    () => validateReleaseFragment(fragment({ summary: 'TODO: document this later.' })),
    /placeholder, markup, or sensitive content/,
  );
  assert.throws(
    () => validateReleaseFragment(fragment({ summary: 'Use [this link](javascript:alert(1)) now.' })),
    /placeholder, markup, or sensitive content/,
  );
});

test('aggregates fragments deterministically using the highest impact', () => {
  const fragments = [
    fragment(),
    fragment({
      id: 'api-validation-fix',
      impact: 'patch',
      components: ['api'],
      category: 'fixed',
      summary: 'API validation now reports consistent document errors.',
      featureIds: [],
      documentation: [],
    }),
    fragment({
      id: 'internal-test-update',
      impact: 'none',
      category: 'improved',
      summary: 'Internal release tooling tests cover another fixture.',
      featureIds: [],
      documentation: [],
      reason: 'This change only updates internal test coverage.',
    }),
  ];
  const aggregate = aggregateReleaseFragments(fragments);
  assert.equal(aggregate.impact, 'minor');
  assert.deepEqual(aggregate.components, ['api', 'designer']);
  assert.equal(aggregate.changes.improved.length, 1);
  assert.equal(aggregate.changes.fixed.length, 1);
  assert.deepEqual(aggregate.fragmentIds, ['api-validation-fix', 'designer-page-navigation']);
  assert.deepEqual(aggregateReleaseFragments(fragments.toReversed()), aggregate);
});

test('prepares a complete release from pending fragments and consumes them', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  await writeFragment(root, 'designer-page-navigation', fragment());
  await writeFragment(root, 'api-validation-fix', fragment({
    id: 'api-validation-fix',
    impact: 'patch',
    components: ['api'],
    category: 'fixed',
    summary: 'API validation now reports consistent document errors.',
    featureIds: [],
    documentation: [],
  }));

  const result = await prepareReleaseFromFragments(root, {
    summary: 'This release improves Designer navigation and API validation.',
    publishedAt: '2026-08-02',
  });

  assert.equal(result.version, '1.1.0');
  assert.equal(result.impact, 'minor');
  assert.equal(result.fragmentCount, 2);
  assert.equal(await checkRepository(root), '1.1.0');
  assert.deepEqual(await readReleaseFragments(root), []);
  const manifest = JSON.parse(await readFile(join(root, 'product-metadata/pxa-releases.json'), 'utf8'));
  assert.deepEqual(manifest.releases[0].components, ['api', 'designer']);
  assert.equal(manifest.releases[0].changes.improved.length, 1);
  assert.equal(manifest.releases[0].changes.fixed.length, 1);
});

test('dry-runs Patch, Minor, and Major releases without changing repository state', async () => {
  const scenarios = [
    {
      impact: 'patch',
      category: 'fixed',
      breaking: false,
      version: '1.0.1',
      summary: 'Document validation now returns the correct error details.',
    },
    {
      impact: 'minor',
      category: 'added',
      breaking: false,
      version: '1.1.0',
      summary: 'Designers can now preview structured release information.',
    },
    {
      impact: 'major',
      category: 'breaking',
      breaking: true,
      version: '2.0.0',
      summary: 'The legacy document API is replaced by the versioned contract.',
    },
  ];

  for (const scenario of scenarios) {
    const root = await fixture();
    await synchronizeVersions(root, '1.0.0');
    await writeFragment(root, `dry-run-${scenario.impact}`, fragment({
      id: `dry-run-${scenario.impact}`,
      impact: scenario.impact,
      category: scenario.category,
      summary: scenario.summary,
      featureIds: [],
      documentation: [],
      breaking: scenario.breaking,
    }));
    const versionBefore = await readFile(join(root, 'VERSION'), 'utf8');
    const manifestPath = join(root, 'product-metadata/pxa-releases.json');
    const manifestBefore = await readFile(manifestPath, 'utf8');

    const preview = await previewReleaseFromFragments(root, {
      summary: `This is the customer-facing ${scenario.impact} release preview.`,
      publishedAt: '2026-08-03',
    });

    assert.equal(preview.impact, scenario.impact);
    assert.equal(preview.version, scenario.version);
    assert.equal(preview.requiresMajorConfirmation, scenario.impact === 'major');
    assert.match(renderReleaseDryRun(preview), /# PXA Release Dry Run/);
    assert.equal(await readFile(join(root, 'VERSION'), 'utf8'), versionBefore);
    assert.equal(await readFile(manifestPath, 'utf8'), manifestBefore);
    assert.equal((await readReleaseFragments(root)).length, 1);
  }
});

test('requires explicit confirmation before preparing a Major release', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  await writeFragment(root, 'breaking-api-contract', fragment({
    id: 'breaking-api-contract',
    impact: 'major',
    components: ['api'],
    category: 'breaking',
    summary: 'The legacy document conversion contract is replaced by the versioned API.',
    featureIds: [],
    documentation: ['/#api-migration'],
    breaking: true,
  }));

  await assert.rejects(
    prepareReleaseFromFragments(root, {
      summary: 'This release introduces the next version of the public document API.',
    }),
    /explicit '--confirm-major'/,
  );
  await assert.rejects(
    previewReleaseFromFragments(root, {
      summary: 'This release introduces the next version of the public document API.',
      publishedAt: '03.08.2026',
    }),
    /publication date must use 'YYYY-MM-DD'/,
  );
});

test('requires a changed release fragment for develop pull requests', () => {
  assert.deepEqual(
    validateChangedReleaseFragment([
      'pxa-designer/src/App.tsx',
      'product-metadata/release-fragments/designer-app.json',
    ]),
    ['product-metadata/release-fragments/designer-app.json'],
  );
  assert.throws(
    () => validateChangedReleaseFragment(['pxa-designer/src/App.tsx']),
    /must change a JSON file/,
  );
});

test('rejects duplicate fragment IDs and unknown feature references', async () => {
  const duplicateRoot = await fixture();
  await writeFragment(duplicateRoot, 'first', fragment());
  await writeFragment(duplicateRoot, 'second', fragment({ summary: 'Another valid customer-facing release summary.' }));
  await assert.rejects(readReleaseFragments(duplicateRoot), /Duplicate release fragment id/);

  const unknownFeatureRoot = await fixture();
  await writeFile(
    join(unknownFeatureRoot, 'product-metadata/designer-features.json'),
    '{"features":[{"id":"designer.known"}]}\n',
  );
  await writeFragment(unknownFeatureRoot, 'unknown-feature', fragment({
    featureIds: ['designer.unknown'],
  }));
  await assert.rejects(readReleaseFragments(unknownFeatureRoot), /Unknown release fragment feature ID/);
});

test('blocks stable release validation while fragments remain pending', async () => {
  const root = await fixture();
  await synchronizeVersions(root, '1.0.0');
  await writeFragment(root, 'pending-fix', fragment({
    id: 'pending-fix',
    impact: 'patch',
    featureIds: [],
  }));
  await assert.rejects(validateNextVersion(root, '0.9.0'), /unaggregated release fragment/);
});
