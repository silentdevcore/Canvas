import React, { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation(['gallery', 'templates']);
  if (!template) return null;
  const cfg = CATEGORY_CONFIG[template.category] ?? CATEGORY_CONFIG['letter'];
  const Icon = cfg.icon;
  const categoryName = CATEGORIES.find(c => c.id === template.category)?.name ?? template.category;

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
          <button className="tpl-detail-close" onClick={onClose} aria-label={t('detail.close', { ns: 'gallery' })}>
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
            {t(`category.${template.category}.name`, { ns: 'templates', defaultValue: categoryName })}
          </span>

          <h2 className="tpl-detail-name">{t(`template.${template.id}.name`, { ns: 'templates', defaultValue: template.name })}</h2>
          <p className="tpl-detail-desc">{t(`template.${template.id}.description`, { ns: 'templates', defaultValue: template.description })}</p>

          <div className="tpl-detail-tags">
            {template.tags.slice(0, 4).map(tag => (
              <span key={tag} className="tpl-detail-tag">{tag}</span>
            ))}
          </div>

          <button
            className="tpl-use-button"
            onClick={() => onUse(template)}
          >
            {t('detail.useTemplate', { ns: 'gallery' })}
            <FiChevronRight size={18} />
          </button>
        </div>
      </motion.aside>
    </>
  );
};

// ─── Template Page ────────────────────────────────────────────────────────────

const TemplatePage: React.FC = () => {
  const { t } = useTranslation(['gallery', 'templates']);
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

  const translatedCategories = CATEGORIES.map(cat => ({
    ...cat,
    name: t(`category.${cat.id}.name`, { ns: 'templates', defaultValue: cat.name }),
  }));

  return (
    <div className="pdf-home">
      <main>
        {/* Toolbar */}
        <section className="tpl-toolbar">
          <div className="tpl-toolbar-left">
            <h1 className="tpl-toolbar-heading">{t('toolbar.heading')}</h1>
            <span className="tpl-toolbar-count">
              {t('toolbar.count', { filtered: filteredTemplates.length, total: TEMPLATES.length })}
            </span>
          </div>
          <div className="tpl-toolbar-right">
            <label className="pdf-search">
              <FiSearch />
              <input
                type="text"
                placeholder={t('toolbar.searchPlaceholder')}
                value={searchQuery}
                onChange={e => setSearchQuery(e.target.value)}
              />
              {searchQuery && (
                <button className="pdf-search-clear" onClick={() => setSearchQuery('')} aria-label={t('toolbar.clearSearch')}>
                  <FiX size={14} />
                </button>
              )}
            </label>
            <select
              className="pdf-sort-select"
              value={selectedFormat}
              onChange={e => setSelectedFormat(e.target.value)}
              aria-label={t('toolbar.formatFilterLabel')}
            >
              <option value="all">{t('toolbar.format.all')}</option>
              <option value="portrait">{t('toolbar.format.portrait')}</option>
              <option value="landscape">{t('toolbar.format.landscape')}</option>
              <option value="square">{t('toolbar.format.square')}</option>
              <option value="widescreen">{t('toolbar.format.widescreen')}</option>
            </select>
            <select
              className="pdf-sort-select"
              value={sortOrder}
              onChange={e => setSortOrder(e.target.value as SortOrder)}
              aria-label={t('toolbar.sortLabel')}
            >
              <option value="default">{t('toolbar.sort.default')}</option>
              <option value="alpha">{t('toolbar.sort.alpha')}</option>
              <option value="category">{t('toolbar.sort.category')}</option>
            </select>
          </div>
        </section>

        {/* Category filter */}
        <div className="tpl-category-wrap">
          <CategoryFilter
            categories={translatedCategories}
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
              <h3>{t('emptyResults.title')}</h3>
              <p>{t('emptyResults.copy')}</p>
              <button className="pdf-outline-button" onClick={clearSearch}>
                {t('emptyResults.showAll')}
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
