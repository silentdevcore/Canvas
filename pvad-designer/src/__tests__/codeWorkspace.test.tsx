/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import LiveCodeEditor from '@/components/CodeEditor/LiveCodeEditor';
import type { ParsedDesign } from '@/components/CodeEditor/CodePreviewPane';
import {
  applyCodeDraft,
  compareCodeDraft,
  convertCodeDraft,
  executeCodeDraft,
  getCodeWorkspace,
  saveCodeDraft,
} from '@/services/codeWorkspaceApi';

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean })
  .IS_REACT_ACT_ENVIRONMENT = true;

jest.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock('@/components/CodeEditor/JsonEditorPane', () => ({
  __esModule: true,
  default: ({ value, onChange }: { value: string; onChange: (value: string) => void }) => (
    <section>
      <output data-testid="draft">{value}</output>
      <button onClick={() => onChange(JSON.stringify(changedDesign))}>edit draft</button>
      <button onClick={() => onChange(JSON.stringify({ ...changedDesign, name: 'Newer edit' }))}>edit again</button>
    </section>
  ),
}));

jest.mock('@/components/CodeEditor/CodePreviewPane', () => ({
  __esModule: true,
  default: ({ parsed, pdfBlobUrl }: { parsed: { name?: string } | null; pdfBlobUrl?: string | null }) => (
    <output data-testid="preview">{pdfBlobUrl ? 'pdf-ready' : parsed?.name ?? 'empty'}</output>
  ),
}));

jest.mock('@/services/ExportService', () => ({
  ExportService: { exportJsonToPDF: jest.fn() },
}));

jest.mock('@/services/codeWorkspaceApi', () => {
  const actual = jest.requireActual('@/services/codeWorkspaceApi');
  return {
    ...actual,
    getCodeWorkspace: jest.fn(),
    saveCodeDraft: jest.fn(),
    validateCodeDraft: jest.fn(),
    convertCodeDraft: jest.fn(),
    executeCodeDraft: jest.fn(),
    applyCodeDraft: jest.fn(),
    compareCodeDraft: jest.fn(),
  };
});

const initialDesign: ParsedDesign = {
  id: 'design-1',
  name: 'Original',
  pages: [{ id: 'page-1', elements: [{ id: 'title', type: 'text', x: 10, y: 20, width: 100, height: 20 }] }],
};

const changedDesign: ParsedDesign = {
  ...initialDesign,
  name: 'Changed',
  pages: [{ ...initialDesign.pages[0], elements: [...initialDesign.pages[0].elements, { id: 'line', type: 'line', x: 10, y: 50, width: 100, height: 1 }] }],
};

const workspace = (revision = 1) => ({
  id: 'workspace-1',
  templateId: 'template-1',
  revision,
  baseTemplateRevision: 4,
  persisted: true,
  json: { source: JSON.stringify(initialDesign), checksum: 'json-checksum' },
  cSharpModel: { source: '', checksum: '' },
  cSharpPdf: { source: '', checksum: '' },
  cSharpBase64: { source: '', checksum: '' },
  canonicalDesign: initialDesign,
  sourceMap: [],
  canonicalChecksum: 'canonical-checksum',
  updatedAt: '2026-08-16T00:00:00Z',
});

const getMock = getCodeWorkspace as jest.MockedFunction<typeof getCodeWorkspace>;
const saveMock = saveCodeDraft as jest.MockedFunction<typeof saveCodeDraft>;
const convertMock = convertCodeDraft as jest.MockedFunction<typeof convertCodeDraft>;
const applyMock = applyCodeDraft as jest.MockedFunction<typeof applyCodeDraft>;
const compareMock = compareCodeDraft as jest.MockedFunction<typeof compareCodeDraft>;
const executeMock = executeCodeDraft as jest.MockedFunction<typeof executeCodeDraft>;
const roots: Root[] = [];

async function flush(): Promise<void> {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

async function renderWorkspace(onApply = jest.fn(), onBeforeCompare = jest.fn().mockResolvedValue(undefined)) {
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);
  roots.push(root);
  await act(async () => root.render(
    <LiveCodeEditor
      onBack={jest.fn()}
      templateId="template-1"
      templateRevision={4}
      initialDesign={initialDesign}
      onApply={onApply}
      onBeforeCompare={onBeforeCompare}
    />,
  ));
  await flush();
  return { container, onApply };
}

function button(container: HTMLElement, text: string): HTMLButtonElement {
  const result = [...container.querySelectorAll('button')]
    .find(candidate => candidate.textContent?.includes(text));
  if (!result) throw new Error(`Button '${text}' was not found.`);
  return result;
}

