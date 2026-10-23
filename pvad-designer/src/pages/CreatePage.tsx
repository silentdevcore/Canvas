import React, { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import SimplePvadSurface from '@/components/Editor/SimplePvadSurface';
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
import { useProjectImportFlow } from '@/hooks/useProjectImportFlow';
import ProjectImportNotice from '@/components/Project/ProjectImportNotice';
import type { Template } from '@/store';
import type { ParsedDesign } from '@/components/CodeEditor/CodePreviewPane';
import ExportService from '@/services/ExportService';
import { isProjectOperationCancelled, openPdfProject, preparePdfProjectAssets, savePdfProject, type PdfProjectDocument, type PreparedProjectAssets } from '@/services/designerProjectApi';
import { getCodeWorkspace, type CodeWorkspace } from '@/services/codeWorkspaceApi';
import { notify } from '@/notifications/toast';
import { useTranslation } from 'react-i18next';
import { consumeDocumentHandoff, documentHandoffKeys, readDocumentHandoff } from '@/auth/documentHandoff';
import type { PageSettings } from '@/types';
import NativeProjectConsumer from '@/native/NativeProjectConsumer';

type SubView = 'editor' | 'preview' | 'code';

const CreatePage: React.FC = () => {
  const { t } = useTranslation('editor');
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const initialMode = searchParams.get('mode') === 'code' ? 'code' : 'editor';
  const persistedTemplateId = searchParams.get('templateId');
  const [subView, setSubView] = useState<SubView>(initialMode);
  const [loadingPersistedTemplate, setLoadingPersistedTemplate] = useState(Boolean(persistedTemplateId));
  const [loadError, setLoadError] = useState('');
  const localInitializationStarted = useRef(false);
  const projectInput = useRef<HTMLInputElement>(null);
  const [projectActionPending, setProjectActionPending] = useState(false);
  const projectImport = useProjectImportFlow();

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
      if (localInitializationStarted.current) return;
      localInitializationStarted.current = true;
      const handoff = readDocumentHandoff<{ template: Template; pageSettings?: PageSettings }>(documentHandoffKeys.migration);
      if (handoff) {
        try {
          if (handoff?.template?.pages) {
            setCurrentTemplate(handoff.template);
            if (handoff.pageSettings) {
              updatePageSettings(normalizePageSettings(handoff.pageSettings));
            }
            consumeDocumentHandoff(documentHandoffKeys.migration);
            return;
          }
        } catch {
          consumeDocumentHandoff(documentHandoffKeys.migration);
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

  const handleSaveProject = async () => {
    if (projectActionPending) return;
    setProjectActionPending(true);
    try {
      const active = useEditorStore.getState();
      if (!active.currentTemplate) return;
      const document = await ExportService.buildProjectPayload(
        active.currentTemplate,
        active.currentTemplate.pages ?? [],
        active.currentTemplate.sharedElements ?? [],
        active.pageSettings,
      ) as PdfProjectDocument;
      let codeWorkspace = active.currentTemplate.projectCodeWorkspace;
      if (active.currentTemplate.persistence?.id) {
        try {
          const persisted = await getCodeWorkspace(active.currentTemplate.persistence.id);
          codeWorkspace = projectWorkspaceFromApi(persisted);
        } catch (error) {
          const status = (error as Error & { status?: number }).status;
          if (status !== 403 && status !== 404) throw error;
        }
      }
      await savePdfProject(document, codeWorkspace);
      notify.success(t('topbar.projectSaved'));
    } catch (error) {
      notify.error(error instanceof Error ? error.message : t('topbar.projectSaveFailed'));
    } finally {
      setProjectActionPending(false);
    }
  };

  const handleOpenProject = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) await handleOpenProjectFile(file);
  };

  const handleOpenProjectFile = async (file: File, preservePersistence = true) => {
    if (!file || projectActionPending) return;
    setProjectActionPending(true);
    let preparedAssets: PreparedProjectAssets<PdfProjectDocument> | undefined;
    try {
      const target = useEditorStore.getState().currentTemplate;
      const targetSettings = useEditorStore.getState().pageSettings;
      const reservedIds = target
        ? [target.id, ...target.pages.flatMap(page => [page.id, ...page.elements.map(element => element.id)]),
          ...(target.sharedElements ?? []).map(element => element.id),
          ...(targetSettings.namedStyles ?? []).map(style => style.id)]
        : [];
      const signal = projectImport.begin();
      const result = await openPdfProject(file, reservedIds, { signal, onProgress: projectImport.report });
      if (!result.document.pages?.length) throw new Error(t('topbar.projectHasNoPages'));
      if (result.compatibility?.requiresReview) {
        const accepted = await projectImport.review(result.compatibility);
        if (!accepted) return;
      }
      projectImport.stage('assets', 94);
      preparedAssets = await preparePdfProjectAssets(result.document);
      const document = preparedAssets.document;
      projectImport.stage('applying', 99);
      useEditorStore.getState().openProject(
        {
          id: document.id,
          name: document.name,
          category: document.category ?? 'project',
          description: document.description ?? '',
          pages: document.pages,
          sharedElements: document.sharedElements ?? [],
          data: {},
          projectExtensionData: document.extensionData,
          projectCodeWorkspace: result.codeWorkspace,
          ...(preservePersistence && target?.persistence ? { persistence: target.persistence } : {}),
        },
        normalizePageSettings(document.pageSettings),
      );
      preparedAssets.commit();
      projectImport.finish();
      notify.success(t('topbar.projectOpened'));
    } catch (error) {
      await preparedAssets?.rollback();
      projectImport.fail(error, t('topbar.projectOpenFailed'));
      if (!isProjectOperationCancelled(error))
        notify.error(error instanceof Error ? error.message : t('topbar.projectOpenFailed'));
    } finally {
      setProjectActionPending(false);
    }
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
    <>
    <NativeProjectConsumer kind="pdf" onFile={file => handleOpenProjectFile(file, false)} />
    <ProjectImportNotice
      state={projectImport.state}
      labels={{
        opening: t('projectImport.opening'), review: t('projectImport.review'), failed: t('projectImport.failed'),
        uploading: t('projectImport.uploading'), validating: t('projectImport.validating'), assets: t('projectImport.assets'),
        applying: t('projectImport.applying'), cancel: t('projectImport.cancel'), dismiss: t('projectImport.dismiss'),
        openAnyway: t('projectImport.openAnyway'), keepCurrent: t('projectImport.keepCurrent'), preserved: t('projectImport.preserved'),
        missingFeature: feature => t('projectImport.missingFeature', { feature }),
      }}
      onCancel={projectImport.cancel}
      onDismiss={projectImport.dismiss}
      onReviewDecision={projectImport.decideReview}
    />
    <AnimatePresence mode="wait">
      {subView === 'editor' && (
        <motion.div
          key="editor"
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, scale: 0.95 }}
          transition={{ duration: 0.3 }}
        >
          <SimplePvadSurface
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
            onOpenProject={() => projectInput.current?.click()}
            onSaveProject={() => void handleSaveProject()}
            projectActionPending={projectActionPending}
          />
          <input
            ref={projectInput}
            type="file"
            accept=".pvadp,.pxap,application/vnd.pvad.pvadp+zip,application/vnd.powerdox.pxap+zip"
            hidden
            onChange={handleOpenProject}
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
    </>
  );
};

const projectWorkspaceFromApi = (workspace: CodeWorkspace) => ({
  schemaVersion: '1.0' as const,
  revision: workspace.revision,
  baseTemplateRevision: workspace.baseTemplateRevision,
  jsonDraft: workspace.json.source,
  cSharpModelDraft: workspace.cSharpModel.source,
  cSharpPdfDraft: workspace.cSharpPdf.source,
  cSharpBase64Draft: workspace.cSharpBase64.source,
  canonicalDesignJson: JSON.stringify(workspace.canonicalDesign),
  sourceMap: workspace.sourceMap,
});

export default CreatePage;
