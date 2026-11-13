/** @jest-environment jsdom */
import { fireEvent, render } from '@testing-library/react';
import '@testing-library/jest-dom';
import { MemoryRouter } from 'react-router-dom';
import type { LocalizedProperty, SimpleElement } from '@/types';
import { ELEMENT_CATALOG } from '@/docs/elementCatalog';
import { DocumentElementRenderer, DOCUMENT_RENDERED_ELEMENT_TYPES, documentContentBoxStyle } from '@/components/Rendering/DocumentElementRenderer';
import { createDocumentContentResolver, documentDateText, effectiveDocumentElement,
  elementMatchesDocumentLanguage, resolveDocumentElement } from '@/components/Rendering/documentContent';
import DocumentImageRenderer from '@/components/Rendering/DocumentImageRenderer';
import EditorDocumentMarker from '@/components/Editor/EditorDocumentMarker';
import LivePreview from '@/components/Preview/LivePreview';
import { DEFAULT_PAGE_SETTINGS, useEditorStore } from '@/store';
import ExportService from '@/services/ExportService';

jest.mock('react-i18next', () => ({ useTranslation: () => ({ t: (key: string) => key }) }));
jest.mock('@/auth/DesignerAuthGate', () => ({ useDesignerAuth: () => ({ user: { permissions: ['designer.pdf.export'] } }) }));
jest.mock('@/services/ExportService', () => ({ __esModule: true, default: { exportToJSON: jest.fn() },
  DesignerAccessibilityPreflightError: class extends Error {} }));
jest.mock('@/components/Editor/ExportModal', () => ({ __esModule: true, default: () => null }));
jest.mock('@/components/Rendering/BackendSymbolRenderer', () => ({ __esModule: true,
  default: ({ element }: { element: SimpleElement }) => <span>{element.qrValue ?? element.barcodeValue}</span> }));

const element = (patch: Partial<SimpleElement> = {}): SimpleElement => ({
  id: 'element', type: 'text', x: 20, y: 30, width: 240, height: 60, content: 'Content', ...patch,
});
const context = { pageIndex: 0, pageCount: 3, pageHeight: 842 };

test.each(ELEMENT_CATALOG)('$type has an explicit printable renderer', entry => {
  const value = entry.example as unknown as SimpleElement;
  const { container } = render(<DocumentElementRenderer {...context} element={value} now={new Date('2026-11-13T12:00:00Z')} />);
  expect(DOCUMENT_RENDERED_ELEMENT_TYPES).toContain(entry.type);
  expect(container.querySelector('[data-unsupported-element]')).toBeNull();
});

test('localized values follow backend base-language, ownership, case and nonempty fallback rules', () => {
  const properties: LocalizedProperty[] = [
    { key: 'Title', scope: 'global', localizedValues: { fr: 'Bonjour', en: 'Hello' } },
    { key: 'Fallback', scope: 'global', localizedValues: { fr: '', en: 'Fallback' } },
    { key: 'Private', scope: 'own', ownerLanguage: 'de-DE', localizedValues: { de: 'Privat' } },
  ];
  const resolve = createDocumentContentResolver(properties, 'fr-FR', 'en-US');
  expect(resolve('{{TITLE}} {{Fallback}} {{Private}} {{Unknown}}')).toBe('Bonjour Fallback {{Private}} {{Unknown}}');
  expect(resolve(undefined)).toBeUndefined();
  expect(createDocumentContentResolver(properties, 'de-CH', 'en')('{{Private}}')).toBe('Privat');
});

test('resolution preserves the source document, unknown tokens and stable form identities', () => {
  const source = element({ type: 'field', fieldName: '{{Key}}', fieldLabel: '{{Key}}', content: '{{Key}}',
    signatureLabel: undefined, cellData: [['{{Key}}']] });
  const resolve = createDocumentContentResolver([{ key: 'Key', scope: 'global', localizedValues: { en: 'Display' } }], 'en', 'en');
  const resolved = resolveDocumentElement(source, resolve);
  expect(resolved).toMatchObject({ content: 'Display', fieldLabel: 'Display', fieldName: '{{Key}}' });
  expect(resolved.signatureLabel).toBeUndefined();
  expect(source.fieldLabel).toBe('{{Key}}');
  // Table datasets have their own binding pipeline; do not introduce UI-only token expansion.
  expect(resolved.cellData).toBe(source.cellData);
});

