import React, { useEffect } from 'react';
import { useDesignerStore } from './store';

interface KeyboardShortcutsProps {
  showHelp?: boolean;
  onClose?: () => void;
}

const KeyboardShortcuts: React.FC<KeyboardShortcutsProps> = ({ showHelp = false, onClose }) => {
  const {
    undo,
    redo,
    copyElements,
    pasteElements,
    selectAll,
    deleteElement,
    selectedIds,
    canUndo,
    canRedo,
    canPaste,
    toggleVirtualScrolling,
    zoomIn,
    zoomOut,
    resetZoom,
    addToast
  } = useDesignerStore();

  useEffect(() => {
    if (!showHelp) {
      return;
    }

    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose?.();
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [showHelp, onClose]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Don't trigger shortcuts when typing in input fields
      if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) {
        return;
      }

      const isCtrlOrCmd = e.ctrlKey || e.metaKey;
      const isShift = e.shiftKey;

      // Undo: Ctrl+Z / Cmd+Z
      if (isCtrlOrCmd && !isShift && e.key === 'z') {
        e.preventDefault();
        if (canUndo) {
          undo();
          addToast('Undid last action', 'info', 2000);
        }
        return;
      }

      // Redo: Ctrl+Y / Cmd+Y or Ctrl+Shift+Z / Cmd+Shift+Z
      if (isCtrlOrCmd && ((e.key === 'y') || (isShift && e.key === 'Z'))) {
        e.preventDefault();
        if (canRedo) {
          redo();
          addToast('Redid last action', 'info', 2000);
        }
        return;
      }

      // Copy: Ctrl+C / Cmd+C
      if (isCtrlOrCmd && e.key === 'c') {
        e.preventDefault();
        if (selectedIds.length > 0) {
          copyElements();
          addToast(`Copied ${selectedIds.length} element${selectedIds.length > 1 ? 's' : ''}`, 'success', 2000);
        }
        return;
      }

      // Paste: Ctrl+V / Cmd+V
      if (isCtrlOrCmd && e.key === 'v') {
        e.preventDefault();
        if (canPaste) {
          pasteElements();
          addToast('Pasted elements', 'success', 2000);
        }
        return;
      }

      // Select All: Ctrl+A / Cmd+A
      if (isCtrlOrCmd && e.key === 'a') {
        e.preventDefault();
        selectAll();
        addToast('Selected all elements', 'info', 2000);
        return;
      }

      // Delete: Delete or Backspace
      if (e.key === 'Delete' || e.key === 'Backspace') {
        e.preventDefault();
        if (selectedIds.length > 0) {
          selectedIds.forEach(id => deleteElement(id));
          addToast(`Deleted ${selectedIds.length} element${selectedIds.length > 1 ? 's' : ''}`, 'warning', 2000);
        }
        return;
      }

      // Zoom shortcuts
      if (isCtrlOrCmd) {
        // Zoom In: Ctrl+= / Cmd+=
        if (e.key === '=') {
          e.preventDefault();
          zoomIn();
          return;
        }

        // Zoom Out: Ctrl+- / Cmd+-
        if (e.key === '-') {
          e.preventDefault();
          zoomOut();
          return;
        }

        // Reset Zoom: Ctrl+0 / Cmd+0
        if (e.key === '0') {
          e.preventDefault();
          resetZoom();
          addToast('Reset zoom to 100%', 'info', 2000);
          return;
        }
      }

      // Toggle Virtual Scrolling: Ctrl+Shift+V / Cmd+Shift+V
      if (isCtrlOrCmd && isShift && e.key === 'V') {
        e.preventDefault();
        toggleVirtualScrolling();
        const isVirtual = useDesignerStore.getState().virtualScrolling;
        addToast(`${isVirtual ? 'Enabled' : 'Disabled'} virtual scrolling`, 'info', 2000);
        return;
      }

      // Help: F1 or Ctrl+/ / Cmd+/
      if (e.key === 'F1' || (isCtrlOrCmd && e.key === '/')) {
        e.preventDefault();
        // This would open the help modal
        addToast('Keyboard shortcuts help: Press ? for shortcuts', 'info', 3000);
        return;
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [undo, redo, copyElements, pasteElements, selectAll, deleteElement, selectedIds, canUndo, canRedo, canPaste, toggleVirtualScrolling, zoomIn, zoomOut, resetZoom, addToast]);

  if (!showHelp) return null;

  const shortcuts = [
    { keys: ['Ctrl', 'Z'], description: 'Undo last action' },
    { keys: ['Ctrl', 'Y'], description: 'Redo last action' },
    { keys: ['Ctrl', 'Shift', 'Z'], description: 'Redo last action (alternative)' },
    { keys: ['Ctrl', 'C'], description: 'Copy selected elements' },
    { keys: ['Ctrl', 'V'], description: 'Paste elements' },
    { keys: ['Ctrl', 'A'], description: 'Select all elements' },
    { keys: ['Delete'], description: 'Delete selected elements' },
    { keys: ['Backspace'], description: 'Delete selected elements' },
    { keys: ['Tab'], description: 'Navigate to next element' },
    { keys: ['Shift', 'Tab'], description: 'Navigate to previous element' },
    { keys: ['↑'], description: 'Navigate to previous element' },
    { keys: ['↓'], description: 'Navigate to next element' },
    { keys: ['←'], description: 'Move selected element left' },
    { keys: ['→'], description: 'Move selected element right' },
    { keys: ['Home'], description: 'Navigate to first element' },
    { keys: ['End'], description: 'Navigate to last element' },
    { keys: ['Page Up'], description: 'Zoom in' },
    { keys: ['Page Down'], description: 'Zoom out' },
    { keys: ['Ctrl', 'Home'], description: 'Reset zoom to 100%' },
    { keys: ['Escape'], description: 'Clear selection' },
    { keys: ['Ctrl', 'Shift', 'V'], description: 'Toggle virtual scrolling' },
    { keys: ['F1'], description: 'Show keyboard shortcuts' },
    { keys: ['Ctrl', '/'], description: 'Show keyboard shortcuts' },
  ];

  return (
    <div
      className="ui-modal-overlay"
      onClick={onClose}
      role="presentation"
    >
      <div
        className="ui-modal-surface"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="keyboard-shortcuts-title"
      >
        <div className="ui-modal-header">
          <h2 id="keyboard-shortcuts-title" className="ui-modal-title">
            Keyboard Shortcuts
          </h2>
          <button
            onClick={onClose}
            className="ui-modal-close"
            aria-label="Close"
          >
            ×
          </button>
        </div>

        <p className="ui-modal-lead">
            Use these keyboard shortcuts to work more efficiently in the UI Designer.
            <br />
            <strong>Note:</strong> On Mac, use <kbd className="ui-kbd">Cmd</kbd> instead of <kbd className="ui-kbd">Ctrl</kbd>.
        </p>

        <div className="ui-modal-grid">
          {shortcuts.map((shortcut, index) => (
            <div key={index} className="ui-modal-row">
              <span className="ui-modal-row-label">
                {shortcut.description}
              </span>
              <div className="ui-key-list">
                {shortcut.keys.map((key, keyIndex) => (
                  <React.Fragment key={keyIndex}>
                    <kbd className="ui-kbd">{key}</kbd>
                    {keyIndex < shortcut.keys.length - 1 && (
                      <span className="ui-kbd-sep">+</span>
                    )}
                  </React.Fragment>
                ))}
              </div>
            </div>
          ))}
        </div>

        <div className="ui-modal-tip">
          <p>
            <strong>Tip:</strong> Shortcuts work when not typing in input fields. Press <kbd className="ui-kbd">F1</kbd> or <kbd className="ui-kbd">Ctrl+/</kbd> anytime to see this help.
          </p>
        </div>
      </div>
    </div>
  );
};

export default KeyboardShortcuts;