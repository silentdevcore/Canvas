/**
 * @jest-environment jsdom
 */
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import ProductExperiencePanel from '@/product/ProductExperiencePanel';

const globalWithAct = globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT?: boolean };
globalWithAct.IS_REACT_ACT_ENVIRONMENT = true;

const openPanel = jest.fn();
const closePanel = jest.fn();
const markAllRead = jest.fn();
const markReleaseRead = jest.fn().mockResolvedValue(undefined);
const mockNotifications: Array<Record<string, unknown>> = [];

jest.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

jest.mock('@/product/productMetadata', () => ({
  designerVersion: '1.1.0',
  designerCommit: 'abc123',
  designerBuildTime: '2026-08-03T12:00:00Z',
  designerDocumentationUrl: 'https://docs.example.test',
}));

jest.mock('@/product/ProductExperienceProvider', () => ({
  useProductExperience: () => ({
    closePanel,
    dismissNotification: jest.fn(),
    features: [],
    markAllRead,
    markNotificationRead: jest.fn(),
    markReleaseRead,
    notifications: mockNotifications,
    openPanel,
    readVersions: new Set(['1.1.0']),
    releases: [{
      version: '1.1.0',
      publishedAt: '2026-08-03',
      channel: 'stable',
      title: 'PXA 1.1.0',
      summary: 'A release that has already been read.',
      documentationPath: '/releases/1.1.0',
      components: ['designer'],
      featureIds: [],
      changes: {
        added: [],
        improved: ['Release notifications remain available.'],
        fixed: [],
        security: [],
        deprecated: [],
        breaking: [],
      },
    }],
    setFeaturePreference: jest.fn(),
  }),
}));

describe('ProductExperiencePanel release history', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockNotifications.splice(0);
  });

  test('keeps a read release visible and allows it to be reopened', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);
    await act(async () => {
      root.render(
        <ProductExperiencePanel
          panel="notifications"
          onSelectVersion={jest.fn()}
        />,
      );
    });

    const release = [...container.querySelectorAll('button')]
      .find(button => button.textContent === 'PXA 1.1.0');
    expect(release).toBeDefined();
    expect(release?.closest('article')?.classList.contains('is-unread')).toBe(false);

    await act(async () => release?.click());
    expect(openPanel).toHaveBeenCalledWith('releases', '1.1.0');

    await act(async () => root.unmount());
    container.remove();
  });

  test('keeps Legal notifications reopenable and links them to PXA Account', async () => {
    mockNotifications.push({
      id: 'a62dd01e-17ce-442c-805a-a67bfde06671',
      category: 'Legal',
      severity: 'Warning',
      title: 'Terms and Conditions updated',
      message: 'Version 1.2 requires acceptance in PXA Account.',
      actionLabel: 'Review legal update',
      actionUrl: '/legal-updates?document=terms',
      dismissible: true,
      createdAt: '2026-08-14T00:00:00Z',
      expiresAt: null,
      read: true,
    });
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <ProductExperiencePanel
          panel="notifications"
          onSelectVersion={jest.fn()}
        />,
      );
    });

    const action = [...container.querySelectorAll('a')]
      .find(link => link.textContent === 'Review legal update');
    expect(action?.href).toBe('http://localhost:5178/legal-updates?document=terms');
    expect(action?.closest('article')?.classList.contains('is-unread')).toBe(false);

    await act(async () => root.unmount());
    container.remove();
  });
});