test('all backend-localized display fields use the same resolver', () => {
  const fields = ['content', 'htmlContent', 'fieldLabel', 'signatureLabel', 'qrValue', 'barcodeValue',
    'noteTitle', 'noteBody', 'footnoteText', 'href', 'buttonAction'] as const;
  for (const key of fields) {
    const source = element({ [key]: '{{Title}}' });
    expect(resolveDocumentElement(source, value => value?.replace('{{Title}}', 'Resolved'))[key]).toBe('Resolved');
  }
});

test('language layout resolves zero values and base tags without modifying the template', () => {
  const source = element({ langOverrides: { fr: { x: 0, width: 100, rotation: 0 } }, style: { rotation: 45 } });
  expect(effectiveDocumentElement(source, 'fr-FR')).toMatchObject({ x: 0, width: 100, height: 60, style: { rotation: 0 } });
  expect(source).toMatchObject({ x: 20, width: 240, style: { rotation: 45 } });
  expect(elementMatchesDocumentLanguage(element({ elementLanguage: 'fr' }), 'fr-FR')).toBe(true);
  expect(elementMatchesDocumentLanguage(element({ elementLanguage: 'de' }), 'fr-FR')).toBe(false);
});

test('static and binding dates do not insert the current date or an unexplained dash', () => {
  expect(documentDateText(element({ dateMode: 'static', content: '' }))).toBe('');
  expect(documentDateText(element({ dateMode: 'binding', binding: 'Invoice.Date' }))).toBe('{{ Invoice.Date }}');
  expect(documentDateText(element({ dateMode: 'render', locale: 'invalid_locale', timezone: 'invalid/timezone' }),
    new Date('2026-11-13T12:00:00Z'))).toBe('Nov 13, 2026');
});

test('image aspect ratio, clipping and zero focal coordinates have one shared behavior', () => {
  const { container, rerender } = render(<DocumentImageRenderer label="Image" element={element({
    type: 'image', content: 'data:image/png;base64,test', fitMode: 'cover', focalX: 0, focalY: 0,
  })} />);
  expect(container.querySelector('img')).toHaveStyle({ objectFit: 'cover', objectPosition: '0% 0%' });
  expect(container.firstElementChild).toHaveStyle({ overflow: 'hidden' });
  rerender(<DocumentImageRenderer label="Image" element={element({ type: 'image', content: 'data:image/png;base64,test',
    fitMode: 'cover', preserveAspectRatio: true })} />);
  expect(container.querySelector('img')).toHaveStyle({ objectFit: 'contain' });
});

test('missing or failed images do not contact an external placeholder service and can recover', () => {
  const { container, rerender } = render(<DocumentImageRenderer label="Image" element={element({ type: 'image', content: '' })} />);
  expect(container.querySelector('img')).toBeNull();
  expect(container.querySelector('[data-image-placeholder]')).not.toBeNull();
  rerender(<DocumentImageRenderer label="Image" element={element({ type: 'image', content: 'first.png' })} />);
  fireEvent.error(container.querySelector('img')!);
  expect(container.querySelector('img')).toBeNull();
  rerender(<DocumentImageRenderer label="Image" element={element({ type: 'image', content: 'second.png' })} />);
  expect(container.querySelector('img')).toHaveAttribute('src', 'second.png');
  rerender(<DocumentImageRenderer label="Image" element={element({ type: 'image', content: 'first.png' })} />);
  expect(container.querySelector('img')).toHaveAttribute('src', 'first.png');
});

test.each(['contain', 'cover', 'fill', 'none'] as const)('image fit %s keeps focus and intrinsic-size semantics', fitMode => {
  const { container } = render(<DocumentImageRenderer label="Image" element={element({
    type: 'image', content: 'local.png', fitMode, focalX: 100, focalY: 0,
  })} />);
  expect(container.querySelector('img')).toHaveStyle({ objectFit: fitMode, objectPosition: '100% 0%' });
  expect(container.firstElementChild).toHaveStyle({ overflow: 'hidden', width: '100%', height: '100%' });
});

test('image focus clamps to edges and invalid values fall back to center', () => {
  const { container, rerender } = render(<DocumentImageRenderer label="Image" element={element({
    type: 'image', content: 'local.png', focalX: -10, focalY: 110,
  })} />);
  expect(container.querySelector('img')).toHaveStyle({ objectFit: 'contain', objectPosition: '0% 100%' });
  rerender(<DocumentImageRenderer label="Image" element={element({ type: 'image', content: 'local.png',
    focalX: Number.NaN, focalY: Number.POSITIVE_INFINITY })} />);
  expect(container.querySelector('img')).toHaveStyle({ objectPosition: '50% 50%' });
});

