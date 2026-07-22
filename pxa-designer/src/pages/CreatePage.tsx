import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import SimplePxaSurface from '@/components/Editor/SimplePxaSurface';
import LivePreview from '@/components/Preview/LivePreview';
import LiveCodeEditor from '@/components/CodeEditor/LiveCodeEditor';
import { normalizePageSettings, useEditorStore } from '@/store';
import { ExportService } from '@/services/ExportService';

type SubView = 'editor' | 'preview' | 'code';

const CreatePage: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const initialMode = searchParams.get('mode') === 'code' ? 'code' : 'editor';
  const [subView, setSubView] = useState<SubView>(initialMode);

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

  useEffect(() => {
    if (!currentTemplate) {
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
  }, [currentTemplate, setCurrentTemplate, updatePageSettings]);

  if (!currentTemplate) return null;

  const pages = currentTemplate.pages ?? [];
  const elements = pages[currentPageIndex]?.elements ?? [];
  const sharedElements = currentTemplate.sharedElements ?? [];

  const handleBack = () => {
    setCurrentTemplate(null);
    navigate('/template');
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
          />
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
              ExportService.exportToJSON(currentTemplate, pages, sharedElements, pageSettings);
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
          <LiveCodeEditor onBack={() => setSubView('editor')} />
        </motion.div>
      )}
    </AnimatePresence>
  );
};

export default CreatePage;
