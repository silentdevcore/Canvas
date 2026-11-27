import React from 'react';
import { useTranslation } from 'react-i18next';
import { useDesignerAuth } from './DesignerAuthGate';
import { designerPermissionDenial, type DesignerPermission } from './designerPermissions';

const RequireDesignerPermission: React.FC<React.PropsWithChildren<{
  permission: DesignerPermission | readonly string[];
}>> = ({
  permission,
  children,
}) => {
  const { user } = useDesignerAuth();
  const { t } = useTranslation('common');
  const acceptedPermissions = typeof permission === 'string' ? [permission] : permission;
  const denial = designerPermissionDenial(user, acceptedPermissions);
  if (denial === null) return <>{children}</>;
  const prefix = denial === 'role' ? 'required' : denial;

  return (
    <main className="designer-auth-state">
      <section>
        <p className="designer-auth-kicker">PVADE</p>
        <h1>{t(`permissions.${prefix}Title`)}</h1>
        <p>{t(`permissions.${prefix}Message`)}</p>
        <code>{acceptedPermissions.join(' | ')}</code>
      </section>
    </main>
  );
};

export default RequireDesignerPermission;
