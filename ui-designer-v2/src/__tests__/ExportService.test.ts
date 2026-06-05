import { ExportService } from '../services/ExportService';
import { DEFAULT_PAGE_SETTINGS } from '../store';
import type { SimpleElement, Template, PageSettings, Page } from '../types';

const mockTemplate: Template = {
  id: 'tpl-1',
  name: 'Test Template',
  category: 'test',
  thumbnail: '',
  description: 'Unit test template',
};

const makeEl = (overrides: Partial<SimpleElement> = {}): SimpleElement => ({
  id: 'el-1',
  type: 'text',
  x: 72,
  y: 72,
  width: 200,
  height: 40,
  content: 'Hello World',
  ...overrides,
});

const makePage = (elements: SimpleElement[] = []): Page => ({ id: 'page-1', elements });

// J: Test save/load round-trip — export payload structure
describe('convertElementsToTemplate', () => {
  test('includes template metadata fields', () => {
    const out = ExportService.convertElementsToTemplate([], mockTemplate);
    expect(out.id).toBe('tpl-1');
    expect(out.name).toBe('Test Template');
    expect(out.version).toBe('1.0');
    expect(out.schemaVersion).toBe('1.0');
  });

  test('maps elements correctly', () => {
    const out = ExportService.convertElementsToTemplate([makePage([makeEl()])], mockTemplate);
    expect(out.elements).toHaveLength(1);
    expect(out.elements[0].type).toBe('Text');
    expect(out.elements[0].id).toBe('el-1');
  });

  // J: Test page presets and custom sizes
  test('pageSettings width/height are included in payload', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, width: 612, height: 792 };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.width).toBe(612);
    expect(out.pageSettings.height).toBe(792);
  });

  // J: Test orientation switching
  test('landscape orientation is preserved', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, orientation: 'landscape', width: 842, height: 595 };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.orientation).toBe('landscape');
    expect(out.pageSettings.width).toBe(842);
    expect(out.pageSettings.height).toBe(595);
  });

  // J: Test header/footer rendering in export
  test('header is included when enabled', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, headerEnabled: true, headerHeight: 80 };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.header).toEqual({ height: 80 });
  });

  test('header is null when disabled', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, headerEnabled: false };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.header).toBeNull();
  });

  test('footer is included when enabled', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, footerEnabled: true, footerHeight: 50 };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.footer).toEqual({ height: 50 });
  });

  // J: Test watermark/background rendering in export
  test('watermark included when enabled', () => {
    const ps: PageSettings = {
      ...DEFAULT_PAGE_SETTINGS,
      globalWatermark: { ...DEFAULT_PAGE_SETTINGS.globalWatermark, enabled: true, content: 'DRAFT', mode: 'text' }
    };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.watermark).not.toBeNull();
    expect((out.pageSettings.watermark as PageSettings['globalWatermark']).content).toBe('DRAFT');
  });

  test('watermark is null when disabled', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, globalWatermark: { ...DEFAULT_PAGE_SETTINGS.globalWatermark, enabled: false } };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.watermark).toBeNull();
  });

  test('backgroundColor is included', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, backgroundColor: '#f0f0f0' };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.backgroundColor).toBe('#f0f0f0');
  });

  test('guides.previewOnly is always true', () => {
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], DEFAULT_PAGE_SETTINGS);
    expect((out.pageSettings as Record<string, unknown>).guides).toEqual({ previewOnly: true });
  });

  // J: Test save/load round-trip — exportDefaults in payload
  test('exportDefaults included in payload', () => {
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], DEFAULT_PAGE_SETTINGS);
    expect(out.exportDefaults).toMatchObject({
      quality: 'printer',
      embedFonts: true,
      compressImages: true,
    });
  });

  // J: Test page numbering in export
  test('pageNumbering included when enabled', () => {
    const ps: PageSettings = {
      ...DEFAULT_PAGE_SETTINGS,
      pageNumbering: { ...DEFAULT_PAGE_SETTINGS.pageNumbering, enabled: true, format: 'roman', startNumber: 3 }
    };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.pageNumbering).not.toBeNull();
    expect((out.pageSettings.pageNumbering as PageSettings['pageNumbering']).format).toBe('roman');
    expect((out.pageSettings.pageNumbering as PageSettings['pageNumbering']).startNumber).toBe(3);
  });

  test('pageNumbering is null when disabled', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, pageNumbering: { ...DEFAULT_PAGE_SETTINGS.pageNumbering, enabled: false } };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.pageNumbering).toBeNull();
  });

  // J: Test margin safe-zone — margins are in payload
  test('margins are preserved in export', () => {
    const ps: PageSettings = { ...DEFAULT_PAGE_SETTINGS, margins: { top: 10, right: 20, bottom: 30, left: 40 } };
    const out = ExportService.convertElementsToTemplate([], mockTemplate, [], ps);
    expect(out.pageSettings.margins).toEqual({ top: 10, right: 20, bottom: 30, left: 40 });
  });
});

