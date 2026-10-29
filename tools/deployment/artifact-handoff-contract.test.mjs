import assert from 'node:assert/strict';
import { mkdtemp, rm, unlink, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { createFixture, verifyFixture } from './artifact-handoff-contract.mjs';

async function fixture(t) {
  const directory = await mkdtemp(join(tmpdir(), 'pvad-artifact-contract-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  await createFixture(directory);
  return directory;
}

test('accepts nested filenames, spaces, empty files and exact binary contents', async (t) => {
  await verifyFixture(await fixture(t));
});

test('rejects modified content even when its length is unchanged', async (t) => {
  const directory = await fixture(t);
  await writeFile(join(directory, 'binary.bin'), Buffer.alloc(256));
  await assert.rejects(verifyFixture(directory), /content changed/);
});

test('rejects missing and unexpected files', async (t) => {
  const directory = await fixture(t);
  await unlink(join(directory, 'empty.txt'));
  await assert.rejects(verifyFixture(directory), /filenames changed/);
  await writeFile(join(directory, 'empty.txt'), '');
  await writeFile(join(directory, 'unexpected.txt'), 'unexpected');
  await assert.rejects(verifyFixture(directory), /filenames changed/);
});
