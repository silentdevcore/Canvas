import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = path => readFileSync(new URL(`../../${path}`, import.meta.url));
const json = path => JSON.parse(read(path));
test('installed app declares only canonical project types and a fresh launch window', () => {
  const manifest = json('pvad-designer/public/manifest.webmanifest');
  const types = json('product-metadata/native-file-types.json');
  assert.equal(manifest.launch_handler.client_mode, 'navigate-new');
  const handler = manifest.file_handlers[0];
  assert.equal(handler.action, '/open-project');
  assert.equal(handler.launch_type, 'multiple-clients');
  for (const type of types.types) assert.deepEqual(handler.accept[type.mediaType], [type.extension]);
  assert.equal(Object.keys(handler.accept).length, types.types.length);
  assert.equal(types.nativeFileOpenSupported, false);
  assert.equal(types.browserFileOpenImplemented, true);
});
test('all six languages provide the same intake labels', () => {
  const keys = language => Object.keys(json(`pvad-designer/src/locales/${language}/nativeProject.json`)).sort();
  for (const language of ['de', 'fr', 'es', 'it', 'ar']) assert.deepEqual(keys(language), keys('en'));
});
test('install icons have their declared PNG dimensions', () => {
  for (const size of [192, 512]) {
    const bytes = read(`pvad-designer/public/icons/project-${size}.png`);
    assert.equal(bytes.subarray(1, 4).toString(), 'PNG');
    assert.equal(bytes.readUInt32BE(16), size);
    assert.equal(bytes.readUInt32BE(20), size);
  }
});
test('native route is allowlisted and worker has no persistent cache', () => {
  assert.match(read('deploy/websites/route-patterns.env.example').toString(), /open-project/);
  assert.doesNotMatch(read('pvad-designer/public/native-project-worker.js').toString(), /caches\.|indexedDB|localStorage|sessionStorage/);
});
