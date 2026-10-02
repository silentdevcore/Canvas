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

  it('opens a native project as one undoable transaction', () => {
    const imported: Template = {
      ...createTemplate(),
      id: 'imported',
      name: 'Imported project',
      pages: [{ id: 'imported-page', elements: [] }],
    };

    useEditorStore.getState().openProject(imported, {
      ...DEFAULT_PAGE_SETTINGS,
      width: 842,
      height: 595,
      orientation: 'landscape',
    });

    expect(useEditorStore.getState().currentTemplate?.name).toBe('Imported project');
    expect(useEditorStore.getState().undoStack).toHaveLength(1);
    useEditorStore.getState().undo();
    expect(useEditorStore.getState().currentTemplate?.name).toBe('Mixed layout');
    expect(useEditorStore.getState().pageSettings).toMatchObject({ width: 595, height: 842 });
  });

  it('retains editable project content, extensions and target persistence through reopen and undo', () => {
    const previous = useEditorStore.getState().currentTemplate!;
    const imported: Template = {
      id: 'portable-project',
      name: 'Portable project',
      category: 'project',
      description: 'Native format contract',
      pages: [{
        id: 'portable-page',
        layout: { width: 700, height: 500, orientation: 'landscape', backgroundColor: '#f8fafc' },
        elements: [{
          id: 'portable-text', type: 'text', content: 'Editable text',
          x: 12, y: 24, width: 220, height: 48,
          binding: 'customer.name',
          style: { fontFamily: 'Inter', fontSize: 16, color: '#112233' },
        }],
        extensionData: { futurePageOption: { enabled: true } },
      }],
      sharedElements: [{
        id: 'portable-footer', type: 'text', content: 'Footer',
        x: 20, y: 460, width: 200, height: 20,
      }],
      data: {},
      persistence: { id: 'server-template', revision: 17, status: 'Draft' },
      projectExtensionData: { futureDocumentOption: ['kept'] },
    };

    useEditorStore.getState().openProject(imported, {
      ...DEFAULT_PAGE_SETTINGS,
      metadata: { title: 'Portable project', author: 'PXA', subject: '', keywords: '' },
      activeLanguages: ['en', 'de'],
    });

    const opened = useEditorStore.getState();
    expect(opened.currentTemplate).toEqual(imported);
    expect(opened.pageSettings).toMatchObject({
      metadata: { title: 'Portable project', author: 'PXA' },
      activeLanguages: ['en', 'de'],
    });
    opened.undo();
    expect(useEditorStore.getState().currentTemplate).toEqual(previous);
  });
});
