/** @jest-environment jsdom */
import { configureSpreadsheetOwner, useSpreadsheetStore } from '../spreadsheet/store';

beforeEach(() => localStorage.clear());
afterEach(async () => { await configureSpreadsheetOwner(null, null); });

test('isolates drafts by user and organization and restores the matching owner', async () => {
  localStorage.setItem('pxa-spreadsheet', '{"state":{"name":"Unassigned"}}');
  await configureSpreadsheetOwner('a', 'one');
  expect(useSpreadsheetStore.getState().name).toBe('Workbook');
  useSpreadsheetStore.setState({ name: 'A one' });
  await configureSpreadsheetOwner('a', 'two');
  expect(useSpreadsheetStore.getState().name).toBe('Workbook');
  useSpreadsheetStore.setState({ name: 'A two' });
  await configureSpreadsheetOwner('b', 'one');
  expect(useSpreadsheetStore.getState().name).toBe('Workbook');
  await configureSpreadsheetOwner('a', 'one');
  expect(useSpreadsheetStore.getState().name).toBe('A one');
  await configureSpreadsheetOwner('a', 'two');
  expect(useSpreadsheetStore.getState().name).toBe('A two');
  expect(localStorage.getItem('pxa-spreadsheet')).toContain('Unassigned');
});

test('no owner means no persistence and no prior workbook content in memory', async () => {
  await configureSpreadsheetOwner('a', 'one');
  useSpreadsheetStore.setState({ name: 'Private' });
  await configureSpreadsheetOwner(null, null);
  expect(useSpreadsheetStore.getState().name).toBe('Workbook');
  const previous = localStorage.getItem('pvad-spreadsheet:v1:a:one');
  useSpreadsheetStore.setState({ name: 'Unowned' });
  expect(localStorage.getItem('pvad-spreadsheet:v1:a:one')).toBe(previous);
  expect(localStorage.getItem('pvad-spreadsheet-inactive')).toBeNull();
});

test('encoded scope components cannot collide', async () => {
  await configureSpreadsheetOwner('a:b', 'c');
  useSpreadsheetStore.setState({ name: 'First' });
  await configureSpreadsheetOwner('a', 'b:c');
  expect(useSpreadsheetStore.getState().name).toBe('Workbook');
});

test('malformed stored data fails closed and cannot be overwritten by later mutations', async () => {
  localStorage.setItem('pvad-spreadsheet:v1:a:one', 'broken-json');
  await expect(configureSpreadsheetOwner('a', 'one')).rejects.toThrow('could not be loaded');
  useSpreadsheetStore.setState({ name: 'Replacement' });
  expect(localStorage.getItem('pvad-spreadsheet:v1:a:one')).toBe('broken-json');
});
