import React, { useState, useEffect, Suspense, lazy } from 'react';
import { DndContext, DragEndEvent } from '@dnd-kit/core';
import { useDesignerStore, useComputedStore, ElementType } from './store';
import Tooltip from './Tooltip';
import Button from './Button';
import ToastContainer from './ToastContainer';
import { useKeyboardNavigation } from './useKeyboardNavigation';
import PerformanceIndicator from './PerformanceIndicator';
import LoadingSpinner from './LoadingSpinner';
import { LocalStorageTemplateRepository } from './infrastructure/repositories/LocalStorageTemplateRepository';
import { DesignTemplate } from './domain/repositories/ITemplateRepository';
import './App.css';

// Lazy load heavy components
const Sidebar = lazy(() => import('./Sidebar'));
const Canvas = lazy(() => import('./Canvas'));
const VirtualCanvas = lazy(() => import('./VirtualCanvas'));
const PropertiesPanel = lazy(() => import('./PropertiesPanel'));
const PageSettingsPanel = lazy(() => import('./PageSettingsPanel'));
const ExportPanel = lazy(() => import('./ExportPanel'));
const ZoomControls = lazy(() => import('./ZoomControls'));
const JSONPanel = lazy(() => import('./JSONPanel'));
const CodePanel = lazy(() => import('./CodePanel'));
const DataPanel = lazy(() => import('./DataPanel'));
const KeyboardShortcuts = lazy(() => import('./KeyboardShortcuts'));

