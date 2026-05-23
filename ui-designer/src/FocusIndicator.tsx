import React, { useState } from 'react';

interface FocusIndicatorProps {
  children: React.ReactNode;
  className?: string;
  style?: React.CSSProperties;
  focusStyle?: React.CSSProperties;
  focusRingColor?: string;
  focusRingWidth?: number;
  focusRingOffset?: number;
  disabled?: boolean;
}

const FocusIndicator: React.FC<FocusIndicatorProps> = ({
  children,
  className = '',
  style,
  focusStyle,
  focusRingColor = 'var(--ui-color-accent)',
  focusRingWidth = 2,
  focusRingOffset = 2,
  disabled = false,
}) => {
  const [isFocused, setIsFocused] = useState(false);

  const handleFocus = (_e: React.FocusEvent) => {
    if (!disabled) {
      setIsFocused(true);
    }
  };

  const handleBlur = (_e: React.FocusEvent) => {
    setIsFocused(false);
  };

  const mergedStyle: React.CSSProperties = {
    ...(style ?? {}),
    ...(focusStyle ?? {}),
    ['--focus-ring-color' as any]: focusRingColor,
    ['--focus-ring-width' as any]: `${focusRingWidth}px`,
    ['--focus-ring-offset' as any]: `${focusRingOffset}px`,
  };

  return (
    <div
      className={`focus-indicator${isFocused ? ' is-focused' : ''}${disabled ? ' is-disabled' : ''}${className ? ` ${className}` : ''}`}
      style={mergedStyle}
      onFocus={handleFocus}
      onBlur={handleBlur}
      tabIndex={disabled ? -1 : 0}
    >
      {children}
    </div>
  );
};

export default FocusIndicator;