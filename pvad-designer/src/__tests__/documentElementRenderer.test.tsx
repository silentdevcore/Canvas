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
import { layoutPdfTextBlock } from '@/components/Rendering/PdfTextRenderer';

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
    new Date('2026-11-13T12:00:00Z'))).toBe('2026-11-13');
});

test('Arabic date content and explicitly Arabic numeric dates use the packaged script font', () => {
  const { container, rerender } = render(<DocumentElementRenderer {...context} now={new Date('2026-11-13T12:00:00Z')}
    element={element({ type: 'date', dateMode: 'render', dateFormat: 'D', locale: 'ar-EG' })} />);
  expect(container.querySelector('text')).toHaveAttribute('direction', 'rtl');
  expect(container.querySelector('text')?.getAttribute('font-family')).toContain('PVADE Arabic');
  expect(container.textContent).toContain('2026');
  rerender(<DocumentElementRenderer {...context} element={element({ type: 'date', dateMode: 'static', content: '2026-11-13', language: 'ar' })} />);
  expect(container.querySelector('text')?.getAttribute('font-family')).toContain('PVADE Arabic');
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

test('narrow dates use paragraph wrapping instead of a clipped single line', () => {
  const date = element({ type: 'date', dateMode: 'static', content: 'Friday 13 November 2026', width: 120 });
  const source = JSON.stringify(date);
  const { container } = render(<DocumentElementRenderer {...context} element={date} />);
  expect([...container.querySelectorAll('text')].map(node => node.textContent)).toEqual(['Friday 13 November', '2026']);
  expect(container.querySelector('svg[data-date-paragraph]')).toHaveStyle({ overflow: 'visible' });
  expect(documentContentBoxStyle(date)).toBeUndefined();
  expect(JSON.stringify(date)).toBe(source);
});

test('date alignment centers within asymmetric padding exactly once', () => {
  const { container } = render(<DocumentElementRenderer {...context} element={element({ type: 'date', width: 180,
    content: 'Friday 13 November 2026', style: { fontSize: 12, paddingLeft: 24, paddingRight: 4, paddingTop: 10, textAlign: 'center' } })} />);
  expect(container.querySelector('text')).toHaveAttribute('x', '100');
  expect(container.querySelector('text')).toHaveAttribute('y', '18.64');
  expect(container.querySelector('text')).toHaveAttribute('text-anchor', 'middle');
});

test('date right alignment and explicit zero padding override the uniform inset', () => {
  const { container } = render(<DocumentElementRenderer {...context} element={element({ type: 'date', width: 180,
    content: 'Friday', style: { padding: 9, paddingRight: 0, textAlign: 'right' } })} />);
  expect(container.querySelector('text')).toHaveAttribute('x', '180');
  expect(container.querySelector('text')).toHaveAttribute('text-anchor', 'end');
});

test('empty dates keep their frame and explicit zero-width sides suppress a uniform border', () => {
  const { container, rerender } = render(<DocumentElementRenderer {...context} element={element({ type: 'date', content: '',
    style: { backgroundColor: '#e0f2fe', borderWidth: 2, borderColor: '#2563eb', borderRadius: 8 } })} />);
  expect(container.querySelectorAll('text')).toHaveLength(0);
  expect(container.querySelectorAll('rect')).toHaveLength(2);
  expect(container.querySelector('rect[stroke]')).toHaveAttribute('rx', '8');
  rerender(<DocumentElementRenderer {...context} element={element({ type: 'date', content: '',
    style: { borderWidth: 2, borderStyle: 'dotted' } })} />);
  expect(container.querySelector('rect[stroke]')).toHaveAttribute('stroke-dasharray', '2 4');
  rerender(<DocumentElementRenderer {...context} element={element({ type: 'date', content: '',
    style: { borderBottomWidth: 2, borderBottomStyle: 'dashed' } })} />);
  expect(container.querySelector('line')).toHaveAttribute('stroke-dasharray', '8 4');
  rerender(<DocumentElementRenderer {...context} element={element({ type: 'date', content: '', style: { borderWidth: 4,
    borderTopWidth: 0, borderRightWidth: 0, borderBottomWidth: 0, borderLeftWidth: 0 } })} />);
  expect(container.querySelectorAll('rect[stroke],line')).toHaveLength(0);
});

test('date paragraph diagnostics detect content overflow without resizing the document', () => {
  const short = element({ type: 'date', width: 110, height: 12 });
  expect(layoutPdfTextBlock(short, 'Friday 13 November 2026').overflows).toBe(true);
  expect(layoutPdfTextBlock(short, '').overflows).toBe(false);
  expect(layoutPdfTextBlock({ ...short, width: 40, height: 100 }, '2026-11-13T12:00:00Z').overflows).toBe(true);
  expect(short.height).toBe(12);
});

test.each([0, 1.8, 22])('date line height %s follows the PDF paragraph rule', requested => {
  const date = element({ type: 'date', width: 110, style: { fontSize: 12, lineHeight: requested } });
  const layout = layoutPdfTextBlock(date, 'Friday 13 November 2026');
  expect(layout.lineHeight).toBeCloseTo(requested === 0 ? 14.4 : requested < 8 ? requested * 12 : requested);
});

test('Live Preview leaves overflowing date content visible like backend PDF paragraphs', () => {
  const { container } = render(<MemoryRouter><LivePreview template={{ id: 'date', name: 'Dates', category: '', description: '' }}
    pages={[{ id: 'page', elements: [element({ type: 'date', width: 110, height: 12, content: 'Friday 13 November 2026' })] }]}
    pageSettings={DEFAULT_PAGE_SETTINGS} visualParityMode onBack={() => {}} onExport={() => {}} /></MemoryRouter>);
  expect(container.querySelector('[data-element-id]')).toHaveStyle({ height: '12px', overflow: 'visible' });
  expect(container.querySelectorAll('[data-element-id] text').length).toBeGreaterThan(1);
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
