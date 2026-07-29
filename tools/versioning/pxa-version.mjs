#!/usr/bin/env node

import { execFile } from 'node:child_process';
import { readFile, readdir, unlink, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { promisify } from 'node:util';

const SEMVER = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;
const RELEASE_LABELS = ['release:major', 'release:minor', 'release:patch'];
const PACKAGE_MANIFESTS = [
  'pxa-designer/package.json',
  'websites/PXA.Account/package.json',
  'websites/PXA.Admin/package.json',
  'websites/PXA.Company/package.json',
  'websites/PXA.Demo/package.json',
  'websites/PXA.Documentation/package.json',
  'tools/PXA.Mcp/package.json',
];
const PACKAGE_LOCKS = [
  'pxa-designer/package-lock.json',
  'websites/PXA.Admin/package-lock.json',
];
const DOCKERFILES = [
  'PXA.WebApi/Dockerfile',
  'src/Observability/PXA.Observability.WebhookRelay/Dockerfile',
];
const COMPOSE_FILES = ['deploy/api/docker-compose.api.yml'];
const CHANGE_CATEGORIES = ['added', 'improved', 'fixed', 'security', 'deprecated', 'breaking'];
const RELEASE_IMPACTS = ['none', 'patch', 'minor', 'major'];
const RELEASE_COMPONENTS = [
  'account',
  'admin',
  'api',
  'company',
  'demo',
  'designer',
  'documentation',
  'generator',
  'importer',
  'infrastructure',
  'migration',
  'observability',
  'sdk',
  'spreadsheet',
];
const RELEASE_FRAGMENT_DIRECTORY = 'product-metadata/release-fragments';
const execFileAsync = promisify(execFile);
const scriptRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export function parseVersion(value) {
  const match = SEMVER.exec(value.trim());
  if (!match) throw new Error(`Invalid stable Semantic Version: ${value}`);
  return { major: Number(match[1]), minor: Number(match[2]), patch: Number(match[3]) };
}

export function compareVersions(left, right) {
  const a = parseVersion(left);
  const b = parseVersion(right);
  return a.major - b.major || a.minor - b.minor || a.patch - b.patch;
}

export function bumpVersion(version, bump) {
  const current = parseVersion(version);
  if (bump === 'major') return `${current.major + 1}.0.0`;
  if (bump === 'minor') return `${current.major}.${current.minor + 1}.0`;
  if (bump === 'patch') return `${current.major}.${current.minor}.${current.patch + 1}`;
  throw new Error(`Unsupported release bump '${bump}'. Use major, minor, or patch.`);
}

export function releaseImpact(baseVersion, nextVersion) {
  parseVersion(baseVersion);
  parseVersion(nextVersion);
  const impact = ['patch', 'minor', 'major'].find(
    value => bumpVersion(baseVersion, value) === nextVersion,
  );
  if (!impact)
    throw new Error(`VERSION ${nextVersion} is not a single release increase from ${baseVersion}.`);
  return impact;
}

async function readJson(path) {
  return JSON.parse(await readFile(path, 'utf8'));
}

async function writeJson(path, value) {
  await writeFile(path, `${JSON.stringify(value, null, 2)}\n`);
}

function requireString(value, field, source) {
  if (typeof value !== 'string' || !value.trim())
    throw new Error(`${source}: '${field}' must be a non-empty string.`);
  return value.trim();
}

function requireUniqueStrings(value, field, source) {
  if (!Array.isArray(value) || value.length === 0)
    throw new Error(`${source}: '${field}' must be a non-empty array.`);
  const items = value.map(item => requireString(item, field, source));
  if (new Set(items).size !== items.length)
    throw new Error(`${source}: '${field}' must not contain duplicates.`);
  return items;
}

function validatePublicText(value, field, source, minimum, maximum) {
  const text = requireString(value, field, source);
  if (text.length < minimum || text.length > maximum)
    throw new Error(`${source}: '${field}' must contain ${minimum} to ${maximum} characters.`);
  if (/TODO|FIXME|[\r\n<>\[\]`]|password\s*=|api[_ -]?key\s*=|bearer\s+[a-z0-9._-]+|private key|internal ticket/i.test(text))
    throw new Error(`${source}: '${field}' contains placeholder, markup, or sensitive content.`);
  return text;
}

export function validateReleaseFragment(fragment, source = 'release fragment') {
  if (!fragment || typeof fragment !== 'object' || Array.isArray(fragment))
    throw new Error(`${source}: release fragment must be a JSON object.`);

  const allowedFields = new Set([
    '$schema',
    'id',
    'impact',
    'components',
    'category',
    'summary',
    'featureIds',
    'documentation',
    'breaking',
    'reason',
  ]);
  const unknownFields = Object.keys(fragment).filter(field => !allowedFields.has(field));
  if (unknownFields.length > 0)
    throw new Error(`${source}: unknown field(s): ${unknownFields.join(', ')}.`);

  const id = requireString(fragment.id, 'id', source);
  if (!/^[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(id))
    throw new Error(`${source}: 'id' must use lowercase letters, numbers, dots, or hyphens.`);

  const impact = requireString(fragment.impact, 'impact', source);
  if (!RELEASE_IMPACTS.includes(impact))
    throw new Error(`${source}: unsupported impact '${impact}'.`);

  const components = requireUniqueStrings(fragment.components, 'components', source);
  const unknownComponents = components.filter(component => !RELEASE_COMPONENTS.includes(component));
  if (unknownComponents.length > 0)
    throw new Error(`${source}: unknown component(s): ${unknownComponents.join(', ')}.`);

  const category = requireString(fragment.category, 'category', source);
  if (!CHANGE_CATEGORIES.includes(category))
    throw new Error(`${source}: unsupported category '${category}'.`);

  const summary = validatePublicText(fragment.summary, 'summary', source, 12, 300);

  if (typeof fragment.breaking !== 'boolean')
    throw new Error(`${source}: 'breaking' must be true or false.`);
  if ((fragment.breaking || category === 'breaking') &&
      !(fragment.breaking && category === 'breaking' && impact === 'major'))
    throw new Error(`${source}: breaking changes require impact 'major', category 'breaking', and breaking true.`);

  const featureIds = fragment.featureIds ?? [];
  if (!Array.isArray(featureIds) || featureIds.some(value => typeof value !== 'string' ||
      !/^[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(value)))
    throw new Error(`${source}: 'featureIds' must contain stable lowercase identifiers.`);
  if (new Set(featureIds).size !== featureIds.length)
    throw new Error(`${source}: 'featureIds' must not contain duplicates.`);

  const documentation = fragment.documentation ?? [];
  if (!Array.isArray(documentation) || documentation.some(value =>
    typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') ||
    value.includes('..') || /\s/.test(value)))
    throw new Error(`${source}: 'documentation' must contain safe root-relative paths.`);
  if (new Set(documentation).size !== documentation.length)
    throw new Error(`${source}: 'documentation' must not contain duplicates.`);

  const reason = typeof fragment.reason === 'string' ? fragment.reason.trim() : '';
  if (impact === 'none' && reason.length < 12)
    throw new Error(`${source}: impact 'none' requires a meaningful reason.`);
  if (impact !== 'none' && reason)
    throw new Error(`${source}: 'reason' is only allowed for impact 'none'.`);

  return {
    id,
    impact,
    components,
    category,
    summary,
    featureIds,
    documentation,
    breaking: fragment.breaking,
    ...(reason ? { reason } : {}),
  };
}

export async function readReleaseFragments(repoRoot = scriptRoot) {
  const directory = resolve(repoRoot, RELEASE_FRAGMENT_DIRECTORY);
  let entries;
  try {
    entries = await readdir(directory, { withFileTypes: true });
  } catch (error) {
    if (error.code === 'ENOENT') return [];
    throw error;
  }

  const fragments = [];
  for (const entry of entries.filter(value => value.isFile() && value.name.endsWith('.json')).sort(
    (left, right) => left.name.localeCompare(right.name),
  )) {
    const path = resolve(directory, entry.name);
    const fragment = validateReleaseFragment(await readJson(path), entry.name);
    fragments.push({ fileName: entry.name, path, ...fragment });
  }
  const ids = fragments.map(fragment => fragment.id);
  const duplicateId = ids.find((id, index) => ids.indexOf(id) !== index);
  if (duplicateId) throw new Error(`Duplicate release fragment id '${duplicateId}'.`);

  try {
    const featureManifest = await readJson(resolve(repoRoot, 'product-metadata/designer-features.json'));
    const knownFeatureIds = new Set(featureManifest.features?.map(feature => feature.id) ?? []);
    const unknownFeatureIds = [...new Set(
      fragments.flatMap(fragment => fragment.featureIds).filter(id => !knownFeatureIds.has(id)),
    )];
    if (unknownFeatureIds.length > 0)
      throw new Error(`Unknown release fragment feature ID(s): ${unknownFeatureIds.join(', ')}.`);
  } catch (error) {
    if (error.code !== 'ENOENT') throw error;
  }
  return fragments;
}

export function aggregateReleaseFragments(fragments) {
  const publicFragments = fragments
    .filter(fragment => fragment.impact !== 'none')
    .toSorted((left, right) => left.id.localeCompare(right.id));
  if (publicFragments.length === 0)
    throw new Error('No customer-facing release fragments are pending.');

  const impact = publicFragments.reduce((highest, fragment) =>
    RELEASE_IMPACTS.indexOf(fragment.impact) > RELEASE_IMPACTS.indexOf(highest)
      ? fragment.impact
      : highest, 'patch');
  const changes = Object.fromEntries(CHANGE_CATEGORIES.map(category => [
    category,
    publicFragments
      .filter(fragment => fragment.category === category)
      .map(fragment => fragment.summary),
  ]));

  return {
    impact,
    components: [...new Set(publicFragments.flatMap(fragment => fragment.components))].sort(),
    featureIds: [...new Set(publicFragments.flatMap(fragment => fragment.featureIds ?? []))].sort(),
    documentation: [...new Set(publicFragments.flatMap(fragment => fragment.documentation ?? []))].sort(),
    changes,
    fragmentIds: publicFragments.map(fragment => fragment.id).sort(),
  };
}

export function validateChangedReleaseFragment(changedFiles) {
  const fragmentChanges = changedFiles.filter(path =>
    path.startsWith(`${RELEASE_FRAGMENT_DIRECTORY}/`) && path.endsWith('.json'));
  if (fragmentChanges.length === 0)
    throw new Error(`Pull requests to develop must change a JSON file in ${RELEASE_FRAGMENT_DIRECTORY}.`);
  return fragmentChanges;
}

export async function readCurrentVersion(repoRoot = scriptRoot) {
  return (await readFile(resolve(repoRoot, 'VERSION'), 'utf8')).trim();
}

export async function synchronizeVersions(repoRoot = scriptRoot, requestedVersion) {
  const version = requestedVersion ?? await readCurrentVersion(repoRoot);
  parseVersion(version);
  await writeFile(resolve(repoRoot, 'VERSION'), `${version}\n`);

  for (const relativePath of PACKAGE_MANIFESTS) {
    const path = resolve(repoRoot, relativePath);
    const manifest = await readJson(path);
    manifest.version = version;
    await writeJson(path, manifest);
  }
  for (const relativePath of PACKAGE_LOCKS) {
    const path = resolve(repoRoot, relativePath);
    const lock = await readJson(path);
    lock.version = version;
    if (lock.packages?.['']) lock.packages[''].version = version;
    await writeJson(path, lock);
  }
  for (const relativePath of DOCKERFILES) {
    const path = resolve(repoRoot, relativePath);
    const source = await readFile(path, 'utf8');
    await writeFile(path, source.replaceAll(/ARG PXA_VERSION=\d+\.\d+\.\d+/g, `ARG PXA_VERSION=${version}`));
  }
  for (const relativePath of COMPOSE_FILES) {
    const path = resolve(repoRoot, relativePath);
    const source = await readFile(path, 'utf8');
    await writeFile(
      path,
      source.replaceAll(/\$\{PXA_VERSION:-\d+\.\d+\.\d+\}/g, `\${PXA_VERSION:-${version}}`),
    );
  }
  return version;
}

function validateReleaseManifest(manifest, currentVersion) {
  if (manifest.schemaVersion !== 1 || manifest.product !== 'PXA')
    throw new Error('Release manifest must use schemaVersion 1 and product PXA.');
  if (!Array.isArray(manifest.releases) || manifest.releases.length === 0)
    throw new Error('Release manifest must contain at least one release.');

  const versions = new Set();
  let previous;
  for (const release of manifest.releases) {
    parseVersion(release.version);
    if (versions.has(release.version)) throw new Error(`Duplicate release ${release.version}.`);
    versions.add(release.version);
    if (previous && compareVersions(previous, release.version) <= 0)
      throw new Error('Release manifest must be ordered newest version first.');
    previous = release.version;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(release.publishedAt))
      throw new Error(`Release ${release.version} has an invalid publication date.`);
    if (!['stable', 'beta', 'alpha'].includes(release.channel))
      throw new Error(`Release ${release.version} has an invalid channel.`);
    if (!release.title?.trim() || !release.summary?.trim())
      throw new Error(`Release ${release.version} needs a title and summary.`);
    if (/TODO/i.test(`${release.title} ${release.summary}`))
      throw new Error(`Release ${release.version} still contains TODO content.`);
    if (!Array.isArray(release.components) || release.components.length === 0)
      throw new Error(`Release ${release.version} must list affected components.`);
    if (!release.changes || CHANGE_CATEGORIES.some(category => !Array.isArray(release.changes[category])))
      throw new Error(`Release ${release.version} must define every change category.`);
    if (!CHANGE_CATEGORIES.some(category => release.changes[category].length > 0))
      throw new Error(`Release ${release.version} must contain at least one change.`);
  }
  if (!versions.has(currentVersion))
    throw new Error(`Release manifest has no entry for current version ${currentVersion}.`);
}

export async function checkRepository(repoRoot = scriptRoot) {
  const version = await readCurrentVersion(repoRoot);
  parseVersion(version);
  const errors = [];

  for (const relativePath of PACKAGE_MANIFESTS) {
    const manifest = await readJson(resolve(repoRoot, relativePath));
    if (manifest.version !== version)
      errors.push(`${relativePath} is ${manifest.version}; expected ${version}.`);
  }
  for (const relativePath of PACKAGE_LOCKS) {
    const lock = await readJson(resolve(repoRoot, relativePath));
    if (lock.version !== version || lock.packages?.['']?.version !== version)
      errors.push(`${relativePath} does not mirror ${version}.`);
  }
  for (const relativePath of DOCKERFILES) {
    const source = await readFile(resolve(repoRoot, relativePath), 'utf8');
    if (!source.includes(`ARG PXA_VERSION=${version}`))
      errors.push(`${relativePath} does not default to ${version}.`);
  }
  for (const relativePath of COMPOSE_FILES) {
    const source = await readFile(resolve(repoRoot, relativePath), 'utf8');
    if (!source.includes(`\${PXA_VERSION:-${version}}`))
      errors.push(`${relativePath} does not default to ${version}.`);
  }
  try {
    validateReleaseManifest(
      await readJson(resolve(repoRoot, 'product-metadata/pxa-releases.json')),
      version,
    );
  } catch (error) {
    errors.push(error.message);
  }
  try {
    await readReleaseFragments(repoRoot);
  } catch (error) {
    errors.push(error.message);
  }
  if (errors.length > 0) throw new Error(errors.join('\n'));
  return version;
}

export async function prepareRelease(repoRoot = scriptRoot, bump, publishedAt) {
  const current = await readCurrentVersion(repoRoot);
  const next = bumpVersion(current, bump);
  const manifestPath = resolve(repoRoot, 'product-metadata/pxa-releases.json');
  const manifest = await readJson(manifestPath);
  if (manifest.releases.some(release => release.version === next))
    throw new Error(`Release ${next} already exists.`);
  manifest.releases.unshift({
    version: next,
    publishedAt: publishedAt ?? new Date().toISOString().slice(0, 10),
    channel: 'stable',
    title: `PXA ${next}`,
    summary: 'TODO: Replace with a customer-facing release summary.',
    documentationPath: `/#release-notes-${next.replaceAll('.', '-')}`,
    components: [],
    featureIds: [],
    changes: Object.fromEntries(CHANGE_CATEGORIES.map(category => [category, []])),
  });
  await synchronizeVersions(repoRoot, next);
  await writeJson(manifestPath, manifest);
  return next;
}

export async function previewReleaseFromFragments(repoRoot = scriptRoot, {
  summary,
  title,
  publishedAt,
} = {}) {
  const releaseSummary = validatePublicText(
    summary,
    'summary',
    'release preparation',
    20,
    500,
  );
  const releaseTitle = title?.trim() || '';
  if (releaseTitle) validatePublicText(releaseTitle, 'title', 'release preparation', 3, 120);

  const fragments = await readReleaseFragments(repoRoot);
  const aggregate = aggregateReleaseFragments(fragments);
  const current = await readCurrentVersion(repoRoot);
  const next = bumpVersion(current, aggregate.impact);
  const releaseDate = publishedAt ?? new Date().toISOString().slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(releaseDate))
    throw new Error("release preparation: publication date must use 'YYYY-MM-DD'.");
  const manifestPath = resolve(repoRoot, 'product-metadata/pxa-releases.json');
  const manifest = await readJson(manifestPath);
  if (manifest.releases.some(release => release.version === next))
    throw new Error(`Release ${next} already exists.`);

  const release = {
    version: next,
    publishedAt: releaseDate,
    channel: 'stable',
    title: releaseTitle || `PXA ${next}`,
    summary: releaseSummary,
    documentationPath: `/#release-notes-${next.replaceAll('.', '-')}`,
    components: aggregate.components,
    featureIds: aggregate.featureIds,
    changes: aggregate.changes,
  };

  return {
    version: next,
    impact: aggregate.impact,
    fragmentCount: fragments.length,
    fragmentIds: aggregate.fragmentIds,
    documentation: aggregate.documentation,
    requiresMajorConfirmation: aggregate.impact === 'major',
    release,
  };
}

