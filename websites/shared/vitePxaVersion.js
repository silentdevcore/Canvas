import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

function readCommit() {
  if (process.env.PXA_BUILD_COMMIT) return process.env.PXA_BUILD_COMMIT;

  try {
    return execFileSync('git', ['rev-parse', '--short=12', 'HEAD'], {
      cwd: repositoryRoot,
      encoding: 'utf8',
    }).trim();
  } catch {
    return 'unknown';
  }
}

export function pxaVersionDefines() {
  return {
    __PXA_VERSION__: JSON.stringify(
      readFileSync(resolve(repositoryRoot, 'VERSION'), 'utf8').trim(),
    ),
    __PXA_BUILD_COMMIT__: JSON.stringify(readCommit()),
    __PXA_BUILD_TIME__: JSON.stringify(
      process.env.PXA_BUILD_TIME ?? new Date().toISOString(),
    ),
  };
}
