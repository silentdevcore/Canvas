import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { FiMenu, FiX } from 'react-icons/fi';

interface AppHeaderProps {
  activePage: 'home' | 'pdf' | 'spreadsheet' | 'docs';
}

const AppHeader: React.FC<AppHeaderProps> = ({ activePage }) => {
  const navigate = useNavigate();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <>
      {mobileMenuOpen && (
        <div className="pdf-mobile-menu" role="dialog" aria-label="Mobile menu">
          <div className="pdf-mobile-menu-header">
            <span className="pdf-logo"><span>PXA</span><strong>Designer</strong></span>
            <button
              className="pdf-mobile-menu-close"
              onClick={() => setMobileMenuOpen(false)}
              aria-label="Close menu"
            >
              <FiX />
            </button>
          </div>
          <nav className="pdf-mobile-nav">
            <button
              className={activePage === 'home' ? 'is-active' : ''}
              onClick={() => { navigate('/'); setMobileMenuOpen(false); }}
            >
              Home
            </button>
            <button
              className={activePage === 'pdf' ? 'is-active' : ''}
              onClick={() => { navigate('/pdf'); setMobileMenuOpen(false); }}
            >
              PDF
            </button>
            <button
              className={activePage === 'spreadsheet' ? 'is-active' : ''}
              onClick={() => { navigate('/spreadsheet'); setMobileMenuOpen(false); }}
            >
              Spreadsheet
            </button>
            <button
              className={activePage === 'docs' ? 'is-active' : ''}
              onClick={() => { navigate('/docs'); setMobileMenuOpen(false); }}
            >
              Documentation
            </button>
          </nav>
        </div>
      )}

      <header className="pdf-nav">
        <button className="pdf-logo" onClick={() => navigate('/')} aria-label="Power Dox Automation home">
          <span>PXA</span>
          <strong>Designer</strong>
        </button>

        <nav className="pdf-nav-links" aria-label="Primary navigation">
          <button className={activePage === 'home' ? 'is-active' : ''} onClick={() => navigate('/')}>
            Home
          </button>
          <button className={activePage === 'pdf' ? 'is-active' : ''} onClick={() => navigate('/pdf')}>
            PDF
          </button>
          <button className={activePage === 'spreadsheet' ? 'is-active' : ''} onClick={() => navigate('/spreadsheet')}>
            Spreadsheet
          </button>
          <button className={activePage === 'docs' ? 'is-active' : ''} onClick={() => navigate('/docs')}>
            Documentation
          </button>
        </nav>

        <div className="pdf-nav-actions">
          <button className="pdf-menu-button" aria-label="Open menu" onClick={() => setMobileMenuOpen(true)}>
            <FiMenu />
          </button>
        </div>
      </header>
    </>
  );
};

export default AppHeader;
