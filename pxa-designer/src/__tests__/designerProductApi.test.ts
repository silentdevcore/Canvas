import {
  dismissDesignerNotification,
  getDesignerFeatures,
  markDesignerReleaseRead,
  setDesignerFeaturePreference,
} from '@/services/designerProductApi';

describe('Designer product API', () => {
  const fetchMock = jest.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    global.fetch = fetchMock;
  });

  test('loads effective features from the authenticated Designer API', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => [{ id: 'designer.notifications', enabled: true }],
    });
    await expect(getDesignerFeatures()).resolves.toEqual([
      { id: 'designer.notifications', enabled: true },
    ]);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/pxa/v1/designer/features',
      expect.objectContaining({ credentials: 'include' }),
    );
  });

  test('uses idempotent PUT mutations for read and preference state', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 204,
      json: async () => null,
    });
    await markDesignerReleaseRead('1.0.0');
    await dismissDesignerNotification('8d479e44-2bb1-4f96-8314-63703c985d30');
    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      '/api/pxa/v1/designer/releases/1.0.0/read',
      expect.objectContaining({ method: 'PUT' }),
    );
    expect(fetchMock).toHaveBeenNthCalledWith(
      2,
      '/api/pxa/v1/designer/notifications/8d479e44-2bb1-4f96-8314-63703c985d30/dismiss',
      expect.objectContaining({ method: 'PUT' }),
    );
  });

  test('sends Alpha preference as structured JSON', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ id: 'designer.ai-layout-assistant', enabled: true }),
    });
    await setDesignerFeaturePreference('designer.ai-layout-assistant', true);
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/pxa/v1/designer/features/designer.ai-layout-assistant/preference',
      expect.objectContaining({
        method: 'PUT',
        body: JSON.stringify({ enabled: true }),
      }),
    );
  });
});
