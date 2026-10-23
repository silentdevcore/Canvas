import * as fs from 'fs';
import * as path from 'path';
import { workbookToWire, emptySheet, cellKey, type CellType } from '../spreadsheet/types';

// Validates the committed JSON Schema (docs/schema/pvad-workbook.schema.json) against the model:
//  - the schema's cell-type enum stays in lock-step with the CellType union, and
//  - a representative workbook satisfies the schema's structural (required-field) constraints.
// Schema-driven (reads the real schema file) without a full JSON-Schema validator dependency,
// mirroring designSchema.test.ts.

const schemaPath = path.resolve(__dirname, '../../../docs/schema/pvad-workbook.schema.json');
const schema = JSON.parse(fs.readFileSync(schemaPath, 'utf8'));

const requiredOf = (def: any): string[] => def.required ?? [];
const has = (obj: any, key: string) => obj != null && Object.prototype.hasOwnProperty.call(obj, key);

const ALL_CELL_TYPES: CellType[] = ['number', 'text', 'boolean', 'date', 'formula', 'empty'];

describe('pvad-workbook.schema.json — stays in sync with the model', () => {
  test('the PVAD schema is primary and retains the legacy validation contract', () => {
    expect(schema.$id).toBe('https://pvade.com/schemas/pvad-workbook.schema.json');
    expect(schema.title).toBe('PVADS Workbook JSON');
    const legacy = JSON.parse(fs.readFileSync(path.resolve(__dirname, '../../../docs/schema/pxa-workbook.schema.json'), 'utf8'));
    const constraints = (value: any): any => Array.isArray(value) ? value.map(constraints)
      : value && typeof value === 'object' ? Object.fromEntries(Object.entries(value)
        .filter(([key, item]) => !(typeof item === 'string' && ['$id', 'title', 'description'].includes(key)))
        .map(([key, item]) => [key, constraints(item)])) : value;
    expect(constraints(schema)).toEqual(constraints(legacy));
  });

  test('the schema cell-type enum matches the CellType union exactly', () => {
    const enumTypes: string[] = schema.$defs.cell.properties.type.enum;
    expect([...enumTypes].sort()).toEqual([...ALL_CELL_TYPES].sort());
  });

  test('a representative workbook satisfies the schema required-field constraints', () => {
    const sheet = emptySheet('Sheet1');
    sheet.cells[cellKey(0, 0)] = { row: 0, col: 0, type: 'text', value: 'Hi', style: { bold: true }, comment: 'note' };
    sheet.cells[cellKey(1, 0)] = { row: 1, col: 0, type: 'formula', formula: '=A1' };
    sheet.columns = [{ index: 0, width: 20, outlineLevel: 1 }];
    sheet.autoFilterRange = 'A1:B2';
    sheet.pageSetup = { orientation: 'landscape' };
    sheet.protection = { protected: true };
    sheet.conditionalFormats = [{ range: 'A1', type: 'cellIs', operator: 'greaterThan', value: '3' }];
    sheet.dataValidations = [{ range: 'A2', type: 'list', listSource: 'a,b,c' }];

    const wb: any = workbookToWire('Book', [sheet], [{ name: 'Sales', refersTo: 'Sheet1!$A$1' }], '1.0');

    for (const k of requiredOf(schema)) expect(has(wb, k)).toBe(true);            // id, name, sheets
    const s = wb.sheets[0];
    for (const k of requiredOf(schema.$defs.sheet)) expect(has(s, k)).toBe(true);  // id, name

    for (const cell of s.cells) {
      for (const k of requiredOf(schema.$defs.cell)) expect(has(cell, k)).toBe(true); // row, col, type
      expect(schema.$defs.cell.properties.type.enum).toContain(cell.type);
    }
    for (const k of requiredOf(schema.$defs.column)) expect(has(s.columns[0], k)).toBe(true);            // index
    for (const k of requiredOf(schema.$defs.conditionalFormat)) expect(has(s.conditionalFormats[0], k)).toBe(true); // range, type
    for (const k of requiredOf(schema.$defs.dataValidation)) expect(has(s.dataValidations[0], k)).toBe(true);       // range, type
    expect(schema.$defs.conditionalFormat.properties.type.enum).toContain(s.conditionalFormats[0].type);
    expect(schema.$defs.dataValidation.properties.type.enum).toContain(s.dataValidations[0].type);
    for (const k of requiredOf(schema.$defs.definedName)) expect(has(wb.definedNames[0], k)).toBe(true); // name, refersTo
  });
});
