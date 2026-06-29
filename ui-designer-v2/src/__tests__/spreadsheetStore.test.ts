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

  test('pasteValues writes a block (incl. formulas); clearRange clears it', () => {
    const s = useSpreadsheetStore.getState();
    s.pasteValues(0, 0, [['1', '2'], ['3', '=A1+B1']]);
    let g = useSpreadsheetStore.getState();
    expect(g.cellAt(0, 0)?.value).toBe(1);
    expect(g.cellAt(1, 1)?.formula).toBe('=A1+B1');
    expect(g.computed(1, 1)).toBe(3); // A1 + B1 = 1 + 2

    g.selectRange({ r0: 0, c0: 0, r1: 1, c1: 1 });
    useSpreadsheetStore.getState().clearRange();
    g = useSpreadsheetStore.getState();
    expect(g.cellAt(0, 0)).toBeUndefined();
    expect(g.cellAt(1, 1)).toBeUndefined();
  });

  test('merge / unmerge / freeze update the sheet model', () => {
    const s = useSpreadsheetStore.getState();
    s.selectRange({ r0: 0, c0: 0, r1: 0, c1: 2 });
    s.mergeSelection();
    expect(useSpreadsheetStore.getState().activeSheet().merges).toContain('A1:C1');

    useSpreadsheetStore.getState().setFrozen(2, 1);
    expect(useSpreadsheetStore.getState().activeSheet().frozenRows).toBe(2);
    expect(useSpreadsheetStore.getState().activeSheet().frozenCols).toBe(1);

    useSpreadsheetStore.getState().selectRange({ r0: 0, c0: 1, r1: 0, c1: 1 }); // a cell inside the merge
    useSpreadsheetStore.getState().unmergeSelection();
    expect(useSpreadsheetStore.getState().activeSheet().merges).toEqual([]);
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

  test('setCellMeta + patchSheet land in the exported wire', () => {
    const s = useSpreadsheetStore.getState();
    s.setCellMeta(0, 0, { comment: 'note', hyperlink: 'https://x.com' });
    s.patchSheet({
      pageSetup: { orientation: 'landscape', header: 'Report' },
      protection: { protected: true },
      autoFilterRange: 'A1:C5',
    });

    const sheet = useSpreadsheetStore.getState().toWire().sheets[0];
    const cell = sheet.cells.find((c) => c.row === 0 && c.col === 0);
    expect(cell?.comment).toBe('note');
    expect(cell?.hyperlink).toBe('https://x.com');
    expect(sheet.pageSetup).toMatchObject({ orientation: 'landscape', header: 'Report' });
    expect(sheet.protection).toMatchObject({ protected: true });
    expect(sheet.autoFilterRange).toBe('A1:C5');
  });

  test('conditional-format + data-validation rules add/remove and reach the wire', () => {
    const s = useSpreadsheetStore.getState();
    s.addConditionalFormat({ range: 'A1:A10', type: 'cellIs', operator: 'greaterThan', value: '100', color: '#ff0000' });
    s.addDataValidation({ range: 'B1:B10', type: 'list', listSource: 'a,b,c' });

    let sheet = useSpreadsheetStore.getState().toWire().sheets[0];
    expect(sheet.conditionalFormats).toHaveLength(1);
    expect(sheet.conditionalFormats?.[0]).toMatchObject({ range: 'A1:A10', operator: 'greaterThan', value: '100', color: '#ff0000' });
    expect(sheet.dataValidations?.[0]).toMatchObject({ range: 'B1:B10', type: 'list', listSource: 'a,b,c' });

    useSpreadsheetStore.getState().removeConditionalFormat(0);
    useSpreadsheetStore.getState().removeDataValidation(0);
    sheet = useSpreadsheetStore.getState().toWire().sheets[0];
    expect(sheet.conditionalFormats).toHaveLength(0);
    expect(sheet.dataValidations).toHaveLength(0);
  });
});
