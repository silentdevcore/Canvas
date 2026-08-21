import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import SimplePxaSurface from '@/components/Editor/SimplePxaSurface';
import LivePreview from '@/components/Preview/LivePreview';
import LiveCodeEditor from '@/components/CodeEditor/LiveCodeEditor';
import { normalizePageSettings, useEditorStore } from '@/store';
import {
  archiveDesignerTemplate,
  createDesignerTemplateVersion,
  getDesignerTemplate,
  publishDesignerTemplate,
} from '@/services/designerTemplateApi';
import { useDesignerTemplateAutosave } from '@/hooks/useDesignerTemplateAutosave';
import type { Template } from '@/store';
import type { ParsedDesign } from '@/components/CodeEditor/CodePreviewPane';

type SubView = 'editor' | 'preview' | 'code';

const CreatePage: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const initialMode = searchParams.get('mode') === 'code' ? 'code' : 'editor';
  const persistedTemplateId = searchParams.get('templateId');
  const [subView, setSubView] = useState<SubView>(initialMode);
  const [loadingPersistedTemplate, setLoadingPersistedTemplate] = useState(Boolean(persistedTemplateId));
  const [loadError, setLoadError] = useState('');

  // /pdf/create and /pdf/edit (an alias that redirects to /pdf/create?mode=code)
  // are the same matched route, so switching between them via the sidebar only
  // changes the `mode` query param — React Router does not remount this
  // component for that, so `subView` would otherwise stay frozen at whatever
  // it was on first mount. Re-sync it whenever the URL's mode actually changes.
  useEffect(() => {
    setSubView(prev => (prev === 'preview' ? prev : (searchParams.get('mode') === 'code' ? 'code' : 'editor')));
  }, [searchParams]);

  const {
    currentTemplate,
    currentPageIndex,
    setCurrentTemplate,
    updatePageSettings,
    addElement,
    updateElement,
    deleteElement,
    reorderElement,
    pageSettings,
    markExported,
    addPage,
    deletePage,
    duplicatePage,
    setCurrentPage,
    addSharedElement,
    updateSharedElement,
    deleteSharedElement,
    movePageTo,
  } = useEditorStore();
  const autosave = useDesignerTemplateAutosave(!loadingPersistedTemplate && !loadError);

  useEffect(() => {
    if (!persistedTemplateId) {
      setLoadingPersistedTemplate(false);
      return;
    }
    const controller = new AbortController();
    setLoadingPersistedTemplate(true);
    setLoadError('');
    void getDesignerTemplate(persistedTemplateId, controller.signal)
      .then(serverDocument => {
        const design = serverDocument.designDocument;
        const template = design.template as unknown as Template;
        useEditorStore.setState({
          currentTemplate: {
            ...template,
            name: serverDocument.name,
            description: serverDocument.description ?? template.description ?? '',
            persistence: {
              id: serverDocument.id,
              revision: serverDocument.revision,
              status: serverDocument.status,
            },
          },
          currentPageIndex: design.currentPageIndex ?? 0,
          pageSettings: normalizePageSettings(design.pageSettings),
          jsonData: design.jsonData ?? {},
          documentMode: design.documentMode ?? 'pdf',
          undoStack: [],
          redoStack: [],
        });
        setLoadingPersistedTemplate(false);
      })
      .catch(error => {
        if (controller.signal.aborted) return;
        setLoadError(error instanceof Error ? error.message : 'The saved template could not be loaded.');
        setLoadingPersistedTemplate(false);
      });
    return () => controller.abort();
  }, [persistedTemplateId]);

  useEffect(() => {
    if (!loadingPersistedTemplate && !persistedTemplateId && !currentTemplate) {
      const handoffJson = sessionStorage.getItem('pxa_migration_designer_handoff');
      if (handoffJson) {
        try {
          const handoff = JSON.parse(handoffJson);
          if (handoff?.template?.pages) {
            setCurrentTemplate(handoff.template);
            if (handoff.pageSettings) {
              updatePageSettings(normalizePageSettings(handoff.pageSettings));
            }
            sessionStorage.removeItem('pxa_migration_designer_handoff');
            return;
          }
        } catch {
          sessionStorage.removeItem('pxa_migration_designer_handoff');
        }
      }
      // Any direct link to /pdf/create (nav bar, sidebar, browser history, a
      // deep link) should just open a blank document rather than bouncing
      // back to "/" — matches the Home page's "Blank document" behavior.
      setCurrentTemplate({
        id: `blank-${Date.now()}`,
        name: 'Untitled document',
        category: 'blank',
        description: '',
        pages: [{ id: 'page-1', elements: [] }],
        sharedElements: [],
        data: {},
      });
    }
  }, [currentTemplate, loadingPersistedTemplate, persistedTemplateId, setCurrentTemplate, updatePageSettings]);

  if (loadingPersistedTemplate) {
    return <main className="designer-document-state" aria-busy="true">Loading saved template...</main>;
  }
  if (loadError) {
    return (
      <main className="designer-document-state">
        <h1>Template unavailable</h1>
        <p>{loadError}</p>
        <button type="button" onClick={() => navigate('/pdf/template')}>Back to templates</button>
      </main>
    );
  }
  if (!currentTemplate) return null;

  const pages = currentTemplate.pages ?? [];
  const elements = pages[currentPageIndex]?.elements ?? [];
  const sharedElements = currentTemplate.sharedElements ?? [];

  const handleBack = () => {
    setCurrentTemplate(null);
    navigate('/template');
  };

  const requirePersistence = () => {
    const persistence = useEditorStore.getState().currentTemplate?.persistence;
    if (!persistence) throw new Error('Wait until the template has been saved.');
    return persistence;
  };

  const applyPersistence = (revision: number, status: string) => {
    const active = useEditorStore.getState().currentTemplate;
    if (!active?.persistence) return;
    useEditorStore.setState({
      currentTemplate: {
        ...active,
        persistence: { ...active.persistence, revision, status },
      },
    });
  };

  const handleCreateVersion = async () => {
    const persistence = requirePersistence();
    const result = await createDesignerTemplateVersion(persistence.id, persistence.revision);
    return result.created
      ? `Version ${result.version.versionNumber} created`
      : `Version ${result.version.versionNumber} already matches the current draft`;
  };

  const handlePublish = async () => {
    const persistence = requirePersistence();
    const result = await publishDesignerTemplate(persistence.id, persistence.revision);
    applyPersistence(result.revision, result.status);
    return 'Template published';
  };

  const handleArchive = async () => {
    const persistence = requirePersistence();
    const result = await archiveDesignerTemplate(persistence.id, persistence.revision);
    applyPersistence(result.revision, result.status);
    setCurrentTemplate(null);
    navigate('/pdf/template');
    return 'Template archived';
  };

  const codeDesign: ParsedDesign = {
    id: currentTemplate.id,
    name: currentTemplate.name,
    category: currentTemplate.category,
    description: currentTemplate.description,
    pages,
    sharedElements,
    pageSettings,
  };

  const handleCodeApply = (design: ParsedDesign, revision: number) => {
    const active = useEditorStore.getState().currentTemplate;
    if (!active) return;
    useEditorStore.setState({
      currentTemplate: {
        ...active,
        id: design.id ?? active.id,
        name: design.name ?? active.name,
        category: design.category ?? active.category,
        description: design.description ?? active.description,
        pages: design.pages,
        sharedElements: design.sharedElements ?? [],
        persistence: active.persistence ? { ...active.persistence, revision } : undefined,
      },
      currentPageIndex: Math.min(useEditorStore.getState().currentPageIndex, Math.max(0, design.pages.length - 1)),
      pageSettings: normalizePageSettings(design.pageSettings),
      undoStack: [],
      redoStack: [],
    });
  };

  return (
    <AnimatePresence mode="wait">
      {subView === 'editor' && (
        <motion.div
          key="editor"
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, scale: 0.95 }}
          transition={{ duration: 0.3 }}
        >
          <SimplePxaSurface
            template={currentTemplate}
            elements={elements}
            pages={pages}
            currentPageIndex={currentPageIndex}
            sharedElements={sharedElements}
            onElementAdd={addElement}
            onElementUpdate={updateElement}
            onElementDelete={deleteElement}
            onElementReorder={reorderElement}
            onPreview={() => setSubView('preview')}
            onBack={handleBack}
            onPageAdd={addPage}
            onPageDelete={deletePage}
            onPageDuplicate={duplicatePage}
            onPageSelect={setCurrentPage}
            onSharedElementAdd={addSharedElement}
            onSharedElementUpdate={updateSharedElement}
            onSharedElementDelete={deleteSharedElement}
            onPageMove={movePageTo}
            autosaveState={autosave.state}
            autosaveMessage={autosave.message}
            onCreateVersion={currentTemplate.persistence ? handleCreateVersion : undefined}
            onPublish={currentTemplate.persistence ? handlePublish : undefined}
            onArchive={currentTemplate.persistence ? handleArchive : undefined}
            onTemplateRename={name => {
              const active = useEditorStore.getState().currentTemplate;
              if (active) useEditorStore.setState({ currentTemplate: { ...active, name } });
            }}
          />
          {autosave.state === 'conflict' && (
            <aside className="designer-save-conflict" role="alert">
              <strong>A newer server draft exists</strong>
              <p>Your local work was not overwritten.</p>
              <div>
                <button type="button" onClick={() => void autosave.reloadServer()}>Reload server version</button>
                <button type="button" onClick={() => void autosave.saveAsNew()}>Save as new template</button>
                <button type="button" onClick={autosave.downloadLocal}>Download local JSON</button>
              </div>
            </aside>
          )}
        </motion.div>
      )}

      {subView === 'preview' && (
        <motion.div
          key="preview"
          initial={{ opacity: 0, x: 100 }}
          animate={{ opacity: 1, x: 0 }}
          exit={{ opacity: 0, x: -100 }}
          transition={{ duration: 0.3 }}
        >
          <LivePreview
            template={currentTemplate}
            pages={pages}
            sharedElements={sharedElements}
            pageSettings={pageSettings}
            onBack={() => setSubView('editor')}
            onExport={() => {
              markExported();
            }}
          />
        </motion.div>
      )}

      {subView === 'code' && (
        <motion.div
          key="code"
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: 20 }}
          transition={{ duration: 0.25 }}
          style={{ height: '100vh' }}
        >
          <LiveCodeEditor
            onBack={() => setSubView('editor')}
            templateId={currentTemplate.persistence?.id}
            templateRevision={currentTemplate.persistence?.revision}
            initialDesign={codeDesign}
            onApply={handleCodeApply}
          />
        </motion.div>
      )}
    </AnimatePresence>
  );
};

export default CreatePage;
