import React from 'react';

interface LoadingSpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  color?: string;
  className?: string;
}

const LoadingSpinner: React.FC<LoadingSpinnerProps> = ({
  size = 'md',
  color = 'var(--ui-color-accent)',
  className = '',
}) => {
  const sizeStyles = {
    sm: { width: '16px', height: '16px', borderWidth: '2px' },
    md: { width: '24px', height: '24px', borderWidth: '3px' },
    lg: { width: '32px', height: '32px', borderWidth: '4px' },
  };

  const spinnerStyle: React.CSSProperties = {
    ...sizeStyles[size],
    border: `${sizeStyles[size].borderWidth} solid var(--ui-color-bg-app)`,
    borderTop: `${sizeStyles[size].borderWidth} solid ${color}`,
    borderRadius: '50%',
    animation: 'spin 1s linear infinite',
    display: 'inline-block',
  };

  return (
    <>
      <style>
        {`
          @keyframes spin {
            0% { transform: rotate(0deg); }
            100% { transform: rotate(360deg); }
          }
        `}
      </style>
      <div
        className={`loading-spinner loading-spinner-${size} ${className}`}
        style={spinnerStyle}
        role="status"
        aria-label="Loading"
      />
    </>
  );
};

export default LoadingSpinner;