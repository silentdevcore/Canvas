import type {
  DesignerFeatureDefinition,
  DesignerReleaseDefinition,
} from '@/product/productMetadata';

const base = '/api/pxa/v1/designer';

export interface EffectiveDesignerFeature extends DesignerFeatureDefinition {
  enabled: boolean;
  decisionCode: string;
  decisionReason: string;
}

export interface DesignerReleaseFeed {
  releases: DesignerReleaseDefinition[];
  readVersions: string[];
}

export interface DesignerNotification {
  id: string;
  category: 'System' | 'Security' | 'Subscription' | 'ActionRequired';
  severity: 'Info' | 'Success' | 'Warning' | 'Error';
  title: string;
  message: string;
  actionLabel: string | null;
  actionUrl: string | null;
  dismissible: boolean;
  createdAt: string;
  expiresAt: string | null;
  read: boolean;
}

export interface DesignerNotificationPage {
  items: DesignerNotification[];
  page: number;
  pageSize: number;
  total: number;
}

export interface DesignerUnreadCount {
  unread: number;
  persistent: number;
  releases: number;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${base}${path}`, {
    credentials: 'include',
    ...init,
    headers: { Accept: 'application/json', ...init.headers },
  });
  if (!response)
    throw new Error('The Designer product service returned no response.');
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok)
    throw new Error(body?.detail || body?.title || 'The Designer product service is unavailable.');
  return body as T;
}

export const getDesignerFeatures = (): Promise<EffectiveDesignerFeature[]> =>
  request('/features');

export const setDesignerFeaturePreference = (
  featureId: string,
  enabled: boolean,
): Promise<EffectiveDesignerFeature> =>
  request(`/features/${encodeURIComponent(featureId)}/preference`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ enabled }),
  });

export const getDesignerReleases = (): Promise<DesignerReleaseFeed> =>
  request('/releases');

export const markDesignerReleaseRead = (version: string): Promise<void> =>
  request(`/releases/${encodeURIComponent(version)}/read`, { method: 'PUT' });

export const getDesignerNotifications = (): Promise<DesignerNotificationPage> =>
  request('/notifications?page=1&pageSize=50');

export const getDesignerUnreadCount = (): Promise<DesignerUnreadCount> =>
  request('/notifications/unread-count');

export const markDesignerNotificationRead = (id: string): Promise<void> =>
  request(`/notifications/${encodeURIComponent(id)}/read`, { method: 'PUT' });

export const dismissDesignerNotification = (id: string): Promise<void> =>
  request(`/notifications/${encodeURIComponent(id)}/dismiss`, { method: 'PUT' });

export const markAllDesignerNotificationsRead = (): Promise<void> =>
  request('/notifications/read-all', { method: 'PUT' });
