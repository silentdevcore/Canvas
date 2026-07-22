import React, { Suspense, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import '@/i18n';
import { RTL_LANGUAGES, type SupportedLanguage } from '@/i18n';

interface LocaleProviderProps {
  children: React.ReactNode;
}

const LocaleProvider: React.FC<LocaleProviderProps> = ({ children }) => {
  const { i18n } = useTranslation();

  useEffect(() => {
    const applyDirection = (language: string) => {
      const isRtl = RTL_LANGUAGES.includes(language as SupportedLanguage);
      document.documentElement.lang = language;
      document.documentElement.dir = isRtl ? 'rtl' : 'ltr';
    };
    applyDirection(i18n.language);
    i18n.on('languageChanged', applyDirection);
    return () => i18n.off('languageChanged', applyDirection);
  }, [i18n]);

  return (
    <Suspense fallback={null}>
      {children}
    </Suspense>
  );
};

export default LocaleProvider;
