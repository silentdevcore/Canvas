import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { importFile } from '../public/examples/file-importer-flow/source.js';
import { importSpreadsheet } from '../public/examples/spreadsheet-import-export/source.js';
import { createViewerSession } from '../public/examples/pdf-viewer-annotations-forms/source.js';
import { migrateProviderCode } from '../public/examples/provider-migration-examples/source.js';
import { demos } from '../src/demoData.js';

test('all eight demo descriptors resolve to current downloadable examples', async () => {
  assert.equal(demos.length, 8);
  for (const demo of demos) {
    const read = relative => readFileSync(new URL(`../public${relative}`, import.meta.url), 'utf8');
    const input = JSON.parse(read(demo.inputFile));
    const output = JSON.parse(read(demo.outputFile));
    const module = await import(new URL(`../public${demo.source}`, import.meta.url));
    const functions = Object.values(module).filter(value => typeof value === 'function');
    assert.equal(functions.length, 1, demo.id);
    const result = functions[0](structuredClone(input));
    assert.ok(result && typeof result === 'object', demo.id);
    assert.equal(demo.download, demo.outputFile);
    const currentData = structuredClone({ output, result });
    if (demo.id === 'provider-migration-examples') {
      for (const value of Object.values(currentData)) {
        assert.deepEqual(value.diagnostics.map(item => item.id), ['PVADMIG001', 'PVADMIG101']);
        value.diagnostics.forEach(item => { delete item.id; });
      }
    }
    assert.doesNotMatch(JSON.stringify({ ...demo, ...currentData }), /pxa|power[\s_-]*dox/i, demo.id);
  }
});

test('provider example keeps stable raw diagnostics and supplied namespace unchanged', () => {
  const input = { provider: 'PVAD customer provider', domain: 'pdf', kind: 'code', targetNamespace: 'PVAD.Customer' };
  const result = migrateProviderCode(input);
  assert.equal(result.provider, input.provider);
  assert.equal(result.targetNamespace, input.targetNamespace);
  assert.deepEqual(result.diagnostics.map(item => item.id), ['PVADMIG001', 'PVADMIG101']);
  assert.equal(result.diagnostics[0].message, `Provider detected: ${input.provider} pdf code`);
  const output = JSON.parse(readFileSync(new URL(
    '../public/examples/provider-migration-examples/output.json', import.meta.url), 'utf8'));
  assert.equal(output.targetApi, 'PVAD.Generator');
  assert.deepEqual(output.diagnostics.map(item => item.id), result.diagnostics.map(item => item.id));
});

test('downloadable illustrative JSON uses the same identities as executable demo descriptors', () => {
  const read = path => JSON.parse(readFileSync(new URL(`../public/examples/${path}`, import.meta.url), 'utf8'));
  assert.equal(read('file-importer-flow/input.json').requestedOutput, 'PVAD.Core.Contracts.DesignExportDto');
  assert.equal(read('file-importer-flow/output.json').normalizedModel, 'PVAD.Core.Contracts.DesignExportDto');
  assert.equal(read('spreadsheet-import-export/output.json').normalizedModel, 'PVAD.Core.Contracts.SpreadsheetDto');
  assert.equal(read('pdf-viewer-annotations-forms/output.json').viewerState, 'PVADE PDF Viewer session');
});

test('illustrative demo descriptors use current identities without rewriting supplied values', () => {
  const file = importFile({ fileName: 'PVAD-customer.pdf', requestedOutput: 'design' });
  assert.equal(file.normalizedModel, 'PVAD.Core.Contracts.DesignExportDto');
  assert.equal(file.fileName, 'PVAD-customer.pdf');
  const rows = [['PVAD customer text']];
  const sheet = importSpreadsheet({ sheet: 'PVAD sheet', rows });
  assert.equal(sheet.normalizedModel, 'PVAD.Core.Contracts.SpreadsheetDto');
  assert.equal(sheet.sheets[0].name, 'PVAD sheet');
  assert.equal(sheet.sheets[0].rows, rows);
  const annotations = [{ text: 'PVAD document note' }];
  const viewer = createViewerSession({ document: 'PVAD.pdf', formFields: [], annotations });
  assert.equal(viewer.viewerState, 'PVADE PDF Viewer session');
  assert.equal(viewer.annotations, annotations);
  assert.equal(viewer.document, 'PVAD.pdf');
});
