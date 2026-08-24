import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDesignerAuth } from './DesignerAuthGate';
import { hasDesignerPermission, type DesignerPermission } from './designerPermissions';

const RequireDesignerPermission: React.FC<React.PropsWithChildren<{ permission: DesignerPermission }>> = ({
  permission,
  children,
}) => {
  const { user } = useDesignerAuth();
  const { t } = useTranslation('common');
  if (hasDesignerPermission(user, permission)) return <>{children}</>;

  return (
    <main className="designer-auth-state">
      <section>
        <p className="designer-auth-kicker">Power Dox Automation</p>
        <h1>{t('permissions.requiredTitle')}</h1>
        <p>{t('permissions.requiredMessage')}</p>
        <code>{permission}</code>
      </section>
    </main>
  );
};

export default RequireDesignerPermission;
