/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import LiveCodeEditor from '@/components/CodeEditor/LiveCodeEditor';
import type { ParsedDesign } from '@/components/CodeEditor/CodePreviewPane';
import {
  applyCodeDraft,
  convertCodeDraft,
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
    </section>
  ),
}));

jest.mock('@/components/CodeEditor/CodePreviewPane', () => ({
  __esModule: true,
  default: ({ parsed }: { parsed: { name?: string } | null }) => (
    <output data-testid="preview">{parsed?.name ?? 'empty'}</output>
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
const roots: Root[] = [];

async function flush(): Promise<void> {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

async function renderWorkspace(onApply = jest.fn()) {
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
    expect(applyMock).toHaveBeenCalledWith(
      'template-1',
      1,
      4,
      'csharpModel',
      converted.generatedSource,
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
});
