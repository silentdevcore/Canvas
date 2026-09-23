import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDesignerAuth } from './DesignerAuthGate';
import { hasAnyDesignerPermission, type DesignerPermission } from './designerPermissions';

const RequireDesignerPermission: React.FC<React.PropsWithChildren<{
  permission: DesignerPermission | readonly string[];
}>> = ({
  permission,
  children,
}) => {
  const { user } = useDesignerAuth();
  const { t } = useTranslation('common');
  const acceptedPermissions = typeof permission === 'string' ? [permission] : permission;
  if (hasAnyDesignerPermission(user, acceptedPermissions)) return <>{children}</>;

  return (
    <main className="designer-auth-state">
      <section>
        <p className="designer-auth-kicker">Power Dox Automation</p>
        <h1>{t('permissions.requiredTitle')}</h1>
        <p>{t('permissions.requiredMessage')}</p>
        <code>{acceptedPermissions.join(' | ')}</code>
      </section>
    </main>
  );
};

export default RequireDesignerPermission;