test('legacy header/footer-only tables have no invented body and retain false/zero overrides', () => {
  const { container } = render(<DocumentElementRenderer {...context} element={element({
    type: 'table', content: undefined, headerRow: true, footerRow: true, style: { rows: 2, columns: 1 },
    cellStyles: [{ row: 0, col: 0, bold: false, italic: false, padding: 0 }],
  })} />);
  expect(container.querySelectorAll('tbody tr')).toHaveLength(0);
  expect(container.querySelector('thead th')).toHaveStyle({ fontWeight: 400, padding: 0, fontStyle: 'normal' });
});

test('zoom scales the entire intrinsic document content, including legacy table borders and fonts', () => {
  const { container } = render(<DocumentElementRenderer {...context} zoom={2} element={element({ type: 'table' })} />);
  expect(container.querySelector('[data-document-scale]')).toHaveStyle({ width: '240px', height: '60px', transform: 'scale(2)' });
  expect(container.querySelector('td')).toHaveStyle({ fontSize: '10px' });
});

test('document frames do not duplicate SVG boxes and retain zero-valued padding', () => {
  expect(documentContentBoxStyle(element({ style: { borderWidth: 2 } }))).toBeUndefined();
  expect(documentContentBoxStyle(element({ type: 'image', style: { borderWidth: 2, paddingLeft: 0 } })))
    .toMatchObject({ border: '2px solid #000', paddingLeft: 0, boxSizing: 'border-box' });
});

test.each(['bookmark', 'pageboundary'] as const)('%s markers are editor-only, not printable content', type => {
  const value = element({ type });
  const printable = render(<DocumentElementRenderer {...context} element={value} />);
  expect(printable.container).toBeEmptyDOMElement();
  const editor = render(<EditorDocumentMarker element={value} />);
  expect(editor.container).not.toBeEmptyDOMElement();
});

test('real Live Preview resolves localized content and layout while excluding other-language and hidden elements', () => {
  useEditorStore.setState({ currentPreviewLanguage: 'fr-FR' });
  const pages = [{ id: 'page', elements: [element({ content: '{{Title}}', langOverrides: { fr: { x: 0, width: 120 } } }),
    element({ id: 'german', content: 'Only German', elementLanguage: 'de' }), element({ id: 'hidden', content: 'Hidden', hidden: true })] }];
  const source = JSON.stringify(pages);
  const { container } = render(<MemoryRouter><LivePreview template={{ id: 'template', name: 'Test', category: 'test', description: '' }}
    pages={pages} pageSettings={{ ...DEFAULT_PAGE_SETTINGS, localizedProperties: [
      { key: 'Title', scope: 'global', localizedValues: { fr: 'Bonjour' } },
    ] }} sharedElements={[element({ id: 'shared', type: 'checkbox', fieldLabel: '{{Title}}' })]}
    visualParityMode onBack={() => {}} onExport={() => {}} /></MemoryRouter>);
  expect(container.querySelector('[data-element-id="element"]')).toHaveTextContent('Bonjour');
  expect(container.querySelector('[data-element-id="element"]')).toHaveStyle({ left: '0px', width: '120px' });
  expect(container.querySelector('[data-element-id="shared"]')).toHaveTextContent('Bonjour');
  expect(container.querySelector('[data-element-id="german"]')).toBeNull();
  expect(container.querySelector('[data-element-id="hidden"]')).toBeNull();
  expect(JSON.stringify(pages)).toBe(source);
});

test('preview exports carry the displayed language without changing persisted page settings', () => {
  useEditorStore.setState({ currentPreviewLanguage: 'fr' });
  const settings = { ...DEFAULT_PAGE_SETTINGS, systemLanguage: 'en', targetLanguage: undefined };
  const { getByRole } = render(<MemoryRouter><LivePreview
    template={{ id: 'template', name: 'Test', category: 'test', description: '' }}
    pages={[{ id: 'page', elements: [element()] }]} pageSettings={settings}
    onBack={() => {}} onExport={() => {}} /></MemoryRouter>);
  fireEvent.click(getByRole('button', { name: 'export' }));
  fireEvent.click(getByRole('button', { name: /exportMenu.json.label/ }));
  expect(ExportService.exportToJSON).toHaveBeenCalledWith(expect.anything(), expect.anything(), [],
    expect.objectContaining({ targetLanguage: 'fr', systemLanguage: 'en' }));
  expect(settings.targetLanguage).toBeUndefined();
});
