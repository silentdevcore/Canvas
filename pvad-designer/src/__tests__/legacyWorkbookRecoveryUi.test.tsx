/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import LegacyWorkbookRecovery from '../components/Project/LegacyWorkbookRecovery';
import { downloadText } from '../spreadsheet/io';
import { emptySheet } from '../spreadsheet/types';

jest.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
jest.mock('../spreadsheet/io', () => ({ downloadText: jest.fn() }));
(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
let root: Root;
let container: HTMLDivElement;
beforeEach(() => {
  jest.clearAllMocks(); localStorage.clear();
  container = document.createElement('div');
  root = createRoot(container);
});
afterEach(() => { act(() => root.unmount()); jest.restoreAllMocks(); });

test('recovery only reads on request, downloads on explicit action and retains the original', () => {
  const raw = JSON.stringify({ state: { name: 'Old', sheets: [emptySheet()] } });
  localStorage.setItem('pxa-spreadsheet', raw);
  const read = jest.spyOn(Storage.prototype, 'getItem');
  act(() => root.render(<LegacyWorkbookRecovery />));
  expect(read).not.toHaveBeenCalled();
  expect(downloadText).not.toHaveBeenCalled();
  act(() => container.querySelector('button')!.click());
  const button = Array.from(container.querySelectorAll('button')).find(item => item.textContent?.includes('legacyRecovery.download'))!;
  expect(downloadText).not.toHaveBeenCalled();
  act(() => button.click());
  expect(downloadText).toHaveBeenCalledWith(expect.stringContaining('"name": "Old"'), 'recovered-workbook.json', 'application/json');
  expect(localStorage.getItem('pxa-spreadsheet')).toBe(raw);
});

test('invalid data shows a localized failure without downloading or deleting it', () => {
  localStorage.setItem('canvas-spreadsheet', 'broken');
  act(() => root.render(<LegacyWorkbookRecovery />));
  act(() => container.querySelector('button')!.click());
  act(() => Array.from(container.querySelectorAll('button')).find(item => item.textContent?.includes('legacyRecovery.download'))!.click());
  expect(container.querySelector('[role="alert"]')?.textContent).toBe('legacyRecovery.failed');
  expect(downloadText).not.toHaveBeenCalled();
  expect(localStorage.getItem('canvas-spreadsheet')).toBe('broken');
});
