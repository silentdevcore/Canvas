/** @jest-environment jsdom */
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import {
  useDesignerTemplateAutosave,
  type AutosaveResult,
} from '@/hooks/useDesignerTemplateAutosave';
import {
  createDesignerTemplate,
  type DesignerTemplateDocument,
  DesignerTemplateApiError,
  updateDesignerTemplateDraft,
} from '@/services/designerTemplateApi';
import { DEFAULT_PAGE_SETTINGS, useEditorStore, type Template } from '@/store';

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean })
  .IS_REACT_ACT_ENVIRONMENT = true;

jest.mock('@/services/designerTemplateApi', () => {
  const actual = jest.requireActual('@/services/designerTemplateApi');
  return {
    ...actual,
    createDesignerTemplate: jest.fn(),
    getDesignerTemplate: jest.fn(),
    updateDesignerTemplateDraft: jest.fn(),
  };
});

const createMock = createDesignerTemplate as jest.MockedFunction<typeof createDesignerTemplate>;
const updateMock = updateDesignerTemplateDraft as jest.MockedFunction<typeof updateDesignerTemplateDraft>;
const mountedRoots: Root[] = [];

function renderAutosave(): { readonly current: AutosaveResult } {
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);
  mountedRoots.push(root);
  let current!: AutosaveResult;
  function Harness() {
    current = useDesignerTemplateAutosave();
    return null;
  }
  act(() => root.render(<Harness />));
  return { get current() { return current; } };
}

function template(id: string, revision?: number): Template {
  return {
    id,
    name: `Template ${id}`,
    category: 'test',
    description: '',
    pages: [{ id: 'page-1', elements: [] }],
    sharedElements: [],
    data: {},
    ...(revision
      ? { persistence: { id: `server-${id}`, revision, status: 'Draft' } }
      : {}),
  };
}

function serverResult(source: Template, revision: number): DesignerTemplateDocument {
  return {
    id: source.persistence?.id ?? `server-${source.id}`,
    name: source.name,
    description: source.description ?? null,
    tags: [],
    status: 'Draft',
    revision,
    designDocument: {
      template: source as unknown as Record<string, unknown>,
      pageSettings: DEFAULT_PAGE_SETTINGS,
      jsonData: {},
      documentMode: 'pdf' as const,
      currentPageIndex: 0,
    },
    checksum: 'checksum',
    schemaVersion: '1.0',
    designerVersion: '1.0.0',
    publishedVersionId: null,
    updatedAt: new Date().toISOString(),
  };
}

function resetStore(source: Template): void {
  useEditorStore.setState({
    currentTemplate: source,
    currentPageIndex: 0,
    pageSettings: DEFAULT_PAGE_SETTINGS,
    jsonData: {},
    documentMode: 'pdf',
    undoStack: [],
    redoStack: [],
  });
}

async function flushPromises(): Promise<void> {
  await act(async () => {
    await Promise.resolve();
    await Promise.resolve();
  });
}

