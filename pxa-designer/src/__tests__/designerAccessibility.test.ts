import {
  accessibilityForButtonAction,
  applySuggestedAccessibility,
  suggestedAccessibility,
  validateDesignerAccessibility,
  withRepeatedPageArtifactAccessibility,
  withSuggestedAccessibility,
} from '@/accessibility/designerAccessibility';
import { DesignerAccessibilityPreflightError, ExportService } from '@/services/ExportService';
import type { Page, PageSettings, SimpleElement, Template } from '@/types';
import { ELEMENT_TYPES } from '@/data/elementRegistry';

const element = (overrides: Partial<SimpleElement> = {}): SimpleElement => ({
  id: 'element-1',
  type: 'text',
  x: 10,
  y: 10,
  width: 100,
  height: 30,
  content: 'Accessible text',
  ...overrides,
});

const template = { id: 'template-1', name: 'Accessible document' } as Template;
const settings = {
  accessibilityProfile: 'PdfUa1',
  systemLanguage: 'en-US',
  metadata: { title: '', author: '', subject: '', keywords: '' },
} as PageSettings;

describe('Designer accessibility', () => {
  test('suggests semantic roles and artifacts without replacing explicit choices', () => {
    expect(suggestedAccessibility(element({ headingLevel: 2 })).role).toBe('H2');
    expect(suggestedAccessibility(element({ type: 'image' })).role).toBe('Figure');
    expect(suggestedAccessibility(element({ type: 'field' })).role).toBe('Form');
    expect(suggestedAccessibility(element({ type: 'line' })).isArtifact).toBe(true);

    const explicit = element({ accessibility: { role: 'Sect' } });
    expect(withSuggestedAccessibility(explicit).accessibility).toEqual({ role: 'Sect' });
  });

  test('marks repeated header and footer content as artifacts', () => {
    const result = withRepeatedPageArtifactAccessibility(element({
      accessibility: { role: 'P', alternateText: 'Repeated header' },
    }));

    expect(result.accessibility).toEqual({ role: 'Span', isArtifact: true });
  });

  test('defines an intentional accessibility default for every registered element', () => {
    const expected: Record<(typeof ELEMENT_TYPES)[number], { role: string; artifact?: true }> = {
      text: { role: 'P' }, richtext: { role: 'P' }, date: { role: 'P' }, number: { role: 'P' },
      field: { role: 'Form' }, textarea: { role: 'Form' }, checkbox: { role: 'Form' },
      radio: { role: 'Form' }, optionlist: { role: 'Form' }, dropdown: { role: 'Form' },
      button: { role: 'Span' }, signature: { role: 'Form' }, table: { role: 'Table' },
      chart: { role: 'Figure' }, image: { role: 'Figure' }, qrcode: { role: 'Figure' },
      barcode: { role: 'Figure' }, shape: { role: 'Span', artifact: true },
      rect: { role: 'Span', artifact: true }, circle: { role: 'Span', artifact: true },
      line: { role: 'Span', artifact: true }, arrow: { role: 'Span', artifact: true },
      draw: { role: 'Span', artifact: true }, area: { role: 'Div' }, subsection: { role: 'Sect' },
      highlight: { role: 'Span', artifact: true }, checkmark: { role: 'Span', artifact: true },
      link: { role: 'Link' }, note: { role: 'P' }, watermark: { role: 'Span', artifact: true },
      pagenumber: { role: 'Span', artifact: true }, pageboundary: { role: 'Span', artifact: true },
      toc: { role: 'L' }, footnote: { role: 'P' }, endnote: { role: 'P' },
      bookmark: { role: 'Span', artifact: true }, comment: { role: 'P' }, contentcontrol: { role: 'Div' },
    };

    expect(Object.keys(expected).sort()).toEqual([...ELEMENT_TYPES].sort());
    for (const type of ELEMENT_TYPES) {
      const accessibility = suggestedAccessibility(element({ type }));
      expect(accessibility.role).toBe(expected[type].role);
      expect(accessibility.isArtifact === true).toBe(expected[type].artifact === true);
    }
  });

  test('applies stable reading order across pages and shared elements', () => {
    const result = applySuggestedAccessibility([
      { id: 'page-1', elements: [element(), element({ id: 'image-1', type: 'image' })] },
    ], [element({ id: 'footer', type: 'pagenumber' })]);

    expect(result.pages[0].elements[0].accessibility).toMatchObject({ role: 'P', readingOrder: 0 });
    expect(result.pages[0].elements[1].accessibility).toMatchObject({ role: 'Figure', readingOrder: 1 });
    expect(result.sharedElements[0].accessibility).toMatchObject({ isArtifact: true });
  });

  test('keeps automatically managed button semantics aligned with its action', () => {
    const button = element({ type: 'button', accessibility: { role: 'Span' } });
    expect(accessibilityForButtonAction(button, 'https://example.test')).toMatchObject({ role: 'Link' });
    expect(accessibilityForButtonAction(button, 'page:2')).toMatchObject({ role: 'Link' });
    expect(accessibilityForButtonAction(button, 'submit')).toMatchObject({ role: 'Form' });
    expect(accessibilityForButtonAction(button, '')).toMatchObject({ role: 'Span' });

    const explicit = element({ type: 'button', accessibility: { role: 'Sect' } });
    expect(accessibilityForButtonAction(explicit, 'https://example.test')).toEqual({ role: 'Sect' });
  });

  test('reports actionable document and element errors only for PDF/UA', () => {
    const pages: Page[] = [{
      id: 'page-1',
      elements: [
        element({ id: 'image', type: 'image', accessibility: { role: 'Figure' } }),
        element({ id: 'field', type: 'field', content: '', fieldLabel: '', accessibility: { role: 'Form' } }),
        element({ id: 'table', type: 'table', accessibility: { role: 'Table' }, headerRow: false }),
      ],
    }];

    expect(validateDesignerAccessibility(template, pages, [], { ...settings, accessibilityProfile: 'None' })).toEqual([]);
    const codes = validateDesignerAccessibility(template, pages, [], settings).map(issue => issue.code);
    expect(codes).toEqual(expect.arrayContaining([
      'PXAPDFUA007', 'PXAPDFUA018', 'PXAPDFUA019', 'PXAPDFUA020',
    ]));
  });

  test('accepts supported widgets, links, and header tables while blocking unsupported controls', () => {
    const pages: Page[] = [{
      id: 'page-1',
      elements: [
        element({ id: 'field', type: 'field', fieldLabel: 'Name', accessibility: { role: 'Form', readingOrder: 0 } }),
        element({ id: 'link', type: 'link', content: 'Help', href: 'https://example.test', accessibility: { role: 'Link', readingOrder: 1 } }),
        element({ id: 'table', type: 'table', headerRow: true, cellData: [['Name'], ['Ada']], accessibility: { role: 'Table' } }),
      ],
    }];

    expect(validateDesignerAccessibility(template, pages, [], settings)).toEqual([]);

    pages[0].elements.push(element({
      id: 'radio', type: 'radio', fieldLabel: 'Choice', accessibility: { role: 'Form' },
    }));
    expect(validateDesignerAccessibility(template, pages, [], settings))
      .toContainEqual(expect.objectContaining({ code: 'PXAPDFUA010', elementId: 'radio' }));
  });

  test('accepts a tagged text document with a valid language and title fallback', () => {
    const pages: Page[] = [{
      id: 'page-1',
      elements: [element({ accessibility: { role: 'P', readingOrder: 0 } })],
    }];

    expect(validateDesignerAccessibility(template, pages, [], settings)).toEqual([]);
  });

  test('accepts native advanced widgets and reports focus-order problems', () => {
    const pages: Page[] = [{
      id: 'page-1',
      elements: [
        element({ id: 'radio', type: 'radio', options: ['Yes', 'No'], accessibility: { role: 'Form', readingOrder: 2 } }),
        element({ id: 'list', type: 'optionlist', options: ['One', 'Two'], accessibility: { role: 'Form', readingOrder: 2 } }),
        element({ id: 'signature', type: 'signature', signatureLabel: 'Sign here', accessibility: { role: 'Form' } }),
        element({ id: 'reset', type: 'button', content: 'Reset', buttonAction: 'reset', accessibility: { role: 'Form', readingOrder: 3 } }),
      ],
    }];

    const issues = validateDesignerAccessibility(template, pages, [], settings);
    expect(issues).not.toContainEqual(expect.objectContaining({ code: 'PXAPDFUA010' }));
    expect(issues).toContainEqual(expect.objectContaining({ code: 'PXAPDFUA023', elementId: 'signature', severity: 'warning' }));
    expect(issues.filter(issue => issue.code === 'PXAPDFUA024')).toHaveLength(2);
  });

  test('blocks PDF rendering before a network request when preflight fails', async () => {
    const fetchMock = jest.spyOn(globalThis, 'fetch');
    const pages: Page[] = [{ id: 'page-1', elements: [element()] }];

    await expect(ExportService.renderDesignPdfBlob(template, pages, [], settings))
      .rejects.toBeInstanceOf(DesignerAccessibilityPreflightError);
    expect(fetchMock).not.toHaveBeenCalled();
    fetchMock.mockRestore();
  });
});