// J: Test element type mapping
describe('element type mapping', () => {
  const types: Array<[SimpleElement['type'], string]> = [
    ['text', 'Text'],
    ['richtext', 'RichText'],
    ['image', 'Image'],
    ['shape', 'Rectangle'],
    ['rect', 'Rectangle'],
    ['circle', 'Circle'],
    ['line', 'Line'],
    ['table', 'Table'],
    ['qrcode', 'QRCode'],
    ['barcode', 'Barcode'],
    ['pagenumber', 'PageNumber'],
    ['watermark', 'Watermark'],
    ['checkbox', 'Checkbox'],
    ['checkmark', 'CheckMark'],
  ];

  test.each(types)('"%s" maps to "%s"', (uiType, expectedType) => {
    const el = makeEl({ id: `el-${uiType}`, type: uiType });
    const out = ExportService.convertElementsToTemplate([makePage([el])], mockTemplate);
    expect(out.elements[0].type).toBe(expectedType);
  });
});

// J: Test validateForExport
describe('validateForExport', () => {
  test('empty elements array is invalid', () => {
    const result = ExportService.validateForExport([]);
    expect(result.isValid).toBe(false);
    expect(result.errors).toHaveLength(1);
  });

  test('text element with no content is invalid', () => {
    const result = ExportService.validateForExport([makeEl({ content: '' })]);
    expect(result.isValid).toBe(false);
    expect(result.errors[0]).toContain('Text element 1 has no content');
  });

  test('text element with content is valid', () => {
    const result = ExportService.validateForExport([makeEl({ content: 'Hello' })]);
    expect(result.isValid).toBe(true);
  });

  test('qrcode with empty value is invalid', () => {
    const el = makeEl({ type: 'qrcode', qrValue: '' });
    const result = ExportService.validateForExport([el]);
    expect(result.isValid).toBe(false);
    expect(result.errors[0]).toContain('QR Code');
  });

  test('qrcode with value is valid', () => {
    const el = makeEl({ type: 'qrcode', qrValue: 'https://example.com' });
    const result = ExportService.validateForExport([el]);
    expect(result.isValid).toBe(true);
  });

  test('barcode with empty value is invalid', () => {
    const el = makeEl({ type: 'barcode', barcodeValue: '' });
    const result = ExportService.validateForExport([el]);
    expect(result.isValid).toBe(false);
  });

  test('image with empty src is invalid', () => {
    const el = makeEl({ type: 'image', content: '' });
    const result = ExportService.validateForExport([el]);
    expect(result.isValid).toBe(false);
  });

  test('dropdown with no options is invalid', () => {
    const el = makeEl({ type: 'dropdown', options: [] });
    const result = ExportService.validateForExport([el]);
    expect(result.isValid).toBe(false);
  });

  test('date in binding mode with no binding is invalid', () => {
    const el = makeEl({ type: 'date', dateMode: 'binding', binding: '' });
    const result = ExportService.validateForExport([el]);
    expect(result.isValid).toBe(false);
  });

  test('multiple errors are all reported', () => {
    const els = [
      makeEl({ id: 'e1', type: 'text', content: '' }),
      makeEl({ id: 'e2', type: 'qrcode', qrValue: '' }),
    ];
    const result = ExportService.validateForExport(els);
    expect(result.isValid).toBe(false);
    expect(result.errors).toHaveLength(2);
  });
});

