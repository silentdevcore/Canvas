import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const fixtures = new Map([
  ['nested/message.txt', Buffer.from('Synthetic artifact contract\n', 'utf8')],
  ['nested/file with spaces.txt', Buffer.from('Exact filename and contents\n', 'utf8')],
  ['empty.txt', Buffer.alloc(0)],
  ['binary.bin', Buffer.from(Array.from({ length: 256 }, (_, index) => index))],
]);

const hash = (bytes) => createHash('sha256').update(bytes).digest('hex');

export async function createFixture(directory) {
  for (const [name, bytes] of fixtures) {
    const target = join(directory, name);
    await mkdir(dirname(target), { recursive: true });
    await writeFile(target, bytes);
  }
}

async function listFiles(directory, prefix = '') {
  const files = [];
  for (const entry of await readdir(join(directory, prefix), { withFileTypes: true })) {
    const name = prefix ? `${prefix}/${entry.name}` : entry.name;
    if (entry.isDirectory()) files.push(...await listFiles(directory, name));
    else {
      assert(entry.isFile(), `Unexpected artifact entry: ${name}`);
      files.push(name);
    }
  }
  return files;
}

export async function verifyFixture(directory) {
  assert.deepEqual((await listFiles(directory)).sort(), [...fixtures.keys()].sort(), 'Artifact filenames changed');
  for (const [name, expected] of fixtures) {
    const actual = await readFile(join(directory, name));
    assert.equal(actual.length, expected.length, `Artifact size changed: ${name}`);
    assert.equal(hash(actual), hash(expected), `Artifact content changed: ${name}`);
  }
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  const [operation, directory] = process.argv.slice(2);
  assert(directory && ['create', 'verify'].includes(operation), 'Usage: artifact-handoff-contract.mjs create|verify DIRECTORY');
  if (operation === 'create') await createFixture(directory);
  else await verifyFixture(directory);
  console.log(`Artifact handoff ${operation} passed (${fixtures.size} synthetic files).`);
}
