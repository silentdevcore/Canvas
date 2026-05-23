import React from 'react';
import { DndContext, useDraggable } from '@dnd-kit/core';
import { useDesignerStore } from './store';
import Tooltip from './Tooltip';

const ELEMENTS = [
  { type: 'Text', label: 'Text' },
  { type: 'Column', label: 'Column' },
  { type: 'Table', label: 'Table' },
  { type: 'Image', label: 'Image' },
  { type: 'Rectangle', label: 'Rectangle' },
  { type: 'Circle', label: 'Circle' },
  { type: 'Line', label: 'Line' },
  { type: 'Link', label: 'Link' },
  { type: 'List', label: 'List' },
  { type: 'PageBreak', label: 'Page Break' },
  { type: 'Grid', label: 'Grid' },
  { type: 'Spacer', label: 'Spacer' },
  { type: 'Button', label: 'Button' },
  { type: 'Checkbox', label: 'Checkbox' },
  { type: 'Radio', label: 'Radio' },
  { type: 'QRCode', label: 'QR Code' },
  { type: 'Barcode', label: 'Barcode' },
  { type: 'Signature', label: 'Signature' },
  { type: 'RichText', label: 'Rich Text' },
];

function DraggableElement({ type, label }: { type: string; label: string }) {
  const { attributes, listeners, setNodeRef } = useDraggable({ id: type });
  const showTooltips = useDesignerStore((state) => state.showTooltips);

  const getTooltipContent = (type: string) => {
    switch (type) {
      case 'Text':
        return 'Drag to add a text element to the canvas';
      case 'Column':
        return 'Drag to add a column container to the canvas';
      case 'Table':
        return 'Drag to add a table element to the canvas';
      case 'Image':
        return 'Drag to add an image element to the canvas';
      default:
        return `Drag to add a ${type.toLowerCase()} element to the canvas`;
    }
  };

  return (
    <Tooltip content={getTooltipContent(type)} disabled={!showTooltips} position="right">
      <div ref={setNodeRef} {...listeners} {...attributes} className="draggable-element">
        {label}
      </div>
    </Tooltip>
  );
}

const Sidebar: React.FC = () => {
  // In the future, we may use the store for custom elements
  return (
    <aside className="sidebar">
      <h2>Elements</h2>
      {ELEMENTS.map((el) => (
        <DraggableElement key={el.type} type={el.type} label={el.label} />
      ))}
    </aside>
  );
};

export default Sidebar;
