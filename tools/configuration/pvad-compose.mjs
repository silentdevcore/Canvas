#!/usr/bin/env node
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { parseEnv } from 'node:util';
import { pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';
import { normalizePvadEnvironment } from './environment.mjs';

export async function prepareComposeEnvironment(args, environment = process.env, cwd = process.cwd()) {
  const files = [];
  for (let index = 0; index < args.length; index++) {
    if (args[index] === '--env-file') {
      if (!args[index + 1]) throw new Error('--env-file requires a path.');
      files.push(args[++index]);
    } else if (args[index].startsWith('--env-file=')) {
      files.push(args[index].slice('--env-file='.length));
    }
  }
  const explicit = files.length > 0;
  if (!explicit) files.push('.env');
  let loaded = {};
  for (const file of files) {
    try {
      loaded = { ...loaded, ...parseEnv(await readFile(resolve(cwd, file), 'utf8')) };
    } catch (error) {
      if (explicit || error.code !== 'ENOENT') throw error;
    }
  }
  for (const [key, value] of Object.entries(loaded)) {
    if (value.includes('${'))
      throw new Error(`Resolve interpolation in env-file key ${key} before using the PVAD launcher.`);
  }
  return normalizePvadEnvironment({ ...loaded, ...environment });
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const args = process.argv.slice(2);
    const environment = await prepareComposeEnvironment(args);
    const result = spawnSync('docker', ['compose', ...args], { env: environment, stdio: 'inherit' });
    if (result.error) throw result.error;
    process.exitCode = result.status ?? 1;
  } catch (error) {
    console.error(error.message);
    process.exitCode = 1;
  }
}
