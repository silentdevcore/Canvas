import React, { useEffect, useRef, useState } from 'react';
import {
  FiCheck,
  FiChevronDown,
  FiCreditCard,
  FiLogOut,
  FiFlag,
  FiSettings,
  FiShield,
  FiUser,
  FiUsers,
} from 'react-icons/fi';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import {
  accountPageUrl,
  DesignerAuthError,
  signOutDesigner,
  switchDesignerOrganization,
} from '@/auth/designerAuth';
import { clearDesignerTenantState } from '@/auth/designerTenantState';
import { useProductExperience } from '@/product/ProductExperienceProvider';
import { designerCommit, designerVersion } from '@/product/productMetadata';

interface DesignerUserMenuProps {
  mobile?: boolean;
  onNavigate?: () => void;
}

const accountLinks = [
  { label: 'Account', path: '/dashboard', icon: FiUser },
  { label: 'Subscription', path: '/subscription', icon: FiCreditCard },
  { label: 'Organization', path: '/organization', icon: FiUsers },
  { label: 'Security', path: '/security', icon: FiShield },
];

const DesignerUserMenu: React.FC<DesignerUserMenuProps> = ({ mobile = false, onNavigate }) => {
  const { user } = useDesignerAuth();
  const { openPanel } = useProductExperience();
  const [open, setOpen] = useState(false);
  const [switchingId, setSwitchingId] = useState<string | null>(null);
  const [error, setError] = useState('');
  const rootRef = useRef<HTMLDivElement>(null);
  const organizations = user.organizations ?? [];
  const activeOrganization = organizations.find(value => value.id === user.activeOrganizationId);
  const displayName = user.displayName || user.email || 'PXA user';
  const email = user.email || '';
  const initials = displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map(value => value[0]?.toUpperCase())
    .join('') || email[0]?.toUpperCase() || 'U';

  useEffect(() => {
    if (mobile || !open) return;
    const close = (event: MouseEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', close);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('mousedown', close);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [mobile, open]);

  const openAccountPage = (path: string) => {
    onNavigate?.();
    location.assign(accountPageUrl(path));
  };

  const switchOrganization = async (organizationId: string) => {
    if (organizationId === user.activeOrganizationId || switchingId) return;
    setError('');
    setSwitchingId(organizationId);
    try {
      await switchDesignerOrganization(organizationId);
      clearDesignerTenantState();
      location.reload();
    } catch (caught) {
      const authError = caught as DesignerAuthError;
      setError(authError.message || 'The organization could not be selected.');
      setSwitchingId(null);
    }
  };

  const signOut = async () => {
    if (switchingId) return;
    setError('');
    try {
      await signOutDesigner();
      clearDesignerTenantState();
      location.replace(accountPageUrl('/dashboard'));
    } catch (caught) {
      const authError = caught as DesignerAuthError;
      setError(authError.message || 'Sign out failed.');
    }
  };

  const content = (
    <>
      <div className="designer-user-summary">
        <span className="designer-user-avatar" aria-hidden="true">{initials}</span>
        <span>
          <strong>{displayName}</strong>
          <small>{email}</small>
        </span>
      </div>

      {organizations.length > 0 && (
        <div className="designer-user-section">
          <p>Workspace</p>
          {organizations.map(organization => {
            const active = organization.id === user.activeOrganizationId;
            return (
              <button
                key={organization.id}
                type="button"
                className={`designer-organization-option${active ? ' is-active' : ''}`}
                disabled={Boolean(switchingId)}
                onClick={() => void switchOrganization(organization.id)}
              >
                <span>
                  <strong>{organization.name}</strong>
                  <small>{organization.slug}</small>
                </span>
                {active && <FiCheck aria-label="Current workspace" />}
                {switchingId === organization.id && <span className="designer-menu-spinner" aria-label="Switching workspace" />}
              </button>
            );
          })}
        </div>
      )}

      <div className="designer-user-section">
        <button type="button" onClick={() => {
          setOpen(false);
          onNavigate?.();
          openPanel('releases');
        }}>
          <FiFlag aria-hidden="true" />
          What's New
        </button>
        <button type="button" onClick={() => {
          setOpen(false);
          onNavigate?.();
          openPanel('features');
        }}>
          <FiSettings aria-hidden="true" />
          Experimental features
        </button>
        {accountLinks.map(link => (
          <button key={link.path} type="button" onClick={() => openAccountPage(link.path)}>
            <link.icon aria-hidden="true" />
            {link.label}
          </button>
        ))}
        <button type="button" onClick={() => openAccountPage('/profile')}>
          <FiSettings aria-hidden="true" />
          Profile settings
        </button>
      </div>

      {error && <p className="designer-user-error" role="alert">{error}</p>}
      <div className="designer-version-info" title={`Commit ${designerCommit}`}>
        PXA {designerVersion}
      </div>
      <button className="designer-signout-button" type="button" onClick={() => void signOut()}>
        <FiLogOut aria-hidden="true" />
        Sign out of Designer
      </button>
    </>
  );

  if (mobile) return <div className="designer-mobile-user-menu">{content}</div>;

  return (
    <div className="designer-user-menu" ref={rootRef}>
      <button
        type="button"
        className="designer-user-trigger"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`Open user menu for ${displayName}`}
        onClick={() => setOpen(value => !value)}
      >
        <span className="designer-user-avatar" aria-hidden="true">{initials}</span>
        <span className="designer-user-trigger-copy">
          <strong>{displayName}</strong>
          <small>{activeOrganization?.name ?? 'No workspace'}</small>
        </span>
        <FiChevronDown aria-hidden="true" />
      </button>
      {open && <div className="designer-user-popover" role="menu">{content}</div>}
    </div>
  );
};

export default DesignerUserMenu;