export async function prepareReleaseFromFragments(repoRoot = scriptRoot, {
  confirmMajor = false,
  ...options
} = {}) {
  const preview = await previewReleaseFromFragments(repoRoot, options);
  if (preview.requiresMajorConfirmation && !confirmMajor)
    throw new Error("A Major release requires explicit '--confirm-major' confirmation.");

  const manifestPath = resolve(repoRoot, 'product-metadata/pxa-releases.json');
  const manifest = await readJson(manifestPath);
  manifest.releases.unshift(preview.release);
  await synchronizeVersions(repoRoot, preview.version);
  await writeJson(manifestPath, manifest);
  for (const fragment of await readReleaseFragments(repoRoot)) await unlink(fragment.path);

  return preview;
}

export async function validateReleasePullRequest({
  repoRoot = scriptRoot,
  baseVersion,
  labels,
  headRef,
}) {
  parseVersion(baseVersion);
  const releaseLabels = labels.filter(label => RELEASE_LABELS.includes(label));
  if (releaseLabels.length !== 1)
    throw new Error('A main release PR requires exactly one release:major, release:minor, or release:patch label.');
  if (headRef !== 'develop' && !headRef.startsWith('hotfix/'))
    throw new Error('Main accepts releases only from develop or hotfix/* branches.');
  const bump = releaseLabels[0].slice('release:'.length);
  const expected = bumpVersion(baseVersion, bump);
  const actual = await checkRepository(repoRoot);
  if (actual !== expected)
    throw new Error(`Label ${releaseLabels[0]} requires ${expected}; VERSION is ${actual}.`);
  const pendingFragments = await readReleaseFragments(repoRoot);
  if (pendingFragments.length > 0)
    throw new Error(`Stable release pull request has ${pendingFragments.length} unaggregated release fragment(s).`);
  return actual;
}

