import { useSpreadsheetStore, parseInput } from '../spreadsheet/store';
import { emptySheet } from '../spreadsheet/types';

const reset = () => {
  useSpreadsheetStore.setState({
    name: 'Workbook', sheets: [emptySheet()], active: 0,
    selection: { row: 0, col: 0 }, past: [], future: [],
  });
  useSpreadsheetStore.getState().rebuildEngine();
};

describe('spreadsheet store + HyperFormula recalc', () => {
  beforeEach(reset);

  test('=SUM(A1:A2) computes and recalculates when an input changes', () => {
    const s = useSpreadsheetStore.getState();
    s.setCellInput(0, 0, '10');           // A1
    s.setCellInput(1, 0, '20');           // A2
    s.setCellInput(2, 0, '=SUM(A1:A2)');  // A3
    expect(useSpreadsheetStore.getState().computed(2, 0)).toBe(30);

    useSpreadsheetStore.getState().setCellInput(0, 0, '100'); // A1 → 100
    expect(useSpreadsheetStore.getState().computed(2, 0)).toBe(120);
  });

  test('cross-function formula (IF + arithmetic)', () => {
    const s = useSpreadsheetStore.getState();
    s.setCellInput(0, 0, '5');                       // A1
    s.setCellInput(0, 1, '=IF(A1>3, A1*2, 0)');     // B1
    expect(useSpreadsheetStore.getState().computed(0, 1)).toBe(10);
  });

  test('parseInput classifies formula / number / text', () => {
    expect(parseInput('=A1+1').type).toBe('formula');
    expect(parseInput('42').type).toBe('number');
    expect(parseInput('42').value).toBe(42);
    expect(parseInput('hello').type).toBe('text');
    expect(parseInput('').type).toBe('empty');
  });

  test('undo/redo restores cell values', () => {
    const s = useSpreadsheetStore.getState();
    s.setCellInput(0, 0, 'a');
    s.setCellInput(0, 0, 'b');
    expect(useSpreadsheetStore.getState().cellAt(0, 0)?.value).toBe('b');
    useSpreadsheetStore.getState().undo();
    expect(useSpreadsheetStore.getState().cellAt(0, 0)?.value).toBe('a');
    useSpreadsheetStore.getState().redo();
    expect(useSpreadsheetStore.getState().cellAt(0, 0)?.value).toBe('b');
  });

  test('toWire produces a sparse workbook the backend can read', () => {
    const s = useSpreadsheetStore.getState();
    s.setCellInput(0, 0, 'Hello');
    s.setCellInput(1, 1, '=1+1');
    const wire = useSpreadsheetStore.getState().toWire();
    expect(wire.sheets).toHaveLength(1);
    const cells = wire.sheets[0].cells;
    expect(cells.find((c) => c.row === 0 && c.col === 0)?.value).toBe('Hello');
    expect(cells.find((c) => c.row === 1 && c.col === 1)?.formula).toBe('=1+1');
  });
});
