import React from 'react';
import { Outlet } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { FiGrid, FiCode, FiUpload, FiRefreshCw, FiGitMerge } from 'react-icons/fi';
import AppHeader from './AppHeader';
import HubSidebar, { type HubSidebarItem } from './HubSidebar';
import { useSidebarCollapsed } from '@/hooks/useSidebarCollapsed';
import FeatureBadge from '@/product/FeatureBadge';
import { useProductExperience } from '@/product/ProductExperienceProvider';
import { useDesignerAuth } from '@/auth/DesignerAuthGate';
import { designerPermissions, hasAnyDesignerPermission, hasDesignerPermission, migrationKindPermission } from '@/auth/designerPermissions';

// "Use Template" and "Viewer" are intentionally absent: neither a spreadsheet
// template gallery nor a standalone read-only spreadsheet viewer exists today
// (see checklists/PXA.Designer-Restructure.md). "Convert to Spreadsheet" ships
// disabled since no capability converts an external format into a spreadsheet
// the way image-to-PDF/OCR does for PDF.
const SpreadsheetLayout: React.FC = () => {
  const { t } = useTranslation('common');
  const [collapsed, toggleCollapsed] = useSidebarCollapsed('pxa-designer:spreadsheet-sidebar-collapsed');
  const { features } = useProductExperience();
  const { user } = useDesignerAuth();
  const permissionRequired = t('permissions.sidebarRequired');
  const spreadsheetFeature = features.find(feature => feature.id === 'designer.spreadsheet');

  const spreadsheetSidebarItems: HubSidebarItem[] = [
    {
      path: '/spreadsheet/create',
      label: t('spreadsheetSidebar.createSpreadsheet'),
      icon: FiGrid,
      badge: spreadsheetFeature ? <FeatureBadge feature={spreadsheetFeature} /> : undefined,
      disabled: (spreadsheetFeature ? !spreadsheetFeature.enabled : false)
        || !hasDesignerPermission(user, designerPermissions.spreadsheetCreate),
      disabledReason: !hasDesignerPermission(user, designerPermissions.spreadsheetCreate)
        ? permissionRequired
        : undefined,
    },
    { path: '/spreadsheet/edit', label: t('spreadsheetSidebar.editSpreadsheet'), icon: FiCode,
      disabled: !hasDesignerPermission(user, designerPermissions.spreadsheetEdit), disabledReason: permissionRequired },
    { path: '/spreadsheet/import', label: t('spreadsheetSidebar.importSpreadsheet'), icon: FiUpload,
      disabled: !hasDesignerPermission(user, designerPermissions.spreadsheetImport), disabledReason: permissionRequired },
    { path: '/spreadsheet/convert', label: t('spreadsheetSidebar.convertToSpreadsheet'), icon: FiRefreshCw, disabled: true },
    { path: '/spreadsheet/migrations', label: t('spreadsheetSidebar.migrations'), icon: FiGitMerge,
      disabled: !hasAnyDesignerPermission(user, [
        designerPermissions.migrationSpreadsheetExecute,
        migrationKindPermission('spreadsheet', 'code', 'execute'),
      ]), disabledReason: permissionRequired },
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