export async function validateNextVersion(repoRoot = scriptRoot, baseVersion) {
  parseVersion(baseVersion);
  const actual = await checkRepository(repoRoot);
  releaseImpact(baseVersion, actual);
  const pendingFragments = await readReleaseFragments(repoRoot);
  if (pendingFragments.length > 0)
    throw new Error(`PXA ${actual} has ${pendingFragments.length} unaggregated release fragment(s).`);
  return actual;
}

function renderReleaseDefinition(release) {
  const sections = CHANGE_CATEGORIES
    .filter(category => release.changes[category].length > 0)
    .map(category => [
      `## ${category[0].toUpperCase()}${category.slice(1)}`,
      ...release.changes[category].map(change => `- ${change}`),
    ].join('\n'));
  return [
    `# ${release.title}`,
    release.summary,
    [
      `**Released:** ${release.publishedAt}`,
      `**Channel:** ${release.channel}`,
      `**Components:** ${release.components.join(', ')}`,
    ].join('\n'),
    ...sections,
  ].join('\n\n');
}

export async function renderReleaseNotes(repoRoot = scriptRoot, requestedVersion) {
  const version = requestedVersion ?? await readCurrentVersion(repoRoot);
  const manifest = await readJson(resolve(repoRoot, 'product-metadata/pxa-releases.json'));
  validateReleaseManifest(manifest, version);
  const release = manifest.releases.find(value => value.version === version);
  if (!release) throw new Error(`Release manifest has no entry for ${version}.`);
  return renderReleaseDefinition(release);
}

