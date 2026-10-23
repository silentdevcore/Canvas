import assert from 'node:assert/strict';
import test from 'node:test';
import { readMigratedStorage } from './storageMigration.js';

test('migration preserves legacy values on failed writes and canonical values win', () => {
  const values = new Map([['old:a', 'de'], ['old:b', 'fr']]);
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: () => { throw new Error('quota'); },
    removeItem: key => values.delete(key),
  };
  assert.equal(readMigratedStorage(storage, 'new:a', 'old:a'), 'de');
  assert.equal(values.get('old:a'), 'de');
  storage.setItem = (key, value) => values.set(key, value);
  assert.equal(readMigratedStorage(storage, 'new:a', 'old:a'), 'de');
  assert.equal(values.has('old:a'), false);
  assert.equal(values.get('old:b'), 'fr');
  values.set('new:a', '');
  values.set('old:a', 'ar');
  assert.equal(readMigratedStorage(storage, 'new:a', 'old:a'), '');
});
