import React from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FiChevronLeft, FiChevronRight } from 'react-icons/fi';

export interface HubSidebarItem {
  path: string;
  label: string;
  icon: React.ElementType;
  disabled?: boolean;
  // Overrides the default pathname-only active check. Needed for items whose
  // `path` is really just a redirect alias into another route's query string
  // (e.g. /pdf/edit -> /pdf/create?mode=code) — NavLink's own matching only
  // looks at the pathname, so it can never tell those two apart on its own.
  isActive?: (location: { pathname: string; search: string }) => boolean;
}

interface HubSidebarProps {
  items: HubSidebarItem[];
  collapsed: boolean;
  onToggle: () => void;
}

const HubSidebar: React.FC<HubSidebarProps> = ({ items, collapsed, onToggle }) => {
  const { t } = useTranslation('common');
  const location = useLocation();
  return (
    <>
      <div
        className={`hub-sidebar-backdrop${collapsed ? '' : ' is-visible'}`}
        onClick={onToggle}
        aria-hidden="true"
      />
      <aside className={`hub-sidebar${collapsed ? ' is-collapsed' : ''}`}>
        <button
          className="hub-sidebar-toggle"
          onClick={onToggle}
          aria-label={collapsed ? t('sidebar.expand') : t('sidebar.collapse')}
          aria-expanded={!collapsed}
        >
          {collapsed ? <FiChevronRight /> : <FiChevronLeft />}
        </button>
        <nav className="hub-sidebar-nav" aria-label={t('sidebar.sectionNav')}>
          {items.map(item => {
            const Icon = item.icon;
            return item.disabled ? (
              <span key={item.path} className="hub-sidebar-link is-disabled" title={item.label}>
                <Icon className="hub-sidebar-link-icon" />
                <span className="hub-sidebar-link-label">
                  <span>{item.label}</span>
                  <small>{t('sidebar.comingSoon')}</small>
                </span>
              </span>
            ) : (
              <NavLink
                key={item.path}
                to={item.path}
                title={item.label}
                className={({ isActive }) => {
                  const active = item.isActive ? item.isActive(location) : isActive;
                  return `hub-sidebar-link${active ? ' is-active' : ''}`;
                }}
              >
                <Icon className="hub-sidebar-link-icon" />
                <span className="hub-sidebar-link-label">{item.label}</span>
              </NavLink>
            );
          })}
        </nav>
      </aside>
    </>
  );
};

export default HubSidebar;
