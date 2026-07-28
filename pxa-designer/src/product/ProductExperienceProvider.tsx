import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { useTranslation } from 'react-i18next';
import {
  designerFeatures as fallbackFeatures,
  designerReleases as fallbackReleases,
  designerVersion,
  type DesignerReleaseDefinition,
} from './productMetadata';
import {
  dismissDesignerNotification,
  getDesignerFeatures,
  getDesignerNotifications,
  getDesignerReleases,
  getDesignerUnreadCount,
  markAllDesignerNotificationsRead,
  markDesignerNotificationRead,
  markDesignerReleaseRead,
  setDesignerFeaturePreference,
  type DesignerNotification,
  type EffectiveDesignerFeature,
} from '@/services/designerProductApi';
import { notify, PxaToaster } from '@/notifications/toast';
import ProductExperiencePanel from './ProductExperiencePanel';

export type ProductPanel = 'releases' | 'notifications' | 'features';

interface ProductExperienceContextValue {
  features: EffectiveDesignerFeature[];
  notifications: DesignerNotification[];
  releases: DesignerReleaseDefinition[];
  readVersions: Set<string>;
  unreadCount: number;
  loading: boolean;
  openPanel: (panel: ProductPanel, version?: string) => void;
  closePanel: () => void;
  markReleaseRead: (version: string) => Promise<void>;
  markNotificationRead: (id: string) => Promise<void>;
  dismissNotification: (id: string) => Promise<void>;
  markAllRead: () => Promise<void>;
  setFeaturePreference: (featureId: string, enabled: boolean) => Promise<void>;
}

const ProductExperienceContext = createContext<ProductExperienceContextValue | null>(null);

export const useProductExperience = (): ProductExperienceContextValue => {
  const value = useContext(ProductExperienceContext);
  if (!value)
    throw new Error('useProductExperience must be used inside ProductExperienceProvider.');
  return value;
};

const fallbackEffectiveFeatures: EffectiveDesignerFeature[] = fallbackFeatures.map(feature => ({
  ...feature,
  enabled: feature.maturity !== 'alpha' && feature.defaultEnabled,
  decisionCode: feature.maturity === 'alpha'
    ? 'PXA_DESIGNER_ALPHA_OPT_IN_REQUIRED'
    : 'PXA_DESIGNER_FEATURE_ENABLED',
  decisionReason: feature.maturity === 'alpha'
    ? 'Enable this Alpha feature before use.'
    : 'The feature is available.',
}));

