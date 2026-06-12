import React, { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  FiActivity,
  FiCheckCircle,
  FiChevronRight,
  FiFileText,
  FiFilePlus,
  FiGrid,
  FiLayout,
  FiShield,
  FiUpload,
} from 'react-icons/fi';
import { CATEGORIES, CATEGORY_CONFIG, TEMPLATES } from '@/data/templates';
import { useTemplateLoader } from '@/hooks/useTemplateLoader';

import AppHeader from '@/components/Layout/AppHeader';

const FEATURE_CARDS = [
  {
    title: 'Multi-format editor',
    copy: 'Design templates visually, then export to PDF, DOCX, ODT, XLSX, TIFF and more — or import existing PDFs and Word documents as editable designs.',
    icon: FiFileText,
  },
  {
    title: 'Template workflows',
    copy: 'Start from invoices, receipts, certificates and business documents. Add data bindings, loops, and expressions for dynamic content generation.',
    icon: FiGrid,
  },
  {
    title: 'Word-fidelity export',
    copy: 'DOCX output includes named styles, footnotes, bookmarks, track changes, document protection, custom properties and X.509 digital signatures.',
    icon: FiShield,
  },
];

const TOOL_LINKS = [
  { label: 'Edit PDF',          copy: 'Change text, images and layout.',                icon: FiFileText, comingSoon: false },
  { label: 'Create form',       copy: 'Add fields, QR codes and signatures.',           icon: FiFilePlus, comingSoon: false },
  { label: 'Import document',   copy: 'Open a PDF, DOCX, DOC, ODT or image file.',     icon: FiUpload,   comingSoon: false },
  { label: 'Sign DOCX',         copy: 'Apply an X.509 digital signature to a DOCX.',   icon: FiShield,   comingSoon: false },
];