const App: React.FC = () => {
  const addElement = useDesignerStore((state) => state.addElement);
  const updateElementPosition = useDesignerStore((state) => state.updateElementPosition);
  const elements = useDesignerStore((state) => state.elements);
  const selectedIds = useDesignerStore((state) => state.selectedIds);
  const groupElements = useDesignerStore((state) => state.groupElements);
  const ungroupElements = useDesignerStore((state) => state.ungroupElements);
  const showTooltips = useDesignerStore((state) => state.showTooltips);
  const virtualScrolling = useDesignerStore((state) => state.virtualScrolling);
  const toggleVirtualScrolling = useDesignerStore((state) => state.toggleVirtualScrolling);
  const undo = useDesignerStore((state) => state.undo);
  const redo = useDesignerStore((state) => state.redo);
  const copyElements = useDesignerStore((state) => state.copyElements);
  const pasteElements = useDesignerStore((state) => state.pasteElements);
  const selectAll = useDesignerStore((state) => state.selectAll);
  const setAlignmentGuides = useDesignerStore((state) => state.setAlignmentGuides);
  const clearAlignmentGuides = useDesignerStore((state) => state.clearAlignmentGuides);
  const { canUndo, canRedo, canPaste } = useComputedStore();
  const [showJson, setShowJson] = useState(false);
  const [showCode, setShowCode] = useState(false);
  const [showData, setShowData] = useState(false);
  const [documentView, setDocumentView] = useState(false);
  const [showKeyboardHelp, setShowKeyboardHelp] = useState(false);

  // Template management state
  const [currentTemplateId, setCurrentTemplateId] = useState<string | null>(null);
  const [templateRepository] = useState(() => new LocalStorageTemplateRepository());
  const [showTemplateDialog, setShowTemplateDialog] = useState(false);
  const [templateDialogMode, setTemplateDialogMode] = useState<'new' | 'open' | 'save' | 'version'>('new');
  const [availableTemplates, setAvailableTemplates] = useState<Array<{ id: string; name: string }>>([]);
  const [templateName, setTemplateName] = useState('');
  const [templateDescription, setTemplateDescription] = useState('');

  // Template management functions
  const loadAvailableTemplates = async () => {
    try {
      const templates = await templateRepository.getTemplateNames();
      setAvailableTemplates(templates);
    } catch (error) {
      console.error('Failed to load templates:', error);
    }
  };

  const createNewTemplate = () => {
    setTemplateDialogMode('new');
    setTemplateName('');
    setTemplateDescription('');
    setShowTemplateDialog(true);
  };

  const openTemplate = () => {
    setTemplateDialogMode('open');
    loadAvailableTemplates();
    setShowTemplateDialog(true);
  };

  const saveTemplate = () => {
    if (currentTemplateId) {
      // Update existing template
      handleSaveTemplate(currentTemplateId, templateName || 'Untitled Template', templateDescription);
    } else {
      // Save as new template
      setTemplateDialogMode('save');
      setTemplateName('');
      setTemplateDescription('');
      setShowTemplateDialog(true);
    }
  };

  const createTemplateVersion = () => {
    if (currentTemplateId) {
      setTemplateDialogMode('version');
      setTemplateName('');
      setShowTemplateDialog(true);
    }
  };

  const handleSaveTemplate = async (templateId: string, name: string, description: string) => {
    try {
      const elements = useDesignerStore.getState().elements;
      const pageSettings = useDesignerStore.getState().pageSettings;
      const templateMetadata = useDesignerStore.getState().templateMetadata;

      const template: DesignTemplate = {
        id: templateId,
        name,
        description,
        elements: Object.values(elements),
        pageSettings,
        createdAt: currentTemplateId ? new Date() : new Date(),
        updatedAt: new Date(),
        metadata: {
          ...templateMetadata,
          name,
          description,
          updatedAt: new Date().toISOString(),
          updatedBy: 'User'
        }
      };

      await templateRepository.save(template);
      setCurrentTemplateId(templateId);

      // Show success message
      useDesignerStore.getState().addToast(
        `Template "${name}" has been saved successfully.`,
        'success',
        3000
      );

      setShowTemplateDialog(false);
    } catch (error) {
      console.error('Failed to save template:', error);
      useDesignerStore.getState().addToast(
        'Failed to save template. Please try again.',
        'error',
        5000
      );
    }
  };

  const handleLoadTemplate = async (templateId: string) => {
    try {
      const template = await templateRepository.findById(templateId);
      if (template) {
        // Load template data into store
        useDesignerStore.getState().loadTemplate(template);
        setCurrentTemplateId(templateId);
        setTemplateName(template.name);
        setTemplateDescription(template.description || '');

        useDesignerStore.getState().addToast(
          `Template "${template.name}" has been loaded successfully.`,
          'success',
          3000
        );

        setShowTemplateDialog(false);
      }
    } catch (error) {
      console.error('Failed to load template:', error);
      useDesignerStore.getState().addToast(
        'Failed to load template. Please try again.',
        'error',
        5000
      );
    }
  };

  const handleCreateVersion = async (versionName?: string) => {
    if (!currentTemplateId) return;

    try {
      const versionedTemplate = await templateRepository.createVersion(currentTemplateId, versionName);

      useDesignerStore.getState().addToast(
        `Version "${versionedTemplate.metadata?.version}" has been created.`,
        'success',
        3000
      );

      setShowTemplateDialog(false);
    } catch (error) {
      console.error('Failed to create version:', error);
      useDesignerStore.getState().addToast(
        'Failed to create template version. Please try again.',
        'error',
        5000
      );
    }
  };

  // Load available templates on component mount
  useEffect(() => {
    loadAvailableTemplates();
  }, []);

  // Enable keyboard navigation
  useKeyboardNavigation({
    enabled: true,
    arrowKeys: true,
    tabNavigation: true,
    homeEndKeys: true,
    pageUpDownKeys: true,
  });

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ctrl+A or Cmd+A for select all
      if ((e.ctrlKey || e.metaKey) && e.key === 'a') {
        e.preventDefault();
        selectAll();
      }
      // Ctrl+C or Cmd+C for copy
      else if ((e.ctrlKey || e.metaKey) && e.key === 'c') {
        e.preventDefault();
        if (selectedIds.length > 0) {
          copyElements();
        }
      }
      // Ctrl+V or Cmd+V for paste
      else if ((e.ctrlKey || e.metaKey) && e.key === 'v') {
        e.preventDefault();
        if (canPaste) {
          pasteElements();
        }
      }
      // Ctrl+Z or Cmd+Z for undo
      else if ((e.ctrlKey || e.metaKey) && e.key === 'z' && !e.shiftKey) {
        e.preventDefault();
        if (canUndo) {
          undo();
        }
      }
      // Ctrl+Y or Cmd+Y or Ctrl+Shift+Z or Cmd+Shift+Z for redo
      else if ((e.ctrlKey || e.metaKey) && (e.key === 'y' || (e.shiftKey && e.key === 'Z'))) {
        e.preventDefault();
        if (canRedo) {
          redo();
        }
      }
      // Ctrl+G or Cmd+G for group
      else if ((e.ctrlKey || e.metaKey) && e.key === 'g' && !e.shiftKey) {
        e.preventDefault();
        if (selectedIds.length >= 2) {
          groupElements(selectedIds);
        }
      }
      // Ctrl+Shift+G or Cmd+Shift+G for ungroup
      else if ((e.ctrlKey || e.metaKey) && e.shiftKey && e.key === 'G') {
        e.preventDefault();
        if (selectedIds.length === 1) {
          const selectedElement = elements[selectedIds[0]];
          if (selectedElement && selectedElement.isGroup) {
            ungroupElements(selectedIds[0]);
          }
        }
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [selectedIds, elements, groupElements, ungroupElements, undo, redo, canUndo, canRedo, copyElements, pasteElements, canPaste, selectAll]);

  // Calculate snapped position based on alignment guides
  const calculateSnappedPosition = (draggedId: string, currentX: number, currentY: number) => {
    const draggedElement = elements[draggedId];
    if (!draggedElement) return { x: currentX, y: currentY };

    const draggedWidth = draggedElement.width || 0;
    const draggedHeight = draggedElement.height || 0;

    let snappedX = currentX;
    let snappedY = currentY;

    const SNAP_THRESHOLD = 8; // pixels - same as alignment threshold
    let minDistanceX = SNAP_THRESHOLD;
    let minDistanceY = SNAP_THRESHOLD;

    // Check alignment with other elements
    Object.values(elements).forEach(element => {
      if (element.id === draggedId || !element.x || !element.y) return;

      const elemWidth = element.width || 0;
      const elemHeight = element.height || 0;

      // Left edge alignment
      const leftDistance = Math.abs(currentX - element.x);
      if (leftDistance < minDistanceX) {
        snappedX = element.x;
        minDistanceX = leftDistance;
      }

      // Right edge alignment
      const rightDistance = Math.abs((currentX + draggedWidth) - (element.x + elemWidth));
      if (rightDistance < minDistanceX) {
        snappedX = element.x + elemWidth - draggedWidth;
        minDistanceX = rightDistance;
      }

      // Center alignment
      const draggedCenterX = currentX + draggedWidth / 2;
      const elemCenterX = element.x + elemWidth / 2;
      const centerXDistance = Math.abs(draggedCenterX - elemCenterX);
      if (centerXDistance < minDistanceX) {
        snappedX = elemCenterX - draggedWidth / 2;
        minDistanceX = centerXDistance;
      }

      // Top edge alignment
      const topDistance = Math.abs(currentY - element.y);
      if (topDistance < minDistanceY) {
        snappedY = element.y;
        minDistanceY = topDistance;
      }

      // Bottom edge alignment
      const bottomDistance = Math.abs((currentY + draggedHeight) - (element.y + elemHeight));
      if (bottomDistance < minDistanceY) {
        snappedY = element.y + elemHeight - draggedHeight;
        minDistanceY = bottomDistance;
      }

      // Center alignment
      const draggedCenterY = currentY + draggedHeight / 2;
      const elemCenterY = element.y + elemHeight / 2;
      const centerYDistance = Math.abs(draggedCenterY - elemCenterY);
      if (centerYDistance < minDistanceY) {
        snappedY = elemCenterY - draggedHeight / 2;
        minDistanceY = centerYDistance;
      }
    });

    return { x: snappedX, y: snappedY };
  };

  // Calculate alignment guides for the dragged element
  const calculateAlignmentGuides = (draggedId: string, newX: number, newY: number) => {
    const draggedElement = elements[draggedId];
    if (!draggedElement) return { vertical: [], horizontal: [], distances: [] };

    const draggedWidth = draggedElement.width || 0;
    const draggedHeight = draggedElement.height || 0;

    const verticalGuides: Array<{ x: number; label?: string }> = [];
    const horizontalGuides: Array<{ y: number; label?: string }> = [];
    const distances: Array<{ x: number; y: number; text: string; vertical?: boolean }> = [];

    const ALIGNMENT_THRESHOLD = 8; // pixels

    // Check alignment with other elements
    Object.values(elements).forEach(element => {
      if (element.id === draggedId || !element.x || !element.y) return;

      const elemWidth = element.width || 0;
      const elemHeight = element.height || 0;

      // Left edge alignment
      const leftDistance = Math.abs(newX - element.x);
      if (leftDistance < ALIGNMENT_THRESHOLD) {
        verticalGuides.push({ x: element.x, label: 'Left' });
        // Add distance measurement
        if (leftDistance > 0) {
          distances.push({
            x: Math.min(newX, element.x) + Math.abs(newX - element.x) / 2,
            y: Math.min(newY, element.y) - 15,
            text: `${Math.round(leftDistance)}px`,
            vertical: false
          });
        }
      }

      // Right edge alignment
      const rightDistance = Math.abs((newX + draggedWidth) - (element.x + elemWidth));
      if (rightDistance < ALIGNMENT_THRESHOLD) {
        verticalGuides.push({ x: element.x + elemWidth, label: 'Right' });
        // Add distance measurement
        if (rightDistance > 0) {
          distances.push({
            x: Math.max(newX + draggedWidth, element.x + elemWidth) - Math.abs((newX + draggedWidth) - (element.x + elemWidth)) / 2,
            y: Math.min(newY, element.y) - 15,
            text: `${Math.round(rightDistance)}px`,
            vertical: false
          });
        }
      }

      // Center alignment
      const draggedCenterX = newX + draggedWidth / 2;
      const elemCenterX = element.x + elemWidth / 2;
      const centerXDistance = Math.abs(draggedCenterX - elemCenterX);
      if (centerXDistance < ALIGNMENT_THRESHOLD) {
        verticalGuides.push({ x: elemCenterX, label: 'Center' });
        // Add distance measurement
        if (centerXDistance > 0) {
          distances.push({
            x: Math.min(draggedCenterX, elemCenterX) + Math.abs(draggedCenterX - elemCenterX) / 2,
            y: Math.min(newY, element.y) - 15,
            text: `${Math.round(centerXDistance)}px`,
            vertical: false
          });
        }
      }

      // Top edge alignment
      const topDistance = Math.abs(newY - element.y);
      if (topDistance < ALIGNMENT_THRESHOLD) {
        horizontalGuides.push({ y: element.y, label: 'Top' });
        // Add distance measurement
        if (topDistance > 0) {
          distances.push({
            x: Math.max(newX + draggedWidth, element.x + elemWidth) + 10,
            y: Math.min(newY, element.y) + Math.abs(newY - element.y) / 2,
            text: `${Math.round(topDistance)}px`,
            vertical: true
          });
        }
      }

      // Bottom edge alignment
      const bottomDistance = Math.abs((newY + draggedHeight) - (element.y + elemHeight));
      if (bottomDistance < ALIGNMENT_THRESHOLD) {
        horizontalGuides.push({ y: element.y + elemHeight, label: 'Bottom' });
        // Add distance measurement
        if (bottomDistance > 0) {
          distances.push({
            x: Math.max(newX + draggedWidth, element.x + elemWidth) + 10,
            y: Math.max(newY + draggedHeight, element.y + elemHeight) - Math.abs((newY + draggedHeight) - (element.y + elemHeight)) / 2,
            text: `${Math.round(bottomDistance)}px`,
            vertical: true
          });
        }
      }

      // Center alignment
      const draggedCenterY = newY + draggedHeight / 2;
      const elemCenterY = element.y + elemHeight / 2;
      const centerYDistance = Math.abs(draggedCenterY - elemCenterY);
      if (centerYDistance < ALIGNMENT_THRESHOLD) {
        horizontalGuides.push({ y: elemCenterY, label: 'Center' });
        // Add distance measurement
        if (centerYDistance > 0) {
          distances.push({
            x: Math.max(newX + draggedWidth, element.x + elemWidth) + 10,
            y: Math.min(draggedCenterY, elemCenterY) + Math.abs(draggedCenterY - elemCenterY) / 2,
            text: `${Math.round(centerYDistance)}px`,
            vertical: true
          });
        }
      }
    });

    return { vertical: verticalGuides, horizontal: horizontalGuides, distances };
  };

  function handleDragStart(_event: any) {
    // Clear any existing guides when starting drag
    clearAlignmentGuides();
  }

  function handleDragMove(event: any) {
    const { active, delta } = event;

    // Only show guides for existing elements being repositioned
    if (!elements[active.id as string]) return;

    const element = elements[active.id as string];
    const newX = (element.x || 0) + delta.x;
    const newY = (element.y || 0) + delta.y;

    const guides = calculateAlignmentGuides(active.id as string, newX, newY);
    setAlignmentGuides(guides);
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, delta } = event;

    // Clear alignment guides
    clearAlignmentGuides();

    // Check if this is an element being repositioned (exists in elements)
    if (elements[active.id as string]) {
      const element = elements[active.id as string];
      let newX = (element.x || 0) + delta.x;
      let newY = (element.y || 0) + delta.y;

      // Apply snapping to alignment positions
      const snappedPosition = calculateSnappedPosition(active.id as string, newX, newY);
      newX = snappedPosition.x;
      newY = snappedPosition.y;

      updateElementPosition(active.id as string, newX, newY);
    } else {
      // This is a new element from sidebar
      addElement(active.id as ElementType);
    }
  }

  return (
    <DndContext onDragStart={handleDragStart} onDragMove={handleDragMove} onDragEnd={handleDragEnd}>
      <div
        className="ui-designer-layout"
        role="application"
        aria-label="UI Designer Application - A visual UI designer for creating and editing document layouts. Use keyboard navigation or mouse to interact with elements."
      >
        <Suspense fallback={<LoadingSpinner />}>
          <Sidebar />
        </Suspense>
        <div className="ui-main-column">
          <Suspense fallback={<LoadingSpinner />}>
            {virtualScrolling ? (
              <VirtualCanvas width={4000} height={4000} viewportWidth={800} viewportHeight={600} />
            ) : (
              <Canvas documentView={documentView} />
            )}
          </Suspense>
          <div className="ui-bottom-panel-shell">
            <div className="ui-designer-toolbar ui-toolbar-shell">
              <div className="ui-toolbar-group ui-toolbar-group-template">
                <Tooltip content="Create new template" disabled={!showTooltips} position="top">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={createNewTemplate}
                  >
                    New
                  </Button>
                </Tooltip>
                <Tooltip content="Open existing template" disabled={!showTooltips} position="top">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={openTemplate}
                  >
                    Open
                  </Button>
                </Tooltip>
                <Tooltip content="Save current template" disabled={!showTooltips} position="top">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={saveTemplate}
                  >
                    Save
                  </Button>
                </Tooltip>
                <Tooltip content="Create template version" disabled={!showTooltips} position="top">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={createTemplateVersion}
                    disabled={!currentTemplateId}
                  >
                    Version
                  </Button>
                </Tooltip>
                {currentTemplateId && (
                  <span className="ui-toolbar-template-name">
                    {templateName || 'Untitled Template'}
                  </span>
                )}
              </div>

              <div className="ui-toolbar-group ui-toolbar-group-core">
                <Suspense fallback={<div className="ui-toolbar-fallback ui-toolbar-fallback-sm" />}>
                  <PageSettingsPanel />
                </Suspense>
                <Suspense fallback={<div className="ui-toolbar-fallback ui-toolbar-fallback-xs" />}>
                  <ExportPanel />
                </Suspense>
                <Suspense fallback={<div className="ui-toolbar-fallback ui-toolbar-fallback-lg" />}>
                  <ZoomControls />
                </Suspense>
              </div>

              <div className="ui-toolbar-group ui-toolbar-group-view">
                <Tooltip content="Design mode - show placeholders and edit controls" disabled={!showTooltips} position="top">
                  <Button
                    variant={useDesignerStore.getState().previewMode === 'design' ? "primary" : "ghost"}
                    size="sm"
                    onClick={() => useDesignerStore.getState().setPreviewMode('design')}
                  >
                    Design
                  </Button>
                </Tooltip>
                <Tooltip content="Data preview - show resolved values from sample data" disabled={!showTooltips} position="top">
                  <Button
                    variant={useDesignerStore.getState().previewMode === 'data' ? "primary" : "ghost"}
                    size="sm"
                    onClick={() => {
                      useDesignerStore.getState().setPreviewMode('data');
                      useDesignerStore.getState().validateTemplate();
                    }}
                  >
                    Data Preview
                  </Button>
                </Tooltip>
                <Tooltip content="Error preview - show validation errors and missing fields" disabled={!showTooltips} position="top">
                  <Button
                    variant={useDesignerStore.getState().previewMode === 'error' ? "primary" : "ghost"}
                    size="sm"
                    onClick={() => {
                      useDesignerStore.getState().setPreviewMode('error');
                      useDesignerStore.getState().validateTemplate();
                    }}
                  >
                    Error Preview
                  </Button>
                </Tooltip>
                <Tooltip content="Toggle virtual scrolling for large canvases" disabled={!showTooltips} position="top">
                  <Button
                    variant={virtualScrolling ? "primary" : "ghost"}
                    size="sm"
                    onClick={toggleVirtualScrolling}
                  >
                    Virtual {virtualScrolling ? '✓' : '○'}
                  </Button>
                </Tooltip>
                <Tooltip content="Toggle document preview view" disabled={!showTooltips} position="top">
                  <Button
                    variant={documentView ? "primary" : "ghost"}
                    size="sm"
                    onClick={() => setDocumentView(!documentView)}
                  >
                    Document View {documentView ? '✓' : '○'}
                  </Button>
                </Tooltip>
                <Tooltip content="Show keyboard shortcuts help (F1)" disabled={!showTooltips} position="top">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setShowKeyboardHelp(true)}
                  >
                    Help (F1)
                  </Button>
                </Tooltip>
              </div>

              <div className="ui-toolbar-group ui-toolbar-group-optional">
                <Tooltip content="Toggle live JSON view of the canvas elements" disabled={!showTooltips} position="top">
                  <Button
                    variant={showJson ? "primary" : "ghost"}
                    size="sm"
                    onClick={() => setShowJson(!showJson)}
                    className="complex-button"
                  >
                    Live JSON {showJson ? '▼' : '▶'}
                  </Button>
                </Tooltip>
                <Tooltip content="Toggle generated C# code view" disabled={!showTooltips} position="top">
                  <Button
                    variant={showCode ? "primary" : "ghost"}
                    size="sm"
                    onClick={() => setShowCode(!showCode)}
                    className="complex-button"
                  >
                    Generated C# {showCode ? '▼' : '▶'}
                  </Button>
                </Tooltip>
                <Tooltip content="Toggle sample data panel for binding preview" disabled={!showTooltips} position="top">
                  <Button
                    variant={showData ? "primary" : "ghost"}
                    size="sm"
                    onClick={() => setShowData(!showData)}
                    className="complex-button"
                  >
                    Sample Data {showData ? '▼' : '▶'}
                  </Button>
                </Tooltip>
              </div>
            </div>
            {showJson && (
              <div className="json-panel">
                <Suspense fallback={<LoadingSpinner />}>
                  <JSONPanel />
                </Suspense>
              </div>
            )}
            {showCode && (
              <div className="code-panel">
                <Suspense fallback={<LoadingSpinner />}>
                  <CodePanel />
                </Suspense>
              </div>
            )}
            {showData && (
              <div className="data-panel">
                <Suspense fallback={<LoadingSpinner />}>
                  <DataPanel />
                </Suspense>
              </div>
            )}
          </div>
        </div>
        <Suspense fallback={<LoadingSpinner />}>
          <PropertiesPanel />
        </Suspense>
      </div>
      <ToastContainer
        toasts={useDesignerStore((state) => state.toasts)}
        onRemove={useDesignerStore((state) => state.removeToast)}
      />
      {showKeyboardHelp && (
        <Suspense fallback={<LoadingSpinner />}>
          <KeyboardShortcuts
            showHelp={showKeyboardHelp}
            onClose={() => setShowKeyboardHelp(false)}
          />
        </Suspense>
      )}
      <PerformanceIndicator showDetails={false} />

      {/* Template Management Dialog */}
      {showTemplateDialog && (
        <div className="ui-modal-overlay" onClick={() => setShowTemplateDialog(false)}>
          <div className="ui-modal" onClick={(e) => e.stopPropagation()}>
            <div className="ui-modal-header">
              <h2>
                {templateDialogMode === 'new' && 'Create New Template'}
                {templateDialogMode === 'open' && 'Open Template'}
                {templateDialogMode === 'save' && 'Save Template'}
                {templateDialogMode === 'version' && 'Create Template Version'}
              </h2>
              <button
                className="ui-modal-close"
                onClick={() => setShowTemplateDialog(false)}
              >
                ×
              </button>
            </div>

            <div className="ui-modal-body">
              {(templateDialogMode === 'new' || templateDialogMode === 'save') && (
                <div className="ui-form-group">
                  <label htmlFor="template-name">Template Name</label>
                  <input
                    id="template-name"
                    type="text"
                    value={templateName}
                    onChange={(e) => setTemplateName(e.target.value)}
                    placeholder="Enter template name"
                    className="ui-input"
                  />
                </div>
              )}

              {(templateDialogMode === 'new' || templateDialogMode === 'save') && (
                <div className="ui-form-group">
                  <label htmlFor="template-description">Description (Optional)</label>
                  <textarea
                    id="template-description"
                    value={templateDescription}
                    onChange={(e) => setTemplateDescription(e.target.value)}
                    placeholder="Enter template description"
                    className="ui-textarea"
                    rows={3}
                  />
                </div>
              )}

              {templateDialogMode === 'open' && (
                <div className="ui-template-list">
                  {availableTemplates.length === 0 ? (
                    <p className="ui-no-templates">No saved templates found.</p>
                  ) : (
                    availableTemplates.map((template) => (
                      <div
                        key={template.id}
                        className="ui-template-item"
                        onClick={() => handleLoadTemplate(template.id)}
                      >
                        <div className="ui-template-item-name">{template.name}</div>
                        <button className="ui-template-item-load">Load</button>
                      </div>
                    ))
                  )}
                </div>
              )}

              {templateDialogMode === 'version' && (
                <div className="ui-form-group">
                  <label htmlFor="version-name">Version Name (Optional)</label>
                  <input
                    id="version-name"
                    type="text"
                    value={templateName}
                    onChange={(e) => setTemplateName(e.target.value)}
                    placeholder="e.g., v1.1, draft-2"
                    className="ui-input"
                  />
                  <p className="ui-form-help">
                    Leave empty for auto-generated version name.
                  </p>
                </div>
              )}
            </div>

            <div className="ui-modal-footer">
              <button
                className="ui-button ui-button-secondary"
                onClick={() => setShowTemplateDialog(false)}
              >
                Cancel
              </button>

              {templateDialogMode === 'new' && (
                <button
                  className="ui-button ui-button-primary"
                  onClick={() => {
                    const templateId = `template_${Date.now()}`;
                    handleSaveTemplate(templateId, templateName || 'Untitled Template', templateDescription);
                  }}
                  disabled={!templateName.trim()}
                >
                  Create Template
                </button>
              )}

              {templateDialogMode === 'save' && (
                <button
                  className="ui-button ui-button-primary"
                  onClick={() => {
                    const templateId = currentTemplateId || `template_${Date.now()}`;
                    handleSaveTemplate(templateId, templateName || 'Untitled Template', templateDescription);
                  }}
                  disabled={!templateName.trim()}
                >
                  Save Template
                </button>
              )}

              {templateDialogMode === 'version' && (
                <button
                  className="ui-button ui-button-primary"
                  onClick={() => handleCreateVersion(templateName || undefined)}
                >
                  Create Version
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </DndContext>
  );
};

export default App;