describe('image OCR import service', () => {
  const originalFetch = global.fetch;
  const originalDocument = global.document;
  const originalCreateObjectURL = URL.createObjectURL;
  const originalRevokeObjectURL = URL.revokeObjectURL;

  afterEach(() => {
    global.fetch = originalFetch;
    global.document = originalDocument;
    URL.createObjectURL = originalCreateObjectURL;
    URL.revokeObjectURL = originalRevokeObjectURL;
    jest.restoreAllMocks();
  });

  test('posts image OCR import to debug endpoint with options', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        design: { id: 'ocr-design', pages: [] },
        diagnostics: { wordCount: 2 },
        warnings: ['Low confidence OCR'],
        debugOverlay: 'data:image/png;base64,abc',
      }),
    });
    global.fetch = fetchMock as unknown as typeof fetch;

    const file = new File(['png'], 'scan.png', { type: 'image/png' });
    const result = await ExportService.importImageOcr(file, 595, 842, {
      languages: 'eng',
      includeBackgroundImage: false,
      includeDiagnostics: true,
      includeDebugOverlay: true,
      lowConfidenceThreshold: 0.45,
    }) as any;

    expect(fetchMock).toHaveBeenCalledWith('/api/document/convert-image-to-pdf?debug=true', expect.objectContaining({
      method: 'POST',
      body: expect.any(FormData),
    }));

    const form = fetchMock.mock.calls[0][1].body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('languages')).toBe('eng');
    expect(form.get('pageWidthPt')).toBe('595');
    expect(form.get('pageHeightPt')).toBe('842');
    expect(form.get('includeBackgroundImage')).toBe('false');
    expect(form.get('includeDiagnostics')).toBe('true');
    expect(form.get('includeDebugOverlay')).toBe('true');
    expect(form.get('lowConfidenceThreshold')).toBe('0.45');
    expect(result.design.id).toBe('ocr-design');
  });

  test('downloads image OCR PDF from non-debug endpoint', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      blob: async () => new Blob(['%PDF'], { type: 'application/pdf' }),
    });
    global.fetch = fetchMock as unknown as typeof fetch;

    const appendChild = jest.fn((node: Node) => node);
    const removeChild = jest.fn((node: Node) => node);
    const click = jest.fn();
    const anchor = {
      href: '',
      download: '',
      click,
    } as unknown as HTMLAnchorElement;
    global.document = {
      body: { appendChild, removeChild },
      createElement: jest.fn(() => anchor),
    } as unknown as Document;
    URL.createObjectURL = jest.fn(() => 'blob:ocr-pdf');
    URL.revokeObjectURL = jest.fn();

    const file = new File(['png'], 'Invoice Scan.png', { type: 'image/png' });
    await ExportService.downloadImageOcrPdf(file, undefined, undefined, {
      languages: 'deu+eng',
      includeBackgroundImage: true,
      lowConfidenceThreshold: 0.5,
    });

    expect(fetchMock).toHaveBeenCalledWith('/api/document/convert-image-to-pdf', expect.objectContaining({
      method: 'POST',
      body: expect.any(FormData),
    }));
    expect(anchor.href).toBe('blob:ocr-pdf');
    expect(anchor.download).toBe('invoice-scan.pdf');
    expect(appendChild).toHaveBeenCalledWith(anchor);
    expect(click).toHaveBeenCalledTimes(1);
    expect(removeChild).toHaveBeenCalledWith(anchor);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:ocr-pdf');
  });
});
