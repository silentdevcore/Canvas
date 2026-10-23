#!/usr/bin/env node

import { execFile } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { promisify } from 'node:util';
import { readPvadEnvironment } from '../configuration/environment.mjs';

const execFileAsync = promisify(execFile);
const scriptRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

function requireString(value, field, source) {
  if (typeof value !== 'string' || !value.trim())
    throw new Error(`${source}: '${field}' must be a non-empty string.`);
  return value.trim();
}

export function validateBuildInfo(buildInfo, expectedVersion, source = 'build info') {
  if (!buildInfo || typeof buildInfo !== 'object' || Array.isArray(buildInfo))
    throw new Error(`${source}: expected a JSON object.`);
  if (buildInfo.product !== 'PVAD') throw new Error(`${source}: product must be PVAD.`);
  if (buildInfo.productVersion !== expectedVersion)
    throw new Error(
      `${source}: productVersion is ${buildInfo.productVersion}; expected ${expectedVersion}.`,
    );
  requireString(buildInfo.commitId, 'commitId', source);
  const buildTime = requireString(buildInfo.buildTime, 'buildTime', source);
  if (Number.isNaN(Date.parse(buildTime)))
    throw new Error(`${source}: 'buildTime' must be an ISO-compatible timestamp.`);
  return buildInfo;
}

export async function writeBuildInfo(
  outputDirectory,
  { repoRoot = scriptRoot, commitId, buildTime } = {},
) {
  const productVersion = (await readFile(resolve(repoRoot, 'VERSION'), 'utf8')).trim();
  const info = {
    product: 'PVAD',
    productVersion,
    commitId: commitId ?? readPvadEnvironment('PVAD_BUILD_COMMIT') ?? 'unknown',
    buildTime: buildTime ?? readPvadEnvironment('PVAD_BUILD_TIME') ?? new Date().toISOString(),
  };
  validateBuildInfo(info, productVersion, 'generated build info');
  await mkdir(resolve(outputDirectory), { recursive: true });
  await writeFile(
    resolve(outputDirectory, 'pvad-build-info.json'),
    `${JSON.stringify(info, null, 2)}\n`,
  );
  return info;
}

export async function verifyBuildDirectories(
  directories,
  { repoRoot = scriptRoot } = {},
) {
  if (!Array.isArray(directories) || directories.length === 0)
    throw new Error('At least one build directory is required.');
  const expectedVersion = (await readFile(resolve(repoRoot, 'VERSION'), 'utf8')).trim();
  for (const directory of directories) {
    const path = resolve(directory, 'pvad-build-info.json');
    let buildInfo;
    try {
      buildInfo = JSON.parse(await readFile(path, 'utf8'));
    } catch (error) {
      throw new Error(`${directory}: cannot read pvad-build-info.json: ${error.message}`);
    }
    validateBuildInfo(buildInfo, expectedVersion, directory);
  }
  return expectedVersion;
}

export async function verifyContainerVersion(
  image,
  { repoRoot = scriptRoot, inspect = execFileAsync } = {},
) {
  const expectedVersion = (await readFile(resolve(repoRoot, 'VERSION'), 'utf8')).trim();
  const { stdout } = await inspect('docker', [
    'inspect',
    '--format',
    '{{ index .Config.Labels "org.opencontainers.image.version" }}',
    image,
  ]);
  const actualVersion = stdout.trim();
  if (actualVersion !== expectedVersion)
    throw new Error(`${image}: container version is ${actualVersion}; expected ${expectedVersion}.`);
  return expectedVersion;
}

export async function verifyContainerMetadata(image, {
  repoRoot = scriptRoot, inspect = execFileAsync, expectedCommit, expectedBuildTime, allowDirty = false,
} = {}) {
  const version = (await readFile(resolve(repoRoot, 'VERSION'), 'utf8')).trim();
  const { stdout } = await inspect('docker', ['inspect', '--format', '{{json .Config.Labels}}', image]);
  const labels = JSON.parse(stdout);
  if (labels['org.opencontainers.image.version'] !== version)
    throw new Error(`${image}: container version does not match VERSION.`);
  const revision = requireString(labels['org.opencontainers.image.revision'], 'revision', image);
  if (!/^[a-f0-9]{40}(?:-dirty)?$/.test(revision)) throw new Error(`${image}: invalid revision.`);
  if (revision.endsWith('-dirty') && !allowDirty) throw new Error(`${image}: dirty images cannot be released.`);
  if (!expectedCommit || revision !== expectedCommit) throw new Error(`${image}: revision mismatch.`);
  const created = requireString(labels['org.opencontainers.image.created'], 'created', image);
  if (Number.isNaN(Date.parse(created)) || !expectedBuildTime || created !== expectedBuildTime)
    throw new Error(`${image}: build timestamp mismatch.`);
  return { productVersion: version, commitId: revision, buildTime: created };
}

async function main() {
  const [command, ...args] = process.argv.slice(2);
  if (command === 'write' && args.length === 1) {
    const info = await writeBuildInfo(args[0]);
    console.log(JSON.stringify(info));
    return;
  }
  if (command === 'verify' && args.length > 0) {
    const version = await verifyBuildDirectories(args);
    console.log(`Verified ${args.length} PVAD ${version} build artifact(s).`);
    return;
  }
  if (command === 'verify-container' && args.length === 1) {
    const version = await verifyContainerVersion(args[0]);
    console.log(`Verified ${args[0]} as PVAD ${version}.`);
    return;
  }
  if (command === 'verify-container-metadata' && (args.length === 3 || (args.length === 4 && args[3] === '--allow-dirty'))) {
    const metadata = await verifyContainerMetadata(args[0], {
      expectedCommit: args[1], expectedBuildTime: args[2], allowDirty: args[3] === '--allow-dirty',
    });
    console.log(JSON.stringify(metadata));
    return;
  }
  throw new Error(
    'Usage: pvad-build-consistency.mjs write <directory> | verify <directory...> | verify-container <image> | verify-container-metadata <image> <commit> <time> [--allow-dirty]',
  );
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
