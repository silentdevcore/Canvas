import React, { StrictMode, useState } from 'react';
import { createRoot } from 'react-dom/client';
import JsonEditorPanel from '../../src/components/Editor/JsonEditorPanel';
import PxaSurface from '../../src/components/Editor/PxaSurface';
import { useEditorStore, type SimpleElement } from '../../src/store';
import { SpreadsheetGrid } from '../../src/spreadsheet/SpreadsheetGrid';
import { useSpreadsheetStore } from '../../src/spreadsheet/store';
import { emptySheet } from '../../src/spreadsheet/types';
import 'react-resizable/css/styles.css';

const data = { title: 'Invoice', quantity: 2, nested: { enabled: true } };
const elements: SimpleElement[] = [{
  id: 'synthetic-compatible-text', type: 'text', x: 20, y: 20, width: 220,
  height: 60, content: 'Synthetic document title', style: { width: 220, height: 60 },
}];
useEditorStore.getState().setCurrentTemplate({
  id: 'synthetic-react-compatibility-template', name: 'Component fixture', category: 'test',
  description: '', pages: [{ id: 'synthetic-page', elements }], sharedElements: [], data,
});
useEditorStore.getState().updateJsonData(data);
useSpreadsheetStore.setState({ sheets: [emptySheet('Synthetic sheet')], active: 0, past: [], future: [] });
useSpreadsheetStore.getState().setCellInput(0, 0, '5');
useSpreadsheetStore.getState().setCellInput(0, 1, '=A1*2');
const view = new URLSearchParams(location.search).get('view');
if (!['json', 'legacy', 'spreadsheet'].includes(view ?? '')) throw new Error('Unknown fixture view');

function Fixture() {
  const [json, setJson] = useState<Record<string, unknown>>(data);
  const template = useEditorStore(state => state.currentTemplate)!;
  const selected = useEditorStore(state => state.selectedElementId);
  const storedJson = useEditorStore(state => state.jsonData);
  const sheet = useSpreadsheetStore(state => state.sheets[state.active]);
  const selection = useSpreadsheetStore(state => state.selection);
  return <>
    <style>{`
      body { margin: 16px; font: 14px Arial, sans-serif; background: white; color: black; }
      .canvas { position: relative; border: 1px solid #999; }
      .canvas > div { background: white; }
      .is-selected { outline: 1px solid #24854b; }
      #grid { width: min(800px, 100%); height: 400px; }
      output { display: block; margin-top: 16px; overflow-wrap: anywhere; }
    `}</style>
    {view === 'json' && <JsonEditorPanel jsonData={json} onUpdate={updated => {
      setJson(updated);
      useEditorStore.getState().updateJsonData(updated);
    }} />}
    {view === 'legacy' && <PxaSurface elements={template.pages[0].elements} />}
    {view === 'spreadsheet' && <div id="grid"><SpreadsheetGrid /></div>}
    <output id="json-state">{JSON.stringify(storedJson)}</output>
    <output id="selection-state">{selected ?? 'none'}</output>
    <output id="sheet-state">{JSON.stringify({ selection, input: sheet.cells['0:0']?.value,
      formula: sheet.cells['0:1']?.formula, result: useSpreadsheetStore.getState().computed(0, 1) })}</output>
  </>;
}

createRoot(document.getElementById('root')!).render(<StrictMode><Fixture /></StrictMode>);
