import * as fs from 'fs';
import * as path from 'path';
import { ELEMENT_TYPES } from '../types';
import { ELEMENT_CATALOG, toDesign } from '../docs/elementCatalog';

// Validates the committed JSON Schema (docs/schema/design-export.schema.json) against the catalog:
//  - the schema's element-type enum stays in lock-step with ELEMENT_TYPES, and
//  - every catalog example, wrapped into a DesignExportDto, satisfies the schema's structural constraints.
// Schema-driven (reads the real schema file) without pulling in a full JSON-Schema validator dependency.

const schemaPath = path.resolve(__dirname, '../../../docs/schema/design-export.schema.json');
const schema = JSON.parse(fs.readFileSync(schemaPath, 'utf8'));

const requiredOf = (def: any): string[] => def.required ?? [];
const has = (obj: any, key: string) => obj != null && Object.prototype.hasOwnProperty.call(obj, key);

describe('design-export.schema.json — stays in sync with the catalog', () => {
  test('the schema element-type enum matches ELEMENT_TYPES exactly', () => {
    const enumTypes: string[] = schema.$defs.element.properties.type.enum;
    expect([...enumTypes].sort()).toEqual([...ELEMENT_TYPES].sort());
  });

  test('every catalog example is a schema-valid DesignExportDto', () => {
    const designReq = requiredOf(schema);
    const pageReq = requiredOf(schema.$defs.page);
    const elemReq = requiredOf(schema.$defs.element);
    const enumTypes: string[] = schema.$defs.element.properties.type.enum;

    for (const entry of ELEMENT_CATALOG) {
      const design: any = toDesign(entry.example, entry.label);

      // design envelope
      for (const k of designReq) expect(has(design, k)).toBe(true);
      expect(Array.isArray(design.pages)).toBe(true);

      // page
      const page = design.pages[0];
      for (const k of pageReq) expect(has(page, k)).toBe(true);

      // element
      const el = page.elements[0];
      for (const k of elemReq) {
        expect(has(el, k)).toBe(true); // required field present (e.g. id, type, x, y, width, height)
      }
      expect(enumTypes).toContain(el.type);
      for (const dim of ['x', 'y', 'width', 'height']) expect(typeof el[dim]).toBe('number');
    }
  });
});
