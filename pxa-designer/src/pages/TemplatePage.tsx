import React, { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import {
  FiChevronRight,
  FiSearch,
  FiX,
} from 'react-icons/fi';
import CategoryFilter from '@/components/Gallery/CategoryFilter';
import TemplateCard from '@/components/Gallery/TemplateCard';
import TemplateMiniPreview from '@/components/Gallery/TemplateMiniPreview';
import { TEMPLATES, CATEGORIES, CATEGORY_CONFIG } from '@/data/templates';
import type { TemplateDefinition } from '@/data/templates';
import { useTemplateLoader } from '@/hooks/useTemplateLoader';

type SortOrder = 'default' | 'alpha' | 'category';

// ─── Template Detail Panel ────────────────────────────────────────────────────

interface DetailPanelProps {
  template: TemplateDefinition | null;
  onClose: () => void;
  onUse: (template: TemplateDefinition) => void;
}

const TemplateDetailPanel: React.FC<DetailPanelProps> = ({ template, onClose, onUse }) => {
  if (!template) return null;
  const cfg = CATEGORY_CONFIG[template.category] ?? CATEGORY_CONFIG['letter'];
  const Icon = cfg.icon;

  return (
    <>
      <div className="tpl-detail-backdrop" onClick={onClose} />
      <motion.aside
        className="tpl-detail-panel"
        initial={{ x: '100%' }}
        animate={{ x: 0 }}
        exit={{ x: '100%' }}
        transition={{ type: 'spring', stiffness: 320, damping: 34 }}
      >
        <div className="tpl-detail-header">
          <button className="tpl-detail-close" onClick={onClose} aria-label="Close">
            <FiX size={18} />
          </button>
        </div>

        <div className="tpl-detail-preview">
          <TemplateMiniPreview templateId={template.id} />
        </div>

        <div className="tpl-detail-body">
          <span
            className="tpl-category-badge"
            style={{ background: cfg.bg, color: cfg.text, borderColor: cfg.accent + '33' }}
          >
            <Icon size={12} color={cfg.text} />
            {CATEGORIES.find(c => c.id === template.category)?.name ?? template.category}
          </span>

          <h2 className="tpl-detail-name">{template.name}</h2>
          <p className="tpl-detail-desc">{template.description}</p>

          <div className="tpl-detail-tags">
            {template.tags.slice(0, 4).map(tag => (
              <span key={tag} className="tpl-detail-tag">{tag}</span>
            ))}
          </div>

          <button
            className="tpl-use-button"
            onClick={() => onUse(template)}
          >
            Use this template
            <FiChevronRight size={18} />
          </button>
        </div>
      </motion.aside>
    </>
  );
};

// ─── Template Page ────────────────────────────────────────────────────────────

const TemplatePage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const { loadTemplate } = useTemplateLoader();

  const [selectedCategory, setSelectedCategory] = useState(
    searchParams.get('category') ?? 'all'
  );
  const [selectedFormat, setSelectedFormat] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [sortOrder, setSortOrder] = useState<SortOrder>('default');
  const [selectedTemplate, setSelectedTemplate] = useState<TemplateDefinition | null>(null);
  const handleCategoryChange = (cat: string) => {
    setSelectedCategory(cat);
    setSelectedTemplate(null);
    if (cat === 'all') {
      setSearchParams({});
    } else {
      setSearchParams({ category: cat });
    }
  };

  const filteredTemplates = useMemo(() => {
    let list = TEMPLATES.filter(t => {
      const matchesCat = selectedCategory === 'all' || t.category === selectedCategory;
      const matchesFmt = selectedFormat === 'all' || (t.format ?? 'portrait') === selectedFormat;
      const text = `${t.name} ${t.description} ${t.tags.join(' ')}`.toLowerCase();
      return matchesCat && matchesFmt && text.includes(searchQuery.toLowerCase());
    });

    if (sortOrder === 'alpha') {
      list = [...list].sort((a, b) => a.name.localeCompare(b.name));
    } else if (sortOrder === 'category') {
      list = [...list].sort((a, b) => {
        const catCmp = a.category.localeCompare(b.category);
        return catCmp !== 0 ? catCmp : a.name.localeCompare(b.name);
      });
    }

    return list;
  }, [selectedCategory, selectedFormat, searchQuery, sortOrder]);

  const clearSearch = () => {
    setSearchQuery('');
    setSelectedCategory('all');
    setSearchParams({});
    setSelectedTemplate(null);
  };

  return (
    <div className="pdf-home">
      <main>
        {/* Toolbar */}
        <section className="tpl-toolbar">
          <div className="tpl-toolbar-left">
            <h1 className="tpl-toolbar-heading">Templates</h1>
            <span className="tpl-toolbar-count">
              {filteredTemplates.length} of {TEMPLATES.length}
            </span>
          </div>
          <div className="tpl-toolbar-right">
            <label className="pdf-search">
              <FiSearch />
              <input
                type="text"
                placeholder="Search templates…"
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
              />
              {searchQuery && (
                <button className="pdf-search-clear" onClick={() => setSearchQuery('')} aria-label="Clear search">
                  <FiX size={14} />
                </button>
              )}
            </label>
            <select
              className="pdf-sort-select"
              value={selectedFormat}
              onChange={e => setSelectedFormat(e.target.value)}
              aria-label="Filter by format"
            >
              <option value="all">All formats</option>
              <option value="portrait">Portrait</option>
              <option value="landscape">Landscape</option>
              <option value="square">Square</option>
              <option value="widescreen">Widescreen</option>
            </select>
            <select
              className="pdf-sort-select"
              value={sortOrder}
              onChange={e => setSortOrder(e.target.value as SortOrder)}
              aria-label="Sort templates"
            >
              <option value="default">Default order</option>
              <option value="alpha">A – Z</option>
              <option value="category">By category</option>
            </select>
          </div>
        </section>

        {/* Category filter */}
        <div className="tpl-category-wrap">
          <CategoryFilter
            categories={CATEGORIES}
            selectedCategory={selectedCategory}
            onCategoryChange={handleCategoryChange}
          />
        </div>

        {/* Template grid */}
        <section className="pdf-template-section" style={{ paddingTop: 0 }}>
          {filteredTemplates.length > 0 ? (
            <motion.div layout className="pdf-template-grid">
              {filteredTemplates.map((template, index) => (
                <motion.div
                  key={template.id}
                  initial={{ opacity: 0, y: 18 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: index * 0.025, duration: 0.2 }}
                  onClick={() => setSelectedTemplate(template)}
                  style={{ cursor: 'pointer' }}
                >
                  <TemplateCard
                    template={template}
                    onSelect={() => setSelectedTemplate(template)}
                  />
                </motion.div>
              ))}
            </motion.div>
          ) : (
            <div className="pdf-empty-results">
              <FiSearch />
              <h3>No templates found</h3>
              <p>Try a different search term or category.</p>
              <button className="pdf-outline-button" onClick={clearSearch}>
                Show all templates
              </button>
            </div>
          )}
        </section>
      </main>

      {/* Detail panel */}
      <AnimatePresence>
        {selectedTemplate && (
          <TemplateDetailPanel
            template={selectedTemplate}
            onClose={() => setSelectedTemplate(null)}
            onUse={tpl => loadTemplate(tpl)}
          />
        )}
      </AnimatePresence>
    </div>
  );
};

export default TemplatePage;
