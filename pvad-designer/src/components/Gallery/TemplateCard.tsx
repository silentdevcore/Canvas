import React from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'framer-motion';
import {
  FiChevronRight,
  FiEye,
} from 'react-icons/fi';
import { CATEGORY_CONFIG } from '@/data/templates';
import type { TemplateDefinition } from '@/data/templates';

type Template = TemplateDefinition;

interface TemplateCardProps {
  template: Template;
  onSelect: () => void;
}

const TemplateCard: React.FC<TemplateCardProps> = ({ template, onSelect }) => {
  const { t } = useTranslation(['gallery', 'templates']);
  const cfg = CATEGORY_CONFIG[template.category] ?? CATEGORY_CONFIG['letter'];
  const Icon = cfg.icon;

  return (
    <motion.article
      className="pdf-template-card"
      onClick={onSelect}
      whileHover={{ y: -4 }}
      whileTap={{ scale: 0.98 }}
    >
      <div className="pdf-template-preview" style={{ background: cfg.bg }}>
        <div className="pdf-document-miniature">
          <div style={{ background: cfg.accent }} />
          <span />
          <span />
          <Icon size={26} color={cfg.accent} style={{ position: 'absolute', right: 10, bottom: 10, opacity: 0.22 }} />
        </div>

        <div className="pdf-template-hover">
          <FiEye />
          <span>{t('card.useTemplateHover', { ns: 'gallery' })}</span>
        </div>
      </div>

      <div className="pdf-template-body">
        <div className="pdf-template-title-row">
          <Icon size={14} color={cfg.accent} />
          <h3>{t(`template.${template.id}.name`, { ns: 'templates', defaultValue: template.name })}</h3>
        </div>
        <p>{t(`template.${template.id}.description`, { ns: 'templates', defaultValue: template.description })}</p>

        <div className="pdf-template-tags">
          {template.tags.slice(0, 3).map(tag => (
            <span key={tag}>{tag}</span>
          ))}
        </div>

        <button
          className="pdf-template-action"
          onClick={(event) => {
            event.stopPropagation();
            onSelect();
          }}
        >
          {t('card.useTemplate', { ns: 'gallery' })}
          <FiChevronRight />
        </button>
      </div>
    </motion.article>
  );
};

export default TemplateCard;
