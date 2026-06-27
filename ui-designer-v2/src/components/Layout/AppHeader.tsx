import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { FiMenu, FiX } from 'react-icons/fi';

interface AppHeaderProps {
  activePage: 'home' | 'templates' | 'docs' | 'migrations' | 'importer' | 'viewer' | 'spreadsheet';
}

const AppHeader: React.FC<AppHeaderProps> = ({ activePage }) => {
  const navigate = useNavigate();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <>
      {mobileMenuOpen && (
        <div className="pdf-mobile-menu" role="dialog" aria-label="Mobile menu">
          <div className="pdf-mobile-menu-header">
            <span className="pdf-logo"><span>UI</span><strong>Designer</strong></span>
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
              className={activePage === 'templates' ? 'is-active' : ''}
              onClick={() => { navigate('/template'); setMobileMenuOpen(false); }}
            >
              Templates
            </button>
            <button
              className={activePage === 'importer' ? 'is-active' : ''}
              onClick={() => { navigate('/importer'); setMobileMenuOpen(false); }}
            >
              Importer
            </button>
            <button
              className={activePage === 'viewer' ? 'is-active' : ''}
              onClick={() => { navigate('/pdf-viewer'); setMobileMenuOpen(false); }}
            >
              PDF Viewer
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
              Docs
            </button>
            <button
              className={activePage === 'migrations' ? 'is-active' : ''}
              onClick={() => { navigate('/migrations'); setMobileMenuOpen(false); }}
            >
              Migrations
            </button>
          </nav>
        </div>
      )}

      <header className="pdf-nav">
        <button className="pdf-logo" onClick={() => navigate('/')} aria-label="UI Designer home">
          <span>UI</span>
          <strong>Designer</strong>
        </button>

        <nav className="pdf-nav-links" aria-label="Primary navigation">
          <button className={activePage === 'home' ? 'is-active' : ''} onClick={() => navigate('/')}>
            Home
          </button>
          <button className={activePage === 'templates' ? 'is-active' : ''} onClick={() => navigate('/template')}>
            Templates
          </button>
          <button className={activePage === 'importer' ? 'is-active' : ''} onClick={() => navigate('/importer')}>
            Importer
          </button>
          <button className={activePage === 'viewer' ? 'is-active' : ''} onClick={() => navigate('/pdf-viewer')}>
            PDF Viewer
          </button>
          <button className={activePage === 'spreadsheet' ? 'is-active' : ''} onClick={() => navigate('/spreadsheet')}>
            Spreadsheet
          </button>
          <button className={activePage === 'docs' ? 'is-active' : ''} onClick={() => navigate('/docs')}>
            Docs
          </button>
          <button className={activePage === 'migrations' ? 'is-active' : ''} onClick={() => navigate('/migrations')}>
            Migrations
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
