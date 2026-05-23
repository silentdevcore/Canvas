import React from 'react';
import { useEditorStore } from '../../store';
import Toolbar from './Toolbar';
import Canvas from './Canvas'; // We'll create this next
import PropertiesPanel from './PropertiesPanel'; // We'll port this
import JsonEditorPanel from './JsonEditorPanel'; // We'll create this

interface ComplexEditorProps {
  onPreview: () => void;
  onBack: () => void;
}

const ComplexEditor: React.FC<ComplexEditorProps> = ({ onPreview, onBack }) => {
  const currentTemplate = useEditorStore((state) => state.currentTemplate);

  if (!currentTemplate) return null;

  return (
    <div className="complex-editor">
      <header>
        {/* Add header with back and preview buttons */}
        <button onClick={onBack}>Back</button>
        <h1>{currentTemplate.name}</h1>
        <button onClick={onPreview}>Preview</button>
      </header>
      <div className="editor-layout">
        <Toolbar />
        <Canvas elements={currentTemplate.pages?.[0]?.elements ?? []} />
        <PropertiesPanel />
        <JsonEditorPanel jsonData={currentTemplate.data} onUpdate={useEditorStore((state) => state.updateJsonData)} />
      </div>
    </div>
  );
};

export default ComplexEditor;