const ProductExperienceProvider: React.FC<React.PropsWithChildren> = ({ children }) => {
  const { t } = useTranslation('common');
  const [features, setFeatures] = useState(fallbackEffectiveFeatures);
  const [releases, setReleases] = useState(fallbackReleases);
  const [readVersions, setReadVersions] = useState<Set<string>>(new Set());
  const [notifications, setNotifications] = useState<DesignerNotification[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [releaseStateLoaded, setReleaseStateLoaded] = useState(false);
  const [panel, setPanel] = useState<ProductPanel | null>(null);
  const [selectedVersion, setSelectedVersion] = useState<string | undefined>();
  const autoOpened = useRef(false);

  const refresh = useCallback(async (showFailure = false) => {
    const [featureResult, releaseResult, notificationResult, countResult] = await Promise.allSettled([
      getDesignerFeatures(),
      getDesignerReleases(),
      getDesignerNotifications(),
      getDesignerUnreadCount(),
    ]);
    if (featureResult.status === 'fulfilled' && Array.isArray(featureResult.value))
      setFeatures(featureResult.value);
    if (releaseResult.status === 'fulfilled' && Array.isArray(releaseResult.value?.releases)) {
      setReleases(releaseResult.value.releases);
      setReadVersions(new Set(releaseResult.value.readVersions));
      setReleaseStateLoaded(true);
    }
    if (notificationResult.status === 'fulfilled' && Array.isArray(notificationResult.value?.items))
      setNotifications(notificationResult.value.items);
    if (countResult.status === 'fulfilled' && Number.isFinite(countResult.value?.unread))
      setUnreadCount(countResult.value.unread);
    if (showFailure && [featureResult, releaseResult, notificationResult, countResult]
      .some(result => result.status === 'rejected')) {
      notify.warning(t('productExperience.refreshFailed'));
    }
    setLoading(false);
  }, [t]);

  useEffect(() => {
    if (typeof navigator !== 'undefined' && navigator.userAgent.includes('jsdom')) {
      setLoading(false);
      return;
    }
    void refresh();
    const interval = window.setInterval(() => void refresh(), 60_000);
    const onFocus = () => void refresh();
    window.addEventListener('focus', onFocus);
    return () => {
      window.clearInterval(interval);
      window.removeEventListener('focus', onFocus);
    };
  }, [refresh]);

  useEffect(() => {
    if (loading || !releaseStateLoaded || autoOpened.current) return;
    const current = releases.find(release => release.version === designerVersion);
    if (current && !readVersions.has(current.version)) {
      autoOpened.current = true;
      setSelectedVersion(current.version);
      setPanel('releases');
    }
  }, [loading, readVersions, releaseStateLoaded, releases]);

  const markReleaseRead = useCallback(async (version: string) => {
    if (readVersions.has(version)) return;
    try {
      await markDesignerReleaseRead(version);
      setReadVersions(previous => new Set(previous).add(version));
      setUnreadCount(previous => Math.max(0, previous - 1));
    } catch {
      // Keep the release visible as unread until the API can persist the state.
    }
  }, [readVersions]);

  const openPanel = useCallback((nextPanel: ProductPanel, version?: string) => {
    setSelectedVersion(version);
    setPanel(nextPanel);
    if (nextPanel === 'releases') {
      const target = version ?? releases[0]?.version;
      if (target) void markReleaseRead(target);
    }
  }, [markReleaseRead, releases]);

  const markNotificationRead = useCallback(async (id: string) => {
    const current = notifications.find(item => item.id === id);
    if (!current || current.read) return;
    await markDesignerNotificationRead(id);
    setNotifications(items => items.map(item => item.id === id ? { ...item, read: true } : item));
    setUnreadCount(previous => Math.max(0, previous - 1));
  }, [notifications]);

  const dismissNotification = useCallback(async (id: string) => {
    const current = notifications.find(item => item.id === id);
    await dismissDesignerNotification(id);
    setNotifications(items => items.filter(item => item.id !== id));
    if (current && !current.read)
      setUnreadCount(previous => Math.max(0, previous - 1));
  }, [notifications]);

  const markAllRead = useCallback(async () => {
    await Promise.all([
      markAllDesignerNotificationsRead(),
      ...releases.filter(release => !readVersions.has(release.version))
        .map(release => markDesignerReleaseRead(release.version)),
    ]);
    setNotifications(items => items.map(item => ({ ...item, read: true })));
    setReadVersions(new Set(releases.map(release => release.version)));
    setUnreadCount(0);
  }, [readVersions, releases]);

  const setFeaturePreference = useCallback(async (featureId: string, enabled: boolean) => {
    const updated = await setDesignerFeaturePreference(featureId, enabled);
    setFeatures(items => items.map(item => item.id === featureId ? updated : item));
    notify.success(enabled
      ? t('productExperience.featureEnabled')
      : t('productExperience.featureDisabled'));
  }, [t]);

  const context = useMemo<ProductExperienceContextValue>(() => ({
    features,
    notifications,
    releases,
    readVersions,
    unreadCount,
    loading,
    openPanel,
    closePanel: () => setPanel(null),
    markReleaseRead,
    markNotificationRead,
    dismissNotification,
    markAllRead,
    setFeaturePreference,
  }), [
    dismissNotification,
    features,
    loading,
    markAllRead,
    markNotificationRead,
    markReleaseRead,
    notifications,
    openPanel,
    readVersions,
    releases,
    setFeaturePreference,
    unreadCount,
  ]);

  return (
    <ProductExperienceContext.Provider value={context}>
      {children}
      <PxaToaster />
      {panel && (
        <ProductExperiencePanel
          panel={panel}
          selectedVersion={selectedVersion}
          onSelectVersion={setSelectedVersion}
        />
      )}
    </ProductExperienceContext.Provider>
  );
};

export default ProductExperienceProvider;
