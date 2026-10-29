import React from 'react';
import { useTranslation } from 'react-i18next';
import { useEditorStore } from '@/store';
import { isDocumentRtlLanguage } from '@/utils/documentDirection';

const LANG_LABELS: Record<string, string> = {
  en: '🇬🇧 EN', de: '🇩🇪 DE', fr: '🇫🇷 FR', es: '🇪🇸 ES', it: '🇮🇹 IT',
  pt: '🇧🇷 PT', ru: '🇷🇺 RU', el: '🇬🇷 EL', ar: '🇸🇦 AR',
  zh: '🇨🇳 ZH', ja: '🇯🇵 JA', ko: '🇰🇷 KO', hi: '🇮🇳 HI', th: '🇹🇭 TH',
};

export const LanguageTabBar: React.FC = () => {
  const { t } = useTranslation('editor');
  const { pageSettings, currentPreviewLanguage, setCurrentPreviewLanguage } = useEditorStore();
  const langs = pageSettings.activeLanguages ?? [];

  if (langs.length < 2) return null;

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      gap: 2,
      padding: '4px 12px',
      background: 'var(--editor-surface, #f8fafc)',
      borderBottom: '1px solid var(--editor-border, #e2e8f0)',
      flexShrink: 0,
    }}>
      <span style={{ fontSize: 11, color: '#64748b', marginRight: 6 }}>
        {t('languageTabs.language')}:
      </span>
      {langs.map(lang => {
        const isActive = currentPreviewLanguage === lang;
        const isRtl = isDocumentRtlLanguage(lang);
        return (
          <button
            key={lang}
            onClick={() => setCurrentPreviewLanguage(lang)}
            title={isRtl ? `${lang} (RTL)` : lang}
            style={{
              padding: '3px 10px',
              fontSize: 11,
              fontWeight: isActive ? 600 : 400,
              border: `1px solid ${isActive ? 'var(--editor-accent, #6366f1)' : 'var(--editor-border, #e2e8f0)'}`,
              borderRadius: 4,
              background: isActive ? 'var(--editor-accent, #6366f1)' : 'white',
              color: isActive ? 'white' : '#374151',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            {LANG_LABELS[lang] ?? lang.toUpperCase()}
            {isRtl && (
              <span style={{ fontSize: 9, opacity: 0.8, fontFamily: 'monospace' }}>RTL</span>
            )}
          </button>
        );
      })}
    </div>
  );
};

export default LanguageTabBar;
