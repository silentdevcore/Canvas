import React, { useEffect, useState } from 'react';

interface ToastProps {
  message: string;
  type?: 'success' | 'error' | 'warning' | 'info';
  duration?: number;
  onClose?: () => void;
}

const Toast: React.FC<ToastProps> = ({
  message,
  type = 'info',
  duration = 4000,
  onClose
}) => {
  const [isVisible, setIsVisible] = useState(true);
  const [isExiting, setIsExiting] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => {
      setIsExiting(true);
      setTimeout(() => {
        setIsVisible(false);
        onClose?.();
      }, 300); // Animation duration
    }, duration);

    return () => clearTimeout(timer);
  }, [duration, onClose]);

  if (!isVisible) return null;

  const iconByType: Record<NonNullable<ToastProps['type']>, string> = {
    success: '✓',
    error: '✕',
    warning: '⚠',
    info: 'ℹ',
  };

  const handleClose = () => {
    setIsExiting(true);
    setTimeout(() => {
      setIsVisible(false);
      onClose?.();
    }, 300);
  };

  return (
    <div className={`ui-toast ui-toast-${type}${isExiting ? ' is-exiting' : ''}`}>
      <div className="ui-toast-content">
        <span className="ui-toast-icon">{iconByType[type]}</span>
        <span>{message}</span>
      </div>
      <button
        onClick={handleClose}
        className="ui-toast-close"
        aria-label="Close notification"
      >
        ×
      </button>
    </div>
  );
};

export default Toast;