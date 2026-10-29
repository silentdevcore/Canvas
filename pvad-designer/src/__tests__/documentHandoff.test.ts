/** @jest-environment jsdom */
import { clearDocumentHandoffs, consumeDocumentHandoff, documentHandoffKeys, readDocumentHandoff,
  setDocumentHandoffOwner, writeDocumentHandoff } from '../auth/documentHandoff';

const key = documentHandoffKeys.migration;
beforeEach(() => { clearDocumentHandoffs(); sessionStorage.clear(); setDocumentHandoffOwner('a', 'one'); });
afterEach(() => { jest.restoreAllMocks(); jest.useRealTimers(); clearDocumentHandoffs(); });

test('roundtrips only within the same authenticated user and organization', () => {
  writeDocumentHandoff(key, { name: 'Private' });
  expect(readDocumentHandoff(key)).toEqual({ name: 'Private' });
  setDocumentHandoffOwner('a', 'two');
  expect(readDocumentHandoff(key)).toBeNull();
  setDocumentHandoffOwner('b', 'one');
  expect(readDocumentHandoff(key)).toBeNull();
  setDocumentHandoffOwner('a', 'one');
  expect(readDocumentHandoff(key)).toEqual({ name: 'Private' });
  consumeDocumentHandoff(key);
  expect(readDocumentHandoff(key)).toBeNull();
});

test('rejects expired, malformed and unassigned legacy transfers', () => {
  jest.useFakeTimers();
  writeDocumentHandoff(key, { name: 'Private' });
  jest.advanceTimersByTime(5 * 60_000);
  expect(readDocumentHandoff(key)).toBeNull();
  clearDocumentHandoffs(); setDocumentHandoffOwner('a', 'one');
  sessionStorage.setItem(key, 'broken-json');
  expect(readDocumentHandoff(key)).toBeNull();
  sessionStorage.setItem('pxa_migration_designer_handoff', '{"template":{"name":"Unassigned"}}');
  sessionStorage.removeItem(key);
  expect(readDocumentHandoff(key)).toBeNull();
});

test('quota fallback is same-owner only and cleanup removes in-memory documents', () => {
  jest.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('quota'); });
  writeDocumentHandoff(key, { name: 'Large preview' });
  expect(readDocumentHandoff(key)).toEqual({ name: 'Large preview' });
  setDocumentHandoffOwner('b', 'one');
  expect(readDocumentHandoff(key)).toBeNull();
  setDocumentHandoffOwner('a', 'one');
  expect(readDocumentHandoff(key)).toBeNull();
  clearDocumentHandoffs();
  expect(() => writeDocumentHandoff(key, {})).toThrow('authenticated');
});
