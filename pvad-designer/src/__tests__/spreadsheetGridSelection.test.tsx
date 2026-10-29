/** @jest-environment jsdom */
import React from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import type { DataEditorProps, GridSelection } from '@glideapps/glide-data-grid';
import { SpreadsheetGrid } from '../spreadsheet/SpreadsheetGrid';
import { useSpreadsheetStore } from '../spreadsheet/store';
import { emptySheet } from '../spreadsheet/types';

let mockGridProps: DataEditorProps;
jest.mock('@glideapps/glide-data-grid', () => ({
  GridCellKind: { Text: 'text' },
  CompactSelection: { empty: () => ({}) },
  DataEditor: (props: DataEditorProps) => {
    mockGridProps = props;
    return React.createElement('div');
  },
}));

const initial = useSpreadsheetStore.getState();
beforeEach(() => {
  useSpreadsheetStore.setState({ ...initial, sheets: [emptySheet('First')], active: 0 });
});
afterEach(() => {
  cleanup();
  useSpreadsheetStore.setState(initial);
});

function selectCells() {
  const selection: GridSelection = {
    ...mockGridProps.gridSelection!,
    current: { cell: [1, 1], range: { x: 1, y: 1, width: 2, height: 2 }, rangeStack: [] },
  };
  act(() => mockGridProps.onGridSelectionChange!(selection));
  return selection;
}

test('feeds controlled selection back to Glide and synchronizes the application range', () => {
  render(<SpreadsheetGrid />);
  const selection = selectCells();
  expect(mockGridProps.gridSelection).toBe(selection);
  expect(useSpreadsheetStore.getState().selection).toEqual({ row: 1, col: 1 });
  expect(useSpreadsheetStore.getState().range).toEqual({ r0: 1, c0: 1, r1: 2, c1: 2 });
});

test('clears the previous sheet selection when the active sheet changes', () => {
  render(<SpreadsheetGrid />);
  selectCells();
  act(() => useSpreadsheetStore.setState({ sheets: [emptySheet('Second')] }));
  expect(mockGridProps.gridSelection?.current).toBeUndefined();
});

test('ordinary application HTML provides the required single overlay portal', () => {
  const html = readFileSync(path.join(process.cwd(), 'index.html'), 'utf8');
  const parsed = new DOMParser().parseFromString(html, 'text/html');
  expect(parsed.querySelectorAll('#portal')).toHaveLength(1);
  expect(parsed.body.lastElementChild?.id).toBe('portal');
});