describe('Designer template autosave', () => {
  beforeEach(() => {
    jest.useFakeTimers();
    jest.clearAllMocks();
  });

  afterEach(() => {
    mountedRoots.splice(0).forEach(root => act(() => root.unmount()));
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
    document.body.replaceChildren();
  });

  test('treats each loaded persisted template as its own saved baseline', () => {
    resetStore(template('first', 3));
    renderAutosave();

    act(() => resetStore(template('second', 7)));
    act(() => jest.advanceTimersByTime(2500));

    expect(updateMock).not.toHaveBeenCalled();
    expect(createMock).not.toHaveBeenCalled();
  });

  test('coalesces edits while one save request is in flight', async () => {
    const source = template('coalesced', 1);
    resetStore(source);
    let resolveFirst!: (value: ReturnType<typeof serverResult>) => void;
    updateMock
      .mockImplementationOnce(() => new Promise(resolve => { resolveFirst = resolve; }))
      .mockResolvedValueOnce(serverResult(source, 3));
    renderAutosave();

    act(() => useEditorStore.setState({
      currentTemplate: { ...useEditorStore.getState().currentTemplate!, name: 'First edit' },
    }));
    act(() => jest.advanceTimersByTime(2000));
    expect(updateMock).toHaveBeenCalledTimes(1);

    act(() => useEditorStore.setState({
      currentTemplate: { ...useEditorStore.getState().currentTemplate!, name: 'Second edit' },
    }));
    act(() => jest.advanceTimersByTime(2000));
    expect(updateMock).toHaveBeenCalledTimes(1);

    await act(async () => {
      resolveFirst(serverResult(source, 2));
      await Promise.resolve();
    });
    act(() => jest.advanceTimersByTime(2000));
    await flushPromises();

    expect(updateMock).toHaveBeenCalledTimes(2);
    expect(updateMock.mock.calls[1][1]).toBe(2);
  });

  test('never applies an old in-flight save result to a newly opened template', async () => {
    const first = template('first-in-flight', 1);
    const second = template('second-in-flight', 8);
    resetStore(first);
    let resolveSave!: (value: DesignerTemplateDocument) => void;
    updateMock.mockImplementationOnce(() => new Promise(resolve => { resolveSave = resolve; }));
    renderAutosave();

    act(() => useEditorStore.setState({
      currentTemplate: { ...useEditorStore.getState().currentTemplate!, name: 'Changed first' },
    }));
    act(() => jest.advanceTimersByTime(2000));
    expect(updateMock).toHaveBeenCalledTimes(1);

    act(() => resetStore(second));
    await act(async () => {
      resolveSave(serverResult(first, 2));
      await Promise.resolve();
    });

    expect(useEditorStore.getState().currentTemplate?.persistence).toEqual(second.persistence);
    expect(useEditorStore.getState().currentTemplate?.id).toBe(second.id);
  });

  test('stops automatic retries after authorization is lost', async () => {
    resetStore(template('forbidden', 1));
    const denied = new DesignerTemplateApiError('Designer access denied.');
    denied.status = 403;
    updateMock.mockRejectedValue(denied);
    const result = renderAutosave();

    act(() => useEditorStore.setState({
      currentTemplate: { ...useEditorStore.getState().currentTemplate!, name: 'Denied edit' },
    }));
    act(() => jest.advanceTimersByTime(2000));
    await flushPromises();
    expect(result.current.state).toBe('failed');

    act(() => useEditorStore.setState({
      currentTemplate: { ...useEditorStore.getState().currentTemplate!, description: 'Still denied' },
    }));
    act(() => jest.advanceTimersByTime(2500));
    await flushPromises();

    expect(updateMock).toHaveBeenCalledTimes(1);
  });

  test('retries a transient server failure with bounded backoff', async () => {
    const source = template('retry', 1);
    resetStore(source);
    const unavailable = new DesignerTemplateApiError('Temporarily unavailable.');
    unavailable.status = 503;
    updateMock
      .mockRejectedValueOnce(unavailable)
      .mockResolvedValueOnce(serverResult(source, 2));
    const result = renderAutosave();

    act(() => useEditorStore.setState({
      currentTemplate: { ...useEditorStore.getState().currentTemplate!, name: 'Retry edit' },
    }));
    act(() => jest.advanceTimersByTime(2000));
    await flushPromises();
    expect(result.current.state).toBe('retrying');

    act(() => jest.advanceTimersByTime(500));
    await flushPromises();

    expect(updateMock).toHaveBeenCalledTimes(2);
    expect(result.current.state).toBe('saved');
  });

  test('keeps local conflict changes and can save them as a new template', async () => {
    const source = template('conflict', 4);
    resetStore(source);
    const conflict = new DesignerTemplateApiError('A newer server draft exists.');
    conflict.status = 409;
    conflict.currentRevision = 5;
    updateMock.mockRejectedValueOnce(conflict);
    const savedAsNew = { ...serverResult(source, 1), id: 'server-conflict-copy' };
    createMock.mockResolvedValueOnce(savedAsNew);
    const result = renderAutosave();

    act(() => useEditorStore.setState({
      currentTemplate: { ...useEditorStore.getState().currentTemplate!, name: 'Local conflict work' },
    }));
    act(() => jest.advanceTimersByTime(2000));
    await flushPromises();

    expect(result.current.state).toBe('conflict');
    expect(useEditorStore.getState().currentTemplate?.name).toBe('Local conflict work');

    await act(async () => result.current.saveAsNew());

    expect(createMock).toHaveBeenCalledTimes(1);
    expect(useEditorStore.getState().currentTemplate?.persistence?.id).toBe('server-conflict-copy');
  });

  test('prevents browser unload while local changes are unsaved', () => {
    resetStore(template('unload', 1));
    renderAutosave();
    act(() => useEditorStore.setState({
      currentTemplate: { ...useEditorStore.getState().currentTemplate!, name: 'Unsaved edit' },
    }));

    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
  });
});
