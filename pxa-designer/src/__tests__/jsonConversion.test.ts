import { jsonToCode } from '../utils/jsonToCode';
import { jsonToCSharp } from '../utils/jsonToCSharp';
import type { ParsedDesign } from '../components/CodeEditor/CodePreviewPane';

const sampleDesign: ParsedDesign = {
  id: 'blank-1779719170271',
  name: 'Untitled document',
  category: 'blank',
  description: '',
  pageSettings: {
    width: 595,
    height: 842,
    orientation: 'portrait',
    unit: 'px',
    margins: {
      top: 48,
      right: 48,
      bottom: 48,
      left: 48,
    },
    systemLanguage: 'de',
    activeLanguages: ['en', 'de', 'ar', 'fa'],
    localizedProperties: [
      {
        key: 'TEST',
        scope: 'global',
        localizedValues: {
          en: 'Test1',
          de: 'Test2',
          ar: 'Test3',
        },
      },
      {
        key: 'TESTT',
        scope: 'global',
        localizedValues: {
          de: 'Ich weis nicht ',
          en: 'I dont know',
          ar: 'Echt? ',
        },
      },
    ],
    targetLanguage: 'de',
  },
  pages: [
    {
      id: 'page-1',
      elements: [
        {
          id: 'text-1779719172002',
          type: 'text',
          x: 96,
          y: 112,
          width: 220,
          height: 56,
          content: '{{TEST}}',
          style: {
            fontSize: 16,
            color: '#111827',
            fontWeight: 'normal',
          },
          name: 'TextBlock1',
        },
        {
          id: 'richtext-1779719173066',
          type: 'richtext',
          x: 96,
          y: 604,
          width: 320,
          height: 148,
          htmlContent: '<p><strong>Rich Text</strong> with <em>formatting</em></p>',
          name: 'RichText1',
          langOverrides: {
            de: {
              x: 58,
              y: 230,
            },
          },
        },
      ],
    },
  ],
  sharedElements: [],
};

describe('jsonToCSharp', () => {
  test('preserves document, language, margins, localized properties, and lang overrides', () => {
    const code = jsonToCSharp(sampleDesign);

    expect(code).toContain('Id = "blank-1779719170271"');
    expect(code).toContain('Category = "blank"');
    expect(code).toContain('Unit = "px"');
    expect(code).toContain('Margins = new MarginsDto');
    expect(code).toContain('Top = 48');
    expect(code).toContain('SystemLanguage = "de"');
    expect(code).toContain('ActiveLanguages = new List<string> { "en", "de", "ar", "fa" }');
    expect(code).toContain('TargetLanguage = "de"');
    expect(code).toContain('Key = "TEST"');
    expect(code).toContain('["de"] = "Test2"');
    expect(code).toContain('Name = "RichText1"');
    expect(code).toContain('LangOverrides = new Dictionary<string, LangOverrideDto>');
    expect(code).toContain('["de"] = new LangOverrideDto');
    expect(code).toContain('X = 58');
    expect(code).toContain('Y = 230');
  });

  test('emits per-cell CellStyles for tables', () => {
    const design: ParsedDesign = {
      id: 't', name: 't', category: 'blank', description: '',
      pageSettings: { width: 595, height: 842, unit: 'px' },
      pages: [{
        id: 'page-1',
        elements: [{
          id: 'table-1', type: 'table', x: 0, y: 0, width: 200, height: 60,
          cellData: [['A', 'B']],
          cellStyles: [{
            row: 0, col: 0,
            backgroundColor: '#FFFF00', textAlign: 'center',
            borderBottom: { color: '#FF0000', width: 2 },
            padding: 6, fontFamily: 'Verdana', fontSize: 12, bold: true, color: '#0000FF',
          }],
        }],
      }],
      sharedElements: [],
    } as unknown as ParsedDesign;

    const code = jsonToCSharp(design);
    expect(code).toContain('CellStyles = new CellStyleDto[]');
    expect(code).toContain('Row = 0, Col = 0');
    expect(code).toContain('BackgroundColor = "#FFFF00"');
    expect(code).toContain('BorderBottom = new() { Color = "#FF0000", Width = 2 }');
    expect(code).toContain('FontFamily = "Verdana"');
    expect(code).toContain('Bold = true');
  });
});

describe('jsonToCode', () => {
  test('renders selected language placeholders and applies lang overrides', () => {
    const code = jsonToCode(sampleDesign);

    expect(code).toContain('var systemLanguage = "de";');
    expect(code).toContain('var targetLanguage = "de";');
    expect(code).toContain('var activeLanguages = new List<string> { "en", "de", "ar", "fa" };');
    expect(code).toContain('["TEST"] = new Dictionary<string, string>');
    expect(code).toContain('["de"] = "Test2"');
    expect(code).toContain('DrawParagraph(Resolve("{{TEST}}")');
    expect(code).toContain('DrawText("Rich Text", x: 58.00, y: 603.36');
  });

  test('filters elements scoped to another language', () => {
    const code = jsonToCode({
      ...sampleDesign,
      pages: [
        {
          id: 'page-1',
          elements: [
            ...sampleDesign.pages[0].elements,
            {
              id: 'english-only',
              type: 'text',
              x: 10,
              y: 10,
              width: 100,
              height: 20,
              content: 'English only',
              elementLanguage: 'en',
            },
          ],
        },
      ],
    });

    expect(code).not.toContain('English only');
  });

  it('emits PDF encryption setup when encryption is enabled', () => {
    const code = jsonToCode({
      ...sampleDesign,
      pageSettings: {
        ...sampleDesign.pageSettings,
        encryption: {
          enabled: true,
          userPassword: 'open-sesame',
          ownerPassword: 'admin',
          algorithm: 'Rc4_128',
          permissions: {
            print: true, modify: false, copy: true, annotate: false,
            fillForms: false, extractAccessibility: false, assemble: false, printHighResolution: false,
          },
        },
      },
    } as any);

    expect(code).toContain('Encryption = new PdfEncryptionOptions');
    expect(code).toContain('UserPassword = "open-sesame"');
    expect(code).toContain('OwnerPassword = "admin"');
    expect(code).toContain('Permissions = PdfPermissions.Print | PdfPermissions.Copy');
    expect(code).toContain('document.Save("output.pdf", saveOptions);');
  });

  it('omits encryption setup when encryption is disabled or absent', () => {
    const code = jsonToCode(sampleDesign);
    expect(code).not.toContain('PdfEncryptionOptions');
  });

  it('uses PdfPermissions.All when every permission is granted', () => {
    const code = jsonToCode({
      ...sampleDesign,
      pageSettings: {
        ...sampleDesign.pageSettings,
        encryption: {
          enabled: true,
          userPassword: 'pw',
          ownerPassword: '',
          algorithm: 'Rc4_128',
          permissions: {
            print: true, modify: true, copy: true, annotate: true,
            fillForms: true, extractAccessibility: true, assemble: true, printHighResolution: true,
          },
        },
      },
    } as any);

    expect(code).toContain('Permissions = PdfPermissions.All');
    expect(code).not.toContain('OwnerPassword');
  });
});
