import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FiMenu, FiX } from 'react-icons/fi';
import LanguageSwitcher from '@/components/Layout/LanguageSwitcher';

interface AppHeaderProps {
  activePage: 'home' | 'pdf' | 'spreadsheet' | 'docs';
}

const AppHeader: React.FC<AppHeaderProps> = ({ activePage }) => {
  const navigate = useNavigate();
  const { t } = useTranslation('common');
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <>
      {mobileMenuOpen && (
        <div className="pdf-mobile-menu" role="dialog" aria-label={t('nav.mobileMenu')}>
          <div className="pdf-mobile-menu-header">
            <span className="pdf-logo"><span>PXA</span><strong>Designer</strong></span>
            <button
              className="pdf-mobile-menu-close"
              onClick={() => setMobileMenuOpen(false)}
              aria-label={t('nav.closeMenu')}
            >
              <FiX />
            </button>
          </div>
          <nav className="pdf-mobile-nav">
            <button
              className={activePage === 'home' ? 'is-active' : ''}
              onClick={() => { navigate('/'); setMobileMenuOpen(false); }}
            >
              {t('nav.home')}
            </button>
            <button
              className={activePage === 'pdf' ? 'is-active' : ''}
              onClick={() => { navigate('/pdf'); setMobileMenuOpen(false); }}
            >
              {t('nav.pdf')}
            </button>
            <button
              className={activePage === 'spreadsheet' ? 'is-active' : ''}
              onClick={() => { navigate('/spreadsheet'); setMobileMenuOpen(false); }}
            >
              {t('nav.spreadsheet')}
            </button>
            <button
              className={activePage === 'docs' ? 'is-active' : ''}
              onClick={() => { navigate('/docs'); setMobileMenuOpen(false); }}
            >
              {t('nav.docs')}
            </button>
            <LanguageSwitcher className="pdf-mobile-language-switcher" />
          </nav>
        </div>
      )}

      <header className="pdf-nav">
        <button className="pdf-logo" onClick={() => navigate('/')} aria-label={t('nav.logoHome')}>
          <span>PXA</span>
          <strong>Designer</strong>
        </button>

        <nav className="pdf-nav-links" aria-label="Primary navigation">
          <button className={activePage === 'home' ? 'is-active' : ''} onClick={() => navigate('/')}>
            {t('nav.home')}
          </button>
          <button className={activePage === 'pdf' ? 'is-active' : ''} onClick={() => navigate('/pdf')}>
            {t('nav.pdf')}
          </button>
          <button className={activePage === 'spreadsheet' ? 'is-active' : ''} onClick={() => navigate('/spreadsheet')}>
            {t('nav.spreadsheet')}
          </button>
          <button className={activePage === 'docs' ? 'is-active' : ''} onClick={() => navigate('/docs')}>
            {t('nav.docs')}
          </button>
        </nav>

        <div className="pdf-nav-actions">
          <LanguageSwitcher />
          <button className="pdf-menu-button" aria-label={t('nav.openMenu')} onClick={() => setMobileMenuOpen(true)}>
            <FiMenu />
          </button>
        </div>
      </header>
    </>
  );
};

export default AppHeader;
