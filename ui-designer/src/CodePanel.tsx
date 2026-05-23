import React from 'react';
import { useDesignerStore } from './store';
import { generateCSharp } from './codegen';

const CodePanel: React.FC = () => {
  const { elements, rootIds } = useDesignerStore();
  const code = generateCSharp(elements, rootIds);

  return (
    <section className="ui-mono-panel">
      <pre className="ui-mono-panel-pre">{code}</pre>
    </section>
  );
};

export default CodePanel;
