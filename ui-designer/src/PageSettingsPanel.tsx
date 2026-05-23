import React, { useState } from 'react';
import { useDesignerStore } from './store';
import { PageSize, PageOrientation } from './store';
import Tooltip from './Tooltip';
import Button from './Button';

type TabType = 'page' | 'grid' | 'help';

const PageSettingsPanel: React.FC = () => {
  const {
    pageSettings,
    updatePageSettings,
    setPageSize,
    setPageOrientation,
    setPageBackgroundColor,
    showTooltips,
    toggleTooltips,
    gridSize,
    gridColor,
    gridOpacity,
    setGridSize,
    setGridColor,
    setGridOpacity,
    snapToGrid,
    toggleSnapToGrid
  } = useDesignerStore();
  const [showPanel, setShowPanel] = useState(false);
  const [activeTab, setActiveTab] = useState<TabType>('page');

  const pageSizeOptions: { value: PageSize; label: string; dimensions: string }[] = [
    { value: 'A4', label: 'A4', dimensions: '210 × 297 mm' },
    { value: 'A5', label: 'A5', dimensions: '148 × 210 mm' },
    { value: 'A6', label: 'A6', dimensions: '105 × 148 mm' },
    { value: 'Letter', label: 'Letter', dimensions: '8.5 × 11 in' },
    { value: 'Legal', label: 'Legal', dimensions: '8.5 × 14 in' },
    { value: 'Custom', label: 'Custom', dimensions: 'Variable' },
  ];

  const handlePageSizeChange = (size: PageSize) => {
    setPageSize(size);
  };

  const handleOrientationChange = (orientation: PageOrientation) => {
    setPageOrientation(orientation);
  };

  const handleBackgroundColorChange = (color: string) => {
    setPageBackgroundColor(color);
  };

  const handleMarginChange = (side: keyof typeof pageSettings.margins, value: number) => {
    updatePageSettings({
      margins: {
        ...pageSettings.margins,
        [side]: value,
      },
    });
  };

  const handleTitleChange = (title: string) => {
    updatePageSettings({ title });
  };

  const handleDescriptionChange = (description: string) => {
    updatePageSettings({ description });
  };

  const renderPageTab = () => (
    <div>
      {/* Page Size */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Page Size
        </label>
        <select
          value={pageSettings.size}
          onChange={(e) => handlePageSizeChange(e.target.value as PageSize)}
          className="ui-input-control"
        >
          {pageSizeOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label} ({option.dimensions})
            </option>
          ))}
        </select>
      </div>

      {/* Orientation */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Orientation
        </label>
        <div className="ui-dual-actions">
          <button
            onClick={() => handleOrientationChange('Portrait')}
            className={`ui-toggle-button ${pageSettings.orientation === 'Portrait' ? 'is-active' : ''} ui-flex-1`}
          >
            📄 Portrait
          </button>
          <button
            onClick={() => handleOrientationChange('Landscape')}
            className={`ui-toggle-button ${pageSettings.orientation === 'Landscape' ? 'is-active' : ''} ui-flex-1`}
          >
            📄 Landscape
          </button>
        </div>
      </div>

      {/* Background Color */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Background Color
        </label>
        <div className="ui-inline-group">
          <input
            type="color"
            value={pageSettings.backgroundColor}
            onChange={(e) => handleBackgroundColorChange(e.target.value)}
            className="ui-color-swatch"
          />
          <input
            type="text"
            value={pageSettings.backgroundColor}
            onChange={(e) => handleBackgroundColorChange(e.target.value)}
            className="ui-input-control ui-input-control--compact ui-input-control--mono ui-flex-1"
          />
        </div>
      </div>

      {/* Dimensions Display */}
      <div className="ui-panel-section ui-card-muted">
        <div className="ui-note-title">
          Dimensions
        </div>
        <div className="ui-note-text ui-note-text-strong">
          {pageSettings.width} × {pageSettings.height} px
        </div>
      </div>

      {/* Document Info */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Document Title
        </label>
        <input
          type="text"
          value={pageSettings.title}
          onChange={(e) => handleTitleChange(e.target.value)}
          placeholder="Enter document title"
          className="ui-input-control"
        />
      </div>

      <div className="ui-panel-section">
        <label className="ui-field-label">
          Description
        </label>
        <textarea
          value={pageSettings.description}
          onChange={(e) => handleDescriptionChange(e.target.value)}
          placeholder="Enter document description"
          rows={3}
          className="ui-input-control ui-resize-vertical"
        />
      </div>

      {/* Margins */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Page Margins (px)
        </label>
        <div className="ui-grid-2">
          <div>
            <label className="ui-field-label-sm">
              Top
            </label>
            <input
              type="number"
              value={pageSettings.margins.top}
              onChange={(e) => handleMarginChange('top', parseInt(e.target.value) || 0)}
              min="0"
              className="ui-input-control ui-input-control--compact"
            />
          </div>
          <div>
            <label className="ui-field-label-sm">
              Right
            </label>
            <input
              type="number"
              value={pageSettings.margins.right}
              onChange={(e) => handleMarginChange('right', parseInt(e.target.value) || 0)}
              min="0"
              className="ui-input-control ui-input-control--compact"
            />
          </div>
          <div>
            <label className="ui-field-label-sm">
              Bottom
            </label>
            <input
              type="number"
              value={pageSettings.margins.bottom}
              onChange={(e) => handleMarginChange('bottom', parseInt(e.target.value) || 0)}
              min="0"
              className="ui-input-control ui-input-control--compact"
            />
          </div>
          <div>
            <label className="ui-field-label-sm">
              Left
            </label>
            <input
              type="number"
              value={pageSettings.margins.left}
              onChange={(e) => handleMarginChange('left', parseInt(e.target.value) || 0)}
              min="0"
              className="ui-input-control ui-input-control--compact"
            />
          </div>
        </div>
      </div>
    </div>
  );

  const renderGridTab = () => (
    <div>
      {/* Snap to Grid Toggle */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Grid Snapping
        </label>
        <button
          onClick={toggleSnapToGrid}
          className={`ui-toggle-button ${snapToGrid ? 'is-active' : ''}`}
        >
          {snapToGrid ? '✓' : '○'} Snap to Grid
        </button>
      </div>

      {/* Grid Size */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Grid Size (px)
        </label>
        <input
          type="number"
          value={gridSize}
          onChange={(e) => setGridSize(parseInt(e.target.value) || 20)}
          min="5"
          max="100"
          className="ui-input-control"
        />
      </div>

      {/* Grid Opacity */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Grid Opacity
        </label>
        <input
          type="range"
          min="0.1"
          max="1"
          step="0.1"
          value={gridOpacity}
          onChange={(e) => setGridOpacity(parseFloat(e.target.value))}
          className="ui-range-control"
        />
        <div className="ui-note-title ui-note-title-top-gap">
          {Math.round(gridOpacity * 100)}%
        </div>
      </div>

      {/* Grid Color */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Grid Color
        </label>
        <div className="ui-inline-group">
          <input
            type="color"
            value={gridColor}
            onChange={(e) => setGridColor(e.target.value)}
            className="ui-color-swatch"
          />
          <input
            type="text"
            value={gridColor}
            onChange={(e) => setGridColor(e.target.value)}
            className="ui-input-control ui-input-control--compact ui-input-control--mono ui-flex-1"
          />
        </div>
      </div>

      {/* Tooltips Toggle */}
      <div className="ui-panel-section">
        <label className="ui-field-label">
          Interface Settings
        </label>
        <button
          onClick={toggleTooltips}
          className={`ui-toggle-button ${showTooltips ? 'is-active' : ''}`}
        >
          {showTooltips ? '✓' : '○'} Show Tooltips
        </button>
      </div>
    </div>
  );

  const renderHelpTab = () => (
    <div>
      <h4 className="ui-help-title">
        How to Use the UI Designer
      </h4>

      <div className="ui-help-section">
        <h5 className="ui-help-subtitle">
          🎨 Getting Started
        </h5>
        <ul className="ui-help-list">
          <li>Drag elements from the sidebar onto the canvas</li>
          <li>Click and drag elements to reposition them</li>
          <li>Use the resize handles to adjust element sizes</li>
          <li>Right-click elements for additional options</li>
        </ul>
      </div>

      <div className="ui-help-section">
        <h5 className="ui-help-subtitle">
          ⌨️ Keyboard Shortcuts
        </h5>
        <div className="ui-help-shortcuts">
          <div><strong>Ctrl+A</strong> - Select all elements</div>
          <div><strong>Ctrl+C</strong> - Copy selected elements</div>
          <div><strong>Ctrl+V</strong> - Paste elements</div>
          <div><strong>Ctrl+Z</strong> - Undo last action</div>
          <div><strong>Ctrl+Y</strong> - Redo last action</div>
          <div><strong>Ctrl+G</strong> - Group selected elements</div>
          <div><strong>Delete</strong> - Remove selected elements</div>
        </div>
      </div>

      <div className="ui-help-section">
        <h5 className="ui-help-subtitle">
          🔍 Navigation & Zoom
        </h5>
        <ul className="ui-help-list">
          <li>Use zoom controls to change canvas magnification</li>
          <li>Hold Ctrl and scroll mouse wheel for quick zoom</li>
          <li>Click "Zoom to Fit" to see entire page</li>
          <li>Double-click canvas to reset zoom</li>
        </ul>
      </div>

      <div className="ui-help-section">
        <h5 className="ui-help-subtitle">
          📄 Page Settings
        </h5>
        <ul className="ui-help-list">
          <li>Configure page size, orientation, and background</li>
          <li>Adjust grid settings for precise alignment</li>
          <li>Set document title and description</li>
          <li>Customize page margins</li>
        </ul>
      </div>

      <div className="ui-help-section">
        <h5 className="ui-help-subtitle">
          💾 Export Options
        </h5>
        <ul className="ui-help-list">
          <li><strong>PNG</strong> - Raster image for web and presentations</li>
          <li><strong>SVG</strong> - Vector format for logos and scalable graphics</li>
          <li><strong>PDF</strong> - Document format for printing (coming soon)</li>
        </ul>
      </div>

      <div className="ui-help-section">
        <h5 className="ui-help-subtitle">
          🎯 Tips & Tricks
        </h5>
        <ul className="ui-help-list">
          <li>Enable grid snapping for pixel-perfect alignment</li>
          <li>Use multi-selection (Ctrl+click) for batch operations</li>
          <li>Group elements to move them as a single unit</li>
          <li>Check the JSON panel to see element structure</li>
          <li>Use the Properties panel to fine-tune element settings</li>
        </ul>
      </div>

      <div className="ui-card-muted">
        <div className="ui-note-title ui-note-title-strong">
          💡 Pro Tip
        </div>
        <div className="ui-note-text ui-note-text-compact">
          Hold Shift while dragging to constrain movement to horizontal/vertical directions only.
        </div>
      </div>
    </div>
  );

  return (
    <div className="ui-popover-anchor">
      <Tooltip content="Configure page, grid, and help settings" disabled={!showTooltips}>
        <Button
          variant={showPanel ? "primary" : "ghost"}
          size="sm"
          onClick={() => setShowPanel(!showPanel)}
        >
          Settings {showPanel ? '▼' : '▶'}
        </Button>
      </Tooltip>

      {showPanel && (
        <div className="ui-popover ui-popover-wide">
          {/* Tab Navigation */}
          <div className="ui-tab-nav-wrap">
            <div className="tab-navigation">
              <button
                onClick={() => setActiveTab('page')}
                className={`tab-button ${activeTab === 'page' ? 'active' : ''}`}
              >
                📄 Page
              </button>
              <button
                onClick={() => setActiveTab('grid')}
                className={`tab-button ${activeTab === 'grid' ? 'active' : ''}`}
              >
                📐 Grid
              </button>
              <button
                onClick={() => setActiveTab('help')}
                className={`tab-button ${activeTab === 'help' ? 'active' : ''}`}
              >
                ❓ Help
              </button>
            </div>
          </div>

          {/* Tab Content */}
          <div className="ui-popover-content">
            {activeTab === 'page' && renderPageTab()}
            {activeTab === 'grid' && renderGridTab()}
            {activeTab === 'help' && renderHelpTab()}
          </div>
        </div>
      )}
    </div>
  );
};

export default PageSettingsPanel;