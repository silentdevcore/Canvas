import { useEffect, useCallback } from 'react';
import { useDesignerStore } from './store';

interface KeyboardNavigationOptions {
  enabled?: boolean;
  arrowKeys?: boolean;
  tabNavigation?: boolean;
  homeEndKeys?: boolean;
  pageUpDownKeys?: boolean;
}

export const useKeyboardNavigation = (options: KeyboardNavigationOptions = {}) => {
  const {
    selectedIds,
    elements,
    rootIds,
    updateElementPosition,
    selectElement,
    deleteElement,
    zoomIn,
    zoomOut,
    resetZoom,
    addToast
  } = useDesignerStore();

  const {
    enabled = true,
    arrowKeys = true,
    tabNavigation = true,
    homeEndKeys = true,
    pageUpDownKeys = true,
  } = options;

  const getNavigableElements = useCallback(() => {
    return rootIds.filter(id => elements[id] && !elements[id].locked);
  }, [rootIds, elements]);

  const getCurrentElementIndex = useCallback(() => {
    if (selectedIds.length === 0) return -1;
    const navigableElements = getNavigableElements();
    return navigableElements.indexOf(selectedIds[0]);
  }, [selectedIds, getNavigableElements]);

  const selectElementByIndex = useCallback((index: number) => {
    const navigableElements = getNavigableElements();
    if (index >= 0 && index < navigableElements.length) {
      selectElement(navigableElements[index]);
    }
  }, [getNavigableElements, selectElement]);

  const moveSelectedElement = useCallback((deltaX: number, deltaY: number) => {
    if (selectedIds.length === 0) return;

    const elementId = selectedIds[0];
    const element = elements[elementId];
    if (!element) return;

    const currentX = element.x || 0;
    const currentY = element.y || 0;

    // Use grid snapping if enabled
    const gridSize = useDesignerStore.getState().gridSize;
    const snapToGrid = useDesignerStore.getState().snapToGrid;

    let newX = currentX + deltaX;
    let newY = currentY + deltaY;

    if (snapToGrid) {
      newX = Math.round(newX / gridSize) * gridSize;
      newY = Math.round(newY / gridSize) * gridSize;
    }

    updateElementPosition(elementId, newX, newY);
  }, [selectedIds, elements, updateElementPosition]);

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    if (!enabled) return;

    // Don't trigger navigation when typing in input fields
    if (e.target instanceof HTMLInputElement ||
        e.target instanceof HTMLTextAreaElement ||
        e.target instanceof HTMLSelectElement) {
      return;
    }

    const { ctrlKey, metaKey, shiftKey, altKey } = e;
    const isModifierPressed = ctrlKey || metaKey || shiftKey || altKey;

    // Arrow key navigation
    if (arrowKeys && !isModifierPressed) {
      const navigableElements = getNavigableElements();
      const currentIndex = getCurrentElementIndex();

      switch (e.key) {
        case 'ArrowUp':
          e.preventDefault();
          if (currentIndex > 0) {
            selectElementByIndex(currentIndex - 1);
          }
          break;

        case 'ArrowDown':
          e.preventDefault();
          if (currentIndex < navigableElements.length - 1) {
            selectElementByIndex(currentIndex + 1);
          }
          break;

        case 'ArrowLeft':
          e.preventDefault();
          moveSelectedElement(-10, 0); // Move left by 10px
          break;

        case 'ArrowRight':
          e.preventDefault();
          moveSelectedElement(10, 0); // Move right by 10px
          break;
      }
    }

    // Tab navigation
    if (tabNavigation && e.key === 'Tab' && !isModifierPressed) {
      e.preventDefault();
      const navigableElements = getNavigableElements();
      const currentIndex = getCurrentElementIndex();

      if (e.shiftKey) {
        // Shift+Tab: previous element
        if (currentIndex > 0) {
          selectElementByIndex(currentIndex - 1);
        } else if (navigableElements.length > 0) {
          selectElementByIndex(navigableElements.length - 1);
        }
      } else {
        // Tab: next element
        if (currentIndex < navigableElements.length - 1) {
          selectElementByIndex(currentIndex + 1);
        } else if (navigableElements.length > 0) {
          selectElementByIndex(0);
        }
      }
    }

    // Home/End navigation
    if (homeEndKeys && !isModifierPressed) {
      const navigableElements = getNavigableElements();

      switch (e.key) {
        case 'Home':
          e.preventDefault();
          if (navigableElements.length > 0) {
            selectElementByIndex(0);
          }
          break;

        case 'End':
          e.preventDefault();
          if (navigableElements.length > 0) {
            selectElementByIndex(navigableElements.length - 1);
          }
          break;
      }
    }

    // Page Up/Down for zoom
    if (pageUpDownKeys && !isModifierPressed) {
      switch (e.key) {
        case 'PageUp':
          e.preventDefault();
          zoomIn();
          break;

        case 'PageDown':
          e.preventDefault();
          zoomOut();
          break;
      }
    }

    // Ctrl+Home/End for zoom reset
    if ((ctrlKey || metaKey) && !shiftKey) {
      switch (e.key) {
        case 'Home':
          e.preventDefault();
          resetZoom();
          addToast('Zoom reset to 100%', 'info', 2000);
          break;
      }
    }

    // Delete/Backspace for deletion
    if (e.key === 'Delete' || e.key === 'Backspace') {
      if (selectedIds.length > 0 && !isModifierPressed) {
        e.preventDefault();
        const count = selectedIds.length;
        selectedIds.forEach(id => deleteElement(id));
        addToast(`Deleted ${count} element${count > 1 ? 's' : ''}`, 'warning', 2000);
      }
    }

    // Escape to deselect
    if (e.key === 'Escape' && !isModifierPressed) {
      e.preventDefault();
      selectElement(null);
      addToast('Selection cleared', 'info', 2000);
    }

  }, [
    enabled, arrowKeys, tabNavigation, homeEndKeys, pageUpDownKeys,
    getNavigableElements, getCurrentElementIndex, selectElementByIndex,
    moveSelectedElement, selectElement, zoomIn, zoomOut, resetZoom,
    selectedIds, deleteElement, addToast
  ]);

  useEffect(() => {
    if (enabled) {
      document.addEventListener('keydown', handleKeyDown);
      return () => document.removeEventListener('keydown', handleKeyDown);
    }
  }, [enabled, handleKeyDown]);

  return {
    getNavigableElements,
    getCurrentElementIndex,
    selectElementByIndex,
    moveSelectedElement,
  };
};