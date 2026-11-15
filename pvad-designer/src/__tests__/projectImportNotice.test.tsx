/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import fs from 'node:fs';
import path from 'node:path';
import '@/i18n';
import ProjectImportNotice from '@/components/Project/ProjectImportNotice';

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const labels = {
  opening: 'Opening project', review: 'Review project', failed: 'Open failed',
  uploading: 'Uploading', validating: 'Validating', assets: 'Assets', applying: 'Applying',
  cancel: 'Cancel', dismiss: 'Dismiss', openAnyway: 'Open project', keepCurrent: 'Keep current',
  preserved: 'Current document is preserved.', missingFeature: (feature: string) => `Missing: ${feature}`,
};

describe('ProjectImportNotice', () => {
  let container: HTMLDivElement;
  let root: Root;

  beforeEach(() => {
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
  });

  test('shows determinate progress and offers cancellation while uploading', () => {
    const cancel = jest.fn();
    act(() => root.render(
      <ProjectImportNotice
        state={{ kind: 'progress', phase: 'uploading', percent: 42, cancellable: true }}
        labels={labels}
        onCancel={cancel}
        onDismiss={jest.fn()}
        onReviewDecision={jest.fn()}
      />,
    ));

    expect(container.querySelector('[role="progressbar"]')?.getAttribute('aria-valuenow')).toBe('42');
    (Array.from(container.querySelectorAll('button')).find(button => button.textContent === 'Cancel') as HTMLButtonElement).click();
    expect(cancel).toHaveBeenCalledTimes(1);
  });

  test('requires an explicit decision before a review project is applied', () => {
    const decide = jest.fn();
    act(() => root.render(
      <ProjectImportNotice
        state={{
          kind: 'review',
          compatibility: {
            requiresReview: true,
            missingFeatures: ['pdf.future'],
            diagnostics: [{ code: 'PVADPROJECT_EXTERNAL_LINK', severity: 'warning', message: 'External link', source: 'page-1' }],
          },
        }}
        labels={labels}
        onCancel={jest.fn()}
        onDismiss={jest.fn()}
        onReviewDecision={decide}
      />,
    ));

    expect(container.textContent).toContain('Missing: pdf.future');
    expect(container.textContent).toContain('PVADPROJECT_EXTERNAL_LINK');
    (Array.from(container.querySelectorAll('button')).find(button => button.textContent === 'Keep current') as HTMLButtonElement).click();
    expect(decide).toHaveBeenCalledWith(false);
  });

  test('keeps stable diagnostics visible after a failed open', () => {
    act(() => root.render(
      <ProjectImportNotice
        state={{ kind: 'error', code: 'PVADPROJECT007', message: 'Integrity validation failed.' }}
        labels={labels}
        onCancel={jest.fn()}
        onDismiss={jest.fn()}
        onReviewDecision={jest.fn()}
      />,
    ));

    const alert = container.querySelector('[role="alert"]');
    expect(alert?.textContent).toContain('PVADPROJECT007');
    expect(alert?.textContent).toContain('Current document is preserved.');
  });

  test('projects diagnostic references in review messages without renaming source documents', () => {
    const diagnostic = {
      code: 'PVADPROJECT_EXTERNAL_LINK', severity: 'warning' as const,
      message: 'Review PVADPROJECT_EXTERNAL_LINK before opening.', source: 'PVAD-customer.pdf',
    };
    act(() => root.render(
      <ProjectImportNotice
        state={{ kind: 'review', compatibility: { requiresReview: true, missingFeatures: [], diagnostics: [diagnostic] } }}
        labels={labels} onCancel={jest.fn()} onDismiss={jest.fn()} onReviewDecision={jest.fn()}
      />,
    ));
    expect(container.textContent).toContain('Review PVADPROJECT_EXTERNAL_LINK before opening.');
    expect(container.textContent).toContain('(PVAD-customer.pdf)');
    expect(diagnostic.code).toBe('PVADPROJECT_EXTERNAL_LINK');
    expect(diagnostic.message).toContain('PVADPROJECT_EXTERNAL_LINK');
    expect(diagnostic.source).toBe('PVAD-customer.pdf');
  });

  test('projects diagnostic references in failure text without changing the failure object', () => {
    const state = { kind: 'error' as const, code: 'PVADPROJECT007', message: 'PVADPROJECT007: Integrity validation failed.' };
    act(() => root.render(
      <ProjectImportNotice state={state} labels={labels}
        onCancel={jest.fn()} onDismiss={jest.fn()} onReviewDecision={jest.fn()} />,
    ));
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('PVADPROJECT007: Integrity validation failed.');
    expect(container.querySelector('[role="alert"]')?.textContent).not.toContain('PXA');
    expect(state.message).toBe('PVADPROJECT007: Integrity validation failed.');
    expect(state.code).toBe('PVADPROJECT007');
  });

  test('uses a bounded full-width action surface on mobile', () => {
    const css = fs.readFileSync(path.join(process.cwd(), 'src/styles/editor.css'), 'utf8');
    expect(css).toMatch(/@media \(max-width: 640px\)[\s\S]*?\.project-import-notice\s*\{[^}]*inset-inline:\s*10px;[^}]*width:\s*auto;/);
    expect(css).toMatch(/\.project-import-notice\s*\{[^}]*max-height:[^;]+;[^}]*overflow:\s*auto;/s);
    expect(css).toMatch(/\.project-import-actions\s*\{[^}]*flex-direction:\s*column-reverse;/s);
  });

  test('legacy conversion guidance is translated in all six languages', () => {
    for (const language of ['en', 'de', 'fr', 'es', 'it', 'ar']) {
      const messages = JSON.parse(fs.readFileSync(path.join(process.cwd(), 'src/locales', language, 'editor.json'), 'utf8'));
      expect(messages.projectImport.legacyFormat).toContain('PVADP');
      expect(messages.projectImport.legacyFormat).toContain('PVADS');
    }
  });
});
