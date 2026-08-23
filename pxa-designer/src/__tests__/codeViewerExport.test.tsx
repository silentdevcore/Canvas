/**
 * @jest-environment jsdom
 */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import CodeViewer from '@/components/Editor/CodeViewer';
import { convertCodeDraft } from '@/services/codeWorkspaceApi';
import { DEFAULT_PAGE_SETTINGS, type Template } from '@/store';

const convertMock = convertCodeDraft as jest.MockedFunction<typeof convertCodeDraft>;

jest.mock('@/services/codeWorkspaceApi', () => ({
  convertCodeDraft: jest.fn(),
}));

const waitUntil = async (assertion: () => void) => {
  let failure: unknown;
  for (let attempt = 0; attempt < 30; attempt++) {
    try {
      assertion();
      return;
    } catch (error) {
      failure = error;
      await act(async () => { await new Promise(resolve => setTimeout(resolve, 0)); });
    }
  }
  throw failure;
};

describe('CodeViewer canonical export', () => {
  let container: HTMLDivElement;
  let root: Root;

  const template: Template = {
    id: 'local-template',
    name: 'Mixed layout',
    category: 'Report',
    description: 'Canonical export fixture',
    pages: [],
    sharedElements: [],
    data: {},
    persistence: { id: '32cc0082-bd99-42fe-8795-6aa830355bc0', revision: 4, status: 'Draft' },
  };

  const pages: Template['pages'] = [{
    id: 'page-landscape',
    layout: {
      width: 842,
      height: 595,
      orientation: 'landscape',
      margins: { top: 12, right: 24, bottom: 36, left: 48 },
      backgroundColor: '#fef3c7',
      bleedSize: 5,
    },
    elements: [{
      id: 'german-text',
      type: 'text',
      x: 20,
      y: 30,
      width: 200,
      height: 40,
      content: 'Nur Deutsch',
      elementLanguage: 'de',
      binding: 'customer.name',
    }],
  }];

  beforeEach(() => {
    convertMock.mockReset();
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: jest.fn().mockResolvedValue(undefined) },
    });
  });

  afterEach(async () => {
    await act(async () => root.unmount());
    container.remove();
  });

  test('exports the complete canonical JSON without filtering another language', async () => {
    const pageSettings = {
      ...DEFAULT_PAGE_SETTINGS,
      metadata: { title: 'Quarterly report', author: 'PXA', subject: 'Parity', keywords: 'export' },
      activeLanguages: ['en', 'de'],
      targetLanguage: 'en',
      encryption: {
        enabled: true,
        userPassword: 'reader',
        ownerPassword: 'owner',
        algorithm: 'Aes128' as const,
        permissions: {
          print: true,
          modify: false,
          copy: false,
          annotate: false,
          fillForms: true,
          extractAccessibility: true,
          assemble: false,
          printHighResolution: true,
        },
      },
    };

    await act(async () => root.render(
      <CodeViewer
        isOpen
        onClose={jest.fn()}
        template={template}
        pages={pages}
        sharedElements={[{ id: 'shared', type: 'text', x: 10, y: 10, width: 100, height: 20, content: 'Header' }]}
        pageSettings={pageSettings}
        currentPreviewLanguage="en"
      />,
    ));

    const payload = JSON.parse(container.querySelector('.code-panel-pre code')?.textContent ?? '{}');
    expect(payload.pageSettings.metadata.title).toBe('Quarterly report');
    expect(payload.pageSettings.encryption.algorithm).toBe('Aes128');
    expect(payload.pageSettings.activeLanguages).toEqual(['en', 'de']);
    expect(payload.pages[0].layout).toMatchObject({ width: 842, height: 595, orientation: 'landscape', bleedSize: 5 });
    expect(payload.pages[0].elements[0]).toMatchObject({ id: 'german-text', elementLanguage: 'de', binding: 'customer.name' });
    expect(payload.sharedElements[0].id).toBe('shared');
  });

  test('generates C# PDF from the exact canonical JSON through the backend converter', async () => {
    convertMock.mockResolvedValue({
      sourceLanguage: 'json',
      targetLanguage: 'csharpPdf',
      fidelity: 'exact',
      documentFidelity: 'exact',
      sourcePreservation: 'regenerated',
      generatedSource: 'PxaPdfCodeBuilder.CreateDocument("Mixed layout");',
      canonicalDesign: {},
      diagnostics: [],
      sourceMap: [],
      sourceChecksum: 'source',
      resultChecksum: 'result',
      canonicalChecksum: 'canonical',
    });

    await act(async () => root.render(
      <CodeViewer
        isOpen
        onClose={jest.fn()}
        template={template}
        pages={pages}
        sharedElements={[]}
        pageSettings={DEFAULT_PAGE_SETTINGS}
      />,
    ));

    const csharpTab = [...container.querySelectorAll('button')]
      .find(button => button.textContent === 'C# Code');
    await act(async () => csharpTab?.click());

    await waitUntil(() => expect(convertMock).toHaveBeenCalledTimes(1));
    expect(convertMock.mock.calls[0][0]).toBe(template.persistence?.id);
    expect(convertMock.mock.calls[0][1]).toBe('json');
    expect(convertMock.mock.calls[0][2]).toBe('csharpPdf');
    const submitted = JSON.parse(convertMock.mock.calls[0][3]);
    expect(submitted.pages[0].layout.orientation).toBe('landscape');
    expect(submitted.pages[0].elements[0].binding).toBe('customer.name');

    await waitUntil(() => {
      expect(container.querySelector('.code-panel-pre code')?.textContent)
        .toContain('PxaPdfCodeBuilder.CreateDocument');
    });
  });

  test('does not present lossy local C# when the template has not been persisted', async () => {
    await act(async () => root.render(
      <CodeViewer
        isOpen
        onClose={jest.fn()}
        template={{ ...template, persistence: undefined }}
        pages={pages}
        sharedElements={[]}
        pageSettings={DEFAULT_PAGE_SETTINGS}
      />,
    ));
    const csharpTab = [...container.querySelectorAll('button')]
      .find(button => button.textContent === 'C# Code');
    await act(async () => csharpTab?.click());

    expect(container.textContent).toContain('Save the document before generating exact C# PDF code.');
    expect(convertMock).not.toHaveBeenCalled();
  });
});
