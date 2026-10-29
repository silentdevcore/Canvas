import React from 'react';
import { useTranslation } from 'react-i18next';
import { SUPPORTED_LANGUAGES, type SupportedLanguage } from '@/i18n';

const LANGUAGE_NAMES: Record<SupportedLanguage, string> = {
  en: 'English',
  de: 'Deutsch',
  fr: 'Français',
  es: 'Español',
  it: 'Italiano',
  ar: 'العربية',
};

interface LanguageSwitcherProps {
  className?: string;
}

const LanguageSwitcher: React.FC<LanguageSwitcherProps> = ({ className }) => {
  const { t, i18n } = useTranslation('common');

  return (
    <select
      className={className ? `pdf-language-switcher ${className}` : 'pdf-language-switcher'}
      aria-label={t('languageSwitcher.chooseLanguage')}
      value={i18n.language}
      onChange={(event) => i18n.changeLanguage(event.target.value)}
    >
      {SUPPORTED_LANGUAGES.map((code) => (
        <option key={code} value={code}>{LANGUAGE_NAMES[code]}</option>
      ))}
    </select>
  );
};

export default LanguageSwitcher;
