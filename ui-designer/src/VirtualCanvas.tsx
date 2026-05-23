import React, { useState, useRef, useCallback, useEffect, memo, useMemo, useDeferredValue } from 'react';
import { useDroppable } from '@dnd-kit/core';
import { useDesignerStore } from './store';
import ElementRenderer from './ElementRenderer';
import ContextMenu from './ContextMenu';

interface VirtualCanvasProps {
  width?: number;
  height?: number;
  viewportWidth?: number;
  viewportHeight?: number;
}

const VirtualCanvas: React.FC<VirtualCanvasProps> = memo(({
  width = 4000,
  height = 4000,
  viewportWidth = 800,
  viewportHeight = 600
}) => {
  const { rootIds, selectElement, elements, snapToGrid, gridSize, selectedIds, pageSettings, gridColor, gridOpacity } = useDesignerStore();
  const { setNodeRef } = useDroppable({ id: 'canvas' });
  const canvasRef = useRef<HTMLDivElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const [scrollLeft, setScrollLeft] = useState(0);
  const [scrollTop, setScrollTop] = useState(0);
  const [isSelecting, setIsSelecting] = useState(false);
  const [selectionRect, setSelectionRect] = useState({ x: 0, y: 0, width: 0, height: 0 });
  const [startPoint, setStartPoint] = useState({ x: 0, y: 0 });
  const [alignmentGuides, setAlignmentGuides] = useState<{
    vertical: number[];
    horizontal: number[];
  }>({ vertical: [], horizontal: [] });
  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    elementId?: string;
  } | null>(null);

  // Calculate visible area bounds
  const visibleBounds = useMemo(() => ({
    left: scrollLeft,
    top: scrollTop,
    right: scrollLeft + viewportWidth,
    bottom: scrollTop + viewportHeight,
  }), [scrollLeft, scrollTop, viewportWidth, viewportHeight]);

  // Performance optimization: Use deferred value for heavy calculations
  const deferredVisibleBounds = useDeferredValue(visibleBounds);

  // Filter elements that are visible in the current viewport with enhanced culling
  const visibleElements = useMemo(() => {
    const startTime = performance.now();

    const visible = rootIds.filter(id => {
      const element = elements[id];
      if (!element || element.locked) return false;

      const elementLeft = element.x || 0;
      const elementTop = element.y || 0;
      const elementRight = elementLeft + (element.width || 100);
      const elementBottom = elementTop + (element.height || 50);

      // Enhanced culling: More aggressive padding and better bounds checking
      const padding = Math.max(150, viewportWidth * 0.2); // Adaptive padding

      // Use early returns for better performance
      if (elementRight < deferredVisibleBounds.left - padding) return false;
      if (elementLeft > deferredVisibleBounds.right + padding) return false;
      if (elementBottom < deferredVisibleBounds.top - padding) return false;
      if (elementTop > deferredVisibleBounds.bottom + padding) return false;

      return true;
    });

    const endTime = performance.now();
    const duration = endTime - startTime;

    // Performance monitoring: Log if culling takes too long
    if (duration > 16.67) { // More than one frame at 60fps
      console.warn(`VirtualCanvas culling took ${duration.toFixed(2)}ms for ${rootIds.length} elements, ${visible.length} visible`);
    }

    return visible;
  }, [rootIds, elements, deferredVisibleBounds, viewportWidth]);

  const handleScroll = useCallback((e: React.UIEvent<HTMLDivElement>) => {
    const target = e.target as HTMLDivElement;
    setScrollLeft(target.scrollLeft);
    setScrollTop(target.scrollTop);
  }, []);

  const handleCanvasMouseDown = useCallback((e: React.MouseEvent) => {
    // Only start selection if clicking directly on canvas, not on elements
    if (e.target !== e.currentTarget) return;

    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;

    const x = e.clientX - rect.left + scrollLeft;
    const y = e.clientY - rect.top + scrollTop;

    setIsSelecting(true);
    setStartPoint({ x, y });
    setSelectionRect({ x, y, width: 0, height: 0 });
  }, [scrollLeft, scrollTop]);

  const handleCanvasMouseMove = useCallback((e: React.MouseEvent) => {
    if (!isSelecting) return;

    const rect = canvasRef.current?.getBoundingClientRect();
    if (!rect) return;

    const currentX = e.clientX - rect.left + scrollLeft;
    const currentY = e.clientY - rect.top + scrollTop;

    const x = Math.min(startPoint.x, currentX);
    const y = Math.min(startPoint.y, currentY);
    const width = Math.abs(currentX - startPoint.x);
    const height = Math.abs(currentY - startPoint.y);

    setSelectionRect({ x, y, width, height });
  }, [isSelecting, startPoint, scrollLeft, scrollTop]);

  const handleCanvasMouseUp = useCallback(() => {
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

  const handleCanvasClick = (e: React.MouseEvent) => {
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

  // Calculate alignment guides for the dragged element
  const calculateAlignmentGuides = useCallback((draggedElementId: string, dragX: number, dragY: number) => {
    const draggedElement = elements[draggedElementId];
    if (!draggedElement) return { vertical: [], horizontal: [] };

    const threshold = 5; // pixels
    const verticalGuides: number[] = [];
    const horizontalGuides: number[] = [];

    // Calculate dragged element bounds
    const draggedLeft = dragX;
    const draggedRight = dragX + (draggedElement.width || 0);
    const draggedTop = dragY;
    const draggedBottom = dragY + (draggedElement.height || 0);
    const draggedCenterX = dragX + (draggedElement.width || 0) / 2;
    const draggedCenterY = dragY + (draggedElement.height || 0) / 2;

    Object.values(elements).forEach(element => {
      if (element.id === draggedElementId || element.isGroup) return;

      const elementLeft = element.x || 0;
      const elementRight = elementLeft + (element.width || 0);
      const elementTop = element.y || 0;
      const elementBottom = elementTop + (element.height || 0);
      const elementCenterX = elementLeft + (element.width || 0) / 2;
      const elementCenterY = elementTop + (element.height || 0) / 2;

      // Check vertical alignments
      if (Math.abs(draggedLeft - elementLeft) < threshold) {
        verticalGuides.push(elementLeft);
      }
      if (Math.abs(draggedRight - elementRight) < threshold) {
        verticalGuides.push(elementRight);
      }
      if (Math.abs(draggedCenterX - elementCenterX) < threshold) {
        verticalGuides.push(elementCenterX);
      }

      // Check horizontal alignments
      if (Math.abs(draggedTop - elementTop) < threshold) {
        horizontalGuides.push(elementTop);
      }
      if (Math.abs(draggedBottom - elementBottom) < threshold) {
        horizontalGuides.push(elementBottom);
      }
      if (Math.abs(draggedCenterY - elementCenterY) < threshold) {
        horizontalGuides.push(elementCenterY);
      }
    });

    return {
      vertical: [...new Set(verticalGuides)], // Remove duplicates
      horizontal: [...new Set(horizontalGuides)]
    };
  }, [elements]);

  // Listen for drag events to show alignment guides
  useEffect(() => {
    const handleDragStart = (_event: any) => {
      // Clear guides when drag starts
      setAlignmentGuides({ vertical: [], horizontal: [] });
    };

    const handleDragMove = (event: any) => {
      const { active, delta } = event;
      const draggedElementId = active.id as string;
      const draggedElement = elements[draggedElementId];

      if (draggedElement) {
        const currentX = (draggedElement.x || 0) + delta.x;
        const currentY = (draggedElement.y || 0) + delta.y;
        const guides = calculateAlignmentGuides(draggedElementId, currentX, currentY);
        setAlignmentGuides(guides);
      }
    };

    const handleDragEnd = () => {
      // Clear guides when drag ends
      setAlignmentGuides({ vertical: [], horizontal: [] });
    };

    document.addEventListener('dnd-kit:drag-start', handleDragStart);
    document.addEventListener('dnd-kit:drag-move', handleDragMove);
    document.addEventListener('dnd-kit:drag-end', handleDragEnd);

    return () => {
      document.removeEventListener('dnd-kit:drag-start', handleDragStart);
      document.removeEventListener('dnd-kit:drag-move', handleDragMove);
      document.removeEventListener('dnd-kit:drag-end', handleDragEnd);
    };
  }, [elements, calculateAlignmentGuides]);

  // Memoize grid lines generation for visible area only
  const gridLines = useMemo(() => {
    if (!snapToGrid) return [];

    const lines = [];
    const startX = Math.floor(scrollLeft / gridSize) * gridSize;
    const endX = Math.ceil((scrollLeft + viewportWidth) / gridSize) * gridSize;
    const startY = Math.floor(scrollTop / gridSize) * gridSize;
    const endY = Math.ceil((scrollTop + viewportHeight) / gridSize) * gridSize;

    // Vertical lines
    for (let x = startX; x <= endX; x += gridSize) {
      lines.push(
        <line
          key={`v-${x}`}
          x1={x}
          y1={startY}
          x2={x}
          y2={endY}
          stroke={gridColor}
          strokeWidth="1"
          opacity={gridOpacity}
        />
      );
    }
    // Horizontal lines
    for (let y = startY; y <= endY; y += gridSize) {
      lines.push(
        <line
          key={`h-${y}`}
          x1={startX}
          y1={y}
          x2={endX}
          y2={y}
          stroke={gridColor}
          strokeWidth="1"
          opacity={gridOpacity}
        />
      );
    }
    return lines;
  }, [snapToGrid, gridSize, gridColor, gridOpacity, scrollLeft, scrollTop, viewportWidth, viewportHeight]);

  return (
    <div
      ref={containerRef}
      className="virtual-canvas-container"
      style={{
        width: viewportWidth,
        height: viewportHeight,
      }}
      onScroll={handleScroll}
    >
      <div
        ref={(el) => {
          setNodeRef(el);
          (canvasRef as any).current = el;
        }}
        className="virtual-canvas-surface"
        style={{
          width: width,
          height: height,
          backgroundColor: pageSettings.backgroundColor,
        }}
        onMouseDown={handleCanvasMouseDown}
        onMouseMove={handleCanvasMouseMove}
        onMouseUp={handleCanvasMouseUp}
        onClick={handleCanvasClick}
        onContextMenu={handleContextMenu}
      >
        {/* Grid Background */}
        {snapToGrid && (
          <svg className="canvas-grid-overlay">
            {gridLines}
          </svg>
        )}

        {/* Render only visible elements */}
        {visibleElements.length === 0 && rootIds.length === 0 && (
          <div className="virtual-canvas-empty-state">
            Drop elements here
          </div>
        )}

        {visibleElements.map((id) => (
          <ElementRenderer key={id} elementId={id} />
        ))}

        {/* Alignment Guides */}
        {alignmentGuides.vertical.length > 0 || alignmentGuides.horizontal.length > 0 ? (
          <svg className="canvas-guides-overlay">
            {/* Vertical guides */}
            {alignmentGuides.vertical.map((x, index) => (
              <line
                key={`v-guide-${index}`}
                x1={x}
                y1={0}
                x2={x}
                y2={height}
                stroke="var(--ui-color-danger)"
                strokeWidth="1"
                strokeDasharray="2,2"
              />
            ))}
            {/* Horizontal guides */}
            {alignmentGuides.horizontal.map((y, index) => (
              <line
                key={`h-guide-${index}`}
                x1={0}
                y1={y}
                x2={width}
                y2={y}
                stroke="var(--ui-color-danger)"
                strokeWidth="1"
                strokeDasharray="2,2"
              />
            ))}
          </svg>
        ) : null}

        {/* Selection Rectangle */}
        {isSelecting && selectionRect.width > 0 && selectionRect.height > 0 && (
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

        {/* Context Menu */}
        {contextMenu && (
          <ContextMenu
            x={contextMenu.x}
            y={contextMenu.y}
            elementId={contextMenu.elementId}
            onClose={closeContextMenu}
          />
        )}
      </div>
    </div>
  );
});

VirtualCanvas.displayName = 'VirtualCanvas';

export default VirtualCanvas;