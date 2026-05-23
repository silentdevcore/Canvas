import React from 'react';
import { useDesignerStore } from './store';
import Tooltip from './Tooltip';
import Button from './Button';

const ZoomControls: React.FC = () => {
  const { zoom, zoomIn, zoomOut, zoomToFit, resetZoom, setZoom } = useDesignerStore();
  const showTooltips = useDesignerStore((state) => state.showTooltips);

  const handleZoomChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newZoom = parseFloat(e.target.value);
    setZoom(newZoom);
  };

  const zoomPercentage = Math.round(zoom * 100);

  return (
    <div className="ui-zoom-controls">
      <Tooltip content="Zoom out" disabled={!showTooltips}>
        <Button variant="secondary" size="sm" onClick={zoomOut}>
          ➖
        </Button>
      </Tooltip>

      <div className="ui-zoom-slider-group">
        <input
          type="range"
          min="0.1"
          max="5"
          step="0.1"
          value={zoom}
          onChange={handleZoomChange}
          className="ui-zoom-slider"
        />
        <span className="ui-zoom-value">
          {zoomPercentage}%
        </span>
      </div>

      <Tooltip content="Zoom in" disabled={!showTooltips}>
        <Button variant="secondary" size="sm" onClick={zoomIn}>
          ➕
        </Button>
      </Tooltip>

      <Tooltip content="Zoom to fit page" disabled={!showTooltips}>
        <Button variant="secondary" size="sm" onClick={zoomToFit}>
          🔍 Fit
        </Button>
      </Tooltip>

      <Tooltip content="Reset zoom to 100%" disabled={!showTooltips}>
        <Button variant="secondary" size="sm" onClick={resetZoom}>
          100%
        </Button>
      </Tooltip>
    </div>
  );
};

export default ZoomControls;