#!/usr/bin/env node

import { readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

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
  'tools/PXA.Mcp/package-lock.json',
];
const DOCKERFILES = [
  'PXA.WebApi/Dockerfile',
  'src/Observability/PXA.Observability.WebhookRelay/Dockerfile',
];
const COMPOSE_FILES = ['deploy/api/docker-compose.api.yml'];
const CHANGE_CATEGORIES = ['added', 'improved', 'fixed', 'security', 'deprecated', 'breaking'];
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

async function readJson(path) {
  return JSON.parse(await readFile(path, 'utf8'));
}

async function writeJson(path, value) {
  await writeFile(path, `${JSON.stringify(value, null, 2)}\n`);
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
  return actual;
}

export async function validateNextVersion(repoRoot = scriptRoot, baseVersion) {
  parseVersion(baseVersion);
  const actual = await checkRepository(repoRoot);
  const allowed = ['patch', 'minor', 'major'].map(bump => bumpVersion(baseVersion, bump));
  if (!allowed.includes(actual))
    throw new Error(
      `VERSION ${actual} is not a single patch, minor, or major increase from ${baseVersion}.`,
    );
  return actual;
}

export async function renderReleaseNotes(repoRoot = scriptRoot, requestedVersion) {
  const version = requestedVersion ?? await readCurrentVersion(repoRoot);
  const manifest = await readJson(resolve(repoRoot, 'product-metadata/pxa-releases.json'));
  validateReleaseManifest(manifest, version);
  const release = manifest.releases.find(value => value.version === version);
  if (!release) throw new Error(`Release manifest has no entry for ${version}.`);

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

function option(args, name) {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : undefined;
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
  if (command === 'notes') {
    console.log(await renderReleaseNotes(scriptRoot, args[0]));
    return;
  }
  throw new Error('Usage: pxa-version.mjs current|sync|check|prepare <major|minor|patch>|validate-pr|validate-next|notes [version]');
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
