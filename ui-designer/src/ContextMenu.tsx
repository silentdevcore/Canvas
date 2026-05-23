import React, { useEffect, useRef, useMemo, useState } from 'react';
import { useDesignerStore } from './store';

interface ContextMenuProps {
  x: number;
  y: number;
  onClose: () => void;
  elementId?: string;
}

type ContextActionItem = {
  label: string;
  action: () => void;
  disabled: boolean;
  danger?: boolean;
  type?: undefined;
};

type ContextDividerItem = {
  type: 'divider';
};

type ContextMenuItem = ContextActionItem | ContextDividerItem;

const ContextMenu: React.FC<ContextMenuProps> = ({ x, y, onClose, elementId }) => {
  const menuRef = useRef<HTMLDivElement>(null);
  const itemRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const {
    deleteElement,
    copyElements,
    pasteElements,
    selectAll,
    toggleElementLock,
    canPaste,
    elements
  } = useDesignerStore();

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        onClose();
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    document.addEventListener('keydown', handleKeyDown);

    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [onClose]);

  const handleAction = (action: () => void) => {
    action();
    onClose();
  };

  const menuItems: ContextMenuItem[] = [];

  if (elementId) {
    // Context menu for specific element
    const element = elements[elementId];
    menuItems.push(
      {
        label: element.locked ? '🔓 Unlock Element' : '🔒 Lock Element',
        action: () => handleAction(() => toggleElementLock(elementId)),
        disabled: false
      },
      {
        label: '📋 Copy',
        action: () => handleAction(() => {
          // Select the element first, then copy
          const { selectElement } = useDesignerStore.getState();
          selectElement(elementId);
          copyElements();
        }),
        disabled: false
      },
      { type: 'divider' },
      {
        label: '🗑️ Delete',
        action: () => handleAction(() => deleteElement(elementId)),
        disabled: false,
        danger: true
      }
    );
  } else {
    // Context menu for canvas
    menuItems.push(
      {
        label: '📋 Paste',
        action: () => handleAction(() => pasteElements()),
        disabled: !canPaste
      },
      {
        label: '📋 Select All',
        action: () => handleAction(() => selectAll()),
        disabled: false
      }
    );
  }

  const actionableIndexes = useMemo(
    () => menuItems.map((item, index) => ({ item, index })).filter(({ item }) => item.type !== 'divider' && !item.disabled).map(({ index }) => index),
    [menuItems]
  );

  const [focusedIndex, setFocusedIndex] = useState(actionableIndexes[0] ?? -1);

  useEffect(() => {
    const initial = actionableIndexes[0] ?? -1;
    setFocusedIndex(initial);
    if (initial >= 0) {
      itemRefs.current[initial]?.focus();
    }
  }, [actionableIndexes]);

  const moveFocus = (direction: 1 | -1) => {
    if (actionableIndexes.length === 0) return;
    const currentPos = actionableIndexes.indexOf(focusedIndex);
    const safePos = currentPos === -1 ? 0 : currentPos;
    const nextPos = (safePos + direction + actionableIndexes.length) % actionableIndexes.length;
    const nextIndex = actionableIndexes[nextPos];
    setFocusedIndex(nextIndex);
    itemRefs.current[nextIndex]?.focus();
  };

  const handleMenuKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      event.preventDefault();
      onClose();
      return;
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      moveFocus(1);
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      moveFocus(-1);
      return;
    }

    if (event.key === 'Home' && actionableIndexes.length > 0) {
      event.preventDefault();
      const first = actionableIndexes[0];
      setFocusedIndex(first);
      itemRefs.current[first]?.focus();
      return;
    }

    if (event.key === 'End' && actionableIndexes.length > 0) {
      event.preventDefault();
      const last = actionableIndexes[actionableIndexes.length - 1];
      setFocusedIndex(last);
      itemRefs.current[last]?.focus();
      return;
    }

    if ((event.key === 'Enter' || event.key === ' ') && focusedIndex >= 0) {
      event.preventDefault();
      itemRefs.current[focusedIndex]?.click();
    }
  };

  return (
    <div
      ref={menuRef}
      role="menu"
      aria-label="Context menu"
      tabIndex={-1}
      onKeyDown={handleMenuKeyDown}
      className="ui-context-menu"
      style={{
        left: x,
        top: y,
      }}
    >
      {menuItems.map((item, index) => {
        if (item.type === 'divider') {
          return (
            <div key={index} className="ui-context-menu-divider" />
          );
        }

        return (
          <button
            key={index}
            ref={(el) => {
              itemRefs.current[index] = el;
            }}
            onClick={item.action}
            disabled={item.disabled}
            role="menuitem"
            tabIndex={focusedIndex === index ? 0 : -1}
            onFocus={() => setFocusedIndex(index)}
            className={`ui-context-menu-item${item.danger ? ' is-danger' : ''}${item.disabled ? ' is-disabled' : ''}`}
          >
            {item.label}
          </button>
        );
      })}
    </div>
  );
};

export default ContextMenu;