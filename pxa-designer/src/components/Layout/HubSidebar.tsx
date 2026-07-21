import React from 'react';
import { NavLink } from 'react-router-dom';
import { FiChevronLeft, FiChevronRight } from 'react-icons/fi';

export interface HubSidebarItem {
  path: string;
  label: string;
  disabled?: boolean;
}

interface HubSidebarProps {
  items: HubSidebarItem[];
  collapsed: boolean;
  onToggle: () => void;
}

const HubSidebar: React.FC<HubSidebarProps> = ({ items, collapsed, onToggle }) => {
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
          aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          aria-expanded={!collapsed}
        >
          {collapsed ? <FiChevronRight /> : <FiChevronLeft />}
        </button>
        <nav className="hub-sidebar-nav" aria-label="Section navigation">
          {items.map(item => (
            item.disabled ? (
              <span key={item.path} className="hub-sidebar-link is-disabled">
                {item.label}
                <small>Coming soon</small>
              </span>
            ) : (
              <NavLink
                key={item.path}
                to={item.path}
                className={({ isActive }) => `hub-sidebar-link${isActive ? ' is-active' : ''}`}
              >
                {item.label}
              </NavLink>
            )
          ))}
        </nav>
      </aside>
    </>
  );
};

export default HubSidebar;
