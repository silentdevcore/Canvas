import React from 'react';
import { useDesignerStore } from './store';

const JSONPanel: React.FC = () => {
  const { elements, rootIds } = useDesignerStore();
  const json = JSON.stringify({ rootIds, elements }, null, 2);
  return (
    <section className="ui-mono-panel">
      <pre className="ui-mono-panel-pre">{json}</pre>
    </section>
  );
};

export default JSONPanel;
