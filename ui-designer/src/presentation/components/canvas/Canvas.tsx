import React, { useCallback } from 'react';
import { useCanvasViewModel } from '../../viewModels/CanvasViewModel';
import ElementRenderer from '../../../ElementRenderer';
import FocusIndicator from '../../../FocusIndicator';

/**
 * Canvas Component following Clean Architecture principles.
 * This component focuses purely on UI rendering and user interaction.
 * All business logic is handled by the CanvasViewModel and use cases.
 */
export const Canvas: React.FC = () => {
  const {
    elements,
    selectedElementId,
    selectedElement,
    isLoading,
    error,
    addElement,
    selectElement,
    clearError
  } = useCanvasViewModel();

  // Handle canvas click (deselect elements)
  const handleCanvasClick = useCallback((event: React.MouseEvent) => {
    // Only deselect if clicking on canvas background, not on elements
    if (event.target === event.currentTarget) {
      selectElement(null);
    }
  }, [selectElement]);

  // Handle element selection
  const handleElementClick = useCallback((elementId: string, event: React.MouseEvent) => {
    event.stopPropagation();
    selectElement(elementId);
  }, [selectElement]);

  // Handle adding elements via drop or other interactions
  const handleAddElement = useCallback(async (type: string, props: any, x: number, y: number) => {
    await addElement(type, props, x, y);
  }, [addElement]);

  return (
    <div className="canvas-container">
      {/* Loading indicator */}
      {isLoading && (
        <div className="canvas-loading">
          <div className="loading-spinner">Loading...</div>
        </div>
      )}

      {/* Error display */}
      {error && (
        <div className="canvas-error">
          <div className="error-message">
            {error}
            <button onClick={clearError} className="error-close">×</button>
          </div>
        </div>
      )}

      {/* Main canvas area */}
      <div
        className="canvas canvas-presentation"
        onClick={handleCanvasClick}
      >
        {/* Render all elements */}
        {elements.map((element) => (
          <div
            key={element.id}
            className="canvas-presentation-element"
            style={{
              left: element.x || 0,
              top: element.y || 0,
              width: element.width || 'auto',
              height: element.height || 'auto',
            }}
            onClick={(e) => handleElementClick(element.id, e)}
          >
            <ElementRenderer elementId={element.id} />
          </div>
        ))}

        {/* Focus indicator for selected element */}
        {selectedElementId && (
          <FocusIndicator>
            <ElementRenderer elementId={selectedElementId} />
          </FocusIndicator>
        )}

        {/* Drop zone for adding elements */}
        <div
          className="canvas-drop-zone canvas-drop-zone-overlay"
          onDrop={(e) => {
            e.preventDefault();
            // Handle element drops from sidebar
            const elementType = e.dataTransfer.getData('elementType');
            const rect = e.currentTarget.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;

            if (elementType) {
              handleAddElement(elementType, {}, x, y);
            }
          }}
          onDragOver={(e) => {
            e.preventDefault(); // Allow drop
          }}
        />
      </div>

      {/* Canvas controls */}
      <div className="canvas-controls">
        <div className="element-count">
          {elements.length} element{elements.length !== 1 ? 's' : ''}
        </div>
        {selectedElementId && (
          <div className="selected-info">
            Selected: {selectedElement?.type} ({selectedElementId})
          </div>
        )}
      </div>
    </div>
  );
};