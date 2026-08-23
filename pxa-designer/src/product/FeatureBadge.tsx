import React from 'react';
import { useTranslation } from 'react-i18next';
import { isFeatureNew, type DesignerFeatureDefinition } from './productMetadata';

interface FeatureBadgeProps {
  feature: DesignerFeatureDefinition;
}

const FeatureBadge: React.FC<FeatureBadgeProps> = ({ feature }) => {
  const { t } = useTranslation('common');
  const isNew = isFeatureNew(feature);
  if (feature.maturity === 'stable' && !isNew) return null;
  const maturity = t(`productExperience.maturity.${feature.maturity}`);
  return (
    <span
      className="pxa-feature-badges"
      aria-label={t('productExperience.featureStatus', {
        status: maturity,
        newStatus: isNew ? `, ${t('productExperience.new')}` : '',
      })}
    >
      {isNew && <span className="pxa-feature-badge is-new">{t('productExperience.new')}</span>}
      {feature.maturity !== 'stable' && (
        <span className={`pxa-feature-badge is-${feature.maturity}`}>{maturity}</span>
      )}
    </span>
  );
};

export default FeatureBadge;
