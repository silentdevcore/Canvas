import React, { useState } from 'react';
import { useDesignerStore } from './store';
import Tooltip from './Tooltip';
import LoadingSpinner from './LoadingSpinner';
import Button from './Button';

const ExportPanel: React.FC = () => {
  const { exportToPNG, exportToSVG, exportToPDF, pageSettings, addToast } = useDesignerStore();
  const [showPanel, setShowPanel] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [exportFormat, setExportFormat] = useState<'PNG' | 'SVG' | 'PDF'>('PNG');

  const handleExport = async (format: 'PNG' | 'SVG' | 'PDF') => {
    setIsExporting(true);
    setExportFormat(format);

    try {
      if (format === 'PNG') {
        const dataUrl = await exportToPNG();
        downloadFile(dataUrl, `${pageSettings.title || 'design'}.png`);
      } else if (format === 'SVG') {
        const svgContent = exportToSVG();
        const dataUrl = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svgContent)}`;
        downloadFile(dataUrl, `${pageSettings.title || 'design'}.svg`);
      } else if (format === 'PDF') {
        const pdfData = await exportToPDF();
        // For now, just show the placeholder message
        addToast(pdfData, 'info');
      }
    } catch (error) {
      console.error('Export failed:', error);
      addToast('Export failed. Please try again.', 'error');
    } finally {
      setIsExporting(false);
    }
  };

  const downloadFile = (dataUrl: string, filename: string) => {
    const link = document.createElement('a');
    link.href = dataUrl;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="ui-popover-anchor">
      <Tooltip content="Export your design as PNG, SVG, or PDF" disabled={!useDesignerStore.getState().showTooltips}>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => setShowPanel(!showPanel)}
          disabled={isExporting}
          className={`ui-export-trigger ${showPanel ? 'is-open' : ''}`}
        >
          Export {showPanel ? '▼' : '▶'}
          {isExporting && (
            <div className="ui-inline-spinner">
              <LoadingSpinner size="sm" />
            </div>
          )}
        </Button>
      </Tooltip>

      {showPanel && (
        <div className="ui-popover ui-popover-medium ui-popover-content">
          <h3 className="ui-popover-header">
            Export Design
          </h3>

          <div className="ui-panel-section">
            <label className="ui-field-label">
              Export Format
            </label>
            <div className="ui-export-option-stack">
              <Button
                variant="secondary"
                onClick={() => handleExport('PNG')}
                disabled={isExporting}
                className={`ui-export-option ${isExporting && exportFormat === 'PNG' ? 'is-active' : ''}`}
              >
                <div>
                  <div className="ui-export-option-title">PNG Image</div>
                  <div className="ui-export-option-desc">
                    Raster image, perfect for web and presentations
                  </div>
                </div>
                {isExporting && exportFormat === 'PNG' && (
                  <LoadingSpinner size="sm" />
                )}
              </Button>

              <Button
                variant="secondary"
                onClick={() => handleExport('SVG')}
                disabled={isExporting}
                className={`ui-export-option ${isExporting && exportFormat === 'SVG' ? 'is-active' : ''}`}
              >
                <div>
                  <div className="ui-export-option-title">SVG Vector</div>
                  <div className="ui-export-option-desc">
                    Scalable vector format, ideal for logos and illustrations
                  </div>
                </div>
                {isExporting && exportFormat === 'SVG' && (
                  <LoadingSpinner size="sm" />
                )}
              </Button>

              <Button
                variant="secondary"
                onClick={() => handleExport('PDF')}
                disabled={isExporting}
                className={`ui-export-option ${isExporting && exportFormat === 'PDF' ? 'is-active' : ''}`}
              >
                <div>
                  <div className="ui-export-option-title">PDF Document</div>
                  <div className="ui-export-option-desc">
                    Portable document format, perfect for printing and sharing
                  </div>
                </div>
                {isExporting && exportFormat === 'PDF' && (
                  <div className="ui-export-option-desc">Coming Soon</div>
                )}
              </Button>
            </div>
          </div>

          <div className="ui-card-muted">
            <div className="ui-note-title">
              Export Settings
            </div>
            <div className="ui-note-text ui-note-text-strong">
              Size: {pageSettings.width} × {pageSettings.height} px
            </div>
            <div className="ui-note-title">
              Background: {pageSettings.backgroundColor}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default ExportPanel;