describe('Code workspace', () => {
  beforeEach(() => {
    jest.useFakeTimers();
    jest.clearAllMocks();
    getMock.mockResolvedValue(workspace());
    saveMock.mockResolvedValue(workspace(2));
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
      configurable: true, value: function (this: HTMLDialogElement) { this.open = true; },
    });
  });

  afterEach(() => {
    roots.splice(0).forEach(root => act(() => root.unmount()));
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
    document.body.replaceChildren();
  });

  test('autosaves only after the two-second editing pause', async () => {
    const { container } = await renderWorkspace();

    act(() => button(container, 'edit draft').click());
    act(() => jest.advanceTimersByTime(1999));
    expect(saveMock).not.toHaveBeenCalled();

    act(() => jest.advanceTimersByTime(1));
    await flush();

    expect(saveMock).toHaveBeenCalledWith(
      'template-1',
      1,
      'json',
      JSON.stringify(changedDesign),
    );
    expect(container.textContent).toContain('status.saved');
  });

  test('reviews a conversion before applying it and can restore the JSON draft', async () => {
    const converted = {
      sourceLanguage: 'json' as const,
      targetLanguage: 'csharpModel' as const,
      fidelity: 'exact' as const,
      documentFidelity: 'exact' as const,
      sourcePreservation: 'regenerated' as const,
      generatedSource: 'return DesignExportDtoFromGeneratedCode();',
      canonicalDesign: changedDesign,
      diagnostics: [],
      sourceMap: [],
      sourceChecksum: 'source',
      resultChecksum: 'result',
      canonicalChecksum: 'canonical',
    };
    convertMock.mockResolvedValue(converted);
    compareMock.mockResolvedValue({ templateRevision: 6, workspaceRevision: 1, baseTemplateRevision: 4,
      templateDesign: initialDesign, conversion: converted });
    applyMock.mockResolvedValue({
      templateRevision: 5,
      workspaceRevision: 2,
      conversion: converted,
    });
    const { container, onApply } = await renderWorkspace();

    await act(async () => button(container, 'actions.convert').click());
    await flush();
    expect(container.querySelector('[role="dialog"]')?.textContent).toContain('workspace.added');
    expect(container.querySelector('[role="dialog"]')?.textContent).toContain('1');

    act(() => button(container, 'actions.accept').click());
    expect(container.querySelector('[data-testid="draft"]')?.textContent).toContain('DesignExportDtoFromGeneratedCode');

    await act(async () => button(container, 'actions.apply').click());
    await flush();
    expect(applyMock).not.toHaveBeenCalled();
    expect(container.querySelector('dialog')?.textContent).toContain('applyReview.staleBase');
    await act(async () => button(container, 'applyReview.confirm').click());
    expect(applyMock).toHaveBeenCalledWith(
      'template-1',
      1,
      6,
      'csharpModel',
      converted.generatedSource,
      'canonical',
    );
    expect(onApply).toHaveBeenCalledWith(changedDesign, 5);

    act(() => button(container, 'lang.json').click());
    act(() => button(container, 'edit draft').click());
    expect(container.querySelector('[data-testid="draft"]')?.textContent).toContain('Changed');
    act(() => button(container, 'actions.restore').click());
    expect(container.querySelector('[data-testid="draft"]')?.textContent).toContain('Changed');
  });

  test('shows independent JSON, model, PDF, and FromBase64String tabs', async () => {
    const { container } = await renderWorkspace();

    expect(container.textContent).toContain('lang.json');
    expect(container.textContent).toContain('lang.csharpDto');
    expect(container.textContent).toContain('lang.csharpCode');
    expect(container.textContent).toContain('lang.csharpBase64');
  });

  const comparison = () => ({
    templateRevision: 8, workspaceRevision: 1, baseTemplateRevision: 4,
    templateDesign: changedDesign,
    conversion: {
      sourceLanguage: 'json' as const, targetLanguage: 'json' as const,
      fidelity: 'exact' as const, documentFidelity: 'exact' as const, sourcePreservation: 'preserved' as const,
      generatedSource: JSON.stringify(initialDesign), canonicalDesign: initialDesign,
      diagnostics: [], sourceMap: [], sourceChecksum: 'source', resultChecksum: 'result', canonicalChecksum: 'reviewed',
    },
  });

  test('cancelling a stale-base comparison preserves all drafts and performs no mutation', async () => {
    compareMock.mockResolvedValue(comparison());
    getMock.mockResolvedValue({ ...workspace(), cSharpModel: { source: '// my draft', checksum: 'model' } });
    const { container, onApply } = await renderWorkspace();
    act(() => button(container, 'edit draft').click());
    await act(async () => button(container, 'actions.apply').click());
    expect(container.querySelector('dialog')?.textContent).toContain('applyReview.currentDesigner');
    act(() => button(container, 'actions.cancel').click());
    expect(container.querySelector('[data-testid="draft"]')?.textContent).toBe(JSON.stringify(changedDesign));
    act(() => button(container, 'lang.csharpDto').click());
    expect(container.querySelector('[data-testid="draft"]')?.textContent).toBe('// my draft');
    expect(applyMock).not.toHaveBeenCalled();
    expect(onApply).not.toHaveBeenCalled();
    expect(saveMock).not.toHaveBeenCalled();
  });

  test('preserves drafts and demands a new comparison after a competing server edit', async () => {
    compareMock.mockResolvedValue(comparison());
    applyMock.mockRejectedValue(Object.assign(new Error('Compare again.'), { status: 409 }));
    const { container, onApply } = await renderWorkspace();
    await act(async () => button(container, 'actions.apply').click());
    await act(async () => button(container, 'applyReview.confirm').click());
    expect(container.querySelector('dialog')).toBeNull();
    expect(container.textContent).toContain('status.conflict');
    expect(container.querySelector('[data-testid="draft"]')?.textContent).toBe(JSON.stringify(initialDesign));
    expect(onApply).not.toHaveBeenCalled();
    await act(async () => button(container, 'actions.apply').click());
    expect(compareMock).toHaveBeenCalledTimes(2);
  });

  test('waits for visual autosave before comparison and stops if it fails', async () => {
    const flush = jest.fn().mockRejectedValue(new Error('Unsaved visual conflict.'));
    const { container } = await renderWorkspace(jest.fn(), flush);
    await act(async () => button(container, 'actions.apply').click());
    expect(flush).toHaveBeenCalledTimes(1);
    expect(compareMock).not.toHaveBeenCalled();
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('applyReview.saveRequired');
  });

  test('shows generated-code preview only for the exact source that produced it', async () => {
    getMock.mockResolvedValue({
      ...workspace(),
      cSharpModel: { source: 'return ExistingModel();', checksum: 'model-checksum' },
    });
    executeMock.mockResolvedValue({
      success: true,
      canonicalDesign: changedDesign,
      diagnostics: [],
      sourceMap: [],
      fidelity: 'exact',
    });
    const { container } = await renderWorkspace();

    act(() => button(container, 'lang.csharpDto').click());
    expect(container.querySelector('[data-testid="preview"]')?.textContent).toBe('empty');

    await act(async () => button(container, 'actions.run').click());
    await flush();
    expect(container.querySelector('[data-testid="preview"]')?.textContent).toBe('Changed');

    act(() => button(container, 'edit draft').click());
    expect(container.querySelector('[data-testid="preview"]')?.textContent).toBe('empty');
  });

  test('keeps a generated PDF across tab switches and invalidates it after editing its source', async () => {
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: jest.fn(() => 'blob:pvad-code') });
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: jest.fn() });
    getMock.mockResolvedValue({
      ...workspace(),
      cSharpPdf: { source: 'return ExistingPdf();', checksum: 'pdf-checksum' },
    });
    executeMock.mockResolvedValue({
      success: true,
      canonicalDesign: changedDesign,
      pdfBytes: btoa('%PDF-1.7'),
      diagnostics: [],
      sourceMap: [],
      fidelity: 'exact',
    });
    const { container } = await renderWorkspace();

    act(() => button(container, 'lang.csharpCode').click());
    await act(async () => button(container, 'actions.run').click());
    await flush();
    expect(container.querySelector('[data-testid="preview"]')?.textContent).toBe('pdf-ready');

    let downloadName = '';
    const download = jest.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      downloadName = this.download;
    });
    try {
      await act(async () => button(container, 'exportPdf').click());
      expect(download).toHaveBeenCalledTimes(1);
      expect(downloadName).toBe('pvad-code.pdf');
    } finally {
      download.mockRestore();
    }

    act(() => button(container, 'lang.json').click());
    act(() => button(container, 'lang.csharpCode').click());
    expect(container.querySelector('[data-testid="preview"]')?.textContent).toBe('pdf-ready');

    act(() => button(container, 'edit draft').click());
    expect(container.querySelector('[data-testid="preview"]')?.textContent).toBe('empty');
  });

  test('shows a conflict when another workspace revision wins', async () => {
    const conflict = Object.assign(new Error('Workspace revision is stale.'), { status: 409 });
    saveMock.mockRejectedValue(conflict);
    const { container } = await renderWorkspace();

    act(() => button(container, 'edit draft').click());
    act(() => jest.advanceTimersByTime(2000));
    await flush();

    expect(container.textContent).toContain('status.conflict');
    expect(container.querySelector('[role="alert"]')?.textContent).toContain('Workspace revision is stale.');
  });

  test('does not mark a newer edit saved when an older request completes', async () => {
    let finish!: (value: ReturnType<typeof workspace>) => void;
    saveMock.mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
    const { container } = await renderWorkspace();
    act(() => button(container, 'edit draft').click());
    act(() => jest.advanceTimersByTime(2000));
    act(() => button(container, 'edit again').click());
    await act(async () => finish(workspace(2)));
    expect(container.querySelector('[data-testid="draft"]')?.textContent).toContain('Newer edit');
    expect(container.textContent).toContain('status.modified');
    act(() => jest.advanceTimersByTime(2000));
    await flush();
    expect(saveMock.mock.calls[1]).toEqual(['template-1', 2, 'json', JSON.stringify({ ...changedDesign, name: 'Newer edit' })]);
  });
});
