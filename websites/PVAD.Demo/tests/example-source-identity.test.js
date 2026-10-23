import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { importFile } from '../public/examples/file-importer-flow/source.js';
import { importSpreadsheet } from '../public/examples/spreadsheet-import-export/source.js';
import { createViewerSession } from '../public/examples/pdf-viewer-annotations-forms/source.js';

test('downloadable illustrative JSON uses the same identities as executable demo descriptors', () => {
  const read = path => JSON.parse(readFileSync(new URL(`../public/examples/${path}`, import.meta.url), 'utf8'));
  assert.equal(read('file-importer-flow/input.json').requestedOutput, 'PVAD.Core.Contracts.DesignExportDto');
  assert.equal(read('file-importer-flow/output.json').normalizedModel, 'PVAD.Core.Contracts.DesignExportDto');
  assert.equal(read('spreadsheet-import-export/output.json').normalizedModel, 'PVAD.Core.Contracts.SpreadsheetDto');
  assert.equal(read('pdf-viewer-annotations-forms/output.json').viewerState, 'PVADE PDF Viewer session');
});

test('illustrative demo descriptors use current identities without rewriting supplied values', () => {
  const file = importFile({ fileName: 'PXA-customer.pdf', requestedOutput: 'design' });
  assert.equal(file.normalizedModel, 'PVAD.Core.Contracts.DesignExportDto');
  assert.equal(file.fileName, 'PXA-customer.pdf');
  const rows = [['PXA customer text']];
  const sheet = importSpreadsheet({ sheet: 'PXA sheet', rows });
  assert.equal(sheet.normalizedModel, 'PVAD.Core.Contracts.SpreadsheetDto');
  assert.equal(sheet.sheets[0].name, 'PXA sheet');
  assert.equal(sheet.sheets[0].rows, rows);
  const annotations = [{ text: 'PXA document note' }];
  const viewer = createViewerSession({ document: 'PXA.pdf', formFields: [], annotations });
  assert.equal(viewer.viewerState, 'PVADE PDF Viewer session');
  assert.equal(viewer.annotations, annotations);
  assert.equal(viewer.document, 'PXA.pdf');
});
