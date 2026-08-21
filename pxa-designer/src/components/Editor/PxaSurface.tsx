import React, { useRef, useState } from 'react';
import { motion } from 'framer-motion';
import { useEditorStore, SimpleElement } from '../../store';
import { DragDropContext, Droppable, Draggable } from 'react-beautiful-dnd';
import { Resizable } from 'react-resizable';
import { sanitizeRichTextHtml } from '@/utils/sanitizeRichTextHtml';
import ChartRenderer from '@/chart/ChartRenderer';

const PAGE_WIDTH = 595;
const PAGE_HEIGHT = 842;

const PxaSurface: React.FC<{ elements: SimpleElement[] }> = ({ elements }) => {
  const { updateElement, selectedElementId, setSelectedElementId } = useEditorStore();
  const canvasRef = useRef<HTMLDivElement | null>(null);
  const [resizingId, setResizingId] = useState<string | null>(null);

  const onDragEnd = (result: any) => {
    if (!result.destination) return;
  };

  const handleResize = (id: string, newSize: { width: number; height: number }) => {
    updateElement(id, newSize);
  };

  const renderElement = (element: SimpleElement) => {
    switch (element.type) {
      case 'text':
        return <div style={element.style}>{element.content}</div>;
      case 'rect':
        return <div style={{ ...element.style, backgroundColor: element.style?.backgroundColor || 'gray' }}>Rect</div>;
      case 'circle':
        return <div style={{ ...element.style, borderRadius: '50%', backgroundColor: element.style?.backgroundColor || 'gray' }}>Circle</div>;
      case 'chart':
        return <ChartRenderer chart={element.chart} legacyType={element.chartType} legacyData={element.chartData} />;
      case 'subsection':
        return <div style={element.style}>Subsection</div>;
      case 'area':
        return <div style={element.style}>Area</div>;
      case 'button':
        return <button style={element.style}>{element.content || 'Button'}</button>;
      case 'dropdown':
        return <select style={element.style}>{element.options?.map(opt => <option key={opt}>{opt}</option>)}</select>;
      case 'optionlist':
        return <ul style={element.style}>{element.options?.map(opt => <li key={opt}>{opt}</li>)}</ul>;
      case 'radio':
        return <div>{element.options?.map(opt => <label key={opt}><input type="radio" name={element.id} /> {opt}</label>)}</div>;
      // Existing types
      case 'image':
        return <img src={element.content} alt="Image" style={element.style} />;
      case 'shape':
        return <div style={element.style}>Shape</div>;
      case 'table':
        return <table style={element.style}><tbody><tr><td>Table</td></tr></tbody></table>;
      case 'line':
        return <hr style={element.style} />;
      case 'qrcode':
        return <div>QR Code</div>;
      case 'barcode':
        return <div>Barcode</div>;
      case 'signature':
        return <div>Signature</div>;
      case 'richtext':
        return <div dangerouslySetInnerHTML={{ __html: sanitizeRichTextHtml(element.htmlContent || '') }} />;
      case 'field':
        return <input type="text" placeholder={element.fieldLabel} style={element.style} />;
      case 'checkbox':
        return <input type="checkbox" style={element.style} />;
      default:
        return <div>{element.type}</div>;
    }
  };

  return (
    <DragDropContext onDragEnd={onDragEnd}>
      <Droppable droppableId="canvas">
        {(provided) => (
          <div ref={(node) => {
            provided.innerRef(node);
            canvasRef.current = node;
          }} {...provided.droppableProps} className="canvas" style={{ width: PAGE_WIDTH, height: PAGE_HEIGHT }}>
            {elements.map((element, index) => (
              <Draggable key={element.id} draggableId={element.id} index={index}>
                {(provided) => (
                    <motion.div
                      ref={provided.innerRef}
                      {...provided.draggableProps}
                      className={`${selectedElementId === element.id ? 'is-selected' : ''} ${resizingId === element.id ? 'is-resizing' : ''}`}
                      style={{ position: 'absolute', left: element.x, top: element.y }}
                      onClick={() => setSelectedElementId(element.id)}
                    >
                      <div {...provided.dragHandleProps}>Drag</div>
                      <Resizable
                        width={element.width}
                        height={element.height}
                        onResize={(_, { size }) => handleResize(element.id, size)}
                        onResizeStart={() => setResizingId(element.id)}
                        onResizeStop={() => setResizingId(null)}
                      >
                        {renderElement(element)}
                      </Resizable>
                    </motion.div>
                )}
              </Draggable>
            ))}
            {provided.placeholder}
          </div>
        )}
      </Droppable>
    </DragDropContext>
  );
};

export default PxaSurface;
