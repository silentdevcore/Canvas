/** @jest-environment jsdom */
import { DEFAULT_PAGE_SETTINGS, useEditorStore, type Template } from '@/store';
import { effectivePageSettings } from '@/utils/pageLayout';

function createTemplate(): Template {
  return {
    id: 'mixed',
    name: 'Mixed layout',
    category: 'test',
    description: '',
    pages: [
      { id: 'portrait', elements: [] },
      { id: 'landscape', elements: [], layout: { width: 842, height: 595, orientation: 'landscape' } },
    ],
    sharedElements: [],
    data: {},
  };
}

describe('per-page layout', () => {
  beforeEach(() => {
    useEditorStore.setState({
      currentTemplate: createTemplate(),
      currentPageIndex: 0,
      pageSettings: { ...DEFAULT_PAGE_SETTINGS },
      undoStack: [],
      redoStack: [],
    });
  });

  it('resolves inherited and overridden page geometry', () => {
    const state = useEditorStore.getState();
    const pages = state.currentTemplate!.pages;

    expect(effectivePageSettings(state.pageSettings, pages[0])).toMatchObject({ width: 595, height: 842 });
    expect(effectivePageSettings(state.pageSettings, pages[1])).toMatchObject({ width: 842, height: 595 });
  });

  it('creates, resets and applies active-page overrides', () => {
    useEditorStore.getState().updatePageSettings({ width: 400, height: 500 });
    expect(useEditorStore.getState().currentTemplate!.pages[0].layout).toMatchObject({ width: 400, height: 500 });

    useEditorStore.getState().resetActivePageLayout();
    expect(useEditorStore.getState().currentTemplate!.pages[0].layout).toBeUndefined();

    useEditorStore.getState().setCurrentPage(1);
    useEditorStore.getState().applyPageLayoutToAll();
    const state = useEditorStore.getState();
    expect(state.pageSettings).toMatchObject({ width: 842, height: 595, orientation: 'landscape' });
    expect(state.currentTemplate!.pages.every(page => page.layout == null)).toBe(true);
  });

  it('inherits active geometry for new and duplicated pages', () => {
    useEditorStore.getState().setCurrentPage(1);
    useEditorStore.getState().addPage();
    expect(useEditorStore.getState().currentTemplate!.pages[2].layout).toMatchObject({ width: 842, height: 595 });

    useEditorStore.getState().duplicatePage(0);
    expect(useEditorStore.getState().currentTemplate!.pages[1].layout).toMatchObject({ width: 595, height: 842 });
  });

  it('restores page geometry through undo', () => {
    useEditorStore.getState().updatePageSettings({ orientation: 'landscape', width: 842, height: 595 });
    expect(useEditorStore.getState().currentTemplate!.pages[0].layout?.orientation).toBe('landscape');

    useEditorStore.getState().undo();
    expect(useEditorStore.getState().currentTemplate!.pages[0].layout).toBeUndefined();
  });
});
