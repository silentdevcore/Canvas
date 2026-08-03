#!/usr/bin/env node

import { execFile } from 'node:child_process';
import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { promisify } from 'node:util';

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
  if (buildInfo.product !== 'PXA') throw new Error(`${source}: product must be PXA.`);
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
    product: 'PXA',
    productVersion,
    commitId: commitId ?? process.env.PXA_BUILD_COMMIT ?? 'unknown',
    buildTime: buildTime ?? process.env.PXA_BUILD_TIME ?? new Date().toISOString(),
  };
  validateBuildInfo(info, productVersion, 'generated build info');
  await mkdir(resolve(outputDirectory), { recursive: true });
  await writeFile(
    resolve(outputDirectory, 'pxa-build-info.json'),
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
    const path = resolve(directory, 'pxa-build-info.json');
    let buildInfo;
    try {
      buildInfo = JSON.parse(await readFile(path, 'utf8'));
    } catch (error) {
      throw new Error(`${directory}: cannot read pxa-build-info.json: ${error.message}`);
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

async function main() {
  const [command, ...args] = process.argv.slice(2);
  if (command === 'write' && args.length === 1) {
    const info = await writeBuildInfo(args[0]);
    console.log(JSON.stringify(info));
    return;
  }
  if (command === 'verify' && args.length > 0) {
    const version = await verifyBuildDirectories(args);
    console.log(`Verified ${args.length} PXA ${version} build artifact(s).`);
    return;
  }
  if (command === 'verify-container' && args.length === 1) {
    const version = await verifyContainerVersion(args[0]);
    console.log(`Verified ${args[0]} as PXA ${version}.`);
    return;
  }
  throw new Error(
    'Usage: pxa-build-consistency.mjs write <directory> | verify <directory...> | verify-container <image>',
  );
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) {
  main().catch(error => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
