import React from 'react';

jest.mock('react-hot-toast', () => {
  const toast = Object.assign(jest.fn(), {
    success: jest.fn(),
    error: jest.fn(),
    loading: jest.fn(),
    dismiss: jest.fn(),
  });
  return {
    __esModule: true,
    default: toast,
    Toaster: () => null,
  };
});

import toast from 'react-hot-toast';
import { notify } from '@/notifications/toast';

const mockToast = toast as typeof toast & jest.Mock;

describe('PXA toast notifications', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  test('uses a stable id for loading-to-success transitions', () => {
    notify.loading('Saving', { id: 'template-save' });
    notify.success('Saved', { id: 'template-save' });

    expect(mockToast.loading).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ id: 'template-save' }),
    );
    expect(mockToast.success).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ id: 'template-save' }),
    );
  });

  test('uses assertive alert semantics and a longer duration for errors', () => {
    notify.error('Save failed');

    expect(mockToast.error).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({
        duration: 9000,
        ariaProps: { role: 'alert', 'aria-live': 'assertive' },
      }),
    );
  });

  test('forwards deduplication ids to informational messages', () => {
    notify.info('Connection restored', { id: 'connection-state' });
    notify.info('Connection restored', { id: 'connection-state' });

    expect(mockToast).toHaveBeenNthCalledWith(
      1,
      expect.anything(),
      expect.objectContaining({ id: 'connection-state' }),
    );
    expect(mockToast).toHaveBeenNthCalledWith(
      2,
      expect.anything(),
      expect.objectContaining({ id: 'connection-state' }),
    );
  });

  test('runs actions and dismisses their identified toast', () => {
    const onClick = jest.fn();
    notify.success('Saved', {
      id: 'save-result',
      action: { label: 'Open', onClick },
    });
    const successMock = mockToast.success as unknown as jest.Mock;
    const content = successMock.mock.calls[0][0] as React.ReactElement<{
      children: React.ReactNode;
    }>;
    const action = React.Children.toArray(content.props.children)[1] as React.ReactElement<{
      onClick: () => void;
    }>;

    action.props.onClick();

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(mockToast.dismiss).toHaveBeenCalledWith('save-result');
  });
});
