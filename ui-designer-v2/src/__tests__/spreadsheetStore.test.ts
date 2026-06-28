import { useSpreadsheetStore, parseInput } from '../spreadsheet/store';
import { emptySheet } from '../spreadsheet/types';

const reset = () => {
  useSpreadsheetStore.setState({
    name: 'Workbook', sheets: [emptySheet()], active: 0,
    selection: { row: 0, col: 0 }, range: null, past: [], future: [],
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

  test('applyStyle styles every cell of the selected range', () => {
    const s = useSpreadsheetStore.getState();
    s.selectRange({ r0: 0, c0: 0, r1: 1, c1: 1 });
    s.applyStyle({ bold: true });
    const g = useSpreadsheetStore.getState();
    expect(g.cellAt(0, 0)?.style?.bold).toBe(true);
    expect(g.cellAt(1, 1)?.style?.bold).toBe(true);
  });

  test('selectionStats sums/averages the numeric cells in the range', () => {
    const s = useSpreadsheetStore.getState();
    s.setCellInput(0, 0, '10');
    s.setCellInput(0, 1, '20');
    s.setCellInput(0, 2, 'text'); // ignored
    s.selectRange({ r0: 0, c0: 0, r1: 0, c1: 2 });
    const stats = useSpreadsheetStore.getState().selectionStats();
    expect(stats).toEqual({ sum: 30, avg: 15, count: 2 });
  });

  test('insertRow shifts data + formula references (HyperFormula)', () => {
    const s = useSpreadsheetStore.getState();
    s.setCellInput(0, 0, '10');           // A1
    s.setCellInput(1, 0, '20');           // A2
    s.setCellInput(2, 0, '=SUM(A1:A2)');  // A3 = 30
    s.insertRow(0);                        // new top row → everything shifts down
    const g = useSpreadsheetStore.getState();
    expect(g.cellAt(3, 0)?.type).toBe('formula');
    expect(g.cellAt(3, 0)?.formula).toBe('=SUM(A2:A3)'); // references shifted
    expect(g.computed(3, 0)).toBe(30);                   // still correct
  });

  test('deleteCol removes the column and shifts the rest left', () => {
    const s = useSpreadsheetStore.getState();
    s.setCellInput(0, 0, 'a');
    s.setCellInput(0, 1, 'b');
    s.setCellInput(0, 2, 'c');
    s.deleteCol(1);
    const g = useSpreadsheetStore.getState();
    expect(g.cellAt(0, 0)?.value).toBe('a');
    expect(g.cellAt(0, 1)?.value).toBe('c'); // c shifted from col 2 → 1
    expect(g.cellAt(0, 2)).toBeUndefined();
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
