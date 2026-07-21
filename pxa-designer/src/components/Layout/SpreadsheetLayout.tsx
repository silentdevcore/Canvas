import React from 'react';
import { Outlet } from 'react-router-dom';
import AppHeader from './AppHeader';
import HubSidebar, { type HubSidebarItem } from './HubSidebar';
import { useSidebarCollapsed } from '@/hooks/useSidebarCollapsed';

// "Use Template" and "Viewer" are intentionally absent: neither a spreadsheet
// template gallery nor a standalone read-only spreadsheet viewer exists today
// (see checklists/PXA.Designer-Restructure.md). "Convert to Spreadsheet" ships
// disabled since no capability converts an external format into a spreadsheet
// the way image-to-PDF/OCR does for PDF.
const SPREADSHEET_SIDEBAR_ITEMS: HubSidebarItem[] = [
  { path: '/spreadsheet/create', label: 'Create Spreadsheet' },
  { path: '/spreadsheet/edit', label: 'Edit Spreadsheet' },
  { path: '/spreadsheet/import', label: 'Import Spreadsheet' },
  { path: '/spreadsheet/convert', label: 'Convert to Spreadsheet', disabled: true },
  { path: '/spreadsheet/migrations', label: 'Migrations' },
];

const SpreadsheetLayout: React.FC = () => {
  const [collapsed, toggleCollapsed] = useSidebarCollapsed('pxa-designer:spreadsheet-sidebar-collapsed');

  return (
    <div className="hub-layout">
      <AppHeader activePage="spreadsheet" />
      <div className={`hub-layout-body${collapsed ? ' is-collapsed' : ''}`}>
        <HubSidebar items={SPREADSHEET_SIDEBAR_ITEMS} collapsed={collapsed} onToggle={toggleCollapsed} />
        <main className="hub-layout-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default SpreadsheetLayout;
