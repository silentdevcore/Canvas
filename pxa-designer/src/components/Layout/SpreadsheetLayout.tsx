import React from 'react';
import { Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FiGrid, FiCode, FiUpload, FiRefreshCw, FiGitMerge } from 'react-icons/fi';
import AppHeader from './AppHeader';
import HubSidebar, { type HubSidebarItem } from './HubSidebar';
import { useSidebarCollapsed } from '@/hooks/useSidebarCollapsed';

// "Use Template" and "Viewer" are intentionally absent: neither a spreadsheet
// template gallery nor a standalone read-only spreadsheet viewer exists today
// (see checklists/PXA.Designer-Restructure.md). "Convert to Spreadsheet" ships
// disabled since no capability converts an external format into a spreadsheet
// the way image-to-PDF/OCR does for PDF.
const SpreadsheetLayout: React.FC = () => {
  const { t } = useTranslation('common');
  const [collapsed, toggleCollapsed] = useSidebarCollapsed('pxa-designer:spreadsheet-sidebar-collapsed');

  const spreadsheetSidebarItems: HubSidebarItem[] = [
    { path: '/spreadsheet/create', label: t('spreadsheetSidebar.createSpreadsheet'), icon: FiGrid },
    { path: '/spreadsheet/edit', label: t('spreadsheetSidebar.editSpreadsheet'), icon: FiCode },
    { path: '/spreadsheet/import', label: t('spreadsheetSidebar.importSpreadsheet'), icon: FiUpload },
    { path: '/spreadsheet/convert', label: t('spreadsheetSidebar.convertToSpreadsheet'), icon: FiRefreshCw, disabled: true },
    { path: '/spreadsheet/migrations', label: t('spreadsheetSidebar.migrations'), icon: FiGitMerge },
  ];

  return (
    <div className="hub-layout">
      <AppHeader activePage="spreadsheet" />
      <div className={`hub-layout-body${collapsed ? ' is-collapsed' : ''}`}>
        <HubSidebar items={spreadsheetSidebarItems} collapsed={collapsed} onToggle={toggleCollapsed} />
        <main className="hub-layout-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default SpreadsheetLayout;
