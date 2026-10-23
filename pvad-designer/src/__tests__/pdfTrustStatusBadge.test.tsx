/**
 * @jest-environment jsdom
 */
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { TrustStatusBadge } from '@/features/pdf-viewer/TrustStatusBadge';

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

describe('TrustStatusBadge', () => {
  test.each([
    [0, 'Valid'],
    [1, 'Invalid'],
    [2, 'Indeterminate'],
    [3, 'Unsupported'],
    [4, 'Not applicable'],
  ] as const)('renders status %s with text, icon semantics, and an explanation', (status, label) => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);
    act(() => root.render(
      <TrustStatusBadge status={status} label={label} explanation={`${label} explanation`} />,
    ));

    const badge = container.querySelector('.pdfv-trust-status');
    expect(badge?.getAttribute('aria-label')).toBe(`${label}. ${label} explanation`);
    expect(badge?.getAttribute('data-status')).toBe(String(status));
    expect(badge?.textContent).toContain(label);
    expect(badge?.querySelector('svg')).not.toBeNull();
    act(() => root.unmount());
    container.remove();
  });
});
