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

/** PDF Migration sub-tabs. */
export const pdfTabs = (active: 'code' | 'designer'): MigrationTab[] => [
  { label: 'Code Migration', to: '/migrations/pdf/code', active: active === 'code' },
  { label: 'UI-Designer Migration', to: '/migrations/pdf/designer', active: active === 'designer' },
];

/** Spreadsheet Migration sub-tabs. */
export const sheetTabs = (active: 'code' | 'datasource'): MigrationTab[] => [
  { label: 'Code Migration', to: '/migrations/spreadsheet/code', active: active === 'code' },
  { label: 'Datasource Migration', to: '/migrations/spreadsheet/datasource', active: active === 'datasource' },
];

export default MigrationTabs;
