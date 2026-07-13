import React from 'react';
import ReactJson from 'react-json-view';

interface JsonEditorPanelProps {
  jsonData: Record<string, any>;
  onUpdate: (data: Record<string, any>) => void;
}

const JsonEditorPanel: React.FC<JsonEditorPanelProps> = ({ jsonData, onUpdate }) => {
  return (
    <div className="json-editor-panel">
      <h3>JSON Data Editor</h3>
      <ReactJson
        src={jsonData}
        onEdit={(edit) => {
          onUpdate(edit.updated_src);
          return true;
        }}
        onAdd={(add) => {
          onUpdate(add.updated_src);
          return true;
        }}
        onDelete={(del) => {
          onUpdate(del.updated_src);
          return true;
        }}
        theme="rjv-default"
        iconStyle="square"
        collapsed={false}
      />
    </div>
  );
};

export default JsonEditorPanel;