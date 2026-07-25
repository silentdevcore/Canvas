import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  createDesignerTemplate,
  DesignerTemplateApiError,
  getDesignerTemplate,
  type PersistedDesignDocument,
  updateDesignerTemplateDraft,
} from '@/services/designerTemplateApi';
import { normalizePageSettings, useEditorStore, type Template } from '@/store';

export type AutosaveState =
  | 'idle'
  | 'changed'
  | 'saving'
  | 'saved'
  | 'retrying'
  | 'conflict'
  | 'offline'
  | 'failed';

export interface AutosaveResult {
  state: AutosaveState;
  message: string;
  reloadServer: () => Promise<void>;
  saveAsNew: () => Promise<void>;
  downloadLocal: () => void;
}

const retryDelays = [500, 1000, 2000];

function withoutPersistence(template: Template): Record<string, unknown> {
  const { persistence: _persistence, ...documentTemplate } = template;
  return documentTemplate;
}

function wait(milliseconds: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

export function useDesignerTemplateAutosave(enabled = true): AutosaveResult {
  const currentTemplate = useEditorStore(state => state.currentTemplate);
  const pageSettings = useEditorStore(state => state.pageSettings);
  const jsonData = useEditorStore(state => state.jsonData);
  const documentMode = useEditorStore(state => state.documentMode);
  const currentPageIndex = useEditorStore(state => state.currentPageIndex);
  const [state, setState] = useState<AutosaveState>('idle');
  const [message, setMessage] = useState('');
  const latestSnapshot = useRef<PersistedDesignDocument | null>(null);
  const latestSerialized = useRef('');
  const savedSerialized = useRef('');
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const saving = useRef(false);
  const queued = useRef(false);
  const blocked = useRef(false);
  const activeDocumentKey = useRef('');
  const mounted = useRef(true);

  const snapshot = useMemo<PersistedDesignDocument | null>(() => {
    if (!enabled || !currentTemplate) return null;
    return {
      template: withoutPersistence(currentTemplate),
      pageSettings,
      jsonData,
      documentMode,
      currentPageIndex,
    };
  }, [currentPageIndex, currentTemplate, documentMode, enabled, jsonData, pageSettings]);
  const serialized = useMemo(() => snapshot ? JSON.stringify(snapshot) : '', [snapshot]);

  const applyServerDocument = useCallback((serverDocument: Awaited<ReturnType<typeof getDesignerTemplate>>) => {
    const design = serverDocument.designDocument;
    const template = design.template as unknown as Template;
    useEditorStore.setState({
      currentTemplate: {
        ...template,
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
    const nextSnapshot: PersistedDesignDocument = {
      template: withoutPersistence(template),
      pageSettings: normalizePageSettings(design.pageSettings),
      jsonData: design.jsonData ?? {},
      documentMode: design.documentMode ?? 'pdf',
      currentPageIndex: design.currentPageIndex ?? 0,
    };
    const nextSerialized = JSON.stringify(nextSnapshot);
    latestSnapshot.current = nextSnapshot;
    latestSerialized.current = nextSerialized;
    savedSerialized.current = nextSerialized;
    setState('saved');
    setMessage('Saved');
  }, []);

  const persist = useCallback(async (forceNew = false) => {
    const sourceTemplate = useEditorStore.getState().currentTemplate;
    if (saving.current || !latestSnapshot.current || !sourceTemplate) {
      queued.current = true;
      return;
    }
    const attemptedDocumentKey = sourceTemplate.persistence?.id || sourceTemplate.id;
    saving.current = true;
    queued.current = false;
    const attemptedSnapshot = latestSnapshot.current;
    const attemptedSerialized = latestSerialized.current;
    let terminalFailure = false;
    setState('saving');
    setMessage('Saving...');

    try {
      let result;
      const persistence = forceNew ? undefined : useEditorStore.getState().currentTemplate?.persistence;
      for (let attempt = 0; ; attempt++) {
        try {
          result = persistence
            ? await updateDesignerTemplateDraft(
                persistence.id,
                persistence.revision,
                attemptedSnapshot,
              )
            : await createDesignerTemplate(
                sourceTemplate.name || 'Untitled document',
                sourceTemplate.description || '',
                attemptedSnapshot,
              );
          break;
        } catch (caught) {
          const error = caught as DesignerTemplateApiError;
          const transient = error.offline || error.status >= 500 || error.status === 429;
          if (!transient || attempt >= retryDelays.length) throw error;
          if (mounted.current) {
            setState('retrying');
            setMessage(`Retrying save (${attempt + 1}/${retryDelays.length})...`);
          }
          await wait(retryDelays[attempt]);
        }
      }

      if (!mounted.current) return;
      const active = useEditorStore.getState().currentTemplate;
      const activeKey = active?.persistence?.id || active?.id;
      if (activeKey !== attemptedDocumentKey) return;
      if (active) {
        useEditorStore.setState({
          currentTemplate: {
            ...active,
            persistence: {
              id: result.id,
              revision: result.revision,
              status: result.status,
            },
          },
        });
      }
      savedSerialized.current = attemptedSerialized;
      setState(latestSerialized.current === attemptedSerialized ? 'saved' : 'changed');
      setMessage(latestSerialized.current === attemptedSerialized ? 'Saved' : 'Unsaved changes');
    } catch (caught) {
      if (!mounted.current) return;
      const error = caught as DesignerTemplateApiError;
      if (error.status === 409) {
        terminalFailure = true;
        setState('conflict');
        setMessage(error.updatedBy
          ? `Conflict: newer changes by ${error.updatedBy}`
          : 'Conflict: a newer server draft exists');
      } else if (error.offline) {
        terminalFailure = true;
        setState('offline');
        setMessage('Offline - changes are not saved');
      } else {
        terminalFailure = true;
        if (error.status === 401 || error.status === 403)
          blocked.current = true;
        setState('failed');
        setMessage(error.message || 'Save failed');
      }
    } finally {
      saving.current = false;
      if (mounted.current &&
          !terminalFailure &&
          latestSerialized.current !== savedSerialized.current &&
          (queued.current || latestSerialized.current !== attemptedSerialized)) {
        queued.current = false;
        timer.current = setTimeout(() => void persist(), 2000);
      }
    }
  }, []);

  useEffect(() => {
    latestSnapshot.current = snapshot;
    latestSerialized.current = serialized;
    if (!snapshot) {
      activeDocumentKey.current = '';
      savedSerialized.current = '';
      blocked.current = false;
      setState('idle');
      return;
    }

    const documentKey = currentTemplate?.persistence?.id || currentTemplate?.id || '';
    if (documentKey !== activeDocumentKey.current) {
      if (timer.current) clearTimeout(timer.current);
      activeDocumentKey.current = documentKey;
      savedSerialized.current = currentTemplate?.persistence ? serialized : '';
      blocked.current = false;
      queued.current = false;
      if (currentTemplate?.persistence) {
        setState('saved');
        setMessage('Saved');
      }
    } else if (!savedSerialized.current && currentTemplate?.persistence) {
      savedSerialized.current = serialized;
    }
    if (serialized === savedSerialized.current) return;
    setState(previous => previous === 'conflict' ? previous : 'changed');
    setMessage(previous => previous === 'conflict' ? 'Conflict: a newer server draft exists' : 'Unsaved changes');
    if (timer.current) clearTimeout(timer.current);
    if (!blocked.current)
      timer.current = setTimeout(() => void persist(), 2000);
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, [currentTemplate?.persistence, persist, serialized, snapshot]);

  useEffect(() => {
    mounted.current = true;
    const warn = (event: BeforeUnloadEvent) => {
      if (latestSerialized.current !== savedSerialized.current) {
        event.preventDefault();
        event.returnValue = '';
      }
    };
    window.addEventListener('beforeunload', warn);
    return () => {
      mounted.current = false;
      window.removeEventListener('beforeunload', warn);
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  const reloadServer = useCallback(async () => {
    const id = useEditorStore.getState().currentTemplate?.persistence?.id;
    if (!id) return;
    setState('saving');
    setMessage('Loading server draft...');
    try {
      applyServerDocument(await getDesignerTemplate(id));
    } catch (caught) {
      const error = caught as DesignerTemplateApiError;
      setState(error.offline ? 'offline' : 'failed');
      setMessage(error.message);
    }
  }, [applyServerDocument]);

  const saveAsNew = useCallback(async () => {
    savedSerialized.current = '';
    blocked.current = false;
    setState('changed');
    await persist(true);
  }, [persist]);

  const downloadLocal = useCallback(() => {
    if (!latestSnapshot.current) return;
    const blob = new Blob([JSON.stringify(latestSnapshot.current, null, 2)], {
      type: 'application/json',
    });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `${currentTemplate?.name || 'pxa-design'}-local.json`;
    link.click();
    URL.revokeObjectURL(link.href);
  }, [currentTemplate?.name]);

  return { state, message, reloadServer, saveAsNew, downloadLocal };
}