const IndexPage: React.FC = () => {
  const navigate = useNavigate();
  const { loadBlank, loadFromFile } = useTemplateLoader();
  const [toast, setToast] = useState<string | null>(null);
  const importInputRef = useRef<HTMLInputElement>(null);
  const [importing, setImporting] = useState(false);

  const displayCategories = CATEGORIES.filter(c => c.id !== 'all');

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
      showToast(err instanceof Error ? err.message : 'Import failed');
    }
  };

  const handleToolClick = (label: string) => {
    if (label === 'Edit PDF') {
      navigate('/template');
    } else if (label === 'Create form') {
      loadBlank();
    } else if (label === 'Import document') {
      navigate('/importer');
    } else if (label === 'Sign DOCX') {
      showToast('Export your design as DOCX first, then use the Sign button in the Export modal.');
    }
  };

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
              <span>Design, import and export documents in one place</span>
            </div>
            <h1>Build, fill, and prepare business documents faster</h1>
            <p>
              Start from a template or import an existing PDF, Word, or ODT file. Add fields, signatures, and data bindings — then export to PDF, DOCX, ODT, TIFF and more.
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
              onClick={() => navigate('/template')}
              whileHover={{ y: -4 }}
              whileTap={{ scale: 0.99 }}
            >
              <span className="pdf-upload-icon"><FiLayout /></span>
              <strong>Start from a template</strong>
              <small>Browse {TEMPLATES.length} ready-made document templates</small>
              <span className="pdf-upload-action">Browse templates <FiChevronRight /></span>
            </motion.button>

            <motion.button
              className="pdf-upload-card"
              onClick={() => importInputRef.current?.click()}
              disabled={importing}
              title="Import a PDF, Word .doc/.docx, ODT, or image (PNG/JPG/WebP/…) as a Canvas design"
              whileHover={{ y: -4 }}
              whileTap={{ scale: 0.99 }}
            >
              <span className="pdf-upload-icon"><FiUpload /></span>
              <strong>{importing ? 'Importing…' : 'Import file'}</strong>
              <small>Open a PDF, Word, ODT or image file as an editable design</small>
              <span className="pdf-upload-action">Choose file <FiChevronRight /></span>
            </motion.button>

            <motion.button
              className="pdf-blank-card"
              onClick={() => loadBlank()}
              whileHover={{ y: -4 }}
              whileTap={{ scale: 0.99 }}
            >
              <span className="pdf-upload-icon"><FiFilePlus /></span>
              <strong>Blank canvas</strong>
              <small>Open the editor with an empty page — no starter elements</small>
              <span className="pdf-upload-action">Start blank <FiChevronRight /></span>
            </motion.button>

            <motion.button
              className="pdf-blank-card"
              onClick={() => loadBlank('code')}
              whileHover={{ y: -4 }}
              whileTap={{ scale: 0.99 }}
            >
              <span className="pdf-upload-icon" style={{ fontSize: 22 }}>{'{ }'}</span>
              <strong>Code Editor</strong>
              <small>Write JSON directly and see a live PDF preview as you type</small>
              <span className="pdf-upload-action">Open editor <FiChevronRight /></span>
            </motion.button>
          </div>
        </section>

        {/* Tools */}
        <section className="pdf-tools-section" id="tools">
          <div className="pdf-section-heading">
            <span>Every tool you need</span>
            <h2>Try these tools to get your PDFs done</h2>
          </div>
          <div className="pdf-tool-grid">
            {TOOL_LINKS.map(tool => {
              const Icon = tool.icon;
              return (
                <motion.button
                  key={tool.label}
                  className={`pdf-tool-card${tool.comingSoon ? ' is-coming-soon' : ''}`}
                  onClick={() => handleToolClick(tool.label)}
                  whileHover={tool.comingSoon ? {} : { y: -3 }}
                  whileTap={tool.comingSoon ? {} : { scale: 0.98 }}
                >
                  <Icon />
                  <strong>{tool.label}</strong>
                  <small>{tool.copy}</small>
                  <span>
                    {tool.comingSoon ? 'Coming soon' : 'Start now'}
                    {!tool.comingSoon && <FiChevronRight />}
                  </span>
                </motion.button>
              );
            })}
          </div>
        </section>

        {/* Trust band */}
        <section className="pdf-trust-band">
          <div>
            <strong>{TEMPLATES.length} templates</strong>
            <span>ready to open in the editor</span>
          </div>
          <div>
            <strong>10 export formats</strong>
            <span>PDF, DOCX, ODT, TIFF, HTML &amp; more</span>
          </div>
          <div>
            <strong>7 import formats</strong>
            <span>PDF, DOCX, PPTX, DOC, ODT, SVG, images</span>
          </div>
          <div>
            <strong>100% browser-based</strong>
            <span>no account required</span>
          </div>
        </section>

        {/* Category grid */}
        <section className="pdf-template-section" id="categories">
          <div className="pdf-template-toolbar">
            <div className="pdf-section-heading">
              <span>Template categories</span>
              <h2>Choose a document type to explore</h2>
            </div>
            <button
              className="pdf-outline-button"
              onClick={() => navigate('/template')}
            >
              Browse all templates
              <FiChevronRight style={{ marginLeft: 4 }} />
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
                  onClick={() => navigate(`/template?category=${cat.id}`)}
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
                    <span className="idx-category-name">{cat.name}</span>
                    <span className="idx-category-count">{cat.count}</span>
                  </div>
                  <p className="idx-category-desc">{cfg.description}</p>
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
            <span>Feature-rich platform</span>
            <h2>Everything you need to manage PDF templates</h2>
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
            <h2>Designed for sensitive business documents</h2>
            <p>Keep the interface calm, structured and predictable for contracts, invoices, HR documents and approvals.</p>
          </div>
          <button className="pdf-outline-button" onClick={() => navigate('/template')}>
            Start designing
          </button>
        </section>

        {/* Usage strip */}
        <UsageStrip />
      </main>
    </div>
  );
};

const UsageStrip: React.FC = () => {
  const count = parseInt(localStorage.getItem('canvas_docs_opened') ?? '0', 10);
  const lastName = localStorage.getItem('canvas_last_template');

  if (count === 0) return null;

  return (
    <section className="pdf-usage-strip">
      <FiActivity />
      <div className="pdf-usage-stat">
        <strong>{count}</strong>
        <span>{count === 1 ? 'document' : 'documents'} opened this session</span>
      </div>
      {lastName && (
        <div className="pdf-usage-stat">
          <strong>{lastName}</strong>
          <span>last opened template</span>
        </div>
      )}
    </section>
  );
};

export default IndexPage;
