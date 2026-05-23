import React, { useState, useEffect, useMemo } from 'react';
import { useDesignerStore } from './store';

interface PerformanceIndicatorProps {
  className?: string;
  showDetails?: boolean;
}

const PerformanceIndicator: React.FC<PerformanceIndicatorProps> = ({
  className = '',
  showDetails = false
}) => {
  const { rootIds, elements, virtualScrolling } = useDesignerStore();
  const [fps, setFps] = useState(60);
  const [memoryUsage, setMemoryUsage] = useState<number | null>(null);
  const [renderTime, setRenderTime] = useState(0);

  // Calculate performance metrics
  const metrics = useMemo(() => {
    const totalElements = rootIds.length;
    const visibleElements = rootIds.filter(id => {
      const element = elements[id];
      return element && !element.locked;
    }).length;

    const memoryEstimate = totalElements * 1024; // Rough estimate: 1KB per element

    return {
      totalElements,
      visibleElements,
      memoryEstimate,
      virtualScrolling
    };
  }, [rootIds, elements, virtualScrolling]);

  // FPS monitoring
  useEffect(() => {
    let frameCount = 0;
    let lastTime = performance.now();
    let animationId: number;

    const measureFPS = () => {
      frameCount++;
      const currentTime = performance.now();

      if (currentTime - lastTime >= 1000) {
        setFps(Math.round((frameCount * 1000) / (currentTime - lastTime)));
        frameCount = 0;
        lastTime = currentTime;
      }

      animationId = requestAnimationFrame(measureFPS);
    };

    animationId = requestAnimationFrame(measureFPS);

    return () => {
      if (animationId) {
        cancelAnimationFrame(animationId);
      }
    };
  }, []);

  // Memory usage monitoring (if available)
  useEffect(() => {
    const checkMemory = () => {
      if ('memory' in performance) {
        const memInfo = (performance as any).memory;
        setMemoryUsage(Math.round(memInfo.usedJSHeapSize / 1024 / 1024)); // MB
      }
    };

    const interval = setInterval(checkMemory, 5000);
    checkMemory(); // Initial check

    return () => clearInterval(interval);
  }, []);

  // Render time monitoring
  useEffect(() => {
    const startTime = performance.now();

    return () => {
      const endTime = performance.now();
      setRenderTime(endTime - startTime);
    };
  });

  const getPerformanceClass = (fps: number) => {
    if (fps >= 50) return 'is-success';
    if (fps >= 30) return 'is-warning';
    return 'is-danger';
  };

  const getMemoryClass = (memory: number | null) => {
    if (!memory) return 'is-text-secondary';
    if (memory < 50) return 'is-success';
    if (memory < 100) return 'is-warning';
    return 'is-danger';
  };

  if (!showDetails) {
    return (
      <div className={`performance-indicator compact ${className}`}>
        <span className={`performance-indicator-value ${getPerformanceClass(fps)}`}>
          {fps} FPS
        </span>
        {memoryUsage && (
          <span className={`performance-indicator-value ${getMemoryClass(memoryUsage)}`}>
            {memoryUsage}MB
          </span>
        )}
        {virtualScrolling && (
          <span className="performance-indicator-value is-info">
            {metrics.visibleElements}/{metrics.totalElements}
          </span>
        )}
      </div>
    );
  }

  return (
    <div className={`performance-indicator detailed ${className}`}>
      <div className="performance-indicator-header">
        Performance Monitor
      </div>

      <div className="performance-indicator-grid">
        <div className="performance-indicator-row">
          <span>FPS:</span>
          <span className={`performance-indicator-value ${getPerformanceClass(fps)}`}>{fps}</span>
        </div>

        {memoryUsage && (
          <div className="performance-indicator-row">
            <span>Memory:</span>
            <span className={`performance-indicator-value ${getMemoryClass(memoryUsage)}`}>{memoryUsage} MB</span>
          </div>
        )}

        <div className="performance-indicator-row">
          <span>Elements:</span>
          <span>{metrics.totalElements}</span>
        </div>

        {virtualScrolling && (
          <div className="performance-indicator-row">
            <span>Visible:</span>
            <span className="performance-indicator-value is-info">{metrics.visibleElements}</span>
          </div>
        )}

        <div className="performance-indicator-row">
          <span>Render:</span>
          <span>{renderTime.toFixed(1)}ms</span>
        </div>

        <div className="performance-indicator-row">
          <span>Virtual:</span>
          <span className={`performance-indicator-value ${virtualScrolling ? 'is-success' : 'is-danger'}`}>
            {virtualScrolling ? 'ON' : 'OFF'}
          </span>
        </div>
      </div>
    </div>
  );
};

export default PerformanceIndicator;