import React from 'react';
import toast, { Toaster, type ToastOptions } from 'react-hot-toast';

export interface PvadToastOptions extends ToastOptions {
  action?: {
    label: string;
    onClick: () => void;
  };
}

const content = (message: string, options?: PvadToastOptions) => (
  <span className="pvad-toast-content">
    <span>{message}</span>
    {options?.action && (
      <button
        type="button"
        onClick={() => {
          options.action?.onClick();
          if (options.id) toast.dismiss(options.id);
        }}
      >
        {options.action.label}
      </button>
    )}
  </span>
);

export const notify = {
  success: (message: string, options?: PvadToastOptions) =>
    toast.success(content(message, options), {
      duration: 4000,
      ariaProps: { role: 'status', 'aria-live': 'polite' },
      ...options,
    }),
  info: (message: string, options?: PvadToastOptions) =>
    toast(content(message, options), {
      duration: 5000,
      icon: 'i',
      ariaProps: { role: 'status', 'aria-live': 'polite' },
      ...options,
    }),
  warning: (message: string, options?: PvadToastOptions) =>
    toast(content(message, options), {
      duration: 7000,
      icon: '!',
      ariaProps: { role: 'status', 'aria-live': 'polite' },
      ...options,
    }),
  error: (message: string, options?: PvadToastOptions) =>
    toast.error(content(message, options), {
      duration: 9000,
      ariaProps: { role: 'alert', 'aria-live': 'assertive' },
      ...options,
    }),
  loading: (message: string, options?: PvadToastOptions) =>
    toast.loading(content(message, options), {
      ariaProps: { role: 'status', 'aria-live': 'polite' },
      ...options,
    }),
  dismiss: (id?: string) => toast.dismiss(id),
};

export const PvadToaster: React.FC = () => (
  <Toaster
    position="top-right"
    gutter={10}
    containerClassName="pvad-toast-region"
    toastOptions={{
      className: 'pvad-toast',
      style: {
        borderRadius: '6px',
        border: '1px solid #dbe2ea',
        background: '#fff',
        color: '#172033',
      },
    }}
  />
);