export function renderReleaseDryRun(preview) {
  return [
    '# PXA Release Dry Run',
    [
      `**Proposed version:** ${preview.version}`,
      `**Impact:** ${preview.impact}`,
      `**Pending fragments:** ${preview.fragmentCount}`,
      `**Major confirmation required:** ${preview.requiresMajorConfirmation ? 'yes' : 'no'}`,
    ].join('\n'),
    preview.documentation.length > 0
      ? `**Related Documentation:** ${preview.documentation.join(', ')}`
      : '**Related Documentation:** none',
    '## Release Notes Preview',
    renderReleaseDefinition(preview.release),
  ].join('\n\n');
}

function option(args, name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : undefined;
}

async function changedFilesFromGit(repoRoot, baseRef) {
  requireString(baseRef, 'base-ref', 'release fragment validation');
  const { stdout } = await execFileAsync(
    'git',
    ['diff', '--name-only', `${baseRef}...HEAD`],
    { cwd: repoRoot },
  );
  return stdout.split(/\r?\n/).map(value => value.trim()).filter(Boolean);
}

async function main() {
  const [command, ...args] = process.argv.slice(2);
  if (command === 'current') {
    console.log(await readCurrentVersion());
    return;
  }
  if (command === 'sync') {
    console.log(`Synchronized PXA ${await synchronizeVersions()}.`);
    return;
  }
  if (command === 'check') {
    console.log(`PXA ${await checkRepository()} is synchronized and release-ready.`);
    return;
  }
  if (command === 'prepare') {
    const next = await prepareRelease(scriptRoot, args[0], option(args, '--date'));
    console.log(`Prepared PXA ${next}. Complete its release manifest entry before committing.`);
    return;
  }
  if (command === 'fragments') {
    const fragments = await readReleaseFragments();
    if (fragments.length === 0) {
      console.log('No pending PXA release fragments.');
      return;
    }
    const publicFragments = fragments.filter(fragment => fragment.impact !== 'none');
    if (publicFragments.length === 0) {
      console.log(`${fragments.length} internal-only PXA release fragment(s) are pending.`);
      return;
    }
    const aggregate = aggregateReleaseFragments(fragments);
    console.log(JSON.stringify({
      pending: fragments.length,
      impact: aggregate.impact,
      components: aggregate.components,
      fragmentIds: aggregate.fragmentIds,
    }, null, 2));
    return;
  }
  if (command === 'validate-change') {
    const changedFiles = await changedFilesFromGit(scriptRoot, option(args, '--base-ref'));
    const changedFragments = validateChangedReleaseFragment(changedFiles);
    await readReleaseFragments();
    console.log(`Validated ${changedFragments.length} changed release fragment file(s).`);
    return;
  }
  if (command === 'prepare-fragments') {
    const options = {
      summary: option(args, '--summary'),
      title: option(args, '--title'),
      publishedAt: option(args, '--date'),
    };
    if (args.includes('--dry-run')) {
      const preview = await previewReleaseFromFragments(scriptRoot, options);
      const format = option(args, '--format') ?? 'json';
      if (format === 'json') console.log(JSON.stringify(preview, null, 2));
      else if (format === 'markdown') console.log(renderReleaseDryRun(preview));
      else throw new Error("Dry-run format must be 'json' or 'markdown'.");
      return;
    }
    const result = await prepareReleaseFromFragments(scriptRoot, {
      ...options,
      confirmMajor: args.includes('--confirm-major'),
    });
    console.log(
      `Prepared PXA ${result.version} (${result.impact}) from ${result.fragmentCount} release fragment(s).`,
    );
    return;
  }
  if (command === 'validate-pr') {
    const labels = (option(args, '--labels') ?? '').split(',').filter(Boolean);
    const version = await validateReleasePullRequest({
      baseVersion: option(args, '--base'),
      labels,
      headRef: option(args, '--head-ref') ?? '',
    });
    console.log(`Release pull request correctly prepares PXA ${version}.`);
    return;
  }
  if (command === 'validate-next') {
    const version = await validateNextVersion(scriptRoot, option(args, '--base'));
    console.log(`PXA ${version} is the next stable version.`);
    return;
  }
  if (command === 'release-impact') {
    console.log(releaseImpact(option(args, '--base'), await readCurrentVersion()));
    return;
  }
  if (command === 'notes') {
    console.log(await renderReleaseNotes(scriptRoot, args[0]));
    return;
  }
  throw new Error(
    'Usage: pxa-version.mjs current|sync|check|fragments|validate-change --base-ref <ref>|' +
    'prepare <major|minor|patch>|prepare-fragments --summary <text> ' +
    '[--dry-run --format json|markdown] [--confirm-major]|' +
    'validate-pr|validate-next|release-impact --base <version>|notes [version]',
  );
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
