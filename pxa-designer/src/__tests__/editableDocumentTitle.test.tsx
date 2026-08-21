/** @jest-environment jsdom */

import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { EditableDocumentTitle } from '@/components/Editor/EditableDocumentTitle';

const reactTestEnvironment = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
reactTestEnvironment.IS_REACT_ACT_ENVIRONMENT = true;

describe('EditableDocumentTitle', () => {
  let container: HTMLDivElement;
  let root: Root;
  let onRename: jest.Mock;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    onRename = jest.fn();
    act(() => {
      root.render(
        <EditableDocumentTitle
          name="Untitled document"
          inputLabel="Document name"
          actionLabel="Rename Untitled document"
          hint="Rename document"
          validationMessage="Enter a name between 1 and 200 characters."
          onRename={onRename}
        />,
      );
    });
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  const startEditing = () => {
    const button = container.querySelector('button') as HTMLButtonElement;
    act(() => button.click());
    return container.querySelector('input') as HTMLInputElement;
  };

  const change = (input: HTMLInputElement, value: string) => {
    act(() => {
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
      setter?.call(input, value);
      input.dispatchEvent(new Event('input', { bubbles: true }));
    });
  };

  const keyDown = (input: HTMLInputElement, key: string) => {
    act(() => input.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true })));
  };

  it('trims and commits a name with Enter', () => {
    const input = startEditing();
    change(input, '  Quarterly report  ');
    keyDown(input, 'Enter');
    expect(onRename).toHaveBeenCalledWith('Quarterly report');
  });

  it('commits a valid name on blur', () => {
    const input = startEditing();
    change(input, 'Invoice');
    act(() => input.dispatchEvent(new FocusEvent('focusout', { bubbles: true })));
    expect(onRename).toHaveBeenCalledWith('Invoice');
  });

  it('restores the original name on Escape', () => {
    const input = startEditing();
    change(input, 'Discard me');
    keyDown(input, 'Escape');
    expect(onRename).not.toHaveBeenCalled();
    expect(container.textContent).toContain('Untitled document');
  });

  it('keeps invalid empty names open and shows an accessible error', () => {
    const input = startEditing();
    change(input, '   ');
    keyDown(input, 'Enter');
    expect(onRename).not.toHaveBeenCalled();
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('1 and 200');
    expect(input.getAttribute('aria-invalid')).toBe('true');
  });
});
