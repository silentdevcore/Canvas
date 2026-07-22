import React, { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';
import {
  FiActivity,
  FiCheckCircle,
  FiChevronRight,
  FiCode,
  FiFileText,
  FiFilePlus,
  FiGitMerge,
  FiGrid,
  FiLayout,
  FiRefreshCw,
  FiShield,
  FiUpload,
  FiEye,
} from 'react-icons/fi';
import { CATEGORIES, CATEGORY_CONFIG, TEMPLATES } from '@/data/templates';
import { useTemplateLoader } from '@/hooks/useTemplateLoader';

import AppHeader from '@/components/Layout/AppHeader';

interface ToolLink {
  label: string;
  copy: string;
  icon: React.ElementType;
  path?: string;
  disabled?: boolean;
  onClick?: () => void;
}

const IndexPage: React.FC = () => {
  const navigate = useNavigate();
  const { t } = useTranslation('home');
  const { loadBlank, loadFromFile } = useTemplateLoader();
  const [toast, setToast] = useState<string | null>(null);
  const importInputRef = useRef<HTMLInputElement>(null);
  const [importing, setImporting] = useState(false);

  const displayCategories = CATEGORIES.filter(c => c.id !== 'all' && c.id !== 'arabic');

  const FEATURE_CARDS = [
    { title: t('featureCards.multiFormat.title'), copy: t('featureCards.multiFormat.copy'), icon: FiFileText },
    { title: t('featureCards.templateWorkflows.title'), copy: t('featureCards.templateWorkflows.copy'), icon: FiGrid },
    { title: t('featureCards.wordFidelity.title'), copy: t('featureCards.wordFidelity.copy'), icon: FiShield },
    { title: t('featureCards.spreadsheetFormulas.title'), copy: t('featureCards.spreadsheetFormulas.copy'), icon: FiGrid },
  ];

  const showToast = (msg: string) => {
    setToast(msg);
    setTimeout(() => setToast(null), 3000);
  };

  const handleFileImport = async (file: File) => {
    setImporting(true);
    try {
      await loadFromFile(file);
    } catch (err) {
      setImporting(false);
      showToast(err instanceof Error ? err.message : t('hero.importCard.importFailed'));
    }
  };

  // One card per PDF hub sidebar item (see PdfLayout.tsx), plus "Sign DOCX"
  // which has no hub-sidebar/route equivalent — it's an Export-modal action.
  const PDF_TOOLS: ToolLink[] = [
    { label: t('pdfTools.createPdf.label'), copy: t('pdfTools.createPdf.copy'), icon: FiFilePlus, onClick: () => loadBlank() },
    { label: t('pdfTools.editPdf.label'), copy: t('pdfTools.editPdf.copy'), icon: FiCode, onClick: () => loadBlank('code') },
    { label: t('pdfTools.useTemplate.label'), copy: t('pdfTools.useTemplate.copy', { count: TEMPLATES.length }), icon: FiLayout, path: '/pdf/template' },
    { label: t('pdfTools.importPdf.label'), copy: t('pdfTools.importPdf.copy'), icon: FiUpload, path: '/pdf/import' },
    { label: t('pdfTools.convertToPdf.label'), copy: t('pdfTools.convertToPdf.copy'), icon: FiEye, path: '/pdf/convert' },
    { label: t('pdfTools.pdfViewer.label'), copy: t('pdfTools.pdfViewer.copy'), icon: FiFileText, path: '/pdf/viewer' },
    { label: t('pdfTools.migrations.label'), copy: t('pdfTools.migrations.copy'), icon: FiGitMerge, path: '/pdf/migrations' },
    { label: t('pdfTools.signDocx.label'), copy: t('pdfTools.signDocx.copy'), icon: FiShield, onClick: () => showToast(t('pdfTools.signDocx.toast')) },
  ];

  // One card per Spreadsheet hub sidebar item — Spreadsheet's first appearance on Home.
  const SPREADSHEET_TOOLS: ToolLink[] = [
    { label: t('spreadsheetTools.createSpreadsheet.label'), copy: t('spreadsheetTools.createSpreadsheet.copy'), icon: FiGrid, path: '/spreadsheet/create' },
    { label: t('spreadsheetTools.editSpreadsheet.label'), copy: t('spreadsheetTools.editSpreadsheet.copy'), icon: FiCode, path: '/spreadsheet/edit' },
    { label: t('spreadsheetTools.importSpreadsheet.label'), copy: t('spreadsheetTools.importSpreadsheet.copy'), icon: FiUpload, path: '/spreadsheet/import' },
    { label: t('spreadsheetTools.convertToSpreadsheet.label'), copy: t('spreadsheetTools.convertToSpreadsheet.copy'), icon: FiRefreshCw, disabled: true },
    { label: t('spreadsheetTools.migrations.label'), copy: t('spreadsheetTools.migrations.copy'), icon: FiGitMerge, path: '/spreadsheet/migrations' },
  ];

  const handleToolClick = (tool: ToolLink) => {
    if (tool.disabled) return;
    if (tool.onClick) { tool.onClick(); return; }
    if (tool.path) navigate(tool.path);
  };

  const renderToolGrid = (tools: ToolLink[]) => (
    <div className="pdf-tool-grid">
      {tools.map(tool => {
        const Icon = tool.icon;
        return (
          <motion.button
            key={tool.label}
            className={`pdf-tool-card${tool.disabled ? ' is-coming-soon' : ''}`}
            onClick={() => handleToolClick(tool)}
            disabled={tool.disabled}
            whileHover={tool.disabled ? {} : { y: -3 }}
            whileTap={tool.disabled ? {} : { scale: 0.98 }}
          >
            <Icon />
            <strong>{tool.label}</strong>
            <small>{tool.copy}</small>
            <span>
              {tool.disabled ? t('toolGrid.comingSoon') : t('toolGrid.startNow')}
              {!tool.disabled && <FiChevronRight />}
            </span>
          </motion.button>
        );
      })}
    </div>
  );

  return (
    <div className="pdf-home">
      {toast && (
        <div className="pdf-toast" role="status" aria-live="polite">
          {toast}
        </div>
      )}

      <AppHeader activePage="home" />

      <main>

        {/* Hero */}
        <section className="pdf-hero">
          <div className="pdf-hero-copy">
            <div className="pdf-eyebrow">
              <FiCheckCircle />
              <span>{t('hero.eyebrow')}</span>
            </div>
            <h1>{t('hero.title')}</h1>
            <p>
              {t('hero.description')}
            </p>
          </div>

          <input
            ref={importInputRef}
            type="file"
            accept=".pdf,.doc,.docx,.odt,.png,.jpg,.jpeg,.gif,.webp,.bmp,.tiff,.tif,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.oasis.opendocument.text,image/png,image/jpeg,image/gif,image/webp,image/bmp,image/tiff"
            style={{ display: 'none' }}
            onChange={e => { const f = e.target.files?.[0]; if (f) handleFileImport(f); e.target.value = ''; }}
          />

          <div className="pdf-hero-cards">
            <motion.button
              className="pdf-upload-card"
              onClick={() => navigate('/pdf/template')}
              whileHover={{ y: -4 }}
              whileTap={{ scale: 0.99 }}
            >
              <span className="pdf-upload-icon"><FiLayout /></span>
              <strong>{t('hero.templateCard.title')}</strong>
              <small>{t('hero.templateCard.copy', { count: TEMPLATES.length })}</small>
              <span className="pdf-upload-action">{t('hero.templateCard.action')} <FiChevronRight /></span>
            </motion.button>

            <motion.button
              className="pdf-upload-card"
              onClick={() => importInputRef.current?.click()}
              disabled={importing}
              title={t('hero.importCard.tooltip')}
              whileHover={{ y: -4 }}
              whileTap={{ scale: 0.99 }}
            >
              <span className="pdf-upload-icon"><FiUpload /></span>
              <strong>{importing ? t('hero.importCard.importing') : t('hero.importCard.title')}</strong>
              <small>{t('hero.importCard.copy')}</small>
              <span className="pdf-upload-action">{t('hero.importCard.action')} <FiChevronRight /></span>
            </motion.button>

            <motion.button
              className="pdf-blank-card"
              onClick={() => loadBlank()}
              whileHover={{ y: -4 }}
              whileTap={{ scale: 0.99 }}
            >
              <span className="pdf-upload-icon"><FiFilePlus /></span>
              <strong>{t('hero.blankCard.title')}</strong>
              <small>{t('hero.blankCard.copy')}</small>
              <span className="pdf-upload-action">{t('hero.blankCard.action')} <FiChevronRight /></span>
            </motion.button>

            <motion.button
              className="pdf-blank-card"
              onClick={() => loadBlank('code')}
              whileHover={{ y: -4 }}
              whileTap={{ scale: 0.99 }}
            >
              <span className="pdf-upload-icon" style={{ fontSize: 22 }}>{'{ }'}</span>
              <strong>{t('hero.codeCard.title')}</strong>
              <small>{t('hero.codeCard.copy')}</small>
              <span className="pdf-upload-action">{t('hero.codeCard.action')} <FiChevronRight /></span>
            </motion.button>
          </div>
        </section>

        {/* PDF tools */}
        <section className="pdf-tools-section" id="tools">
          <div className="pdf-section-heading">
            <span>{t('pdfTools.sectionLabel')}</span>
            <h2>{t('pdfTools.sectionTitle')}</h2>
          </div>
          {renderToolGrid(PDF_TOOLS)}
        </section>

        {/* Spreadsheet tools */}
        <section className="pdf-tools-section" id="spreadsheet-tools">
          <div className="pdf-section-heading">
            <span>{t('spreadsheetTools.sectionLabel')}</span>
            <h2>{t('spreadsheetTools.sectionTitle')}</h2>
          </div>
          {renderToolGrid(SPREADSHEET_TOOLS)}
        </section>

        {/* Trust band */}
        <section className="pdf-trust-band">
          <div>
            <strong>{t('trustBand.templatesCount', { count: TEMPLATES.length })}</strong>
            <span>{t('trustBand.templatesSub')}</span>
          </div>
          <div>
            <strong>{t('trustBand.exportFormats')}</strong>
            <span>{t('trustBand.exportFormatsSub')}</span>
          </div>
          <div>
            <strong>{t('trustBand.importFormats')}</strong>
            <span>{t('trustBand.importFormatsSub')}</span>
          </div>
          <div>
            <strong>{t('trustBand.browserBased')}</strong>
            <span>{t('trustBand.browserBasedSub')}</span>
          </div>
        </section>

        {/* Category grid */}
        <section className="pdf-template-section" id="categories">
          <div className="pdf-template-toolbar">
            <div className="pdf-section-heading">
              <span>{t('categories.sectionLabel')}</span>
              <h2>{t('categories.sectionTitle')}</h2>
            </div>
            <button
              className="pdf-outline-button"
              onClick={() => navigate('/pdf/template')}
            >
              {t('categories.browseAll')}
              <FiChevronRight style={{ marginInlineStart: 4 }} />
            </button>
          </div>

          <div className="idx-category-grid">
            {displayCategories.map((cat, index) => {
              const cfg = CATEGORY_CONFIG[cat.id];
              if (!cfg) return null;
              const Icon = cfg.icon;
              return (
                <motion.button
                  key={cat.id}
                  className="idx-category-card"
                  onClick={() => navigate(`/pdf/template?category=${cat.id}`)}
                  initial={{ opacity: 0, y: 16 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: index * 0.04, duration: 0.2 }}
                  whileHover={{ y: -3 }}
                  whileTap={{ scale: 0.98 }}
                  style={{ borderColor: 'transparent' }}
                >
                  <span
                    className="idx-category-icon"
                    style={{ background: cfg.bg, color: cfg.accent }}
                  >
                    <Icon size={22} />
                  </span>
                  <div className="idx-category-info">
                    <span className="idx-category-name">{t(`templates:category.${cat.id}.name`, { defaultValue: cat.name })}</span>
                    <span className="idx-category-count">{cat.count}</span>
                  </div>
                  <p className="idx-category-desc">{t(`templates:category.${cat.id}.description`, { defaultValue: cfg.description })}</p>
                  <span className="idx-category-arrow">
                    <FiChevronRight size={16} />
                  </span>
                </motion.button>
              );
            })}
          </div>
        </section>

        {/* Features */}
        <section className="pdf-feature-section" id="features">
          <div className="pdf-section-heading">
            <span>{t('features.sectionLabel')}</span>
            <h2>{t('features.sectionTitle')}</h2>
          </div>
          <div className="pdf-feature-grid">
            {FEATURE_CARDS.map(feature => {
              const Icon = feature.icon;
              return (
                <article className="pdf-feature-card" key={feature.title}>
                  <Icon />
                  <h3>{feature.title}</h3>
                  <p>{feature.copy}</p>
                </article>
              );
            })}
          </div>
        </section>

        {/* Security strip */}
        <section className="pdf-security-strip" id="security">
          <FiShield />
          <div>
            <h2>{t('security.title')}</h2>
            <p>{t('security.copy')}</p>
          </div>
          <button className="pdf-outline-button" onClick={() => navigate('/pdf/template')}>
            {t('security.action')}
          </button>
        </section>

        {/* Usage strip */}
        <UsageStrip />
      </main>
    </div>
  );
};

const UsageStrip: React.FC = () => {
  const { t } = useTranslation('home');
  // `pxa_*` are the current keys; `canvas_*` are read as a fallback so a
  // count/name saved before the rename isn't lost.
  const count = parseInt(
    localStorage.getItem('pxa_docs_opened') ?? localStorage.getItem('canvas_docs_opened') ?? '0',
    10,
  );
  const lastName = localStorage.getItem('pxa_last_template') ?? localStorage.getItem('canvas_last_template');

  if (count === 0) return null;

  return (
    <section className="pdf-usage-strip">
      <FiActivity />
      <div className="pdf-usage-stat">
        <strong>{count}</strong>
        <span>{count === 1 ? t('usage.documentSingular') : t('usage.documentPlural')} {t('usage.openedThisSession')}</span>
      </div>
      {lastName && (
        <div className="pdf-usage-stat">
          <strong>{lastName}</strong>
          <span>{t('usage.lastOpenedTemplate')}</span>
        </div>
      )}
    </section>
  );
};

export default IndexPage;
