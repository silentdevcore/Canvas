import React from 'react';
import { Outlet } from 'react-router-dom';
import AppHeader from './AppHeader';
import HubSidebar, { type HubSidebarItem } from './HubSidebar';
import { useSidebarCollapsed } from '@/hooks/useSidebarCollapsed';

const PDF_SIDEBAR_ITEMS: HubSidebarItem[] = [
  { path: '/pdf/create', label: 'Create PDF' },
  { path: '/pdf/edit', label: 'Edit PDF' },
  { path: '/pdf/template', label: 'Use Template' },
  { path: '/pdf/import', label: 'Import PDF' },
  { path: '/pdf/convert', label: 'Convert to PDF' },
  { path: '/pdf/viewer', label: 'PDF Viewer' },
  { path: '/pdf/migrations', label: 'Migrations' },
];

const PdfLayout: React.FC = () => {
  const [collapsed, toggleCollapsed] = useSidebarCollapsed('pxa-designer:pdf-sidebar-collapsed');

  return (
    <div className="hub-layout">
      <AppHeader activePage="pdf" />
      <div className={`hub-layout-body${collapsed ? ' is-collapsed' : ''}`}>
        <HubSidebar items={PDF_SIDEBAR_ITEMS} collapsed={collapsed} onToggle={toggleCollapsed} />
        <main className="hub-layout-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
};

export default PdfLayout;
