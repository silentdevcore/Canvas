import React from 'react';
import { useNavigate } from 'react-router-dom';

export interface MigrationTab {
  label: string;
  /** Route to navigate to when clicked (for cross-page sub-tabs). */
  to?: string;
  /** Click handler for in-page sub-tabs (used instead of `to`). */
  onClick?: () => void;
  active?: boolean;
}

/** A horizontal sub-tab bar shared by the migration type views (Code: PDF | Spreadsheet;
 *  Format: Report designers | Documents | Spreadsheets). */
const MigrationTabs: React.FC<{ tabs: MigrationTab[] }> = ({ tabs }) => {
  const navigate = useNavigate();
  return (
    <div className="mgr-subtabs" role="tablist">
      {tabs.map((t) => (
        <button
          key={t.label}
          type="button"
          role="tab"
          aria-selected={!!t.active}
          className={`mgr-subtab${t.active ? ' is-active' : ''}`}
          onClick={() => (t.to ? navigate(t.to) : t.onClick?.())}
        >
          {t.label}
        </button>
      ))}
    </div>
  );
};

/** The DataSource/Format Migration sub-tabs, with the given one marked active. */
export const formatTabs = (active: 'designer' | 'documents' | 'spreadsheet'): MigrationTab[] => [
  { label: 'Report designers', to: '/migrations/format/designer', active: active === 'designer' },
  { label: 'Documents', to: '/migrations/format/documents', active: active === 'documents' },
  { label: 'Spreadsheets', to: '/migrations/format/spreadsheet', active: active === 'spreadsheet' },
];

export default MigrationTabs;
