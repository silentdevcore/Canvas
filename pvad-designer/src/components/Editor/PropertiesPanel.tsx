import React from 'react';
import { useEditorStore, SimpleElement } from '../../store';

const PropertiesPanel: React.FC = () => {
  const { selectedElementId, currentTemplate, updateElement } = useEditorStore();

  if (!selectedElementId || !currentTemplate) return <div>Select an element to edit properties</div>;

  const element = (currentTemplate.pages?.flatMap(p => p.elements) ?? []).find((el: any) => el.id === selectedElementId);
  if (!element) return null;

  const handleChange = (key: keyof SimpleElement, value: any) => {
    updateElement(selectedElementId, { [key]: value });
  };

  return (
    <div className="properties-panel">
      <h3>{element.type} Properties</h3>
      <label>
        X:
        <input type="number" value={element.x} onChange={(e) => handleChange('x', parseInt(e.target.value))} />
      </label>
      <label>
        Y:
        <input type="number" value={element.y} onChange={(e) => handleChange('y', parseInt(e.target.value))} />
      </label>
      <label>
        Width:
        <input type="number" value={element.width} onChange={(e) => handleChange('width', parseInt(e.target.value))} />
      </label>
      <label>
        Height:
        <input type="number" value={element.height} onChange={(e) => handleChange('height', parseInt(e.target.value))} />
      </label>
      {['text', 'button'].includes(element.type) && (
        <label>
          Content:
          <input type="text" value={element.content} onChange={(e) => handleChange('content', e.target.value)} />
        </label>
      )}
      {element.type === 'chart' && (
        <>
          <label>
            Chart Type:
            <select value={element.chartType} onChange={(e) => handleChange('chartType', e.target.value)}>
              <option value="bar">Bar</option>
              <option value="line">Line</option>
              <option value="pie">Pie</option>
            </select>
          </label>
          <label>
            Chart Data (JSON):
            <textarea value={JSON.stringify(element.chartData)} onChange={(e) => handleChange('chartData', JSON.parse(e.target.value))} />
          </label>
        </>
      )}
      {['dropdown', 'optionlist', 'radio'].includes(element.type) && (
        <label>
          Options (comma-separated):
          <input type="text" value={element.options?.join(', ')} onChange={(e) => handleChange('options', e.target.value.split(', ').filter(Boolean))} />
        </label>
      )}
      <label>
        Binding:
        <input type="text" value={element.binding || ''} onChange={(e) => handleChange('binding', e.target.value)} />
      </label>
      <label>
        Expression:
        <input type="text" value={element.expression || ''} onChange={(e) => handleChange('expression', e.target.value)} />
      </label>
      <label>
        Formatter:
        <input type="text" value={element.formatter || ''} onChange={(e) => handleChange('formatter', e.target.value)} />
      </label>
      {/* Add fields for repeat, etc. */}
    </div>
  );
};

export default PropertiesPanel;