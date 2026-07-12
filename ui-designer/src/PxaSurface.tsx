import React, { useState, useRef, useCallback, useEffect, memo, useMemo } from 'react';
import { useDroppable } from '@dnd-kit/core';
import { useDesignerStore } from './store';
import ElementRenderer from './ElementRenderer';
import ContextMenu from './ContextMenu';

interface PxaSurfaceProps {
  documentView?: boolean;
}

const PxaSurface: React.FC<PxaSurfaceProps> = memo(({ documentView = false }) => {
  const { rootIds, selectElement, elements, snapToGrid, gridSize, selectedIds, pageSettings, gridColor, gridOpacity, zoom, setZoom, alignmentGuides } = useDesignerStore();
  const { setNodeRef } = useDroppable({ id: 'canvas' });
  const canvasRef = useRef<HTMLElement>(null);

  const [isSelecting, setIsSelecting] = useState(false);
  const [selectionRect, setSelectionRect] = useState({ x: 0, y: 0, width: 0, height: 0 });
  const [startPoint, setStartPoint] = useState({ x: 0, y: 0 });
  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    elementId?: string;
  } | null>(null);

  const handleSurfaceMouseDown = useCallback((e: React.MouseEvent) => {
    // Only start selection if clicking directly on canvas, not on elements
    if (e.target !== e.currentTarget) return;

    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;

    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    setIsSelecting(true);
    setStartPoint({ x, y });
    setSelectionRect({ x, y, width: 0, height: 0 });
  }, []);

  const handleSurfaceMouseMove = useCallback((e: React.MouseEvent) => {
    if (!isSelecting) return;

    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;

    const currentX = e.clientX - rect.left;
    const currentY = e.clientY - rect.top;

    const x = Math.min(startPoint.x, currentX);
    const y = Math.min(startPoint.y, currentY);
    const width = Math.abs(currentX - startPoint.x);
    const height = Math.abs(currentY - startPoint.y);

    setSelectionRect({ x, y, width, height });
  }, [isSelecting, startPoint]);

  const handleSurfaceMouseUp = useCallback(() => {
    if (!isSelecting) return;

    setIsSelecting(false);

    // Find elements within the selection rectangle
    const selectedElementIds: string[] = [];

    Object.values(elements).forEach(element => {
      if (element.x !== undefined && element.y !== undefined &&
          element.width !== undefined && element.height !== undefined) {
        const elementRight = element.x + element.width;
        const elementBottom = element.y + element.height;

        // Check if element overlaps with selection rectangle
        const overlaps = !(element.x > selectionRect.x + selectionRect.width ||
                          elementRight < selectionRect.x ||
                          element.y > selectionRect.y + selectionRect.height ||
                          elementBottom < selectionRect.y);

        if (overlaps) {
          selectedElementIds.push(element.id);
        }
      }
    });

    if (selectedElementIds.length > 0) {
      // Select all elements in the rectangle
      selectedElementIds.forEach(id => selectElement(id, true));
    }

    setSelectionRect({ x: 0, y: 0, width: 0, height: 0 });
  }, [isSelecting, elements, selectionRect, selectElement]);

  const handleSurfaceClick = (e: React.MouseEvent) => {
    // Only clear selection if clicking directly on canvas and not selecting
    if (e.target === e.currentTarget && !isSelecting) {
      selectElement(null);
    }
  };

  const handleContextMenu = (e: React.MouseEvent) => {
    e.preventDefault();

    // Check if we right-clicked on an element
    const target = e.target as HTMLElement;
    const elementDiv = target.closest('.canvas-element') as HTMLElement;
    let elementId: string | undefined;

    if (elementDiv) {
      // Find the element ID from the data attributes or by traversing the React tree
      // For now, we'll use a simple approach - check if the element is selected
      if (selectedIds.length === 1) {
        elementId = selectedIds[0];
      }
    }

    const x = e.clientX;
    const y = e.clientY;

    setContextMenu({ x, y, elementId });
  };

  const closeContextMenu = () => {
    setContextMenu(null);
  };

  // Listen for drag events to show grid snapping feedback
  useEffect(() => {
    const handleDragStart = (_event: any) => {
      setDraggedElement(null);
    };

    const handleDragMove = (event: any) => {
      const { active, delta } = event;
      const draggedElementId = active.id as string;
      const draggedElement = elements[draggedElementId];

      if (draggedElement) {
        const currentX = (draggedElement.x || 0) + delta.x;
        const currentY = (draggedElement.y || 0) + delta.y;

        // Update dragged element for grid snapping feedback
        setDraggedElement({
          id: draggedElementId,
          x: currentX,
          y: currentY,
          width: draggedElement.width || 0,
          height: draggedElement.height || 0,
        });
      }
    };

    const handleDragEnd = () => {
      setDraggedElement(null);
    };

    document.addEventListener('dnd-kit:drag-start', handleDragStart);
    document.addEventListener('dnd-kit:drag-move', handleDragMove);
    document.addEventListener('dnd-kit:drag-end', handleDragEnd);

    return () => {
      document.removeEventListener('dnd-kit:drag-start', handleDragStart);
      document.removeEventListener('dnd-kit:drag-move', handleDragMove);
      document.removeEventListener('dnd-kit:drag-end', handleDragEnd);
    };
  }, [elements]);

  // Handle mouse wheel zoom
  useEffect(() => {
    const handleWheel = (e: WheelEvent) => {
      // Only zoom if Ctrl/Cmd is pressed
      if (!e.ctrlKey && !e.metaKey) return;

      e.preventDefault();

      const zoomFactor = e.deltaY > 0 ? 0.9 : 1.1; // Zoom out or in
      const newZoom = Math.max(0.1, Math.min(5, zoom * zoomFactor));
      setZoom(newZoom);
    };

    const canvasElement = canvasRef.current;
    if (canvasElement) {
      canvasElement.addEventListener('wheel', handleWheel, { passive: false });
      return () => canvasElement.removeEventListener('wheel', handleWheel);
    }
  }, [zoom, setZoom]);

  // Track dragged element for grid snapping feedback
  const [draggedElement, setDraggedElement] = useState<{
    id: string;
    x: number;
    y: number;
    width: number;
    height: number;
  } | null>(null);

  // Memoize grid lines generation with snapping feedback
  const gridLines = useMemo(() => {
    if (!snapToGrid) return [];

    const lines = [];
    const snapThreshold = gridSize * 0.3; // Snap when within 30% of grid size

    // Vertical lines
    for (let x = 0; x < 2000; x += gridSize) {
      let strokeColor = gridColor;
      let strokeWidth = "1";
      let opacity = gridOpacity;

      // Highlight grid lines near dragged element
      if (draggedElement) {
        const elementLeft = draggedElement.x;
        const elementRight = draggedElement.x + draggedElement.width;
        const elementCenter = draggedElement.x + draggedElement.width / 2;

        if (Math.abs(elementLeft - x) < snapThreshold ||
            Math.abs(elementRight - x) < snapThreshold ||
            Math.abs(elementCenter - x) < snapThreshold) {
          strokeColor = "var(--ui-color-accent)";
          strokeWidth = "2";
          opacity = 0.8;
        }
      }

      lines.push(
        <line
          key={`v-${x}`}
          x1={x}
          y1={0}
          x2={x}
          y2={2000}
          stroke={strokeColor}
          strokeWidth={strokeWidth}
          opacity={opacity}
        />
      );
    }

    // Horizontal lines
    for (let y = 0; y < 2000; y += gridSize) {
      let strokeColor = gridColor;
      let strokeWidth = "1";
      let opacity = gridOpacity;

      // Highlight grid lines near dragged element
      if (draggedElement) {
        const elementTop = draggedElement.y;
        const elementBottom = draggedElement.y + draggedElement.height;
        const elementCenter = draggedElement.y + draggedElement.height / 2;

        if (Math.abs(elementTop - y) < snapThreshold ||
            Math.abs(elementBottom - y) < snapThreshold ||
            Math.abs(elementCenter - y) < snapThreshold) {
          strokeColor = "var(--ui-color-accent)";
          strokeWidth = "2";
          opacity = 0.8;
        }
      }

      lines.push(
        <line
          key={`h-${y}`}
          x1={0}
          y1={y}
          x2={2000}
          y2={y}
          stroke={strokeColor}
          strokeWidth={strokeWidth}
          opacity={opacity}
        />
      );
    }
    return lines;
  }, [snapToGrid, gridSize, gridColor, gridOpacity, draggedElement]);

  return (
    <main
      className="canvas"
      ref={(el) => {
        setNodeRef(el);
        (canvasRef as any).current = el;
      }}
      style={{
        backgroundColor: pageSettings.backgroundColor,
        minWidth: pageSettings.width,
        minHeight: pageSettings.height,
        padding: documentView ? '0' : '1rem', // Remove padding in document view
      }}
      onMouseDown={handleSurfaceMouseDown}
      onMouseMove={handleSurfaceMouseMove}
      onMouseUp={handleSurfaceMouseUp}
      onClick={handleSurfaceClick}
      onContextMenu={handleContextMenu}
    >
      <div
        className="canvas-zoom-layer"
        style={{
          transform: `scale(${zoom})`,
          width: `${100 / zoom}%`,
          height: `${100 / zoom}%`,
        }}
      >
        {/* Grid Background - Hidden in document view */}
        {snapToGrid && !documentView && (
          <svg className="canvas-grid-overlay">
            {gridLines}
          </svg>
        )}

        {rootIds.length === 0 && !documentView && (
          <div className="canvas-placeholder canvas-empty-state" role="status" aria-live="polite">
            <h3 className="canvas-empty-state-title">Start building your layout</h3>
            <p className="canvas-empty-state-copy">Drag an element from the left panel into the canvas.</p>
            <p className="canvas-empty-state-copy">Then select it to edit settings in the right Properties panel.</p>
            <p className="canvas-empty-state-hint">Tip: Right-click the canvas for quick actions.</p>
          </div>
        )}
        {rootIds.map((id) => (
          <ElementRenderer key={id} elementId={id} documentView={documentView} />
        ))}

        {/* Document End Marker - Always visible */}
        <div className="canvas-end-marker" title="Document End" />
        <div className="canvas-end-label">
          END
        </div>

        {/* Alignment Guides - Hidden in document view */}
        {!documentView && (alignmentGuides.vertical.length > 0 || alignmentGuides.horizontal.length > 0) ? (
          <svg className="canvas-guides-overlay">
            {/* Vertical guides */}
            {alignmentGuides.vertical.map((guide, index) => (
              <g key={`v-guide-${index}`}>
                <line
                  x1={guide.x}
                  y1={0}
                  x2={guide.x}
                  y2={2000}
                  stroke="var(--ui-color-danger)"
                  strokeWidth="1"
                  strokeDasharray="2,2"
                />
                {guide.label && (
                  <text
                    x={guide.x + 4}
                    y={20}
                    fill="var(--ui-color-danger)"
                    fontSize="12"
                    fontWeight="bold"
                  >
                    {guide.label}
                  </text>
                )}
              </g>
            ))}
            {/* Horizontal guides */}
            {alignmentGuides.horizontal.map((guide, index) => (
              <g key={`h-guide-${index}`}>
                <line
                  x1={0}
                  y1={guide.y}
                  x2={2000}
                  y2={guide.y}
                  stroke="var(--ui-color-danger)"
                  strokeWidth="1"
                  strokeDasharray="2,2"
                />
                {guide.label && (
                  <text
                    x={10}
                    y={guide.y - 4}
                    fill="var(--ui-color-danger)"
                    fontSize="12"
                    fontWeight="bold"
                  >
                    {guide.label}
                  </text>
                )}
              </g>
            ))}
            {/* Distance measurements */}
            {alignmentGuides.distances.map((distance, index) => (
              <text
                key={`distance-${index}`}
                x={distance.x}
                y={distance.y}
                className="canvas-distance-label"
                fill="var(--ui-color-accent)"
                fontSize="11"
                fontWeight="bold"
                textAnchor="middle"
                dominantBaseline="middle"
              >
                {distance.text}
              </text>
            ))}
          </svg>
        ) : null}

        {/* Selection Rectangle - Hidden in document view */}
        {!documentView && isSelecting && selectionRect.width > 0 && selectionRect.height > 0 && (
          <div
            className="canvas-selection-rect"
            style={{
              left: selectionRect.x,
              top: selectionRect.y,
              width: selectionRect.width,
              height: selectionRect.height,
            }}
          />
        )}

        {/* Context Menu - Hidden in document view */}
        {!documentView && contextMenu && (
          <ContextMenu
            x={contextMenu.x}
            y={contextMenu.y}
            elementId={contextMenu.elementId}
            onClose={closeContextMenu}
          />
        )}
      </div>
    </main>
  );
});

PxaSurface.displayName = 'PxaSurface';

export default PxaSurface;
