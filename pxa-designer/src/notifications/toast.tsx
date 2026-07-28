import React from 'react';
import toast, { Toaster, type ToastOptions } from 'react-hot-toast';

export interface PxaToastOptions extends ToastOptions {
  action?: {
    label: string;
    onClick: () => void;
  };
}

const content = (message: string, options?: PxaToastOptions) => (
  <span className="pxa-toast-content">
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
  success: (message: string, options?: PxaToastOptions) =>
    toast.success(content(message, options), {
      duration: 4000,
      ariaProps: { role: 'status', 'aria-live': 'polite' },
      ...options,
    }),
  info: (message: string, options?: PxaToastOptions) =>
    toast(content(message, options), {
      duration: 5000,
      icon: 'i',
      ariaProps: { role: 'status', 'aria-live': 'polite' },
      ...options,
    }),
  warning: (message: string, options?: PxaToastOptions) =>
    toast(content(message, options), {
      duration: 7000,
      icon: '!',
      ariaProps: { role: 'status', 'aria-live': 'polite' },
      ...options,
    }),
  error: (message: string, options?: PxaToastOptions) =>
    toast.error(content(message, options), {
      duration: 9000,
      ariaProps: { role: 'alert', 'aria-live': 'assertive' },
      ...options,
    }),
  loading: (message: string, options?: PxaToastOptions) =>
    toast.loading(content(message, options), {
      ariaProps: { role: 'status', 'aria-live': 'polite' },
      ...options,
    }),
  dismiss: (id?: string) => toast.dismiss(id),
};

export const PxaToaster: React.FC = () => (
  <Toaster
    position="top-right"
    gutter={10}
    containerClassName="pxa-toast-region"
    toastOptions={{
      className: 'pxa-toast',
      style: {
        borderRadius: '6px',
        border: '1px solid #dbe2ea',
        background: '#fff',
        color: '#172033',
      },
    }}
  />
);
