import React from 'react';
import { isFeatureNew, type DesignerFeatureDefinition } from './productMetadata';

interface FeatureBadgeProps {
  feature: DesignerFeatureDefinition;
}

const FeatureBadge: React.FC<FeatureBadgeProps> = ({ feature }) => {
  const isNew = isFeatureNew(feature);
  if (feature.maturity === 'stable' && !isNew) return null;
  return (
    <span className="pxa-feature-badges" aria-label={`Feature status: ${feature.maturity}${isNew ? ', new' : ''}`}>
      {isNew && <span className="pxa-feature-badge is-new">New</span>}
      {feature.maturity !== 'stable' && (
        <span className={`pxa-feature-badge is-${feature.maturity}`}>{feature.maturity}</span>
      )}
    </span>
  );
};

export default FeatureBadge;
