import React from 'react';
import { useEditorStore, ElementType } from '../../store';
import { v4 as uuidv4 } from 'uuid'; // Add uuid to dependencies if not present

const elementTypes: { type: ElementType; label: string }[] = [
  { type: 'text', label: 'Text' },
  { type: 'image', label: 'Image' },
  { type: 'shape', label: 'Shape' },
  { type: 'table', label: 'Table' },
  { type: 'line', label: 'Line' },
  { type: 'qrcode', label: 'QR Code' },
  { type: 'barcode', label: 'Barcode' },
  { type: 'signature', label: 'Signature' },
  { type: 'richtext', label: 'Rich Text' },
  { type: 'field', label: 'Field' },
  { type: 'checkbox', label: 'Checkbox' },
  { type: 'rect', label: 'Rectangle' },
  { type: 'circle', label: 'Circle' },
  { type: 'chart', label: 'Chart' },
  { type: 'subsection', label: 'Subsection' },
  { type: 'area', label: 'Area' },
  { type: 'button', label: 'Button' },
  { type: 'dropdown', label: 'Dropdown' },
  { type: 'optionlist', label: 'Option List' },
  { type: 'radio', label: 'Radio' },
];

const Toolbar: React.FC = () => {
  const addElement = useEditorStore((state) => state.addElement);
  const [expandedGroups, setExpandedGroups] = React.useState<string[]>(['Text Elements', 'Form Elements', 'Visual Elements', 'Shapes & Layout']);

  const toggleGroup = (group: string) => {
    setExpandedGroups(prev => prev.includes(group) ? prev.filter(g => g !== group) : [...prev, group]);
  };

  const groups = {
    'Text Elements': ['text', 'richtext'],
    'Form Elements': ['field', 'checkbox', 'button', 'dropdown', 'optionlist', 'radio', 'signature'],
    'Visual Elements': ['image', 'qrcode', 'barcode', 'chart'],
    'Shapes & Layout': ['shape', 'rect', 'circle', 'line', 'table', 'subsection', 'area'],
  };

  const handleAddElement = (type: ElementType) => {
    const newElement = {
      id: uuidv4(),
      type,
      x: 0,
      y: 0,
      width: 100,
      height: 50,
      content: type === 'text' ? 'New Text' : undefined,
      style: {},
      chartType: type === 'chart' ? 'bar' as 'bar' : undefined,
      chartData: type === 'chart' ? {} : undefined,
      options: ['dropdown', 'optionlist', 'radio'].includes(type) ? [] : undefined,
    };
    addElement(newElement);
  };

  return (
    <div className="toolbar bg-gradient-to-b from-gray-50 to-gray-100 p-4 shadow-lg rounded-lg overflow-y-auto max-h-screen border border-gray-200">
      {Object.entries(groups).map(([group, types]) => (
        <div key={group} className="mb-3 bg-white rounded-md shadow-sm overflow-hidden">
          <button 
            onClick={() => toggleGroup(group)} 
            className="w-full text-left font-semibold py-3 px-4 bg-gray-100 hover:bg-gray-200 transition-colors flex justify-between items-center border-b border-gray-200"
          >
            {group} ({types.length})
            <span className={`transition-transform ${expandedGroups.includes(group) ? 'rotate-180' : ''}`}>▼</span>
          </button>
          {expandedGroups.includes(group) && (
            <div className="flex flex-wrap gap-2 p-4 bg-gray-50">
              {types.map(type => {
                const label = elementTypes.find(el => el.type === type)?.label || type;
                return (
                  <button
                    key={type}
                    onClick={() => handleAddElement(type as ElementType)}
                    className="px-4 py-2 bg-white border border-gray-300 rounded-lg hover:bg-blue-50 hover:border-blue-400 text-sm font-medium transition-all shadow-sm hover:shadow"
                  >
                    {label}
                  </button>
                );
              })}
            </div>
          )}
        </div>
      ))}
    </div>
  );
};

export default Toolbar;