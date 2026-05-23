import React, { useState, useRef, useEffect } from 'react';

interface TooltipProps {
  content: string;
  children: React.ReactNode;
  position?: 'top' | 'bottom' | 'left' | 'right';
  delay?: number;
  disabled?: boolean;
}

const Tooltip: React.FC<TooltipProps> = ({
  content,
  children,
  position = 'top',
  delay = 300,
  disabled = false
}) => {
  const [isVisible, setIsVisible] = useState(false);
  const [timeoutId, setTimeoutId] = useState<number | null>(null);
  const tooltipRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLDivElement>(null);

  const showTooltip = () => {
    if (disabled) return;
    const id = setTimeout(() => setIsVisible(true), delay);
    setTimeoutId(id);
  };

  const hideTooltip = () => {
    if (timeoutId) {
      clearTimeout(timeoutId);
      setTimeoutId(null);
    }
    setIsVisible(false);
  };

  useEffect(() => {
    return () => {
      if (timeoutId) {
        clearTimeout(timeoutId);
      }
    };
  }, [timeoutId]);

  const getTooltipPosition = () => {
    if (!tooltipRef.current || !triggerRef.current) return {};

    const triggerRect = triggerRef.current.getBoundingClientRect();
    const tooltipRect = tooltipRef.current.getBoundingClientRect();

    switch (position) {
      case 'top':
        return {
          bottom: window.innerHeight - triggerRect.top + 8,
          left: triggerRect.left + (triggerRect.width / 2) - (tooltipRect.width / 2),
        };
      case 'bottom':
        return {
          top: triggerRect.bottom + 8,
          left: triggerRect.left + (triggerRect.width / 2) - (tooltipRect.width / 2),
        };
      case 'left':
        return {
          top: triggerRect.top + (triggerRect.height / 2) - (tooltipRect.height / 2),
          right: window.innerWidth - triggerRect.left + 8,
        };
      case 'right':
        return {
          top: triggerRect.top + (triggerRect.height / 2) - (tooltipRect.height / 2),
          left: triggerRect.right + 8,
        };
      default:
        return {};
    }
  };

  if (disabled) {
    return <>{children}</>;
  }

  return (
    <div
      ref={triggerRef}
      className="tooltip-trigger"
      onMouseEnter={showTooltip}
      onMouseLeave={hideTooltip}
      onFocus={showTooltip}
      onBlur={hideTooltip}
    >
      {children}
      {isVisible && (
        <div
          ref={tooltipRef}
          className="tooltip"
          style={{
            ...getTooltipPosition(),
          }}
        >
          {content}
          <div className={`tooltip-arrow tooltip-arrow-${position}`} />
        </div>
      )}
    </div>
  );
};

export default Tooltip;